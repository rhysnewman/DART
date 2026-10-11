!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "nsDialogs.nsh"
Var LaunchCheckbox
Var InternetCheckbox
Unicode true
Name "DART"
OutFile "DART-Setup-0.3.2.exe"
InstallDir "$PROGRAMFILES64\DART"
InstallDirRegKey HKLM "Software\DART" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
VIProductVersion "0.3.2.0"
VIAddVersionKey "ProductName" "DART"
VIAddVersionKey "FileDescription" "DART Windows desktop camera installer"
VIAddVersionKey "FileVersion" "0.3.2"
VIAddVersionKey "LegalCopyright" "DART contributors"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TEXT "Install DART, its dedicated FFmpeg/MediaMTX tools, and the bundled .NET runtime.$\r$\n$\r$\nDART starts in each user's session at logon and stops at logoff. Camera credentials and debug data remain in that user's LocalAppData.$\r$\n$\r$\nExit any running DART tray instance before continuing."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
Page custom FinishPage FinishLeave
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

!macro CheckStopped PREFIX
Function ${PREFIX}CheckDartStopped
 retry:
 System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\DART.Capture") p .r0'
 ${If} $0 != 0
  System::Call 'kernel32::CloseHandle(p r0)'
  MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "DART is running in this Windows session. Exit it from the tray menu, then click Retry." IDRETRY retry
  Abort
 ${EndIf}
FunctionEnd
!macroend
!insertmacro CheckStopped ""
!insertmacro CheckStopped "un."
Function .onInit
 ${IfNot} ${RunningX64}
  MessageBox MB_ICONSTOP "DART requires 64-bit Windows."
  Abort
 ${EndIf}
 SetRegView 64
 SetShellVarContext all
 Call CheckDartStopped
FunctionEnd

Function ConfigureFirewall
 nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$INSTDIR\Configure-Firewall.ps1" -InstallDirectory "$INSTDIR" $2'
 Pop $0
 Pop $1
 ${If} $0 != 0
  DetailPrint "$1"
  MessageBox MB_ICONSTOP "Could not configure DART firewall rules (exit $0):$\r$\n$1"
  Abort
 ${EndIf}
FunctionEnd

Function FinishPage
 !insertmacro MUI_HEADER_TEXT "DART is installed" "Choose the final options, then click Finish."
 nsDialogs::Create 1018
 Pop $0
 ${NSD_CreateLabel} 0 0 100% 35u "LAN connections are enabled on Private, Public and Domain networks. Double-click the tray icon for camera URLs and credentials."
 Pop $0
 ${NSD_CreateCheckbox} 0 48u 100% 16u "Launch DART now"
 Pop $LaunchCheckbox
 ${NSD_Check} $LaunchCheckbox
 ${NSD_CreateCheckbox} 0 75u 100% 20u "Allow internet connections through Windows Firewall"
 Pop $InternetCheckbox
 ${NSD_CreateLabel} 12u 100u 95% 42u "Unchecked: this PC and the local subnet can connect. Checked: any remote address can connect. Router forwarding and external firewalls need separate configuration."
 Pop $0
 GetDlgItem $0 $HWNDPARENT 1
 SendMessage $0 ${WM_SETTEXT} 0 "STR:Finish"
 nsDialogs::Show
FunctionEnd
Function FinishLeave
 ${NSD_GetState} $InternetCheckbox $0
 StrCpy $2 ""
 ${If} $0 == ${BST_CHECKED}
  StrCpy $2 "-AllowInternet"
 ${EndIf}
 Call ConfigureFirewall
 ${NSD_GetState} $LaunchCheckbox $0
 ${If} $0 == ${BST_CHECKED}
  nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$INSTDIR\Launch-Dart.ps1" -InstallDirectory "$INSTDIR"'
  Pop $0
  Pop $1
  ${If} $0 != 0
   MessageBox MB_ICONEXCLAMATION "DART is installed but could not start automatically:$\r$\n$1$\r$\nOpen DART from the Start menu."
  ${EndIf}
 ${EndIf}
FunctionEnd

Section "DART and logon startup (required)" Main
 SectionIn RO
 SetOutPath "$INSTDIR"
 File /r "payload\*.*"
 WriteUninstaller "$INSTDIR\Uninstall.exe"
 WriteRegStr HKLM "Software\DART" "InstallDir" "$INSTDIR"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "DisplayName" "DART Desktop Camera"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "DisplayVersion" "0.3.2"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "Publisher" "DART"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "InstallLocation" "$INSTDIR"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "DisplayIcon" "$INSTDIR\Dart.exe"
 WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "NoModify" 1
 WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART" "NoRepair" 1
 DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DART"
 WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "DART" '$\"$INSTDIR\Dart.exe$\"'
 CreateDirectory "$SMPROGRAMS\DART"
 CreateShortcut "$SMPROGRAMS\DART\DART.lnk" "$INSTDIR\Dart.exe"
 CreateShortcut "$SMPROGRAMS\DART\Uninstall DART.lnk" "$INSTDIR\Uninstall.exe"
 StrCpy $2 ""
 Call ConfigureFirewall
SectionEnd

Function un.onInit
 Call un.CheckDartStopped
 SetRegView 64
 SetShellVarContext all
 MessageBox MB_OKCANCEL|MB_ICONINFORMATION "Exit DART from the tray in every logged-on session before uninstalling. User camera settings and debug data will be retained." IDOK +2
 Abort
FunctionEnd
Section "Uninstall"
 DeleteRegValue HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "DART"
 nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$INSTDIR\Configure-Firewall.ps1" -InstallDirectory "$INSTDIR" -Remove'
 Pop $0
 Pop $1
 ${If} $0 != 0
  MessageBox MB_ICONSTOP "Could not remove DART firewall rules:$\r$\n$1"
  Abort
 ${EndIf}
 Delete "$SMPROGRAMS\DART\DART.lnk"
 Delete "$SMPROGRAMS\DART\Uninstall DART.lnk"
 RMDir "$SMPROGRAMS\DART"
 ; Remove only explicitly bundled files; preserve unrelated files in the chosen directory.
 !include "uninstall-files.nsh"
 Delete "$INSTDIR\installation.json"
 Delete "$INSTDIR\Uninstall.exe"
 RMDir "$INSTDIR"
 DeleteRegKey HKLM "Software\DART"
 DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\DART"
SectionEnd
