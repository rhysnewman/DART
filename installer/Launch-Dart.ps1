param([Parameter(Mandatory=$true)][string]$InstallDirectory)
$ErrorActionPreference='Stop'
# A temporary Interactive/Limited task launches as the desktop user, including
# when setup was elevated using another administrator's credentials.
$session=[Diagnostics.Process]::GetCurrentProcess().SessionId
$explorer=Get-CimInstance Win32_Process -Filter "Name='explorer.exe'" | Where-Object SessionId -eq $session | Select-Object -First 1
if(!$explorer) { throw 'No interactive desktop found. Open DART from the Start menu.' }
$owner=Invoke-CimMethod -InputObject $explorer -MethodName GetOwnerSid
if($owner.ReturnValue -ne 0) { throw 'Could not identify desktop user.' }
$name='DART-Launch-'+[Guid]::NewGuid().ToString('N')
try {
 $action=New-ScheduledTaskAction -Execute (Join-Path $InstallDirectory 'Dart.exe') -WorkingDirectory $InstallDirectory
 $principal=New-ScheduledTaskPrincipal -UserId $owner.Sid -LogonType Interactive -RunLevel Limited
 $settings=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
 Register-ScheduledTask -TaskName $name -Action $action -Principal $principal -Settings $settings | Out-Null
 Start-ScheduledTask -TaskName $name
 $deadline=[DateTime]::UtcNow.AddSeconds(15)
 do {
  Start-Sleep -Milliseconds 200
  $info=Get-ScheduledTaskInfo -TaskName $name
  if($info.LastRunTime.Year -gt 2000) {
   if($info.LastTaskResult -notin @(0,267009)) { throw "DART launch failed: $($info.LastTaskResult)" }
   exit 0
  }
 } while([DateTime]::UtcNow -lt $deadline)
 throw 'Timed out launching DART. Open it from the Start menu.'
} finally {
 Unregister-ScheduledTask -TaskName $name -Confirm:$false -ErrorAction SilentlyContinue
}
