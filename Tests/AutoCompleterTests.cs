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

            // These commands must NOT appear in suggestions.
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

        // ── Null / missing userData ───────────────────────────────────────────────

        [Fact]
        public void GetSuggestions_NullUserData_DoesNotThrow()
        {
            var exception = Record.Exception(() =>
                AutoCompleter.GetSuggestions("ssh", "profiles ", null, "icon.png"));

            Assert.Null(exception);
        }

        // ── Exact command match — must return empty (command handler owns the view) ──

        [Theory]
        [InlineData("add")]
        [InlineData("remove")]
        [InlineData("profiles")]
        [InlineData("shell")]
        [InlineData("config")]
        [InlineData("export")]
        [InlineData("import")]
        [InlineData("copy")]
        [InlineData("rename")]
        [InlineData("help")]
        public void GetSuggestions_ExactCommandName_NoTrailingSpace_ReturnsEmpty(string exactCommand)
        {
            // When the first token exactly matches a known command (no trailing space),
            // the Query() switch routes to that command's handler. AutoCompleter must
            // return no suggestions to avoid polluting the command-specific result view.
            var results = AutoCompleter.GetSuggestions("ssh", exactCommand, null, "icon.png");
            Assert.Empty(results);
        }

        [Fact]
        public void GetSuggestions_ExactCommandName_CaseInsensitive_ReturnsEmpty()
        {
            // Exact-match guard should be case-insensitive (verb is always ToLowerInvariant).
            var results = AutoCompleter.GetSuggestions("ssh", "RENAME", null, "icon.png");
            Assert.Empty(results);
        }

        [Fact]
        public void GetSuggestions_ExactCommandName_WithTrailingSpace_IsNotBlocked()
        {
            // "profiles " (with trailing space) means the user pressed space after the command
            // and is about to type an argument. The guard must NOT block this — profile names
            // should still be suggested.
            var userData = new UserData();
            userData.Attach(() => { });
            userData.Entries["dev"] = "ssh dev@host";

            var results = AutoCompleter.GetSuggestions("ssh", "profiles ", userData, "icon.png");
            Assert.Contains(results, r => r.Title == "dev");
        }

        // ── Partial names just before exact match still return suggestions ─────────

        [Fact]
        public void GetSuggestions_PartialRename_ReturnsRenameSuggestion()
        {
            // "renam" is a prefix of "rename" but is NOT an exact match — still suggest.
            var results = AutoCompleter.GetSuggestions("ssh", "renam", null, "icon.png");
            Assert.Contains(results, r => r.Title == "rename");
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
