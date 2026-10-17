# Architecture and migration

`HydroLink.App` is the native WPF shell. `HydroLink.Core` owns module selection, provider lifetime, reconnect loops, OSC and external services. `HydroLink.Sdk` contains stable minimal contracts. `tests/HydroLink.Tests` is an executable integration harness, requiring no external test runner. The example plugin demonstrates an independent source.

## Inspected sources

- Hydroman-dot/VR-Fitness: `releases/V11.17/vr_fitness_v11_17_preview.pyw`, README, Android guide and MIT license.
- Hydroman-dot/VRC-Windforce: README, `docs/protocol.md`, ESP32 firmware and MIT license. The complete PC application was decoded from the concatenated base64/gzip bootstrap parts and inspected. Core mapping/fade behavior has been ported; remaining gaps are listed in FEATURE-PARITY.md.
- vrchat-community/vrc-oscquery-lib: official C# OSCQuery service/discovery implementation. Consumed as NuGet `VRChat.OSCQuery` 0.0.7.
- buttplugio/buttplug-csharp: official client API/examples. Consumed as NuGet `Buttplug` 5.0.1.

Exact inspected commits are recorded in `SOURCE-REVISIONS.txt`.

## Migrated behavior

VRTI sends EventSubscribe with a JSON-encoded payload string and requests initial treadmill/statistics state. It reads JSON-string or object payloads, converts distance meters to kilometers, and publishes speed, steps and active time. FitOSC reads state/telemetry metric objects and converts MPH/MILES. VRCOSC recognizes numeric and nested pulse payloads without application pings. Pulsoid uses its realtime token endpoint. HypeRate joins `hr:{id}` and sends Phoenix heartbeats. All reconnect loops cancel on host stop.

WindForce uses firmware-compatible `FAN percent sequence` commands on 4210. A stale OSC signal forces zero; shutdown attempts an immediate zero command. The ESP32 firmware provides an additional communication timeout. The parameter name is explicitly configurable: this preview does not silently treat PhysBone angle as a calibrated wind value.

Health Companion uses the existing `X-VR-Fitness-Code` header for `/status`. Pairing secrets use Windows DPAPI. Session payload construction, `/session` synchronization and the offline queue are implemented and tested with simulated acknowledgments. Real Android interoperability and a new local WebSocket/HTTP server remain pending.

## OSCQuery design

The receiver binds UDP before publishing its actual port. A separate free TCP port hosts OSCQuery. Discovery filters VRChat names and validates HOST_INFO. Sending never assumes 9000. HOST_INFO is refreshed and an unreachable target is cleared. Manual mode is explicit. One bridge is shared across built-in consumers.

Known hardening work: selection among multiple VRChat instances; handling a same-name mDNS service changing its query port without a goodbye; a bounded discovered-service cache; explicit richer provider connection states; more OSC atom types; full avatar namespace/routing editor. Runtime errors are logged and are not presented as successful connections.

## Version 0.2.0

The host now reconciles per-module consumers when a tool starts or stops. Shared components retain their identity until their last consumer stops; failure while adding a module rolls back only its new components. An injected provider factory/program launcher supports deterministic lifecycle tests.

Custom dashboards persist typed card descriptors (topic, title, unit, representation, width, order). The bus stores no history; the UI keeps bounded chart windows. Program autostart belongs to source acquisition, not to the dashboard. Actual process startup is gated by an explicitly enabled per-provider setting and a configured EXE or the known VRTI Steam URI.

Sources also publish `sources/{provider}/{metric}` for independent tool selection. WindForce has source-specific calibration, priority, fades and device discovery/status; original data models and algorithms remain the reference. Sessions write semicolon CSVs with original column names; the analyzer accepts both semicolon and prior-preview comma files and reconstructs older session totals.

## Remaining migration

See [FEATURE-PARITY.md](FEATURE-PARITY.md) for the complete acceptance matrix. Calorie profiles, goals, automatic sessions, Health upload queue and backup UI are implemented. Open work includes original PDF layout, experimental Pulsoid widgets, full WindForce calibration conveniences/diagnostics, plugin-requested built-in dependencies, installer validation and hardware acceptance. Version 0.3.0 must not be described as fully equivalent to the originals.

## Version 0.3.0

OscMonitor stores at most 8192 addresses and distinguishes query, incoming UDP and outgoing UDP values. The shared bridge advertises /avatar, reads the discovered VRChat tree and invalidates old values on avatar changes. OscRouting persists typed routes, handles per-avatar activation and refuses avatar feedback chains. Only incoming data events activate routes; query snapshots do not actuate devices.

Haptics adopts Backend, BridgeController, ReliableWebSocketConnector and BridgeSettings from the user's local VrcHapticsBridge project. Its separate socket and OSCQuery server are intentionally not included. HapticsService consumes the host bus and shares the host OSC bridge. The Unity prefab remains a separate artifact and is unchanged. A simulated protocol-v4 server verifies real WebSocket messages and advertised feature indices.
