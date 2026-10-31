# Herkunft und Lizenzen — aktualisiert 31.10.2026

Geprüft: Projektverweise, aufgelöste NuGet-Metadaten, veröffentlichte Windows-Abhängigkeiten, mitgelieferte Lizenztexte, vorhandene Migrationsdokumentation und Pfade aller drei Unity-Pakete. Quellbasis 47b412c (Bluetooth-Vorbereitung), ergänzt um Zeitkorrektur 0.16.1. Dies ist ein technisches Inventar, keine vollständige juristische Freigabe oder forensischer Ähnlichkeitsvergleich sämtlicher historischer Quellen.

## Wie viel fremder Code?

- **Zwei ausdrücklich dokumentierte Herkunftsprojekte für portierte/adaptierte Funktionen:** VR Fitness und VRC WindForce. Das sind Projekte unter Hydroman-dot bzw. mit dem Copyright-Namen martinhesmer3-alt. Diese Herkunft bleibt auch beim Portieren nach C# relevant.
- **Fünf externe Nicht-Microsoft-Bibliotheken mit Laufzeitcode:** VRChat.OSCQuery, MeaMod.DNS, Buttplug, MQTTnet und Newtonsoft.Json. Dazu Microsoft.Logging, Microsoft/.NET-Laufzeit und Windows-Bluetooth-Projektionen.
- **Vier direkte NuGet-Verweise** im Core-Projekt: VRChat.OSCQuery, Buttplug, MQTTnet und ProtectedData.
- Der aufgelöste NuGet-Graph enthält **82 Pakete** einschließlich transitiver, historischer Framework-, Build- und plattformspezifischer Referenzen. Das bedeutet nicht 82 zusätzlich ausgelieferte Bibliotheken: Im Windows-deps-Manifest haben sechs normale Pakete konkrete Laufzeitassets. Hinzu kommen drei Runtime-Packs (.NET, WPF, Windows SDK). Vollständige Paketliste: [DEPENDENCY-INVENTORY.csv](DEPENDENCY-INVENTORY.csv).
- Bei Quellstand 47b412c: **85 versionierte .cs/.xaml/.ps1-Dateien mit 7.999 Zeilen**, einschließlich Tests, Unity-Wizard, Beispielplugin und Updater. Diese Zahl misst den eigenen Repository-Bestand, nicht die intern gebündelten Bibliotheken.
- **Eine seriöse Prozentzahl „x % fremder Quellcode“ lässt sich daraus nicht ableiten.** Portierte Algorithmen wurden umgeschrieben; NuGet-Bibliotheken werden binär gebündelt; eine Volltext-Provenienzanalyse aller ursprünglichen Dateien liegt nicht vor. Es wäre falsch, die restlichen Zeilen pauschal als garantiert originär zu deklarieren.

## Herkunft und Lizenz

| Bestandteil | Urheber / Herkunft | Lizenz / Befund |
| --- | --- | --- |
| VR Fitness: Adapter, Fitness-/Sessionverhalten, HA-v1-Protokoll | martinhesmer3-alt, Repository Hydroman-dot/VR-Fitness | MIT; Text unter licenses/VR-Fitness-MIT.txt |
| VRC WindForce: Protokoll, Mapping-/Ausklangverhalten | Hydroman-dot | MIT; licenses/VRC-Windforce-MIT.txt |
| VRChat.OSCQuery 0.0.7 | VRChat Inc. / VRChat Developers | MIT; licenses/OSCQuery-MIT.txt |
| MeaMod.DNS 1.0.70 | James Weston / MeaMod Group | MIT; licenses/MeaMod.DNS-MIT.txt |
| Buttplug 5.0.1 | Nonpolynomial Labs, LLC | BSD-3-Clause; licenses/Buttplug.txt |
| MQTTnet 4.3.7.1207 | .NET Foundation und Mitwirkende | MIT; licenses/MQTTnet.txt |
| Newtonsoft.Json 13.0.4 | James Newton-King | MIT; licenses/Newtonsoft.Json.txt |
| Microsoft.Extensions.Logging.Abstractions 6.0.2 | Microsoft / .NET-Mitwirkende | NuGet: MIT; licenses/Microsoft-Logging-MIT.txt |
| System.Security.Cryptography.ProtectedData 8.0.0 | Microsoft | MIT; licenses/Microsoft-ProtectedData.txt; kann durch Runtime-Pack bereitgestellt werden |
| .NET/WPF Runtime 8.0.31 | Microsoft / .NET Foundation und Drittanbieter | MIT plus enthaltene ThirdPartyNotices, beide unter licenses/ |
| Windows SDK .NET Ref 10.0.19041.57 | Microsoft | Paket verweist auf Windows-SDK-Lizenz, **nicht pauschal MIT**; offizieller Text als licenses/Windows-SDK-License.rtf ergänzt |
| WinRT.Runtime / C#/WinRT | Microsoft | Upstream MIT, licenses/CsWinRT-MIT.txt; vom Windows-SDK-Pack mitgebracht |

Früher geprüfte Revisionen der ersten vier Herkunftsprojekte stehen in [SOURCE-REVISIONS.txt](SOURCE-REVISIONS.txt). Lizenzangaben in der CSV sind die wörtlichen Paketdeklarationen; historische licenseUrl-Einträge sind keine einzeln juristisch geprüften Lizenzzuordnungen.

## VRCOSC, Bluetooth-Modul und Unity

VRCOSC und WentTheFox/VRCOSC-BluetoothHeartrate stehen unter GPL-3.0. In dieser Vorbereitung wurden deren öffentliche Parameternamen, Datentypen und Verhalten abgeglichen. Der neue HydroLink-Bluetoothzugriff, Paketparser und Ausgabealgorithmus wurden separat implementiert; es wurden keine Dateien dieser Projekte kopiert und keine ihrer DLLs eingebunden. Im Paketgraphen ist keine VRCOSC-/Bluetooth-Heartrate-Abhängigkeit vorhanden. Ein solcher Protokollabgleich ist kein Beleg dafür, dass fremder GPL-Code eingebaut wurde; umgekehrt ersetzt diese Prüfung keinen vollständigen Ähnlichkeits-/Lizenzaudit des gesamten Verlaufs.

Die drei geprüften .unitypackage-Dateien enthalten ausschließlich Pfade unter `Assets/VRC HydroLink Avatar Toolkit` (2, 62 bzw. 18 Einträge). VRChat SDK, Modular Avatar, VRCFury/SPS, Rindo und das VRCOSC-Heartrate-Prefab sind nicht mitgeliefert. Der Wizard referenziert installierte Komponenten. Deren getrennte Lizenzen/Asset-Nutzungsbedingungen gelten bei ihrer eigenen Installation und Weitergabe. Es gibt keinen Nachweis einer mitgelieferten Rindo- oder VRCFury-Implementierung.

## Pflichten und offene Punkte

1. **Erledigt: MIT als Hauptlizenz.** Der Projektinhaber hat am 31.10.2026 MIT für den eigenen HydroLink-Code gewählt. Der vollständige Text steht in [LICENSE](../LICENSE). Fremde Bestandteile behalten ihre eigenen Copyright- und Lizenzhinweise.
2. MIT erfordert insbesondere die Beibehaltung der Copyright- und Lizenzhinweise bei Weitergabe. BSD-3-Clause verlangt ebenfalls Hinweise und Haftungsausschluss; die Urhebernamen dürfen nicht ohne Erlaubnis als Empfehlung/Werbung verwendet werden. Bestehende Lizenztexte werden im Windows-Paket mitgeliefert.
3. Windows SDK hat eigene Bedingungen, insbesondere Regeln zu weiterverteilbarem Code. Die Paketlizenz wurde ergänzt; eine vollständige Prüfung aller konkreten Weiterverteilungsbedingungen des SDK-Packs ist noch offen. Keine Aussage „alles ist MIT“ oder „rechtlich vollständig freigegeben“.
4. **Herkunft des App-Icons dokumentiert:** Der Projektinhaber bestätigte am 31.10.2026, dass das Icon vom KI-Assistenten für das Projekt generiert wurde. Dateizuordnung und Grenzen dieses Nachweises: [ASSET-PROVENANCE.md](ASSET-PROVENANCE.md).
5. Für einen belastbaren Prozentanteil übernommenen Codes wäre ein zusätzlicher datei-/abschnittsweiser Vergleich mit den ursprünglichen Quellständen erforderlich.

Quellen: [MIT](https://opensource.org/license/mit), [BSD-3-Clause](https://opensource.org/license/bsd-3-clause), [GitHub zur Repository-Lizenz](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository), [Windows-SDK-Lizenz](https://aka.ms/WinSDKLicenseURL), [C#/WinRT-Lizenz](https://github.com/microsoft/CsWinRT/blob/master/LICENSE).

## Nachprüfung 31.10.2026

Ausgangsstand: Commit `21baf10`, Windows 0.16.4. Die vier direkten Core-Paketverweise sind gegenüber dem bisherigen Inventar unverändert. Die Zahlen oben bleiben ausdrücklich historische Messwerte; es wurde kein neuer vollständiger Ähnlichkeitsvergleich durchgeführt. Hauptlizenz und Icon-Herkunft wurden ergänzt. Der Build prüft erforderliche Lizenzdateien und übernimmt LICENSE sowie Drittanbieterhinweise in neue Windows-Pakete. Bereits veröffentlichte ZIP-Dateien werden dadurch nicht nachträglich verändert.
