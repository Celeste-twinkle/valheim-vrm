param(
    [string]$ValheimPath = $env:VALHEIM_INSTALL_PATH,
    [string]$BepInExPackage
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$changes = git -C $repo status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'Could not read the source checkout.' }
if ($changes) { throw 'Commit your source before packaging so the source ZIP matches the build.' }
$revision = git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Could not identify the source commit.' }

$project = Join-Path $repo 'ValheimVRM.csproj'
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup[0].Version

$runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
$folder = "ValheimVRM-$version-$($revision.Substring(0, 8))-$runId"
$release = (New-Item -ItemType Directory -Path (Join-Path $repo "artifacts/releases/$folder")).FullName
$log = Join-Path $release 'build.log'
"Source commit $revision" | Tee-Object -FilePath $log
"Release version $version" | Tee-Object -FilePath $log -Append

try {
    & (Join-Path $PSScriptRoot 'Build.ps1') -ValheimPath $ValheimPath -BepInExPackage $BepInExPackage -OutputDirectory $release *>&1 |
        Tee-Object -FilePath $log -Append

    $clientZip = Join-Path $release "ValheimVRM-$version.zip"
    $serverZip = (Get-ChildItem -LiteralPath $release -File -Filter 'ValheimVRM-Server-*.zip').FullName
    $exe = (Get-ChildItem -LiteralPath $release -File -Filter 'ValheimVRM-Manager-*-win-x64.exe').FullName
    $manager = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
    $payloads = @{
        'Payload.Client.zip' = $clientZip
        'Payload.Server.zip' = $serverZip
    }
    foreach ($payload in $payloads.GetEnumerator()) {
        $stream = $manager.GetManifestResourceStream($payload.Key)
        if (!$stream) { throw "The manager is missing $($payload.Key)." }
        try {
            if ((Get-FileHash -InputStream $stream).Hash -ne (Get-FileHash -LiteralPath $payload.Value).Hash) {
                throw "The manager payload differs from $($payload.Value)."
            }
        } finally { $stream.Dispose() }
    }
    if (!$manager.GetManifestResourceInfo('Payload.Loader.zip')) {
        throw 'The manager is missing the BepInEx loader.'
    }

    $sourceZip = Join-Path $release "ValheimVRM-$version-source.zip"
    git -C $repo archive --format=zip "--prefix=ValheimVRM-$version/" "--output=$sourceZip" $revision
    if ($LASTEXITCODE -ne 0) { throw 'The source archive could not be created.' }

    # These archives accompany the same FreeImage and Doorstop binaries bundled by the manager.
    $provenanceTag = 'v2.0.7-fork.1'
    $sourceBase = "https://github.com/Celeste-twinkle/valheim-vrm/releases/download/$provenanceTag"
    $sourceHashes = [ordered]@{
        'FreeImage-ydm-0.1.6-source.zip' = '099165CF00C2E3787AE68147A61CDA01891F59C176939328C5BE89B81A0A5A8D'
        'UnityDoorstop-4.4.0-source.zip' = 'F982A2DC2C7FA50F35013051B82268B0BC9FCD73786F5F0B4C664A0E5381B1E9'
    }
    foreach ($source in $sourceHashes.GetEnumerator()) {
        $destination = Join-Path $release $source.Key
        Invoke-WebRequest -UseBasicParsing -Uri "$sourceBase/$($source.Key)" -OutFile $destination
        if ((Get-FileHash -LiteralPath $destination).Hash -ne $source.Value) {
            throw "Source archive SHA-256 mismatch for $($source.Key)."
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $assets = Get-ChildItem -LiteralPath $release -File |
        Where-Object { $_.Extension -in '.zip', '.exe' } | Sort-Object Name
    foreach ($asset in $assets) {
        if ($asset.Extension -ne '.zip') { continue }
        $zip = [IO.Compression.ZipFile]::OpenRead($asset.FullName)
        try {
            if ($zip.Entries.Count -eq 0) { throw "The ZIP $($asset.Name) is empty." }
            foreach ($entry in $zip.Entries) {
                if (!$entry.Name) { continue }
                $entryStream = $entry.Open()
                try { $entryStream.CopyTo([IO.Stream]::Null) } finally { $entryStream.Dispose() }
            }
        } finally { $zip.Dispose() }
    }

    $currentRevision = git -C $repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not verify the source commit after building.' }
    $changes = git -C $repo status --porcelain
    if ($LASTEXITCODE -ne 0 -or $changes -or $currentRevision -ne $revision) {
        throw 'The source checkout changed during packaging. Run the release command from a clean, stable checkout.'
    }

    $checksums = foreach ($asset in $assets) {
        $hash = (Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($asset.Name)"
    }
    Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Value $checksums -Encoding ascii
    "Release bundle verified: $release" | Tee-Object -FilePath $log -Append
} catch {
    "Release preparation failed: $($_.Exception.Message)" | Tee-Object -FilePath $log -Append
    throw
}
