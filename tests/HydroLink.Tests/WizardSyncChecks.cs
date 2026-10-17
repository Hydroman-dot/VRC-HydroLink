using System.Text;
using System.Text.Json;
using HydroLink.Core;
using VrcHapticsBridge.Core;
using VrcHapticsBridge.Tests;

internal static class WizardSyncChecks
{
    public static async Task Run(Action<bool,string> check, bool network)
    {
        const string avatar="avtr_00000000-0000-0000-0000-000000000001";
        var profile=new WizardProfile(1,Guid.NewGuid().ToString(),avatar,"Test Avatar",["Max2","Ridge"],1,12);
        byte[] Json(WizardProfile p)=>JsonSerializer.SerializeToUtf8Bytes(p,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
        var folder=Path.Combine(Path.GetTempPath(),"hydrolink-wizard-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var file=Path.Combine(folder,"active.json"); var reader=new WizardSyncReader(file);
            check(!reader.Read().Present,"wizard sync missing file preserves manual setup");
            await File.WriteAllBytesAsync(file,Json(profile)); var state=reader.Read();
            check(state.Profile?.MatchesAvatar(avatar)==true && state.Profile.Includes(ToyKind.Ridge),"Unity camelCase manifest loads avatar and toys without import");
            var changed=profile with{Revision=Guid.NewGuid().ToString(),Toys=["Ridge"]};
            var temp=Path.Combine(folder,"next.tmp"); await File.WriteAllBytesAsync(temp,Json(changed)); File.Move(temp,file,true);
            var next=reader.Read();
            check(next.Fingerprint!=state.Fingerprint && next.Profile?.Includes(ToyKind.Max2)==false,"wizard file replacement is detected and changes toy selection");
            await File.WriteAllTextAsync(file,"{broken");
            check(reader.Read() is {Present:true,Profile:null},"malformed wizard handoff blocks automatic profile");
            await File.WriteAllBytesAsync(file,Json(profile with{Toys=["Max2","Max2"]}));
            check(reader.Read().Profile==null,"duplicate toy entries rejected");
            await File.WriteAllBytesAsync(file,Json(profile with{AvatarId="other-avatar"}));
            check(reader.Read().Profile==null,"invalid avatar identity rejected");
            await File.WriteAllBytesAsync(file,Json(profile with{SchemaVersion=99}));
            check(reader.Read().Profile==null,"unknown wizard schema rejected");
            await File.WriteAllTextAsync(file,new string('x',65537));
            check(reader.Read().Profile==null,"oversized wizard file rejected");
            var unbound=WizardSyncReader.Parse(Json(profile with{AvatarId=""}));
            check(!unbound.MatchesAvatar(avatar),"unpublished Unity avatar never binds by guessed name");
            const string secondId="avtr_00000000-0000-0000-0000-000000000002";
            var second=profile with{AvatarId=secondId,Revision=Guid.NewGuid().ToString(),Toys=["Ridge"]};
            var archive=Path.Combine(folder,"Avatars"); Directory.CreateDirectory(archive);
            var firstFile=Path.Combine(archive,avatar+".json"); var secondFile=Path.Combine(archive,secondId+".json");
            await File.WriteAllBytesAsync(firstFile,Json(profile)); await File.WriteAllBytesAsync(secondFile,Json(second));
            await File.WriteAllBytesAsync(file,Json(second));
            var firstState=reader.Read(avatar); var secondState=reader.Read(secondId);
            check(firstState.Profile?.Includes(ToyKind.Max2)==true && secondState.Profile?.Includes(ToyKind.Max2)==false,"multiple avatars select independent toy profiles by identity, even with identical names");
            check(reader.Read(avatar).Fingerprint==firstState.Fingerprint,"switching back restores previous avatar profile without another wizard handoff");
            second=second with{Revision=Guid.NewGuid().ToString(),Toys=["Max2"]};
            await File.WriteAllBytesAsync(secondFile,Json(second));
            check(reader.Read(avatar).Fingerprint==firstState.Fingerprint && reader.Read(secondId).Profile?.Includes(ToyKind.Max2)==true,"updating another avatar does not invalidate current profile");
            check(reader.Read("avtr_00000000-0000-0000-0000-000000000003") is {Present:true,Profile:null},"unknown avatar never falls back to another saved avatar");
            check(reader.Read("../active") is {Present:true,Profile:null},"avatar identity cannot select arbitrary filesystem paths");
            await File.WriteAllTextAsync(secondFile,"{broken");
            check(reader.Read(secondId).Profile==null && reader.Read(avatar).Profile!=null,"damaged avatar profile is isolated and never replaced by stale latest handoff");
            await File.WriteAllBytesAsync(secondFile,Json(profile));
            check(reader.Read(secondId).Profile==null,"saved profile filename and avatar identity must agree");
            File.Delete(file);
            check(reader.Read(avatar).Profile!=null,"saved avatar works even if legacy latest file is missing");
            File.Delete(secondFile); await File.WriteAllBytesAsync(file,Json(second));
            check(reader.Read(secondId).Profile?.MatchesAvatar(secondId)==true,"old single-avatar handoff remains compatible");
            await File.WriteAllBytesAsync(secondFile,Json(second));
            if(!network) return;
            await using var server=new FakeServer(); server.UniversalDevices=true; await server.StartAsync();
            var bus=new DataBus(); await using var service=new HapticsService(new(){IntifaceUrl=server.Url},bus,Console.WriteLine);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25)); await service.StartAsync(timeout.Token);
            while(service.Backend.Features.Count<8) await Task.Delay(25,timeout.Token);
            service.ApplyWizardSync(next,avatar);
            check(!service.WizardAllows(ToyKind.Max2) && service.WizardAllows(ToyKind.Ridge) && !service.Toys[ToyKind.Ridge].Snapshot.Armed,"wizard restricts configured toys without auto-arming");
            bool refused=false; try { await service.ActivateToyAsync(ToyKind.Max2,7,.2,.2,true); } catch(InvalidOperationException){refused=true;}
            check(refused,"unconfigured toy cannot be activated by service");
            await service.ActivateToyAsync(ToyKind.Ridge,9,.2,.2,true);
            service.ApplyWizardSync(state,avatar);
            check(!service.Toys[ToyKind.Ridge].Snapshot.Armed && service.WizardAllows(ToyKind.Max2),"new Unity handoff disarms active toy before applying setup");
            service.ApplyWizardSync(state,"avtr_00000000-0000-0000-0000-000000000000");
            refused=false; try {await service.ActivateToyAsync(ToyKind.Ridge,9,.2,.2,true);} catch(InvalidOperationException){refused=true;}
            check(refused,"wizard profile cannot drive a different avatar");
            bus.Publish("osc/avatar-id",avatar,"test");
            check(service.WizardAllows(ToyKind.Ridge),"live OSC avatar identity automatically resolves wizard binding");
            await service.ActivateToyAsync(ToyKind.Ridge,9,.2,.2,true);
            service.ApplyWizardSync(new(true,null,"invalid","broken"),avatar);
            check(!service.Toys[ToyKind.Ridge].Snapshot.Armed && !service.WizardAllows(ToyKind.Ridge),"invalidated wizard file blocks output and disarms existing session");
            service.ApplyWizardSync(firstState,avatar);
            await service.ActivateToyAsync(ToyKind.Ridge,9,.2,.2,true);
            bus.Publish("osc/avatar-id",secondId,"test");
            service.ApplyWizardSync(reader.Read(secondId),secondId);
            check(!service.Toys[ToyKind.Ridge].Snapshot.Armed && service.WizardAllows(ToyKind.Max2) && !service.WizardAllows(ToyKind.Ridge),"avatar switch stops previous session and adopts only new avatar toys");
            service.ApplyWizardSync(reader.Read(avatar),avatar);
            check(service.WizardAllows(ToyKind.Ridge) && !service.Toys[ToyKind.Ridge].Snapshot.Armed,"returning to saved avatar restores setup without restarting hardware");
        }
        finally
        {
            var resolved = Path.GetFullPath(folder);
            if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("hydrolink-wizard-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup path");
            Directory.Delete(resolved,true);
        }
    }
}
