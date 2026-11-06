using HydroLink.Core;
using VrcHapticsBridge.Core;
using VrcHapticsBridge.Tests;
internal static class AvatarGateChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var gate=new AvatarGateTracker(); var at=DateTimeOffset.UtcNow;
        gate.Begin("Max2","avatar",at);
        check(!gate.Accept("Max2","avatar",at.AddSeconds(-1),true) && !gate.Accept("Max2","other",at.AddSeconds(1),true),"gate rejects pre-activation and other-avatar query");
        check(gate.Accept("Max2","avatar",at.AddSeconds(1),false) && gate.Accept("Max2","avatar",at.AddSeconds(2),true),"initial query OFF does not consume subsequent query ON");
        gate.Live("Max2",false,at.AddSeconds(3));
        check(!gate.Accept("Max2","avatar",at.AddSeconds(2.5),true) && !gate.Accept("Max2","avatar",at.AddSeconds(4),true),"fresh live OFF wins over old query and immediate conflicting reply");
        check(!gate.Accept("Max2","avatar",at.AddSeconds(7),true) && !gate.Accept("Max2","avatar",at.AddSeconds(7),true) && gate.Accept("Max2","avatar",at.AddSeconds(10),true),"two distinct fresh confirmations recover missed ON after live OFF grace period");
        gate.Remove("Max2");check(!gate.Accept("Max2","avatar",at.AddSeconds(12),true),"removed gate cannot be resurrected by query");
        await using var server=new FakeServer {UniversalDevices=true};await server.StartAsync();
        var bus=new DataBus();await using var service=new HapticsService(new(){IntifaceUrl=server.Url},bus,_=>{});
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));await service.StartAsync(timeout.Token);
        while(service.Backend.Features.Count<8)await Task.Delay(25,timeout.Token);
        bus.Publish("osc/avatar-id","avtr_test","test");
        await service.ActivateToyAsync(ToyKind.Max2,7,.2,.2,true);
        void Query(bool enabled, DateTimeOffset time) => bus.Publish("osc/avatar-gate",new{avatar="avtr_test",queryStarted=time,address="/avatar/parameters/HydroLink/Max2/Enabled",enabled=enabled?1d:0d},"test");
        var start=DateTimeOffset.UtcNow;
        Query(false,start);Query(true,start.AddMilliseconds(1));
        check(service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled && service.Toys[ToyKind.Max2].SourceCount==0 && !service.Toys[ToyKind.Max2].ManualToy,"service recovers menu activation through repeated query without replaying contacts or manual mode");
        bus.Publish("osc/query-warning","temporary HTTP failure","test");
        check(service.Toys[ToyKind.Max2].Snapshot.Armed && service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled,"parameter-query warning leaves armed session and known gate intact");
        await service.ActivateToyAsync(ToyKind.Ridge,9,.2,.2,true);await service.StopToyAsync(ToyKind.Ridge);
        Query(false,start.AddSeconds(1));
        check(!service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled && service.Toys[ToyKind.Max2].Snapshot.Armed,"other toy stop does not cancel ongoing gate synchronization");
        Query(true,start.AddSeconds(2));
        check(service.GateStatus(ToyKind.Max2).Contains("OSCQuery: eingeschaltet"),"gate diagnostics identify query source and recognized state");
        service.SetDesktopManual(ToyKind.Max2,true);Query(false,start.AddSeconds(3));
        check(service.Toys[ToyKind.Max2].DesktopManual,"query polling does not take over desktop manual test");
        service.SetDesktopManual(ToyKind.Max2,false);Query(false,start.AddSeconds(4));Query(true,start.AddSeconds(5));
        check(service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled && !service.Toys[ToyKind.Max2].ManualToy,"leaving desktop manual mode resumes fresh menu gate synchronization");
        await service.StopToyAsync(ToyKind.Max2);Query(true,start.AddSeconds(6));
        check(!service.Toys[ToyKind.Max2].Snapshot.Armed && !service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled,"query cannot restart explicitly stopped toy");
    }
}
