using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

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
        private const string CommandExport = "export";
        private const string CommandImport = "import";
        private const string CommandDocs = "docs";
        private const string CommandCopy = "copy";
        private const string CommandRename = "rename";

        private const string AppIconPath = "Images\\app.png";
        private const string AppIconGreenPath = "Images\\app-green.png";
        private const string AppIconRedPath = "Images\\app-red.png";

        private string _databasePath;
        private string _dataDir;
        private bool _isSshInstalled = true;
        private bool _isDatabaseCreated = true;

        public void Init(PluginInitContext context)
        {
            _pluginContext = context;

            var sshDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh");

            _databasePath = Path.Combine(sshDir, "profiles.json");
            _dataDir = Path.Combine(context.CurrentPluginMetadata.PluginDirectory, "data");

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
                case CommandExport:
                    results.AddRange(HandleExport(query, rest));
                    break;
                case CommandImport:
                    results.AddRange(HandleImport(query, rest));
                    break;
                case CommandDocs:
                    results.AddRange(HandleDocs());
                    break;
                case CommandCopy:
                    results.AddRange(HandleCopy(query, rest));
                    break;
                case CommandRename:
                    results.AddRange(HandleRename(query, rest));
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

            // Always show usage hint at the top.
            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandadd"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandadd"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " add "
            });

            if (string.IsNullOrEmpty(rest))
                return results;

            var addParts = rest.Split(new[] { ' ' }, 2);
            var profileName = addParts[0];
            var rawSshCommand = addParts.Length > 1 ? addParts[1].Trim() : "";

            // Normalise: strip cmd-style /flags, ensure "ssh " prefix.
            var sshCommand = string.IsNullOrEmpty(rawSshCommand)
                ? ""
                : (NormalizeSshCommand(rawSshCommand) ?? "");

            if (!string.IsNullOrEmpty(sshCommand))
            {
                results.Add(new Result
                {
                    Title = "Save: " + profileName,
                    SubTitle = sshCommand,
                    IcoPath = AppIconGreenPath,
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
                    IcoPath = AppIconRedPath,
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
                    IcoPath = AppIconGreenPath,
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
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect") + " d user@host",
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " d "
                });
                return results;
            }

            // Normalise the user input: strip accidental cmd-style /flags and
            // ensure the command starts with "ssh ".
            var sshCmd = NormalizeSshCommand(rest);
            if (string.IsNullOrEmpty(sshCmd))
            {
                results.Add(new Result
                {
                    Title = "Direct connect",
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect") + " d user@host",
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " d "
                });
                return results;
            }

            results.Add(new Result
            {
                Title = "Connect: " + rest,
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect") + " " + sshCmd,
                IcoPath = AppIconGreenPath,
                Action = _ =>
                {
                    RunSshCommand(sshCmd);
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
                            IcoPath = AppIconGreenPath,
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
                                IcoPath = AppIconRedPath,
                                AutoCompleteText = query.ActionKeyword + " shell remove " + shell.Key,
                                Action = _ =>
                                {
                                    // Suppress auto-save so we can update SelectedCustomShell
                                    // atomically before the single explicit save below.
                                    _profileManager.UserData.CustomShell.SetCallback(null);
                                    _profileManager.UserData.CustomShell.Remove(shell.Key);
                                    if (_profileManager.UserData.SelectedCustomShell == shell.Key)
                                    {
                                        // Auto-select the first remaining shell (if any).
                                        _profileManager.UserData.SelectedCustomShell =
                                            _profileManager.UserData.CustomShell.Keys.FirstOrDefault();
                                    }
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
                                IcoPath = AppIconGreenPath,
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

                    // Sub-command suggestions for TAB completion
                    var shellSubCmds = new[]
                    {
                        ("add", GetTranslation("plugin_quickssh_title_commandshell_add"), GetTranslation("plugin_quickssh_subtitle_commandshell_add_usage")),
                        ("remove", GetTranslation("plugin_quickssh_title_commandshell_remove"), GetTranslation("plugin_quickssh_subtitle_commandshell_remove")),
                    };
                    foreach (var (scName, scTitle, scSubTitle) in shellSubCmds)
                    {
                        if (string.IsNullOrEmpty(subCmd) || scName.StartsWith(subCmd))
                        {
                            var autoText = query.ActionKeyword + " shell " + scName + " ";
                            results.Add(new Result
                            {
                                Title = scTitle,
                                SubTitle = scSubTitle,
                                IcoPath = AppIconPath,
                                AutoCompleteText = autoText,
                                Action = _ =>
                                {
                                    _pluginContext?.API?.ChangeQuery(autoText, true);
                                    return false;
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
                IcoPath = AppIconGreenPath,
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

        private List<Result> HandleCopy(Query query, string search)
        {
            var results = new List<Result>();
            var entries = _profileManager.UserData.Entries;

            if (entries.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandcopy"),
                    SubTitle = "No profiles saved.",
                    IcoPath = AppIconPath
                });
                return results;
            }

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(search) &&
                    !entry.Key.ToLowerInvariant().Contains(search.ToLowerInvariant()))
                    continue;

                var name = entry.Key;
                var command = entry.Value;
                results.Add(new Result
                {
                    Title = name,
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandcopy") + " " + command,
                    IcoPath = AppIconGreenPath,
                    AutoCompleteText = query.ActionKeyword + " copy " + name,
                    Action = _ =>
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(command);
                        }
                        catch (Exception)
                        {
                            _pluginContext?.API?.ShowMsg("QuickSSH",
                                GetTranslation("plugin_quickssh_copy_clipboard_error"));
                        }
                        return true;
                    }
                });
            }

            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandcopy"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandcopy_empty"),
                    IcoPath = AppIconPath
                });
            }

            return results;
        }

        private List<Result> HandleRename(Query query, string rest)
        {
            var results = new List<Result>();
            var entries = _profileManager.UserData.Entries;

            var parts = rest.Split(new[] { ' ' }, 2);
            var oldName = parts[0].Trim();
            var newName = parts.Length > 1 ? parts[1].Trim() : "";

            if (string.IsNullOrEmpty(oldName))
            {
                // No name typed yet – show all profiles as suggestions.
                if (entries.Count == 0)
                {
                    results.Add(new Result
                    {
                        Title = GetTranslation("plugin_quickssh_title_commandrename"),
                        SubTitle = "No profiles saved.",
                        IcoPath = AppIconPath
                    });
                    return results;
                }

                foreach (var entry in entries)
                {
                    var name = entry.Key;
                    var autoText = query.ActionKeyword + " rename " + name + " ";
                    results.Add(new Result
                    {
                        Title = name,
                        SubTitle = entry.Value,
                        IcoPath = AppIconPath,
                        AutoCompleteText = autoText,
                        Action = _ =>
                        {
                            _pluginContext?.API?.ChangeQuery(autoText, true);
                            return false;
                        }
                    });
                }
                return results;
            }

            if (!entries.ContainsKey(oldName))
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandrename") + ": " + oldName,
                    SubTitle = GetTranslation("plugin_quickssh_rename_notfound"),
                    IcoPath = AppIconRedPath
                });
                return results;
            }

            if (string.IsNullOrEmpty(newName))
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandrename") + ": " + oldName,
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandrename"),
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " rename " + oldName + " "
                });
                return results;
            }

            var cmdValue = entries[oldName];
            results.Add(new Result
            {
                Title = oldName + " → " + newName,
                SubTitle = cmdValue,
                IcoPath = AppIconGreenPath,
                Action = _ =>
                {
                    var value = entries[oldName];
                    entries.SetCallback(null);
                    try
                    {
                        entries.Remove(oldName);
                        entries[newName] = value;
                    }
                    finally
                    {
                        entries.SetCallback(_profileManager.SaveConfiguration);
                    }
                    _profileManager.SaveConfiguration();
                    return true;
                }
            });

            return results;
        }

        private List<Result> HandleExport(Query query, string rest)
        {
            var results = new List<Result>();
            var exportPath = Path.Combine(_dataDir, "profiles_export.json");

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandexport"),
                SubTitle = string.Format(GetTranslation("plugin_quickssh_subtitle_commandexport"), exportPath),
                IcoPath = AppIconGreenPath,
                AutoCompleteText = query.ActionKeyword + " export ",
                Action = _ =>
                {
                    try
                    {
                        Directory.CreateDirectory(_dataDir);
                        var entries = _profileManager.UserData.Entries
                            .ToDictionary(e => e.Key, e => e.Value);
                        var json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                        File.WriteAllText(exportPath, json);
                        _pluginContext.API.ShowMsg("QuickSSH",
                            string.Format(GetTranslation("plugin_quickssh_export_success"),
                                entries.Count, exportPath));
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

        private List<Result> HandleImport(Query query, string rest)
        {
            var results = new List<Result>();

            string[] importFiles = Array.Empty<string>();
            try
            {
                if (Directory.Exists(_dataDir))
                    importFiles = Directory.GetFiles(_dataDir, "*.json");
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            if (importFiles.Length == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandimport"),
                    SubTitle = string.Format(GetTranslation("plugin_quickssh_import_nofiles"), _dataDir),
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " import "
                });
                return results;
            }

            foreach (var file in importFiles)
            {
                var fileName = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(rest) &&
                    !fileName.ToLowerInvariant().Contains(rest.ToLowerInvariant()))
                    continue;

                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandimport") + ": " + fileName,
                    SubTitle = file,
                    IcoPath = AppIconGreenPath,
                    AutoCompleteText = query.ActionKeyword + " import " + fileName,
                    Action = _ =>
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                            if (entries == null || entries.Count == 0)
                            {
                                _pluginContext.API.ShowMsg("QuickSSH",
                                    GetTranslation("plugin_quickssh_import_empty"));
                                return true;
                            }

                            int imported = 0;
                            _profileManager.UserData.Entries.SetCallback(null);
                            try
                            {
                                foreach (var entry in entries)
                                {
                                    if (!_profileManager.UserData.Entries.ContainsKey(entry.Key))
                                    {
                                        _profileManager.UserData.Entries[entry.Key] = entry.Value;
                                        imported++;
                                    }
                                }
                            }
                            finally
                            {
                                _profileManager.UserData.Entries.SetCallback(_profileManager.SaveConfiguration);
                            }

                            if (imported > 0)
                                _profileManager.SaveConfiguration();

                            _pluginContext.API.ShowMsg("QuickSSH",
                                string.Format(GetTranslation("plugin_quickssh_import_success"), imported));
                        }
                        catch (Exception ex)
                        {
                            _pluginContext.API.ShowMsg("QuickSSH", "Error: " + ex.Message);
                        }
                        return true;
                    }
                });
            }

            if (results.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandimport"),
                    SubTitle = string.Format(GetTranslation("plugin_quickssh_import_nofiles"), _dataDir),
                    IcoPath = AppIconPath
                });
            }

            return results;
        }

        #endregion

        #region SSH Execution

        /// <summary>
        /// Normalises a raw SSH command string so it is safe to pass to a terminal.
        /// <list type="bullet">
        ///   <item>Strips leading Windows cmd.exe-style /flags (e.g. "/c", "/k") that
        ///   users sometimes accidentally prepend. SSH uses POSIX '-' options, so a
        ///   leading '/' token is always wrong.</item>
        ///   <item>Auto-prepends "ssh " when the user supplied only a destination
        ///   (e.g. "user@host" instead of "ssh user@host").</item>
        ///   <item>Removes /flags that appear immediately after the "ssh " prefix for
        ///   the same reason (e.g. "ssh /c user@host" → "ssh user@host").</item>
        /// </list>
        /// Returns <see langword="null"/> if nothing valid remains after stripping.
        /// </summary>
        internal static string NormalizeSshCommand(string rawCommand)
        {
            if (string.IsNullOrWhiteSpace(rawCommand))
                return null;

            var cmd = rawCommand.Trim();

            // Strip any leading Windows cmd-style /flags.
            while (cmd.StartsWith("/", StringComparison.Ordinal))
            {
                var space = cmd.IndexOf(' ');
                if (space < 0)
                    return null; // nothing left after stripping
                cmd = cmd.Substring(space + 1).TrimStart();
            }

            if (string.IsNullOrEmpty(cmd))
                return null;

            // Auto-prepend "ssh " when only a destination was given.
            if (!cmd.StartsWith("ssh ", StringComparison.OrdinalIgnoreCase)
                && !cmd.Equals("ssh", StringComparison.OrdinalIgnoreCase))
            {
                cmd = "ssh " + cmd;
            }

            // Remove any /flags that appear right after the "ssh " prefix
            // (e.g. a user stored "ssh /c user@host" by mistake).
            const string sshPrefix = "ssh ";
            if (cmd.Length > sshPrefix.Length)
            {
                var rest = cmd.Substring(sshPrefix.Length);
                bool changed = false;
                while (rest.StartsWith("/", StringComparison.Ordinal))
                {
                    var space = rest.IndexOf(' ');
                    if (space < 0) { rest = string.Empty; changed = true; break; }
                    rest = rest.Substring(space + 1).TrimStart();
                    changed = true;
                }
                if (changed)
                    cmd = string.IsNullOrEmpty(rest) ? null : "ssh " + rest;
            }

            return cmd;
        }

        private void RunSshCommand(string sshCommand)
        {
            // Normalise: strip accidental Windows cmd-style /flags and ensure the
            // command starts with "ssh " so it can always be passed verbatim to a
            // terminal (cmd.exe /k <sshCommand>).
            sshCommand = NormalizeSshCommand(sshCommand);
            if (string.IsNullOrEmpty(sshCommand))
                return;

            var selectedShell = _profileManager.UserData.SelectedCustomShell;
            var customShells = _profileManager.UserData.CustomShell;

            string fileName;
            string arguments;
            string customShellName = null;

            if (!string.IsNullOrEmpty(selectedShell) && customShells.ContainsKey(selectedShell))
            {
                var shellValue = customShells[selectedShell];
                customShellName = selectedShell;

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
                fileName = GetCmdExePath();
                arguments = "/k " + sshCommand;
            }

            // Use the user's home directory as the working directory so SSH can always
            // find ~/.ssh keys and config, even when FlowLauncher itself is installed in
            // a path that contains non-ASCII characters or spaces.
            var workingDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                });
            }
            catch (Exception ex)
            {
                if (customShellName != null)
                {
                    // The custom shell executable could not be started; fall back to
                    // cmd.exe so the connection still opens despite the broken shell.
                    try
                    {
                        using var fallback = Process.Start(new ProcessStartInfo
                        {
                            FileName = GetCmdExePath(),
                            Arguments = "/k " + sshCommand,
                            UseShellExecute = true,
                            WorkingDirectory = workingDir
                        });
                    }
                    catch (Exception ex2)
                    {
                        _pluginContext?.API?.ShowMsg("QuickSSH", "Error: " + ex2.Message);
                    }
                }
                else
                {
                    _pluginContext?.API?.ShowMsg("QuickSSH", "Error: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Returns the absolute path to cmd.exe (always in %SystemRoot%\System32).
        /// Falls back to the bare name as a last resort so PATH can resolve it.
        /// </summary>
        private static string GetCmdExePath()
        {
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var cmdPath = Path.Combine(systemDir, "cmd.exe");
            return File.Exists(cmdPath) ? cmdPath : "cmd.exe";
        }

        #endregion

        #region Search Scoring

        internal static int ScoreProfile(string search, string name, string command)
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