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

## License

[MIT](LICENSE)