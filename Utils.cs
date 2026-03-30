using System.Diagnostics;
using System.IO;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Utility helpers for SSH detection and executable resolution.
    /// </summary>
    public abstract class Utils
    {
        /// <summary>
        /// Resolves the full path of an executable using the 'where' command.
        /// Returns the original name if resolution fails.
        /// </summary>
        public static string ResolveExecutable(string exeName)
        {
            if (Path.IsPathRooted(exeName) && File.Exists(exeName))
                return exeName;

            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = exeName,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                string output = p.StandardOutput.ReadLine();
                p.WaitForExit();
                if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Trim()))
                    return output.Trim();
            }
            catch { /* fall through */ }

            return exeName;
        }

        /// <summary>
        /// Checks whether an SSH client is installed on the system.
        /// </summary>
        public static bool IsSshInstalled()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "ssh",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return !string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error);
            }
            catch
            {
                return false;
            }
        }
    }
}