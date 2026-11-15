using System.Text.Json;
using HydroLink.Sdk;
namespace HydroLink.Core;
public static class OscSnapshots
{
    public static void Publish(IDataBus bus,JsonElement tree,string avatar,DateTimeOffset queryStarted)
    {
        if(string.IsNullOrWhiteSpace(avatar)) return;
        foreach(var item in OscMonitor.ParseTree(tree).Take(8192)) {
            if(item.Value=="—") continue;
            using var values=JsonDocument.Parse(item.Value);
            if(values.RootElement.ValueKind==JsonValueKind.Array && values.RootElement.GetArrayLength()>0)
                bus.Publish("osc/query-value"+item.Address,new { avatar,queryStarted,value=values.RootElement[0] },"osc-query");
        }
    }
}
