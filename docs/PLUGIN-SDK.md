# HydroLink Plugin SDK — preview API 1

Reference `HydroLink.Sdk`. Implement `IHydroLinkPlugin`, expose a stable unique `Id`, `Name` and `ApiVersion = 1`. `Configure` registers components and pages; it must not open connections. The host calls `StartAsync` only for explicitly enabled plugins and awaits `DisposeAsync` during shutdown. Respect cancellation and stop all external outputs before disposal completes.

Each folder below `plugins/` beside the EXE has a manifest:

```json
{"id":"example.counter","name":"Example Counter","assembly":"ExamplePlugin.dll"}
```

Build the included example, then copy its output folder into `plugins/example.counter`. Add `example.counter` to enabled plugin IDs in the Plugins page and save. Restart the application, then start modules. Include the plugin `.deps.json` and dependency DLLs; the host shares its SDK assembly and resolves private dependencies from the plugin directory.

```powershell
dotnet build examples/ExamplePlugin -c Release
```

## Generic event/data bus

```csharp
context.Bus.Publish("myvendor/device/temperature", new { celsius = 22.5 }, Id);
var subscription = context.Bus.Subscribe("fitness", evt =>
{
    // evt.Topic, evt.Value (JsonElement), evt.Source, evt.Timestamp
});
// Dispose subscriptions at component shutdown.
```

Any JSON-serializable payload and new topic are allowed; there is no closed enum of topics. Prefix subscriptions match topic boundaries: `fitness` matches `fitness/heart/bpm` but not `fitnessExtra`. An empty prefix receives everything. The bus dispatches synchronously on the publishing thread, without replay or unbounded queues. Handlers must remain fast; perform heavy processing through a bounded worker queue. Exceptions are isolated and logged. UI callbacks must marshal through the WPF Dispatcher. Do not republish to your own input topic in a synchronous loop.

Current built-in topics (0.2 also adds `sources/{provider}/{metric}`, `provider/{provider}/status`, `session/*`, and `windforce/device/*`):

| Topic | Payload |
|---|---|
| `fitness/heart/bpm` | number, beats/minute |
| `fitness/speed-kmh` | number, km/h |
| `fitness/distance-km` | number, km |
| `fitness/steps` | number |
| `fitness/active-seconds` | number |
| `provider/{provider}/raw` | raw JSON message |
| `osc/receive/avatar/parameters/{name}` | `{address, arguments}` |
| `osc/send` | `{address, arguments}`; scalar int/float/string/bool |
| `windforce/output-percent` | number 0–100 |
| `haptics/devices` | array of device names/indices |
| `companion/status` | existing companion status JSON |

Topics are transient; consumers must track freshness. Cloud raw events should not be persisted without user intent. The host UI ignores raw provider payloads.

## Components and pages

`IComponent.Kind` can be Source, Processor, Output or Service. Register multiple components in `Configure`. IDs must be unique. Components start in registration order and stop in reverse order. This preview has no dependency graph between plugin components: a plugin coordinates its own dependencies. Built-in provider selection is controlled by module settings, and the host deduplicates shared built-in OSC use. Plugins can consume already-active built-in topics but cannot yet request built-in providers dynamically.

For UI, target `net8.0-windows` with `UseWPF=true` and register a `PluginPage("Title", () => new MyUserControl())`. The factory runs on the UI thread; the returned object must be a WPF FrameworkElement. The SDK itself remains independent of WPF.

Only enabled plugins are loaded. Disabled manifests may be listed without loading their assemblies. Enabled plugins have the full process privileges of HydroLink; this is not a security sandbox. Do not install untrusted DLLs. There is no hot-unload; restart to change installed plugin binaries. Network errors must not crash the host or leave motors running.

## Version 0.3 topics

- `osc/value/avatar/parameters/{name}`: first received OSC argument; useful for dashboard cards.
- `osc/sent-value/avatar/parameters/{name}`: first successfully sent UDP argument, not a device or avatar acknowledgment. Routing ignores sent topics to prevent local feedback.
- `osc/avatar-id`: current avatar identity from UDP or query data; resets routes/device output state.
- `haptics/input/vibrate`, `haptics/input/pump`: normalized scalar inputs, accepted only when Haptics output is explicitly armed.
- `haptics/state`, `haptics/vibrate-sent`, `haptics/pump-sent`: controller snapshot and command values.
- `windforce/input/mapped`: normalized 0–1 input for the enabled Direct OSC WindForce rule.
- `routing/reset`: configuration/avatar invalidation; publishers should normally leave this to the host.
