using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Parses ~/.ssh/config and extracts Host entries as structured <see cref="SshProfile"/> objects.
    /// </summary>
    public static class SshConfigParser
    {
        /// <summary>
        /// Parses the SSH config file and returns a dictionary of Host alias → <see cref="SshProfile"/>.
        /// Wildcard patterns (containing * or ?) are skipped.
        /// Supports both space-separated and '='-separated key/value pairs as per ssh_config(5).
        /// Multiple host aliases on a single Host line are each stored as separate entries.
        /// </summary>
        public static Dictionary<string, SshProfile> Parse(string? configPath = null)
        {
            configPath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh", "config");

            var profiles = new Dictionary<string, SshProfile>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(configPath))
                return profiles;

            // Current Host block state
            var currentAliases = new List<string>();
            string? hostName = null;
            string? user = null;
            string? port = null;
            string? identityFile = null;
            bool identitiesOnly = false;
            var localForwards = new List<string>();
            var remoteForwards = new List<string>();
            string? dynamicForward = null;
            string? proxyJump = null;
            string? proxyCommand = null;

            foreach (var rawLine in File.ReadLines(configPath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                if (!TrySplitKeyValue(line, out var key, out var value))
                    continue;

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    // Flush previous Host block before starting a new one.
                    if (currentAliases.Count > 0)
                    {
                        foreach (var alias in currentAliases)
                            AddEntry(profiles, alias, hostName, user, port, identityFile,
                                identitiesOnly, localForwards, remoteForwards, dynamicForward,
                                proxyJump, proxyCommand);
                    }

                    // Reset block state
                    currentAliases.Clear();
                    hostName = user = port = identityFile = dynamicForward =
                        proxyJump = proxyCommand = null;
                    identitiesOnly = false;
                    localForwards = new List<string>();
                    remoteForwards = new List<string>();

                    var aliases = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (aliases.Any(a => a.Contains('*') || a.Contains('?')))
                        continue; // skip wildcard blocks

                    currentAliases.AddRange(aliases);
                }
                else if (currentAliases.Count > 0)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "hostname":       hostName = value;       break;
                        case "user":           user = value;           break;
                        case "port":           port = value;           break;
                        case "identityfile":   identityFile = value;   break;
                        case "identitiesonly":
                            identitiesOnly = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "localforward":   localForwards.Add(value);  break;
                        case "remoteforward":  remoteForwards.Add(value); break;
                        case "dynamicforward": dynamicForward = value;    break;
                        case "proxyjump":      proxyJump = value;         break;
                        case "proxycommand":   proxyCommand = value;      break;
                    }
                }
            }

            // Flush the last Host block.
            if (currentAliases.Count > 0)
            {
                foreach (var alias in currentAliases)
                    AddEntry(profiles, alias, hostName, user, port, identityFile,
                        identitiesOnly, localForwards, remoteForwards, dynamicForward,
                        proxyJump, proxyCommand);
            }

            return profiles;
        }

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

            // Handle "Key = Value": space won the separator race but value still starts with "="
            if (!isEquals && value.StartsWith("="))
                value = value.Substring(1).TrimStart();

            return !string.IsNullOrEmpty(key);
        }

        private static void AddEntry(
            Dictionary<string, SshProfile> profiles,
            string alias,
            string? hostName,
            string? user,
            string? port,
            string? identityFile,
            bool identitiesOnly,
            List<string> localForwards,
            List<string> remoteForwards,
            string? dynamicForward,
            string? proxyJump,
            string? proxyCommand)
        {
            var profile = new SshProfile
            {
                Type = "ssh",
                HostName = hostName ?? alias,
                User = string.IsNullOrEmpty(user) ? null : user,
                Port = string.IsNullOrEmpty(port) || port == "22" ? null : port,
                IdentityFile = string.IsNullOrEmpty(identityFile) ? null : identityFile,
                IdentitiesOnly = identitiesOnly,
                LocalForward = localForwards.Count > 0 ? new List<string>(localForwards) : null,
                RemoteForward = remoteForwards.Count > 0 ? new List<string>(remoteForwards) : null,
                DynamicForward = string.IsNullOrEmpty(dynamicForward) ? null : dynamicForward,
                ProxyJump = string.IsNullOrEmpty(proxyJump) ? null : proxyJump,
                ProxyCommand = string.IsNullOrEmpty(proxyCommand) ? null : proxyCommand,
            };

            profiles[alias] = profile;
        }
    }
}
