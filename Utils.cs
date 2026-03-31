using System;
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
                // Drain stderr to prevent deadlock; we only need the first stdout line.
                p.StandardError.ReadToEnd();
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
            // First, check the default Windows built-in OpenSSH location
            // (available since Windows 10 version 1809 / Windows Server 2019).
            // Environment.SpecialFolder.System resolves to %SystemRoot%\System32.
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (File.Exists(Path.Combine(systemDir, "OpenSSH", "ssh.exe")))
                return true;

            // Fallback: search PATH via the 'where' command and rely on its exit code.
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
                // Drain both streams to prevent deadlock; we only need the exit code.
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}