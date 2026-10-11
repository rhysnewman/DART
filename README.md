# DART

DART turns a logged-on Windows desktop into a virtual camera for an ONVIF/RTSP recorder. It captures foreground-window changes and completed moves/resizes, plus a fresh screenshot every 10 seconds. FFmpeg repeats the latest image into a continuous H.264 video stream served by a dedicated MediaMTX instance.

DART runs visibly in the Windows notification area, starts at user logon and stops at logoff. It is a per-user-session application, not a Windows service. Its ONVIF Device/Media1 implementation is a testing subset, not a certified ONVIF profile.

## Install and try it

1. Obtain `DART-Setup-0.3.2.exe` from the project maintainer or a published GitHub Release. Only that EXE is needed; .NET, FFmpeg and MediaMTX are bundled.
2. Run setup, approve elevation, and keep **Launch DART now** selected. LAN access is enabled by default. The optional internet firewall setting is unchecked by default.
3. Find DART in the **notification area beside the Windows clock**. It may be hidden behind the **^** arrow. Double-click its icon, or right-click and select **Camera connection details…**.
4. Copy the displayed **ONVIF service URL** (including the PC's IP address and port), **Username** and **Password**. Use **Show password** or the password's **Copy** button.
5. Add an ONVIF camera to Agent DVR using those details, then choose RTSP over TCP. Start recording explicitly to test playback.

Follow the [installation guide](docs/INSTALLATION.md) and [Agent DVR test guide](docs/AGENT-DVR.md) for the complete process and troubleshooting.

## Documentation

| Document | Contents |
|---|---|
| [Design](docs/DESIGN.md) | Components, capture triggers, metadata, encoding, lifecycle and limitations |
| [Installation](docs/INSTALLATION.md) | Setup, tray controls, addresses/password, network access and uninstall |
| [Agent DVR testing](docs/AGENT-DVR.md) | Local/LAN camera setup, recording and acceptance checks |
| [Building](docs/BUILDING.md) | SDK, dependency versions, portable builds, installer and tests |
| [ONVIF reference](ONVIF.md) | Supported SOAP operations, authentication and stream behaviour |
| [GitHub publishing](docs/PUBLISHING.md) | Source repository, release packaging and exclusions |
| [Validation history](VALIDATION.md) | Earlier checks plus current user-reported installation/interoperability results |

## Source and dependencies

C# source is in `src/Dart`; installer source is in `installer`; build/test scripts are in `scripts` and `tests`. Generated binaries, downloaded dependencies, captures and credentials are excluded from Git.

The DART source currently has **no open-source licence grant**. Publishing it on GitHub does not make it MIT-licensed. FFmpeg, MediaMTX, .NET and NSIS retain their own licences; see [third-party notices](installer/THIRD-PARTY.txt). The current bundled FFmpeg build is GPL-enabled. Its corresponding-source/build-material requirements must be addressed before publishing the bundled installer externally; uploading DART source alone does not provide the source of those dependencies.

DART has been tested with Agent DVR by the project owner, including installation on a second machine following the 0.3.2 firewall fix. RDP/disconnected sessions, multiple simultaneous users and mixed monitor configurations still need deployment-specific testing. HTTP/RTSP transport is unencrypted; use a trusted network or VPN for remote access.
