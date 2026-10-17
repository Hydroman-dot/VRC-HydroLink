# Verification

Version 0.10 development preview: 170 checks passed locally, including simulated WebSocket devices, OSCQuery discovery, isolated multi-avatar profiles and the optional live Unity handoff checks. Without the two local-profile checks, the full network suite contains 168 checks. CI does not require a personal Unity project or live handoff file.

Unity 2022.3.22f1: both toy prefabs and SDK avatar preprocessing validated with 16 local, unsaved parameters. Multi-avatar export, legacy handoff migration and isolated profile updates tested in the editor. Required packages in that test environment: Modular Avatar 1.18.2, VRCFury 1.1426.0 and VRChat SDK 3.10.5. These are tested versions, not a guarantee for all future package combinations.

Real Max 2/Ridge hardware, clean-machine installer and a full live VRChat session remain to be verified. The app is an unsigned development preview.
