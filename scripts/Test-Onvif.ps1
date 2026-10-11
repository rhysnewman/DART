param([string]$DataDirectory = (Join-Path $PSScriptRoot '..\test-output\onvif-run'))
$ErrorActionPreference='Stop'
$DataDirectory=[IO.Path]::GetFullPath($DataDirectory)
New-Item -ItemType Directory -Force $DataDirectory | Out-Null
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$settings=@{ BindAddress='127.0.0.1'; AdvertisedHost='127.0.0.1'; OnvifPort=28080; RtspPort=28554; FrameRate=5; BitrateKbps=1000; Username='dart'; Password=[guid]::NewGuid().ToString('N'); Discovery=$true; DeviceId=[guid]::NewGuid().ToString() }
$settings | ConvertTo-Json | Set-Content (Join-Path $DataDirectory 'camera.json')
$app=Start-Process -FilePath (Join-Path $root 'dist\Dart.exe') -ArgumentList @('--test-seconds','55','--data-dir',('"'+$DataDirectory+'"')) -WindowStyle Hidden -PassThru
$results=[Collections.Generic.List[string]]::new()
function Check($condition,$name) { if(-not $condition) { throw ('FAILED: '+$name) }; $results.Add('PASS: '+$name); Write-Host ('PASS: '+$name) }
function Soap($operation,[string]$service='device_service',[string]$header='', [string]$content='') {
 $ns=if($service -eq 'device_service') {'http://www.onvif.org/ver10/device/wsdl'} else {'http://www.onvif.org/ver10/media/wsdl'}
 return "<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope'><s:Header>$header</s:Header><s:Body><m:$operation xmlns:m='$ns'>$content</m:$operation></s:Body></s:Envelope>"
}
function SendSoap([string]$xml,[string]$service='device_service',[bool]$basic=$false) {
 $headers=@{}
 if($basic) { $headers.Authorization='Basic '+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($settings.Username+':'+$settings.Password)) }
 return Invoke-WebRequest -Uri ('http://127.0.0.1:28080/onvif/'+$service) -Method Post -ContentType 'application/soap+xml' -Body $xml -Headers $headers -SkipHttpErrorCheck -TimeoutSec 8
}
try {
 Start-Sleep -Seconds 8
 Check (-not $app.HasExited) 'Host running'
 $r=SendSoap (Soap 'GetSystemDateAndTime'); Check ($r.StatusCode -eq 200) 'Unauthenticated clock'
 $r=SendSoap (Soap 'GetProfiles' 'media_service') 'media_service'; Check ($r.StatusCode -eq 500 -and $r.Content.Contains('NotAuthorized')) 'Reject missing credentials'
 $nonce=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
 $created=[DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
 $digestBytes=[Convert]::FromBase64String($nonce)+[Text.Encoding]::UTF8.GetBytes($created+$settings.Password)
 $digest=[Convert]::ToBase64String([Security.Cryptography.SHA1]::HashData($digestBytes))
 $security="<wsse:Security xmlns:wsse='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd' xmlns:wsu='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd'><wsse:UsernameToken><wsse:Username>dart</wsse:Username><wsse:Password Type='http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest'>$digest</wsse:Password><wsse:Nonce>$nonce</wsse:Nonce><wsu:Created>$created</wsu:Created></wsse:UsernameToken></wsse:Security>"
 $request=Soap 'GetProfiles' 'media_service' $security
 $r=SendSoap $request 'media_service'; Check ($r.StatusCode -eq 200 -and $r.Content.Contains('profile1')) 'WS-Security PasswordDigest authentication and profiles'
 $r=SendSoap $request 'media_service'; Check ($r.StatusCode -eq 500 -and $r.Content.Contains('NotAuthorized')) 'Reject replayed nonce'
 foreach($operation in @('GetDeviceInformation','GetServices','GetCapabilities','GetScopes','GetServiceCapabilities')) {
  $r=SendSoap (Soap $operation) 'device_service' $true
  Check ($r.StatusCode -eq 200 -and ([xml]$r.Content).DocumentElement.LocalName -eq 'Envelope') $operation
 }
 foreach($operation in @('GetProfiles','GetVideoSources','GetVideoEncoderConfigurations','GetServiceCapabilities','GetSnapshotUri')) {
  $r=SendSoap (Soap $operation 'media_service') 'media_service' $true; Check ($r.StatusCode -eq 200) $operation
 }
 $r=SendSoap (Soap 'GetStreamUri' 'media_service' '' '<m:StreamSetup><tt:Stream xmlns:tt="http://www.onvif.org/ver10/schema">RTP-Unicast</tt:Stream><tt:Transport xmlns:tt="http://www.onvif.org/ver10/schema"><tt:Protocol>RTSP</tt:Protocol></tt:Transport></m:StreamSetup><m:ProfileToken>profile1</m:ProfileToken>') 'media_service' $true
 Check ($r.StatusCode -eq 200 -and $r.Content.Contains('rtsp://127.0.0.1:28554/desktop')) 'GetStreamUri returns reachable address'
 $r=SendSoap (Soap 'GetProfile' 'media_service' '' '<m:ProfileToken>invalid</m:ProfileToken>') 'media_service' $true
 Check ($r.StatusCode -eq 500 -and $r.Content.Contains('InvalidArgVal')) 'Reject unknown profile token'
 $r=SendSoap (Soap 'SetSystemFactoryDefault') 'device_service' $true; Check ($r.StatusCode -eq 500 -and $r.Content.Contains('ActionNotSupported')) 'Unsupported operation SOAP fault'
 $r=Invoke-WebRequest 'http://127.0.0.1:28080/snapshot.jpg' -SkipHttpErrorCheck
 Check ($r.StatusCode -eq 401) 'Snapshot authentication required'
 $authorization='Basic '+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($settings.Username+':'+$settings.Password))
 $r=Invoke-WebRequest 'http://127.0.0.1:28080/snapshot.jpg' -Headers @{Authorization=$authorization} -SkipHttpErrorCheck
 Check ($r.StatusCode -eq 200 -and $r.Headers.'Content-Type'.Contains('image/jpeg')) 'Authenticated snapshot'
 $udp=[Net.Sockets.UdpClient]::new(0); $udp.Client.ReceiveTimeout=3000
 try {
  $id='urn:uuid:'+ [guid]::NewGuid()
  $probe="<s:Envelope xmlns:s='http://www.w3.org/2003/05/soap-envelope' xmlns:a='http://schemas.xmlsoap.org/ws/2004/08/addressing' xmlns:d='http://schemas.xmlsoap.org/ws/2005/04/discovery'><s:Header><a:MessageID>$id</a:MessageID></s:Header><s:Body><d:Probe/></s:Body></s:Envelope>"
  $bytes=[Text.Encoding]::UTF8.GetBytes($probe); $udp.Send($bytes,$bytes.Length,'127.0.0.1',3702) | Out-Null
  $remote=[Net.IPEndPoint]::new([Net.IPAddress]::Any,0)
  $reply=[Text.Encoding]::UTF8.GetString($udp.Receive([ref]$remote))
  Check ($reply.Contains($id) -and $reply.Contains('http://127.0.0.1:28080/onvif/device_service')) 'WS-Discovery ProbeMatch'
 } finally { $udp.Dispose() }
 $ffprobe=Join-Path $root 'vendor\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffprobe.exe'
 $probeOutput=& $ffprobe -v error -rtsp_transport tcp -timeout 8000000 -read_intervals '%+4' -show_entries stream=codec_name,profile,width,height,r_frame_rate:packet=pts_time,size -of json 'rtsp://127.0.0.1:28554/desktop'
 Check ($LASTEXITCODE -eq 0) 'RTSP stream read'
 $probeOutput | Set-Content (Join-Path $DataDirectory 'stream-probe.json')
 $video=$probeOutput | ConvertFrom-Json
 Check ($video.streams[0].codec_name -eq 'h264' -and $video.streams[0].r_frame_rate -eq '5/1') 'H.264 at configured 5 fps'
 Check ($video.streams[0].profile -eq 'Main') 'Advertised H.264 Main profile matches stream'
 $packets=@($video.packets)
 $span=[double]$packets[-1].pts_time-[double]$packets[0].pts_time
 $kbps=($packets | Measure-Object size -Sum).Sum*8/1000/($span+0.2)
 Check ($kbps -gt 800 -and $kbps -lt 1200) ('Measured bitrate near 1000 kbps: '+[math]::Round($kbps))
 $app.WaitForExit(60000) | Out-Null
 Check ($app.HasExited -and $app.ExitCode -eq 0) 'Graceful timed shutdown'
 $results | Set-Content (Join-Path $DataDirectory 'results.txt')
} finally {
 if(-not $app.HasExited) { & taskkill.exe /PID $app.Id /T /F | Out-Null }
}
