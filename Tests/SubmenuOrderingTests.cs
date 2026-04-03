using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Verifies the submenu score invariants that drive consistent display ordering.
    /// Flow Launcher sorts Result objects by Score descending, so the required layout:
    ///   1. management row
    ///   2. action rows
    ///   3. saved items
    /// must be enforced through the Score constants alone.
    ///
    /// Root cause of the original profiles ordering bug:
    ///   Action row scores were 10-60 and saved profiles used Score=0.  Flow Launcher's
    ///   built-in fuzzy-match bonus can boost a Score=0 result by hundreds of points,
    ///   pushing saved profiles above the "Import profiles" action row (Score=10).
    ///   The fix mirrors the shell submenu: action rows use the 1010-1060 range and
    ///   saved profiles decrement from 500, matching the scale used by ScoreShellOtherStart.
    /// </summary>
    public class SubmenuOrderingTests
    {
        // ── profiles submenu ──────────────────────────────────────────────────────

        [Fact]
        public void ProfilesSubmenu_ManagementRowIsAboveAllActionRows()
        {
            Assert.True(QuickSsh.ScoreSubMenuManagement > QuickSsh.ScoreProfilesActionAdd,
                "Management row must outrank every profiles action row.");
        }

        [Fact]
        public void ProfilesSubmenu_AllActionRowsAreAboveSavedItems()
        {
            // The lowest-priority action row (import) must still beat a saved profile entry.
            Assert.True(QuickSsh.ScoreProfilesActionImport > QuickSsh.ScoreProfilesSavedItem,
                "The import action row (lowest action score) must appear above saved profiles.");
        }

        [Fact]
        public void ProfilesSubmenu_ActionRowScoresAreInDescendingOrder()
        {
            // add > remove > rename > copy > export > import
            Assert.True(QuickSsh.ScoreProfilesActionAdd    > QuickSsh.ScoreProfilesActionRemove);
            Assert.True(QuickSsh.ScoreProfilesActionRemove > QuickSsh.ScoreProfilesActionRename);
            Assert.True(QuickSsh.ScoreProfilesActionRename > QuickSsh.ScoreProfilesActionCopy);
            Assert.True(QuickSsh.ScoreProfilesActionCopy   > QuickSsh.ScoreProfilesActionExport);
            Assert.True(QuickSsh.ScoreProfilesActionExport > QuickSsh.ScoreProfilesActionImport);
        }

        [Fact]
        public void ProfilesSubmenu_ActionRowScoresAreSafeAboveSavedItemBase()
        {
            // The gap between the lowest action row (import) and the highest possible saved
            // profile score (ScoreProfilesSavedItem, used as the decrement start) must be
            // large enough that Flow Launcher's fuzzy-match bonus cannot bridge it.
            // A gap > 500 is considered safe based on observed Flow Launcher scoring.
            int gap = QuickSsh.ScoreProfilesActionImport - QuickSsh.ScoreProfilesSavedItem;
            Assert.True(gap > 500,
                $"Import action score must exceed saved item base by > 500 (actual gap: {gap}).");
        }

        // ── shell submenu ─────────────────────────────────────────────────────────

        [Fact]
        public void ShellSubmenu_ManagementRowIsAboveAllActionRows()
        {
            Assert.True(QuickSsh.ScoreSubMenuManagement > QuickSsh.ScoreShellActionAdd,
                "Management row must outrank every shell action row.");
        }

        [Fact]
        public void ShellSubmenu_AllActionRowsAreAboveSelectedShell()
        {
            // The lower-priority action row (remove) must still beat the selected shell entry.
            Assert.True(QuickSsh.ScoreShellActionRemove > QuickSsh.ScoreShellSelected,
                "The remove action row must appear above the selected shell entry.");
        }

        [Fact]
        public void ShellSubmenu_AllActionRowsAreAboveOtherShells()
        {
            // "other shells" start at ScoreShellOtherStart and decrement; the action rows
            // must exceed even the maximum (first) other-shell score.
            Assert.True(QuickSsh.ScoreShellActionRemove > QuickSsh.ScoreShellOtherStart,
                "The remove action row must appear above the highest-scored non-selected shell.");
        }

        [Fact]
        public void ShellSubmenu_SelectedShellIsAboveOtherShells()
        {
            Assert.True(QuickSsh.ScoreShellSelected > QuickSsh.ScoreShellOtherStart,
                "The selected shell must appear above other (non-selected) shells.");
        }

        [Fact]
        public void ShellSubmenu_ActionRowAddIsAboveActionRowRemove()
        {
            Assert.True(QuickSsh.ScoreShellActionAdd > QuickSsh.ScoreShellActionRemove);
        }

        // ── cross-submenu consistency ─────────────────────────────────────────────

        [Fact]
        public void BothSubmenus_ShareTheSameManagementRowScore()
        {
            // The management row constant is used identically in both submenus.
            Assert.Equal(int.MaxValue, QuickSsh.ScoreSubMenuManagement);
        }

        [Fact]
        public void BothSubmenus_ActionRowScoresAreOnTheSameScale()
        {
            // Both submenus must use the 1000+ range for action rows so the ordering
            // invariant holds regardless of Flow Launcher's internal fuzzy-match bonus.
            Assert.True(QuickSsh.ScoreProfilesActionImport >= 1000,
                "Profiles import action score must be >= 1000 to be safe from fuzzy boosting.");
            Assert.True(QuickSsh.ScoreShellActionRemove >= 1000,
                "Shell remove action score must be >= 1000 to be safe from fuzzy boosting.");
        }
    }
}
