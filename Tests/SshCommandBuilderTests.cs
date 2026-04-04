using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    public class SshCommandBuilderTests
    {
        // ── QuoteArgument ─────────────────────────────────────────────────────────

        [Fact]
        public void QuoteArgument_EmptyString_ReturnsEmptyQuotes()
        {
            Assert.Equal("\"\"", SshCommandBuilder.QuoteArgument(""));
        }

        [Fact]
        public void QuoteArgument_NullString_ReturnsEmptyQuotes()
        {
            Assert.Equal("\"\"", SshCommandBuilder.QuoteArgument(null!));
        }

        [Fact]
        public void QuoteArgument_NoSpecialChars_ReturnsUnchanged()
        {
            Assert.Equal("user@host", SshCommandBuilder.QuoteArgument("user@host"));
        }

        [Fact]
        public void QuoteArgument_ContainsSpace_IsQuoted()
        {
            var result = SshCommandBuilder.QuoteArgument("my key");
            Assert.StartsWith("\"", result);
            Assert.EndsWith("\"", result);
            Assert.Contains("my key", result);
        }

        [Fact]
        public void QuoteArgument_ContainsTab_IsQuoted()
        {
            var result = SshCommandBuilder.QuoteArgument("my\tkey");
            Assert.StartsWith("\"", result);
        }

        [Fact]
        public void QuoteArgument_ContainsDoubleQuote_EscapesIt()
        {
            var result = SshCommandBuilder.QuoteArgument("key\"file");
            Assert.Contains("\\\"", result);
        }

        [Fact]
        public void QuoteArgument_ContainsBackslash_EscapesIt()
        {
            var result = SshCommandBuilder.QuoteArgument(@"C:\Users\key");
            Assert.Contains("\\\\", result);
        }

        [Fact]
        public void QuoteArgument_PathWithSpaces_ProducesValidQuotedPath()
        {
            var result = SshCommandBuilder.QuoteArgument(@"C:\My Keys\id_rsa");
            Assert.Equal(@"""C:\\My Keys\\id_rsa""", result);
        }

        // ── QuoteForDisplay ──────────────────────────────────────────────────────

        [Fact]
        public void QuoteForDisplay_PathWithoutSpaces_NoDoubleBackslash()
        {
            var result = SshCommandBuilder.QuoteForDisplay(@"C:\Users\info\.ssh\custom\skuska");
            Assert.Equal(@"C:\Users\info\.ssh\custom\skuska", result);
            Assert.DoesNotContain("\\\\", result);
        }

        [Fact]
        public void QuoteForDisplay_PathWithSpaces_QuotedNoDoubleBackslash()
        {
            var result = SshCommandBuilder.QuoteForDisplay(@"C:\Users\info\.ssh\My Keys\medzera");
            Assert.Equal("\"" + @"C:\Users\info\.ssh\My Keys\medzera" + "\"", result);
            Assert.DoesNotContain("\\\\", result);
        }

        [Fact]
        public void QuoteForDisplay_InsertedQueryText_NeverContainsDoubleBackslash()
        {
            // Simulate the exact flow: "-i " + QuoteForDisplay(path)
            var path = @"C:\Users\info\.ssh\custom\skuska";
            var queryText = "-i " + SshCommandBuilder.QuoteForDisplay(path);
            Assert.DoesNotContain("\\\\", queryText);
            Assert.Contains(path, queryText);
        }

        // ── Build ─────────────────────────────────────────────────────────────────

        [Fact]
        public void Build_HostOnly_ProducesBasicSshCommand()
        {
            var result = SshCommandBuilder.Build("example.com");
            Assert.Equal("ssh example.com", result);
        }

        [Fact]
        public void Build_WithUser_ProducesUserAtHost()
        {
            var result = SshCommandBuilder.Build("example.com", user: "alice");
            Assert.Equal("ssh alice@example.com", result);
        }

        [Fact]
        public void Build_WithDefaultPort22_OmitsPort()
        {
            var result = SshCommandBuilder.Build("example.com", port: "22");
            Assert.DoesNotContain("-p", result);
        }

        [Fact]
        public void Build_WithNonDefaultPort_IncludesPort()
        {
            var result = SshCommandBuilder.Build("example.com", port: "2222");
            Assert.Contains("-p 2222", result);
        }

        [Fact]
        public void Build_WithIdentityFile_IncludesFlag()
        {
            var result = SshCommandBuilder.Build("example.com", identityFile: "/home/user/.ssh/id_rsa");
            Assert.Contains("-i /home/user/.ssh/id_rsa", result);
        }

        [Fact]
        public void Build_IdentityFileWithSpaces_IsQuoted()
        {
            var result = SshCommandBuilder.Build("example.com", identityFile: @"C:\My Keys\id_rsa");
            Assert.Contains("-i \"", result);
        }

        [Fact]
        public void Build_WithRemoteCommand_AppendsAtEnd()
        {
            var result = SshCommandBuilder.Build("example.com", remoteCommand: "ls -la");
            Assert.EndsWith("\"ls -la\"", result);
        }

        [Fact]
        public void Build_AllOptions_ProducesCorrectOrder()
        {
            var result = SshCommandBuilder.Build(
                "example.com",
                user: "alice",
                port: "2222",
                identityFile: "/home/alice/.ssh/id_rsa");

            Assert.StartsWith("ssh", result);
            Assert.Contains("-i /home/alice/.ssh/id_rsa", result);
            Assert.Contains("-p 2222", result);
            Assert.Contains("alice@example.com", result);
            // identity file and port should come before the destination
            Assert.True(result.IndexOf("-i") < result.IndexOf("alice@example.com"));
            Assert.True(result.IndexOf("-p") < result.IndexOf("alice@example.com"));
        }
    }
}
