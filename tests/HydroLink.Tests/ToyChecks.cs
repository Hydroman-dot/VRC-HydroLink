using VrcHapticsBridge.Core;
using VrcHapticsBridge.Tests;
using HydroLink.Core;
using Buttplug.Core.Messages;

internal static class ToyChecks
{
    public static async Task Run(Action<bool,string> check, bool network)
    {
        check(ToyHaptics.Matches(ToyKind.Max2, "Lovense Max 2") && ToyHaptics.Matches(ToyKind.Max2, "Lovense Max") && ToyHaptics.Matches(ToyKind.Ridge, "Lovense Ridge") && !ToyHaptics.Matches(ToyKind.Max2, "Maximum"), "toy auto matching uses complete model words");
        var backend = new UniversalMemory();
        await using var toys = new ToyHaptics(backend);
        int contactRequests = 0;
        toys.SpsContactsRequested += () => contactRequests++;
        async Task Drain() { for (var i=0;i<5;i++) { await toys[ToyKind.Max2].TickAsync(); await toys[ToyKind.Ridge].TickAsync(); await Task.Delay(60); } }
        string P(string id, string signal) => "/avatar/parameters/OGB/Orf/" + id + "/" + signal;
        toys.Receive([new(P("Before", "PenOthers"), 1)]);
        check(contactRequests == 0, "incoming contacts never enable avatar contact receivers on their own");
        await toys.ActivateAsync(ToyKind.Max2, 1, .2, .3, true); toys.Receive([new("/avatar/parameters/HydroLink/Max2/Enabled",1)]);
        check(contactRequests == 1, "explicit Max 2 activation requests VRCFury contact receivers");
        await toys.ActivateAsync(ToyKind.Ridge, 2, .4, .5, false); toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]);
        check(contactRequests == 2, "explicit Ridge activation also requests VRCFury contact receivers");
        await Drain();
        check(backend.Values.Count == 0 && toys[ToyKind.Max2].SourceCount == 0, "toy activation never replays pre-activation contacts");
        toys.Receive([new(P("Hand", "PenOthers"), .6)]); await Drain();
        check(toys[ToyKind.Max2].Snapshot.Armed && toys[ToyKind.Ridge].Snapshot.Armed && backend.Values.Any(v => v.Feature.Type==OutputType.Constrict && Math.Abs(v.Amount-.18)<.0001)
            && backend.Values.Any(v => v.Feature.Type==OutputType.Rotate && Math.Abs(v.Amount-.3)<.0001 && !v.Clockwise), "both toy tabs route fresh sockets with independent levels and rotation");
        toys.Receive([new(P("Second", "PenSelfNewTip"), 1), new(P("Second", "PenSelfNewRoot"), .96)]); await Drain();
        check(toys[ToyKind.Ridge].SourceCount==2 && Math.Abs(toys[ToyKind.Ridge].Snapshot.SourceLevel-.8)<.0001, "new sockets join automatically while active, including tip-before-root order");
        toys[ToyKind.Ridge].SetSpsLevels(.1,.2,true); await Drain();
        check(Math.Abs(toys[ToyKind.Ridge].Snapshot.Sent[backend.Features[2].Key]-.16)<.0001, "toy sliders apply live without transfer or rearming");
        toys.Receive([new("/avatar/parameters/HydroLink/Enabled",0),new("/avatar/parameters/HydroLink/Strength",0)]); await Drain();
        check(toys[ToyKind.Ridge].Snapshot.Armed && toys[ToyKind.Ridge].Snapshot.Strength==1, "simple toy mode does not require old HydroLink menu gates");
        var count=toys[ToyKind.Ridge].SourceCount;
        toys.Receive([new("/avatar/parameters/OGB/Orf/Wrong/NotAPenSignal",1),new("/avatar/parameters/OGB/Pen/Wrong/NotAPenSignal",1)]);
        check(toys[ToyKind.Ridge].SourceCount==count, "automatic SPS ignores unknown plug and socket signals");
        toys.Receive([new("/avatar/parameters/OGB/Pen/Plug/PenOthers",1)]); await Drain();
        check(toys[ToyKind.Ridge].SourceCount==count+1 && toys[ToyKind.Ridge].Snapshot.SourceLevel==1 && toys[ToyKind.Max2].Snapshot.SourceLevel==1,
            "live plug is automatically added to both toy tabs alongside sockets");
        toys.Receive([new("/avatar/parameters/OGB/Pen/Plug/PenOthers",0)]); await Drain();
        check(Math.Abs(toys[ToyKind.Ridge].Snapshot.SourceLevel-.8)<.0001, "plug exit falls back to strongest remaining socket");
        toys.Receive([new(P("Hand","PenOthers"),0),new(P("Second","PenSelfNewTip"),0),new("/avatar/parameters/OGB/Pen/Plug/PenSelf",.45)]); await Drain();
        check(Math.Abs(toys[ToyKind.Ridge].Snapshot.Sent[backend.Features[2].Key]-.09)<.0001,
            "plug self signal alone drives the selected toy output");
        toys.Receive([new("/avatar/parameters/HydroLink/Max2/Vibration",.6),new("/avatar/parameters/HydroLink/Max2/Pump",.7)]); await Drain();
        check(toys[ToyKind.Max2].SpsLevels.Vibration==.6 && toys[ToyKind.Max2].SpsLevels.Secondary==.7 && toys[ToyKind.Ridge].SpsLevels.Secondary==.2,
            "Max2 prefab menu controls only its own channels");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Rotation",.4),new("/avatar/parameters/HydroLink/Ridge/Clockwise",0)]); await Drain();
        check(toys[ToyKind.Ridge].SpsLevels.Secondary==.4 && !toys[ToyKind.Ridge].SpsLevels.Clockwise && toys[ToyKind.Max2].SpsLevels.Secondary==.7,
            "Ridge prefab menu controls rotation and direction independently");
        toys.Receive([new("/avatar/parameters/HydroLink/Max2/Rotation",1),new("/avatar/parameters/HydroLink/Ridge/Pump",1)]);
        check(toys[ToyKind.Max2].SpsLevels.Secondary==.7 && toys[ToyKind.Ridge].SpsLevels.Secondary==.4, "foreign toy channels are ignored");
        toys.Receive([new("/avatar/parameters/HydroLink/Max2/Stop",1),new("/avatar/parameters/HydroLink/Max2/Pump",1)]); await Drain();
        check(!toys[ToyKind.Max2].Snapshot.Armed && toys[ToyKind.Ridge].Snapshot.Armed, "stopping Max 2 leaves Ridge active");
        toys.Receive([new("/avatar/parameters/HydroLink/Max2/Stop",0),new("/avatar/parameters/HydroLink/Max2/Pump",1)]);
        check(!toys[ToyKind.Max2].Snapshot.Armed, "prefab stop button release and menu values cannot rearm hardware");
        bool duplicate=false;
        try { await toys.ActivateAsync(ToyKind.Max2,2,.2,.2,true); toys.Receive([new("/avatar/parameters/HydroLink/Max2/Enabled",1)]); } catch(InvalidOperationException) { duplicate=true; }
        check(duplicate && toys[ToyKind.Ridge].Snapshot.Armed, "same physical device cannot be owned by both toy tabs");
        toys.Receive([new("/avatar/change",null)]); await Drain();
        check(!toys[ToyKind.Ridge].Snapshot.Armed && toys[ToyKind.Ridge].SourceCount==0, "avatar change clears automatic sockets and stops toys");
        await toys.ActivateAsync(ToyKind.Ridge,2,.2,.2,true); toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]);
        toys.Receive([new(P("NewAvatar", "PenOthers"), .5),new("/avatar/parameters/OGB_ENABLED",0)]); await Drain();
        check(!toys[ToyKind.Ridge].Snapshot.Armed, "SPS global disable remains effective in automatic toy mode");
        await toys.ActivateAsync(ToyKind.Max2,1,.2,.3,true); toys.Receive([new("/avatar/parameters/HydroLink/Max2/Enabled",1)]);
        await toys.ActivateAsync(ToyKind.Ridge,2,.4,.5,false); toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]);
        toys.Receive([new("/avatar/parameters/HydroLink/Max2/ManualPump",.3),new("/avatar/parameters/HydroLink/Ridge/ManualRotation",.5),new("/avatar/parameters/HydroLink/Max2/Manual",1),new("/avatar/parameters/HydroLink/Ridge/Manual",1)]); await Drain();
        check(toys[ToyKind.Max2].ManualToy && toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.SourceLevel==0
            && Math.Abs(toys[ToyKind.Max2].Snapshot.Sent[backend.Features[1].Key]-.3)<.0001 && Math.Abs(toys[ToyKind.Ridge].Snapshot.Sent[backend.Features[2].Key]-.5)<.0001,
            "radial manual mode drives Max2 pump and Ridge rotation with no SPS input");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/ManualVibration",.7),new("/avatar/parameters/HydroLink/Ridge/ManualClockwise",1)]); await Drain();
        check(Math.Abs(toys[ToyKind.Ridge].Snapshot.Sent[backend.Features[3].Key]-.7)<.0001 && toys[ToyKind.Max2].SpsLevels.Vibration==.2,
            "manual radial channels remain independent per toy");
        toys.Receive([new("/avatar/parameters/OGB_ENABLED",0)]); await Drain();
        check(toys[ToyKind.Max2].Snapshot.Armed && toys[ToyKind.Ridge].Snapshot.Armed,"manual toy control works while SPS is globally off");
        toys.Receive([new("/avatar/parameters/HydroLink/Max2/Stop",1)]); await Drain();
        check(!toys[ToyKind.Max2].Snapshot.Armed && toys[ToyKind.Ridge].Snapshot.Armed,"radial stop ends manual output for only its toy");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Manual",0)]); await Drain();
        check(!toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.Sent.Values.All(v=>v==0),"return from manual mode waits for new SPS contact instead of retaining direct output");
        toys.Receive([new(P("ManualReturn","PenOthers"),.6)]); await Drain();
        check(Math.Abs(toys[ToyKind.Ridge].Snapshot.SourceLevel-.6)<.0001,"automatic SPS resumes after manual mode");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Manual",1),new("/avatar/change",null)]); await Drain();
        await toys.ActivateAsync(ToyKind.Ridge,2,.2,.2,true); toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]); await Drain();
        check(!toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.Sent.Count==0,"avatar change and rearm never restore manual movement");
        await toys.StopAllAsync();
        await toys.ActivateAsync(ToyKind.Ridge,2,.2,.4,true);
        toys.Receive([new(P("Gate","PenOthers"),1),new("/avatar/parameters/HydroLink/Ridge/Manual",1)]); await Drain();
        check(toys[ToyKind.Ridge].Snapshot.Armed && !toys[ToyKind.Ridge].Snapshot.AvatarEnabled && !toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.Sent.Count==0, "desktop release requires radial haptics gate even for manual mode");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]); await Drain();
        check(toys[ToyKind.Ridge].Snapshot.Sent.Count==0, "radial enable clears contacts received while disabled");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/ManualRotation",.8),new("/avatar/parameters/HydroLink/Ridge/ManualClockwise",0),new("/avatar/parameters/HydroLink/Ridge/Manual",1)]); await Drain();
        check(toys[ToyKind.Ridge].SpsLevels.Secondary==.4 && toys[ToyKind.Ridge].SpsLevels.Clockwise && toys[ToyKind.Ridge].ActiveToyLevels.Secondary==.8 && !toys[ToyKind.Ridge].ActiveToyLevels.Clockwise, "manual submenu keeps strength and direction separate from SPS");
        check(Math.Abs(toys[ToyKind.Ridge].Snapshot.Sent[backend.Features[2].Key]-.8)<.0001 && backend.Values.Last(v=>v.Feature.Type==OutputType.Rotate).Clockwise==false, "manual submenu sends own rotation value and direction");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",0),new("/avatar/parameters/HydroLink/Ridge/Manual",1)]); await Drain();
        check(toys[ToyKind.Ridge].Snapshot.Armed && !toys[ToyKind.Ridge].Snapshot.AvatarEnabled && !toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.Sent.Values.All(v=>v==0), "radial off overrides bundled manual enable and stops all channels without losing desktop release");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1),new(P("GateReturn","PenOthers"),.5)]); await Drain();
        check(!toys[ToyKind.Ridge].ManualToy && Math.Abs(toys[ToyKind.Ridge].Snapshot.Sent[backend.Features[2].Key]-.2)<.0001, "radial re-enable resumes SPS using retained SPS strengths");
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Manual",1)]); await Drain();
        check(toys[ToyKind.Ridge].ActiveToyLevels.Secondary==.8, "manual values survive mode and haptics switches within session");
        toys[ToyKind.Ridge].SetActiveToyLevels(.6,.7,true);
        check(toys[ToyKind.Ridge].ActiveToyLevels.Secondary==.7 && toys[ToyKind.Ridge].SpsLevels.Secondary==.4, "desktop manual slider never overwrites SPS strengths");
        await toys.StopAllAsync(); await toys.ActivateAsync(ToyKind.Ridge,2,.2,.4,true);
        check(!toys[ToyKind.Ridge].Snapshot.AvatarEnabled && !toys[ToyKind.Ridge].ManualToy, "stop and new desktop release reset radial enables");
        toys[ToyKind.Ridge].SetDesktopManual(true); await Drain();
        check(toys[ToyKind.Ridge].ManualToy && !toys[ToyKind.Ridge].Snapshot.AvatarEnabled && toys[ToyKind.Ridge].Snapshot.Sent.Values.Any(v => v > 0), "explicit desktop test works without radial gate and never enables SPS gate");
        toys[ToyKind.Ridge].SetDesktopManual(false); await Drain();
        check(!toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.Sent.Values.All(v => v == 0), "ending desktop test stops output while radial gate remains off");
        toys[ToyKind.Ridge].SetDesktopManual(true);
        toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",0)]); await Drain();
        check(!toys[ToyKind.Ridge].ManualToy && toys[ToyKind.Ridge].Snapshot.Sent.Values.All(v => v == 0), "explicit radial off also cancels desktop test");
        toys[ToyKind.Ridge].SetDesktopManual(true); await toys.StopAllAsync();
        toys[ToyKind.Ridge].SetDesktopManual(true);
        check(!toys[ToyKind.Ridge].ManualToy && !toys[ToyKind.Ridge].Snapshot.Armed, "desktop test cannot restart stopped device");
        var solaceBackend = new UniversalMemory([
            new(3, 7, "Lovense Solace Pro", "Stroker", OutputType.Oscillate, 0, 20, 0),
            new(3, 7, "Lovense Solace Pro", "Stroker", OutputType.HwPositionWithDuration, 0, 100, 0, 0, 100000)]);
        await using var solace = new ToyHaptics(solaceBackend);
        check(ToyHaptics.Matches(ToyKind.SolacePro,"Lovense Solace Pro") && !ToyHaptics.Matches(ToyKind.SolacePro,"Lovense Solace"), "Solace Pro auto selection never confuses original Solace");
        await solace.ActivateAsync(ToyKind.SolacePro,3,.4,0,true);
        var se = solace[ToyKind.SolacePro];
        check(se.Snapshot.SelectedFeatures==1 && !se.Snapshot.AvatarEnabled, "Solace selects speed only, never both commands of the same actuator");
        solace.Receive([new(P("BeforeGate","PenOthers"),1)]); await se.TickAsync();
        check(solaceBackend.Values.Count==0,"Solace requires radial gate before movement");
        solace.Receive([new("/avatar/parameters/HydroLink/SolacePro/Enabled",1),new(P("Socket","PenOthers"),.5)]); await se.TickAsync();
        check(solaceBackend.Values.Count==1 && solaceBackend.Values[0].Feature.Type==OutputType.Oscillate && Math.Abs(solaceBackend.Values[0].Amount-.2)<.0001,"Solace SPS socket scales oscillation speed using its own limit");
        solace.Receive([new("/avatar/parameters/HydroLink/SolacePro/ManualMotion",.7),new("/avatar/parameters/HydroLink/SolacePro/Manual",1)]); await Task.Delay(60); await se.TickAsync();
        check(Math.Abs(solaceBackend.Values.Last().Amount-.7)<.0001 && se.SpsLevels.Vibration==.4,"Solace manual speed is independent of SPS speed");
        se.SetActiveToyLevels(.6,0,true); await Task.Delay(60); await se.TickAsync();
        check(Math.Abs(solaceBackend.Values.Last().Amount-.6)<.0001,"Solace desktop manual speed reaches oscillation output");
        solace.Receive([new("/avatar/parameters/HydroLink/SolacePro/Manual",0)]); await Task.Delay(60); await se.TickAsync();
        check(solaceBackend.Stops.Contains(3) && se.Snapshot.Sent.Count==0,"Solace leaving manual mode stops until fresh contact");
        solace.Receive([new("/avatar/parameters/HydroLink/SolacePro/Motion",.3),new("/avatar/parameters/OGB/Pen/Plug/PenOthers",1)]); await Task.Delay(60); await se.TickAsync();
        check(Math.Abs(solaceBackend.Values.Last().Amount-.3)<.0001,"Solace radial SPS speed and plug input are routed");
        solace.Receive([new("/avatar/parameters/HydroLink/SolacePro/Stop",1)]); await se.TickAsync();
        check(!se.Snapshot.Armed && solaceBackend.Stops.Contains(3),"Solace stop uses device stop and never positional movement");
        var allProfile = new WizardProfile(1,Guid.NewGuid().ToString(),"avtr_00000000-0000-0000-0000-000000000001","Test",["Max2","Ridge","SolacePro"],1,2);
        check(WizardSyncReader.Parse(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(allProfile)).Includes(ToyKind.SolacePro),"wizard handoff accepts three independent toy profiles");
        await SolaceControlChecks.Run(check);
        if(!network) return;
        await using var server=new FakeServer(); server.UniversalDevices=true; await server.StartAsync();
        var bus=new DataBus(); await using var service=new HapticsService(new(){IntifaceUrl=server.Url},bus,Console.WriteLine);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25)); await service.StartAsync(timeout.Token);
        while(service.Backend.Features.Count<8) await Task.Delay(25,timeout.Token);
        bus.Publish("osc/avatar-id", "avtr_gate_test", "test");
        var oldQuery = DateTimeOffset.UtcNow.AddMinutes(-1);
        await service.ActivateToyAsync(ToyKind.Max2,7,.2,.2,true);
        void QueryGate(DateTimeOffset started, string avatar = "avtr_gate_test") => bus.Publish("osc/avatar-gate", new { avatar, queryStarted = started, address = "/avatar/parameters/HydroLink/Max2/Enabled", enabled = 1.0 }, "test");
        QueryGate(oldQuery);
        check(!service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled, "pre-arm query cannot restore radial gate");
        QueryGate(DateTimeOffset.UtcNow, "avtr_other");
        check(!service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled, "other avatar query cannot enable radial gate");
        bus.Publish("osc/receive/avatar/parameters/OGB_ENABLED", new { address = "/avatar/parameters/OGB_ENABLED", arguments = new[] { true } }, "test");
        QueryGate(DateTimeOffset.UtcNow);
        check(service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled && !service.Toys[ToyKind.Max2].ManualToy && service.Toys[ToyKind.Max2].SourceCount == 0, "fresh post-arm gate initializes already-enabled haptics without contacts or manual movement");
        await service.StopToyAsync(ToyKind.Max2);
        await service.ActivateToyAsync(ToyKind.Max2,7,.2,.2,true);
        var gateAddress = "/avatar/parameters/HydroLink/Max2/Enabled";
        bus.Publish("osc/receive" + gateAddress, new { address = gateAddress, arguments = new[] { false } }, "test");
        QueryGate(DateTimeOffset.UtcNow);
        check(!service.Toys[ToyKind.Max2].Snapshot.AvatarEnabled, "live radial off takes priority over pending query");
        await service.StopToyAsync(ToyKind.Max2);
        QueryGate(DateTimeOffset.UtcNow);
        check(!service.Toys[ToyKind.Max2].Snapshot.Armed, "query never rearms stopped hardware");
        await service.ActivateToyAsync(ToyKind.Max2,7,.2,.2,true); service.Toys.Receive([new("/avatar/parameters/HydroLink/Max2/Enabled",1)]);
        await service.ActivateToyAsync(ToyKind.Ridge,9,.3,.3,true); service.Toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]);
        await service.ActivateToyAsync(ToyKind.SolacePro,10,.4,0,true);
        service.Toys.Receive([new("/avatar/parameters/HydroLink/SolacePro/Enabled",1)]);
        var address=P("DiscoveredAtRuntime","PenOthers");
        bus.Publish("osc/receive"+address,new{address,arguments=new[]{.7}},"test");
        while(service.Toys[ToyKind.Max2].Snapshot.Sent.Count<2 || service.Toys[ToyKind.Ridge].Snapshot.Sent.Count<3) await Task.Delay(25,timeout.Token);
        check(service.Toys[ToyKind.Ridge].SourceCount==1 && service.Toys[ToyKind.Max2].SourceCount==1, "service auto routes live bus sockets to both toys over real WebSocket");
        while(service.Toys[ToyKind.SolacePro].Snapshot.Sent.Count<1) await Task.Delay(25,timeout.Token);
        check(server.Messages.Where(m=>m.TryGetProperty("OutputCmd",out _)).Select(m=>m.GetProperty("OutputCmd")).Any(m=>m.GetProperty("DeviceIndex").GetInt32()==10 && m.GetProperty("FeatureIndex").GetInt32()==17 && m.GetProperty("Command").TryGetProperty("Oscillate",out var c) && c.GetProperty("Value").GetInt32()==28), "Solace profile sends correctly scaled Oscillate command over WebSocket alongside Max 2 and Ridge");
        var solaceStop="/avatar/parameters/HydroLink/SolacePro/Stop";
        bus.Publish("osc/receive"+solaceStop,new{address=solaceStop,arguments=new[]{true}},"test");
        check(!service.Toys[ToyKind.SolacePro].Snapshot.Armed && service.Toys[ToyKind.Max2].Snapshot.Armed && service.Toys[ToyKind.Ridge].Snapshot.Armed,"Solace radial stop is isolated from both other toy sessions");
        var plugAddress="/avatar/parameters/OGB/Pen/LivePlug/PenOthers";
        bus.Publish("osc/receive"+plugAddress,new{address=plugAddress,arguments=new[]{1.0}},"test");
        while(service.Toys[ToyKind.Ridge].Snapshot.SourceLevel!=1 || service.Toys[ToyKind.Ridge].Snapshot.Sent.Values.Any(v=>Math.Abs(v-.3)>.0001)) await Task.Delay(25,timeout.Token);
        check(service.Toys[ToyKind.Ridge].SourceCount==2 && service.Toys[ToyKind.Max2].SourceCount==2, "live plug bus messages reach both toy sessions over WebSocket");
        var menuAddress="/avatar/parameters/HydroLink/Ridge/Rotation";
        bus.Publish("osc/receive"+menuAddress,new{address=menuAddress,arguments=new[]{.15}},"test");
        while(service.Toys[ToyKind.Ridge].Snapshot.Sent.Where(kv=>kv.Key.EndsWith(":Rotate")).Any(kv=>Math.Abs(kv.Value-.15)>.0001)) await Task.Delay(25,timeout.Token);
        check(service.Toys[ToyKind.Ridge].SpsLevels.Secondary==.15 && service.Toys[ToyKind.Max2].SpsLevels.Secondary==.2, "prefab menu OSC changes correct toy output over WebSocket");
        var stopAddress="/avatar/parameters/HydroLink/Max2/Stop";
        bus.Publish("osc/receive"+stopAddress,new{address=stopAddress,arguments=new[]{1}},"test");
        check(!service.Toys[ToyKind.Max2].Snapshot.Armed, "prefab stop OSC disarms its toy through the service");
        check(service.Toys[ToyKind.Ridge].Snapshot.Armed, "service per-toy stop preserves the other toy");
        await service.ConfigureUniversalAsync([new(service.Backend.Features.First(f=>f.Supported))],UniversalSourceMode.Menu,[]);
        check(!service.Toys[ToyKind.Ridge].Snapshot.Armed, "advanced routing stops simple toy controllers before takeover");
        await service.ActivateToyAsync(ToyKind.Ridge,9,.3,.3,true); service.Toys.Receive([new("/avatar/parameters/HydroLink/Ridge/Enabled",1)]);
        bus.Publish("osc/query-lost",true,"test");
        check(!service.Toys[ToyKind.Ridge].Snapshot.Armed, "query loss disarms simple toy routing");
    }
}
