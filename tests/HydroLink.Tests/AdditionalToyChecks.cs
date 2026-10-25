using VrcHapticsBridge.Core;
using Buttplug.Core.Messages;
internal static class AdditionalToyChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        foreach(var toy in Enum.GetValues<ToyKind>().Where(ToyProfiles.Extended)) {
            check(ToyHaptics.Matches(toy, "Lovense " + ToyProfiles.Label(toy)), toy + " model matching");
            var backend = new Memory();
            backend.Features = ToyProfiles.Channels(toy).Select((c,i) => new HapticFeature(1,(uint)(7+i*3),ToyProfiles.Label(toy),c.Label,c.Type,c.Type == OutputType.Rotate ? -20 : 0,20,0)).ToArray();
            await using var toys = new ToyHaptics(backend); var engine = toys[toy];
            await toys.ActivateAsync(toy,1,.25,.25,true);
            async Task Drain() { for(var j=0;j<5;j++) { await engine.TickAsync(); await Task.Delay(60); } }
            var prefix = "/avatar/parameters/HydroLink/" + toy + "/";
            toys.Receive([new(prefix + "Enabled",1)]);
            var channels=ToyProfiles.Channels(toy);
            for(var i=0;i<channels.Length;i++) toys.Receive([new(prefix+channels[i].Key,.2 + i*.5)]);
            toys.Receive([new("/avatar/parameters/OGB/Orf/Test/PenOthers",.5)]);
            await Drain();
            for(var i=0;i<channels.Length;i++) check(Math.Abs(engine.Snapshot.Sent[backend.Features[i].Key]-(.2+i*.5)*.5)<.0001,toy+" independent SPS "+channels[i].Key);
            engine.SetDesktopManual(true);
            for(var i=0;i<channels.Length;i++) engine.SetActiveChannel(channels[i].Key,.8-i*.5);
            await Drain();
            for(var i=0;i<channels.Length;i++) check(Math.Abs(engine.Snapshot.Sent[backend.Features[i].Key]-(.8-i*.5))<.0001,toy+" independent manual "+channels[i].Key);
            if(toy==ToyKind.Nora) { engine.SetSpsChannel("ManualClockwise",0); await Drain(); check(!backend.Direction,"Nora reverse rotation"); }
            engine.SetDesktopManual(false); await Drain();
            check(engine.Snapshot.Sent.Values.All(x=>x==0),toy+" leaving manual requires fresh contact");
            check(Math.Abs(engine.ActiveChannel(channels[0].Key)-.2)<.0001,toy+" SPS values preserved");
            toys.Receive([new(prefix+"Stop",1)]); await engine.TickAsync();
            check(!engine.Snapshot.Armed,toy+" menu stop disarms");
        }
        check(!ToyHaptics.Matches(ToyKind.LushMini,"Lovense Lush 3"),"Lush Mini cannot auto-select Lush 3");
        check(ToyHaptics.Matches(ToyKind.Dolce,"Lovense Quake"),"Dolce legacy Quake name");
        check(ToyProfiles.Feature(ToyProfiles.Channels(ToyKind.Gemini)[1],[new(1,7,"Gemini","Motor",OutputType.Vibrate,0,20,0)])==null,"Missing second motor is not duplicated");
    }
    private sealed class Memory : IUniversalHapticsBackend {
        public bool Connected => true;
        public IReadOnlyList<HapticFeature> Features {get;set;}=[];
        public bool Direction=true;
        public Task SetFeatureAsync(HapticFeature feature,double amount,bool clockwise,CancellationToken token) { if(feature.Type==OutputType.Rotate) Direction=clockwise;return Task.CompletedTask; }
        public Task StopDeviceAsync(uint id,CancellationToken token)=>Task.CompletedTask;
    }
}
