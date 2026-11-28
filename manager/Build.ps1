param(
    [string]$ValheimPath = $env:VALHEIM_INSTALL_PATH,
    [string]$BepInExPackage,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (!$ValheimPath) { throw 'Pass -ValheimPath for a Valheim client installation with BepInEx 5 build references.' }
$ValheimPath = (Resolve-Path -LiteralPath $ValheimPath).Path
$repo = Split-Path $PSScriptRoot -Parent
$client = Join-Path $repo 'ValheimVRM.csproj'
$server = Join-Path $repo 'server/ValheimVRM.Server.csproj'
$manager = Join-Path $repo 'manager/ValheimVRM.Manager.csproj'
$clientVersion = ([xml](Get-Content -LiteralPath $client -Raw)).Project.PropertyGroup[0].Version
$serverVersion = ([xml](Get-Content -LiteralPath $server -Raw)).Project.PropertyGroup.Version
$managerVersion = ([xml](Get-Content -LiteralPath $manager -Raw)).Project.PropertyGroup.Version
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo 'release' }

$loaderName = 'BepInEx_win_x64_5.4.23.3.zip'
$loaderHash = '41A089E5B1B1F0713B331346BAF6677B1184C69EABEBF51101097954E854C749'
if (!$BepInExPackage) {
    $cache = Join-Path $repo 'artifacts/manager'
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $BepInExPackage = Join-Path $cache $loaderName
    if (!(Test-Path -LiteralPath $BepInExPackage -PathType Leaf)) {
        $download = Join-Path $cache ("loader-" + [Guid]::NewGuid().ToString('N') + '.zip')
        try {
            Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/$loaderName" -OutFile $download
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $loaderHash) {
                throw 'Downloaded BepInEx package has the wrong SHA-256.'
            }
            Move-Item -LiteralPath $download -Destination $BepInExPackage
        } finally {
            if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download }
        }
    }
}
$BepInExPackage = (Resolve-Path -LiteralPath $BepInExPackage).Path
if ((Get-FileHash -LiteralPath $BepInExPackage -Algorithm SHA256).Hash -ne $loaderHash) {
    throw 'Use the original BepInEx 5.4.23.3 Windows x64 release ZIP.'
}

# The client MSBuild target clears its release directories. Give it a fresh
# staging directory so a build cannot clear an existing set of release files.
$intermediate = New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'obj') -Force
if ($intermediate.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The manager build directory must be inside its checkout.'
}
$stage = (New-Item -ItemType Directory -Path (Join-Path $intermediate.FullName ('package-' + [Guid]::NewGuid().ToString('N')))).FullName
$packages = Join-Path $stage 'packages'
$layout = Join-Path $stage 'layout'
try {
    dotnet build $client -c Release "-p:VALHEIM_INSTALL_PATH=$ValheimPath" -p:InstallToGame=false "-p:FINAL_RELEASE=$packages" "-p:RELEASE_STAGING=$layout" -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
    & (Join-Path $repo 'tools/Build-ServerPackage.ps1') -ValheimPath $ValheimPath -OutputDirectory $packages

    $clientZip = Join-Path $packages "ValheimVRM-$clientVersion.zip"
    $serverZip = Join-Path $packages "ValheimVRM-Server-$serverVersion.zip"
    $binaryDirectory = Join-Path $stage 'manager'
    dotnet build $manager -c Release "-p:ManagerClientZip=$clientZip" "-p:ManagerServerZip=$serverZip" "-p:ManagerLoaderZip=$BepInExPackage" -o $binaryDirectory -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Manager build failed.' }

    $output = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
    $exe = Join-Path $output "ValheimVRM-Manager-$managerVersion-win-x64.exe"
    Copy-Item -LiteralPath $clientZip, $serverZip -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $binaryDirectory 'ValheimVRM.Manager.exe') -Destination $exe -Force
    Get-Item -LiteralPath $exe | Select-Object Name, Length
} finally {
    $stagePath = (Resolve-Path -LiteralPath $stage).ProviderPath
    if ((Split-Path $stagePath -Parent) -eq $intermediate.FullName -and
        !((Get-Item -LiteralPath $stagePath).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        Remove-Item -LiteralPath $stagePath -Recurse -Force -ErrorAction Continue
    }
}
