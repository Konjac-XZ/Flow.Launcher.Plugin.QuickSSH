using System.Collections.Generic;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    public class AutoCompleterTests
    {
        // ── Empty input ───────────────────────────────────────────────────────────

        [Fact]
        public void GetSuggestions_EmptyInput_ReturnsAllCommands()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "", null, "icon.png");

            Assert.NotEmpty(results);
            // All visible commands must be present.
            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("add", titles);
            Assert.Contains("remove", titles);
            Assert.Contains("profiles", titles);
            Assert.Contains("shell", titles);
            Assert.Contains("config", titles);
            Assert.Contains("export", titles);
            Assert.Contains("import", titles);
            Assert.Contains("copy", titles);
            Assert.Contains("rename", titles);
            Assert.Contains("help", titles);

            // Hidden aliases must NOT appear in suggestions.
            Assert.DoesNotContain("p", titles);
            Assert.DoesNotContain("d", titles);
            Assert.DoesNotContain("docs", titles);
        }

        [Fact]
        public void GetSuggestions_EmptyInput_AutoCompleteTextIncludesTrailingSpace()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "", null, "icon.png");

            foreach (var r in results)
                Assert.True(r.AutoCompleteText?.EndsWith(" "),
                    $"AutoCompleteText for '{r.Title}' should end with a space.");
        }

        // ── Partial input matching ────────────────────────────────────────────────

        [Fact]
        public void GetSuggestions_PartialAdd_ReturnsSuggestionForAdd()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "ad", null, "icon.png");

            Assert.Contains(results, r => r.Title == "add");
        }

        [Fact]
        public void GetSuggestions_PartialRe_ReturnsBothRemoveAndRename()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "re", null, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("remove", titles);
            Assert.Contains("rename", titles);
        }

        [Fact]
        public void GetSuggestions_PartialCo_ReturnsMatchingCommands()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "co", null, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("copy", titles);
            Assert.Contains("config", titles);
        }

        [Fact]
        public void GetSuggestions_UnmatchedInput_ReturnsEmptyList()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "zzz", null, "icon.png");
            Assert.Empty(results);
        }

        // ── Profile suggestions after "profiles " ─────────────────────────────────

        [Fact]
        public void GetSuggestions_ProfilesPrefix_SuggestsProfileNames()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.Entries["work"] = "ssh alice@work.example.com";
            userData.Entries["home"] = "ssh alice@home.example.com";

            var results = AutoCompleter.GetSuggestions("ssh", "profiles ", userData, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("work", titles);
            Assert.Contains("home", titles);
        }

        [Fact]
        public void GetSuggestions_ProfilesPrefixWithSearch_FiltersProfiles()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.Entries["work"] = "ssh alice@work.example.com";
            userData.Entries["home"] = "ssh alice@home.example.com";

            var results = AutoCompleter.GetSuggestions("ssh", "profiles wor", userData, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("work", titles);
            Assert.DoesNotContain("home", titles);
        }

        [Fact]
        public void GetSuggestions_ShortAlias_p_AlsoProvidesProfiles()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.Entries["dev"] = "ssh dev@host";

            var results = AutoCompleter.GetSuggestions("ssh", "p dev", userData, "icon.png");

            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("dev", titles);
        }

        // ── Null / missing userData ───────────────────────────────────────────────

        [Fact]
        public void GetSuggestions_NullUserData_DoesNotThrow()
        {
            var exception = Record.Exception(() =>
                AutoCompleter.GetSuggestions("ssh", "profiles ", null, "icon.png"));

            Assert.Null(exception);
        }

        // ── AutoCompleteText format ───────────────────────────────────────────────

        [Fact]
        public void GetSuggestions_AutoCompleteText_StartsWithActionKeyword()
        {
            var results = AutoCompleter.GetSuggestions("myssh", "", null, "icon.png");

            foreach (var r in results)
                Assert.True(r.AutoCompleteText?.StartsWith("myssh "),
                    $"AutoCompleteText '{r.AutoCompleteText}' should start with the action keyword.");
        }
    }
}
