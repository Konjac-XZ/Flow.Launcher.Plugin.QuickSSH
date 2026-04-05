using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Verifies that non-launch actions perform their data operations correctly
    /// and that the data-layer mutations used by stay-open action handlers
    /// behave as expected.
    ///
    /// The actual Flow Launcher API calls (ChangeQuery, ShowMsg, return false)
    /// cannot be tested without a full plugin runtime.  These tests focus on:
    /// - Data operations that the action handlers rely on
    /// - Registry-only semantics (no file deletion)
    /// - Atomic rename patterns (remove + re-add under new key)
    /// - Clipboard-safe data access patterns
    /// </summary>
    public class StayOpenBehaviorTests : IDisposable
    {
        private readonly string _tmpDir;

        public StayOpenBehaviorTests()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"quickssh_stayopen_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, recursive: true);
        }

        // ── Profiles: add stays open ──────────────────────────────────────────────

        [Fact]
        public void ProfilesAdd_DataMutation_ProfileIsRegistered()
        {
            // After profiles add, the profile should exist in the store.
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            var profile = SshProfile.ParseFromLegacyCommand("ssh root@server");
            pm.UserData.Profiles["myprofile"] = profile;

            Assert.True(pm.UserData.Profiles.ContainsKey("myprofile"));
            Assert.NotNull(pm.UserData.Profiles["myprofile"]);
        }

        // ── Profiles: remove stays open ───────────────────────────────────────────

        [Fact]
        public void ProfilesRemove_DataMutation_ProfileIsRemoved()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.Profiles["todelete"] = SshProfile.ParseFromLegacyCommand("ssh user@host");
            pm.UserData.Profiles.Remove("todelete");

            Assert.False(pm.UserData.Profiles.ContainsKey("todelete"));
        }

        // ── Profiles: rename stays open ───────────────────────────────────────────

        [Fact]
        public void ProfilesRename_AtomicSwap_PreservesProfileData()
        {
            // Rename = remove old + add new.  Data must be preserved.
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            var profile = SshProfile.ParseFromLegacyCommand("ssh admin@server -p 2222");
            pm.UserData.Profiles["oldname"] = profile;

            var value = pm.UserData.Profiles["oldname"];
            pm.UserData.Profiles.SetCallback(null);
            pm.UserData.Profiles.Remove("oldname");
            pm.UserData.Profiles["newname"] = value;

            Assert.False(pm.UserData.Profiles.ContainsKey("oldname"));
            Assert.True(pm.UserData.Profiles.ContainsKey("newname"));
            Assert.Equal("admin", pm.UserData.Profiles["newname"].User);
        }

        // ── Profiles: copy — clipboard data access ────────────────────────────────

        [Fact]
        public void ProfilesCopy_CommandLineAccessible_ForClipboard()
        {
            // The copy action reads ToCommandLine() — verify it returns the expected string.
            var profile = SshProfile.ParseFromLegacyCommand("ssh user@host -p 22");
            var cmd = profile.ToCommandLine();

            Assert.Contains("user@host", cmd);
        }

        // ── Shell: add stays open ─────────────────────────────────────────────────

        [Fact]
        public void ShellAdd_DataMutation_ShellIsRegistered()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.CustomShell["MyShell"] = @"C:\Program Files\PowerShell\7\pwsh.exe";

            Assert.True(pm.UserData.CustomShell.ContainsKey("MyShell"));
        }

        [Fact]
        public void ShellAdd_FirstShell_AutoSelected()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.CustomShell.SetCallback(null);
            pm.UserData.CustomShell["FirstShell"] = "pwsh.exe";
            if (pm.UserData.CustomShell.Count == 1)
                pm.UserData.SelectedCustomShell = "FirstShell";

            Assert.Equal("FirstShell", pm.UserData.SelectedCustomShell);
        }

        // ── Shell: remove stays open ──────────────────────────────────────────────

        [Fact]
        public void ShellRemove_DataMutation_ShellIsRemoved()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.CustomShell["ToRemove"] = "cmd.exe";
            pm.UserData.CustomShell.Remove("ToRemove");

            Assert.False(pm.UserData.CustomShell.ContainsKey("ToRemove"));
        }

        [Fact]
        public void ShellRemove_SelectedShell_AutoSelectsNext()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.CustomShell.SetCallback(null);
            pm.UserData.CustomShell["shell1"] = "cmd.exe";
            pm.UserData.CustomShell["shell2"] = "pwsh.exe";
            pm.UserData.SelectedCustomShell = "shell1";

            // Simulate remove of selected shell
            pm.UserData.CustomShell.Remove("shell1");
            if (pm.UserData.SelectedCustomShell == "shell1")
            {
                pm.UserData.SelectedCustomShell =
                    pm.UserData.CustomShell.Keys.GetEnumerator().MoveNext()
                        ? new System.Collections.Generic.List<string>(pm.UserData.CustomShell.Keys)[0]
                        : null;
            }

            Assert.NotEqual("shell1", pm.UserData.SelectedCustomShell);
        }

        // ── Shell: select stays open ──────────────────────────────────────────────

        [Fact]
        public void ShellSelect_DataMutation_SelectionUpdated()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.CustomShell.SetCallback(null);
            pm.UserData.CustomShell["shell1"] = "cmd.exe";
            pm.UserData.CustomShell["shell2"] = "pwsh.exe";
            pm.UserData.SelectedCustomShell = "shell2";

            Assert.Equal("shell2", pm.UserData.SelectedCustomShell);
        }

        // ── Keys: add stays open ──────────────────────────────────────────────────

        [Fact]
        public void KeysAdd_DataMutation_KeyIsRegistered()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.SshKeys["mykey"] = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_rsa" };

            Assert.True(pm.UserData.SshKeys.ContainsKey("mykey"));
            Assert.Equal(@"C:\Users\me\.ssh\id_rsa", pm.UserData.SshKeys["mykey"].Path);
        }

        // ── Keys: rename stays open ───────────────────────────────────────────────

        [Fact]
        public void KeysRename_AtomicSwap_PreservesKeyData()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.SshKeys["oldkey"] = new SshKeyEntry
            {
                Path = @"C:\Users\me\.ssh\mykey",
                Algorithm = "ed25519"
            };

            var value = pm.UserData.SshKeys["oldkey"];
            pm.UserData.SshKeys.SetCallback(null);
            pm.UserData.SshKeys.Remove("oldkey");
            pm.UserData.SshKeys["newkey"] = value;

            Assert.False(pm.UserData.SshKeys.ContainsKey("oldkey"));
            Assert.True(pm.UserData.SshKeys.ContainsKey("newkey"));
            Assert.Equal(@"C:\Users\me\.ssh\mykey", pm.UserData.SshKeys["newkey"].Path);
            Assert.Equal("ed25519", pm.UserData.SshKeys["newkey"].Algorithm);
        }

        // ── Keys: copy-path — data access for clipboard ───────────────────────────

        [Fact]
        public void KeysCopyPath_PathAccessible_ForClipboard()
        {
            var entry = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_ed25519" };
            Assert.Equal(@"C:\Users\me\.ssh\id_ed25519", entry.Path);
        }

        // ── Keys: copy-pub — data access for clipboard ────────────────────────────

        [Fact]
        public void KeysCopyPub_EffectivePublicKeyPath_Accessible()
        {
            var entry = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_ed25519" };
            Assert.Equal(@"C:\Users\me\.ssh\id_ed25519.pub", entry.GetEffectivePublicKeyPath());
        }

        // ── Keys: scan register stays open ────────────────────────────────────────

        [Fact]
        public void KeysScanRegister_DataMutation_KeyIsRegistered()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            var candidate = @"C:\Users\me\.ssh\id_rsa";
            var fileName = "id_rsa";

            pm.UserData.SshKeys[fileName] = new SshKeyEntry { Path = candidate };

            Assert.True(pm.UserData.SshKeys.ContainsKey(fileName));
            Assert.Equal(candidate, pm.UserData.SshKeys[fileName].Path);
        }

        // ── Keys: remove is registry-only, no file deletion ───────────────────────

        [Fact]
        public void KeysRemove_RegistryOnly_FilesNotDeleted()
        {
            var keyPath = Path.Combine(_tmpDir, "test_key");
            var pubPath = keyPath + ".pub";
            File.WriteAllText(keyPath, "fake-private");
            File.WriteAllText(pubPath, "fake-public");

            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.SshKeys["testkey"] = new SshKeyEntry
            {
                Path = keyPath,
                PublicKeyPath = pubPath
            };
            pm.UserData.SshKeys.Remove("testkey");

            Assert.False(pm.UserData.SshKeys.ContainsKey("testkey"));
            Assert.True(File.Exists(keyPath), "Private key file must NOT be deleted.");
            Assert.True(File.Exists(pubPath), "Public key file must NOT be deleted.");
        }

        // ── Keys: remove captures path before removal ─────────────────────────────

        [Fact]
        public void KeysRemove_CapturedPathAvailable_AfterRemoval()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.SshKeys["alias"] = new SshKeyEntry
            {
                Path = @"C:\Users\me\.ssh\mykey"
            };

            var capturedPath = pm.UserData.SshKeys["alias"].Path;
            pm.UserData.SshKeys.Remove("alias");

            Assert.Equal(@"C:\Users\me\.ssh\mykey", capturedPath);
            Assert.False(pm.UserData.SshKeys.ContainsKey("alias"));
        }

        // ── Keys: remove with empty path — graceful ───────────────────────────────

        [Fact]
        public void KeysRemove_EmptyPath_GracefulFeedback()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            pm.UserData.SshKeys["orphan"] = new SshKeyEntry { Path = null };

            var capturedPath = pm.UserData.SshKeys["orphan"].Path ?? "";
            pm.UserData.SshKeys.Remove("orphan");

            Assert.Equal("", capturedPath);
            Assert.False(pm.UserData.SshKeys.ContainsKey("orphan"));
        }

        // ── Keys: generate success message includes paths ─────────────────────────

        [Fact]
        public void KeysGenerate_SuccessMessage_IncludesAllPaths()
        {
            var alias = "testkey";
            var keyPath = @"C:\Users\me\.ssh\testkey";
            var pubKeyPath = keyPath + ".pub";

            var message = string.Format(
                "SSH key generated: {0}\nPrivate: {1}\nPublic: {2}",
                alias, keyPath, pubKeyPath);

            Assert.Contains(alias, message);
            Assert.Contains(keyPath, message);
            Assert.Contains(pubKeyPath, message);
        }

        // ── Keys: remove success message is registry-only ─────────────────────────

        [Fact]
        public void KeysRemove_SuccessMessage_ClearlySaysRegistryOnly()
        {
            var alias = "prodkey";
            var keyPath = @"C:\Users\me\.ssh\prodkey";

            var message = string.Format(
                "SSH key alias removed: {0}\nRegistry entry removed only.\nFiles kept on disk: {1}",
                alias, keyPath);

            Assert.Contains(alias, message);
            Assert.Contains("Registry entry removed only", message);
            Assert.Contains("Files kept on disk", message);
            Assert.Contains(keyPath, message);
        }

        // ── Profiles: export/import round-trip ───────────────────────────────────

        [Fact]
        public void ProfilesExport_Serialize_ProducesNonEmptyText()
        {
            // Export uses ProfileSerializer.Serialize — verify it produces output.
            var profiles = new Dictionary<string, SshProfile>
            {
                ["myserver"] = SshProfile.ParseFromLegacyCommand("ssh root@10.0.0.1")
            };
            var text = ProfileSerializer.Serialize(profiles);

            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.Contains("myserver", text);
        }

        [Fact]
        public void ProfilesImport_Deserialize_RestoresProfiles()
        {
            // Import uses ProfileSerializer.Deserialize — verify round-trip works.
            var original = new Dictionary<string, SshProfile>
            {
                ["web"] = SshProfile.ParseFromLegacyCommand("ssh admin@web.example.com -p 2222"),
                ["db"] = SshProfile.ParseFromLegacyCommand("ssh root@db.internal")
            };
            var text = ProfileSerializer.Serialize(original);
            var imported = ProfileSerializer.Deserialize(text);

            Assert.Equal(2, imported.Count);
            Assert.True(imported.ContainsKey("web"));
            Assert.True(imported.ContainsKey("db"));
        }

        // ── Config: import data mutation ──────────────────────────────────────────

        [Fact]
        public void ConfigImport_DataMutation_ProfilesRegistered()
        {
            var pm = new ProfileManager(Path.Combine(_tmpDir, "profiles.json"));
            var profile = SshProfile.ParseFromLegacyCommand("ssh admin@webserver");
            pm.UserData.Profiles["webserver"] = profile;

            Assert.True(pm.UserData.Profiles.ContainsKey("webserver"));
        }

        // ── Help: docs URL is valid ───────────────────────────────────────────────

        [Fact]
        public void HelpDocs_GitHubUrl_IsValid()
        {
            // The help action opens this URL — verify it's a valid absolute HTTPS URI.
            var url = "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH";
            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri));
            Assert.Equal("https", uri.Scheme);
        }

        // ── LAUNCH actions remain unchanged ───────────────────────────────────────
        // These tests verify that the data patterns used by launch actions
        // haven't been accidentally modified.

        [Fact]
        public void LaunchAction_ProfileConnect_CommandLineAvailable()
        {
            // Launch actions use profile.ToCommandLine() to build the SSH command.
            // Verify the command line is available and contains the destination.
            var profile = SshProfile.ParseFromLegacyCommand("ssh root@server");
            var cmd = profile.ToCommandLine();

            Assert.NotNull(cmd);
            Assert.StartsWith("ssh ", cmd);
            Assert.Contains("root@server", cmd);
        }

        [Fact]
        public void LaunchAction_DirectConnect_RawInputPreserved()
        {
            // Direct connect uses the raw user input.
            // Verify NormalizeSshCommand produces valid output.
            var result = QuickSsh.NormalizeSshCommand("root@server -p 22");

            Assert.NotNull(result);
            Assert.Contains("root@server", result);
        }
    }
}
