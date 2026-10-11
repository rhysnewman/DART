$ErrorActionPreference='Stop'
$global:dartLaunchRegistered=$null
$global:dartLaunchRemoved=$false
function Get-CimInstance { param($ClassName,$Filter) [pscustomobject]@{SessionId=[Diagnostics.Process]::GetCurrentProcess().SessionId} }
function Invoke-CimMethod { param($InputObject,$MethodName) [pscustomobject]@{ReturnValue=0;Sid='S-1-5-21-test-desktop-user'} }
function New-ScheduledTaskAction { param($Execute,$WorkingDirectory) [pscustomobject]@{Execute=$Execute} }
function New-ScheduledTaskPrincipal { param($UserId,$LogonType,$RunLevel) [pscustomobject]@{UserId=$UserId;LogonType=$LogonType;RunLevel=$RunLevel} }
function New-ScheduledTaskSettingsSet { param([switch]$AllowStartIfOnBatteries,[switch]$DontStopIfGoingOnBatteries,$ExecutionTimeLimit) @{} }
function Register-ScheduledTask { param($TaskName,$Action,$Principal,$Settings) $global:dartLaunchRegistered=$Principal }
function Start-ScheduledTask { param($TaskName) }
function Get-ScheduledTaskInfo { param($TaskName) [pscustomobject]@{LastRunTime=[DateTime]::Now;LastTaskResult=267009} }
function Unregister-ScheduledTask { param($TaskName,[switch]$Confirm,$ErrorAction) $global:dartLaunchRemoved=$true }
$source=Get-Content (Join-Path $PSScriptRoot '..\..\installer\Launch-Dart.ps1') -Raw
# Return from the test script block rather than exiting the harness.
& ([scriptblock]::Create($source.Replace('exit 0','return'))) -InstallDirectory 'C:\Program Files\DART'
if($global:dartLaunchRegistered.UserId -ne 'S-1-5-21-test-desktop-user' -or $global:dartLaunchRegistered.RunLevel -ne 'Limited' -or $global:dartLaunchRegistered.LogonType -ne 'Interactive' -or !$global:dartLaunchRemoved) { throw 'Post-install launch must use desktop user, limited token, and remove temporary registration' }
Write-Output 'PASS: post-install launch selects the desktop user, requests limited interactive execution, and unregisters the temporary task'
