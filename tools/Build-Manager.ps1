param(
    [string]$ValheimPath = $env:VALHEIM_INSTALL_PATH,
    [string]$BepInExPackage,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $repo 'manager/Build.ps1') -ValheimPath $ValheimPath -BepInExPackage $BepInExPackage -OutputDirectory $OutputDirectory
