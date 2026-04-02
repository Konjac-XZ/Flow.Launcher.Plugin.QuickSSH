using System.Collections.Generic;
using Xunit;

namespace Flow.Launcher.Plugin.QuickSSH.Tests
{
    /// <summary>
    /// Tests for <see cref="ProfileSerializer"/> — the human-readable SSH-config-like
    /// profile export/import format.
    /// </summary>
    public class ProfileSerializerTests
    {
        // ── Serialize ─────────────────────────────────────────────────────────────

        [Fact]
        public void Serialize_EmptyDictionary_ReturnsEmptyString()
        {
            var text = ProfileSerializer.Serialize(new Dictionary<string, SshProfile>());
            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public void Serialize_BasicSshProfile_ContainsHostLine()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["myserver"] = new SshProfile { Type = "ssh", HostName = "10.0.0.1", User = "root" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("Host myserver", text);
            Assert.Contains("Type ssh", text);
            Assert.Contains("HostName 10.0.0.1", text);
            Assert.Contains("User root", text);
        }

        [Fact]
        public void Serialize_ProfileWithPort_IncludesPort()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["srv"] = new SshProfile { Type = "ssh", HostName = "host", Port = "2222" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("Port 2222", text);
        }

        [Fact]
        public void Serialize_ProfileWithIdentityFile_IncludesIt()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["srv"] = new SshProfile { HostName = "host", IdentityFile = "~/.ssh/id_rsa" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("IdentityFile ~/.ssh/id_rsa", text);
        }

        [Fact]
        public void Serialize_IdentityFileWithSpaces_IsQuoted()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["srv"] = new SshProfile { HostName = "host", IdentityFile = @"C:\My Keys\id_rsa" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("IdentityFile \"", text);
        }

        [Fact]
        public void Serialize_ProfileWithRemoteCommand_IncludesIt()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["reboot-srv"] = new SshProfile { HostName = "host", RemoteCommand = "reboot" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("RemoteCommand reboot", text);
        }

        [Fact]
        public void Serialize_ProfileWithRequestTTY_IncludesIt()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["srv"] = new SshProfile { HostName = "host", RequestTTY = "force" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("RequestTTY force", text);
        }

        [Fact]
        public void Serialize_ProfileWithLocalForward_IncludesIt()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["tunnel"] = new SshProfile
                {
                    HostName = "host",
                    LocalForward = new List<string> { "8443 127.0.0.1:443", "8080 127.0.0.1:80" }
                }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("LocalForward 8443 127.0.0.1:443", text);
            Assert.Contains("LocalForward 8080 127.0.0.1:80", text);
        }

        [Fact]
        public void Serialize_ScpProfile_IncludesSourceAndTarget()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["upload"] = new SshProfile
                {
                    Type = "scp",
                    HostName = "host",
                    User = "root",
                    Source = @"C:\web\index.html",
                    Target = "/var/www/html/index.html"
                }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("Type scp", text);
            Assert.Contains("Source", text);
            Assert.Contains("Target", text);
        }

        [Fact]
        public void Serialize_ScpProfileWithRecursive_IncludesIt()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["backup"] = new SshProfile { Type = "scp", HostName = "host", Recursive = true }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("Recursive yes", text);
        }

        [Fact]
        public void Serialize_MultipleProfiles_AllIncluded()
        {
            var profiles = new Dictionary<string, SshProfile>
            {
                ["srv1"] = new SshProfile { HostName = "10.0.0.1" },
                ["srv2"] = new SshProfile { HostName = "10.0.0.2" }
            };
            var text = ProfileSerializer.Serialize(profiles);
            Assert.Contains("Host srv1", text);
            Assert.Contains("Host srv2", text);
        }

        // ── Deserialize ───────────────────────────────────────────────────────────

        [Fact]
        public void Deserialize_EmptyString_ReturnsEmpty()
        {
            var result = ProfileSerializer.Deserialize("");
            Assert.Empty(result);
        }

        [Fact]
        public void Deserialize_BasicProfile_ExtractsFields()
        {
            var text = "Host myserver\n    Type ssh\n    HostName 10.0.0.1\n    User root\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.True(result.ContainsKey("myserver"));
            Assert.Equal("10.0.0.1", result["myserver"].HostName);
            Assert.Equal("root", result["myserver"].User);
            Assert.Equal("ssh", result["myserver"].Type);
        }

        [Fact]
        public void Deserialize_ProfileWithPort_ExtractsPort()
        {
            var text = "Host srv\n    HostName host\n    Port 2222\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.Equal("2222", result["srv"].Port);
        }

        [Fact]
        public void Deserialize_ProfileWithRemoteCommand_ExtractsIt()
        {
            var text = "Host reboot-srv\n    HostName host\n    RemoteCommand reboot\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.Equal("reboot", result["reboot-srv"].RemoteCommand);
        }

        [Fact]
        public void Deserialize_ProfileWithQuotedRemoteCommand_UnquotesIt()
        {
            var text = "Host srv\n    HostName host\n    RemoteCommand \"shutdown -h now\"\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.Equal("shutdown -h now", result["srv"].RemoteCommand);
        }

        [Fact]
        public void Deserialize_ProfileWithLocalForward_ExtractsAll()
        {
            var text = "Host tunnel\n    HostName host\n    LocalForward 8443 127.0.0.1:443\n    LocalForward 8080 127.0.0.1:80\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.Equal(2, result["tunnel"].LocalForward.Count);
        }

        [Fact]
        public void Deserialize_ScpProfile_ExtractsSourceAndTarget()
        {
            var text = "Host upload\n    Type scp\n    HostName host\n    Source \"C:\\web\\index.html\"\n    Target /var/www/html/index.html\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.Equal("scp", result["upload"].Type);
            Assert.Equal(@"C:\web\index.html", result["upload"].Source);
            Assert.Equal("/var/www/html/index.html", result["upload"].Target);
        }

        [Fact]
        public void Deserialize_SkipsCommentLines()
        {
            var text = "# This is a comment\nHost srv\n    HostName host\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.True(result.ContainsKey("srv"));
            Assert.False(result.ContainsKey("# This is a comment"));
        }

        [Fact]
        public void Deserialize_MultipleHosts_AllParsed()
        {
            var text = "Host srv1\n    HostName 10.0.0.1\n\nHost srv2\n    HostName 10.0.0.2\n";
            var result = ProfileSerializer.Deserialize(text);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey("srv1"));
            Assert.True(result.ContainsKey("srv2"));
        }

        // ── Round-trip ────────────────────────────────────────────────────────────

        [Fact]
        public void RoundTrip_SshProfile_PreservesAllFields()
        {
            var original = new SshProfile
            {
                Type = "ssh",
                HostName = "10.0.0.150",
                User = "root",
                Port = "22",
                IdentityFile = "~/.ssh/private_key",
                IdentitiesOnly = true,
                RemoteCommand = "reboot",
                RequestTTY = "force"
            };
            var profiles = new Dictionary<string, SshProfile> { ["srv"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);

            Assert.True(restored.ContainsKey("srv"));
            var p = restored["srv"];
            Assert.Equal("ssh", p.Type);
            Assert.Equal("10.0.0.150", p.HostName);
            Assert.Equal("root", p.User);
            Assert.Equal("~/.ssh/private_key", p.IdentityFile);
            Assert.True(p.IdentitiesOnly);
            Assert.Equal("reboot", p.RemoteCommand);
            Assert.Equal("force", p.RequestTTY);
        }

        [Fact]
        public void RoundTrip_TunnelProfile_PreservesForwards()
        {
            var original = new SshProfile
            {
                Type = "ssh",
                HostName = "10.0.0.1",
                LocalForward = new List<string> { "8443 127.0.0.1:443", "8080 127.0.0.1:80" }
            };
            var profiles = new Dictionary<string, SshProfile> { ["tunnel"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);

            Assert.Equal(2, restored["tunnel"].LocalForward.Count);
            Assert.Contains("8443 127.0.0.1:443", restored["tunnel"].LocalForward);
        }

        [Fact]
        public void RoundTrip_ScpProfile_PreservesFields()
        {
            var original = new SshProfile
            {
                Type = "scp",
                HostName = "10.0.0.1",
                User = "root",
                Source = @"C:\web\index.html",
                Target = "/var/www/html/index.html",
                Recursive = true
            };
            var profiles = new Dictionary<string, SshProfile> { ["upload"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);

            Assert.Equal("scp", restored["upload"].Type);
            Assert.Equal(@"C:\web\index.html", restored["upload"].Source);
            Assert.True(restored["upload"].Recursive);
        }

        [Fact]
        public void RoundTrip_ScpProfile_HostNameAndUserPreserved()
        {
            // HostName and User must survive a full serialize → deserialize cycle
            // because BuildScpCommand() needs them to construct the remote endpoint.
            var original = new SshProfile
            {
                Type = "scp",
                HostName = "10.100.100.241",
                User = "root",
                Source = @"C:\web\index.html",
                Target = "/var/www/html/index.html"
            };
            var profiles = new Dictionary<string, SshProfile> { ["upload"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);

            Assert.Equal("10.100.100.241", restored["upload"].HostName);
            Assert.Equal("root", restored["upload"].User);
        }

        [Fact]
        public void RoundTrip_ScpReadmeCanonicalExample_CommandContainsRemoteEndpoint()
        {
            // Verify that the canonical SCP example from the README survives a full
            // serialize → deserialize cycle and produces a correct SCP command.
            // README example:
            //   Host Homepage-Upload
            //       Type scp
            //       HostName 10.100.100.241
            //       User root
            //       Source "C:\web\index.html"
            //       Target "/var/www/html/index.html"
            var original = new SshProfile
            {
                Type = "scp",
                HostName = "10.100.100.241",
                User = "root",
                Source = @"C:\web\index.html",
                Target = "/var/www/html/index.html"
            };
            var profiles = new Dictionary<string, SshProfile> { ["Homepage-Upload"] = original };

            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);
            var cmd = restored["Homepage-Upload"].ToCommandLine();

            Assert.StartsWith("scp", cmd);
            // Remote target must include user@host: prefix — built from canonical structured fields
            Assert.Contains("root@10.100.100.241:/var/www/html/index.html", cmd);
        }

        [Fact]
        public void RoundTrip_ScpDownloadProfile_CommandContainsRemoteSource()
        {
            // Download profile: Source is bare remote path, Target is Windows local path.
            var original = new SshProfile
            {
                Type = "scp",
                HostName = "10.0.0.1",
                User = "root",
                Source = "/var/log/app.log",
                Target = @"C:\logs\app.log"
            };
            var profiles = new Dictionary<string, SshProfile> { ["download"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);
            var cmd = restored["download"].ToCommandLine();

            Assert.StartsWith("scp", cmd);
            // Remote source must include user@host: prefix
            Assert.Contains("root@10.0.0.1:/var/log/app.log", cmd);
        }

        [Fact]
        public void RoundTrip_ProfileWithExtraArgs_PreservesExtraArgs()
        {
            // ExtraArgs holds flags that could not be represented as structured fields.
            // They must survive a full serialize → deserialize cycle unchanged.
            var original = new SshProfile
            {
                Type = "ssh",
                HostName = "10.0.0.1",
                User = "root",
                ExtraArgs = "-X -A"
            };
            var profiles = new Dictionary<string, SshProfile> { ["srv"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);

            Assert.True(restored.ContainsKey("srv"));
            Assert.Equal("-X -A", restored["srv"].ExtraArgs);
        }

        [Fact]
        public void RoundTrip_ExtraArgs_ThenToCommandLine_IncludesExtraArgs()
        {
            // After a serialize → deserialize round-trip, ToCommandLine must still include ExtraArgs.
            var original = new SshProfile
            {
                Type = "ssh",
                HostName = "host",
                User = "root",
                ExtraArgs = "-X"
            };
            var profiles = new Dictionary<string, SshProfile> { ["srv"] = original };
            var text = ProfileSerializer.Serialize(profiles);
            var restored = ProfileSerializer.Deserialize(text);

            var cmd = restored["srv"].ToCommandLine();
            Assert.Contains("-X", cmd);
        }

        [Fact]
        public void RoundTrip_Deterministic_SecondRoundTripMatchesFirst()
        {
            // Parse → serialize → deserialize → serialize must produce the same text.
            // This verifies there are no fields that expand or collapse on repeated round-trips.
            var original = new SshProfile
            {
                Type = "ssh",
                HostName = "10.0.0.150",
                User = "root",
                Port = "2222",
                IdentityFile = "~/.ssh/id_rsa",
                IdentitiesOnly = true,
                RemoteCommand = "reboot",
                RequestTTY = "force",
                LocalForward = new List<string> { "8443 127.0.0.1:443" },
                ProxyJump = "bastion.example.com"
            };
            var profiles = new Dictionary<string, SshProfile> { ["srv"] = original };

            var text1 = ProfileSerializer.Serialize(profiles);
            var restored1 = ProfileSerializer.Deserialize(text1);
            var text2 = ProfileSerializer.Serialize(restored1);

            Assert.Equal(text1, text2);
        }
    }
}
