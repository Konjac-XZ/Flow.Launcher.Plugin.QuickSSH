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

        private Dictionary<string, SshProfile> ParseConfig(string content)
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
            Assert.Equal("admin", result["myserver"].User);
            Assert.Equal("192.168.1.10", result["myserver"].HostName);
        }

        [Fact]
        public void Parse_HostWithNonDefaultPort_IncludesPort()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    Port 2222\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Equal("2222", result["myserver"].Port);
        }

        [Fact]
        public void Parse_HostWithDefaultPort22_OmitsPort()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    Port 22\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Null(result["myserver"].Port);
        }

        [Fact]
        public void Parse_HostWithIdentityFile_CapturesIt()
        {
            var config = "Host myserver\n    HostName 192.168.1.10\n    IdentityFile /home/user/.ssh/id_rsa\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Equal("/home/user/.ssh/id_rsa", result["myserver"].IdentityFile);
        }

        [Fact]
        public void Parse_HostNoHostName_UsesAliasAsHostName()
        {
            var config = "Host myserver\n    User alice\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Equal("myserver", result["myserver"].HostName);
            Assert.Equal("alice", result["myserver"].User);
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

        // ── Advanced SSH config fields ────────────────────────────────────────────

        [Fact]
        public void Parse_LocalForward_Captured()
        {
            var config = "Host proxy\n    HostName 10.0.0.1\n    LocalForward 8443 127.0.0.1:443\n";
            var result = ParseConfig(config);
            Assert.NotNull(result["proxy"].LocalForward);
            Assert.Contains("8443 127.0.0.1:443", result["proxy"].LocalForward);
        }

        [Fact]
        public void Parse_MultipleLocalForwards_AllCaptured()
        {
            var config =
                "Host proxy\n    HostName 10.0.0.1\n" +
                "    LocalForward 8443 127.0.0.1:443\n" +
                "    LocalForward 8080 127.0.0.1:80\n";
            var result = ParseConfig(config);
            Assert.Equal(2, result["proxy"].LocalForward.Count);
        }

        [Fact]
        public void Parse_ProxyJump_Captured()
        {
            var config = "Host internal\n    HostName 10.0.0.1\n    ProxyJump bastion.example.com\n";
            var result = ParseConfig(config);
            Assert.Equal("bastion.example.com", result["internal"].ProxyJump);
        }

        [Fact]
        public void Parse_IdentitiesOnly_Captured()
        {
            var config = "Host myserver\n    HostName 10.0.0.1\n    IdentitiesOnly yes\n";
            var result = ParseConfig(config);
            Assert.True(result["myserver"].IdentitiesOnly);
        }

        // ── = separator support ───────────────────────────────────────────────────

        [Fact]
        public void Parse_EqualsSeparator_ParsesCorrectly()
        {
            var config = "Host=myserver\n    HostName=192.168.1.10\n    User=alice\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Equal("alice", result["myserver"].User);
            Assert.Equal("192.168.1.10", result["myserver"].HostName);
        }

        [Fact]
        public void Parse_EqualsSeparatorWithSpaces_ParsesCorrectly()
        {
            var config = "Host = myserver\n    HostName = 192.168.1.10\n    Port = 2222\n";
            var result = ParseConfig(config);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Equal("2222", result["myserver"].Port);
            Assert.Equal("192.168.1.10", result["myserver"].HostName);
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
        public void Parse_MultipleAliases_AllShareSameHostName()
        {
            var config = "Host web1 web2\n    HostName 10.0.0.1\n    User deploy\n";
            var result = ParseConfig(config);
            Assert.Equal(result["web1"].HostName, result["web2"].HostName);
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
            Assert.Equal("root", result["final"].User);
            Assert.Equal("10.0.0.99", result["final"].HostName);
        }

        // ── ToCommandLine consistency ─────────────────────────────────────────────

        [Fact]
        public void Parse_SimpleHost_CommandLineStartsWithSsh()
        {
            var config = "Host myserver\n    HostName example.com\n    User alice\n    Port 2222\n";
            var result = ParseConfig(config);
            Assert.StartsWith("ssh", result["myserver"].ToCommandLine());
        }

        [Fact]
        public void Parse_HostWithPort_CommandLineContainsPort()
        {
            var config = "Host myserver\n    HostName example.com\n    Port 2222\n";
            var result = ParseConfig(config);
            Assert.Contains("-p 2222", result["myserver"].ToCommandLine());
        }
    }
}
