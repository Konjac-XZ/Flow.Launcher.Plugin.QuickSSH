using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    public class SshKeysTests
    {
        // ── SshKeyEntry ───────────────────────────────────────────────────────────

        [Fact]
        public void SshKeyEntry_ToDisplayString_PathOnly()
        {
            var entry = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_rsa" };
            Assert.Equal(@"C:\Users\me\.ssh\id_rsa", entry.ToDisplayString());
        }

        [Fact]
        public void SshKeyEntry_ToDisplayString_WithDescription()
        {
            var entry = new SshKeyEntry
            {
                Path = @"C:\Users\me\.ssh\id_rsa",
                Description = "Production key"
            };
            Assert.Equal(@"Production key — C:\Users\me\.ssh\id_rsa", entry.ToDisplayString());
        }

        [Fact]
        public void SshKeyEntry_ToDisplayString_EmptyPath()
        {
            var entry = new SshKeyEntry();
            Assert.Equal("", entry.ToDisplayString());
        }

        [Fact]
        public void SshKeyEntry_GetEffectivePublicKeyPath_Default()
        {
            var entry = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_rsa" };
            Assert.Equal(@"C:\Users\me\.ssh\id_rsa.pub", entry.GetEffectivePublicKeyPath());
        }

        [Fact]
        public void SshKeyEntry_GetEffectivePublicKeyPath_Explicit()
        {
            var entry = new SshKeyEntry
            {
                Path = @"C:\Users\me\.ssh\id_rsa",
                PublicKeyPath = @"C:\Users\me\.ssh\custom.pub"
            };
            Assert.Equal(@"C:\Users\me\.ssh\custom.pub", entry.GetEffectivePublicKeyPath());
        }

        [Fact]
        public void SshKeyEntry_GetEffectivePublicKeyPath_NullPath()
        {
            var entry = new SshKeyEntry();
            Assert.Null(entry.GetEffectivePublicKeyPath());
        }

        // ── Storage backward compatibility ────────────────────────────────────────
        // New fields (PublicKeyPath, Fingerprint, Comment) are nullable and use
        // NullValueHandling.Ignore — old JSON without these fields must deserialize cleanly.

        [Fact]
        public void SshKeyEntry_Deserialize_OldFormat_NoNewFields()
        {
            var json = @"{ ""Path"": ""C:\\Users\\me\\.ssh\\id_rsa"", ""Description"": ""my key"" }";
            var entry = Newtonsoft.Json.JsonConvert.DeserializeObject<SshKeyEntry>(json);

            Assert.NotNull(entry);
            Assert.Equal(@"C:\Users\me\.ssh\id_rsa", entry.Path);
            Assert.Equal("my key", entry.Description);
            Assert.Null(entry.PublicKeyPath);
            Assert.Null(entry.Fingerprint);
            Assert.Null(entry.Comment);
        }

        [Fact]
        public void SshKeyEntry_Deserialize_NewFormat_AllFields()
        {
            var json = @"{
                ""Path"": ""C:\\Users\\me\\.ssh\\id_ed25519"",
                ""PublicKeyPath"": ""C:\\Users\\me\\.ssh\\id_ed25519.pub"",
                ""Fingerprint"": ""SHA256:abc123"",
                ""Comment"": ""user@host"",
                ""Description"": ""test""
            }";
            var entry = Newtonsoft.Json.JsonConvert.DeserializeObject<SshKeyEntry>(json);

            Assert.Equal(@"C:\Users\me\.ssh\id_ed25519", entry.Path);
            Assert.Equal(@"C:\Users\me\.ssh\id_ed25519.pub", entry.PublicKeyPath);
            Assert.Equal("SHA256:abc123", entry.Fingerprint);
            Assert.Equal("user@host", entry.Comment);
            Assert.Equal("test", entry.Description);
        }

        [Fact]
        public void SshKeyEntry_Serialize_NullFieldsOmitted()
        {
            var entry = new SshKeyEntry { Path = @"C:\path\key" };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(entry);

            Assert.Contains("Path", json);
            Assert.DoesNotContain("PublicKeyPath", json);
            Assert.DoesNotContain("Fingerprint", json);
            Assert.DoesNotContain("Comment", json);
            Assert.DoesNotContain("Description", json);
        }

        // ── UserData key registry ─────────────────────────────────────────────────

        [Fact]
        public void UserData_SshKeys_InitializedEmpty()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            Assert.NotNull(userData.SshKeys);
            Assert.Empty(userData.SshKeys);
        }

        [Fact]
        public void UserData_SshKeys_AddAndRetrieve()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.SshKeys["mykey"] = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_rsa" };

            Assert.True(userData.SshKeys.ContainsKey("mykey"));
            Assert.Equal(@"C:\Users\me\.ssh\id_rsa", userData.SshKeys["mykey"].Path);
        }

        [Fact]
        public void UserData_SshKeys_Remove()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.SshKeys["mykey"] = new SshKeyEntry { Path = @"C:\Users\me\.ssh\id_rsa" };
            userData.SshKeys.Remove("mykey");

            Assert.False(userData.SshKeys.ContainsKey("mykey"));
        }

        [Fact]
        public void UserData_SshKeys_AutoSaveCallback()
        {
            int saveCount = 0;
            var userData = new UserData();
            userData.Attach(() => saveCount++);
            userData.SshKeys["mykey"] = new SshKeyEntry { Path = @"C:\path\key" };

            Assert.True(saveCount > 0, "Save callback should fire on SshKeys mutation.");
        }

        [Fact]
        public void UserData_SshKeys_RenamePreservesValue()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.SshKeys["old"] = new SshKeyEntry
            {
                Path = @"C:\Users\me\.ssh\id_rsa",
                Description = "test"
            };

            var value = userData.SshKeys["old"];
            userData.SshKeys.SetCallback(null);
            userData.SshKeys.Remove("old");
            userData.SshKeys["new"] = value;

            Assert.False(userData.SshKeys.ContainsKey("old"));
            Assert.True(userData.SshKeys.ContainsKey("new"));
            Assert.Equal(@"C:\Users\me\.ssh\id_rsa", userData.SshKeys["new"].Path);
            Assert.Equal("test", userData.SshKeys["new"].Description);
        }

        // ── AutoCompleter keys suggestions ────────────────────────────────────────

        [Fact]
        public void GetSuggestions_PartialKe_ReturnsKeys()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "ke", null, "icon.png");
            Assert.Contains(results, r => r.Title == "keys");
        }

        [Fact]
        public void GetSuggestions_KeysSpace_SuggestsAllSubCommands()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "keys ", null, "icon.png");
            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("add", titles);
            Assert.Contains("remove", titles);
            Assert.Contains("rename", titles);
            Assert.Contains("copy-path", titles);
            Assert.Contains("copy-pub", titles);
            Assert.Contains("scan", titles);
        }

        [Theory]
        [InlineData("a",      new[] { "add" })]
        [InlineData("ad",     new[] { "add" })]
        [InlineData("add",    new[] { "add" })]
        [InlineData("r",      new[] { "remove", "rename" })]
        [InlineData("re",     new[] { "remove", "rename" })]
        [InlineData("rem",    new[] { "remove" })]
        [InlineData("ren",    new[] { "rename" })]
        [InlineData("c",      new[] { "copy-path", "copy-pub" })]
        [InlineData("co",     new[] { "copy-path", "copy-pub" })]
        [InlineData("copy-pa", new[] { "copy-path" })]
        [InlineData("copy-pu", new[] { "copy-pub" })]
        [InlineData("s",      new[] { "scan" })]
        [InlineData("sc",     new[] { "scan" })]
        public void GetSuggestions_KeysPartialPrefix_ShowsMatchingSubCommands(
            string partial, string[] expected)
        {
            var results = AutoCompleter.GetSuggestions("ssh", "keys " + partial, null, "icon.png");
            var allSubCmds = new HashSet<string> { "add", "remove", "rename", "copy-path", "copy-pub", "scan" };
            var subCommandTitles = results
                .Select(r => r.Title)
                .Where(t => allSubCmds.Contains(t))
                .ToHashSet();

            foreach (var e in expected)
                Assert.Contains(e, subCommandTitles);
            Assert.Equal(expected.Length, subCommandTitles.Count);
        }

        [Fact]
        public void GetSuggestions_KeysPrefix_SuggestsKeyAliases()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.SshKeys["prod"] = new SshKeyEntry { Path = @"C:\Users\me\.ssh\prod_key" };
            userData.SshKeys["dev"]  = new SshKeyEntry { Path = @"C:\Users\me\.ssh\dev_key" };

            var results = AutoCompleter.GetSuggestions("ssh", "keys ", userData, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("prod", titles);
            Assert.Contains("dev", titles);
        }

        [Fact]
        public void GetSuggestions_KeysPrefixWithSearch_FiltersAliases()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.SshKeys["prod"] = new SshKeyEntry { Path = @"C:\Users\me\.ssh\prod_key" };
            userData.SshKeys["dev"]  = new SshKeyEntry { Path = @"C:\Users\me\.ssh\dev_key" };

            var results = AutoCompleter.GetSuggestions("ssh", "keys pro", userData, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("prod", titles);
            Assert.DoesNotContain("dev", titles);
        }

        // ── Keys submenu score invariants ─────────────────────────────────────────

        [Fact]
        public void KeysSubmenu_ManagementRowIsAboveAllActionRows()
        {
            Assert.True(QuickSsh.ScoreSubMenuManagement > QuickSsh.ScoreKeysActionAdd,
                "Management row must outrank every keys action row.");
        }

        [Fact]
        public void KeysSubmenu_AllActionRowsAreAboveSavedItems()
        {
            Assert.True(QuickSsh.ScoreKeysActionScan > QuickSsh.ScoreKeysSavedItem,
                "The scan action row (lowest action score) must appear above saved keys.");
        }

        [Fact]
        public void KeysSubmenu_ActionRowScoresAreInDescendingOrder()
        {
            // add > remove > rename > copy-path > copy-pub > scan
            Assert.True(QuickSsh.ScoreKeysActionAdd      > QuickSsh.ScoreKeysActionRemove);
            Assert.True(QuickSsh.ScoreKeysActionRemove   > QuickSsh.ScoreKeysActionRename);
            Assert.True(QuickSsh.ScoreKeysActionRename   > QuickSsh.ScoreKeysActionCopyPath);
            Assert.True(QuickSsh.ScoreKeysActionCopyPath > QuickSsh.ScoreKeysActionCopyPub);
            Assert.True(QuickSsh.ScoreKeysActionCopyPub  > QuickSsh.ScoreKeysActionScan);
        }

        [Fact]
        public void KeysSubmenu_ActionRowScoresAreSafeAboveSavedItemBase()
        {
            int gap = QuickSsh.ScoreKeysActionScan - QuickSsh.ScoreKeysSavedItem;
            Assert.True(gap > 500,
                $"Scan action score must exceed saved item base by > 500 (actual gap: {gap}).");
        }

        // ── ScanSshDirectory filtering ────────────────────────────────────────────

        [Fact]
        public void ScanSshDirectory_FiltersOutPubFiles()
        {
            var dir = Path.Combine(Path.GetTempPath(), "quickssh_test_scan_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "id_rsa"), "private");
                File.WriteAllText(Path.Combine(dir, "id_rsa.pub"), "public");
                File.WriteAllText(Path.Combine(dir, "id_ed25519"), "private");
                File.WriteAllText(Path.Combine(dir, "id_ed25519.pub"), "public");

                var results = QuickSsh.ScanSshDirectory(dir);

                var names = results.Select(Path.GetFileName).ToHashSet();
                Assert.Contains("id_rsa", names);
                Assert.Contains("id_ed25519", names);
                Assert.DoesNotContain("id_rsa.pub", names);
                Assert.DoesNotContain("id_ed25519.pub", names);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void ScanSshDirectory_FiltersOutKnownNonKeyFiles()
        {
            var dir = Path.Combine(Path.GetTempPath(), "quickssh_test_scan_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "id_rsa"), "private");
                File.WriteAllText(Path.Combine(dir, "known_hosts"), "hosts");
                File.WriteAllText(Path.Combine(dir, "known_hosts.old"), "old hosts");
                File.WriteAllText(Path.Combine(dir, "config"), "ssh config");
                File.WriteAllText(Path.Combine(dir, "authorized_keys"), "authorized");
                File.WriteAllText(Path.Combine(dir, "authorized_keys2"), "authorized2");
                File.WriteAllText(Path.Combine(dir, "environment"), "env");
                File.WriteAllText(Path.Combine(dir, "profiles.json"), "{}");

                var results = QuickSsh.ScanSshDirectory(dir);

                var names = results.Select(Path.GetFileName).ToHashSet();
                Assert.Contains("id_rsa", names);
                Assert.DoesNotContain("known_hosts", names);
                Assert.DoesNotContain("known_hosts.old", names);
                Assert.DoesNotContain("config", names);
                Assert.DoesNotContain("authorized_keys", names);
                Assert.DoesNotContain("authorized_keys2", names);
                Assert.DoesNotContain("environment", names);
                Assert.DoesNotContain("profiles.json", names);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void ScanSshDirectory_FiltersOutLogBakTmpOldJsonExtensions()
        {
            var dir = Path.Combine(Path.GetTempPath(), "quickssh_test_scan_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "id_rsa"), "private");
                File.WriteAllText(Path.Combine(dir, "something.log"), "log");
                File.WriteAllText(Path.Combine(dir, "backup.bak"), "bak");
                File.WriteAllText(Path.Combine(dir, "temp.tmp"), "tmp");
                File.WriteAllText(Path.Combine(dir, "file.old"), "old");
                File.WriteAllText(Path.Combine(dir, "data.json"), "{}");

                var results = QuickSsh.ScanSshDirectory(dir);

                var names = results.Select(Path.GetFileName).ToHashSet();
                Assert.Contains("id_rsa", names);
                Assert.DoesNotContain("something.log", names);
                Assert.DoesNotContain("backup.bak", names);
                Assert.DoesNotContain("temp.tmp", names);
                Assert.DoesNotContain("file.old", names);
                Assert.DoesNotContain("data.json", names);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void ScanSshDirectory_NonExistentDir_ReturnsEmpty()
        {
            var results = QuickSsh.ScanSshDirectory(@"C:\nonexistent\path\that\does\not\exist");
            Assert.Empty(results);
        }
    }
}
