# DART installer 0.3.2

Exit DART in every Windows session, run DART-Setup-0.3.2.exe, and approve the administrator prompt. Install over the existing installation to retain user camera credentials, device IDs and ports.

The last page has two options:
- **Launch DART now**: checked by default. A temporary Windows scheduled task runs DART as the interactive desktop user with Limited privileges, then the task registration is removed. This also handles setup elevated using another administrator account. If Task Scheduler is unavailable, setup reports the failure and DART can be opened from Start.
- **Allow internet connections through Windows Firewall**: unchecked by default. Unchecked allows LocalSubnet; checked allows any remote address. Router forwarding and external firewalls remain separate. For internet ONVIF connections, configure AdvertisedHost in camera.json to the reachable public address/DNS name and forward the actual ONVIF and RTSP ports shown in DART's connection window. ONVIF discovery remains a LAN mechanism.

LAN listening and discovery are always enabled, including upgrades from localhost-only settings. Existing non-local AdvertisedHost values are preserved; a localhost value is replaced with a detected LAN IPv4 address. Check the advertised address on PCs with multiple adapters. Ordinary LAN means Windows Firewall's LocalSubnet scope; routed remote subnets require the any-address option or custom explicit rules.

Setup installs executable-scoped inbound allow rules for Dart.exe, tools/mediamtx.exe and tools/ffmpeg.exe, for all protocols and all three Windows profiles (Domain, Private and Public). Rules use the chosen address scope, and ports may vary by Windows session. Setup removes the old DART-named rules and local blocking rules targeting these exact installed executable paths, including deny rules created by earlier Windows prompts. It does not modify rules for portable copies or unrelated programs, disable the firewall, or override centrally enforced policies. Outbound defaults are unchanged. Start the installed Start-menu copy rather than an older dist copy; firewall exceptions are path-specific.

All-user HKLM logon startup remains enabled. DART stops at logoff; its FFmpeg and MediaMTX processes belong to a kill-on-close job. The .NET runtime, FFmpeg and MediaMTX are bundled, so no SDK installation is needed. Runtime credentials, logs and the retained capture window remain under the user's LocalAppData. Uninstall removes the bundled files, machine startup entry, shortcuts and named firewall rules, while retaining user data. Silent installation uses LAN-only defaults and does not launch DART.

Build: scripts/Build-Installer.ps1, using the pinned dependencies and NSIS compiler in vendor. The installer is unsigned. See THIRD-PARTY.txt before redistribution.

Validation: self-contained Release publish and NSIS compilation; child cleanup on disposal and forced parent termination; camera identity preservation and legacy policy migration; mocked firewall checks for LAN/internet address scopes, all profiles, and exact-path block repair. The firewall checks also run under Windows PowerShell 5.1, used by setup. Actual elevated install/uninstall, post-install launch, firewall prompts and remote connectivity require installation testing; building this package does not alter this PC's firewall or startup settings.


Version 0.3.2 fixes setup failing when no blocking firewall rules exist. Setup now enumerates the persistent rule store and filters locally, avoiding CIM no-match errors while retaining genuine access-error reporting. Windows PowerShell 5.1 regression checks cover empty stores, stores without blocks, existing app-specific blocks, LAN/internet modes, and genuine enumeration failures.
