# FFmpeg download and verification

Status: downloaded, publisher SHA256 matched, extracted and executable tested.
Official discovery page: https://ffmpeg.org/download.html
FFmpeg provides source code itself; this official page links Gyan as a third-party Windows binary provider.
Distributor page: https://www.gyan.dev/ffmpeg/builds/
Archive URL: https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip
Publisher checksum URL: https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256
Actual SHA256: 60F467265B1E312373DBCD92200C2618A74850F98D3D078E94296BB3FA2047BA
Executable version: ffmpeg version 9.0.2-essentials_build-www.gyan.dev
Executable relative to this directory: ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe

Complete Windows x64 static essentials distribution retained, including ffmpeg.exe, ffprobe.exe, ffplay.exe, doc, presets, LICENSE and README.txt. The distributor describes this build as GPLv3. No global installation or PATH changes were made.

Verification: executable ran successfully; libx264 encoder, MJPEG decoder, image2pipe demuxer and RTSP muxer are present. Generated a two-frame H.264 MP4 using libx264 and decoded it successfully. version.txt records build configuration. smoke-test.mp4 is synthetic blue video only. Live RTSP interoperability and DART integration were not tested in this task.

The archive URL is mutable; use this recorded version/hash when reproducing the bundle. Archive and publisher checksum are preserved alongside the extracted distribution.
