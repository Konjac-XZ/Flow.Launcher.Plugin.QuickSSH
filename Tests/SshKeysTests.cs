using System.Collections.Generic;
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

        // ── AutoCompleter keys suggestions ────────────────────────────────────────

        [Fact]
        public void GetSuggestions_PartialKe_ReturnsKeys()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "ke", null, "icon.png");
            Assert.Contains(results, r => r.Title == "keys");
        }

        [Fact]
        public void GetSuggestions_KeysSpace_SuggestsSubCommands()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "keys ", null, "icon.png");
            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("add", titles);
            Assert.Contains("remove", titles);
        }

        [Theory]
        [InlineData("a",      new[] { "add" })]
        [InlineData("ad",     new[] { "add" })]
        [InlineData("add",    new[] { "add" })]
        [InlineData("r",      new[] { "remove" })]
        [InlineData("re",     new[] { "remove" })]
        [InlineData("rem",    new[] { "remove" })]
        public void GetSuggestions_KeysPartialPrefix_ShowsMatchingSubCommands(
            string partial, string[] expected)
        {
            var results = AutoCompleter.GetSuggestions("ssh", "keys " + partial, null, "icon.png");
            var subCommandTitles = results
                .Select(r => r.Title)
                .Where(t => t == "add" || t == "remove")
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
            Assert.True(QuickSsh.ScoreKeysActionRemove > QuickSsh.ScoreKeysSavedItem,
                "The remove action row must appear above saved keys.");
        }

        [Fact]
        public void KeysSubmenu_ActionRowAddIsAboveActionRowRemove()
        {
            Assert.True(QuickSsh.ScoreKeysActionAdd > QuickSsh.ScoreKeysActionRemove);
        }

        [Fact]
        public void KeysSubmenu_ActionRowScoresAreSafeAboveSavedItemBase()
        {
            int gap = QuickSsh.ScoreKeysActionRemove - QuickSsh.ScoreKeysSavedItem;
            Assert.True(gap > 500,
                $"Remove action score must exceed saved item base by > 500 (actual gap: {gap}).");
        }
    }
}
