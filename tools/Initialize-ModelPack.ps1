param([string]$GameRoot = (Join-Path $PSScriptRoot '../../..'))
$ErrorActionPreference = 'Stop'
try {
    $packGameRoot = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\')
    if (!(Test-Path -LiteralPath (Join-Path $packGameRoot 'valheim.exe') -PathType Leaf)) {
        throw '请先把客户端插件包解压到 valheim.exe 所在目录。'
    }
    $packManifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($packEntry in $packManifest.requiredFiles) {
        $packTarget = [IO.Path]::GetFullPath((Join-Path $packGameRoot $packEntry.path))
        if (!$packTarget.StartsWith($packGameRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '清单中存在无效路径。' }
        if (!(Test-Path -LiteralPath $packTarget -PathType Leaf)) { throw ('插件缺少文件，请重新解压完整插件包：' + $packEntry.path) }
        if ((Get-FileHash -LiteralPath $packTarget -Algorithm SHA256).Hash -ne $packEntry.sha256) { throw ('插件文件校验不一致：' + $packEntry.path) }
    }
    Write-Host '插件文件校验通过。无需初始化模型：把任意 .vrm 放入游戏根目录的 ValheimVRM，进入世界后按 F8 选择。' -ForegroundColor Green
    Write-Host 'settings_*.txt、默认模型、固定的九个模型和本脚本都不是运行必需项。个人选项自动保存到 BepInEx/config/ValheimVRM。'
    exit 0
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
