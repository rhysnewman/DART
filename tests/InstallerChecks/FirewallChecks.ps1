$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\test-output\firewall-checks'))
New-Item -ItemType Directory -Force (Join-Path $taskRoot 'tools') | Out-Null
foreach($file in @('Dart.exe','tools\mediamtx.exe','tools\ffmpeg.exe')) { Set-Content (Join-Path $taskRoot $file) '' }
$global:dartTestCreated=[Collections.Generic.List[object]]::new()
$global:dartTestRemoved=[Collections.Generic.List[string]]::new()
function Get-NetFirewallRule {
 param($PolicyStore,$DisplayName,$Action,$ErrorAction)
 if($DisplayName -or $Action) { throw 'Regression: filtered CIM queries fail when no matches exist' }
 if($global:dartTestQueryFailure) { throw 'Simulated firewall access failure' }
 if($global:dartTestEmptyStore) { return }
 if($global:dartTestHasBlocks) {
  [pscustomobject]@{Name='same-app';DisplayName='prior prompt';Action='Block';Program=(Join-Path $taskRoot 'Dart.exe')}
  [pscustomobject]@{Name='other-app';DisplayName='unrelated';Action='Block';Program='C:\Other.exe'}
 } else {
  [pscustomobject]@{Name='unrelated-allow';DisplayName='unrelated';Action='Allow';Program='C:\Other.exe'}
 }
}
function Get-NetFirewallApplicationFilter { process { if($_.Name -in $global:dartTestRemoved) { throw 'Cannot query a deleted firewall rule' }; $_ } }
function Remove-NetFirewallRule { process { $global:dartTestRemoved.Add($_.Name) } }
function New-NetFirewallRule { param($DisplayName,$Group,$Direction,$Action,$Program,$Protocol,$Profile,$RemoteAddress) $global:dartTestCreated.Add([pscustomobject]@{Profile=$Profile;RemoteAddress=$RemoteAddress;Action=$Action;Direction=$Direction}) }
foreach($testCase in @('EmptyStore','NoBlocks','ExistingBlocks')) {
$global:dartTestHasBlocks=($testCase -eq 'ExistingBlocks')
$global:dartTestEmptyStore=($testCase -eq 'EmptyStore')
foreach($internet in @($false,$true)) {
 $global:dartTestCreated=[Collections.Generic.List[object]]::new();$global:dartTestRemoved=[Collections.Generic.List[string]]::new()
 & (Join-Path $PSScriptRoot '..\..\installer\Configure-Firewall.ps1') -InstallDirectory $taskRoot -AllowInternet:$internet
 $expected=if($internet){'Any'}else{'LocalSubnet'}
 if($global:dartTestCreated.Count -ne 3) { throw 'Expected three executable rules' }
 foreach($rule in $global:dartTestCreated) { if($rule.Profile -ne 'Any' -or $rule.RemoteAddress -ne $expected -or $rule.Action -ne 'Allow' -or $rule.Direction -ne 'Inbound') { throw 'Wrong firewall scope' } }
 if($global:dartTestRemoved -contains 'other-app' -or ($global:dartTestHasBlocks -and $global:dartTestRemoved -notcontains 'same-app')) { throw 'Block repair escaped exact application scope' }
 $policy=Get-Content (Join-Path $taskRoot 'installation.json') -Raw | ConvertFrom-Json
 if(!$policy.AllowLan -or $policy.AllowInternet -ne $internet) { throw 'Incorrect persisted policy' }
 Write-Output "PASS: firewall mode Internet=$internet, Store=$testCase uses correct scope and exact-path block repair"
}


}
$global:dartTestCreated=[Collections.Generic.List[object]]::new()
$global:dartTestQueryFailure=$true
try {
 try {
  & (Join-Path $PSScriptRoot '..\..\installer\Configure-Firewall.ps1') -InstallDirectory $taskRoot
  throw 'Expected firewall enumeration failure'
 } catch {
  if($_.Exception.Message -ne 'Simulated firewall access failure') { throw }
 }
 if($global:dartTestCreated.Count) { throw 'Rules must not be created after a real enumeration failure' }
 Write-Output 'PASS: genuine firewall access failures still abort setup'
} finally { $global:dartTestQueryFailure=$false }
