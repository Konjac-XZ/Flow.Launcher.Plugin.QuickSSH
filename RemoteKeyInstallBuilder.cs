using System;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Builds SSH commands for deploying a public key to a remote host's
    /// <c>~/.ssh/authorized_keys</c> file.
    /// </summary>
    public static class RemoteKeyInstallBuilder
    {
        /// <summary>
        /// Key type prefixes that are accepted in public key validation.
        /// </summary>
        private static readonly string[] AllowedKeyTypes = new[]
        {
            "ssh-ed25519",
            "ssh-rsa",
            "ecdsa-sha2-nistp256",
            "ecdsa-sha2-nistp384",
            "ecdsa-sha2-nistp521",
            "sk-ssh-ed25519@openssh.com",
            "sk-ecdsa-sha2-nistp256@openssh.com"
        };

        /// <summary>
        /// Validates that <paramref name="line"/> is a safe, well-formed public
        /// key line suitable for embedding in a remote shell command.
        /// <list type="bullet">
        ///   <item>Must start with a known key type prefix.</item>
        ///   <item>Must contain at least two space-separated tokens
        ///         (<c>&lt;type&gt; &lt;base64&gt;</c>).</item>
        ///   <item>Everything after the second token is treated as an optional
        ///         comment — no field-count constraint.</item>
        ///   <item>Must not contain single quotes (<c>'</c>), double quotes
        ///         (<c>"</c>), newlines (<c>\n</c>, <c>\r</c>), or null bytes
        ///         (<c>\0</c>).</item>
        /// </list>
        /// </summary>
        public static bool ValidatePublicKeyLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            // Reject dangerous characters.
            // Single quotes would break the inner quoting in the bootstrap command.
            // Double quotes would break the outer double-quote wrapper in BuildFullSshCommand.
            // Newlines and null bytes would break single-line shell command parsing.
            if (line.IndexOf('\'') >= 0 ||
                line.IndexOf('"')  >= 0 ||
                line.IndexOf('\n') >= 0 ||
                line.IndexOf('\r') >= 0 ||
                line.IndexOf('\0') >= 0)
                return false;

            // Must contain at least <type> <base64>.
            var firstSpace = line.IndexOf(' ');
            if (firstSpace <= 0 || firstSpace >= line.Length - 1)
                return false;

            var keyType = line.Substring(0, firstSpace);

            bool typeMatch = false;
            for (int i = 0; i < AllowedKeyTypes.Length; i++)
            {
                if (string.Equals(keyType, AllowedKeyTypes[i], StringComparison.Ordinal))
                {
                    typeMatch = true;
                    break;
                }
            }

            if (!typeMatch)
                return false;

            // The base64 portion (second token) must not be empty.
            var afterType = line.Substring(firstSpace + 1).TrimStart();
            if (afterType.Length == 0)
                return false;

            return true;
        }

        /// <summary>
        /// Builds the idempotent remote bootstrap shell command that creates
        /// <c>~/.ssh</c> and appends the public key to <c>authorized_keys</c>
        /// only if it is not already present.
        /// </summary>
        /// <param name="publicKeyLine">
        /// The full public key line (e.g. <c>ssh-ed25519 AAAA... user@host</c>).
        /// Must have been validated by <see cref="ValidatePublicKeyLine"/>.
        /// </param>
        /// <returns>The remote shell one-liner (without the <c>ssh user@host</c> wrapper).</returns>
        public static string BuildBootstrapCommand(string publicKeyLine)
        {
            // The public key is embedded inside single quotes.  ValidatePublicKeyLine
            // guarantees no single quotes, newlines, or null bytes are present.
            return
                "umask 077 && " +
                "mkdir -p ~/.ssh && " +
                "touch ~/.ssh/authorized_keys && " +
                "chmod 700 ~/.ssh && " +
                "chmod 600 ~/.ssh/authorized_keys && " +
                "grep -qxF '" + publicKeyLine + "' ~/.ssh/authorized_keys || " +
                "printf '%s\\n' '" + publicKeyLine + "' >> ~/.ssh/authorized_keys";
        }

        /// <summary>
        /// Builds the full SSH command that runs the bootstrap on a remote host.
        /// <para>
        /// The bootstrap command is wrapped in <b>double quotes</b> so that:
        /// <list type="bullet">
        ///   <item><c>cmd.exe /k</c> (the default RunCommand shell on Windows)
        ///   does not interpret <c>&amp;&amp;</c> / <c>||</c> as command separators.</item>
        ///   <item>The bootstrap's internal single-quoted segments
        ///   (<c>'KEY'</c>, <c>'%s\n'</c>) are preserved as literal characters
        ///   and arrive intact on the remote shell.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="userAtHost"><c>user@host</c> destination string.</param>
        /// <param name="bootstrapCommand">
        /// The command returned by <see cref="BuildBootstrapCommand"/>.
        /// Must not contain double-quote characters (guaranteed when the public
        /// key line passes <see cref="ValidatePublicKeyLine"/>).
        /// </param>
        /// <returns>
        /// A string like <c>ssh user@host "umask 077 &amp;&amp; ..."</c>.
        /// </returns>
        public static string BuildFullSshCommand(string userAtHost, string bootstrapCommand)
        {
            return "ssh " + userAtHost + " \"" + bootstrapCommand + "\"";
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="input"/> looks like
        /// a valid <c>user@host</c> destination (contains exactly one <c>@</c> with
        /// non-empty parts on both sides).
        /// </summary>
        public static bool IsValidUserAtHost(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var atIndex = input.IndexOf('@');
            if (atIndex <= 0 || atIndex >= input.Length - 1)
                return false;

            // Must not contain spaces.
            if (input.IndexOf(' ') >= 0)
                return false;

            return true;
        }
    }
}
