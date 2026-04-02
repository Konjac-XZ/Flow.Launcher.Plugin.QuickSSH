using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Tests for the implicit direct-SSH detection helper.
    /// </summary>
    public class IsImplicitSshInputTests
    {
        // ── Null / empty ─────────────────────────────────────────────────────────

        [Fact]
        public void ImplicitSsh_Null_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput(null!));
        }

        [Fact]
        public void ImplicitSsh_EmptyString_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput(""));
        }

        // ── user@host format ─────────────────────────────────────────────────────

        [Fact]
        public void ImplicitSsh_UserAtHost_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("root@10.100.100.110"));
        }

        [Fact]
        public void ImplicitSsh_UserAtHostname_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("deploy@staging.example.com"));
        }

        // ── SSH option flags ─────────────────────────────────────────────────────

        [Fact]
        public void ImplicitSsh_DashP_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("-p 22 root@10.100.100.110"));
        }

        [Fact]
        public void ImplicitSsh_DashI_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("-i \"C:\\Users\\info\\.ssh\\private_key\" -o IdentitiesOnly=yes root@10.100.100.110"));
        }

        [Fact]
        public void ImplicitSsh_DashO_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("-o StrictHostKeyChecking=no user@host"));
        }

        // ── Bare IP / hostname ───────────────────────────────────────────────────

        [Fact]
        public void ImplicitSsh_BareIp_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("10.100.100.110"));
        }

        [Fact]
        public void ImplicitSsh_BareHostname_ReturnsTrue()
        {
            Assert.True(QuickSsh.IsImplicitSshInput("myserver.example.com"));
        }

        // ── Known partial command names should NOT trigger implicit SSH ──────────

        [Fact]
        public void ImplicitSsh_PartialCommandAdd_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput("ad"));
        }

        [Fact]
        public void ImplicitSsh_PartialCommandRename_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput("rena"));
        }

        [Fact]
        public void ImplicitSsh_PartialCommandShell_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput("sh"));
        }

        [Fact]
        public void ImplicitSsh_PartialCommandHelp_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput("hel"));
        }

        [Fact]
        public void ImplicitSsh_SingleLetter_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput("r"));
        }

        [Fact]
        public void ImplicitSsh_BareWordNoAtNoDot_ReturnsFalse()
        {
            Assert.False(QuickSsh.IsImplicitSshInput("export"));
        }
    }
}
