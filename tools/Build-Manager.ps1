param(
    [string]$ValheimPath = $env:VALHEIM_INSTALL_PATH,
    [string]$BepInExPackage
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
$release = Join-Path $repo 'release'

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

dotnet build $client -c Release "-p:VALHEIM_INSTALL_PATH=$ValheimPath" -p:InstallToGame=false -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
& (Join-Path $PSScriptRoot 'Build-ServerPackage.ps1') -ValheimPath $ValheimPath

$clientZip = Join-Path $release "ValheimVRM-$clientVersion.zip"
$serverZip = Join-Path $release "ValheimVRM-Server-$serverVersion.zip"
dotnet build $manager -c Release "-p:ManagerClientZip=$clientZip" "-p:ManagerServerZip=$serverZip" "-p:ManagerLoaderZip=$BepInExPackage" -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Manager build failed.' }

$exe = Join-Path $release "ValheimVRM-Manager-$managerVersion-win-x64.exe"
Copy-Item -LiteralPath (Join-Path $repo 'manager/bin/Release/net471/ValheimVRM.Manager.exe') -Destination $exe
Get-Item -LiteralPath $exe | Select-Object Name, Length
