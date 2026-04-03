# QuickSSH — Flow Launcher Plugin

Enhanced SSH/SCP connection plugin for [Flow Launcher](https://www.flowlauncher.com/) with query autocomplete, structured profile management, SSH config import, human-readable profile export/import, custom shell support, and fuzzy search.

Inspired by [Melv1no/Flow.Launcher.Plugin.easyssh](https://github.com/Melv1no/Flow.Launcher.Plugin.easyssh).

## Command Structure

| Command | Description |
|---------|-------------|
| `ssh profiles [filter]` | Browse saved profiles and connect |
| `ssh profiles add <name> <ssh-command>` | Save a new SSH or SCP profile |
| `ssh profiles remove [filter]` | Delete a saved profile |
| `ssh profiles rename <oldname> <newname>` | Rename an existing profile |
| `ssh profiles copy [filter]` | Copy an SSH/SCP command to the clipboard |
| `ssh profiles export` | Export all profiles to a human-readable `.sshconfig` file |
| `ssh profiles import [filter]` | Import profiles from a `.sshconfig` or legacy `.json` file |
| `ssh shell` | Manage custom terminal shells (add / remove / select) |
| `ssh config` | Import hosts from `~/.ssh/config` |
| `ssh help` | Open plugin documentation |
| `ssh <destination>` | **Implicit direct connect** — type a destination or SSH options directly |

> **Suggestion order:** Typing bare `ssh` (with no arguments) shows top-level suggestions in this order: **profiles**, **shell**, **config**, **help**.

> **Partial subcommand matching:** Under `ssh profiles`, subcommand matching reacts from the first matching character, consistently with top-level command matching.
> Examples: `ssh profiles a` → **add**; `ssh profiles r` → **remove**, **rename**; `ssh profiles rem` → **remove**; `ssh profiles ren` → **rename**.
> Single-letter prefixes that match only one subcommand show just that suggestion (e.g. `a` → **add**, `e` → **export**, `i` → **import**, `c` → **copy**).

> **Shell subcommand matching:** Under `ssh shell`, partial subcommand matching works the same way.
> Examples: `ssh shell a` → **add**; `ssh shell r` → **remove**; `ssh shell rem` → **remove**.

> **Note for v1 users:** The top-level `add` command (v1: `ssh add <name> <cmd>`) has been moved to `ssh profiles add <name> <cmd>`.
> Typing `ssh add ...` shows an explicit redirect hint in the UI — it will not silently do something unexpected.

### Capabilities

- **Structured profile model** — profiles are stored as typed, structured objects (not raw strings); supports SSH, RemoteCommand, port-forwards, SCP, ProxyJump, and more
- **Human-readable export/import** — profiles are exported and imported in an SSH-config-like text format (`.sshconfig` files)
- **Legacy migration** — v1 raw-command profiles (JSON) are automatically migrated to the structured format on first load
- **Query autocomplete** — type partial commands or profile names to see matching suggestions; select a result to expand the query
- **Implicit direct SSH input** — type a destination (`user@host`, bare IP/hostname) or SSH options (`-p 22 user@host`, `-i key user@host`) directly without any command prefix
- **SSH config import** — parse and import hosts from `~/.ssh/config`
- **SCP support** — save SCP upload/download profiles with all SCP options
- **Tunnel support** — save SSH tunnel profiles with LocalForward, RemoteForward, DynamicForward
- **RemoteCommand support** — run arbitrary remote commands (e.g. `reboot`, `systemctl restart nginx`)
- **Fuzzy search** — accent-insensitive search with Damerau-Levenshtein distance
- **Custom shells** — use cmd.exe, PowerShell, WSL, Git Bash, Windows Terminal, or any terminal
- **Multi-language support** — English, Slovak, French, German, Russian, Polish, and Spanish
- **Atomic saves** — profile data is written atomically to prevent corruption

---

## Usage Examples

### Browse and connect to saved profiles

```
ssh profiles           → profile management view: action rows + saved profiles
ssh profiles prod      → filter saved profiles containing "prod"
```

Press Enter on a profile row to launch the connection.

> **Display order** — `ssh profiles` always shows results in a fixed, stable order regardless of fuzzy-match scoring:
> 1. **Správa profilov / Profile management** (usage hint, always pinned at the top)
> 2. **Action rows** as one continuous block: Add profile → Remove profile → Rename profile → Copy SSH command → Export profiles → Import profiles
> 3. **Saved profiles** (filtered / sorted by relevance when a search term is given)

### Add a profile

Profiles are added using standard SSH or SCP command syntax.
The plugin parses the command into a structured profile automatically.

**SSH — basic login:**
```
ssh profiles add myserver ssh root@10.0.0.150
ssh profiles add dev-box ssh -p 2222 dev@10.0.0.50
```

**SSH — with identity file:**
```
ssh profiles add production ssh -i "C:\Users\me\.ssh\id_rsa" -o IdentitiesOnly=yes admin@prod.example.com
```

**SSH — run a remote command:**
```
ssh profiles add reboot-proxmox ssh -t -t root@10.0.0.150 reboot
```

**SSH — local port forward (tunnel):**
```
ssh profiles add pangolin-tunnel ssh -L 8443:127.0.0.1:443 -L 8080:127.0.0.1:80 root@10.100.100.242
```

**SSH — SOCKS proxy:**
```
ssh profiles add socks-proxy ssh -D 1080 root@jump.example.com
```

**SSH — ProxyJump:**
```
ssh profiles add internal-host ssh -J bastion.example.com root@10.0.0.10
```

**SCP — upload a file:**
```
ssh profiles add upload-index scp -i "~/.ssh/key" "C:\web\index.html" root@10.0.0.1:/var/www/html/index.html
```

You can also omit the `ssh ` prefix — the plugin adds it automatically:
```
ssh profiles add bastion admin@bastion.example.com -p 22222
```

### Remove a profile

```
ssh profiles remove             → list all profiles for removal
ssh profiles remove prod        → filter profiles by "prod", then click to delete
```

### Rename a profile

```
ssh profiles rename                      → list all profiles to select for renaming
ssh profiles rename myserver             → pick "myserver" as the source
ssh profiles rename myserver new-name    → rename "myserver" to "new-name"
```

### Copy an SSH command to the clipboard

```
ssh profiles copy               → list all profiles for copying
ssh profiles copy myserver      → filter by "myserver", then click to copy
```

### Quick one-time connection (without saving)

Type a destination or SSH options directly — the plugin detects these automatically:

```
ssh root@10.0.0.1
ssh -p 2222 deploy@staging.example.com
ssh -i "C:\Users\me\.ssh\private_key" -o IdentitiesOnly=yes root@10.100.100.110
ssh 10.100.100.110
```

**Implicit detection rules** — input is treated as a direct connect when it:
- contains `@` (e.g. `user@host`, `root@10.0.0.1`)
- starts with `-` (e.g. `-p 22 user@host`, `-i key user@host`)
- is a bare hostname or IP with at least one dot (e.g. `10.0.0.1`, `myserver.example.com`)

### Import hosts from `~/.ssh/config`

```
ssh config             → import all Host entries from ~/.ssh/config
```

Only new hosts are imported — existing profiles are not overwritten.

The parser captures: `HostName`, `User`, `Port`, `IdentityFile`, `IdentitiesOnly`,
`LocalForward`, `RemoteForward`, `DynamicForward`, `ProxyJump`, `ProxyCommand`.

Example `~/.ssh/config` that is fully supported:

```
Host proxmox
    HostName 10.0.0.150
    User root
    Port 22
    IdentityFile ~/.ssh/id_ed25519

Host production
    HostName prod.example.com
    User deploy
    Port 2222
    IdentityFile ~/.ssh/id_ed25519
    IdentitiesOnly yes

Host bastion
    HostName bastion.corp.internal
    User ec2-user
    IdentityFile "C:\Users\me\.ssh\corp_key"

Host internal
    HostName 10.0.0.10
    ProxyJump bastion
```

Wildcard entries (`Host *`) are skipped automatically.

### Export and import profiles

**Export** — saves all current profiles to a human-readable `.sshconfig` file:

```
ssh profiles export
```

The file is written to:
```
%APPDATA%\FlowLauncher\Plugins\QuickSSH\data\profiles_export.sshconfig
```

**Import** — loads profiles from any `.sshconfig` file (or legacy `.json` file) placed in the `data\` folder:

```
ssh profiles import                    → list all importable files
ssh profiles import mybackup           → filter files containing "mybackup"
```

Only profiles that do not already exist are added (no overwriting).

### Saved profile format (`.sshconfig`)

Profiles are exported in a human-readable SSH-config-like format.
This format is intentionally similar to OpenSSH `ssh_config(5)` but is **not** a strict clone —
it adds QuickSSH-specific fields like `Type`, `RemoteCommand`, `RequestTTY`, `Source`, `Target`, etc.

**SSH profile — normal login:**
```
Host Proxmox-Host
    Type ssh
    HostName 10.0.0.150
    User root
    Port 22
    IdentityFile ~/.ssh/private_key
    IdentitiesOnly yes
```

**SSH profile — remote command with TTY:**
```
Host RustDesk-REBOOT
    Type ssh
    HostName 10.100.100.110
    User root
    Port 22
    IdentityFile "C:\Users\info\.ssh\private_key"
    IdentitiesOnly yes
    RemoteCommand reboot
    RequestTTY force
```

**SSH profile — local port forwards (tunnel):**
```
Host Pangolin-Tunnel
    Type ssh
    HostName 10.100.100.242
    User root
    Port 22
    IdentityFile ~/.ssh/private_key
    IdentitiesOnly yes
    LocalForward 8443 127.0.0.1:443
    LocalForward 8080 127.0.0.1:80
```

**SCP profile — file upload:**
```
Host Homepage-Upload
    Type scp
    HostName 10.100.100.241
    User root
    Port 22
    IdentityFile "C:\Users\info\.ssh\private_key"
    IdentitiesOnly yes
    Source "C:\web\index.html"
    Target "/var/www/html/index.html"
```

### Supported profile fields

**Common fields (SSH and SCP):**

| Field | Description |
|-------|-------------|
| `Type` | `ssh` (default) or `scp` |
| `HostName` | Hostname or IP address |
| `User` | Remote user name |
| `Port` | Port number (omitted from command when 22) |
| `IdentityFile` | Path to private key file |
| `IdentitiesOnly` | `yes` adds `-o IdentitiesOnly=yes` |
| `ExtraArgs` | Raw extra arguments (fallback for unparsed flags) |

**SSH-specific fields:**

| Field | Description |
|-------|-------------|
| `RemoteCommand` | Command to execute on the remote host |
| `RequestTTY` | TTY allocation: `force` (-t -t), `yes` (-t), `no` (-T) |
| `LocalForward` | Local port forward spec, e.g. `8443 127.0.0.1:443` (repeatable) |
| `RemoteForward` | Remote port forward spec (repeatable) |
| `DynamicForward` | SOCKS5 proxy port, e.g. `1080` |
| `ProxyJump` | Jump host(s) for `-J` |
| `ProxyCommand` | Proxy command string |

**SCP-specific fields:**

| Field | Description |
|-------|-------------|
| `Source` | **Bare source path** — local path for upload, remote path for download |
| `Target` | **Bare target path** — remote path for upload, local path for download |
| `Recursive` | `yes` adds `-r` |
| `PreserveTimes` | `yes` adds `-p` |
| `Compression` | `yes` adds `-C` |

**SCP normalization rule:**  
`Source` and `Target` always store **bare paths** — no `user@host:` prefix.
`HostName` and `User` are always in the common structured fields.  
The command builder determines transfer direction by inspecting the paths:

- **Upload** — `Source` is a Windows local path (e.g. `C:\...`): builds `scp source user@host:target`
- **Download** — `Target` is a Windows local path: builds `scp user@host:source target`
- **Ambiguous** (both are Unix-style paths): upload is assumed, Source is treated as local

This means on-disk profiles are always portable and can be re-parsed without data loss.
Legacy SCP commands with `user@host:path` positionals are automatically normalised on import.

### Custom shell management

```
ssh shell                                             → shell management view: action rows + saved shells
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

---

## Data Storage

| File | Purpose |
|------|---------|
| `~/.ssh/profiles.json` | Main profile and shell database (v2 structured JSON) |
| `%APPDATA%\FlowLauncher\Plugins\QuickSSH\data\*.sshconfig` | Human-readable export/import files |
| `%APPDATA%\FlowLauncher\Plugins\QuickSSH\data\*.json` | Legacy import files (v1, still readable) |

### profiles.json schema (v2)

```json
{
  "PluginVersion": "2.0",
  "ProfilesLists": {
    "<profile-name>": {
      "Type": "ssh",
      "HostName": "10.0.0.150",
      "User": "root",
      "Port": "22",
      "IdentityFile": "~/.ssh/private_key",
      "IdentitiesOnly": true,
      "RemoteCommand": "reboot",
      "RequestTTY": "force"
    }
  },
  "CustomShellLists": {
    "<shell-name>": "<executable path + optional args>"
  },
  "SelectedCustomShell": "<shell-name or null>"
}
```

### Migration from v1

**v1 profiles.json** stored profiles as raw SSH command strings:
```json
{
  "PluginVersion": "1.0",
  "EntriesLists": {
    "myserver": "ssh user@192.168.1.100",
    "production": "ssh -i \"C:\\...\\id_rsa\" admin@prod -p 2222"
  }
}
```

**On first load**, QuickSSH automatically:
1. Parses each raw command string into a structured `SshProfile`
2. Stores them in the new `ProfilesLists` format
3. Clears the legacy `EntriesLists` field from memory
4. **Immediately persists the v2 format** so the disk file is canonical after the first run

Migration handles:
- Simple `ssh user@host` → structured with `User`, `HostName`
- `ssh -p 22 user@host` → structured with `Port`, `User`, `HostName`
- `ssh -i key -o IdentitiesOnly=yes user@host` → structured with all fields
- Remote commands after the destination → `RemoteCommand` field
- Unknown/unsupported flags (e.g. `-X`, `-A`) → stored verbatim in `ExtraArgs`
- SCP upload `scp C:\file.txt user@host:/path` → `Source` = local bare path, `Target` = remote bare path, `User`/`HostName` extracted safely (Windows drive paths never misidentified as remote specs)
- SCP download `scp user@host:/remote/file C:\local\file` → `Source` = remote bare path, `Target` = local bare path

**Unparseable flag fallback (ExtraArgs):** SSH has many options. Flags that this plugin does not map to a named structured field are preserved verbatim in the `ExtraArgs` field and appended to the generated command. This means:
- **No data is silently lost** during migration.
- The `ExtraArgs` field is round-trip stable: it is included when exporting to `.sshconfig` and is read back unchanged on import.
- The generated SSH command still includes the flag, so the connection behaviour is preserved.

> **Note:** "profiles import" still accepts legacy `.json` files for backward-compatible migration.
> JSON is **never written** by this plugin — `.sshconfig` is the canonical export format.
> Legacy `.json` files are clearly labelled "(legacy)" in the import UI.

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

1. **Create a GitHub Release** — open a Pull Request in this repository, update `plugin.json` with the desired version, add one label (`release:patch`, `release:minor`, or `release:major`), and merge it into `main`. GitHub Actions will automatically build `QuickSSH.zip`, create a tag, and publish the release.

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
     "Description": "Enhanced SSH/SCP connection plugin with query autocomplete, structured profiles, SSH config support, and custom shell handling",
     "Author": "Vaso73",
     "Version": "3.0.5",
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

### Versioning

`plugin.json` is the **single source of truth** for the plugin version. There is no separate version in the project file or any other location. GitHub tags and release names always match the version committed in `plugin.json`.

### Releasing a new version

1. Open a Pull Request with your changes.
2. **Update `plugin.json`** — set `Version` to the desired new version (e.g. `"3.0.1"` for a patch, `"3.1.0"` for a minor, `"4.0.0"` for a major). This must be done in the PR itself before merge — the workflow reads whatever version is already in `plugin.json` on `main` after the merge.
3. Add **exactly one** release label: `release:patch`, `release:minor`, or `release:major` (or `skip-release` to skip the release entirely).
4. Merge the Pull Request into `main`.
5. GitHub Actions automatically:
   - Validates that exactly one release label is present
   - Reads the current version from `plugin.json` on `main`
   - Builds `QuickSSH.zip`
   - Creates a matching git tag and GitHub Release
6. Update the Plugin Manifest entry in `Flow-Launcher/Flow.Launcher.PluginsManifest` if needed.

`plugin.json` is the **single source of truth** for the plugin version. The version must already be correct in the PR — the workflow does **not** modify `main` after merge. This is required because `main` is a protected branch and post-merge pushes from CI would be rejected.

> **Note:** `skip-release` prevents the entire release job from running. It is different from a missing label: a PR without any label at all that is merged to `main` will cause the release job to **fail** with a clear error. Always include either a release label or `skip-release`.

## License

[MIT](LICENSE)
