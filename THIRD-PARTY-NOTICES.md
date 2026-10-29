# Third-party notices

Protocol adapters were ported/adapted from VR Fitness (MIT, copyright 2026 martinhesmer3-alt) and VRC WindForce (MIT, copyright 2026 Hydroman-dot). Their complete licenses are included in `licenses/`.

The application uses VRChat.OSCQuery 0.0.7 (MIT), MeaMod.DNS 1.0.70, Buttplug 5.0.1 (BSD-3-Clause), Newtonsoft.Json, Microsoft.Extensions.Logging.Abstractions, System.Security.Cryptography.ProtectedData and their transitive dependencies. Included license files are taken from the inspected upstream repositories or installed NuGet packages. The self-contained runtime also includes Microsoft .NET runtime components and their license/third-party notices.

Upstream locations:

- https://github.com/Hydroman-dot/VR-Fitness
- https://github.com/Hydroman-dot/VRC-Windforce
- https://github.com/vrchat-community/vrc-oscquery-lib
- https://github.com/buttplugio/buttplug-csharp

VRChat and Intiface are separate products. HydroLink is not an official VRChat application.

MQTTnet 4.3.7.1207 (MIT), https://github.com/dotnet/MQTTnet, is used for Home Assistant MQTT. License included in licenses/MQTTnet.txt.

## Inventory update 0.16.1

See docs/LICENSE-AUDIT.md and docs/DEPENDENCY-INVENTORY.csv for scope, versions, provenance and remaining checks. Microsoft.Extensions.Logging.Abstractions (MIT) and C#/WinRT (MIT) texts have been added. The Windows SDK .NET targeting pack points to separate Microsoft Windows SDK terms, included as licenses/Windows-SDK-License.rtf; it is not labelled MIT here. HydroLink itself has no selected top-level source license yet. VRCOSC's public heartrate parameter contract was inspected; its GPL implementation and prefabs are not bundled.
