using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    public class SshConfigParserTests : IDisposable
    {
        private readonly string _tmpFile;

        public SshConfigParserTests()
        {
            _tmpFile = Path.Combine(Path.GetTempPath(), $"ssh_config_test_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (File.Exists(_tmpFile))
                File.Delete(_tmpFile);
        }

        private Dictionary<string, string> ParseConfig(string content)
        {
            File.WriteAllText(_tmpFile, content);
            return SshConfigParser.Parse(_tmpFile);
        }

        // ── Basic parsing ─────────────────────────────────────────────────────────

        [Fact]
        public void Parse_NonExistentFile_ReturnsEmpty()
        {
            var result = SshConfigParser.Parse("/nonexistent/path/config");
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_EmptyFile_ReturnsEmpty()
        {
            var result = ParseConfig("");
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_CommentLinesOnly_ReturnsEmpty()
        {
            var result = ParseConfig("# just a comment\n# another comment");
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_SimpleHost_ReturnsEntry()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    User admin\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("admin@192.168.1.10", result["myserver"]);
        }

        [Fact]
        public void Parse_HostWithNonDefaultPort_IncludesPort()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    Port 2222\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("-p 2222", result["myserver"]);
        }

        [Fact]
        public void Parse_HostWithDefaultPort22_OmitsPort()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    Port 22\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.DoesNotContain("-p", result["myserver"]);
        }

        [Fact]
        public void Parse_HostWithIdentityFile_IncludesFlag()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    IdentityFile /home/user/.ssh/id_rsa\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("-i /home/user/.ssh/id_rsa", result["myserver"]);
        }

        [Fact]
        public void Parse_HostNoHostName_UsesAliasAsTarget()
        {
            var config = "Host myserver\n    User alice\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("alice@myserver", result["myserver"]);
        }

        [Fact]
        public void Parse_WildcardHost_IsSkipped()
        {
            var config = "Host *\n    ServerAliveInterval 60\n";
            var result = ParseConfig(config);
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_WildcardHostAmongOthers_OnlySkipsWildcard()
        {
            var config =
                "Host web\n    HostName 10.0.0.1\n\n" +
                "Host *\n    ServerAliveInterval 60\n\n" +
                "Host db\n    HostName 10.0.0.2\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("web"));
            Assert.True(result.ContainsKey("db"));
            Assert.False(result.ContainsKey("*"));
        }

        // ── = separator support ───────────────────────────────────────────────────

        [Fact]
        public void Parse_EqualsSeparator_ParsesCorrectly()
        {
            var config = "Host=myserver\n    HostName=192.168.1.10\n    User=alice\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("alice@192.168.1.10", result["myserver"]);
        }

        [Fact]
        public void Parse_EqualsSeparatorWithSpaces_ParsesCorrectly()
        {
            var config = "Host = myserver\n    HostName = 192.168.1.10\n    Port = 2222\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("-p 2222", result["myserver"]);
            Assert.Contains("192.168.1.10", result["myserver"]);
        }

        [Fact]
        public void Parse_MixedSeparators_ParsesCorrectly()
        {
            var config = "Host myserver\n    HostName=10.0.0.1\n    User alice\n    Port=2222\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Contains("alice@10.0.0.1", result["myserver"]);
            Assert.Contains("-p 2222", result["myserver"]);
        }

        // ── Multiple aliases per Host line ────────────────────────────────────────

        [Fact]
        public void Parse_MultipleAliases_CreatesEntryForEach()
        {
            var config = "Host web1 web2 web3\n    HostName 10.0.0.1\n    User deploy\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("web1"), "web1 should be present");
            Assert.True(result.ContainsKey("web2"), "web2 should be present");
            Assert.True(result.ContainsKey("web3"), "web3 should be present");
        }

        [Fact]
        public void Parse_MultipleAliases_AllShareSameCommand()
        {
            var config = "Host web1 web2\n    HostName 10.0.0.1\n    User deploy\n";
            var result = ParseConfig(config);
            Assert.Equal(result["web1"], result["web2"]);
        }

        [Fact]
        public void Parse_WildcardInMultiAlias_SkipsEntireBlock()
        {
            var config = "Host web1 *.internal\n    HostName 10.0.0.1\n";
            var result = ParseConfig(config);
            Assert.Empty(result);
        }

        // ── Multiple hosts in file ────────────────────────────────────────────────

        [Fact]
        public void Parse_MultipleHostBlocks_AllParsed()
        {
            var config =
                "Host web\n    HostName 10.0.0.1\n    User www\n\n" +
                "Host db\n    HostName 10.0.0.2\n    User dbadmin\n";
            var result = ParseConfig(config);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("web"));
            Assert.True(result.ContainsKey("db"));
        }

        [Fact]
        public void Parse_LastBlockWithNoTrailingNewline_IsParsed()
        {
            var config = "Host final\n    HostName 10.0.0.99\n    User root";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("final"));
            Assert.Contains("root@10.0.0.99", result["final"]);
        }

        // ── Generated command format ──────────────────────────────────────────────

        [Fact]
        public void Parse_FullEntry_CommandStartsWithSsh()
        {
            var config = "Host myserver\n    HostName example.com\n    User alice\n    Port 2222\n";
            var result = ParseConfig(config);
            Assert.StartsWith("ssh", result["myserver"]);
        }
    }
}
