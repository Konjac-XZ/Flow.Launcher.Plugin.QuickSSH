<#
.SYNOPSIS
    Bumps the plugin version in plugin.json and writes it back.

.DESCRIPTION
    Reads the current Version from plugin.json, applies the requested
    bump (patch / minor / major), writes the result back to plugin.json,
    and outputs the new version string on stdout.

    plugin.json is the single source of truth for the plugin version.

.PARAMETER Bump
    The bump type: patch, minor, or major.

.EXAMPLE
    ./scripts/bump-version.ps1 -Bump patch
    Reads "3.0.0" from plugin.json, writes "3.0.1", outputs "3.0.1".
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump
)

$ErrorActionPreference = "Stop"

$manifestPath = Join-Path $PSScriptRoot "..\plugin.json"
$manifestPath = [System.IO.Path]::GetFullPath($manifestPath)

if (-not (Test-Path $manifestPath)) {
    Write-Error "plugin.json not found at: $manifestPath"
    exit 1
}

$content = [System.IO.File]::ReadAllText($manifestPath)

if ($content -notmatch '"Version"\s*:\s*"(\d+\.\d+\.\d+)"') {
    Write-Error "Cannot find a valid semver Version field in plugin.json"
    exit 1
}

$currentVersion = $Matches[1]

if ($currentVersion -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    Write-Error "Cannot parse version '$currentVersion' from plugin.json"
    exit 1
}

$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$patch = [int]$Matches[3]

switch ($Bump) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}

$newVersion = "$major.$minor.$patch"

$newContent = $content -replace '"Version"\s*:\s*"[^"]*"', ('"Version": "' + $newVersion + '"')

if ($newContent -eq $content) {
    Write-Error "Version replacement had no effect — check plugin.json formatting"
    exit 1
}

[System.IO.File]::WriteAllText($manifestPath, $newContent, [System.Text.Encoding]::UTF8)

Write-Host "Bumped $currentVersion -> $newVersion ($Bump)"
Write-Output $newVersion
