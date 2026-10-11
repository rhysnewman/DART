# Test DART using Agent DVR

This guide uses Agent DVR as a local recorder for DART's virtual camera. Start with both applications on the same PC; then repeat with Agent DVR on another LAN PC if required. DART captures the desktop; Agent DVR receives and records the stream.

## Set up the recorder

1. Download Agent DVR from the [official download page](https://www.ispyconnect.com/download), install it and open its local web interface using the installer/start-menu link. Use the address/port supplied by your Agent DVR installation.
2. Install and launch DART using the [installation guide](INSTALLATION.md).
3. Open DART's **notification-area icon → Camera connection details…**. Copy the full ONVIF service URL, Username and Password. Use **Show password** or **Copy** for the masked password.

Agent DVR has separate licensing terms. Check the [vendor's current licensing](https://www.ispyconnect.com/buy) for your use, especially business deployments and remote access; do not assume employee-monitoring deployments are covered by free private local use.

## Add the ONVIF camera

1. In Agent DVR, use **Add Device** and choose an **ONVIF** camera, or edit a camera and configure its ONVIF source on the General tab. Exact navigation varies by Agent DVR version.
2. Enter DART's **Service URL**, **Username** and **Password**. Paste the URL explicitly if automatic LAN discovery does not find it.
3. Click **Discover** to retrieve the available stream/profile, and select DART's stream for both **Live URL** and **Record URL**. DART currently supplies one video profile.
4. Set the RTSP transport to **TCP** in the source's available advanced/FFmpeg options. DART's MediaMTX configuration enables TCP RTSP transport only. Leave RTSP/HTTP port overrides at their defaults unless your network mapping requires them.
5. Save the camera and verify a live desktop picture. The live image remains unchanged between DART captures even though encoded video frames continue to arrive.

These fields and the discovery flow are documented in Agent DVR's [ONVIF source reference](https://www.ispyconnect.com/docs/agent/video-source-types#onvif) and [adding cameras guide](https://www.ispyconnect.com/docs/agent/adding-cameras). Use DART's camera password, not an iSpyConnect account password.

### Direct RTSP fallback

If ONVIF addition fails, add an **IP/Network Camera** source. Paste DART's RTSP stream URL into the Live URL and Record URL fields, enter the DART camera username/password separately, and use TCP transport. This tests the video path independently of ONVIF control/discovery. See the [network camera source reference](https://www.ispyconnect.com/docs/agent/video-source-types).

## Record and play back

A live picture does not by itself prove recording is enabled. Select the DART camera and start a manual recording with Agent DVR's recording control. For an extended test, configure continuous recording using the recording settings in your installed version. Raw recording can store the supplied video without re-encoding; DART has no audio.

1. Record for at least 30 seconds.
2. Switch foreground windows and complete a move or resize. Allow roughly a second for event settling, and longer if another capture was just made.
3. Play a video in a stationary window without interacting. A new screenshot should still arrive at approximately 10-second intervals while the desktop is available.
4. Stop recording. Open Agent DVR's **Recordings** view and play the clip; verify both kinds of update appear.
5. Check **Server Settings → Storage** for the actual recording location and configure retention/disk limits. Per-camera Storage settings may choose a different location. Do not look in DART's debug folder for DVR recordings.

Agent DVR documents [recordings and their location](https://www.ispyconnect.com/docs/agent/recordings) and [storage/retention](https://www.ispyconnect.com/docs/agent/storage).

## Acceptance checklist

| Check | Expected result |
|---|---|
| Finish installation with Launch selected | DART tray icon appears without elevation; network rules are already installed |
| Copy connection details | Exact IP/host, ONVIF/RTSP ports and DART password are available from the tray window |
| Add ONVIF camera | Device/profile resolves and desktop video appears |
| Change foreground or finish moving a window | New picture after settling/throttling |
| Watch video without interacting | Picture refreshed about every 10 seconds, not every video frame |
| Record and play back | Saved clip contains the desktop changes |
| Exit DART | Stream disconnects and dedicated tools stop |
| Restart DART | Same session's credentials/ports persist; stream returns |
| Log off and on | DART exits/restarts; recheck addresses/ports if session ID changes |
| Lock/unlock | Placeholder while unavailable and capture resumes; validate on the target PC |

## Troubleshooting

- **No tray icon:** check the notification area's **^** panel, then use Start → DART. If startup fails, inspect `dart.log` through the debug folder.
- **No discovery:** enter the full service URL manually. UDP multicast may be blocked or use another adapter. Discovery is not required for direct connection.
- **Authentication failed:** recopy the DART credentials and confirm the camera PC and recorder clocks are reasonably synchronized; UsernameToken digest authentication rejects stale requests.
- **ONVIF connects but video fails:** verify the returned RTSP URL is reachable, use TCP, and inspect DART/Agent DVR logs. Test direct RTSP to isolate the control path.
- **Wrong IP address:** DART's automatic adapter selection may choose a VPN/virtual adapter. Set an appropriate `AdvertisedHost` in `camera.json` and restart; use the LAN address for a recorder on another PC.
- **Network prompts:** use the installed Start-menu copy, not an old portable copy. Rerun 0.3.2 setup after closing DART. The old 0.3.1 installer could stop before creating exceptions when no blocking rules existed.
- **Frozen picture:** wait at least 10 seconds or choose Capture now. Check for a locked/unavailable desktop and log errors.
- **No recordings:** enable recording explicitly and verify Agent DVR's storage settings and available disk space.

The project owner reported successful Agent DVR streaming and installation on a second machine after the 0.3.2 fix. This is practical interoperability testing, not ONVIF certification.
