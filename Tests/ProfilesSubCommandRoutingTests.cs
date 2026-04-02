using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Tests for QuickSsh.ResolveProfilesSubCommandPrefix — the helper that maps
    /// an exact or partial sub-command name to the canonical sub-command string,
    /// and drives prefix-based routing in HandleProfiles.
    /// </summary>
    public class ProfilesSubCommandRoutingTests
    {
        // ── Exact matches ─────────────────────────────────────────────────────────

        [Theory]
        [InlineData("add",    "add")]
        [InlineData("remove", "remove")]
        [InlineData("rename", "rename")]
        [InlineData("copy",   "copy")]
        [InlineData("export", "export")]
        [InlineData("import", "import")]
        public void ResolvePrefix_ExactSubCommand_ReturnsThatSubCommand(string input, string expected)
        {
            var result = QuickSsh.ResolveProfilesSubCommandPrefix(input);
            Assert.Equal(expected, result);
        }

        // ── Unique prefix matches ─────────────────────────────────────────────────

        [Theory]
        [InlineData("ad",    "add")]
        [InlineData("rem",   "remove")]
        [InlineData("ren",   "rename")]
        [InlineData("cop",   "copy")]
        [InlineData("expor", "export")]
        [InlineData("impor", "import")]
        public void ResolvePrefix_UniquePrefix_ReturnsMatchedSubCommand(string prefix, string expected)
        {
            var result = QuickSsh.ResolveProfilesSubCommandPrefix(prefix);
            Assert.Equal(expected, result);
        }

        // ── Ambiguous prefixes — must return null ─────────────────────────────────

        [Theory]
        [InlineData("r")]   // remove + rename
        [InlineData("re")]  // remove + rename
        [InlineData("i")]   // import (not ambiguous — but test edge-case single-char)
        public void ResolvePrefix_AmbiguousOrUnknownPrefix_ReturnsNullOrUnique(string prefix)
        {
            // "r" and "re" are ambiguous (remove + rename) → null
            // "i" uniquely matches "import" → "import"
            var result = QuickSsh.ResolveProfilesSubCommandPrefix(prefix);

            if (prefix == "i")
                Assert.Equal("import", result);
            else
                Assert.Null(result);
        }

        // ── No match ─────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("wor")]
        [InlineData("zzz")]
        [InlineData("xyz")]
        public void ResolvePrefix_NoMatchingSubCommand_ReturnsNull(string prefix)
        {
            var result = QuickSsh.ResolveProfilesSubCommandPrefix(prefix);
            Assert.Null(result);
        }

        // ── Empty / null input ────────────────────────────────────────────────────

        [Fact]
        public void ResolvePrefix_EmptyString_ReturnsNull()
        {
            var result = QuickSsh.ResolveProfilesSubCommandPrefix("");
            Assert.Null(result);
        }

        // ── Case insensitivity ────────────────────────────────────────────────────

        [Theory]
        [InlineData("ADD",    "add")]
        [InlineData("Remove", "remove")]
        [InlineData("COP",    "copy")]
        public void ResolvePrefix_CaseInsensitiveInput_Matches(string input, string expected)
        {
            var result = QuickSsh.ResolveProfilesSubCommandPrefix(input);
            Assert.Equal(expected, result);
        }
    }
}
