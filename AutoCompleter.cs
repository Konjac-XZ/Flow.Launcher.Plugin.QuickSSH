using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Provides TAB auto-completion suggestions for the plugin.
    /// </summary>
    public class AutoCompleter
    {
        /// <summary>
        /// Commands visible in the autocomplete / suggestion UI.
        /// Hidden aliases ("p", "d", "docs") are intentionally excluded;
        /// they still work when typed but are not shown in suggestions.
        /// </summary>
        private static readonly string[] VisibleCommands = new[]
        {
            "add", "remove", "profiles", "shell", "config", "export", "import", "copy", "rename", "help"
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
                // Show all visible commands (hidden aliases are excluded)
                foreach (var cmd in VisibleCommands)
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

            // When the input exactly matches a known command, return no suggestions.
            // The Query() switch will have already entered that command's dedicated handler,
            // which owns the result list for that view. Returning an autocomplete suggestion
            // here would pollute the command-specific results with an unrelated entry.
            if (System.Array.IndexOf(VisibleCommands, trimmed) >= 0)
                return results;

            // Match visible commands that start with the input
            // (hidden aliases like "p", "d", and "docs" are not shown as suggestions)
            var matchingCommands = VisibleCommands
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

            // If typing after "profiles" or "p", also suggest profile names.
            // Use TrimStart (not Trim) so a trailing space in "profiles " is preserved
            // and correctly matched by StartsWith("profiles ").
            var prefixCheck = input.TrimStart().ToLowerInvariant();
            bool isProfilesPrefix = prefixCheck.StartsWith("profiles ");
            bool isPPrefix = !isProfilesPrefix && prefixCheck.StartsWith("p ");
            if (isProfilesPrefix || isPPrefix)
            {
                var search = isProfilesPrefix ? prefixCheck.Substring(9) : prefixCheck.Substring(2);

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
            var key = command switch
            {
                "add" => "plugin_quickssh_subtitle_commandadd",
                "remove" => "plugin_quickssh_subtitle_commandremove",
                "profiles" => "plugin_quickssh_subtitle_commandprofiles",
                "p" => "plugin_quickssh_subtitle_commandp_usage",
                "d" => "plugin_quickssh_subtitle_commandd_usage",
                "shell" => "plugin_quickssh_subtitle_commandshell_help",
                "config" => "plugin_quickssh_subtitle_commandconfig_usage",
                "export" => "plugin_quickssh_subtitle_commandexport_usage",
                "import" => "plugin_quickssh_subtitle_commandimport_usage",
                "copy" => "plugin_quickssh_subtitle_commandcopy_usage",
                "rename" => "plugin_quickssh_subtitle_commandrename",
                "help" => "plugin_quickssh_subtitle_commandhelp_usage",
                "docs" => "plugin_quickssh_subtitle_commanddocs_usage",
                _ => null
            };
            return key != null ? QuickSsh.GetTranslation(key) : "";
        }
    }
}