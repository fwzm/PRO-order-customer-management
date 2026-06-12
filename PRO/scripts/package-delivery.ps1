[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "src\PRO.Desktop\PRO.Desktop.csproj"
$publishDir = Join-Path $root "publish"
$artifactsDir = Join-Path $root "artifacts"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageDir = Join-Path $artifactsDir "PRO-delivery-$stamp"
$appDir = Join-Path $packageDir "app"
$docsDir = Join-Path $packageDir "docs"
$zipPath = Join-Path $artifactsDir "PRO-delivery-$stamp.zip"

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
if (Test-Path $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $appDir, $docsDir | Out-Null

if (-not $SkipPublish) {
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    dotnet publish $project -c $Configuration -r $Runtime --self-contained true -o $publishDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path (Join-Path $publishDir "PRO.exe"))) {
    throw "Published PRO.exe was not found in $publishDir"
}

Copy-Item -Path (Join-Path $publishDir "*") -Destination $appDir -Recurse -Force
Get-ChildItem -Path $appDir -Filter "*.pdb" -Recurse | Remove-Item -Force

$docCandidates = @(
    (Join-Path $root "README.md"),
    (Join-Path $root "..\PRO订单与客户管理系统 - 功能需求.docx"),
    (Join-Path $root "..\PRO订单与客户管理系统-功能需求_修订版.docx"),
    (Join-Path $root "..\注意事项.txt")
)

foreach ($doc in $docCandidates) {
    if (Test-Path $doc) {
        Copy-Item -LiteralPath $doc -Destination $docsDir -Force
    }
}

$readmePath = Join-Path $packageDir "启动说明.txt"
@"
PRO 订单与客户管理系统

1. 主程序: app\PRO.exe
2. 数据库连接: 默认读取 app\appsettings.json，也可用环境变量 PRO_ConnectionStrings__PostgreSQL 覆盖。
3. 企业微信: 未配置 CorpId/CorpSecret 前保持 WeChat:Enabled=false。
4. 真实凭据不要写入 GitHub；本地可使用 appsettings.local.json 或环境变量保存。
"@ | Set-Content -LiteralPath $readmePath -Encoding UTF8

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

Write-Host "Package created: $zipPath"
