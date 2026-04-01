using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Tests for the internal QuickSsh.ScoreProfile fuzzy search helper.
    /// </summary>
    public class ScoreProfileTests
    {
        // ── Exact matches ─────────────────────────────────────────────────────────

        [Fact]
        public void Score_ExactMatchInBoth_Returns0()
        {
            // search term appears in both name and command → score 0
            int score = QuickSsh.ScoreProfile("web", "web", "ssh web@host");
            Assert.Equal(0, score);
        }

        [Fact]
        public void Score_ExactMatchInNameOnly_Returns1()
        {
            int score = QuickSsh.ScoreProfile("web", "web", "ssh other@host");
            Assert.Equal(1, score);
        }

        [Fact]
        public void Score_ExactMatchInCommandOnly_Returns2()
        {
            int score = QuickSsh.ScoreProfile("web", "myserver", "ssh web@host");
            Assert.Equal(2, score);
        }

        [Fact]
        public void Score_NoMatch_ReturnsIntMaxValue()
        {
            int score = QuickSsh.ScoreProfile("zzz", "myserver", "ssh user@host");
            Assert.Equal(int.MaxValue, score);
        }

        // ── Case-insensitive ──────────────────────────────────────────────────────

        [Fact]
        public void Score_CaseInsensitiveMatch_Scores()
        {
            int score = QuickSsh.ScoreProfile("WEB", "web", "ssh web@host");
            Assert.NotEqual(int.MaxValue, score);
        }

        // ── Diacritics ────────────────────────────────────────────────────────────

        [Fact]
        public void Score_SearchWithDiacritics_MatchesWithoutDiacritics()
        {
            int score = QuickSsh.ScoreProfile("server", "sérver", "ssh host");
            Assert.NotEqual(int.MaxValue, score);
        }

        // ── Partial substring match ───────────────────────────────────────────────

        [Fact]
        public void Score_PartialSubstring_MatchesName()
        {
            // "web" is a substring of "webserver"
            int score = QuickSsh.ScoreProfile("web", "webserver", "ssh prod@host");
            Assert.Equal(1, score);
        }

        [Fact]
        public void Score_PartialSubstringInCommand_MatchesCommand()
        {
            // "prod" is a substring of "prod.example.com"
            int score = QuickSsh.ScoreProfile("prod", "myserver", "ssh user@prod.example.com");
            Assert.Equal(2, score);
        }

        // ── Score ordering ────────────────────────────────────────────────────────

        [Fact]
        public void Score_NameMatchBetterThanCommandMatch()
        {
            int nameScore = QuickSsh.ScoreProfile("web", "web", "ssh other@host");
            int cmdScore = QuickSsh.ScoreProfile("web", "myserver", "ssh web@host");
            Assert.True(nameScore < cmdScore,
                "A match in name should have a lower (better) score than match in command only.");
        }
    }
}
