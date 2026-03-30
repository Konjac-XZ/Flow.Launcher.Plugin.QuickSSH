using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Provides TAB auto-completion suggestions for the plugin.
    /// </summary>
    public class AutoCompleter
    {
        private static readonly string[] Commands = new[]
        {
            "add", "remove", "profiles", "p", "d", "shell", "config", "docs"
        };

        /// <summary>
        /// Returns auto-completion suggestions for the given partial input.
        /// </summary>
        public static List<Result> GetSuggestions(
            string actionKeyword,
            string input,
            UserData userData,
            string iconPath,
            IPublicAPI api = null)
        {
            var results = new List<Result>();
            var trimmed = input.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(trimmed))
            {
                // Show all available commands
                foreach (var cmd in Commands)
                {
                    var autoText = actionKeyword + " " + cmd + " ";
                    results.Add(new Result
                    {
                        Title = cmd,
                        SubTitle = GetCommandDescription(cmd),
                        IcoPath = iconPath,
                        Action = _ =>
                        {
                            api?.ChangeQuery(autoText, true);
                            return false;
                        },
                        AutoCompleteText = autoText
                    });
                }
                return results;
            }

            // Match commands that start with the input
            var matchingCommands = Commands
                .Where(c => c.StartsWith(trimmed))
                .ToList();

            foreach (var cmd in matchingCommands)
            {
                var autoText = actionKeyword + " " + cmd + " ";
                results.Add(new Result
                {
                    Title = cmd,
                    SubTitle = GetCommandDescription(cmd),
                    IcoPath = iconPath,
                    Action = _ =>
                    {
                        api?.ChangeQuery(autoText, true);
                        return false;
                    },
                    AutoCompleteText = autoText
                });
            }

            // If typing after "profiles" or "p", also suggest profile names
            if (trimmed.StartsWith("profiles ") || trimmed.StartsWith("p "))
            {
                var search = trimmed.StartsWith("profiles ")
                    ? trimmed.Substring(9)
                    : trimmed.Substring(2);

                if (userData?.Entries != null)
                {
                    foreach (var entry in userData.Entries)
                    {
                        if (string.IsNullOrEmpty(search) ||
                            entry.Key.ToLowerInvariant().Contains(search))
                        {
                            var autoText = actionKeyword + " profiles " + entry.Key;
                            results.Add(new Result
                            {
                                Title = entry.Key,
                                SubTitle = entry.Value,
                                IcoPath = iconPath,
                                Action = _ =>
                                {
                                    api?.ChangeQuery(autoText, true);
                                    return false;
                                },
                                AutoCompleteText = autoText
                            });
                        }
                    }
                }
            }

            return results;
        }

        private static string GetCommandDescription(string command)
        {
            return command switch
            {
                "add" => "Add a new SSH profile",
                "remove" => "Remove an SSH profile",
                "profiles" or "p" => "List and search saved profiles",
                "d" => "Direct SSH connection without saving",
                "shell" => "Manage custom shell interpreters",
                "config" => "Import hosts from ~/.ssh/config",
                "docs" => "Open plugin documentation",
                _ => ""
            };
        }
    }
}