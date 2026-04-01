using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    public class ProfileManagerTests : IDisposable
    {
        private readonly string _tmpDir;

        public ProfileManagerTests()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"quickssh_tests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, recursive: true);
        }

        private string GetTmpPath(string filename = "profiles.json")
            => Path.Combine(_tmpDir, filename);

        // ── Creation ──────────────────────────────────────────────────────────────

        [Fact]
        public void Constructor_NewFile_CreatesEmptyUserData()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);

            Assert.NotNull(pm.UserData);
            Assert.Empty(pm.UserData.Entries);
            Assert.Empty(pm.UserData.CustomShell);
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void Constructor_MissingDirectory_CreatesDirectory()
        {
            var subDir = Path.Combine(_tmpDir, "sub", "dir");
            var path = Path.Combine(subDir, "profiles.json");

            var pm = new ProfileManager(path);

            Assert.True(Directory.Exists(subDir));
            Assert.True(File.Exists(path));
        }

        // ── Save & Load ───────────────────────────────────────────────────────────

        [Fact]
        public void SaveConfiguration_WritesValidJson()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            pm.UserData.Entries["work"] = "ssh alice@work.example.com";

            pm.SaveConfiguration();

            var json = File.ReadAllText(path);
            Assert.Contains("work", json);
            Assert.Contains("ssh alice@work.example.com", json);
        }

        [Fact]
        public void LoadConfiguration_ReadsPersistedEntries()
        {
            var path = GetTmpPath();
            var pm1 = new ProfileManager(path);
            pm1.UserData.Entries["myhost"] = "ssh user@myhost";
            pm1.SaveConfiguration();

            var pm2 = new ProfileManager(path);

            Assert.True(pm2.UserData.Entries.ContainsKey("myhost"));
            Assert.Equal("ssh user@myhost", pm2.UserData.Entries["myhost"]);
        }

        [Fact]
        public void AutoSave_TriggeredOnEntryAdd()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);

            // Adding an entry triggers auto-save through the AutoSaveDictionary callback.
            pm.UserData.Entries["autokey"] = "ssh auto@host";

            // Reload from file to verify it was persisted.
            var pm2 = new ProfileManager(path);
            Assert.True(pm2.UserData.Entries.ContainsKey("autokey"));
        }

        [Fact]
        public void AutoSave_TriggeredOnEntryRemove()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            pm.UserData.Entries["removeme"] = "ssh user@host";

            pm.UserData.Entries.Remove("removeme");

            var pm2 = new ProfileManager(path);
            Assert.False(pm2.UserData.Entries.ContainsKey("removeme"));
        }

        [Fact]
        public void SaveConfiguration_UsesAtomicWrite()
        {
            // The tmp file must not be left behind after a successful save.
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            pm.UserData.Entries["k"] = "v";
            pm.SaveConfiguration();

            Assert.False(File.Exists(path + ".tmp"),
                "Temp file should be cleaned up after atomic move.");
        }

        // ── CustomShell ───────────────────────────────────────────────────────────

        [Fact]
        public void CustomShell_AddAndPersist()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            pm.UserData.CustomShell["wt"] = "wt.exe -p";

            var pm2 = new ProfileManager(path);
            Assert.True(pm2.UserData.CustomShell.ContainsKey("wt"));
            Assert.Equal("wt.exe -p", pm2.UserData.CustomShell["wt"]);
        }

        [Fact]
        public void SelectedCustomShell_PersistsAcrossReload()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            pm.UserData.CustomShell.SetCallback(null);
            pm.UserData.CustomShell["myshell"] = "myshell.exe";
            pm.UserData.SelectedCustomShell = "myshell";
            pm.UserData.CustomShell.SetCallback(pm.SaveConfiguration);
            pm.SaveConfiguration();

            var pm2 = new ProfileManager(path);
            Assert.Equal("myshell", pm2.UserData.SelectedCustomShell);
        }

        // ── AutoSaveDictionary ────────────────────────────────────────────────────

        [Fact]
        public void AutoSaveDictionary_SetCallbackNull_SuppressesSave()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);

            // Disable callback, add entry; no save should occur.
            pm.UserData.Entries.SetCallback(null);
            pm.UserData.Entries["silent"] = "ssh silent@host";

            // Read raw file – it should NOT contain the new entry yet.
            var pm2 = new ProfileManager(path);
            Assert.False(pm2.UserData.Entries.ContainsKey("silent"),
                "Entry should not be saved while callback is null.");

            // Restore callback and explicitly save.
            pm.UserData.Entries.SetCallback(pm.SaveConfiguration);
            pm.SaveConfiguration();

            var pm3 = new ProfileManager(path);
            Assert.True(pm3.UserData.Entries.ContainsKey("silent"));
        }

        [Fact]
        public void AutoSaveDictionary_Clear_TriggersCallback()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            pm.UserData.Entries["a"] = "ssh a@host";
            pm.UserData.Entries["b"] = "ssh b@host";

            pm.UserData.Entries.Clear();

            var pm2 = new ProfileManager(path);
            Assert.Empty(pm2.UserData.Entries);
        }

        [Fact]
        public void AutoSaveDictionary_Count_ReflectsCurrentState()
        {
            var path = GetTmpPath();
            var pm = new ProfileManager(path);
            Assert.Equal(0, pm.UserData.Entries.Count);

            pm.UserData.Entries["x"] = "ssh x@host";
            Assert.Equal(1, pm.UserData.Entries.Count);

            pm.UserData.Entries.Remove("x");
            Assert.Equal(0, pm.UserData.Entries.Count);
        }
    }
}
