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

        private const string CommandProfiles = "profiles";
        private const string CommandCustomShell = "shell";
        private const string CommandConfig = "config";
        private const string CommandHelp = "help";

        /// <summary>
        /// All recognised top-level command verbs.
        /// Used in the Query default case to prevent exact command names from
        /// accidentally being routed to the autocomplete / implicit-SSH paths.
        /// </summary>
        private static readonly string[] AllCommandVerbs = new[]
        {
            CommandProfiles, CommandCustomShell, CommandConfig, CommandHelp, "add"
        };

        // Sub-commands of "profiles"
        private const string ProfilesSubAdd    = "add";
        private const string ProfilesSubRemove = "remove";
        private const string ProfilesSubRename = "rename";
        private const string ProfilesSubCopy   = "copy";
        private const string ProfilesSubExport = "export";
        private const string ProfilesSubImport = "import";

        private static readonly string[] ProfilesSubCommands = new[]
        {
            ProfilesSubAdd, ProfilesSubRemove, ProfilesSubRename,
            ProfilesSubCopy, ProfilesSubExport, ProfilesSubImport
        };

        // Sub-commands of "shell"
        private static readonly string[] ShellSubCommands = new[]
        {
            "add", "remove"
        };

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
                case CommandProfiles:
                    results.AddRange(HandleProfiles(query, rest));
                    break;
                case CommandCustomShell:
                    results.AddRange(HandleShell(query, rest));
                    break;
                case CommandConfig:
                    results.AddRange(HandleConfig(query, rest));
                    break;
                case CommandHelp:
                    results.AddRange(HandleDocs());
                    break;

                // Legacy top-level "add" is no longer the canonical command.
                // Show an explicit user-facing redirect so the user is never left confused.
                case "add":
                    results.AddRange(HandleLegacyAddRedirect(query, rest));
                    break;
                default:
                    // Guard: if the verb exactly matches any known command it should have
                    // been handled by one of the cases above. Reaching here means either a
                    // future refactoring gap or an unexpected call path. Return empty to
                    // prevent unrelated top-level suggestions from appearing in a command view.
                    if (System.Array.IndexOf(AllCommandVerbs, verb) >= 0)
                        break;

                    // If the input looks like a direct SSH destination or option string,
                    // treat it as an implicit direct-connect.
                    if (IsImplicitSshInput(input))
                    {
                        results.AddRange(HandleDirectConnect(query, input));
                    }
                    else
                    {
                        // Show auto-complete suggestions for partial command names.
                        results.AddRange(AutoCompleter.GetSuggestions(
                            query.ActionKeyword, input,
                            _profileManager?.UserData, AppIconPath,
                            _pluginContext?.API));
                    }
                    break;
            }

            return results;
        }

        #region Command Handlers

        private List<Result> HandleProfiles(Query query, string rest)
        {
            var parts = rest.Split(new[] { ' ' }, 2);
            var subCmd = parts[0].ToLowerInvariant();
            var subRest = parts.Length > 1 ? parts[1].Trim() : "";

            switch (subCmd)
            {
                case ProfilesSubAdd:    return HandleProfilesAdd(query, subRest);
                case ProfilesSubRemove: return HandleProfilesRemove(query, subRest);
                case ProfilesSubRename: return HandleProfilesRename(query, subRest);
                case ProfilesSubCopy:   return HandleProfilesCopy(query, subRest);
                case ProfilesSubExport: return HandleProfilesExport(query);
                case ProfilesSubImport: return HandleProfilesImport(query, subRest);
                default:
                    // Mirror the top-level matching pattern: when the partial input
                    // is a prefix of one or more sub-commands, delegate to the
                    // autocompleter so that "profiles a" suggests "add" the same way
                    // "ssh p" suggests "profiles" at the top level.
                    if (!string.IsNullOrEmpty(subCmd) &&
                        ProfilesSubCommands.Any(s => s.StartsWith(subCmd)))
                    {
                        return new List<Result>(AutoCompleter.GetSuggestions(
                            query.ActionKeyword, "profiles " + rest,
                            _profileManager?.UserData, AppIconPath,
                            _pluginContext?.API));
                    }
                    return HandleProfilesList(query, rest);
            }
        }

        // ── profiles (list / connect) ─────────────────────────────────────────────

        private List<Result> HandleProfilesList(Query query, string search)
        {
            var results = new List<Result>();
            var profiles = _profileManager.UserData.Profiles;

            // Always show management hint at the top.
            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles ",
                Score = int.MaxValue
            });

            if (profiles.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles"),
                    SubTitle = GetTranslation("plugin_quickssh_noprofiles"),
                    IcoPath = AppIconPath
                });
                // Fall through to show action rows even when no profiles exist.
            }
            else
            {
                var scored = new List<(int score, string name, SshProfile profile)>();

                foreach (var entry in profiles)
                {
                    var displayCmd = entry.Value?.ToDisplayString() ?? "";
                    if (string.IsNullOrEmpty(search))
                    {
                        scored.Add((0, entry.Key, entry.Value));
                    }
                    else
                    {
                        int score = ScoreProfile(search, entry.Key, displayCmd);
                        if (score < int.MaxValue)
                            scored.Add((score, entry.Key, entry.Value));
                    }
                }

                int profileScore = 500;
                foreach (var item in scored.OrderBy(s => s.score))
                {
                    var name = item.name;
                    var profile = item.profile;
                    var cmd = profile?.ToCommandLine() ?? "";
                    results.Add(new Result
                    {
                        Title = name,
                        SubTitle = cmd,
                        IcoPath = AppIconGreenPath,
                        Score = string.IsNullOrEmpty(search) ? profileScore-- : 0,
                        Action = _ =>
                        {
                            RunCommand(cmd);
                            return true;
                        },
                        AutoCompleteText = query.ActionKeyword + " profiles " + name
                    });
                }
            }

            // Sub-command action rows — always below profile entries, only shown when
            // no search text is active (mirroring the shell sub-command layout).
            if (string.IsNullOrEmpty(search))
            {
                var profileSubCmds = new[]
                {
                    ("add",    GetTranslation("plugin_quickssh_title_commandprofiles_add"),    GetTranslation("plugin_quickssh_subtitle_commandprofiles_add"),         60),
                    ("remove", GetTranslation("plugin_quickssh_title_commandprofiles_remove"), GetTranslation("plugin_quickssh_subtitle_commandprofiles_remove"),      50),
                    ("rename", GetTranslation("plugin_quickssh_title_commandprofiles_rename"), GetTranslation("plugin_quickssh_subtitle_commandprofiles_rename"),      40),
                    ("copy",   GetTranslation("plugin_quickssh_title_commandprofiles_copy"),   GetTranslation("plugin_quickssh_subtitle_commandprofiles_copy_usage"),  30),
                    ("export", GetTranslation("plugin_quickssh_title_commandprofiles_export"), GetTranslation("plugin_quickssh_subtitle_commandprofiles_export_usage"), 20),
                    ("import", GetTranslation("plugin_quickssh_title_commandprofiles_import"), GetTranslation("plugin_quickssh_subtitle_commandprofiles_import_usage"), 10),
                };
                foreach (var (scName, scTitle, scSubTitle, scScore) in profileSubCmds)
                {
                    var autoText = query.ActionKeyword + " profiles " + scName + " ";
                    results.Add(new Result
                    {
                        Title = scTitle,
                        SubTitle = scSubTitle,
                        IcoPath = AppIconPath,
                        AutoCompleteText = autoText,
                        Score = scScore,
                        Action = _ =>
                        {
                            _pluginContext?.API?.ChangeQuery(autoText, true);
                            return false;
                        }
                    });
                }
            }

            return results;
        }

        // ── legacy "add" redirect ─────────────────────────────────────────────────

        /// <summary>
        /// The top-level "add" command was removed in v2.  All profile operations are now
        /// sub-commands of "profiles".  This handler shows an unambiguous user-facing message
        /// rather than silently falling through to autocomplete or implicit-SSH detection.
        /// </summary>
        private List<Result> HandleLegacyAddRedirect(Query query, string rest)
        {
            var redirectTarget = query.ActionKeyword + " profiles add " + rest;
            return new List<Result>
            {
                // Pinned hint at the top.
                new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandadd_legacy"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandadd_legacy"),
                    IcoPath = AppIconPath,
                    AutoCompleteText = redirectTarget,
                    Score = int.MaxValue,
                    Action = _ =>
                    {
                        _pluginContext?.API?.ChangeQuery(redirectTarget, true);
                        return false;
                    }
                }
            };
        }

        // ── profiles add ──────────────────────────────────────────────────────────

        private List<Result> HandleProfilesAdd(Query query, string rest)
        {
            var results = new List<Result>();

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_add"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_add"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles add ",
                Score = int.MaxValue
            });

            if (string.IsNullOrEmpty(rest))
                return results;

            var addParts = rest.Split(new[] { ' ' }, 2);
            var profileName = addParts[0];
            var rawCommand = addParts.Length > 1 ? addParts[1].Trim() : "";

            if (!string.IsNullOrEmpty(rawCommand))
            {
                // Normalise: strip cmd-style /flags, ensure "ssh " prefix.
                var sshCommand = NormalizeSshCommand(rawCommand) ?? "";
                if (!string.IsNullOrEmpty(sshCommand))
                {
                    var profile = SshProfile.ParseFromLegacyCommand(sshCommand);
                    var displayCmd = profile.ToCommandLine();
                    results.Add(new Result
                    {
                        Title = GetTranslation("plugin_quickssh_save_label") + " " + profileName,
                        SubTitle = displayCmd,
                        IcoPath = AppIconGreenPath,
                        Action = _ =>
                        {
                            _profileManager.UserData.Profiles[profileName] = profile;
                            return true;
                        }
                    });
                }
            }

            return results;
        }

        // ── profiles remove ───────────────────────────────────────────────────────

        private List<Result> HandleProfilesRemove(Query query, string rest)
        {
            var results = new List<Result>();
            var profiles = _profileManager.UserData.Profiles;

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_remove"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_remove"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles remove ",
                Score = int.MaxValue
            });

            if (profiles.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles_remove"),
                    SubTitle = GetTranslation("plugin_quickssh_noprofiles"),
                    IcoPath = AppIconPath
                });
                return results;
            }

            foreach (var entry in profiles)
            {
                if (!string.IsNullOrEmpty(rest) &&
                    !entry.Key.ToLowerInvariant().Contains(rest.ToLowerInvariant()))
                    continue;

                var cmd = entry.Value?.ToCommandLine() ?? "";
                results.Add(new Result
                {
                    Title = entry.Key,
                    SubTitle = cmd,
                    IcoPath = AppIconRedPath,
                    AutoCompleteText = query.ActionKeyword + " profiles remove " + entry.Key,
                    Action = _ =>
                    {
                        _profileManager.UserData.Profiles.Remove(entry.Key);
                        return true;
                    }
                });
            }

            return results;
        }

        // ── profiles rename ───────────────────────────────────────────────────────

        private List<Result> HandleProfilesRename(Query query, string rest)
        {
            var results = new List<Result>();
            var profiles = _profileManager.UserData.Profiles;

            var parts = rest.Split(new[] { ' ' }, 2);
            var oldName = parts[0].Trim();
            var newName = parts.Length > 1 ? parts[1].Trim() : "";

            if (string.IsNullOrEmpty(oldName))
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles_rename"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_rename"),
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " profiles rename ",
                    Score = int.MaxValue
                });

                if (profiles.Count == 0)
                {
                    results.Add(new Result
                    {
                        Title = GetTranslation("plugin_quickssh_title_commandprofiles_rename"),
                        SubTitle = GetTranslation("plugin_quickssh_noprofiles"),
                        IcoPath = AppIconPath
                    });
                    return results;
                }

                foreach (var entry in profiles)
                {
                    var name = entry.Key;
                    var autoText = query.ActionKeyword + " profiles rename " + name + " ";
                    results.Add(new Result
                    {
                        Title = name,
                        SubTitle = entry.Value?.ToCommandLine() ?? "",
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

            if (!profiles.ContainsKey(oldName))
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles_rename"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_rename"),
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " profiles rename ",
                    Score = int.MaxValue
                });
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles_rename") + ": " + oldName,
                    SubTitle = GetTranslation("plugin_quickssh_rename_notfound"),
                    IcoPath = AppIconRedPath
                });
                return results;
            }

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_rename"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_rename"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles rename " + oldName + " ",
                Score = int.MaxValue
            });

            if (!string.IsNullOrEmpty(newName))
            {
                var profileValue = profiles[oldName];
                results.Add(new Result
                {
                    Title = oldName + " → " + newName,
                    SubTitle = profileValue?.ToCommandLine() ?? "",
                    IcoPath = AppIconGreenPath,
                    Action = _ =>
                    {
                        var value = profiles[oldName];
                        profiles.SetCallback(null);
                        try
                        {
                            profiles.Remove(oldName);
                            profiles[newName] = value;
                        }
                        finally
                        {
                            profiles.SetCallback(_profileManager.SaveConfiguration);
                        }
                        _profileManager.SaveConfiguration();
                        return true;
                    }
                });
            }

            return results;
        }

        // ── profiles copy ─────────────────────────────────────────────────────────

        private List<Result> HandleProfilesCopy(Query query, string search)
        {
            var results = new List<Result>();
            var profiles = _profileManager.UserData.Profiles;

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_copy"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_copy_usage"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles copy ",
                Score = int.MaxValue
            });

            if (profiles.Count == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles_copy"),
                    SubTitle = GetTranslation("plugin_quickssh_noprofiles"),
                    IcoPath = AppIconPath
                });
                return results;
            }

            foreach (var entry in profiles)
            {
                if (!string.IsNullOrEmpty(search) &&
                    !entry.Key.ToLowerInvariant().Contains(search.ToLowerInvariant()))
                    continue;

                var name = entry.Key;
                var cmd = entry.Value?.ToCommandLine() ?? "";
                results.Add(new Result
                {
                    Title = name,
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_copy") + " " + cmd,
                    IcoPath = AppIconGreenPath,
                    AutoCompleteText = query.ActionKeyword + " profiles copy " + name,
                    Action = _ =>
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(cmd);
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

            return results;
        }

        // ── profiles export ───────────────────────────────────────────────────────

        private List<Result> HandleProfilesExport(Query query)
        {
            var results = new List<Result>();
            var exportPath = Path.Combine(_dataDir, "profiles_export.sshconfig");

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_export"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_export_usage"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles export ",
                Score = int.MaxValue
            });

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_export"),
                SubTitle = string.Format(GetTranslation("plugin_quickssh_subtitle_commandprofiles_export"), exportPath),
                IcoPath = AppIconGreenPath,
                AutoCompleteText = query.ActionKeyword + " profiles export ",
                Action = _ =>
                {
                    try
                    {
                        Directory.CreateDirectory(_dataDir);
                        var profiles = _profileManager.UserData.Profiles
                            .ToDictionary(e => e.Key, e => e.Value);
                        var text = ProfileSerializer.Serialize(profiles);
                        File.WriteAllText(exportPath, text);
                        _pluginContext.API.ShowMsg("QuickSSH",
                            string.Format(GetTranslation("plugin_quickssh_export_success"),
                                profiles.Count, exportPath));
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

        // ── profiles import ───────────────────────────────────────────────────────

        private List<Result> HandleProfilesImport(Query query, string rest)
        {
            var results = new List<Result>();

            string[] importFiles = Array.Empty<string>();
            try
            {
                if (Directory.Exists(_dataDir))
                {
                    // Accept both new .sshconfig and legacy .json files
                    var sshconfig = Directory.GetFiles(_dataDir, "*.sshconfig");
                    var json = Directory.GetFiles(_dataDir, "*.json");
                    importFiles = sshconfig.Concat(json).ToArray();
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandprofiles_import"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandprofiles_import_usage"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " profiles import ",
                Score = int.MaxValue
            });

            if (importFiles.Length == 0)
            {
                results.Add(new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandprofiles_import"),
                    SubTitle = string.Format(GetTranslation("plugin_quickssh_import_nofiles"), _dataDir),
                    IcoPath = AppIconPath,
                    AutoCompleteText = query.ActionKeyword + " profiles import "
                });
                return results;
            }

            foreach (var file in importFiles)
            {
                var fileName = Path.GetFileName(file);
                if (!string.IsNullOrEmpty(rest) &&
                    !fileName.ToLowerInvariant().Contains(rest.ToLowerInvariant()))
                    continue;

                // Mark legacy .json files clearly in the result title so users understand
                // that .json is migration-only and the canonical format is .sshconfig.
                bool isLegacyJson = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                var displayTitle = GetTranslation("plugin_quickssh_title_commandprofiles_import") + ": " + fileName
                    + (isLegacyJson ? " " + GetTranslation("plugin_quickssh_import_legacy_label") : "");

                results.Add(new Result
                {
                    Title = displayTitle,
                    SubTitle = file,
                    IcoPath = AppIconGreenPath,
                    AutoCompleteText = query.ActionKeyword + " profiles import " + fileName,
                    Action = _ =>
                    {
                        try
                        {
                            ImportProfilesFromFile(file);
                        }
                        catch (Exception ex)
                        {
                            _pluginContext.API.ShowMsg("QuickSSH", "Error: " + ex.Message);
                        }
                        return true;
                    }
                });
            }

            return results;
        }

        /// <summary>
        /// Imports profiles from a file into the structured profile store.
        /// </summary>
        /// <remarks>
        /// Canonical import format: <c>.sshconfig</c> (SSH-config-like text, written by "profiles export").
        /// <para/>
        /// Migration-only format: <c>.json</c> (v1 raw-command dictionary).
        /// JSON files are <b>never written</b> by this plugin and are accepted here solely for
        /// backward-compatibility migration.  They are clearly labelled "(legacy)" in the UI.
        /// </remarks>
        private void ImportProfilesFromFile(string filePath)
        {
            var text = File.ReadAllText(filePath);
            Dictionary<string, SshProfile> imported;

            if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                // MIGRATION-ONLY PATH: read v1 raw-command JSON (Dictionary<string, string>)
                // and parse each command into a structured SshProfile.
                // Unknown flags that cannot be parsed are stored in SshProfile.ExtraArgs
                // so no information is silently lost.
                // This path is NOT used for canonical import/export; use .sshconfig files instead.
                var legacy = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
                if (legacy == null || legacy.Count == 0)
                {
                    _pluginContext.API.ShowMsg("QuickSSH",
                        GetTranslation("plugin_quickssh_import_empty"));
                    return;
                }
                imported = new Dictionary<string, SshProfile>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in legacy)
                    imported[kvp.Key] = SshProfile.ParseFromLegacyCommand(kvp.Value);
            }
            else
            {
                imported = ProfileSerializer.Deserialize(text);
                if (imported.Count == 0)
                {
                    _pluginContext.API.ShowMsg("QuickSSH",
                        GetTranslation("plugin_quickssh_import_empty"));
                    return;
                }
            }

            int count = 0;
            _profileManager.UserData.Profiles.SetCallback(null);
            try
            {
                foreach (var kvp in imported)
                {
                    if (!_profileManager.UserData.Profiles.ContainsKey(kvp.Key))
                    {
                        _profileManager.UserData.Profiles[kvp.Key] = kvp.Value;
                        count++;
                    }
                }
            }
            finally
            {
                _profileManager.UserData.Profiles.SetCallback(_profileManager.SaveConfiguration);
            }

            if (count > 0)
                _profileManager.SaveConfiguration();

            _pluginContext.API.ShowMsg("QuickSSH",
                string.Format(GetTranslation("plugin_quickssh_import_success"), count));
        }

        private List<Result> HandleDirectConnect(Query query, string rest)
        {
            var results = new List<Result>();

            // Always show usage hint at the top.
            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commanddirect"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect_usage"),
                IcoPath = AppIconPath,
                Score = int.MaxValue
            });

            if (string.IsNullOrEmpty(rest))
                return results;

            // Normalise the user input: strip accidental cmd-style /flags and
            // ensure the command starts with "ssh ".
            var sshCmd = NormalizeSshCommand(rest);
            if (string.IsNullOrEmpty(sshCmd))
                return results;

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_connect_label") + " " + rest,
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commanddirectconnect") + " " + sshCmd,
                IcoPath = AppIconGreenPath,
                Action = _ =>
                {
                    RunCommand(sshCmd);
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
                    // Always show usage hint at the top.
                    results.Add(new Result
                    {
                        Title = GetTranslation("plugin_quickssh_title_commandshell_add"),
                        SubTitle = GetTranslation("plugin_quickssh_subtitle_commandshell_add_usage"),
                        IcoPath = AppIconPath,
                        Score = int.MaxValue
                    });
                    if (!string.IsNullOrEmpty(subRest))
                    {
                        var (name, value) = ParseShellAddArgs(subRest);
                        results.Add(new Result
                        {
                            Title = GetTranslation("plugin_quickssh_addshell_label") + " " + name,
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
                    // Always show usage hint at the top.
                    results.Add(new Result
                    {
                        Title = GetTranslation("plugin_quickssh_title_commandshell_remove"),
                        SubTitle = GetTranslation("plugin_quickssh_subtitle_commandshell_remove"),
                        IcoPath = AppIconPath,
                        AutoCompleteText = query.ActionKeyword + " shell remove ",
                        Score = int.MaxValue
                    });
                    if (shells.Count == 0)
                    {
                        results.Add(new Result
                        {
                            Title = GetTranslation("plugin_quickssh_title_commandshell_remove"),
                            SubTitle = GetTranslation("plugin_quickssh_noshells"),
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
                    // Mirror the top-level matching pattern: when the partial input
                    // is a prefix of one or more sub-commands, delegate to the
                    // autocompleter so that "shell a" suggests "add" the same way
                    // "ssh p" suggests "profiles" at the top level.
                    if (!string.IsNullOrEmpty(subCmd) &&
                        ShellSubCommands.Any(s => s.StartsWith(subCmd)))
                    {
                        return new List<Result>(AutoCompleter.GetSuggestions(
                            query.ActionKeyword, "shell " + rest,
                            _profileManager?.UserData, AppIconPath,
                            _pluginContext?.API));
                    }

                    // Always show "Shell management" hint at the top.
                    results.Add(new Result
                    {
                        Title = GetTranslation("plugin_quickssh_title_commandshell"),
                        SubTitle = GetTranslation("plugin_quickssh_subtitle_commandshell_help"),
                        IcoPath = AppIconPath,
                        AutoCompleteText = query.ActionKeyword + " shell ",
                        Score = int.MaxValue
                    });

                    // List shells in deterministic order:
                    //   1. selected shell  (Score 1000)
                    //   2. other shells    (decreasing from 500)
                    //   3. action rows     (10 = add, 5 = remove)
                    var allShells = _profileManager.UserData.CustomShell;
                    var selected = _profileManager.UserData.SelectedCustomShell;

                    // Selected shell (if any) — pinned just below the usage hint.
                    if (!string.IsNullOrEmpty(selected) && allShells.ContainsKey(selected))
                    {
                        var shellVal = allShells[selected];
                        results.Add(new Result
                        {
                            Title = selected + " " + GetTranslation("plugin_quickssh_shell_selected"),
                            SubTitle = string.IsNullOrEmpty(shellVal) ? selected : shellVal,
                            IcoPath = AppIconGreenPath,
                            AutoCompleteText = query.ActionKeyword + " shell " + selected,
                            Score = 1000,
                            Action = _ =>
                            {
                                _profileManager.UserData.SelectedCustomShell = selected;
                                _profileManager.SaveConfiguration();
                                return true;
                            }
                        });
                    }

                    // Remaining (non-selected) shell profiles.
                    int otherShellScore = 500;
                    foreach (var shell in allShells)
                    {
                        if (shell.Key == selected)
                            continue;
                        results.Add(new Result
                        {
                            Title = shell.Key,
                            SubTitle = string.IsNullOrEmpty(shell.Value) ? shell.Key : shell.Value,
                            IcoPath = AppIconGreenPath,
                            AutoCompleteText = query.ActionKeyword + " shell " + shell.Key,
                            Score = otherShellScore--,
                            Action = _ =>
                            {
                                _profileManager.UserData.SelectedCustomShell = shell.Key;
                                _profileManager.SaveConfiguration();
                                return true;
                            }
                        });
                    }

                    // Sub-command action rows (add / remove) — always below shell entries.
                    var shellSubCmds = new[]
                    {
                        ("add",    GetTranslation("plugin_quickssh_title_commandshell_add"),    GetTranslation("plugin_quickssh_subtitle_commandshell_add_usage"),    10),
                        ("remove", GetTranslation("plugin_quickssh_title_commandshell_remove"), GetTranslation("plugin_quickssh_subtitle_commandshell_remove"),        5),
                    };
                    foreach (var (scName, scTitle, scSubTitle, scScore) in shellSubCmds)
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
                                Score = scScore,
                                Action = _ =>
                                {
                                    _pluginContext?.API?.ChangeQuery(autoText, true);
                                    return false;
                                }
                            });
                        }
                    }
                    break;
            }

            return results;
        }

        private List<Result> HandleConfig(Query query, string rest)
        {
            // Both "config" and "config import" trigger the same import action.
            var results = new List<Result>();

            results.Add(new Result
            {
                Title = GetTranslation("plugin_quickssh_title_commandconfig"),
                SubTitle = GetTranslation("plugin_quickssh_subtitle_commandconfig_usage"),
                IcoPath = AppIconPath,
                AutoCompleteText = query.ActionKeyword + " config ",
                Score = int.MaxValue
            });

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
                            if (!_profileManager.UserData.Profiles.ContainsKey(host.Key))
                            {
                                _profileManager.UserData.Profiles[host.Key] = host.Value;
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
                // Always show usage hint at the top.
                new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandhelp"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandhelp_usage"),
                    IcoPath = AppIconPath,
                    Score = int.MaxValue
                },
                new Result
                {
                    Title = GetTranslation("plugin_quickssh_title_commandhelp"),
                    SubTitle = GetTranslation("plugin_quickssh_subtitle_commandhelp"),
                    IcoPath = AppIconGreenPath,
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

        #region SSH / SCP Execution

        /// <summary>
        /// Normalises a raw SSH or SCP command string so it is safe to pass to a terminal.
        /// <list type="bullet">
        ///   <item>Strips leading Windows cmd.exe-style /flags (e.g. "/c", "/k") that
        ///   users sometimes accidentally prepend.</item>
        ///   <item>Auto-prepends "ssh " when the user supplied only a destination
        ///   (e.g. "user@host" instead of "ssh user@host").</item>
        ///   <item>Removes /flags that appear immediately after the "ssh " prefix for
        ///   the same reason (e.g. "ssh /c user@host" → "ssh user@host").</item>
        /// </list>
        /// SCP commands ("scp ...") are returned unchanged after /flag stripping.
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

            // SCP commands are returned as-is (after /flag stripping above).
            if (cmd.StartsWith("scp ", StringComparison.OrdinalIgnoreCase)
                || cmd.Equals("scp", StringComparison.OrdinalIgnoreCase))
                return cmd;

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

        /// <summary>
        /// Returns <see langword="true"/> when the input string looks like a direct SSH
        /// destination or option string rather than a plugin command name.
        /// Supports:
        /// <list type="bullet">
        ///   <item><c>user@host</c> — contains an at-sign</item>
        ///   <item><c>-p 22 user@host</c> — starts with a dash (SSH option flag)</item>
        ///   <item><c>10.100.100.110</c> or <c>myserver.example.com</c> — bare hostname / IP
        ///   (only hostname-safe characters, at least one dot)</item>
        /// </list>
        /// </summary>
        internal static bool IsImplicitSshInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            // SSH option flag (e.g. -p, -i, -o, -L, -R, -D, etc.)
            if (input[0] == '-')
                return true;

            // user@host format
            if (input.Contains('@'))
                return true;

            // Bare hostname or IP address: check only the first token so that
            // "10.0.0.1 -p 22" is still detected via the first token.
            var firstToken = input.Split(' ', 2)[0];
            return IsHostnameOrIp(firstToken);
        }

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="token"/> looks like a
        /// hostname or dotted IP address (only hostname-safe chars and at least one dot).
        /// </summary>
        private static bool IsHostnameOrIp(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            bool hasDot = false;
            foreach (var c in token)
            {
                if (c == '.') { hasDot = true; continue; }
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') continue;
                return false; // illegal char — not a hostname/IP
            }
            return hasDot; // must have at least one dot to be unambiguous
        }

        private void RunCommand(string command)
        {
            // Normalise: strip accidental Windows cmd-style /flags. For SSH commands,
            // also ensure the "ssh " prefix is present.
            command = NormalizeSshCommand(command);
            if (string.IsNullOrEmpty(command))
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
                    arguments = command;
                }
                else
                {
                    // Parse the shell value into exe + args
                    var spaceIdx = shellValue.IndexOf(' ');
                    if (spaceIdx < 0)
                    {
                        fileName = Utils.ResolveExecutable(shellValue);
                        arguments = command;
                    }
                    else
                    {
                        fileName = Utils.ResolveExecutable(shellValue.Substring(0, spaceIdx));
                        arguments = shellValue.Substring(spaceIdx + 1) + " " + command;
                    }
                }
            }
            else
            {
                // Default: use cmd.exe with /k so the window stays open after SSH/SCP exits,
                // allowing the user to see any connection-error messages.
                fileName = GetCmdExePath();
                arguments = "/k " + command;
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
                            Arguments = "/k " + command,
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