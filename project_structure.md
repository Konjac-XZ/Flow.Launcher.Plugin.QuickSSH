# Project Structure

```
Flow.Launcher.Plugin.QuickSSH/
├── Flow.Launcher.Plugin.QuickSSH.csproj  # .NET project file
├── plugin.json                           # Flow Launcher plugin metadata
├── Main.cs                               # Main plugin class (IPlugin, IPluginI18n)
├── Profile.cs                            # UserData, AutoSaveDictionary, ProfileManager
├── Utils.cs                              # SSH detection, executable resolution
├── SshConfigParser.cs                    # Parse ~/.ssh/config hosts
├── SshCommandBuilder.cs                  # SSH command quoting/escaping
├── AutoCompleter.cs                      # TAB auto-completion suggestions
├── Languages/
│   └── en.xaml                           # English translations
├── Images/
│   └── app.png                           # Plugin icon
├── README.md                             # Documentation
└── .gitignore                            # Git ignore rules
```