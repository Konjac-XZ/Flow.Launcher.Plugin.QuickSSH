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

## Installation

### From Flow Launcher

1. Open Flow Launcher
2. Type `pm install QuickSSH`
3. Restart Flow Launcher

### Manual Installation

1. Download the latest release from [Releases](https://github.com/Vaso73/Flow.Launcher.Plugin.QuickSSH/releases)
2. Extract to `%APPDATA%\FlowLauncher\Plugins\`
3. Restart Flow Launcher

## Building from Source

```bash
dotnet restore
dotnet build
```

## Requirements

- Windows with OpenSSH client installed
- [Flow Launcher](https://www.flowlauncher.com/) v1.8+
- .NET 7.0 Runtime

## Data Storage

Profiles are stored in `~/.ssh/profiles.json`.

## License

MIT