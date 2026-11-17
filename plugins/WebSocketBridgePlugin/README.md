# HydroLink WebSocket Bridge (preview 0.1)

Optional plugin for **VRC HydroLink** (Plugin SDK API 1): exposes existing `fitness/heart/bpm` measurements to local apps. No modifications to HydroLink.Core or VRChat OSC, and no second Bluetooth/sensor connection.

## Endpoints

- `ws://localhost:8766/hydrolink` – real-time JSON messages, 8 simultaneous clients maximum.
- `http://localhost:8766/hr` – latest fresh BPM as `{"bpm":128,"measured_at":"..."}` for Beat Saber's HRCounter WebRequest.
- `http://localhost:8766/health` – local health information, no BPM.

All endpoints are **localhost-only**. The plugin keeps no permanent history. Sensor values more than 10 seconds old are never served through `/hr` (HTTP 503 instead). WebSocket clients receive an explicit `"stale"` status. HydroLink's synchronous internal data bus is not blocked by network I/O; a bounded queue drops older messages if clients fall behind.

## Build / install (Windows)

Requires .NET SDK 8+ and the HydroLink source repository.

```powershell
dotnet publish plugins/WebSocketBridgePlugin/WebSocketBridgePlugin.csproj -c Release -o "$env:TEMP\hydrolink.websocket-bridge"
```

1. Exit HydroLink.
2. Copy **all published files** (DLL, manifest, `.deps.json`, configuration and dependencies) into `plugins/hydrolink.websocket-bridge/` next to `VRC HydroLink.exe`.
3. Start HydroLink. Under **Einstellungen → Plugins**, enable `hydrolink.websocket-bridge`, save, then restart the app after first enabling the plugin.
4. Start HydroLink's **Heartrate** module (or another producer of `fitness/heart/bpm`) and the enabled plugin host.
5. Open `http://localhost:8766/health` in the browser. With a live pulse, `http://localhost:8766/hr` returns JSON.

The included GitHub Actions workflow builds a ZIP artifact already containing the folder `hydrolink.websocket-bridge` to copy next to the EXE.

## Beat Saber / HRCounter setup

The upstream HRCounter mod supports a `WebRequest` data source with a configurable `FeedLink`.

Set:
- **DataSource** = `WebRequest`
- **FeedLink** = `http://localhost:8766/hr`

Use the HRCounter in-game settings where available, or edit `Beat Saber/UserData/HRCounter.json` while Beat Saber is closed. This is **not** the HRCounter `HttpServer` mode and requires no POST adapter. HRCounter's `ReplayRecordHr` currently defaults to `true`; switch it off if you do not wish to embed your heart rate in replays.

Upstream instructions: https://github.com/qe201020335/HRCounter#other-sources

## WebSocket protocol (read-only)

Measurement:

```json
{"type":"measurement","topic":"fitness/heart/bpm","value":128,"unit":"bpm","status":"live","timestamp":"2026-11-02T11:00:00+00:00"}
```

No data / stale:

```json
{"type":"status","topic":"fitness/heart/bpm","value":null,"unit":"bpm","status":"stale","timestamp":"2026-11-02T11:00:15+00:00"}
```

On connection, clients immediately receive a snapshot. The server accepts no commands and disconnects clients that send messages.

## Settings

Edit `bridge.settings.json` beside the deployed plugin, then restart HydroLink:

```json
{"Port":8766,"StaleAfterSeconds":10}
```

Port: 1024–65535. Stale timeout: 2–300 seconds. The bind address always remains `localhost`. If Windows HTTP.sys rejects the URL reservation or the port is occupied, the plugin logs a startup failure.

## Testing / known limitations

- The source module must be active; HydroLink SDK API 1 cannot start built-in providers on demand.
- HRCounter may retain the previously displayed value during source outages, depending on its own timeout behavior. The HTTP source correctly returns 503 once readings are stale.
- The plugin presently has JSON settings rather than a WPF configuration page.
- On Windows test: /health, /hr live and stale, a WebSocket client, reconnection, plugin disable/stop, and app shutdown. GitHub CI compiles the plugin, but a live HRCounter/Coospo hardware test is still needed.
