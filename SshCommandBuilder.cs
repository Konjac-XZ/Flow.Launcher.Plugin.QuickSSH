namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Builds SSH command strings with proper quoting and escaping.
    /// </summary>
    public static class SshCommandBuilder
    {
        /// <summary>
        /// Wraps <paramref name="arg"/> in double-quotes if it contains spaces
        /// or other characters that need quoting.
        /// </summary>
        public static string QuoteArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";

            // If it contains spaces, quotes, or backslashes we need quoting
            bool needsQuoting = false;
            foreach (var c in arg)
            {
                if (c == ' ' || c == '"' || c == '\\' || c == '\t')
                {
                    needsQuoting = true;
                    break;
                }
            }

            if (!needsQuoting)
                return arg;

            // Escape inner double-quotes and trailing backslashes
            var escaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }

        /// <summary>
        /// Builds a complete SSH command line from components.
        /// </summary>
        public static string Build(
            string host,
            string? user = null,
            string? port = null,
            string? identityFile = null,
            string? remoteCommand = null)
        {
            var cmd = "ssh";

            if (!string.IsNullOrEmpty(identityFile))
                cmd += " -i " + QuoteArgument(identityFile);

            if (!string.IsNullOrEmpty(port) && port != "22")
                cmd += " -p " + port;

            if (!string.IsNullOrEmpty(user))
                cmd += " " + user + "@" + host;
            else
                cmd += " " + host;

            if (!string.IsNullOrEmpty(remoteCommand))
                cmd += " " + QuoteArgument(remoteCommand);

            return cmd;
        }
    }
}