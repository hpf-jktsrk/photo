$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"

$project = Join-Path $root "desktop\PngCompressor.Desktop.csproj"
$output = Join-Path $root "dist\windows"

dotnet publish $project `
  --configuration Release `
  -p:RuntimeIdentifier=win-x64 `
  -p:PublishSingleFile=true `
  -p:SelfContained=false `
  -p:PublishSelfContained=false `
  -p:UseAppHost=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  --output $output

Write-Host "Windows exe generated at: $output\图片压缩.exe"
