# QuickSSH — Flow Launcher Plugin

Enhanced SSH connection plugin for [Flow Launcher](https://www.flowlauncher.com/) with TAB auto-completion, SSH config import, profile export/import, custom shell support, and fuzzy search.

Inspired by [Melv1no/Flow.Launcher.Plugin.easyssh](https://github.com/Melv1no/Flow.Launcher.Plugin.easyssh).

## Features

| Command | Description |
|---------|-------------|
| `ssh add <name> <ssh-command>` | Save a new SSH profile |
| `ssh remove [filter]` | Delete a saved profile |
| `ssh profiles [filter]` / `ssh p [filter]` | List, search, and connect to saved profiles |
| `ssh d <ssh-command>` | Direct SSH connection without saving |
| `ssh shell` | Manage custom shell interpreters (add / remove / select) |
| `ssh config` | Import hosts from `~/.ssh/config` |
| `ssh export` | Export all profiles to a JSON file |
| `ssh import [filter]` | Import profiles from a JSON file |
| `ssh copy [filter]` | Copy an SSH command to the clipboard |
| `ssh rename <oldname> <newname>` | Rename an existing profile |
| `ssh docs` | Open plugin documentation |

### Capabilities

- **TAB auto-completion** — press TAB to auto-complete commands and profile names
- **In-line usage hints** — every command view shows a pinned usage/help entry at the top of the results list
- **SSH config import** — parse and import hosts from `~/.ssh/config`
- **Profile export / import** — back up and restore profiles as plain JSON files
- **Proper quoting & escaping** — handles SSH keys and paths with spaces correctly
- **Fuzzy search** — accent-insensitive search with Damerau-Levenshtein distance
- **Command normalisation** — auto-prepends `ssh ` when you type only a destination
- **Custom shells** — use cmd.exe, PowerShell, WSL, Git Bash, Kitty, or any terminal
- **Multi-language support** — English, Slovak, French, German, Russian, Polish, and Spanish (i18n via Flow Launcher)
- **Atomic saves** — profile data is written atomically to prevent corruption
- **Custom-shell fallback** — if the selected shell cannot start, cmd.exe is used automatically

---

## Usage Examples

### Add a profile and connect

```
ssh add myserver ssh user@192.168.1.100
ssh add production ssh -i "C:\Users\me\.ssh\id_rsa" admin@prod.example.com -p 2222
ssh add dev-box ssh -p 2222 dev@10.0.0.50
```

You can also omit the `ssh` prefix — the plugin adds it for you:

```
ssh add bastion admin@bastion.example.com -p 22222
```

### List and connect to saved profiles

```
ssh profiles           → show all saved profiles
ssh p                  → shorthand for ssh profiles
ssh p myserver         → fuzzy-search for "myserver"; press Enter to connect
ssh profiles prod      → filter profiles containing "prod"
```

### Remove a profile

```
ssh remove             → list all profiles for removal
ssh remove prod        → filter profiles by "prod", then click to delete
```

### Copy an SSH command to the clipboard

```
ssh copy               → list all profiles for copying
ssh copy myserver      → filter by "myserver", then click to copy the SSH command
```

### Rename a profile

```
ssh rename                        → list all profiles to select for renaming
ssh rename myserver               → pick "myserver" as the source (TAB to auto-complete)
ssh rename myserver new-name      → rename "myserver" to "new-name"
```

### Quick one-time connection (without saving)

```
ssh d root@10.0.0.1
ssh d ssh -p 2222 deploy@staging.example.com
ssh d user@host        → "ssh " prefix is added automatically
```

### Import hosts from `~/.ssh/config`

```
ssh config             → import all Host entries from ~/.ssh/config
```

Only new hosts are imported — existing profiles are not overwritten.

Example `~/.ssh/config` that is fully supported:

```
Host myserver
    HostName 192.168.1.100
    User admin
    Port 22

Host production
    HostName prod.example.com
    User deploy
    Port 2222
    IdentityFile ~/.ssh/id_ed25519

Host bastion
    HostName bastion.corp.internal
    User ec2-user
    IdentityFile "C:\Users\me\.ssh\corp_key"
```

Wildcard entries (`Host *`) are skipped automatically.

### Custom shell management

```
ssh shell                                             → list shells; click one to select
ssh shell add PowerShell                              → add PowerShell (found via PATH)
ssh shell add GitBash "C:\Program Files\Git\bin\bash.exe" --login -i -c
ssh shell add WSL wsl.exe --
ssh shell add WindowsTerminal wt.exe ssh
ssh shell remove PowerShell                           → remove a shell entry
```

**Shell value format** — `ssh shell add <name> [<executable> [extra-args]]`:

| Example | Effect |
|---------|--------|
| `ssh shell add PowerShell` | Name = value = `PowerShell`; resolved via PATH |
| `ssh shell add PS "C:\...\pwsh.exe"` | Name `PS`, explicit exe path, no extra args |
| `ssh shell add GitBash "C:\...\bash.exe" -c` | Name `GitBash`, exe + `-c` flag prepended to command |

Click any shell in the list to **select** it. All SSH connections will then launch through that shell. Click it again to deselect (returns to default `cmd.exe`).

### Export and import profiles

**Export** — saves all current profiles to a JSON file in the plugin data folder:

```
ssh export
```

The file is written to:

```
%APPDATA%\FlowLauncher\Plugins\QuickSSH\data\profiles_export.json
```

**Import** — loads profiles from any `*.json` file placed in the same `data\` folder:

```
ssh import                       → list all JSON files available for import
ssh import mybackup              → filter files containing "mybackup"
```

Place your backup file in the `data\` folder, then run `ssh import` and click the file.
Only profiles that do not already exist are added (no overwriting).

Example JSON format accepted by import:

```json
{
  "myserver": "ssh user@192.168.1.100",
  "production": "ssh -i \"C:\\Users\\me\\.ssh\\id_rsa\" admin@prod.example.com -p 2222",
  "bastion": "ssh -p 22222 ec2-user@bastion.corp.internal"
}
```

---

## Data Storage

| File | Purpose |
|------|---------|
| `~/.ssh/profiles.json` | Main profile and shell database |
| `%APPDATA%\FlowLauncher\Plugins\QuickSSH\data\*.json` | Import / export files |

### profiles.json schema

```json
{
  "PluginVersion": "1.0",
  "EntriesLists": {
    "<profile-name>": "<full ssh command>"
  },
  "CustomShellLists": {
    "<shell-name>": "<executable path + optional args>"
  },
  "SelectedCustomShell": "<shell-name or null>"
}
```

**Example:**

```json
{
  "PluginVersion": "1.0",
  "EntriesLists": {
    "myserver": "ssh user@192.168.1.100",
    "production": "ssh -i \"C:\\Users\\me\\.ssh\\id_rsa\" admin@prod.example.com -p 2222"
  },
  "CustomShellLists": {
    "PowerShell": "",
    "GitBash": "C:\\Program Files\\Git\\bin\\bash.exe -c"
  },
  "SelectedCustomShell": "GitBash"
}
```

---

## Installation

### From Flow Launcher (after plugin is published)

1. Open Flow Launcher
2. Type `pm install QuickSSH`
3. Restart Flow Launcher

### Manual Installation

1. Download `QuickSSH.zip` from [Releases](https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH/releases)
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

# Output: bin\Release\win-x64\publish\
# Copy that folder's contents to:
# %APPDATA%\FlowLauncher\Plugins\QuickSSH\
```

## Requirements

- Windows 10 version 1809+ or Windows Server 2019+ (built-in OpenSSH)  
  — or any Windows with `ssh.exe` available in PATH
- [Flow Launcher](https://www.flowlauncher.com/) v1.19+
- .NET 9.0 Runtime (bundled with Flow Launcher v1.19+)

## Languages

| Code | Language |
|------|----------|
| `en` | English |
| `sk` | Slovak (Slovenčina) |
| `fr` | French (Français) |
| `de` | German (Deutsch) |
| `ru` | Russian (Русский) |
| `pl` | Polish (Polski) |
| `es` | Spanish (Español) |

Flow Launcher automatically selects the language that matches your system locale.

## Publishing to Flow Launcher Plugin Store

To make QuickSSH available via `pm install QuickSSH` in Flow Launcher:

1. **Create a GitHub Release** — open a Pull Request in this repository, add one label (`release:patch`, `release:minor`, or `release:major`), and merge it into `main`. GitHub Actions will automatically build `QuickSSH.zip`, create a tag, and publish the release.

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
     "Version": "1.0.13",
     "Language": "csharp",
     "MinFlowLauncherVersion": "1.19.0",
     "Website": "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH",
     "UrlSourceCode": "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH",
     "UrlDownload": "https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH/releases/latest/download/QuickSSH.zip",
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
4. If the Pull Request should create a release, add one label: `release:patch`, `release:minor`, or `release:major`.
5. Open a Pull Request describing your changes.

### Releasing a new version

1. Open a Pull Request with your changes.
2. Add one label: `release:patch`, `release:minor`, or `release:major`.
3. Merge the Pull Request into `main`.
4. GitHub Actions automatically builds `QuickSSH.zip`, creates a new tag, and publishes a GitHub Release.
5. Update the Plugin Manifest entry in `Flow-Launcher/Flow.Launcher.PluginsManifest` if needed.

## License

[MIT](LICENSE)
