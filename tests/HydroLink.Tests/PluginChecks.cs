using System.IO.Compression;
using System.Text.Json;
using HydroLink.Core;
using HydroLink.Sdk;

public static class PluginChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var folder=Path.Combine(Path.GetTempPath(),"hydrolink-plugin-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try {
            var catalog=new PluginCatalog(Path.Combine(folder,"store"),_=>{});
            string Package(string id,string version,string? badPath=null,int api=1) {
                var path=Path.Combine(folder,Guid.NewGuid()+".zip");
                using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
                using(var writer=new StreamWriter(zip.CreateEntry("plugin.json").Open())) writer.Write(JsonSerializer.Serialize(new PluginManifest(id,id,"HydroLink.Tests.dll",version,api,EntryType:id=="fixture.one"?typeof(PluginOne).FullName:typeof(PluginTwo).FullName),PluginManifest.Json));
                zip.CreateEntryFromFile(typeof(PluginChecks).Assembly.Location,"HydroLink.Tests.dll");
                if(badPath is not null) { using var writer=new StreamWriter(zip.CreateEntry(badPath).Open()); writer.Write("bad"); }
                return path;
            }
            catalog.Install(Package("fixture.one","1.0.0")); catalog.Install(Package("fixture.two","1.0.0"));
            check(catalog.Discover().Count==2,"two plugins installed independently");
            catalog.Install(Package("fixture.one","1.1.0"));
            check(catalog.Discover().Single(x=>x.Manifest.Id=="fixture.one").Manifest.Version=="1.1.0","new version selected");
            catalog.Rollback("fixture.one");
            check(catalog.Discover().Single(x=>x.Manifest.Id=="fixture.one").Manifest.Version=="1.0.0","rollback selects preserved version");
            foreach(var bad in new[]{"../outside.txt","/outside.txt","folder/../../outside.txt","C:/outside.txt","nested/file:stream"}) {
                bool rejected=false; try { catalog.Install(Package("fixture.bad","1.0.0",bad)); } catch(InvalidDataException) { rejected=true; }
                check(rejected,"unsafe ZIP path rejected: "+bad);
            }
            bool mismatch=false; try { catalog.Install(Package("fixture.one","1.2.0"),"fixture.other","1.2.0",null); } catch(InvalidDataException){ mismatch=true; }
            check(mismatch && catalog.Discover().Single(x=>x.Manifest.Id=="fixture.one").Manifest.Version=="1.0.0","identity mismatch preserves active version");
            bool incompatible=false; try { catalog.Install(Package("fixture.bad","1.0.0",api:2)); } catch(InvalidDataException){ incompatible=true; }
            check(incompatible,"incompatible API rejected before loading code");
            catalog.Install(Package("fixture.one","1.1.0"));
            check(catalog.Discover().Single(x=>x.Manifest.Id=="fixture.one").Manifest.Version=="1.1.0","retained identical version can be reselected after rollback");
            var bus=new DataBus(); var outputs=new List<string>(); using var sub=bus.Subscribe("fixture/output",e=>outputs.Add(e.Source));
            await using var runtime=new PluginRuntime(bus,Console.WriteLine);
            foreach(var entry in catalog.Discover()) await runtime.StartAsync(entry,CancellationToken.None);
            bus.Publish("fixture/input",1,"test");
            check(runtime.Running.Count==2 && outputs.Count==2,"two active plugins receive shared bus events");
            await runtime.StopAsync("fixture.one"); outputs.Clear(); bus.Publish("fixture/input",2,"test");
            check(outputs.SequenceEqual(new[]{"fixture.two"}),"stopping one plugin preserves the other and removes subscriptions");
            await runtime.StartAsync(catalog.Discover().Single(x=>x.Manifest.Id=="fixture.one"),CancellationToken.None); outputs.Clear(); bus.Publish("fixture/input",3,"test");
            check(outputs.Count==2,"plugin restarts with fresh components");
            await runtime.DisposeAsync(); outputs.Clear(); bus.Publish("fixture/input",4,"test"); check(outputs.Count==0,"all plugin subscriptions removed on shutdown");
            var osc=new PluginHostProbe();
            await using(var host=new HydroHost(new Settings(),_=>{},factory:_=>osc)) {
                var needsOsc=catalog.Discover().Single(x=>x.Manifest.Id=="fixture.one");
                needsOsc=needsOsc with { Manifest=needsOsc.Manifest with { RequiresOsc=true } };
                await host.SetPluginAsync(needsOsc,true);
                await host.SetModuleAsync("osc",false);
                check(host.Running.Contains("osc") && osc.Starts==1,"plugin holds shared OSC dependency without duplicate receiver");
                await host.SetPluginAsync(needsOsc,false);
                check(!host.Running.Contains("osc") && osc.Stops==1,"last plugin releases unused OSC dependency");
            }
            using var http=new HttpClient(new ReleaseFixtureHandler());
            var entryForUpdate=catalog.Discover()[0] with { Manifest=catalog.Discover()[0].Manifest with { GitHubRepository="owner/plugins" } };
            var update=await new PluginUpdates(http).CheckAsync(entryForUpdate,CancellationToken.None);
            check(update?.Version=="2.0.0","GitHub release selects versioned ZIP and checksum");
            var download=Path.Combine(folder,"download.zip");
            using var badHttp=new HttpClient(new DownloadFixtureHandler(false));
            bool corrupt=false; try { await new PluginUpdates(badHttp).DownloadAsync(update!,download,CancellationToken.None); } catch(InvalidDataException) { corrupt=true; }
            check(corrupt,"checksum mismatch rejects download");
            using var goodHttp=new HttpClient(new DownloadFixtureHandler(true));
            await new PluginUpdates(goodHttp).DownloadAsync(update!,download,CancellationToken.None);
            check(File.ReadAllText(download)=="fixture bytes","verified download succeeds");
        } finally { try { Directory.Delete(folder,true); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { /* Loaded test assemblies remain locked until process exit. */ } }
    }
    private sealed class DownloadFixtureHandler(bool valid) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
            var content=request.RequestUri!.AbsolutePath.EndsWith(".sha256") ? (valid ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("fixture bytes"))) : new string('0',64)) : "fixture bytes";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=new StringContent(content) });
        }
    }
    private sealed class ReleaseFixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=new StringContent("""{"draft":false,"prerelease":false,"tag_name":"v2.0.0","assets":[{"name":"fixture.one-2.0.0.zip","browser_download_url":"https://github.com/owner/plugins/releases/download/v2.0.0/fixture.one-2.0.0.zip"},{"name":"fixture.one-2.0.0.zip.sha256","browser_download_url":"https://github.com/owner/plugins/releases/download/v2.0.0/fixture.one-2.0.0.zip.sha256"}]}""") });
    }
}
public sealed class PluginOne : IHydroLinkPlugin
{
    public string Id=>"fixture.one"; public string Name=>Id; public int ApiVersion=>1;
    public void Configure(IPluginContext context)=>context.Register(new PluginProbe(Id,context.Bus));
}
public sealed class PluginTwo : IHydroLinkPlugin
{
    public string Id=>"fixture.two"; public string Name=>Id; public int ApiVersion=>1;
    public void Configure(IPluginContext context)=>context.Register(new PluginProbe(Id,context.Bus));
}
public sealed class PluginProbe(string id,IDataBus bus) : IComponent
{
    public string Id=>id; public ComponentKind Kind=>ComponentKind.Processor;
    public Task StartAsync(CancellationToken token) { bus.Subscribe("fixture/input",e=>bus.Publish("fixture/output",1,id)); return Task.CompletedTask; }
    // Deliberately omit disposing subscription: the host must still clean it up.
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}

public sealed class PluginHostProbe : IComponent
{
    public string Id=>"osc"; public ComponentKind Kind=>ComponentKind.Service; public int Starts,Stops;
    public Task StartAsync(CancellationToken token) { Starts++; return Task.CompletedTask; }
    public ValueTask DisposeAsync() { Stops++; return ValueTask.CompletedTask; }
}
