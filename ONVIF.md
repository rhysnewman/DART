# ONVIF and cloud testing

DART now exposes a read-only ONVIF Device/Media1 subset for testing camera clients. It is not certified Profile S/T, does not advertise profile-conformance scopes, and has been tested by the project owner with Agent DVR. No cloud account is created or footage uploaded by DART itself.

## Run and connect locally

Install the current self-contained setup, then launch DART from the final installation page or the Start menu. For a portable development bundle, run `dist/Dart.exe` with its whole bundle present.

Open **Camera connection details…** by double-clicking the DART notification-area icon (look behind the **^** arrow beside the clock if hidden). Copy the ONVIF service URL, RTSP URL, username and password. Use the password Copy button or Show password. These are DART camera credentials, not Agent DVR account credentials. See [installation](docs/INSTALLATION.md) and [Agent DVR testing](docs/AGENT-DVR.md).

Defaults are ONVIF HTTP `18080 + Windows session ID` and RTSP TCP `18554 + session ID`; always use the active connection window values. LAN listening/discovery are enabled. The advertised host is a detected LAN IPv4 address when the existing configuration specifies localhost. Localhost RTSP readers can access without credentials; remote readers must authenticate.

Add the complete service URL manually if discovery is unavailable. Device service returns the media service URL; GetProfiles returns profile1; GetStreamUri returns the RTSP URI. SOAP 1.2 and Media1 are supported. Choose RTSP-over-TCP.

Camera settings are read at startup: exit DART before editing and relaunch afterwards. Invalid configuration is logged as startup failure.

## Continuous video / triggered screenshots

Capture remains driven by foreground and move/resize events, with an independent 10-second periodic capture. The independently scheduled sender repeats the latest in-memory JPEG at FrameRate (default 5 fps). Screenshot/metadata files remain debugging artifacts; the encoder does not poll partially written files.

FFmpeg encodes H.264 Main, 1920x1080, yuv420p, with a two-second keyframe interval. BitrateKbps defaults to 1000. VBV min/max bitrate and x264 NAL-HRD CBR add filler to repeated/static frames. This is a codec bitrate target: packet/network overhead, VBV startup and scheduling can change observed throughput. Windows scheduling is not a real-time guarantee. A stalled/crashed encoder reconnects; repeated frames preserve a regular encoded media timeline during normal operation.

Changing FrameRate or BitrateKbps in camera.json changes BOTH encoder settings and advertised ONVIF profile after restart. Encoder configuration setters intentionally fault: clients should use the supplied fixed profile. JPEG XMP metadata is not transported as ONVIF timed metadata.

## LAN/cloud adapter access

A cloud recorder cannot connect to localhost on your PC. The easiest cloud route is a vendor adapter on your LAN, which pulls ONVIF/RTSP and uploads through its own authenticated outbound connection.

For an adapter on another machine, stop DART and edit camera.json:

```json
{
  "BindAddress": "0.0.0.0",
  "AdvertisedHost": "192.168.1.50",
  "OnvifPort": 18081,
  "RtspPort": 18555,
  "FrameRate": 5,
  "BitrateKbps": 1000,
  "Username": "dart",
  "Password": "replace-with-your-own-long-random-password",
  "Discovery": true,
  "DeviceId": "keep-the-existing-device-uuid"
}
```

Preserve the valid existing DeviceId, and replace AdvertisedHost with the PC's reachable LAN IPv4 address or DNS name. Runtime loading enables the 0.0.0.0 listener and discovery. Setup creates executable-scoped firewall rules across all profiles, LocalSubnet by default or any remote address with the internet option. UDP 3702 is used for LAN WS-Discovery. If discovery is blocked/unavailable, enter the endpoint URL manually.

Use these username/password credentials for both camera control and RTSP reading. HTTP Basic and WS-Security UsernameToken PasswordDigest are implemented; replayed nonces and digests more than five minutes from the PC clock are rejected. HTTP Digest and WS PasswordText are not implemented. The ONVIF clock operation is intentionally public. HTTP/RTSP are not encrypted in this implementation; use a trusted LAN or VPN, not direct public-internet forwarding. If a bridge runs in a VM/container, localhost is its own namespace: configure the PC's LAN address.

For a cloud-hosted recorder that directly pulls cameras, establish a routed VPN and configure AdvertisedHost with the reachable VPN/DNS address. WS-Discovery multicast is LAN-local and won't normally traverse the VPN; use manual endpoint entry. A vendor requiring proprietary device registration or outbound camera upload needs its connector; ONVIF Media1 by itself does not provide that uplink.

## Suggested test services

**Videoloft + its Windows Virtual Cloud Adapter** is the first cloud option to evaluate. The vendor offers a software adapter for Windows/Linux and supports cameras via ONVIF Profiles S/T. Its adapter uploads to the Videoloft cloud, requiring a subscription. DART implements a subset, not a certified camera, so confirm acceptance of its fixed Media1 profile before committing to a subscription. The vendor may require additional methods/auth modes; record the requested operation if addition fails.

- https://videoloft.com/videoloft-launches-virtual-cloud-adapter-for-windows/
- https://videoloft.com/resources/faqs/

**Agent DVR** is a useful local interoperability test first: it accepts ONVIF/RTSP sources and provides subscribed remote browser access. It is local VMS software with remote access rather than a purely hosted ONVIF ingest server.

- https://www.ispyconnect.com/docs/agent/adding-cameras
- https://www.ispyconnect.com/docs/agent/remoteaccess

Eagle Eye Networks is another cloud VMS using a local bridge that discovers ONVIF cameras, but involves that bridge/account setup rather than a simple open online test URL:
https://support.een.com/portal/en/kb/articles/common-support-questions

## Implemented surface and limitations

- Device: GetDeviceInformation, GetSystemDateAndTime, GetServices, GetCapabilities, GetServiceCapabilities, GetScopes, GetHostname, GetDiscoveryMode.
- Media1: GetProfiles, GetProfile, GetVideoSources, GetVideoSourceConfigurations, GetVideoSourceConfiguration, GetVideoEncoderConfigurations, GetVideoEncoderConfiguration, compatible source/encoder configurations, GetVideoEncoderConfigurationOptions, GetStreamUri, GetSnapshotUri, GetServiceCapabilities.
- WS-Discovery 2005/04: Probe/ProbeMatches, stable endpoint UUID and type/scope filtering. No Hello/Bye, Resolve or remote discovery proxy. Multicast discovery on multiple interfaces and multiple RDP sessions remains untested. Only one primary DART camera/adapter per machine is recommended initially.
- Authenticated HTTP snapshot endpoint; returns the current stream image, including unavailable-desktop placeholder.
- Unsupported commands and unknown profile/config tokens return SOAP faults. No PTZ, audio, events, Media2/Profile T or camera configuration mutation.
- Camera configuration and generated MediaMTX configuration contain credentials on disk; stored under the user's LocalAppData by default. Do not commit or distribute these files.
- Native children belong to a kill-on-close Windows job; normal exit and forced parent termination clean them up. A small process-creation/job-assignment race remains.

## Automated integration test

With DART not already running in your session:

```powershell
.\scripts\Test-Onvif.ps1
```

It runs for 55 seconds, saves debug data under test-output/onvif-run, and checks SOAP operations, valid/missing/replayed credentials, profile token faults, snapshots, a local WS-Discovery probe, RTSP profile/frame rate and measured bitrate. This is a protocol smoke test, not ONVIF certification or independent VMS interoperability validation. The script overwrites ONLY its test camera.json with fresh test credentials.


## Connection details window

Double-click the DART tray icon or choose **Camera connection details…** at the top of its menu. The window shows the active ONVIF/RTSP addresses, ports and username/password with copy buttons. Password is masked until Show password is checked. Closing this window keeps DART running. Open debug folder remains below Capture now in the tray menu. This window reflects settings loaded by the running process, not un-applied edits to camera.json.
