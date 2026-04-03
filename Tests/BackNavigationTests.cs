using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Verifies the back-navigation score invariants.
    ///
    /// The back-navigation row appears in every submenu immediately below the pinned
    /// usage-hint row, so that the user can press Enter on row 2 to return to the
    /// parent command level without manually erasing text.
    ///
    /// Note: True Backspace-driven parent navigation is not possible via the
    /// Flow Launcher plugin SDK.  The Query() method receives only the post-processed
    /// search string; no keyboard-event hook is exposed.  Explicit back-navigation rows
    /// are therefore the correct and reliable solution.
    /// </summary>
    public class BackNavigationTests
    {
        [Fact]
        public void BackNavScore_IsBelowManagementRow()
        {
            Assert.True(QuickSsh.ScoreBackNavigation < QuickSsh.ScoreSubMenuManagement,
                "Back-nav row must appear below the pinned usage/management hint.");
        }

        [Fact]
        public void BackNavScore_IsAboveAllProfilesActionRows()
        {
            Assert.True(QuickSsh.ScoreBackNavigation > QuickSsh.ScoreProfilesActionAdd,
                "Back-nav row must appear above every profiles action row (including 'add', the highest).");
        }

        [Fact]
        public void BackNavScore_IsAboveAllShellActionRows()
        {
            Assert.True(QuickSsh.ScoreBackNavigation > QuickSsh.ScoreShellActionAdd,
                "Back-nav row must appear above every shell action row (including 'add', the highest).");
        }

        [Fact]
        public void BackNavScore_IsAboveProfilesSavedItems()
        {
            Assert.True(QuickSsh.ScoreBackNavigation > QuickSsh.ScoreProfilesSavedItem,
                "Back-nav row must appear above saved profile entries.");
        }

        [Fact]
        public void BackNavScore_IsAboveShellOtherEntries()
        {
            Assert.True(QuickSsh.ScoreBackNavigation > QuickSsh.ScoreShellOtherStart,
                "Back-nav row must appear above non-selected shell entries.");
        }

        [Fact]
        public void BackNavScore_IsExactlyOneBelow_ManagementRow()
        {
            // Ensures the back-nav row is pinned immediately adjacent to the management row
            // with no other score values between them.
            Assert.Equal(QuickSsh.ScoreSubMenuManagement - 1, QuickSsh.ScoreBackNavigation);
        }

        // ── config submenu back-navigation ────────────────────────────────────────

        [Fact]
        public void ConfigSubmenu_BackNavScoreGuaranteesSecondRowPosition()
        {
            // The config submenu must display:
            //   1. management row  (ScoreSubMenuManagement)
            //   2. back-nav row    (ScoreBackNavigation)
            //   3. config action   (no explicit Score, defaults to 0)
            // The back-nav score must be below management but above 0 (default).
            Assert.True(QuickSsh.ScoreBackNavigation < QuickSsh.ScoreSubMenuManagement,
                "Config back-nav must be below the management row.");
            Assert.True(QuickSsh.ScoreBackNavigation > 0,
                "Config back-nav must be above the config action row (Score = 0 default).");
        }

        // ── help submenu back-navigation ──────────────────────────────────────────

        [Fact]
        public void HelpSubmenu_BackNavScoreGuaranteesSecondRowPosition()
        {
            // The help submenu must display:
            //   1. management row  (ScoreSubMenuManagement)
            //   2. back-nav row    (ScoreBackNavigation)
            //   3. help action     (no explicit Score, defaults to 0)
            // The back-nav score must be below management but above 0 (default).
            Assert.True(QuickSsh.ScoreBackNavigation < QuickSsh.ScoreSubMenuManagement,
                "Help back-nav must be below the management row.");
            Assert.True(QuickSsh.ScoreBackNavigation > 0,
                "Help back-nav must be above the help action row (Score = 0 default).");
        }
    }
}
