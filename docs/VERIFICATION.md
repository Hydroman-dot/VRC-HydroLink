# Verification

Version 0.10 development preview: 170 checks passed locally, including simulated WebSocket devices, OSCQuery discovery, isolated multi-avatar profiles and the optional live Unity handoff checks. Without the two local-profile checks, the full network suite contains 168 checks. CI does not require a personal Unity project or live handoff file.

Unity 2022.3.22f1: both toy prefabs and SDK avatar preprocessing validated with 16 local, unsaved parameters. Multi-avatar export, legacy handoff migration and isolated profile updates tested in the editor. Required packages in that test environment: Modular Avatar 1.18.2, VRCFury 1.1426.0 and VRChat SDK 3.10.5. These are tested versions, not a guarantee for all future package combinations.

Real Max 2/Ridge hardware, clean-machine installer and a full live VRChat session remain to be verified. The app is an unsigned development preview.

0.10.1: Build completed with zero warnings/errors; 173 checks passed with network and live handoff checks. Added activation regression checks for the VRCFury OGB receiver request on both toy profiles. The OSC network test now ignores unrelated datagrams from a simultaneously running VRChat client. Read-only diagnosis in the live client showed OGB_ENABLED=false and zero contact values during the reported test; installed VRCFury OgbEnabledService confirms that this flag gates its contact receiver objects. Physical response after installing 0.10.1 remains to be confirmed by the user.

0.11.0 Solace Pro preparation: build passed with no warnings or errors; 183 checks passed including network tests (without live Unity handoff). Added tests for model matching, selection of only one output type per physical motor, radial gate, SPS socket/plug speed, separate manual values, desktop speed, stopping, three-toy handoff, real WebSocket Oscillate command scaling and stop isolation during simultaneous operation. Unity editor sources are prepared but not compiled or exercised in Unity for this version. Physical Solace Pro testing remains outstanding.

0.12.0: build and publish completed without warnings/errors; 200 checks passed including network simulations. New tests cover bounded SPS positions, configurable duration, explicit manual zero target, separate manual limits, alternating software stroke endpoints, timing, zero speed stop, crossed bounds, switching back to native oscillation, radial stop without a zero-position command, unavailable positioning, and the real WebSocket command encoding/range checks for position and duration. No physical Solace Pro commands were sent. Unity sources remain prepared but editor validation and avatar setup are pending.

0.12.1: 206 checks passed, including six new network checks covering a peer started before HydroLink and after HydroLink. The peer queries the first discovered receiver tree, verifies avatar addresses are already present, resolves HOST_INFO and delivers an actual UDP message; HydroLink also discovers the outgoing target in both orders. Build/publish passed with no warnings or errors. Actual VRChat startup-order confirmation remains pending.

0.12.2: 212 checks passed including network simulations. Added occupied-port fallback, stale and wrong-avatar gate rejection, fresh post-arm gate initialization, live radial off priority, and stopped-hardware protection. Query snapshots initialize only Enabled after explicit desktop activation; manual and contact values are never replayed. Live read-only diagnosis observed VRChat sending to free UDP 9001 while its log lacked the current HydroLink discovery registration. No physical device activation was performed; real-world confirmation remains pending.

0.12.3: 216 checks passed, including explicit desktop manual operation with radial gate off, stopping manual output, radial-off cancellation, and prevention of restarting a stopped device. WPF preview inspected. Plugin test now has its own timeout instead of inheriting the earlier network-test deadline. Real hardware test remains pending.

0.12.4: 226 checks passed including concurrent worker starts/disposal/restart, non-finite OSC rejection, atomic settings replacement, recovery from damaged JSON with warning and preservation of backup, and concurrent saves. Build and publish completed without warnings/errors. Hardware longevity and power-loss tests remain pending.

0.12.5: 231 checks passed. Added isolated module restart, shared OSC receiver replacement with output invalidation, cache selection, unapproved path rejection, and backup/unrelated-file preservation tests. Cache tests use temporary fixtures only; real VRChat files untouched. Module and OSC Hub WPF previews inspected; build/publish passed.

0.12.6: Added persisted per-module hide-when-inactive settings. Active modules and shared active OSC remain visible; hidden selected tabs return to the module overview. Build/publish passed without warnings/errors and settings-page WPF preview inspected. No new hardware behavior; network suite was not rerun for this display-only change.

0.13.0: 233 checks passed, including visible worker faults and unexpected completion. Build/publish passed; diagnosis page inspected through non-actuating WPF preview. Central diagnostics and module states added. Physical long-run testing remains pending.
