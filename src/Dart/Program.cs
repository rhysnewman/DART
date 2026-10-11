using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Dart;
internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var single = new Mutex(true, "Local\\DART.Capture", out var created);
        if (!created) return;
        Application.Run(new CaptureContext(args));
    }
}
internal sealed class CaptureContext : ApplicationContext
{
    readonly CancellationTokenSource stop = new();
    readonly Control dispatcher = new();
    readonly NotifyIcon tray;
    readonly Desktop.WinEvent callback;
    readonly List<IntPtr> hooks = new();
    readonly object gate = new(), logGate = new();
    readonly string data;
    readonly Task worker;
    CameraSettings? activeSettings;
    ConnectionDetailsForm? details;
    long pendingSince = Environment.TickCount64, lastCapture;
    string reason = "startup";
    volatile bool locked;
    readonly System.Windows.Forms.Timer statusTimer = new() { Interval = 1000 };
    volatile string status = "Starting";
    public CaptureContext(string[] args)
    {
        var session = Process.GetCurrentProcess().SessionId;
        data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DART", $"session-{session}");
        var dataOption = Array.IndexOf(args, "--data-dir");
        if (dataOption >= 0 && dataOption + 1 < args.Length) data = Path.GetFullPath(args[dataOption + 1]);
        Directory.CreateDirectory(data);
        _ = dispatcher.Handle;
        tray = new NotifyIcon { Icon=SystemIcons.Information, Visible=true, Text="DART: starting",
            ContextMenuStrip=new ContextMenuStrip() };
        tray.ContextMenuStrip.Items.Add("Camera connection details…", null, (_, _) => ShowConnectionDetails());
        tray.DoubleClick += (_, _) => ShowConnectionDetails();
        tray.ContextMenuStrip.Items.Add("Capture now", null, (_, _) => Trigger("manual"));
        tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        tray.ContextMenuStrip.Items.Add("Open debug folder", null, (_, _) => Process.Start(new ProcessStartInfo(data) { UseShellExecute=true }));
        tray.ContextMenuStrip.Items.Add("Exit DART", null, (_, _) => ExitThread());
        callback = (_, evt, hwnd, obj, child, _, _) => {
            if (hwnd != IntPtr.Zero && obj == 0 && child == 0)
                Trigger(evt == 3 ? "foreground" : "move-size-end");
        };
        foreach (uint evt in new uint[] { 3, 11 })
        {
            var hook = Desktop.SetWinEventHook(evt, evt, IntPtr.Zero, callback, 0, 0, 2); // OUTOFCONTEXT | SKIPOWNPROCESS
            if (hook == IntPtr.Zero) Log("Could not register WinEvent hook " + evt);
            else hooks.Add(hook);
        }
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.SessionEnding += SessionEnding;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        statusTimer.Tick += (_, _) => tray.Text = ("DART: " + status)[..Math.Min(63, ("DART: " + status).Length)];
        statusTimer.Start();
        worker = Task.Run(async () => { try { await Run(session); } catch(Exception e) { Log("Startup failed: "+e.Message); status="Startup failed; see log"; } });
        var testOption = Array.IndexOf(args, "--test-seconds");
        if (testOption >= 0 && testOption + 1 < args.Length && int.TryParse(args[testOption + 1], out var seconds) && seconds > 0)
        {
            var deadline = Environment.TickCount64 + seconds * 1000L;
            statusTimer.Tick += (_, _) => { if (Environment.TickCount64 >= deadline) ExitThread(); };
        }
    }
    void ShowConnectionDetails()
    {
        if (details is null || details.IsDisposed)
            details = new ConnectionDetailsForm(() => Volatile.Read(ref activeSettings), () => status);
        details.Show();
        if (details.WindowState == FormWindowState.Minimized) details.WindowState = FormWindowState.Normal;
        details.Activate();
    }
    void Trigger(string why)
    {
        lock (gate) { if (pendingSince == 0) pendingSince=Environment.TickCount64; reason=why; }
    }
    void SessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect) locked=true;
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.RemoteConnect or SessionSwitchReason.ConsoleConnect) { locked=false; Trigger("session-available"); }
    }
    void SessionEnding(object? sender, SessionEndingEventArgs e)
    {
        stop.Cancel();
        if (!dispatcher.IsDisposed) dispatcher.BeginInvoke((Action)ExitThread);
    }
    void DisplayChanged(object? sender, EventArgs e) => Trigger("display-change");
    void Log(string text)
    {
        lock(logGate)
        {
            try {
                var path=Path.Combine(data,"dart.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path+".old", true);
                File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} {text}{Environment.NewLine}");
            } catch (IOException) { }
        }
    }
    async Task Run(int session)
    {
        var settings=CameraSettings.Load(data,session);
        Volatile.Write(ref activeSettings, settings);
        using var host = new StreamHost(AppContext.BaseDirectory, data, settings, Log);
        byte[]? current = null;
        await using var onvif = new OnvifHost(settings, () => Volatile.Read(ref current), Log);
        await onvif.Start(stop.Token);
        var unavailable = false;
        long sequence=Directory.GetFiles(data, "*.jpg")
            .Select(path => long.TryParse(Path.GetFileNameWithoutExtension(path), out var n) ? n : 0L).DefaultIfEmpty().Max();
                long nextPeriodicCapture=Environment.TickCount64+10000;
        var streamTask=Task.Run(async () => {
            using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1.0/settings.FrameRate));
            while(await timer.WaitForNextTickAsync(stop.Token)) {
                var frame=Volatile.Read(ref current);
                if(frame is null) continue;
                try { await host.Send(frame,stop.Token); }
                catch(OperationCanceledException) when(stop.IsCancellationRequested) { break; }
                catch(Exception e) { Log("Streaming: "+e.Message); status="Streaming unavailable; see log"; await Task.Delay(5000,stop.Token); }
            }
        });
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var now=Environment.TickCount64;
                if (locked || !Desktop.Available())
                {
                    if (!unavailable || current is null) current=Placeholder("Desktop unavailable / session locked");
                    unavailable=true; status="Desktop unavailable";
                }
                else
                {
                    if (unavailable) { Trigger("desktop-returned"); unavailable=false; }
                    string? captureReason=null;
                    lock(gate)
                    {
                        if ((pendingSince != 0 && now-pendingSince >= 750 && now-lastCapture >= 3000) || now >= nextPeriodicCapture)
                        {
                            var periodicDue=now >= nextPeriodicCapture;
                            captureReason=periodicDue ? "periodic-10s" : reason;
                            if(periodicDue) nextPeriodicCapture=now+10000;
                            pendingSince=0;
                        }
                    }
                    if(captureReason is not null)
                    {
                        try
                        {
                            var bounds=SystemInformation.VirtualScreen;
                            var before=DateTimeOffset.UtcNow;
                            var windows=Desktop.Windows();
                            using var bitmap=new Bitmap(bounds.Width, bounds.Height);
                            using(var g=Graphics.FromImage(bitmap)) g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                            var id=$"{++sequence:D8}";
                            var metadata=JsonSerializer.Serialize(new { sequence, capturedAtUtc=before, captureEndedAtUtc=DateTimeOffset.UtcNow,
                                sessionId=session, trigger=captureReason, coordinateSpace="physical screen pixels",
                                desktop=new { x=bounds.X,y=bounds.Y,width=bounds.Width,height=bounds.Height }, windows }, new JsonSerializerOptions { WriteIndented=true });
                            using var buffer=new MemoryStream(); bitmap.Save(buffer, ImageFormat.Jpeg);
                            current=JpegMetadata.Embed(buffer.ToArray(), metadata);
                            PublishDebug(id, current, metadata);
                            lastCapture=now; status=$"Capturing; stream {settings.AdvertisedHost}:{settings.RtspPort}/desktop";
                        }
                        catch(Exception e) { Log("Capture: "+e.Message); current=Placeholder("Capture failed"); status="Capture failed; see log"; lastCapture=now; }
                    }
                }
                await Task.Delay(100,stop.Token);
            }
        }
        catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
        catch(Exception e) { Log("Worker stopped: "+e); status="Stopped; see log"; }
        finally { stop.Cancel(); try { await streamTask; } catch(OperationCanceledException) { } }
    }
    void PublishDebug(string id, byte[] jpeg, string json)
    {
        // Each immutable pair is published through latest.json only after both writes finish.
        File.WriteAllBytes(Path.Combine(data,id+".jpg"),jpeg);
        File.WriteAllText(Path.Combine(data,id+".json"),json);
        var temp=Path.Combine(data,"latest.tmp");
        File.WriteAllText(temp,JsonSerializer.Serialize(new { image=id+".jpg", metadata=id+".json" }));
        File.Move(temp,Path.Combine(data,"latest.json"),true);
        var images=Directory.GetFiles(data,"????????.jpg").OrderDescending().Skip(20).ToArray();
        foreach(var image in images) { File.Delete(image); File.Delete(Path.ChangeExtension(image,".json")); }
    }
    static byte[] Placeholder(string message)
    {
        using var b=new Bitmap(1920,1080); using var g=Graphics.FromImage(b);
        g.Clear(Color.Black); using var font=new Font("Segoe UI",32);
        g.DrawString(message,font,Brushes.White,40,40);
        using var m=new MemoryStream(); b.Save(m,ImageFormat.Jpeg); return m.ToArray();
    }
    protected override void ExitThreadCore()
    {
        details?.Close(); details?.Dispose();
        statusTimer.Stop(); statusTimer.Dispose();
        foreach(var hook in hooks) Desktop.UnhookWinEvent(hook);
        SystemEvents.SessionEnding-=SessionEnding;
        SystemEvents.SessionSwitch-=SessionSwitch; SystemEvents.DisplaySettingsChanged-=DisplayChanged;
        stop.Cancel();
        try { worker.GetAwaiter().GetResult(); } catch(OperationCanceledException) { }
        tray.Visible=false; tray.Dispose(); dispatcher.Dispose(); stop.Dispose(); base.ExitThreadCore();
    }
}
internal static class JpegMetadata
{
    public static byte[] Embed(byte[] jpeg,string json)
    {
        // Custom XMP property; JSON sidecar remains authoritative if the APP1 size limit is exceeded.
        var xmp="<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:dart='urn:dart:metadata:1' dart:jsonBase64='"+
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json))+"'/></rdf:RDF></x:xmpmeta>";
        var payload=Encoding.UTF8.GetBytes("http://ns.adobe.com/xap/1.0/\0"+xmp);
        if(payload.Length > 65533) return jpeg;
        using var output=new MemoryStream(); output.Write(jpeg,0,2);
        output.WriteByte(0xff); output.WriteByte(0xe1);
        var length=payload.Length+2; output.WriteByte((byte)(length>>8)); output.WriteByte((byte)length);
        output.Write(payload); output.Write(jpeg,2,jpeg.Length-2); return output.ToArray();
    }
}
