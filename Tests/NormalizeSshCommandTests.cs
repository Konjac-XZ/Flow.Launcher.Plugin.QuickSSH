using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Tests for the internal QuickSsh.NormalizeSshCommand helper.
    /// </summary>
    public class NormalizeSshCommandTests
    {
        // ── Null / empty / whitespace ─────────────────────────────────────────────

        [Fact]
        public void Normalize_Null_ReturnsNull()
        {
            Assert.Null(QuickSsh.NormalizeSshCommand(null!));
        }

        [Fact]
        public void Normalize_EmptyString_ReturnsNull()
        {
            Assert.Null(QuickSsh.NormalizeSshCommand(""));
        }

        [Fact]
        public void Normalize_WhitespaceOnly_ReturnsNull()
        {
            Assert.Null(QuickSsh.NormalizeSshCommand("   "));
        }

        // ── Auto-prepend "ssh " ───────────────────────────────────────────────────

        [Fact]
        public void Normalize_DestinationOnly_PrependsSsh()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("user@host"));
        }

        [Fact]
        public void Normalize_HostOnly_PrependsSsh()
        {
            Assert.Equal("ssh host.example.com", QuickSsh.NormalizeSshCommand("host.example.com"));
        }

        [Fact]
        public void Normalize_AlreadyHasSshPrefix_NoDoublePrepend()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("ssh user@host"));
        }

        [Fact]
        public void Normalize_SshPrefixCaseInsensitive_NoDoublePrepend()
        {
            Assert.Equal("SSH user@host", QuickSsh.NormalizeSshCommand("SSH user@host"));
        }

        // ── Strip leading /flags ──────────────────────────────────────────────────

        [Fact]
        public void Normalize_LeadingSlashC_IsStripped()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("/c ssh user@host"));
        }

        [Fact]
        public void Normalize_LeadingSlashK_IsStripped()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("/k ssh user@host"));
        }

        [Fact]
        public void Normalize_MultipleLeadingFlags_AllStripped()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("/k /c ssh user@host"));
        }

        [Fact]
        public void Normalize_OnlySlashFlag_NoRemainder_ReturnsNull()
        {
            Assert.Null(QuickSsh.NormalizeSshCommand("/c"));
        }

        // ── Strip /flags after "ssh " prefix ─────────────────────────────────────

        [Fact]
        public void Normalize_SshWithSlashFlag_FlagStripped()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("ssh /c user@host"));
        }

        [Fact]
        public void Normalize_SshWithMultipleSlashFlags_AllStripped()
        {
            Assert.Equal("ssh user@host", QuickSsh.NormalizeSshCommand("ssh /k /c user@host"));
        }

        // ── Valid commands passed through unchanged ───────────────────────────────

        [Fact]
        public void Normalize_SshWithOptions_PassedThrough()
        {
            Assert.Equal("ssh -p 2222 user@host", QuickSsh.NormalizeSshCommand("ssh -p 2222 user@host"));
        }

        [Fact]
        public void Normalize_SshWithIdentityFile_PassedThrough()
        {
            Assert.Equal("ssh -i /home/user/.ssh/id_rsa user@host",
                QuickSsh.NormalizeSshCommand("ssh -i /home/user/.ssh/id_rsa user@host"));
        }
    }
}
