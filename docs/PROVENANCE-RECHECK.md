# Herkunftsnachprüfung — 01.11.2026

Die Migrationsdokumentation nennt zusätzlich zu VR Fitness und WindForce das lokale VrcHapticsBridge-Projekt. Dessen MIT-Lizenz (Copyright 2026 VRC Haptics Bridge contributors) wurde als `licenses/VrcHapticsBridge-MIT.txt` ergänzt. Die bisherige Zählung von nur zwei Herkunftsprojekten war unvollständig; es sind mindestens drei dokumentierte Quellprojekte.

## Direkter Vergleich des verfügbaren Bridge-Quellstands

Zeilenvergleich gleichnamiger Dateien gegen HydroLink, ohne Leerraum-Normalisierung. Übereinstimmende Zeilen sind kein Prozentnachweis für das Gesamtprojekt. SHA-256 identifiziert die lokale Quelldatei.

| Datei | Quellzeilen | HydroLink-Zeilen | Gleiche Zeilen | Quell-SHA256 |
|---|---:|---:|---:|---|
| Backend.cs | 109 | 113 | 106 | `9ea3c0a613049538884dd42f7fa04f1ee09a9c623e15a579571c7717d220e412` |
| BridgeController.cs | 217 | 217 | 217 | `d0e2ea8cbe6cfd91230702f93d24989f15ad761f4eba259b2b6a5eb682e2dcdd` |
| ReliableWebSocketConnector.cs | 121 | 121 | 121 | `d3ab5bca0159c7623bf0eb6651a2a2b8fae45053ae464fcf7f500e5572f8e4f6` |
| Settings.cs | 42 | 42 | 42 | `53ab7c62678c48f45e622276823780d47680455a66cbe24018d7fc6c042aec9b` |

VR Fitness und WindForce wurden sprachübergreifend portiert; ihre Herkunft und damaligen Revisionen stehen in MIGRATION.md und SOURCE-REVISIONS.txt. Eine automatisierte Textähnlichkeit zwischen Python und C# kann keine vollständige Herkunftsfreiheit beweisen. Eine vollständige manuelle Abschnittsprüfung sämtlicher historischer Änderungen ist weiterhin nicht abgeschlossen.

## Windows-Binärdateien

Das lokale SDK-Paket 10.0.19041.57 weist in data/RuntimeList.xml Microsoft.Windows.SDK.NET.dll und WinRT.Runtime.dll als Laufzeitdateien aus. Beide stehen auch in der win-x64-Build-Dateiliste. Die C#/WinRT-Runtime besitzt MIT-Hinweise; die Windows-Projektion wird nicht pauschal als MIT bezeichnet.

Die Build-Dateiliste enthält außerdem coreclr.dll, PresentationNative_cor3.dll und D3DCompiler_47_cor3.dll. Microsoft ordnet die Windows-Runtime einschließlich Single-File-Paketen und bestimmte native WPF-Dateien der .NET Library License zu, D3DCompiler_47_cor3.dll den Windows-SDK-Bedingungen. Der zusätzliche .NET-Lizenztext ist jetzt beigefügt.

Die Microsoft-Bedingungen erlauben Weitergabe im Rahmen eigener Programme unter Bedingungen. Die Anforderungen an eine ausreichende Endnutzervereinbarung bei portablen ZIPs sind mit dem bloßen Beilegen der Texte nicht abschließend nachgewiesen. Daher keine pauschale rechtliche Freigabe; dieser konkrete Punkt bleibt klärungsbedürftig.

Quellen:
- https://github.com/dotnet/core/blob/main/license-information-windows.md
- https://dotnet.microsoft.com/en-us/dotnet_library_license.htm
- https://learn.microsoft.com/en-us/legal/windows-sdk/license
- https://learn.microsoft.com/en-us/legal/windows-sdk/redist
- https://github.com/microsoft/WindowsAppSDK/discussions/4368
