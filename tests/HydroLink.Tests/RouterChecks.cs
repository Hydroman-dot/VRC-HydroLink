using System.Net;
using System.Net.Sockets;
using HydroLink.Core;
internal static class RouterChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var rule = new RouterRule { Enabled=true, From="A", To="B", Address="/test/*", ConvertValue=true, Invert=true, Scale=2, Offset=.1, Min=0, Max=1, Rename="/mapped" };
        check(rule.Matches("A","/test/x","Standard","") && !rule.Matches("B","/test/x","Standard","") && !rule.Matches("A","/test/x","Other","") && !rule.Matches("A","/testing/x","Standard",""), "router filters source, prefix and selected profile");
        var transformed=rule.Transform(new("/test/x", [.8f, "keep", true]));
        check(transformed.Address=="/mapped" && Math.Abs((float)transformed.Arguments[0]-.5)<.0001 && Equals(transformed.Arguments[1],"keep") && Equals(transformed.Arguments[2],true), "router rename, inversion, scaling and extra arguments");
        check(Equals(rule.Transform(new("/test/x", [-10f])).Arguments[0],1f), "router transformed values clamp to configured bounds");
        rule.Avatar="avtr_test";
        check(!rule.Matches("A","/test/x","Standard","") && rule.Matches("A","/test/x","Standard","avtr_test"), "router avatar profile only matches active avatar");
        void Reject(OscRouterOptions options, string name) { bool rejected=false;try{options.Validate();}catch(ArgumentException){rejected=true;}check(rejected,name); }
        Reject(new() { Endpoints=[new() { ReceivePort=9001 }] }, "router reserves conventional OSC port 9001");
        Reject(new() { Endpoints=[new() { Name="Hub" }] }, "router reserves hub name");
        Reject(new() { Endpoints=[new() {ReceivePort=9100,SendPort=9100}] }, "router rejects self forwarding endpoints");
        using var destination = new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback,0));
        int inputPort; using(var reservation=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))) inputPort=((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        var options = new OscRouterOptions { Endpoints=[new() {Name="Input",ReceivePort=inputPort,SendPort=1234}, new(){Name="Output",SendPort=((IPEndPoint)destination.Client.LocalEndPoint!).Port},new(){Name="Second",SendPort=((IPEndPoint)second.Client.LocalEndPoint!).Port}], Rules=[new(){Enabled=true,From="Input",To="Output",Address="/test",Rename="/renamed",ConvertValue=true,Scale=2},new(){Enabled=true,From="Input",To="Second",Address="/test"},new(){Enabled=true,From="Input",To="Hub",Address="/to-vrc"},new(){Enabled=true,From="Hub",To="Output",Address="/from-vrc"}] };
        options.Validate();
        var bus = new DataBus(); int hub=0; using var sub=bus.Subscribe("osc/send",_=>Interlocked.Increment(ref hub));
        var router = new OscRouter(new Settings {Router=options},bus,_=>{});
        async Task Wait(Func<bool> condition) { using var deadline=new CancellationTokenSource(4000);while(!condition())await Task.Delay(20,deadline.Token); }
        async Task<OscMessage> Read(UdpClient client) { using var deadline=new CancellationTokenSource(4000); return OscCodec.Decode((await client.ReceiveAsync(deadline.Token)).Buffer).Single(); }
        using var source = new UdpClient();
        await router.StartAsync(default); await Wait(()=>router.Status.StartsWith("Bereit"));
        await source.SendAsync(OscCodec.Encode("/test",.3f),new IPEndPoint(IPAddress.Loopback,inputPort));
        var a=await Read(destination);var b=await Read(second);
        check(a.Address=="/renamed" && Math.Abs((float)a.Arguments[0]-.6f)<.0001 && b.Address=="/test", "router UDP fan-out and transformed destination verified through real loopback sockets");
        await source.SendAsync(new byte[] {1,2,3},new IPEndPoint(IPAddress.Loopback,inputPort));
        await source.SendAsync(OscCodec.Encode("/to-vrc",true),new IPEndPoint(IPAddress.Loopback,inputPort));await Wait(()=>Volatile.Read(ref hub)==1);
        check(router.Dropped>=1 && hub==1,"malformed datagram does not stop legacy-to-hub routing");
        bus.Publish("osc/receive/from-vrc",new {address="/from-vrc",arguments=new object[]{1f},types=new[]{"f"}},"osc");
        var typed = await Read(destination);
        check(typed.Address=="/from-vrc" && typed.Arguments[0] is float, "shared hub messages preserve whole-valued OSC floats at legacy UDP destination");
        var before=router.Sent;
        bus.Publish("osc/receive/to-vrc",new {address="/to-vrc",arguments=new object[]{true}},"osc");await Wait(()=>router.Dropped>=2);
        check(router.Sent==before,"router suppresses echo returned from the destination hub");
        await router.DisposeAsync();
        using(var reclaimed=new UdpClient(new IPEndPoint(IPAddress.Loopback,inputPort)))check(true,"router stop releases legacy receive port");
        var components=new Dictionary<string,FakeComponent>();
        await using var host=new HydroHost(new Settings(),_=>{},new FakeLauncher(),id=>components[id]=new FakeComponent(id));
        await host.SetModuleAsync("osc",true);var osc=components["osc"];
        await host.SetModuleAsync("osc-router",true);var first=components["osc-router"];
        await host.RestartModuleAsync("osc-router");
        check(first.Stops==1 && osc.Stops==0 && host.ActiveModules.Contains("osc"),"router restart preserves separately running OSC hub");
        await host.SetModuleAsync("osc-router",false);
        check(osc.Stops==0 && host.Running.Contains("osc") && !host.Running.Contains("osc-router"),"router stop leaves shared OSC service active");
    }
}

