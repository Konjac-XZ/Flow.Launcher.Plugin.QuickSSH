using System;
using System.Collections.Generic;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Tests for <see cref="SshProfile.ToCommandLine"/> and
    /// <see cref="SshProfile.ParseFromLegacyCommand"/> (migration helper).
    /// </summary>
    public class SshProfileTests
    {
        // ── ToCommandLine — SSH ───────────────────────────────────────────────────

        [Fact]
        public void ToCommandLine_BasicSsh_ProducesExpectedCommand()
        {
            var p = new SshProfile { Type = "ssh", User = "root", HostName = "10.0.0.1" };
            Assert.Equal("ssh root@10.0.0.1", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithPort_IncludesPort()
        {
            var p = new SshProfile { Type = "ssh", User = "root", HostName = "host", Port = "2222" };
            Assert.Contains("-p 2222", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithDefaultPort22_OmitsPort()
        {
            var p = new SshProfile { Type = "ssh", User = "root", HostName = "host", Port = "22" };
            Assert.DoesNotContain("-p", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithIdentityFile_IncludesFlag()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", IdentityFile = "/home/user/.ssh/id_rsa" };
            Assert.Contains("-i /home/user/.ssh/id_rsa", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithSpaceInIdentityFile_QuotesIt()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", IdentityFile = @"C:\My Keys\id_rsa" };
            Assert.Contains("-i \"", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithIdentitiesOnly_IncludesOption()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", IdentitiesOnly = true };
            Assert.Contains("-o IdentitiesOnly=yes", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithRemoteCommand_AppendsAtEnd()
        {
            var p = new SshProfile { Type = "ssh", User = "root", HostName = "host", RemoteCommand = "reboot" };
            var cmd = p.ToCommandLine();
            Assert.EndsWith("\"reboot\"", cmd);
        }

        [Fact]
        public void ToCommandLine_SshWithRequestTtyForce_ProducesDoubleDashT()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", RequestTTY = "force" };
            Assert.Contains("-t -t", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithRequestTtyYes_ProducesSingleDashT()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", RequestTTY = "yes" };
            Assert.Contains(" -t ", p.ToCommandLine());
            Assert.DoesNotContain("-t -t", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithRequestTtyNo_ProducesDashT_Upper()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", RequestTTY = "no" };
            Assert.Contains("-T", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithLocalForward_IncludesFlag()
        {
            var p = new SshProfile
            {
                Type = "ssh",
                HostName = "host",
                LocalForward = new List<string> { "8443 127.0.0.1:443" }
            };
            Assert.Contains("-L 8443 127.0.0.1:443", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithMultipleLocalForwards_AllIncluded()
        {
            var p = new SshProfile
            {
                Type = "ssh",
                HostName = "host",
                LocalForward = new List<string> { "8443 127.0.0.1:443", "8080 127.0.0.1:80" }
            };
            var cmd = p.ToCommandLine();
            Assert.Contains("-L 8443 127.0.0.1:443", cmd);
            Assert.Contains("-L 8080 127.0.0.1:80", cmd);
        }

        [Fact]
        public void ToCommandLine_SshWithDynamicForward_IncludesDashD()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", DynamicForward = "1080" };
            Assert.Contains("-D 1080", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_SshWithProxyJump_IncludesDashJ()
        {
            var p = new SshProfile { Type = "ssh", HostName = "host", ProxyJump = "bastion.example.com" };
            Assert.Contains("-J bastion.example.com", p.ToCommandLine());
        }

        // ── ToCommandLine — SCP ───────────────────────────────────────────────────

        [Fact]
        public void ToCommandLine_BasicScp_StartWithScp()
        {
            var p = new SshProfile
            {
                Type = "scp",
                User = "root",
                HostName = "10.0.0.1",
                Source = @"C:\web\index.html",
                Target = "/var/www/html/index.html"
            };
            Assert.StartsWith("scp", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_ScpRecursive_IncludesDashR()
        {
            var p = new SshProfile { Type = "scp", HostName = "host", Recursive = true };
            Assert.Contains("-r", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_ScpCompression_IncludesDashC()
        {
            var p = new SshProfile { Type = "scp", HostName = "host", Compression = true };
            Assert.Contains("-C", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_ScpPreserveTimes_IncludesDashP()
        {
            var p = new SshProfile { Type = "scp", HostName = "host", PreserveTimes = true };
            Assert.Contains(" -p", p.ToCommandLine());
        }

        [Fact]
        public void ToCommandLine_ScpWithPort_UsesDashUpperP()
        {
            var p = new SshProfile { Type = "scp", HostName = "host", Port = "2222" };
            Assert.Contains("-P 2222", p.ToCommandLine());
        }

        // ── ParseFromLegacyCommand — SSH ──────────────────────────────────────────

        [Fact]
        public void ParseFromLegacy_BasicSshUserAtHost_ExtractsFields()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh root@10.0.0.1");
            Assert.Equal("ssh", p.Type);
            Assert.Equal("root", p.User);
            Assert.Equal("10.0.0.1", p.HostName);
        }

        [Fact]
        public void ParseFromLegacy_SshWithPort_ExtractsPort()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh -p 2222 dev@10.0.0.50");
            Assert.Equal("2222", p.Port);
            Assert.Equal("dev", p.User);
        }

        [Fact]
        public void ParseFromLegacy_SshWithIdentityFile_ExtractsKey()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh -i /home/user/.ssh/id_rsa root@host");
            Assert.Equal("/home/user/.ssh/id_rsa", p.IdentityFile);
            Assert.Equal("root", p.User);
        }

        [Fact]
        public void ParseFromLegacy_SshWithQuotedIdentityFile_ExtractsKey()
        {
            var p = SshProfile.ParseFromLegacyCommand(@"ssh -i ""C:\Users\info\.ssh\key"" root@host");
            Assert.Contains(@"C:\Users\info\.ssh\key", p.IdentityFile);
        }

        [Fact]
        public void ParseFromLegacy_SshWithRemoteCommand_ExtractsRemoteCommand()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh root@host reboot");
            Assert.Equal("root", p.User);
            Assert.Equal("host", p.HostName);
            Assert.Equal("reboot", p.RemoteCommand);
        }

        [Fact]
        public void ParseFromLegacy_SshWithIdentitiesOnly_ExtractsBoolField()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh -o IdentitiesOnly=yes root@host");
            Assert.True(p.IdentitiesOnly);
        }

        [Fact]
        public void ParseFromLegacy_SshWithTTyForce_ExtractsRequestTty()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh -t -t root@host reboot");
            Assert.Equal("force", p.RequestTTY);
        }

        [Fact]
        public void ParseFromLegacy_BareDestination_PrependsSsh()
        {
            var p = SshProfile.ParseFromLegacyCommand("root@host");
            Assert.Equal("ssh", p.Type);
            Assert.Equal("root", p.User);
            Assert.Equal("host", p.HostName);
        }

        // ── ParseFromLegacyCommand — SCP ──────────────────────────────────────────

        [Fact]
        public void ParseFromLegacy_ScpCommand_SetTypeToScp()
        {
            var p = SshProfile.ParseFromLegacyCommand(@"scp C:\file.txt root@host:/remote/file.txt");
            Assert.Equal("scp", p.Type);
        }

        [Fact]
        public void ParseFromLegacy_ScpWithRecursive_SetsRecursive()
        {
            var p = SshProfile.ParseFromLegacyCommand("scp -r /local/ root@host:/remote/");
            Assert.True(p.Recursive);
        }

        // ── SCP: Windows-path safety ──────────────────────────────────────────────

        [Fact]
        public void ParseFromLegacy_ScpUpload_WindowsLocalSource_NotMisdetectedAsRemote()
        {
            // C:\file.txt has a colon but must NOT be treated as a remote SCP endpoint.
            // The remote spec is the second positional (root@host:/path).
            var p = SshProfile.ParseFromLegacyCommand(@"scp C:\file.txt root@host:/remote/file.txt");
            Assert.Equal("scp", p.Type);
            Assert.Equal("host", p.HostName);
            Assert.Equal("root", p.User);
            // Source is the bare local path; it must not contain 'host' or 'root@'
            Assert.Equal(@"C:\file.txt", p.Source);
            // Target is the bare remote path (no user@host: prefix)
            Assert.Equal("/remote/file.txt", p.Target);
        }

        [Fact]
        public void ParseFromLegacy_ScpUpload_WindowsSourceWithSpaces_NotMisdetectedAsRemote()
        {
            var p = SshProfile.ParseFromLegacyCommand(@"scp ""C:\My Documents\file.txt"" root@host:/remote/");
            Assert.Equal("host", p.HostName);
            Assert.Equal(@"C:\My Documents\file.txt", p.Source);
            Assert.Equal("/remote/", p.Target);
        }

        [Fact]
        public void ParseFromLegacy_ScpDownload_WindowsLocalTarget_ExtractsUserHostFromSource()
        {
            // Download: remote spec is the first positional, local Windows path is second.
            var p = SshProfile.ParseFromLegacyCommand(@"scp root@host:/remote/file.txt C:\local\file.txt");
            Assert.Equal("scp", p.Type);
            Assert.Equal("host", p.HostName);
            Assert.Equal("root", p.User);
            // Source is the bare remote path (no user@host: prefix)
            Assert.Equal("/remote/file.txt", p.Source);
            // Target is the local Windows path, unchanged
            Assert.Equal(@"C:\local\file.txt", p.Target);
        }

        [Fact]
        public void ParseFromLegacy_ScpUpload_ExtractsUserHostFromTargetPositional()
        {
            // Upload: local source is first, remote spec is second.
            var p = SshProfile.ParseFromLegacyCommand("scp /local/file.txt admin@10.0.0.50:/remote/");
            Assert.Equal("10.0.0.50", p.HostName);
            Assert.Equal("admin", p.User);
            Assert.Equal("/local/file.txt", p.Source);
            Assert.Equal("/remote/", p.Target);
        }

        // ── ToCommandLine — SCP canonical model ───────────────────────────────────

        [Fact]
        public void ToCommandLine_ScpCanonicalUpload_BuildsRemoteTarget()
        {
            // Canonical upload: Source is local Windows path, Target is bare remote path.
            // Command must be: scp source user@host:target
            var p = new SshProfile
            {
                Type = "scp",
                User = "root",
                HostName = "10.0.0.1",
                Source = @"C:\web\index.html",
                Target = "/var/www/html/index.html"
            };
            var cmd = p.ToCommandLine();
            Assert.StartsWith("scp", cmd);
            // Remote target must include user@host: prefix
            Assert.Contains("root@10.0.0.1:/var/www/html/index.html", cmd);
            // Source must appear verbatim (without host prefix)
            Assert.DoesNotContain("root@10.0.0.1:/web", cmd);
        }

        [Fact]
        public void ToCommandLine_ScpCanonicalDownload_BuildsRemoteSource()
        {
            // Canonical download: Target is local Windows path, Source is bare remote path.
            // Command must be: scp user@host:source target
            var p = new SshProfile
            {
                Type = "scp",
                User = "root",
                HostName = "10.0.0.1",
                Source = "/remote/data.tar.gz",
                Target = @"C:\downloads\data.tar.gz"
            };
            var cmd = p.ToCommandLine();
            Assert.StartsWith("scp", cmd);
            // Remote source must include user@host: prefix
            Assert.Contains("root@10.0.0.1:/remote/data.tar.gz", cmd);
        }

        [Fact]
        public void ToCommandLine_ScpCanonicalUpload_UserAndHostInRemoteTarget()
        {
            // Verify the remote endpoint is exactly user@host:target (not bare target).
            var p = new SshProfile
            {
                Type = "scp",
                User = "deploy",
                HostName = "srv.example.com",
                Source = @"C:\build\app.zip",
                Target = "/opt/deploy/app.zip"
            };
            var cmd = p.ToCommandLine();
            Assert.Contains("deploy@srv.example.com:/opt/deploy/app.zip", cmd);
        }

        [Fact]
        public void ToCommandLine_ScpLegacyParsed_ThenBuildCommand_RoundTripPreservesIntent()
        {
            // Parse a legacy upload command → canonical model → rebuild command.
            // The rebuilt command must be semantically equivalent.
            var p = SshProfile.ParseFromLegacyCommand(@"scp C:\web\index.html root@10.0.0.1:/var/www/html/index.html");
            var cmd = p.ToCommandLine();
            Assert.StartsWith("scp", cmd);
            Assert.Contains("root@10.0.0.1:/var/www/html/index.html", cmd);
        }

        [Fact]
        public void ToCommandLine_ScpLegacyDownload_ThenBuildCommand_RoundTripPreservesIntent()
        {
            var p = SshProfile.ParseFromLegacyCommand(@"scp root@10.0.0.1:/var/log/app.log C:\logs\app.log");
            var cmd = p.ToCommandLine();
            Assert.StartsWith("scp", cmd);
            Assert.Contains("root@10.0.0.1:/var/log/app.log", cmd);
        }

        // ── Round-trip: parse legacy → ToCommandLine ──────────────────────────────

        [Fact]
        public void ParseFromLegacy_ThenToCommandLine_ProducesEquivalentCommand()
        {
            var original = "ssh -i /key root@10.0.0.1";
            var p = SshProfile.ParseFromLegacyCommand(original);
            var rebuilt = p.ToCommandLine();
            // Both should be valid SSH commands with the same semantic content
            Assert.StartsWith("ssh", rebuilt);
            Assert.Contains("root@10.0.0.1", rebuilt);
            Assert.Contains("-i /key", rebuilt);
        }

        // ── ExtraArgs: unparseable flag fallback ──────────────────────────────────

        [Fact]
        public void ParseFromLegacy_UnknownFlag_PreservedInExtraArgs()
        {
            // "-X" (X11 forwarding) is not a recognised structured field.
            var p = SshProfile.ParseFromLegacyCommand("ssh -X root@host");
            Assert.NotNull(p.ExtraArgs);
            Assert.Contains("-X", p.ExtraArgs);
        }

        [Fact]
        public void ParseFromLegacy_MultipleUnknownFlags_AllPreservedInExtraArgs()
        {
            var p = SshProfile.ParseFromLegacyCommand("ssh -A -X root@host");
            // Both -A (agent forwarding) and -X should end up in ExtraArgs
            Assert.NotNull(p.ExtraArgs);
            Assert.Contains("-A", p.ExtraArgs);
            Assert.Contains("-X", p.ExtraArgs);
        }

        [Fact]
        public void ParseFromLegacy_UnknownFlag_NoSilentDegradation_HostNameStillExtracted()
        {
            // Even when there are unknown flags, known fields must still be extracted correctly.
            var p = SshProfile.ParseFromLegacyCommand("ssh -X root@myhost");
            Assert.Equal("root", p.User);
            Assert.Equal("myhost", p.HostName);
            Assert.Contains("-X", p.ExtraArgs);
        }

        [Fact]
        public void ToCommandLine_WithExtraArgs_IncludesVerbatimBeforeDestination()
        {
            // ExtraArgs must appear verbatim in the generated command, before the destination.
            var p = new SshProfile { Type = "ssh", HostName = "host", User = "root", ExtraArgs = "-X -A" };
            var cmd = p.ToCommandLine();
            Assert.Contains("-X -A", cmd);
            // ExtraArgs must come before "root@host"
            int extraArgsIdx = cmd.IndexOf("-X -A", StringComparison.Ordinal);
            int destIdx = cmd.IndexOf("root@host", StringComparison.Ordinal);
            Assert.True(extraArgsIdx < destIdx,
                "ExtraArgs should appear before the destination in the command line.");
        }

        [Fact]
        public void ParseFromLegacy_UnknownFlagWithValue_PreservedInExtraArgs()
        {
            // "-o ServerAliveInterval=60" uses the -o key=value form; the key is not in our
            // explicit structured fields so the whole -o option goes into ExtraArgs.
            var p = SshProfile.ParseFromLegacyCommand("ssh -o ServerAliveInterval=60 root@host");
            // Either ExtraArgs contains it, or (future enhancement) it's parsed explicitly.
            // For now the contract is: no data is lost.
            var cmd = p.ToCommandLine();
            Assert.Contains("ServerAliveInterval=60", cmd);
        }
    }
}
