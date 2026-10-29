using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HydroLink.Core;
using VRC.OSCQuery;

internal static class OscStartupChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        using (var first = OscBridge.OpenReceiver())
        using (var second = OscBridge.OpenReceiver())
            check(((IPEndPoint)first.Client.LocalEndPoint!).Port != ((IPEndPoint)second.Client.LocalEndPoint!).Port, "independent OSC receivers never share a port");
        foreach (var peerFirst in new[] { true, false })
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
            using var http = new HttpClient { Timeout=TimeSpan.FromSeconds(3) };
            var port=((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            var name="HydroLink-OrderTest-"+Guid.NewGuid().ToString("N");
            var bus=new DataBus();
            await using var bridge=new OscBridge(new(),bus,Console.WriteLine,name);
            var advertised=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var received=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var sub=bus.Subscribe("osc/receive/avatar/parameters/OrderProbe",_=>received.TrySetResult(true));
            async Task Probe(OSCQueryServiceProfile p)
            {
                if (!p.name.StartsWith("VRC-HydroLink-") || p.port!=bridge.QueryPort) return;
                try
                {
                    using var root=JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{p.port}/",timeout.Token));
                    var avatar=root.RootElement.GetProperty("CONTENTS").GetProperty("avatar");
                    var complete=avatar.GetProperty("ACCESS").GetInt32()==2
                        && avatar.GetProperty("CONTENTS").GetProperty("parameters").GetProperty("CONTENTS").TryGetProperty("HydroLink",out _);
                    advertised.TrySetResult(complete);
                    using var host=JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{p.port}/?HOST_INFO",timeout.Token));
                    await udp.SendAsync(OscCodec.Encode("/avatar/parameters/OrderProbe",.5f),new IPEndPoint(IPAddress.Loopback,host.RootElement.GetProperty("OSC_PORT").GetInt32()),timeout.Token);
                }
                catch(Exception ex) { advertised.TrySetException(ex); }
            }
            OSCQueryService Peer()
            {
                var service=new OSCQueryServiceBuilder().WithServiceName(name).WithTcpPort(Extensions.GetAvailableTcpPort()).WithUdpPort(port).Build();
                service.AddEndpoint("/avatar/parameters/Ready","f",Attributes.AccessValues.ReadWrite,[.5f]);
                service.OnOscQueryServiceAdded+=p=>_ = Probe(p);
                service.StartHttpServer(); service.SetDiscovery(new MeaModDiscovery());
                service.AdvertiseOSCQueryService(name,service.TcpPort); service.AdvertiseOSCService(name,port);
                service.RefreshServices();
                return service;
            }
            OSCQueryService? peer=null;
            try
            {
                if(peerFirst) peer=Peer();
                await bridge.StartAsync(timeout.Token);
                if(!peerFirst)
                {
                    while(bridge.QueryPort==0) await Task.Delay(50,timeout.Token);
                    peer=Peer();
                }
                check(await advertised.Task.WaitAsync(timeout.Token),$"{(peerFirst ? "VRChat first" : "HydroLink first")}: first advertised tree already contains avatar receiver");
                check(await received.Task.WaitAsync(timeout.Token),$"{(peerFirst ? "VRChat first" : "HydroLink first")}: peer discovers receiver and delivers live OSC without restart");
                while(bridge.Target!=$"127.0.0.1:{port}") await Task.Delay(100,timeout.Token);
                check(true,$"{(peerFirst ? "VRChat first" : "HydroLink first")}: outgoing peer discovered automatically");
            }
            finally { peer?.Dispose(); }
        }
    }
}
