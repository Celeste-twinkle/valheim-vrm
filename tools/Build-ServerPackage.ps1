param([string]$ValheimPath = $env:VALHEIM_INSTALL_PATH)
$ErrorActionPreference = 'Stop'
if (!$ValheimPath) { throw 'Pass -ValheimPath for a Valheim client installation with BepInEx 5 build references.' }
$repo = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repo 'server/ValheimVRM.Server.csproj'
dotnet build $project -c Release "-p:VALHEIM_INSTALL_PATH=$ValheimPath" -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Server build failed.' }
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$destination = Join-Path $repo "release/ValheimVRM-Server-$version.zip"
New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Package an explicit allowlist, never the build directory's copied game references.
$files = [ordered]@{
    'BepInEx/plugins/ValheimVRM.Server/ValheimVRM.Server.dll' = 'server/bin/Release/net471/ValheimVRM.Server.dll'
    'ValheimVRM.Server/README.md' = 'docs/SERVER-SYNC.md'
    'ValheimVRM.Server/LICENSE' = 'LICENSE'
    "ValheimVRM.Server/release-$version-validation.md" = "docs/release-$version-validation.md"
}
$stream = [IO.File]::Open($destination, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($item in $files.GetEnumerator()) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $repo $item.Value), $item.Key, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Get-Item -LiteralPath $destination | Select-Object Name, Length
