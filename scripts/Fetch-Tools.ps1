param(
 [Parameter(Mandatory)][uri]$MediaMtxZipUrl,
 [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$MediaMtxSha256,
 [Parameter(Mandatory)][uri]$FFmpegZipUrl,
 [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$FFmpegSha256
)
$ErrorActionPreference='Stop'
$tools=Join-Path $PSScriptRoot '..\vendor'
New-Item -ItemType Directory -Force $tools | Out-Null
foreach($item in @(
 @{Name='mediamtx'; Url=$MediaMtxZipUrl; Hash=$MediaMtxSha256; Exe='mediamtx.exe'},
 @{Name='ffmpeg'; Url=$FFmpegZipUrl; Hash=$FFmpegSha256; Exe='ffmpeg.exe'}
)) {
 if($item.Url.Scheme -ne 'https') { throw 'Dependencies must use HTTPS' }
 $archive=Join-Path $tools ($item.Name+'.zip')
 Invoke-WebRequest -Uri $item.Url -OutFile $archive
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $item.Hash) { throw ('Hash mismatch: '+$item.Name) }
 $unpacked=Join-Path $tools ($item.Name+'-'+$item.Hash.Substring(0,12))
 Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
 $found=@(Get-ChildItem -LiteralPath $unpacked -Recurse -File -Filter $item.Exe)
 if($found.Count -ne 1) { throw ('Expected exactly one '+$item.Exe) }
 Write-Host ($item.Name+' executable: '+$found[0].FullName)
}
