using Buttplug.Core.Messages;
using VrcHapticsBridge.Core;
internal static class SolaceControlChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var speed = new HapticFeature(3,7,"Solace Pro","Motor",OutputType.Oscillate,0,20,0);
        var position = speed with { Type=OutputType.HwPositionWithDuration, Maximum=100, DurationMin=0, DurationMax=100000 };
        var backend = new UniversalMemory([speed,position]);
        var clock = new HapticsClock();
        await using var engine = new UniversalHaptics(backend,clock);
        await engine.ConfigureAsync([new(speed,1)],UniversalSourceMode.Sps,[],allSps:true,toyGate:true,solace:true);
        await engine.ArmAsync(); engine.SetSpsLevels(.5,0);
        engine.SetSpsChannel("PositionMode",1); engine.SetSpsChannel("StrokeStart",.2); engine.SetSpsChannel("StrokeEnd",.8);
        await engine.TickAsync(); check(backend.Positions.Count==0,"position mode never moves before radial enable");
        engine.SetSpsChannel("Enabled",1);
        void Contact(double value) => engine.Receive([new("/avatar/parameters/OGB/Orf/Socket/PenOthers",value)]);
        Contact(.5); await engine.TickAsync();
        check(Math.Abs(backend.Positions.Last().Position-.5)<.0001 && backend.Positions.Last().Duration==500,"SPS position maps contact depth within both stroke limits with explicit duration");
        var count=backend.Positions.Count; await engine.TickAsync(); clock.Advance(1); await engine.TickAsync();
        check(backend.Positions.Count==count,"unchanged position is not repeatedly commanded");
        engine.SetSpsChannel("TravelTime",1); await engine.TickAsync();
        check(backend.Positions.Last().Duration==5000,"position travel time is configurable up to five seconds");
        Contact(0); var stops=backend.Stops.Count; await engine.TickAsync();
        check(backend.Stops.Count>stops && backend.Positions.Count==count+1,"lost SPS contact stops instead of commanding position zero");
        engine.SetSpsChannel("ManualPositionMode",1); engine.SetSpsChannel("Manual",1);
        engine.SetActiveSolaceControl("Position",0); await engine.TickAsync();
        check(backend.Positions.Last().Position==0 && backend.Positions.Last().Duration==500,"manual zero is an explicit valid target rather than an automatic stop movement");
        check(engine.ActiveSolaceSettings.StrokeStart==0 && engine.ActiveSolaceSettings.StrokeEnd==1,"manual stroke bounds are separate from SPS bounds");
        engine.SetActiveSolaceControl("PositionMode",0); engine.SetActiveSolaceControl("StrokeStart",.3); engine.SetActiveSolaceControl("StrokeEnd",.6);
        engine.SetActiveToyLevels(.5,0,true); await engine.TickAsync();
        check(backend.Positions.Last().Position==.6,"bounded hub begins at upper configured endpoint");
        count=backend.Positions.Count; await engine.TickAsync(); check(backend.Positions.Count==count,"bounded hub respects travel duration before reversing");
        clock.Advance(2); await engine.TickAsync();
        check(backend.Positions.Last().Position==.3 && backend.Positions.Last().Duration==1100,"bounded hub alternates endpoints at speed-dependent duration");
        engine.SetActiveToyLevels(0,0,true); stops=backend.Stops.Count; await engine.TickAsync();
        check(backend.Stops.Count>stops,"zero hub speed stops immediately even before next scheduled endpoint");
        engine.SetActiveToyLevels(.5,0,true); engine.SetActiveSolaceControl("StrokeStart",.9); count=backend.Positions.Count; await engine.TickAsync();
        check(backend.Positions.Count==count && engine.Snapshot.Status.Contains("ungültig"),"crossed stroke bounds block movement");
        engine.SetActiveSolaceControl("StrokeStart",0); engine.SetActiveSolaceControl("StrokeEnd",1); await engine.TickAsync();
        check(backend.Values.Last().Feature.Type==OutputType.Oscillate,"restoring full-range hub switches back to native oscillation after stop");
        engine.SetActiveSolaceControl("PositionMode",1); await engine.TickAsync();
        engine.SetSpsChannel("Enabled",0); count=backend.Positions.Count; stops=backend.Stops.Count; await engine.TickAsync();
        check(backend.Stops.Count>stops && backend.Positions.Count==count,"radial off stops positioning without a return-to-zero command");
        await using var absent=new UniversalHaptics(new UniversalMemory([speed]),clock);
        await absent.ConfigureAsync([new(speed,1)],UniversalSourceMode.Sps,[],allSps:true,toyGate:true,solace:true);
        await absent.ArmAsync(); absent.SetSpsLevels(.5,0); absent.SetSpsChannel("Enabled",1); absent.SetSpsChannel("Manual",1); absent.SetActiveSolaceControl("PositionMode",1);
        await absent.TickAsync(); check(absent.Snapshot.Sent.Count==0 && absent.Snapshot.Status.Contains("nicht verfügbar"),"missing position capability never silently falls back to unrestricted speed");
    }
}
