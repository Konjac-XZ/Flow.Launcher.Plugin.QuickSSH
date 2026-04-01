$ErrorActionPreference = "Stop"

if ($env:GITHUB_EVENT_NAME -ne "pull_request") {
    Write-Host "Docs check skipped: not a pull_request event."
    exit 0
}

$event = Get-Content $env:GITHUB_EVENT_PATH | ConvertFrom-Json
$baseSha = $event.pull_request.base.sha
$headSha = $event.pull_request.head.sha

if ([string]::IsNullOrWhiteSpace($baseSha) -or [string]::IsNullOrWhiteSpace($headSha)) {
    Write-Error "Could not determine base/head SHA from pull request event."
    exit 1
}

git fetch --no-tags origin $baseSha $headSha | Out-Null

$changedFiles = @(git diff --name-only $baseSha $headSha)

if ($changedFiles.Count -eq 0) {
    Write-Host "No changed files detected."
    exit 0
}

Write-Host "Changed files:"
$changedFiles | ForEach-Object { Write-Host " - $_" }

$readmeChanged = $changedFiles -contains "README.md"

$userFacingPatterns = @(
    '^Main\.cs$',
    '^AutoCompleter\.cs$',
    '^SshConfigParser\.cs$',
    '^SshCommandBuilder\.cs$',
    '^Profile\.cs$',
    '^Languages\/',
    '^plugin\.json$'
)

$userFacingChangedFiles = @()

foreach ($file in $changedFiles) {
    foreach ($pattern in $userFacingPatterns) {
        if ($file -match $pattern) {
            $userFacingChangedFiles += $file
            break
        }
    }
}

$userFacingChangedFiles = $userFacingChangedFiles | Sort-Object -Unique

if ($userFacingChangedFiles.Count -eq 0) {
    Write-Host "Docs check passed: no tracked user-facing files changed."
    exit 0
}

Write-Host "Tracked user-facing files changed:"
$userFacingChangedFiles | ForEach-Object { Write-Host " - $_" }

if (-not $readmeChanged) {
    Write-Error "README.md was not updated even though tracked user-facing files changed."
    exit 1
}

Write-Host "Docs check passed: README.md was updated."
exit 0
