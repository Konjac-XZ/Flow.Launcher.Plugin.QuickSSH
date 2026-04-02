using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Canonical structured model for a saved SSH or SCP profile.
    /// </summary>
    public class SshProfile
    {
        // ── Common fields ─────────────────────────────────────────────────────────

        /// <summary>Profile connection type: "ssh" (default) or "scp".</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; } = "ssh";

        /// <summary>Hostname or IP address of the remote server.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string HostName { get; set; }

        /// <summary>Remote user name.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string User { get; set; }

        /// <summary>SSH/SCP port (omitted from the command when "22" or null/empty).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Port { get; set; }

        /// <summary>Path to the private key file (-i flag).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string IdentityFile { get; set; }

        /// <summary>Equivalent to -o IdentitiesOnly=yes when true.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IdentitiesOnly { get; set; }

        // ── SSH-specific fields ───────────────────────────────────────────────────

        /// <summary>Command to execute on the remote host.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string RemoteCommand { get; set; }

        /// <summary>
        /// Pseudo-terminal allocation: "force" (-t -t), "yes" (-t), "no" (-T).
        /// Null means no explicit flag.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string RequestTTY { get; set; }

        /// <summary>Local port-forward specs (e.g. "8443 127.0.0.1:443").</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> LocalForward { get; set; }

        /// <summary>Remote port-forward specs.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> RemoteForward { get; set; }

        /// <summary>Dynamic port-forward (SOCKS proxy) spec.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DynamicForward { get; set; }

        /// <summary>Jump host(s) for ProxyJump (-J).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProxyJump { get; set; }

        /// <summary>ProxyCommand string.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProxyCommand { get; set; }

        /// <summary>
        /// Extra/raw SSH arguments not captured by the structured fields above.
        /// Appended verbatim before the destination.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ExtraArgs { get; set; }

        // ── SCP-specific fields ───────────────────────────────────────────────────

        /// <summary>Source path (local or remote) for scp.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }

        /// <summary>Target path (local or remote) for scp.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Target { get; set; }

        /// <summary>Recursive transfer (-r).</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Recursive { get; set; }

        /// <summary>Preserve modification times and permissions (-p).</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool PreserveTimes { get; set; }

        /// <summary>Enable compression (-C).</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Compression { get; set; }

        // ── Command building ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds the full command-line string for this profile (ssh or scp).
        /// </summary>
        public string ToCommandLine()
        {
            bool isScp = string.Equals(Type, "scp", StringComparison.OrdinalIgnoreCase);
            return isScp ? BuildScpCommand() : BuildSshCommand();
        }

        /// <summary>Returns a short human-readable summary for the Flow Launcher result subtitle.</summary>
        public string ToDisplayString()
        {
            return ToCommandLine();
        }

        private string BuildSshCommand()
        {
            var sb = new StringBuilder("ssh");

            if (!string.IsNullOrEmpty(IdentityFile))
                sb.Append(" -i ").Append(SshCommandBuilder.QuoteArgument(IdentityFile));

            if (IdentitiesOnly)
                sb.Append(" -o IdentitiesOnly=yes");

            if (!string.IsNullOrEmpty(Port) && Port != "22")
                sb.Append(" -p ").Append(Port);

            if (!string.IsNullOrEmpty(RequestTTY))
            {
                if (RequestTTY.Equals("force", StringComparison.OrdinalIgnoreCase))
                    sb.Append(" -t -t");
                else if (RequestTTY.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    sb.Append(" -t");
                else if (RequestTTY.Equals("no", StringComparison.OrdinalIgnoreCase))
                    sb.Append(" -T");
            }

            if (!string.IsNullOrEmpty(DynamicForward))
                sb.Append(" -D ").Append(DynamicForward);

            if (LocalForward != null)
                foreach (var fwd in LocalForward)
                    if (!string.IsNullOrEmpty(fwd))
                        sb.Append(" -L ").Append(fwd);

            if (RemoteForward != null)
                foreach (var fwd in RemoteForward)
                    if (!string.IsNullOrEmpty(fwd))
                        sb.Append(" -R ").Append(fwd);

            if (!string.IsNullOrEmpty(ProxyJump))
                sb.Append(" -J ").Append(ProxyJump);

            if (!string.IsNullOrEmpty(ProxyCommand))
                sb.Append(" -o ProxyCommand=").Append(SshCommandBuilder.QuoteArgument(ProxyCommand));

            if (!string.IsNullOrEmpty(ExtraArgs))
                sb.Append(" ").Append(ExtraArgs);

            // Destination
            var target = HostName ?? "";
            if (!string.IsNullOrEmpty(User))
                sb.Append(" ").Append(User).Append("@").Append(target);
            else if (!string.IsNullOrEmpty(target))
                sb.Append(" ").Append(target);

            if (!string.IsNullOrEmpty(RemoteCommand))
                sb.Append(" ").Append(SshCommandBuilder.QuoteArgument(RemoteCommand));

            return sb.ToString();
        }

        private string BuildScpCommand()
        {
            var sb = new StringBuilder("scp");

            if (Compression)
                sb.Append(" -C");

            if (Recursive)
                sb.Append(" -r");

            if (PreserveTimes)
                sb.Append(" -p");

            if (!string.IsNullOrEmpty(IdentityFile))
                sb.Append(" -i ").Append(SshCommandBuilder.QuoteArgument(IdentityFile));

            if (IdentitiesOnly)
                sb.Append(" -o IdentitiesOnly=yes");

            if (!string.IsNullOrEmpty(Port) && Port != "22")
                sb.Append(" -P ").Append(Port);

            if (!string.IsNullOrEmpty(ExtraArgs))
                sb.Append(" ").Append(ExtraArgs);

            if (!string.IsNullOrEmpty(Source))
                sb.Append(" ").Append(SshCommandBuilder.QuoteArgument(Source));

            if (!string.IsNullOrEmpty(Target))
                sb.Append(" ").Append(SshCommandBuilder.QuoteArgument(Target));

            return sb.ToString();
        }

        // ── Legacy migration ──────────────────────────────────────────────────────

        /// <summary>
        /// Parses a raw SSH or SCP command string (as stored in the legacy profiles.json)
        /// into a structured <see cref="SshProfile"/>.
        /// </summary>
        /// <param name="rawCommand">The raw command string, e.g. "ssh -p 22 user@host".</param>
        /// <returns>
        /// A best-effort <see cref="SshProfile"/>. Fields that cannot be parsed are placed in
        /// <see cref="ExtraArgs"/> so no information is lost.
        /// </returns>
        public static SshProfile ParseFromLegacyCommand(string rawCommand)
        {
            if (string.IsNullOrWhiteSpace(rawCommand))
                return new SshProfile { Type = "ssh" };

            var cmd = rawCommand.Trim();

            if (cmd.StartsWith("scp ", StringComparison.OrdinalIgnoreCase))
                return ParseScpArgs(cmd.Substring(4).TrimStart());

            if (cmd.StartsWith("ssh ", StringComparison.OrdinalIgnoreCase))
                cmd = cmd.Substring(4).TrimStart();
            else if (cmd.Equals("ssh", StringComparison.OrdinalIgnoreCase))
                return new SshProfile { Type = "ssh" };

            return ParseSshArgs(cmd);
        }

        private static SshProfile ParseSshArgs(string args)
        {
            var profile = new SshProfile { Type = "ssh" };
            var tokens = TokenizeShellLine(args);
            var extraArgsList = new List<string>();
            int i = 0;

            while (i < tokens.Count)
            {
                var token = tokens[i];

                if (token == "-i" && i + 1 < tokens.Count)
                {
                    profile.IdentityFile = tokens[++i];
                }
                else if (token == "-p" && i + 1 < tokens.Count)
                {
                    profile.Port = tokens[++i];
                }
                else if (token == "-t")
                {
                    profile.RequestTTY = profile.RequestTTY == "yes" ? "force" : "yes";
                }
                else if (token == "-T")
                {
                    profile.RequestTTY = "no";
                }
                else if (token == "-L" && i + 1 < tokens.Count)
                {
                    profile.LocalForward ??= new List<string>();
                    profile.LocalForward.Add(tokens[++i]);
                }
                else if (token == "-R" && i + 1 < tokens.Count)
                {
                    profile.RemoteForward ??= new List<string>();
                    profile.RemoteForward.Add(tokens[++i]);
                }
                else if (token == "-D" && i + 1 < tokens.Count)
                {
                    profile.DynamicForward = tokens[++i];
                }
                else if (token == "-J" && i + 1 < tokens.Count)
                {
                    profile.ProxyJump = tokens[++i];
                }
                else if (token == "-o" && i + 1 < tokens.Count)
                {
                    var optVal = tokens[++i];
                    if (optVal.StartsWith("IdentitiesOnly=", StringComparison.OrdinalIgnoreCase))
                        profile.IdentitiesOnly = optVal.Substring(15).Trim()
                            .Equals("yes", StringComparison.OrdinalIgnoreCase);
                    else if (optVal.StartsWith("ProxyCommand=", StringComparison.OrdinalIgnoreCase))
                        profile.ProxyCommand = optVal.Substring(13).Trim();
                    else
                        extraArgsList.Add("-o " + optVal);
                }
                else if (!token.StartsWith("-", StringComparison.Ordinal))
                {
                    // First positional argument is the destination
                    if (token.Contains('@'))
                    {
                        var at = token.IndexOf('@');
                        profile.User = token.Substring(0, at);
                        profile.HostName = token.Substring(at + 1);
                    }
                    else
                    {
                        profile.HostName = token;
                    }

                    // Everything after the destination is the remote command
                    if (i + 1 < tokens.Count)
                    {
                        var remParts = new List<string>();
                        for (int j = i + 1; j < tokens.Count; j++)
                            remParts.Add(tokens[j]);
                        profile.RemoteCommand = string.Join(" ", remParts);
                    }
                    break;
                }
                else
                {
                    // Unknown flag — preserve verbatim
                    extraArgsList.Add(token);
                }

                i++;
            }

            if (extraArgsList.Count > 0)
                profile.ExtraArgs = string.Join(" ", extraArgsList);

            return profile;
        }

        private static SshProfile ParseScpArgs(string args)
        {
            var profile = new SshProfile { Type = "scp" };
            var tokens = TokenizeShellLine(args);
            var positional = new List<string>();
            var extraArgsList = new List<string>();
            int i = 0;

            while (i < tokens.Count)
            {
                var token = tokens[i];
                if (token == "-r" || token == "-R") profile.Recursive = true;
                else if (token == "-p") profile.PreserveTimes = true;
                else if (token == "-C") profile.Compression = true;
                else if (token == "-i" && i + 1 < tokens.Count) profile.IdentityFile = tokens[++i];
                else if (token == "-P" && i + 1 < tokens.Count) profile.Port = tokens[++i];
                else if (token == "-o" && i + 1 < tokens.Count)
                {
                    var optVal = tokens[++i];
                    if (optVal.StartsWith("IdentitiesOnly=", StringComparison.OrdinalIgnoreCase))
                        profile.IdentitiesOnly = optVal.Substring(15).Trim()
                            .Equals("yes", StringComparison.OrdinalIgnoreCase);
                    else
                        extraArgsList.Add("-o " + optVal);
                }
                else if (!token.StartsWith("-", StringComparison.Ordinal))
                    positional.Add(token);
                else
                    extraArgsList.Add(token);
                i++;
            }

            if (positional.Count >= 1) profile.Source = positional[0];
            if (positional.Count >= 2) profile.Target = positional[1];

            // Extract user@host from the remote path (whichever positional has a colon)
            foreach (var pos in positional)
            {
                int colon = pos.LastIndexOf(':');
                if (colon > 0)
                {
                    var hostPart = pos.Substring(0, colon);
                    if (hostPart.Contains('@'))
                    {
                        int at = hostPart.IndexOf('@');
                        profile.User = hostPart.Substring(0, at);
                        profile.HostName = hostPart.Substring(at + 1);
                    }
                    else
                    {
                        profile.HostName = hostPart;
                    }
                    break;
                }
            }

            if (extraArgsList.Count > 0)
                profile.ExtraArgs = string.Join(" ", extraArgsList);

            return profile;
        }

        // ── Shell tokenizer ───────────────────────────────────────────────────────

        /// <summary>
        /// Splits a shell command line into tokens, respecting single- and double-quoted strings.
        /// </summary>
        internal static List<string> TokenizeShellLine(string line)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            bool inDoubleQuote = false;
            bool inSingleQuote = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inDoubleQuote)
                {
                    if (c == '\\' && i + 1 < line.Length)
                    {
                        var next = line[i + 1];
                        if (next == '"' || next == '\\' || next == '$' || next == '`')
                        {
                            current.Append(next);
                            i++;
                        }
                        else
                        {
                            current.Append(c);
                        }
                    }
                    else if (c == '"')
                        inDoubleQuote = false;
                    else
                        current.Append(c);
                }
                else if (inSingleQuote)
                {
                    if (c == '\'')
                        inSingleQuote = false;
                    else
                        current.Append(c);
                }
                else if (c == '"')
                {
                    inDoubleQuote = true;
                }
                else if (c == '\'')
                {
                    inSingleQuote = true;
                }
                else if (c == ' ' || c == '\t')
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }
    }
}
