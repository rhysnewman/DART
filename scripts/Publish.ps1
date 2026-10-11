param([Parameter(Mandatory)][string]$MediaMtxExe,
      [Parameter(Mandatory)][string]$FFmpegExe)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\Dart\Dart.csproj'
$output = Join-Path $PSScriptRoot '..\dist'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
New-Item -ItemType Directory -Force (Join-Path $output 'tools') | Out-Null
Copy-Item -LiteralPath $MediaMtxExe -Destination (Join-Path $output 'tools\mediamtx.exe')
Copy-Item -LiteralPath $FFmpegExe -Destination (Join-Path $output 'tools\ffmpeg.exe')
Write-Host "Bundle created at $output. Include third-party licences and matching source notices before distribution."
