# DART installation and operation

## Requirements and setup

Use a 64-bit Windows PC with an interactive desktop and administrator approval for installation. The installer bundles the runtime and native tools; a recipient does not need the .NET SDK, a separate FFmpeg installation or a separate MediaMTX installation.

1. Close any running DART instance using its notification-area menu. For an upgrade, close it in every logged-on Windows session.
2. Run `DART-Setup-0.3.2.exe` and approve Windows' administrator prompt. The default destination is `C:\Program Files\DART`.
3. On the final page, **Launch DART now** is checked. Leave it checked to start immediately as the interactive desktop user with limited privileges.
4. **Allow internet connections through Windows Firewall** is unchecked. Leave it unchecked for a same-PC or ordinary LAN recorder. Checking it broadens the installed applications' inbound Windows Firewall rules from LocalSubnet to any remote IP address.
5. Click **Finish**. DART also starts automatically when users next log on and stops at logoff. It does not capture before logon.

Windows may show an unsigned-publisher/SmartScreen warning because this test installer is not code-signed. This is separate from the administrator prompt and application network-access prompts. Setup 0.3.2 fixes a firewall query failure that occurred on machines without existing blocking rules.

## Find the IP address, ports and password

DART does not open a main window automatically. Look in the **notification area beside the clock**, including the **^** hidden-icons panel.

1. Double-click the DART icon, or right-click and select **Camera connection details…**.
2. Copy the **ONVIF service URL**. It contains the camera PC's advertised IP address and ONVIF port.
3. Copy **Username** and **Password** using their buttons. The password is masked; select **Show password** to reveal it.
4. Copy the **RTSP stream URL** if your recorder needs a direct video URL. Both individual ports are also displayed.

These are DART camera credentials, not the Windows login or Agent DVR account credentials. The connection window displays the running configuration. Closing it leaves DART running.

Example only: a session-1 PC at `192.168.1.50` might show `http://192.168.1.50:18081/onvif/device_service` and `rtsp://192.168.1.50:18555/desktop`. Always use the actual window values. Default ports are `18080 + session ID` and `18554 + session ID`; they can differ on another PC/session. Session IDs can change, so recheck details if a recorder stops connecting after a session change.

## Tray controls

- **Camera connection details…** opens the addresses/credentials window.
- **Capture now** requests a new capture, subject to normal settling/throttling.
- **Open debug folder** opens this session's runtime data.
- **Exit DART** stops capture, encoding and its dedicated MediaMTX server. Use the Start-menu DART shortcut to restart.

## Network behaviour

DART listens on all IPv4 interfaces and enables LAN discovery, including when upgrading a previously localhost-only configuration. If the configured advertised address is localhost, DART chooses an active LAN IPv4 address, preferring an adapter with a gateway. An explicit non-local advertised host is preserved. On multi-adapter PCs or after address changes, verify it and edit `AdvertisedHost` in `camera.json` if necessary, then restart.

Setup runs elevated and creates executable-scoped inbound rules for installed `Dart.exe`, `tools\mediamtx.exe` and `tools\ffmpeg.exe`, across Domain, Private and Public profiles. LAN-only scope means Windows' LocalSubnet addresses; routed remote subnets are not necessarily included. Setup also repairs prior local blocking rules for those exact executable paths. Portable copies in other directories have different paths and are not covered. Centrally enforced firewall policies can override local rules.

The internet checkbox changes only Windows Firewall scope. It does not arrange router port forwarding, public DNS, a cloud account, upload or TLS. A remote recorder needs to reach both the actual ONVIF HTTP and RTSP TCP ports, and ONVIF must advertise a reachable host. Discovery multicast is LAN-local. HTTP/RTSP are unencrypted in this prototype; use a VPN or trusted network. Kestrel hosts HTTP directly, so no HTTP.sys URL reservation is required.

## Runtime files and retention

Data is in `%LOCALAPPDATA%\DART\session-<id>` for the logged-on user:

| File | Purpose / retention |
|---|---|
| `camera.json` | Persistent credentials, device UUID, advertised host and encoding/port settings; retain to keep identity |
| Numbered `.jpg` / `.json` | Latest 20 snapshot/metadata pairs, older pairs removed |
| `latest.json` | Pointer to the latest complete pair |
| `mediamtx.yml` | Regenerated configuration, contains credentials |
| `dart.log`, `dart.log.old` | Diagnostic log, rotated at roughly 2 MB with one previous file |

The installation directory contains machine-controlled `installation.json`, executables and notices. Agent DVR recordings are separate and managed by Agent DVR. DART debug retention does not limit DVR recording retention.

Stop DART before deleting debug files. Deleting `camera.json` causes new credentials/device identity to be generated; update your recorder afterwards. Do not share debug folders publicly: they contain desktop contents, window titles and camera credentials.

## Upgrades and removal

Install over the existing directory after closing DART. Existing user credentials/device IDs/ports are preserved. Choose the desired final-page options again. Uninstall through Windows Installed apps or **Uninstall DART** in the Start menu; it removes bundled files, startup registration, shortcuts and named firewall rules, while retaining user data.

For a local Agent DVR test, continue with [the test guide](AGENT-DVR.md).
