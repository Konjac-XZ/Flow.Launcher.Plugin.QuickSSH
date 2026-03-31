using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Flow.Launcher.Plugin.QuickSSH
{
    /// <summary>
    /// Flow Launcher plugin for managing and launching SSH connections.
    /// </summary>
    public class QuickSsh : IPlugin, IPluginI18n
    {
        private static PluginInitContext _pluginContext;
        private ProfileManager _profileManager;

        private const string CommandAdd = "add";
        private const string CommandRemove = "remove";
        private const string CommandProfiles = "profiles";
        private const string CommandProfilesShort = "p";
        private const string CommandDirectConnect = "d";
        private const string CommandCustomShell = "shell";
        private const string CommandConfig = "config";
        private const string CommandDocs = "docs";

        private const string AppIconPath = "Images\\app.png";

        private string _databasePath;
        private string _sshClient = "cmd.exe";
        private bool _isSshInstalled = true;
        private bool _isDatabaseCreated = true;

        public void Init(PluginInitContext context)
        {
            _pluginContext = context;

            var sshDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh");

            _databasePath = Path.Combine(sshDir, "profiles.json");

            try
            {
                _profileManager = new ProfileManager(_databasePath);
            }
            catch
            {
                _isDatabaseCreated = false;
            }

            _isSshInstalled = Utils.IsSshInstalled();
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();

            if (!_isSshInstalled)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_sshnotinstalled_title"),
                    SubTitle = GetTranslation("plugin_quickssh_sshnotinstalled_subtitle"),
                    IcoPath = AppIconPath
                });
                return results;
            }

            if (!_isDatabaseCreated)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_databasenotcreated_title"),
                    SubTitle = GetTranslation("plugin_quickssh_databasenotcreated_subtitle"),
                    IcoPath = AppIconPath
                });
                return results;
            }

            var input = query.Search?.Trim() ?? "";

            if (string.IsNullOrEmpty(input))
            {
                // Show all command suggestions for TAB auto-completion
                results.AddRange(AutoCompleter.GetSuggestions(
                    query.ActionKeyword, "",
                    _profileManager?.UserData, AppIconPath,
                    _pluginContext?.API));
                return results;
            }

            var parts = input.Split(new[] { ' ' }, 2);
            var verb = parts[0].ToLowerInvariant();
            var rest = parts.Length > 1 ? parts[1].Trim() : "";

            switch (verb)
            {
                case CommandAdd:
                    results.AddRange(HandleAdd(query, rest));
                    break;
                case CommandRemove:
                    results.AddRange(HandleRemove(query, rest));
                    break;
                case CommandProfiles:
                case CommandProfilesShort:
                    results.AddRange(HandleProfiles(query, rest));
                    break;
                case CommandDirectConnect:
                    results.AddRange(HandleDirectConnect(query, rest));
                    break;
                case CommandCustomShell:
                    results.AddRange(HandleShell(query, rest));
                    break;
                case CommandConfig:
                    results.AddRange(HandleConfig(query));
                    break;
                case CommandDocs:
                    results.AddRange(HandleDocs());
                    break;
                default:
                    // Show auto-complete suggestions
                    results.AddRange(AutoCompleter.GetSuggestions(
                        query.ActionKeyword, input,
                        _profileManager?.UserData, AppIconPath,
                        _pluginContext?.API));
                    break;
            }

            return results;
        }

        #region Command Handlers

        private List<Result> HandleAdd(Query query, string rest)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(rest))
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandadd"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandadd"),
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " add "
                });
                return results;
            }

            var addParts = rest.Split(new[] { ' ' }, 2);
            var profileName = addParts[0];
            var sshCommand = addParts.Length > 1 ? addParts[1].Trim() : "";

            if (string.IsNullOrEmpty(sshCommand))
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandadd") + ": " + profileName,
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandadd"),
                    IcoPath = AppIconPath
                });
            }
            else
            {
                results.Add(new Result
                {
                    Title = "Save: " + profileName,
                    SubTitle = sshCommand,
                    IcoPath = AppIconPath,
                    Action = _ =>
                    {
                        _profileManager.UserData.Entries[profileName] = sshCommand;
                        return true;
                    }
                });
            }

            return results;
        }

        private List<Result> HandleRemove(Query query, string rest)
        {
            var results = new List<Result>();
            var entries = _profileManager.UserData.Entries;

            if (entries.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandremove"),
                    SubTitle = "No profiles saved.",
                    IcoPath = AppIconPath
                });
                return results;
            }

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(rest) &&
                    !entry.Key.ToLowerInvariant().Contains(rest.ToLowerInvariant()))
                    continue;

                results.Add(new Result
                {
                    Title = entry.Key,
                    SubTitle = entry.Value,
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " remove " + entry.Key,
                    Action = _ =>
                    {
                        _profileManager.UserData.Entries.Remove(entry.Key);
                        return true;
                    }
                });
            }

            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandremove"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandremove"),
                    IcoPath = AppIconPath
                });
            }

            return results;
        }

        private List<Result> HandleProfiles(Query query, string search)
        {
            var results = new List<Result>();
            var entries = _profileManager.UserData.Entries;

            if (entries.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles"),
                    SubTitle = "No profiles saved.",
                    IcoPath = AppIconPath
                });
                return results;
            }

            var scored = new List<(int score, string name, string command)>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(search))
                {
                    scored.Add((0, entry.Key, entry.Value));
                }
                else
                {
                    int score = ScoreProfile(search, entry.Key, entry.Value);
                    if (score < int.MaxValue)
                        scored.Add((score, entry.Key, entry.Value));
                }
            }

            foreach (var item in scored.OrderBy(s => s.score))
            {
                var name = item.name;
                var command = item.command;
                results.Add(new Result
                {
                    Title = name,
                    SubTitle = command,
                    IcoPath = AppIconPath,
                    Action = _ =>
                    {
                        RunSshCommand(command);
                        return true;
                    },
                    AutoCompleteText = query.ActionKeyword + " profiles " + name
                });
            }

            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles"),
                    IcoPath = AppIconPath
                });
            }

            return results;
        }

        private List<Result> HandleDirectConnect(Query query, string rest)
        {
            var results = new List<Result>();

            if (string.IsNullOrEmpty(rest))
            {
                results.Add(new Result
                {
                    Title = "Direct connect",
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect") + " d ssh user@host",
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " d "
                });
                return results;
            }

            results.Add(new Result
            {
                Title = "Connect: " + rest,
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect") + " " + rest,
                IcoPath = AppIconPath,
                Action = _ =>
                {
                    RunSshCommand(rest);
                    return true;
                }
            });

            return results;
        }

        private List<Result> HandleShell(Query query, string rest)
        {
            var results = new List<Result>();
            var parts = rest.Split(new[] { ' ' }, 2);
            var subCmd = parts[0].ToLowerInvariant();
            var subRest = parts.Length > 1 ? parts[1].Trim() : "";

            switch (subCmd)
            {
                case "add":
                    if (string.IsNullOrEmpty(subRest))
                    {
                        results.Add(new Result
                        {
                            Title = GetTranslation("plugin_quickssh_title_commandshell_add"),
                            SubTitle = GetTranslation("plugin_quickssh_subtitle_commandshell_add_usage"),
                            IcoPath = AppIconPath
                        });
                    }
                    else
                    {
                        var (name, value) = ParseShellAddArgs(subRest);
                        results.Add(new Result
                        {
                            Title = "Add shell: " + name,
                            SubTitle = string.IsNullOrEmpty(value) ? name : value,
                            IcoPath = AppIconPath,
                            Action = _ =>
                            {
                                // Suppress auto-save during mutations so we can set all
                                // fields (including SelectedCustomShell) before persisting once.
                                _profileManager.UserData.CustomShell.SetCallback(null);
                                _profileManager.UserData.CustomShell[name] = value ?? "";
                                if (_profileManager.UserData.CustomShell.Count == 1)
                                    _profileManager.UserData.SelectedCustomShell = name;
                                _profileManager.UserData.CustomShell.SetCallback(_profileManager.SaveConfiguration);
                                _profileManager.SaveConfiguration();
                                return true;
                            }
                        });
                    }
                    break;

                case "remove":
                    var shells = _profileManager.UserData.CustomShell;
                    if (shells.Count == 0)
                    {
                        results.Add(new Result
                        {
                            Title = GetTranslation("plugin_quickssh_title_commandshell_remove"),
                            SubTitle = "No shell profiles saved.",
                            IcoPath = AppIconPath
                        });
                    }
                    else
                    {
                        foreach (var shell in shells)
                        {
                            results.Add(new Result
                            {
                                Title = shell.Key,
                                SubTitle = string.IsNullOrEmpty(shell.Value) ? shell.Key : shell.Value,
                                IcoPath = AppIconPath,
                                AutoCompleteText = query.ActionKeyword + " shell remove " + shell.Key,
                                Action = _ =>
                                {
                                    // Suppress auto-save so we can update SelectedCustomShell
                                    // atomically before the single explicit save below.
                                    _profileManager.UserData.CustomShell.SetCallback(null);
                                    _profileManager.UserData.CustomShell.Remove(shell.Key);
                                    if (_profileManager.UserData.SelectedCustomShell == shell.Key)
                                        _profileManager.UserData.SelectedCustomShell = null;
                                    _profileManager.UserData.CustomShell.SetCallback(_profileManager.SaveConfiguration);
                                    _profileManager.SaveConfiguration();
                                    return true;
                                }
                            });
                        }
                    }
                    break;

                default:
                    // List shells + help
                    var allShells = _profileManager.UserData.CustomShell;
                    var selected = _profileManager.UserData.SelectedCustomShell;

                    if (allShells.Count > 0)
                    {
                        foreach (var shell in allShells)
                        {
                            var isSelected = shell.Key == selected;
                            var marker = isSelected
                                ? " " + GetTranslation("plugin_quickssh_shell_selected")
                                : "";

                            results.Add(new Result
                            {
                                Title = shell.Key + marker,
                                SubTitle = string.IsNullOrEmpty(shell.Value) ? shell.Key : shell.Value,
                                IcoPath = AppIconPath,
                                AutoCompleteText = query.ActionKeyword + " shell " + shell.Key,
                                Action = _ =>
                                {
                                    _profileManager.UserData.SelectedCustomShell = shell.Key;
                                    _profileManager.SaveConfiguration();
                                    return true;
                                }
                            });
                        }
                    }

                    results.Add(new Result
                    {
                        Title = "Shell management",
                        SubTitle = GetTranslation("plugin_quickssh_subtitle_commandshell_help"),
                        IcoPath = AppIconPath,
                        AutoCompleteText = query.ActionKeyword + " shell "
                    });
                    break;
            }

            return results;
        }

        private List<Result> HandleConfig(Query query)
        {
            var results = new List<Result>();

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandconfig"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandconfig"),
                IcoPath = AppIconPath,
                Action = _ =>
                {
                    try
                    {
                        var hosts = SshConfigParser.Parse();
                        if (hosts.Count == 0)
                        {
                            _pluginContext.API.ShowMsg("QuickSSH",
                                GetTranslation("plugin_quickssh_config_notfound"));
                            return true;
                        }

                        int imported = 0;
                        foreach (var host in hosts)
                        {
                            if (!_profileManager.UserData.Entries.ContainsKey(host.Key))
                            {
                                _profileManager.UserData.Entries[host.Key] = host.Value;
                                imported++;
                            }
                        }

                        _pluginContext.API.ShowMsg("QuickSSH",
                            string.Format(GetTranslation("plugin_quickssh_config_imported"), imported));
                    }
                    catch (Exception ex)
                    {
                        _pluginContext.API.ShowMsg("QuickSSH", "Error: " + ex.Message);
                    }
                    return true;
                }
            });

            return results;
        }

        private List<Result> HandleDocs()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commanddocs"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddocs"),
                    IcoPath = AppIconPath,
                    Action = _ =>
                    {
                        using var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH",
                            UseShellExecute = true
                        });
                        return true;
                    }
                }
            };
        }

        #endregion

        #region SSH Execution

        private void RunSshCommand(string sshCommand)
        {
            var selectedShell = _profileManager.UserData.SelectedCustomShell;
            var customShells = _profileManager.UserData.CustomShell;

            string fileName;
            string arguments;

            if (!string.IsNullOrEmpty(selectedShell) && customShells.ContainsKey(selectedShell))
            {
                var shellValue = customShells[selectedShell];
                if (string.IsNullOrEmpty(shellValue))
                {
                    // Shell name is the executable
                    fileName = Utils.ResolveExecutable(selectedShell);
                    arguments = sshCommand;
                }
                else
                {
                    // Parse the shell value into exe + args
                    var spaceIdx = shellValue.IndexOf(' ');
                    if (spaceIdx < 0)
                    {
                        fileName = Utils.ResolveExecutable(shellValue);
                        arguments = sshCommand;
                    }
                    else
                    {
                        fileName = Utils.ResolveExecutable(shellValue.Substring(0, spaceIdx));
                        arguments = shellValue.Substring(spaceIdx + 1) + " " + sshCommand;
                    }
                }
            }
            else
            {
                // Default: use cmd.exe with /k so the window stays open after SSH exits,
                // allowing the user to see any connection-error messages.
                // sshCommand is a user-supplied SSH command (e.g. "ssh user@host") that
                // was explicitly typed or saved by the user; passing it verbatim is intentional.
                fileName = _sshClient;
                arguments = "/k " + sshCommand;
            }

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _pluginContext?.API?.ShowMsg("QuickSSH", "Error: " + ex.Message);
            }
        }

        #endregion

        #region Search Scoring

        private static int ScoreProfile(string search, string name, string command)
        {
            var searchLower = RemoveDiacritics(search.ToLowerInvariant());
            var nameLower = RemoveDiacritics(name.ToLowerInvariant());
            var commandLower = RemoveDiacritics(command.ToLowerInvariant());

            bool nameExact = ContainsIgnoreAccents(nameLower, searchLower);
            bool cmdExact = ContainsIgnoreAccents(commandLower, searchLower);

            if (nameExact && cmdExact) return 0;
            if (nameExact) return 1;
            if (cmdExact) return 2;

            bool nameFuzzy = FuzzyContains(nameLower, searchLower);
            bool cmdFuzzy = FuzzyContains(commandLower, searchLower);

            if (nameFuzzy && cmdFuzzy) return 3;
            if (nameFuzzy) return 4;
            if (cmdFuzzy) return 5;

            return int.MaxValue;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool ContainsIgnoreAccents(string source, string search)
        {
            return RemoveDiacritics(source).Contains(RemoveDiacritics(search));
        }

        private static bool FuzzyContains(string source, string search)
        {
            if (search.Length < 5)
                return source.Contains(search);

            int tolerance = search.Length / 5;

            for (int i = 0; i <= source.Length - search.Length + tolerance; i++)
            {
                int end = Math.Min(i + search.Length + tolerance, source.Length);
                var window = source.Substring(i, end - i);
                if (DamerauLevenshteinDistance(window, search) <= tolerance)
                    return true;
            }

            return false;
        }

        private static int DamerauLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
                }
            }

            return d[n, m];
        }

        #endregion

        #region Shell Argument Parsing

        private static (string name, string value) ParseShellAddArgs(string input)
        {
            if (string.IsNullOrEmpty(input))
                return ("", "");

            // Check for quoted strings
            if (input.StartsWith("\""))
            {
                int endQuote = input.IndexOf('"', 1);
                if (endQuote > 0)
                {
                    var name = input.Substring(1, endQuote - 1);
                    var value = input.Length > endQuote + 1
                        ? input.Substring(endQuote + 1).Trim()
                        : "";
                    return (name, value);
                }
            }

            var spaceIdx = input.IndexOf(' ');
            if (spaceIdx < 0)
                return (input, "");

            return (input.Substring(0, spaceIdx), input.Substring(spaceIdx + 1).Trim());
        }

        #endregion

        #region i18n

        public string GetTranslatedPluginTitle()
        {
            return GetTranslation("plugin_quickssh_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return GetTranslation("plugin_quickssh_plugin_description");
        }

        public static string GetTranslation(string key)
        {
            try
            {
                return _pluginContext?.API?.GetTranslation(key) ?? key;
            }
            catch
            {
                return key;
            }
        }

        #endregion
    }
}