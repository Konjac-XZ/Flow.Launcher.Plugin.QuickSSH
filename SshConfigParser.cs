using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Parses ~/.ssh/config and extracts Host entries.
    /// </summary>
    public static class SshConfigParser
    {
        /// <summary>
        /// Parses the SSH config file and returns a dictionary of Host alias → ssh command string.
        /// Wildcard patterns (containing * or ?) are skipped.
        /// Supports both space-separated and '='-separated key/value pairs as per ssh_config(5).
        /// Multiple host aliases on a single Host line are each stored as separate entries.
        /// </summary>
        public static Dictionary<string, string> Parse(string? configPath = null)
        {
            configPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "config");

            var hosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(configPath))
                return hosts;

            // Current Host block state
            var currentAliases = new List<string>();
            string? hostName = null;
            string? user = null;
            string? port = null;
            string? identityFile = null;

            foreach (var rawLine in File.ReadLines(configPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // SSH config allows both "Key Value" and "Key=Value" (with optional whitespace
                // around the '=').  Find the first separator character to split key from value.
                if (!TrySplitKeyValue(line, out var key, out var value))
                    continue;

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    // Flush previous Host block before starting a new one.
                    if (currentAliases.Count > 0)
                    {
                        foreach (var alias in currentAliases)
                            AddHostEntry(hosts, alias, hostName, user, port, identityFile);
                    }

                    currentAliases.Clear();
                    hostName = user = port = identityFile = null;

                    // A single Host line may declare multiple space-separated aliases,
                    // e.g. "Host web1 web2 web3".  Wildcard aliases (*) cause the whole
                    // block to be skipped because they represent catch-all rules.
                    var aliases = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (aliases.Any(a => a.Contains('*') || a.Contains('?')))
                        continue; // skip wildcard blocks

                    currentAliases.AddRange(aliases);
                }
                else if (currentAliases.Count > 0)
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

            // Flush the last Host block.
            if (currentAliases.Count > 0)
            {
                foreach (var alias in currentAliases)
                    AddHostEntry(hosts, alias, hostName, user, port, identityFile);
            }

            return hosts;
        }

        /// <summary>
        /// Splits a config line into key and value.
        /// Accepts both "Key Value" and "Key=Value" (with optional whitespace around '=').
        /// Returns false if no separator is found.
        /// </summary>
        private static bool TrySplitKeyValue(string line, out string key, out string value)
        {
            var eqIdx = line.IndexOf('=');
            var spIdx = line.IndexOfAny(new[] { ' ', '\t' });

            int sepIdx;
            bool isEquals;
            if (eqIdx >= 0 && (spIdx < 0 || eqIdx <= spIdx))
            {
                sepIdx = eqIdx;
                isEquals = true;
            }
            else if (spIdx >= 0)
            {
                sepIdx = spIdx;
                isEquals = false;
            }
            else
            {
                key = value = string.Empty;
                return false;
            }

            key = line.Substring(0, sepIdx).TrimEnd();
            value = isEquals
                ? line.Substring(sepIdx + 1).TrimStart()
                : line.Substring(sepIdx + 1).Trim();
            return !string.IsNullOrEmpty(key);
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