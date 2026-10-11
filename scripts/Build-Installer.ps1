param([string]$Compiler = (Join-Path $PSScriptRoot '..\vendor\nsis\nsis-3.12\Bin\makensis.exe'))
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnetExe=Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$payload=Join-Path $projectRoot 'installer\payload'
& $dotnetExe publish (Join-Path $projectRoot 'src\Dart\Dart.csproj') -c Release -r win-x64 --self-contained true -o $payload
if($LASTEXITCODE -ne 0) { throw 'DART publish failed' }
New-Item -ItemType Directory -Force (Join-Path $payload 'tools'),(Join-Path $payload 'licenses\ffmpeg'),(Join-Path $payload 'licenses\mediamtx') | Out-Null
Copy-Item (Join-Path $projectRoot 'vendor\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe') (Join-Path $payload 'tools\ffmpeg.exe')
Copy-Item (Join-Path $projectRoot 'vendor\mediamtx\mediamtx.exe') (Join-Path $payload 'tools\mediamtx.exe')
Copy-Item (Join-Path $projectRoot 'vendor\ffmpeg\ffmpeg-9.0.2-essentials_build\LICENSE') (Join-Path $payload 'licenses\ffmpeg\LICENSE')
Copy-Item (Join-Path $projectRoot 'vendor\ffmpeg\ffmpeg-9.0.2-essentials_build\README.txt') (Join-Path $payload 'licenses\ffmpeg\README.txt')
Copy-Item (Join-Path $projectRoot 'vendor\ffmpeg\DOWNLOAD-STATUS.md') (Join-Path $payload 'licenses\ffmpeg\PROVENANCE.md')
Copy-Item (Join-Path $projectRoot 'vendor\mediamtx\LICENSE') (Join-Path $payload 'licenses\mediamtx\LICENSE')
Copy-Item (Join-Path $projectRoot 'vendor\mediamtx\provenance.json') (Join-Path $payload 'licenses\mediamtx\provenance.json')
Copy-Item (Join-Path $projectRoot 'installer\THIRD-PARTY.txt') (Join-Path $payload 'THIRD-PARTY.txt')
New-Item -ItemType Directory -Force (Join-Path $payload 'licenses\dotnet') | Out-Null
Copy-Item (Join-Path $env:ProgramFiles 'dotnet\LICENSE.txt') (Join-Path $payload 'licenses\dotnet\LICENSE.txt')
Copy-Item (Join-Path $env:ProgramFiles 'dotnet\ThirdPartyNotices.txt') (Join-Path $payload 'licenses\dotnet\ThirdPartyNotices.txt')
Copy-Item (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.AspNetCore.App\8.0.31\THIRD-PARTY-NOTICES.txt') (Join-Path $payload 'licenses\dotnet\ASP-NET-THIRD-PARTY-NOTICES.txt')
Copy-Item (Join-Path $projectRoot 'installer\Configure-Firewall.ps1') $payload
Copy-Item (Join-Path $projectRoot 'installer\Launch-Dart.ps1') $payload
$deletes=[Collections.Generic.List[string]]::new()
Get-ChildItem -LiteralPath $payload -Recurse -File | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($payload,$_.FullName)
 $deletes.Add('Delete "$INSTDIR\'+$relative.Replace('$','$$')+'"')
}
Get-ChildItem -LiteralPath $payload -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
 $relative=[IO.Path]::GetRelativePath($payload,$_.FullName)
 $deletes.Add('RMDir "$INSTDIR\'+$relative.Replace('$','$$')+'"')
}
$deletes | Set-Content (Join-Path $projectRoot 'installer\uninstall-files.nsh')
& $Compiler (Join-Path $projectRoot 'installer\Dart.nsi')
if($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Get-FileHash (Join-Path $projectRoot 'installer\DART-Setup-0.3.2.exe') -Algorithm SHA256 | Format-List
