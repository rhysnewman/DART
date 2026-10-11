# Local validation — 2026-10-11

- .NET SDK 8.0.425 detected; Windows Desktop runtime 8.0.31 installed.
- Release build: zero warnings and errors.
- Dedicated FFmpeg 9.0.2 / MediaMTX 1.21.2 bundled in dist/tools.
- Live capture: 3840x2160; JSON contained approximately 20 visible top-level window entries.
- Startup, foreground and move-size-end captures observed, respecting the three-second minimum interval in the sampled captures.
- Custom JPEG XMP JSON decoded and matched its sidecar exactly.
- FFprobe on localhost TCP RTSP: H.264, 1920x1080.
- FFmpeg decoded two stream frames to null output, exit 0.
- Timed host exit: exit 0; no persistent monitoring intended after test.
- Fixed unwanted MediaMTX MoQ listener by explicitly disabling it.
- Added timed test/data-directory switches; fixed snapshot numbering to continue after restart (build verified, restart case not yet exercised).
- Stream log contains a deprecated pixel-format/range warning from swscale; playback decoded successfully. Exact colour-range fidelity remains to be checked.

Not validated: session lock/RDP states, mixed-DPI/multiple monitors, external VMS/ONVIF, crash recovery and distribution installation.

Debug captures are in test-output/local-run and test-output/stream-run; they contain desktop contents and window titles and are ignored by Git. No logon startup registration was performed.

## ONVIF integration — 2026-10-11

Built with zero warnings/errors. Timed integration test passed device/media SOAP calls, WS-Security PasswordDigest, replay rejection, missing-credential rejection, invalid token/unsupported operation faults, HTTP snapshot authentication and local WS-Discovery ProbeMatch. FFprobe confirmed H.264 Main 1920x1080 at 5 fps; the four-second sample measured approximately 1057 kbps against a 1000 kbps CBR target. Timed exit returned zero. LAN multicast discovery, remote RTSP credentials, vendor VMS/cloud interoperability, TLS and ONVIF certification were not tested/implemented as applicable. See ONVIF.md for exact supported scope.

## Installer and agent lifecycle

Self-contained Windows x64 publish passed. NSIS 3.12 compiled the setup. InstallerChecks passed: job disposal kills the owned child, forced host termination kills the owned child, LAN installation mode preserves existing credentials/device ID/ports, and runtime policy does not overwrite existing user configuration. Actual machine-wide installation/uninstallation, logoff/relogon and LAN firewall reachability remain untested. No system startup/firewall changes were applied during the build.

## Current status — installer 0.3.2

The project owner reported successful Agent DVR streaming and recording tests during development. Version 0.3.1 failed on a second PC when Get-NetFirewallRule -Action Block returned a CIM no-match error; version 0.3.2 fixes this by local filtering and pre-resolving application filters. The owner subsequently reported the installation working.

Automated checks passed: Release/self-contained publish, NSIS compilation, child job cleanup, camera identity/LAN migration, Windows PowerShell 5.1 mocked firewall modes with empty/no-block/existing-block stores, genuine-error propagation, and mocked limited interactive post-install launch. This user report supplements the earlier history above; it does not imply exhaustive RDP, multiple-session, public-internet, clean-machine or certification validation.
