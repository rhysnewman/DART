using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dart;
internal sealed class OnvifHost : IAsyncDisposable
{
 const string Soap="http://www.w3.org/2003/05/soap-envelope", Device="http://www.onvif.org/ver10/device/wsdl", Media="http://www.onvif.org/ver10/media/wsdl", Schema="http://www.onvif.org/ver10/schema";
 readonly CameraSettings c;
 readonly Func<byte[]?> snapshot;
 readonly Action<string> log;
 readonly Dictionary<string,DateTimeOffset> nonces=new();
 readonly CancellationTokenSource shutdown=new();
 WebApplication? app;
 UdpClient? discovery;
 Task? discoveryTask;
 public OnvifHost(CameraSettings settings,Func<byte[]?> snapshot,Action<string> log) { c=settings; this.snapshot=snapshot; this.log=log; }
 static string E(string text) => System.Security.SecurityElement.Escape(text)!;
 public async Task Start(CancellationToken token)
 {
  var b=WebApplication.CreateSlimBuilder();
  b.Logging.ClearProviders();
  b.WebHost.ConfigureKestrel(k=> { k.Listen(IPAddress.Parse(c.BindAddress),c.OnvifPort); k.Limits.MaxRequestBodySize=65536; k.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10); });
  app=b.Build();
  app.MapPost("/onvif/{service}", Handle);
  app.MapGet("/snapshot.jpg",async context=> {
   if(!Basic(context)) { Challenge(context); return; }
   var bytes=snapshot();
   if(bytes is null) { context.Response.StatusCode=503; return; }
   context.Response.ContentType="image/jpeg"; context.Response.Headers.CacheControl="no-store";
   await context.Response.Body.WriteAsync(bytes,context.RequestAborted);
  });
  await app.StartAsync(token);
  log("ONVIF device endpoint: "+c.BaseUrl+"/onvif/device_service");
  if(c.Discovery) {
   try {
    discovery=new UdpClient(AddressFamily.InterNetwork);
    discovery.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);
    discovery.Client.Bind(new IPEndPoint(IPAddress.Parse(c.BindAddress),3702));
    discovery.JoinMulticastGroup(IPAddress.Parse("239.255.255.250"));
    discoveryTask=Discover(shutdown.Token);
   } catch(SocketException e) { discovery?.Dispose(); discovery=null; log("WS-Discovery unavailable; use endpoint manually: "+e.Message); }
  }
 }
 static XDocument Parse(string text) {
  using var r=XmlReader.Create(new StringReader(text),new XmlReaderSettings{ DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536 });
  return XDocument.Load(r);
 }
 bool Basic(HttpContext context) {
  var header=context.Request.Headers.Authorization.ToString();
  if(!header.StartsWith("Basic ",StringComparison.OrdinalIgnoreCase)) return false;
  try { return Equal(Convert.FromBase64String(header[6..]),Encoding.UTF8.GetBytes(c.Username+":"+c.Password)); } catch(FormatException) { return false; }
 }
 static bool Equal(byte[] a,byte[] b) => CryptographicOperations.FixedTimeEquals(a,b);
 bool Authenticate(HttpContext context,XDocument doc) {
  if(Basic(context)) return true;
  XNamespace ws="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
  XNamespace wu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
  var t=doc.Root?.Element(XName.Get("Header",Soap))?.Descendants(ws+"UsernameToken").SingleOrDefault();
  if(t is null || (string?)t.Element(ws+"Username")!=c.Username) return false;
  var created=(string?)t.Element(wu+"Created"); var nonce=(string?)t.Element(ws+"Nonce"); var password=t.Element(ws+"Password");
  if(password?.Attribute("Type")?.Value.EndsWith("#PasswordDigest")!=true || nonce is null || created is null ||
   !DateTimeOffset.TryParse(created,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var timestamp) ||
   Math.Abs((DateTimeOffset.UtcNow-timestamp).TotalMinutes)>5) return false;
  try {
   var digest=SHA1.HashData(Convert.FromBase64String(nonce).Concat(Encoding.UTF8.GetBytes(created+c.Password)).ToArray());
   if(!Equal(digest,Convert.FromBase64String(password.Value))) return false;
   lock(nonces) {
    foreach(var key in nonces.Where(p=>(DateTimeOffset.UtcNow-p.Value).TotalMinutes>10).Select(p=>p.Key).ToArray()) nonces.Remove(key);
    if(nonces.ContainsKey(nonce)) return false;
    if(nonces.Count>=10000) return false;
    nonces[nonce]=DateTimeOffset.UtcNow;
   }
   return true;
  } catch(FormatException) { return false; }
 }
 static void Challenge(HttpContext context) { context.Response.StatusCode=401; context.Response.Headers.WWWAuthenticate="Basic realm=\"DART\", charset=\"UTF-8\""; }
 async Task Handle(HttpContext context) {
  try {
   using var reader=new StreamReader(context.Request.Body,Encoding.UTF8);
   var doc=Parse(await reader.ReadToEndAsync(context.RequestAborted));
   var body=doc.Root?.Element(XName.Get("Body",Soap));
   var op=body?.Elements().SingleOrDefault();
   if(op is null) { await Fault(context,"ter:InvalidArgVal","SOAP 1.2 Body requires one operation"); return; }
   // ONVIF clients need the unauthenticated clock before computing UsernameToken digests.
   if(op.Name!=XName.Get("GetSystemDateAndTime",Device) && !Authenticate(context,doc)) { await Fault(context,"ter:NotAuthorized","Valid credentials required"); return; }
   var service=context.Request.RouteValues["service"]?.ToString();
   if((op.Name.NamespaceName==Device && service!="device_service") || (op.Name.NamespaceName==Media && service!="media_service")) { await Fault(context,"ter:ActionNotSupported","Wrong service"); return; }
   var tokenNames=new[]{"ProfileToken","ConfigurationToken"};
   foreach(var value in op.Elements().Where(x=>tokenNames.Contains(x.Name.LocalName))) {
    var expected=value.Name.LocalName=="ProfileToken" ? "profile1" : op.Name.LocalName.Contains("VideoSource") ? "sourceconfig1" : "encoder1";
    if(value.Value!=expected) { await Fault(context,"ter:InvalidArgVal","Unknown token"); return; }
   }
   var response=Response(op);
   if(response is null) { await Fault(context,"ter:ActionNotSupported","Operation not supported"); return; }
      var action=op.Name.NamespaceName+"/"+op.Name.LocalName+"Response";
   context.Response.ContentType=$"application/soap+xml; charset=utf-8; action=\"{action}\"";
   context.Response.Headers.CacheControl="no-store";
   var addressHeader=doc.Root?.Element(XName.Get("Header",Soap))?.Elements().FirstOrDefault(x=>x.Name.LocalName=="MessageID");
   var envelope=Envelope(response);
   if(addressHeader is not null && addressHeader.Name.NamespaceName is "http://www.w3.org/2005/08/addressing" or "http://schemas.xmlsoap.org/ws/2004/08/addressing") {
    var header=$"<s:Header xmlns:a='{addressHeader.Name.NamespaceName}'><a:Action>{E(action)}</a:Action><a:RelatesTo>{E(addressHeader.Value)}</a:RelatesTo></s:Header>";
    envelope=envelope.Replace("<s:Body>",header+"<s:Body>");
   }
   await context.Response.WriteAsync(envelope,context.RequestAborted);
  } catch(Exception e) when(e is XmlException or InvalidOperationException or FormatException) { await Fault(context,"ter:InvalidArgVal","Malformed request"); }
 }
 string? Response(XElement op) {
  var n=op.Name.LocalName;
  if(op.Name.NamespaceName==Device) return n switch {
   "GetDeviceInformation" => "<tds:GetDeviceInformationResponse><tds:Manufacturer>DART</tds:Manufacturer><tds:Model>Desktop Camera</tds:Model><tds:FirmwareVersion>0.2</tds:FirmwareVersion><tds:SerialNumber>"+E(c.DeviceId)+"</tds:SerialNumber><tds:HardwareId>Windows</tds:HardwareId></tds:GetDeviceInformationResponse>",
   "GetSystemDateAndTime" => Clock(),
   "GetServices" => $"<tds:GetServicesResponse>{Service(Device,"device_service")}{Service(Media,"media_service")}</tds:GetServicesResponse>",
   "GetCapabilities" => $"<tds:GetCapabilitiesResponse><tds:Capabilities><tt:Device><tt:XAddr>{E(c.BaseUrl)}/onvif/device_service</tt:XAddr><tt:Network><tt:IPFilter>false</tt:IPFilter><tt:ZeroConfiguration>false</tt:ZeroConfiguration><tt:IPVersion6>false</tt:IPVersion6><tt:DynDNS>false</tt:DynDNS></tt:Network><tt:System><tt:DiscoveryResolve>false</tt:DiscoveryResolve><tt:DiscoveryBye>false</tt:DiscoveryBye><tt:RemoteDiscovery>false</tt:RemoteDiscovery><tt:SystemBackup>false</tt:SystemBackup><tt:SystemLogging>false</tt:SystemLogging><tt:FirmwareUpgrade>false</tt:FirmwareUpgrade><tt:SupportedVersions><tt:Major>2</tt:Major><tt:Minor>0</tt:Minor></tt:SupportedVersions></tt:System><tt:IO/><tt:Security><tt:TLS1.1>false</tt:TLS1.1><tt:TLS1.2>false</tt:TLS1.2><tt:OnboardKeyGeneration>false</tt:OnboardKeyGeneration><tt:AccessPolicyConfig>false</tt:AccessPolicyConfig><tt:X.509Token>false</tt:X.509Token><tt:SAMLToken>false</tt:SAMLToken><tt:KerberosToken>false</tt:KerberosToken><tt:RELToken>false</tt:RELToken></tt:Security></tt:Device><tt:Media><tt:XAddr>{E(c.BaseUrl)}/onvif/media_service</tt:XAddr><tt:StreamingCapabilities><tt:RTPMulticast>false</tt:RTPMulticast><tt:RTP_TCP>false</tt:RTP_TCP><tt:RTP_RTSP_TCP>true</tt:RTP_RTSP_TCP></tt:StreamingCapabilities></tt:Media></tds:Capabilities></tds:GetCapabilitiesResponse>",
   "GetServiceCapabilities" => "<tds:GetServiceCapabilitiesResponse><tds:Capabilities><tds:Network IPFilter='false' ZeroConfiguration='false' IPVersion6='false' DynDNS='false'/><tds:Security TLS1.0='false' TLS1.1='false' TLS1.2='false' UsernameToken='true' HttpDigest='false'/><tds:System DiscoveryResolve='false' DiscoveryBye='false' RemoteDiscovery='false' SystemBackup='false' SystemLogging='false' FirmwareUpgrade='false'/></tds:Capabilities></tds:GetServiceCapabilitiesResponse>",
   "GetScopes" => "<tds:GetScopesResponse><tds:Scopes><tt:ScopeDef>Fixed</tt:ScopeDef><tt:ScopeItem>onvif://www.onvif.org/name/DART</tt:ScopeItem></tds:Scopes><tds:Scopes><tt:ScopeDef>Fixed</tt:ScopeDef><tt:ScopeItem>onvif://www.onvif.org/type/video_encoder</tt:ScopeItem></tds:Scopes></tds:GetScopesResponse>",
   "GetHostname" => "<tds:GetHostnameResponse><tds:HostnameInformation><tt:FromDHCP>false</tt:FromDHCP><tt:Name>DART</tt:Name></tds:HostnameInformation></tds:GetHostnameResponse>",
   "GetDiscoveryMode" => $"<tds:GetDiscoveryModeResponse><tds:DiscoveryMode>{(c.Discovery?"Discoverable":"NonDiscoverable")}</tds:DiscoveryMode></tds:GetDiscoveryModeResponse>",
   _ => null
  };
  if(op.Name.NamespaceName!=Media) return null;
  return n switch {
   "GetProfiles" => $"<trt:GetProfilesResponse>{Profile("Profiles")}</trt:GetProfilesResponse>",
   "GetProfile" => $"<trt:GetProfileResponse>{Profile("Profile")}</trt:GetProfileResponse>",
   "GetVideoSources" => $"<trt:GetVideoSourcesResponse><trt:VideoSources token='source1'><tt:Framerate>{c.FrameRate}</tt:Framerate><tt:Resolution><tt:Width>1920</tt:Width><tt:Height>1080</tt:Height></tt:Resolution></trt:VideoSources></trt:GetVideoSourcesResponse>",
   "GetVideoSourceConfigurations" => $"<trt:GetVideoSourceConfigurationsResponse>{Source("trt:Configurations")}</trt:GetVideoSourceConfigurationsResponse>",
   "GetVideoSourceConfiguration" => $"<trt:GetVideoSourceConfigurationResponse>{Source("trt:Configuration")}</trt:GetVideoSourceConfigurationResponse>",
   "GetCompatibleVideoEncoderConfigurations" => $"<trt:GetCompatibleVideoEncoderConfigurationsResponse>{Encoder("trt:Configurations")}</trt:GetCompatibleVideoEncoderConfigurationsResponse>",
   "GetCompatibleVideoSourceConfigurations" => $"<trt:GetCompatibleVideoSourceConfigurationsResponse>{Source("trt:Configurations")}</trt:GetCompatibleVideoSourceConfigurationsResponse>",
   "GetVideoEncoderConfigurations" => $"<trt:GetVideoEncoderConfigurationsResponse>{Encoder("trt:Configurations")}</trt:GetVideoEncoderConfigurationsResponse>",
   "GetVideoEncoderConfiguration" => $"<trt:GetVideoEncoderConfigurationResponse>{Encoder("trt:Configuration")}</trt:GetVideoEncoderConfigurationResponse>",
   "GetStreamUri" => StreamUri(op),
   "GetSnapshotUri" => $"<trt:GetSnapshotUriResponse><trt:MediaUri><tt:Uri>{E(c.BaseUrl)}/snapshot.jpg</tt:Uri><tt:InvalidAfterConnect>false</tt:InvalidAfterConnect><tt:InvalidAfterReboot>false</tt:InvalidAfterReboot><tt:Timeout>PT0S</tt:Timeout></trt:MediaUri></trt:GetSnapshotUriResponse>",
   "GetServiceCapabilities" => "<trt:GetServiceCapabilitiesResponse><trt:Capabilities SnapshotUri='true'><trt:ProfileCapabilities MaximumNumberOfProfiles='1'/><trt:StreamingCapabilities RTPMulticast='false' RTP_TCP='false' RTP_RTSP_TCP='true' NonAggregateControl='false'/></trt:Capabilities></trt:GetServiceCapabilitiesResponse>",
   "GetVideoEncoderConfigurationOptions" => $"<trt:GetVideoEncoderConfigurationOptionsResponse><trt:Options><tt:QualityRange><tt:Min>1</tt:Min><tt:Max>6</tt:Max></tt:QualityRange><tt:H264><tt:ResolutionsAvailable><tt:Width>1920</tt:Width><tt:Height>1080</tt:Height></tt:ResolutionsAvailable><tt:GovLengthRange><tt:Min>{c.FrameRate*2}</tt:Min><tt:Max>{c.FrameRate*2}</tt:Max></tt:GovLengthRange><tt:FrameRateRange><tt:Min>{c.FrameRate}</tt:Min><tt:Max>{c.FrameRate}</tt:Max></tt:FrameRateRange><tt:EncodingIntervalRange><tt:Min>1</tt:Min><tt:Max>1</tt:Max></tt:EncodingIntervalRange><tt:H264ProfilesSupported>Main</tt:H264ProfilesSupported></tt:H264></trt:Options></trt:GetVideoEncoderConfigurationOptionsResponse>",
   _ => null
  };
 }
 string StreamUri(XElement op) {
  var protocol=op.Descendants(XName.Get("Protocol",Schema)).FirstOrDefault()?.Value;
  if(protocol is not null && protocol!="RTSP") throw new InvalidOperationException("Only RTSP-over-TCP supported");
  return $"<trt:GetStreamUriResponse><trt:MediaUri><tt:Uri>{E(c.RtspUrl)}</tt:Uri><tt:InvalidAfterConnect>false</tt:InvalidAfterConnect><tt:InvalidAfterReboot>false</tt:InvalidAfterReboot><tt:Timeout>PT0S</tt:Timeout></trt:MediaUri></trt:GetStreamUriResponse>";
 }
 string Service(string ns,string path) => $"<tds:Service><tds:Namespace>{ns}</tds:Namespace><tds:XAddr>{E(c.BaseUrl)}/onvif/{path}</tds:XAddr><tds:Version><tt:Major>2</tt:Major><tt:Minor>0</tt:Minor></tds:Version></tds:Service>";
 static string Clock() {
  var d=DateTime.UtcNow;
  return $"<tds:GetSystemDateAndTimeResponse><tds:SystemDateAndTime><tt:DateTimeType>Manual</tt:DateTimeType><tt:DaylightSavings>false</tt:DaylightSavings><tt:TimeZone><tt:TZ>UTC0</tt:TZ></tt:TimeZone><tt:UTCDateTime><tt:Time><tt:Hour>{d.Hour}</tt:Hour><tt:Minute>{d.Minute}</tt:Minute><tt:Second>{d.Second}</tt:Second></tt:Time><tt:Date><tt:Year>{d.Year}</tt:Year><tt:Month>{d.Month}</tt:Month><tt:Day>{d.Day}</tt:Day></tt:Date></tt:UTCDateTime></tds:SystemDateAndTime></tds:GetSystemDateAndTimeResponse>";
 }
 string Source(string tag) => $"<{tag} token='sourceconfig1'><tt:Name>Desktop</tt:Name><tt:UseCount>1</tt:UseCount><tt:SourceToken>source1</tt:SourceToken><tt:Bounds x='0' y='0' width='1920' height='1080'/></{tag}>";
 string Encoder(string tag) => $"<{tag} token='encoder1'><tt:Name>H264</tt:Name><tt:UseCount>1</tt:UseCount><tt:Encoding>H264</tt:Encoding><tt:Resolution><tt:Width>1920</tt:Width><tt:Height>1080</tt:Height></tt:Resolution><tt:Quality>4</tt:Quality><tt:RateControl><tt:FrameRateLimit>{c.FrameRate}</tt:FrameRateLimit><tt:EncodingInterval>1</tt:EncodingInterval><tt:BitrateLimit>{c.BitrateKbps}</tt:BitrateLimit></tt:RateControl><tt:H264><tt:GovLength>{c.FrameRate*2}</tt:GovLength><tt:H264Profile>Main</tt:H264Profile></tt:H264><tt:Multicast><tt:Address><tt:Type>IPv4</tt:Type><tt:IPv4Address>0.0.0.0</tt:IPv4Address></tt:Address><tt:Port>0</tt:Port><tt:TTL>1</tt:TTL><tt:AutoStart>false</tt:AutoStart></tt:Multicast><tt:SessionTimeout>PT60S</tt:SessionTimeout></{tag}>";
 string Profile(string tag) => $"<trt:{tag} token='profile1' fixed='true'><tt:Name>DART Desktop</tt:Name>{Source("tt:VideoSourceConfiguration")}{Encoder("tt:VideoEncoderConfiguration")}</trt:{tag}>";
 static string Envelope(string body) => $"<s:Envelope xmlns:s='{Soap}' xmlns:tds='{Device}' xmlns:trt='{Media}' xmlns:tt='{Schema}' xmlns:ter='http://www.onvif.org/ver10/error'><s:Body>{body}</s:Body></s:Envelope>";
 static async Task Fault(HttpContext context,string code,string message) {
  context.Response.StatusCode=500;
  context.Response.ContentType="application/soap+xml; charset=utf-8";
  await context.Response.WriteAsync(Envelope($"<s:Fault><s:Code><s:Value>s:Sender</s:Value><s:Subcode><s:Value>{code}</s:Value></s:Subcode></s:Code><s:Reason><s:Text xml:lang='en'>{E(message)}</s:Text></s:Reason></s:Fault>"));
 }
 async Task Discover(CancellationToken token) {
  const string w="http://schemas.xmlsoap.org/ws/2005/04/discovery", a="http://schemas.xmlsoap.org/ws/2004/08/addressing";
  try {
   while(!token.IsCancellationRequested) {
    var packet=await discovery!.ReceiveAsync(token);
    if(packet.Buffer.Length>32768) continue;
    try {
     var doc=Parse(Encoding.UTF8.GetString(packet.Buffer));
     var probe=doc.Root?.Element(XName.Get("Body",Soap))?.Element(XName.Get("Probe",w));
     if(probe is null) continue;
     // Match requested QName types, and URI-prefix scopes; don't reply to unrelated device searches.
     var types=probe.Element(XName.Get("Types",w));
     if(types is not null && types.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries).Any(type=> {
      var parts=type.Split(':'); var ns=parts.Length==2?types.GetNamespaceOfPrefix(parts[0]):types.GetDefaultNamespace();
      return parts[^1]!="NetworkVideoTransmitter" || ns?.NamespaceName!="http://www.onvif.org/ver10/network/wsdl";
     })) continue;
     var scopes="onvif://www.onvif.org/name/DART onvif://www.onvif.org/type/video_encoder";
     var requested=probe.Element(XName.Get("Scopes",w));
     if(requested is not null && requested.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries).Any(s=>!scopes.Split(' ').Any(v=>v==s || v.StartsWith(s.TrimEnd('/')+"/",StringComparison.Ordinal)))) continue;
     var message=doc.Descendants(XName.Get("MessageID",a)).FirstOrDefault()?.Value;
     if(message is null) continue;
     var response=$"<s:Envelope xmlns:s='{Soap}' xmlns:a='{a}' xmlns:d='{w}' xmlns:dn='http://www.onvif.org/ver10/network/wsdl'><s:Header><a:Action>{w}/ProbeMatches</a:Action><a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID><a:RelatesTo>{E(message)}</a:RelatesTo><a:To>{a}/role/anonymous</a:To></s:Header><s:Body><d:ProbeMatches><d:ProbeMatch><a:EndpointReference><a:Address>urn:uuid:{E(c.DeviceId)}</a:Address></a:EndpointReference><d:Types>dn:NetworkVideoTransmitter</d:Types><d:Scopes>{scopes}</d:Scopes><d:XAddrs>{E(c.BaseUrl)}/onvif/device_service</d:XAddrs><d:MetadataVersion>1</d:MetadataVersion></d:ProbeMatch></d:ProbeMatches></s:Body></s:Envelope>";
     var bytes=Encoding.UTF8.GetBytes(response);
     await discovery.SendAsync(bytes,packet.RemoteEndPoint,token);
    } catch(Exception e) when(e is XmlException or InvalidOperationException) { }
   }
  } catch(OperationCanceledException) when(token.IsCancellationRequested) { }
  catch(SocketException e) { log("Discovery stopped: "+e.Message); }
 }
 public async ValueTask DisposeAsync() {
  shutdown.Cancel();
  if(discoveryTask is not null) await discoveryTask;
  discovery?.Dispose();
  if(app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
  shutdown.Dispose();
 }
}
