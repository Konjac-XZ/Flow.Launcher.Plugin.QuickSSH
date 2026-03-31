# QuickSSH — Flow Launcher Plugin

Enhanced SSH connection plugin for [Flow Launcher](https://www.flowlauncher.com/) with TAB auto-completion, SSH config import, custom shell support, and fuzzy search.

Inspired by [Melv1no/Flow.Launcher.Plugin.easyssh](https://github.com/Melv1no/Flow.Launcher.Plugin.easyssh).

## Features

| Command | Description |
|---------|-------------|
| `ssh profiles` (or `ssh p`) | List, search, and connect to saved SSH profiles |
| `ssh add <name> <ssh-command>` | Save a new SSH profile |
| `ssh remove` | Delete a saved profile |
| `ssh d <ssh-command>` | Direct SSH connection without saving |
| `ssh shell` | Manage custom shell interpreters (add / remove / select) |
| `ssh config` | Import hosts from `~/.ssh/config` |
| `ssh docs` | Open plugin documentation |

### Enhanced Features

- **TAB auto-completion** — press TAB to auto-complete commands and profile names
- **SSH config import** — parse and import hosts from `~/.ssh/config`
- **Proper quoting & escaping** — handles SSH keys and paths with spaces correctly
- **Fuzzy search** — accent-insensitive search with Damerau-Levenshtein distance
- **Custom shells** — use cmd.exe, PowerShell, WSL, Git Bash, Kitty, or any shell
- **Multi-language support** — i18n ready via Flow Launcher translations
- **Atomic saves** — profile data is saved atomically to prevent corruption

## Usage Examples

### Save a profile and connect

```
ssh add myserver ssh user@192.168.1.100
ssh add production ssh -i "C:\Users\me\.ssh\id_rsa" admin@prod.example.com -p 2222
```

### List and connect to saved profiles

```
ssh profiles           → shows all saved profiles
ssh p myserver         → fuzzy-searches for "myserver" and connects on Enter
```

### Quick one-time connection (without saving)

```
ssh d ssh root@10.0.0.1
ssh d ssh -p 2222 deploy@staging.example.com
```

### Import hosts from SSH config

```
ssh config             → imports all hosts from ~/.ssh/config
```

### Custom shell management

```
ssh shell                          → list available shells
ssh shell add PowerShell           → add PowerShell as a shell option
ssh shell add GitBash "C:\Program Files\Git\bin\bash.exe" -c
ssh shell remove PowerShell        → remove a shell
```

Select a shell by clicking it in the list — all SSH commands will then launch through that shell.

## Installation

### From Flow Launcher (after plugin is published)

1. Open Flow Launcher
2. Type `pm install QuickSSH`
3. Restart Flow Launcher

### Manual Installation

1. Download `Flow.Launcher.Plugin.QuickSSH.zip` from [Releases](https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH/releases)
2. Extract the zip into a new folder:
   ```
   %APPDATA%\FlowLauncher\Plugins\QuickSSH\
   ```
3. Restart Flow Launcher
4. Type `ssh` to verify the plugin loaded

## Building from Source

Requires Windows with .NET 9.0 SDK installed.

```powershell
# Clone the repository
git clone https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH.git
cd Flow.Launcher.Plugin.QuickSSH

# Build
dotnet publish -c Release -r win-x64 --no-self-contained

# The output will be in bin\Release\win-x64\publish\
# Copy the contents to your Flow Launcher plugins folder:
# %APPDATA%\FlowLauncher\Plugins\QuickSSH\
```

## Requirements

- Windows with OpenSSH client installed (`ssh` available in PATH)
- [Flow Launcher](https://www.flowlauncher.com/) v1.19+
- .NET 9.0 Runtime

## Data Storage

Profiles are stored in `~/.ssh/profiles.json`.

## Publishing to Flow Launcher Plugin Store

To make QuickSSH available via `pm install QuickSSH` in Flow Launcher:

1. **Create a GitHub Release** — run the *Publish Release* workflow from the **Actions** tab. It builds the zip and creates a tagged release automatically.

2. **Fork the Plugin Manifest** — fork [Flow-Launcher/Flow.Launcher.PluginsManifest](https://github.com/Flow-Launcher/Flow.Launcher.PluginsManifest).

3. **Add a manifest entry** — in your fork create the file:

   ```
   plugins/QuickSSH-86AC23FE48BC45E5B7E0A94F5847FA83.json
   ```

   with the following content (update `UrlDownload` to point to the latest release zip):

   ```json
   {
     "ID": "86AC23FE48BC45E5B7E0A94F5847FA83",
     "Name": "QuickSSH",
     "Description": "Enhanced SSH connection plugin with TAB auto-completion, SSH config support, and improved shell handling",
     "Author": "Vaso73",
     "Version": "1.0.3",
     "Language": "csharp",
     "MinFlowLauncherVersion": "1.19.0",
     "Website": "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH",
     "UrlSourceCode": "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH",
     "UrlDownload": "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH/releases/download/v1.0.3/Flow.Launcher.Plugin.QuickSSH.zip",
     "IcoPath": "https://raw.githubusercontent.com/Vaso73/Flow.Launcher.Plugin.QuickSSH/main/Images/app.png"
   }
   ```

4. **Open a Pull Request** — submit the PR to `Flow-Launcher/Flow.Launcher.PluginsManifest`. Once merged the plugin becomes available in the Flow Launcher store.

## Contributing

Contributions are welcome! Here is the typical workflow:

1. Fork the repository and create a feature branch.
2. Make your changes.
3. Run a local build to verify nothing is broken:
   ```powershell
   dotnet publish -c Release -r win-x64 --no-self-contained
   ```
4. Update `plugin.json` version if you are preparing a new release.
5. Open a Pull Request describing your changes.

### Releasing a new version

1. Update `"Version"` in `plugin.json` (e.g. `1.0.4`).
2. Commit and push to `main`.
3. Go to **Actions → Publish Release → Run workflow** — this builds the zip and creates a GitHub Release tagged `v<version>`.
4. Update `UrlDownload` and `Version` in the Plugin Manifest entry (see above) and submit a PR there.

## License

[MIT](LICENSE)