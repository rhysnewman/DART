using System.Diagnostics;
using System.Net.Sockets;

namespace Dart;
internal sealed class StreamHost : IDisposable
{
    readonly string root, data;
    readonly int port;
    readonly CameraSettings settings;
    readonly Action<string> log;
    Process? server, encoder;
    readonly ChildProcessJob children = new();
    public StreamHost(string root, string data, CameraSettings settings, Action<string> log)
    { this.root=root; this.data=data; this.port=settings.RtspPort; this.settings=settings; this.log=log; }
    Process Start(string name, IEnumerable<string> args, bool input = false)
    {
        var exe = Path.Combine(root, "tools", name);
        if (!File.Exists(exe)) throw new FileNotFoundException("Bundle dependency missing", exe);
        var info = new ProcessStartInfo(exe) { UseShellExecute=false, CreateNoWindow=true,
            RedirectStandardInput=input, RedirectStandardError=true, RedirectStandardOutput=true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        var p = new Process { StartInfo=info };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) log(name+": "+e.Data); };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log(name+": "+e.Data); };
        p.Start();
        try { children.Add(p); } catch { try { p.Kill(true); } finally { p.Dispose(); } throw; }
        p.BeginErrorReadLine(); p.BeginOutputReadLine(); return p;
    }
    public async Task Send(byte[] jpeg, CancellationToken token)
    {
        if (server is null || server.HasExited)
        {
            Stop(ref encoder); Stop(ref server);
            // Fail rather than accidentally publishing to another application's server.
            var listener = new TcpListener(System.Net.IPAddress.Parse(settings.BindAddress), port);
            listener.Start(); listener.Stop();
            var config = Path.Combine(data, "mediamtx.yml");
            File.WriteAllText(config, $"""
                logLevel: warn
                rtspAddress: {settings.BindAddress}:{port}
                rtspTransports: [tcp]
                rtmp: false
                hls: false
                webrtc: false
                srt: false
                moq: false
                api: false
                metrics: false
                pprof: false
                authInternalUsers:
                  - user: any
                    ips: ['127.0.0.1', '::1']
                    permissions:
                      - action: publish
                        path: desktop
                      - action: read
                        path: desktop
                  - user: {System.Text.Json.JsonSerializer.Serialize(settings.Username)}
                    pass: {System.Text.Json.JsonSerializer.Serialize(settings.Password)}
                    permissions:
                      - action: read
                        path: desktop
                paths:
                  desktop:
                    source: publisher
                """);
            server = Start("mediamtx.exe", new[] { config });
            var ready = false;
            for (var i=0; i<30; i++)
            {
                if (server.HasExited) throw new IOException("MediaMTX exited during startup; inspect log.");
                try { using var c = new TcpClient(); await c.ConnectAsync("127.0.0.1", port, token); ready=true; break; }
                catch (SocketException) { await Task.Delay(100, token); }
            }
            if (!ready) throw new IOException("MediaMTX startup timed out.");
        }
        if (encoder is null || encoder.HasExited)
        {
            Stop(ref encoder);
            encoder = Start("ffmpeg.exe", new[] { "-hide_banner", "-loglevel", "warning",
                "-f", "image2pipe", "-framerate", settings.FrameRate.ToString(), "-probesize", "32768", "-analyzeduration", "0", "-vcodec", "mjpeg", "-i", "pipe:0",
                "-vf", "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1",
                "-c:v", "libx264", "-profile:v", "main", "-preset", "veryfast", "-tune", "zerolatency", "-pix_fmt", "yuv420p",
                "-b:v", $"{settings.BitrateKbps}k", "-minrate", $"{settings.BitrateKbps}k", "-maxrate", $"{settings.BitrateKbps}k",
                "-bufsize", $"{settings.BitrateKbps*2}k", "-x264-params", "nal-hrd=cbr:force-cfr=1",
                "-r", settings.FrameRate.ToString(), "-g", (settings.FrameRate*2).ToString(), "-keyint_min", (settings.FrameRate*2).ToString(), "-sc_threshold", "0", "-an", "-f", "rtsp", "-rtsp_transport", "tcp", $"rtsp://127.0.0.1:{port}/desktop" }, true);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { await encoder.StandardInput.BaseStream.WriteAsync(jpeg, timeout.Token);
            await encoder.StandardInput.BaseStream.FlushAsync(timeout.Token); }
        catch { Stop(ref encoder); throw; }
    }
    static void Stop(ref Process? p)
    {
        if (p is null) return;
        try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { }
        p.Dispose(); p=null;
    }
    public void Dispose() { Stop(ref encoder); Stop(ref server); children.Dispose(); }
}
