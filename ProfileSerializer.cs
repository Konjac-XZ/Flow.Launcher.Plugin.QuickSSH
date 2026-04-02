using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Serializes and deserializes saved profiles using a human-readable SSH-config-like
    /// text format. This format is used by "profiles export" and "profiles import".
    /// It is intentionally similar to OpenSSH config syntax but is NOT a strict clone —
    /// it supports QuickSSH-specific fields such as Type, Source, Target, and RequestTTY.
    /// </summary>
    public static class ProfileSerializer
    {
        // ── Serialization ─────────────────────────────────────────────────────────

        /// <summary>
        /// Serializes a dictionary of named profiles to the SSH-config-like text format.
        /// </summary>
        public static string Serialize(IReadOnlyDictionary<string, SshProfile> profiles)
        {
            if (profiles == null || profiles.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            bool first = true;

            foreach (var kvp in profiles)
            {
                if (!first)
                    sb.AppendLine();
                first = false;

                var name = kvp.Key;
                var p = kvp.Value;

                sb.AppendLine("Host " + name);

                AppendField(sb, "Type", p.Type ?? "ssh");

                AppendField(sb, "HostName", p.HostName);
                AppendField(sb, "User", p.User);
                AppendField(sb, "Port", p.Port);
                AppendFieldQuoted(sb, "IdentityFile", p.IdentityFile);

                if (p.IdentitiesOnly)
                    AppendField(sb, "IdentitiesOnly", "yes");

                bool isScp = string.Equals(p.Type, "scp", StringComparison.OrdinalIgnoreCase);

                if (!isScp)
                {
                    // SSH-specific
                    AppendFieldQuoted(sb, "RemoteCommand", p.RemoteCommand);
                    AppendField(sb, "RequestTTY", p.RequestTTY);

                    if (p.LocalForward != null)
                        foreach (var fwd in p.LocalForward)
                            if (!string.IsNullOrEmpty(fwd))
                                AppendField(sb, "LocalForward", fwd);

                    if (p.RemoteForward != null)
                        foreach (var fwd in p.RemoteForward)
                            if (!string.IsNullOrEmpty(fwd))
                                AppendField(sb, "RemoteForward", fwd);

                    AppendField(sb, "DynamicForward", p.DynamicForward);
                    AppendField(sb, "ProxyJump", p.ProxyJump);
                    AppendFieldQuoted(sb, "ProxyCommand", p.ProxyCommand);
                }
                else
                {
                    // SCP-specific
                    AppendFieldQuoted(sb, "Source", p.Source);
                    AppendFieldQuoted(sb, "Target", p.Target);

                    if (p.Recursive)
                        AppendField(sb, "Recursive", "yes");
                    if (p.PreserveTimes)
                        AppendField(sb, "PreserveTimes", "yes");
                    if (p.Compression)
                        AppendField(sb, "Compression", "yes");
                }

                AppendFieldQuoted(sb, "ExtraArgs", p.ExtraArgs);
            }

            return sb.ToString();
        }

        // ── Deserialization ───────────────────────────────────────────────────────

        /// <summary>
        /// Parses the SSH-config-like text format back into a dictionary of named profiles.
        /// </summary>
        public static Dictionary<string, SshProfile> Deserialize(string text)
        {
            var profiles = new Dictionary<string, SshProfile>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(text))
                return profiles;

            string currentHost = null;
            SshProfile current = null;

            foreach (var rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
            {
                var line = rawLine.Trim();

                // Skip blank lines and comments
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (!TrySplitKeyValue(line, out var key, out var value))
                    continue;

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    // Flush previous block
                    if (currentHost != null && current != null)
                        profiles[currentHost] = current;

                    currentHost = value;
                    current = new SshProfile();
                    continue;
                }

                if (current == null)
                    continue;

                ApplyField(current, key, value);
            }

            // Flush last block
            if (currentHost != null && current != null)
                profiles[currentHost] = current;

            return profiles;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void AppendField(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            sb.Append("    ").Append(key).Append(' ').AppendLine(value);
        }

        /// <summary>
        /// Appends a field value, quoting it with double-quotes when it contains spaces.
        /// </summary>
        private static void AppendFieldQuoted(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            // Quote if it contains whitespace
            bool needsQuote = value.IndexOfAny(new[] { ' ', '\t' }) >= 0;
            var formatted = needsQuote ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
            sb.Append("    ").Append(key).Append(' ').AppendLine(formatted);
        }

        private static void ApplyField(SshProfile p, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "type":
                    p.Type = value;
                    break;
                case "hostname":
                    p.HostName = value;
                    break;
                case "user":
                    p.User = value;
                    break;
                case "port":
                    p.Port = value;
                    break;
                case "identityfile":
                    p.IdentityFile = UnquoteValue(value);
                    break;
                case "identitiesonly":
                    p.IdentitiesOnly = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    break;
                case "remotecommand":
                    p.RemoteCommand = UnquoteValue(value);
                    break;
                case "requesttty":
                    p.RequestTTY = value;
                    break;
                case "localforward":
                    p.LocalForward ??= new List<string>();
                    p.LocalForward.Add(value);
                    break;
                case "remoteforward":
                    p.RemoteForward ??= new List<string>();
                    p.RemoteForward.Add(value);
                    break;
                case "dynamicforward":
                    p.DynamicForward = value;
                    break;
                case "proxyjump":
                    p.ProxyJump = value;
                    break;
                case "proxycommand":
                    p.ProxyCommand = UnquoteValue(value);
                    break;
                case "source":
                    p.Source = UnquoteValue(value);
                    break;
                case "target":
                    p.Target = UnquoteValue(value);
                    break;
                case "recursive":
                    p.Recursive = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    break;
                case "preservetimes":
                    p.PreserveTimes = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    break;
                case "compression":
                    p.Compression = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    break;
                case "extraargs":
                    p.ExtraArgs = UnquoteValue(value);
                    break;
            }
        }

        /// <summary>
        /// Splits a line into key and value (space-separated or equals-separated).
        /// </summary>
        internal static bool TrySplitKeyValue(string line, out string key, out string value)
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

            // Handle "Key = Value"
            if (!isEquals && value.StartsWith("=", StringComparison.Ordinal))
                value = value.Substring(1).TrimStart();

            return !string.IsNullOrEmpty(key);
        }

        /// <summary>
        /// Strips surrounding double-quotes and un-escapes inner \" sequences.
        /// </summary>
        private static string UnquoteValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2).Replace("\\\"", "\"");

            return value;
        }
    }
}
