using System.Collections.Generic;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    public class AutoCompleterTests
    {
        // ── Empty input ───────────────────────────────────────────────────────────

        [Fact]
        public void GetSuggestions_EmptyInput_ReturnsTopLevelCommands()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "", null, "icon.png");

            Assert.NotEmpty(results);
            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            // New top-level commands
            Assert.Contains("profiles", titles);
            Assert.Contains("config", titles);
            Assert.Contains("shell", titles);
            Assert.Contains("help", titles);

            // Removed top-level commands must NOT appear
            Assert.DoesNotContain("add", titles);
            Assert.DoesNotContain("remove", titles);
            Assert.DoesNotContain("export", titles);
            Assert.DoesNotContain("import", titles);
            Assert.DoesNotContain("copy", titles);
            Assert.DoesNotContain("rename", titles);

            // Hidden aliases must NOT appear
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
        public void GetSuggestions_PartialPr_ReturnsProfiles()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "pr", null, "icon.png");
            Assert.Contains(results, r => r.Title == "profiles");
        }

        [Fact]
        public void GetSuggestions_PartialCo_ReturnsConfig()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "co", null, "icon.png");
            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);
            Assert.Contains("config", titles);
        }

        [Fact]
        public void GetSuggestions_UnmatchedInput_ReturnsEmptyList()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "zzz", null, "icon.png");
            Assert.Empty(results);
        }

        // ── Profile sub-command suggestions after "profiles " ─────────────────────

        [Fact]
        public void GetSuggestions_ProfilesSpace_SuggestsSubCommands()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "profiles ", null, "icon.png");
            var titles = new HashSet<string>();
            foreach (var r in results) titles.Add(r.Title);

            Assert.Contains("add", titles);
            Assert.Contains("remove", titles);
            Assert.Contains("rename", titles);
            Assert.Contains("copy", titles);
            Assert.Contains("export", titles);
            Assert.Contains("import", titles);
        }

        [Fact]
        public void GetSuggestions_ProfilesPrefix_SuggestsProfileNames()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.Profiles["work"] = new SshProfile { Type = "ssh", HostName = "work.example.com", User = "alice" };
            userData.Profiles["home"] = new SshProfile { Type = "ssh", HostName = "home.example.com", User = "alice" };

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
            userData.Profiles["work"] = new SshProfile { HostName = "work.example.com" };
            userData.Profiles["home"] = new SshProfile { HostName = "home.example.com" };

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
        [InlineData("profiles")]
        [InlineData("config")]
        [InlineData("shell")]
        [InlineData("help")]
        public void GetSuggestions_ExactTopLevelCommandName_NoTrailingSpace_ReturnsEmpty(string exactCommand)
        {
            var results = AutoCompleter.GetSuggestions("ssh", exactCommand, null, "icon.png");
            Assert.Empty(results);
        }

        [Fact]
        public void GetSuggestions_ExactCommandName_CaseInsensitive_ReturnsEmpty()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "PROFILES", null, "icon.png");
            Assert.Empty(results);
        }

        [Fact]
        public void GetSuggestions_ExactCommandName_WithTrailingSpace_IsNotBlocked()
        {
            var userData = new UserData();
            userData.Attach(() => { });
            userData.Profiles["dev"] = new SshProfile { HostName = "dev.host" };

            var results = AutoCompleter.GetSuggestions("ssh", "profiles ", userData, "icon.png");
            Assert.Contains(results, r => r.Title == "dev");
        }

        // ── Partial names just before exact match still return suggestions ─────────

        [Fact]
        public void GetSuggestions_PartialProfilesPrefix_ReturnsSuggestion()
        {
            var results = AutoCompleter.GetSuggestions("ssh", "profi", null, "icon.png");
            Assert.Contains(results, r => r.Title == "profiles");
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
