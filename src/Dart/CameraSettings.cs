using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
namespace Dart;
internal sealed class CameraSettings
{
 public string BindAddress {get;set;} = "127.0.0.1";
 public string AdvertisedHost {get;set;} = "127.0.0.1";
 public int OnvifPort {get;set;} = 18080;
 public int RtspPort {get;set;} = 18554;
 public int FrameRate {get;set;} = 5;
 public int BitrateKbps {get;set;} = 1000;
 public string Username {get;set;} = "dart";
 public string Password {get;set;} = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
 public bool Discovery {get;set;}
 public string DeviceId {get;set;} = Guid.NewGuid().ToString();
 public string BaseUrl => $"http://{AdvertisedHost}:{OnvifPort}";
 public string RtspUrl => $"rtsp://{AdvertisedHost}:{RtspPort}/desktop";
 public static CameraSettings Load(string data, int session)
 {
  var path=Path.Combine(data,"camera.json");
  var settings=File.Exists(path) ? JsonSerializer.Deserialize<CameraSettings>(File.ReadAllText(path))! : new CameraSettings { OnvifPort=18080+session, RtspPort=18554+session };
  // LAN listening is always enabled, including upgrades from localhost-only installs.
  settings.BindAddress="0.0.0.0";
  settings.Discovery=true;
  if(settings.AdvertisedHost=="127.0.0.1" || settings.AdvertisedHost=="localhost") {
   var address=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
    .Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up && n.NetworkInterfaceType!=System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
    .OrderByDescending(n=>n.GetIPProperties().GatewayAddresses.Count>0)
    .SelectMany(n=>n.GetIPProperties().UnicastAddresses)
    .FirstOrDefault(a=>a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."));
   if(address is not null) settings.AdvertisedHost=address.Address.ToString();
  }
  if (!IPAddress.TryParse(settings.BindAddress,out var bind) || bind.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || (settings.BindAddress != "0.0.0.0" && settings.BindAddress != "127.0.0.1") ||
      Uri.CheckHostName(settings.AdvertisedHost)==UriHostNameType.Unknown || settings.AdvertisedHost.Contains(':') ||
      settings.OnvifPort is <1024 or >65535 || settings.RtspPort is <1024 or >65535 || settings.OnvifPort==settings.RtspPort ||
      settings.FrameRate is <1 or >30 || settings.BitrateKbps is <100 or >20000 || string.IsNullOrWhiteSpace(settings.Username) || settings.Password.Length<12 || !Guid.TryParse(settings.DeviceId,out _))
   throw new InvalidDataException("Invalid camera.json: IPv4 bind, valid host/ports, fps 1..30, bitrate 100..20000 and password >=12 characters required.");
  if(!File.Exists(path)) File.WriteAllText(path,JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
  return settings;
 }
}
