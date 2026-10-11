# Build DART from source

## Prerequisites

Use Windows x64, PowerShell, Git and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). The source targets `net8.0-windows` with Windows Forms and ASP.NET Core. Installer recipients do not need the SDK or runtime because setup is self-contained.

```powershell
dotnet build .\src\Dart\Dart.csproj -c Release
```

## Portable bundle

Obtain Windows x64 FFmpeg and MediaMTX distributions from their publishers. FFmpeg's [official download page](https://ffmpeg.org/download.html) links third-party Windows binary builders; FFmpeg itself publishes source. The tested build includes libx264, the MJPEG decoder, image2pipe demuxer and RTSP muxer. Use a MediaMTX version compatible with the generated authentication/service configuration, including the `moq` option.

```powershell
.\scripts\Publish.ps1 -MediaMtxExe C:\dependencies\mediamtx.exe -FFmpegExe C:\dependencies\ffmpeg.exe
.\dist\Dart.exe
```

Deploy the whole `dist` directory for a portable build. Publish.ps1 does not add firewall rules or package all dependency notices; use the installer workflow for managed setup. `scripts/Fetch-Tools.ps1` can fetch HTTPS ZIP archives when supplied explicit URLs and independently verified SHA-256 values. It prints actual executable paths; its hash-suffixed extraction directories differ from the fixed layout currently expected by Build-Installer.ps1.

## Tested dependency baseline

| Component | Version | Archive SHA-256 |
|---|---|---|
| FFmpeg Gyan essentials | 9.0.2 | `60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA` |
| MediaMTX Windows amd64 | 1.21.2 | `A84224F5A490855684E62BA6FB71E80EA2A5E16AED61883BB58380290E93E467` |
| NSIS portable | 3.12 | `56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F` |
| .NET SDK/runtime used | SDK 8.0.425 / runtime 8.0.31 | Installed Microsoft runtime |

Dependency provenance from the development machine is preserved in [dependencies](dependencies). FFmpeg's recorded release URL is mutable: a later download may have another hash/version. A hash mismatch must be resolved by obtaining the exact build or explicitly reviewing a dependency upgrade; do not bypass verification.

## Build the installer

The current script expects these untracked local dependency files:

```text
vendor/ffmpeg/ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe
vendor/ffmpeg/ffmpeg-9.0.2-essentials_build/LICENSE
vendor/ffmpeg/ffmpeg-9.0.2-essentials_build/README.txt
vendor/ffmpeg/DOWNLOAD-STATUS.md
vendor/mediamtx/mediamtx.exe
vendor/mediamtx/LICENSE
vendor/mediamtx/provenance.json
vendor/nsis/nsis-3.12/Bin/makensis.exe
```

Extract the publisher distributions into that layout and copy the corresponding provenance files from `docs/dependencies` (FFMPEG.md → DOWNLOAD-STATUS.md and mediamtx-provenance.json → provenance.json). The script also expects `C:\Program Files\dotnet` licence files and ASP.NET Core runtime `8.0.31` notices. For another runtime baseline, update that notice path and dependency assumptions together. This is a documented pinned local build, not yet a fully automated clean-machine dependency bootstrap.

```powershell
.\scripts\Build-Installer.ps1
```

It publishes the self-contained Windows x64 payload, bundles tools/notices/scripts, generates an exact uninstall list and compiles `installer/DART-Setup-0.3.2.exe`. NSIS can also be supplied using `-Compiler C:\path\makensis.exe`. Building does not install DART or alter firewall/startup configuration. In restricted execution environments the compiler may require permission to run outside the sandbox.

## Checks

```powershell
dotnet run --project .\tests\InstallerChecks -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\InstallerChecks\FirewallChecks.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\InstallerChecks\LaunchChecks.ps1
```

The firewall/launch tests mock Windows configuration commands and do not change real firewall rules or register real tasks. Job tests create temporary child processes. Firewall regressions cover no existing rules, no blocking rules, existing app blocks, LAN/internet scopes and real enumeration errors.

Optional integration: with DART stopped, inspect and run `scripts/Test-Onvif.ps1`. It expects the test tooling/bundle paths declared in that script, starts a timed capture run, writes private test data under ignored `test-output`, and probes SOAP/authentication/discovery/RTSP. It is not certification. For user-facing acceptance checks use [Agent DVR testing](AGENT-DVR.md).
