using System;
using System.Collections.Generic;
using System.IO;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Parses ~/.ssh/config and extracts Host entries.
    /// </summary>
    public static class SshConfigParser
    {
        /// <summary>
        /// Parses the SSH config file and returns a dictionary of Host → ssh command string.
        /// Wildcards (*) are skipped.
        /// </summary>
        public static Dictionary<string, string> Parse(string? configPath = null)
        {
            configPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "config");

            var hosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(configPath))
                return hosts;

            string? currentHost = null;
            string? hostName = null;
            string? user = null;
            string? port = null;
            string? identityFile = null;

            foreach (var rawLine in File.ReadLines(configPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                var spaceIdx = line.IndexOf(' ');
                if (spaceIdx < 0)
                    spaceIdx = line.IndexOf('\t');
                if (spaceIdx < 0)
                    continue;

                var key = line.Substring(0, spaceIdx).Trim();
                var value = line.Substring(spaceIdx + 1).Trim();

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentHost != null)
                        AddHostEntry(hosts, currentHost, hostName, user, port, identityFile);

                    if (value.Contains("*"))
                    {
                        currentHost = null;
                        hostName = user = port = identityFile = null;
                        continue;
                    }

                    currentHost = value;
                    hostName = user = port = identityFile = null;
                }
                else if (currentHost != null)
                {
                    if (key.Equals("HostName", StringComparison.OrdinalIgnoreCase))
                        hostName = value;
                    else if (key.Equals("User", StringComparison.OrdinalIgnoreCase))
                        user = value;
                    else if (key.Equals("Port", StringComparison.OrdinalIgnoreCase))
                        port = value;
                    else if (key.Equals("IdentityFile", StringComparison.OrdinalIgnoreCase))
                        identityFile = value;
                }
            }

            // Save last host entry
            if (currentHost != null)
                AddHostEntry(hosts, currentHost, hostName, user, port, identityFile);

            return hosts;
        }

        private static void AddHostEntry(
            Dictionary<string, string> hosts,
            string alias,
            string? hostName,
            string? user,
            string? port,
            string? identityFile)
        {
            var target = hostName ?? alias;
            var cmd = "ssh";

            if (!string.IsNullOrEmpty(identityFile))
                cmd += " -i " + SshCommandBuilder.QuoteArgument(identityFile);

            if (!string.IsNullOrEmpty(port) && port != "22")
                cmd += " -p " + port;

            if (!string.IsNullOrEmpty(user))
                cmd += " " + user + "@" + target;
            else
                cmd += " " + target;

            hosts[alias] = cmd;
        }
    }
}