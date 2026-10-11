using System.Diagnostics;
using System.Text.Json;
using Dart;
if(args.Contains("--child")) { await Task.Delay(600000); return; }
Process Child() => Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { ArgumentList={"--child"},UseShellExecute=false,CreateNoWindow=true })!;
if(args.Contains("--parent")) {
 using var ownership=new ChildProcessJob(); using var nested=Child(); ownership.Add(nested);
 Console.WriteLine(nested.Id); Console.Out.Flush(); await Task.Delay(600000); return;
}
using(var child=Child()) {
 using(var job=new ChildProcessJob()) job.Add(child);
 if(!child.WaitForExit(5000)) throw new Exception("Closing job did not terminate child");
 Console.WriteLine("PASS: job disposal terminates child");
}
using(var parent=Process.Start(new ProcessStartInfo(Environment.ProcessPath!) {ArgumentList={"--parent"},UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true})!) {
 var pid=int.Parse((await parent.StandardOutput.ReadLineAsync())!);
 using var child=Process.GetProcessById(pid);
 parent.Kill(); parent.WaitForExit();
 if(!child.WaitForExit(5000)) throw new Exception("Parent crash left child running");
 Console.WriteLine("PASS: forced parent termination also terminates child");
}
var data=Path.Combine(AppContext.BaseDirectory,"camera-test"); Directory.CreateDirectory(data);
var policy=Path.Combine(AppContext.BaseDirectory,"installation.json");
try {
 File.WriteAllText(policy,"{\"AllowLan\":false}");
 var first=CameraSettings.Load(data,1);
 var password=first.Password; var id=first.DeviceId; var port=first.OnvifPort;
 File.WriteAllText(policy,"{\"AllowLan\":true}");
 var lan=CameraSettings.Load(data,1);
 if(lan.Password!=password || lan.DeviceId!=id || lan.OnvifPort!=port || lan.BindAddress!="0.0.0.0" || !lan.Discovery) throw new Exception("LAN install policy changed identity or failed");
 Console.WriteLine("PASS: LAN installation preserves credentials/device ID/ports and enables listening/discovery");
 File.WriteAllText(policy,"{\"AllowLan\":false}");
 var local=CameraSettings.Load(data,1);
 if(local.BindAddress!="0.0.0.0" || !local.Discovery) throw new Exception("Existing user configuration was overwritten by runtime policy");
 Console.WriteLine("PASS: legacy localhost policy cannot disable LAN listening");
} finally { File.Delete(policy); }
