param([Parameter(Mandatory=$true)][string]$InstallDirectory,[switch]$AllowInternet,[switch]$Remove)
$ErrorActionPreference='Stop'
$names=@('DART - ONVIF TCP','DART - RTSP TCP','DART - Discovery UDP','DART - Agent','DART - MediaMTX','DART - FFmpeg')
# Enumerate once and filter locally: CIM property-filter queries report an error
# when no matching rules exist, which is normal on a clean installation.
$existingRules=@(Get-NetFirewallRule -PolicyStore PersistentStore -ErrorAction Stop)
$existingRules | Where-Object { $_.DisplayName -in $names } | Remove-NetFirewallRule
$blockRules=@($existingRules | Where-Object { $_.Action -eq 'Block' -and $_.DisplayName -notin $names })
if($Remove) { exit 0 }
# Resolve filters once, before deleting any blocks; deleted CIM rules cannot be queried again.
$blockPrograms=@($blockRules | ForEach-Object {
 $rule=$_
 $filter=$rule | Get-NetFirewallApplicationFilter
 [pscustomobject]@{Rule=$rule;Program=$filter.Program}
})
$scope=if($AllowInternet){'Any'}else{'LocalSubnet'}
$programs=@{Agent='Dart.exe';MediaMTX='tools\mediamtx.exe';FFmpeg='tools\ffmpeg.exe'}
foreach($entry in $programs.GetEnumerator()) {
 $program=[IO.Path]::GetFullPath((Join-Path $InstallDirectory $entry.Value))
 if(!(Test-Path -LiteralPath $program -PathType Leaf)) { throw "Missing installed executable: $program" }
 # Remove local prompt-generated deny rules for this exact installed executable.
 $blockPrograms | Where-Object { $_.Program -ieq $program } | ForEach-Object { $_.Rule | Remove-NetFirewallRule }
 New-NetFirewallRule -DisplayName "DART - $($entry.Key)" -Group DART -Direction Inbound -Action Allow -Program $program -Protocol Any -Profile Any -RemoteAddress $scope | Out-Null
}
@{AllowLan=$true;AllowInternet=[bool]$AllowInternet} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $InstallDirectory 'installation.json') -Encoding UTF8
