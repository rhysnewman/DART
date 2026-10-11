param([Parameter(Mandatory)][string]$DartExe, [switch]$Remove)
$ErrorActionPreference='Stop'
$name='DART'
if($Remove) { Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $name -ErrorAction SilentlyContinue; exit }
$exe=(Resolve-Path -LiteralPath $DartExe).Path
New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Force | Out-Null
Set-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $name -Value ('"'+$exe+'"')
