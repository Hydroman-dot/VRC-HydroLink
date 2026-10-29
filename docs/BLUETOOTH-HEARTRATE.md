# Bluetooth-Puls und VRCOSC-Prefabs

HydroLink 0.16.0 enthält eine vorbereitete direkte BLE-Pulsquelle für Windows 10 ab Version 2004 und Windows 11. Zielgerät: Coospo H9Z. Die Implementierung nutzt den standardisierten Bluetooth Heart Rate Service (180D), Measurement (2A37). Der echte H9Z-/VRChat-Praxistest steht aus. Bestehende Quellen bleiben unverändert; die neue Quelle und Avatar-Ausgabe werden nicht automatisch aktiviert.

## Einrichtung

1. H9Z tragen, Windows-Bluetooth einschalten. Für den ersten Test VRCOSC/Bluetooth-Heartrate und andere Sensor-Verbindungen beenden.
2. **Einstellungen → Bluetooth-Puls → Pulssensoren suchen**. Nach acht Sekunden den eigenen H9Z auswählen. Geräte werden nicht automatisch verbunden.
3. **Bluetooth als Fitness-Pulsquelle auswählen**. Bei WindForce gegebenenfalls ebenfalls Bluetooth als Pulsquelle wählen, damit VRCOSC nicht wegen dieser Quelle weiter benötigt wird.
4. **Alle neun VRCOSC-Pulsparameter an den Avatar senden** aktivieren, wenn das Avatar-Prefab verwendet wird. Die Adressen können im aufklappbaren Abschnitt angepasst werden.
5. Speichern, dann **VR Fitness → Tool starten**. OSC in VRChat einschalten. Das vorhandene Heartrate-Prefab benötigt die unten genannten Parameter mit passenden Typen. Ein automatischer Unity-Import ist nicht Teil dieses Windows-Updates.

Zum Ändern einer laufenden Konfiguration zuerst **Module → Alles stoppen**. Mit aktivem VRCOSC-Profil ersetzt dessen Ausgabe die einzelne HeartRate-Ausgabe aus OSCQuery. Für andere Pulsquellen (Pulsoid/HypeRate/VRCOSC-WebSocket) kann dasselbe Profil ebenfalls genutzt werden.

## Parameter

Standardpräfix: `/avatar/parameters/VRCOSC/Heartrate/`. Alle neun Adressen sind einzeln einstellbar.

| Parameter | OSC-Typ | Bedeutung |
| --- | --- | --- |
| Connected | Bool | Gültiger Puls wurde innerhalb der letzten fünf Sekunden empfangen |
| Value | Int | Aktueller, optional geglätteter Puls |
| Normalised | Float | Puls zwischen konfigurierbaren Grenzen auf 0–1 abgebildet; Standard 0–240 bpm |
| Average | Int | Mittelwert im einstellbaren Zeitfenster, optional geglättet |
| Beat | Bool | Aus BPM berechneter Takt: wechselnd an/aus oder kurzer 50-ms-Impuls |
| Enabled | Bool | Ältere Entsprechung von Connected |
| Units | Float | Einerziffer geteilt durch 10 |
| Tens | Float | Zehnerziffer geteilt durch 10 |
| Hundreds | Float | Hunderterziffer geteilt durch 10 |

Beispiel 123 bpm: Units 0,3; Tens 0,2; Hundreds 0,1. Bei Signalverlust werden Status, Werte und Beat zurückgesetzt. Der Beat ist eine berechnete Animation, keine zeitgenaue Erkennung einzelner Herzschläge. Glättung und Pulsrücksetzung sind unabhängig implementiert, nicht bitgenau zum VRCOSC-Algorithmus.

Glättung für Wert/Mittelwert getrennt, Zeitfenster 1–300 Sekunden, Glättungsdauer 0–30 Sekunden und Normalisierungsgrenzen einstellbar. Geänderte Werte werden sofort, unveränderte Werte einmal pro Sekunde erneut gesendet. Der Animationstakt wird alle 50 ms aktualisiert.

## Sensor und Datenfluss

Ein gewählter Sensor wird automatisch erneut verbunden, wenn die Verbindung abbricht oder zehn Sekunden lang keine Meldung kommt. Fehlender gemeldeter Hautkontakt und ungültige BPM werden nicht als gültiger Puls weitergereicht. Optional gemeldete RR-Abstände, Kontaktstatus und Energie werden intern als Bluetooth-Daten veröffentlicht; diese sind keine zusätzlichen VRCOSC-Pulsparameter. Eine Akku-, LED- oder Vibrationssteuerung des H9Z ist nicht enthalten.

Der Puls steht gleichzeitig Fitness, Aufzeichnung, WindForce und der bereits eingerichteten Home-Assistant-Anbindung zur Verfügung. HydroLink verbindet keine zusätzlichen Cloud-Dienste. VRCOSC kann weiterhin für andere Funktionen genutzt werden; beide Programme sollten nicht gleichzeitig dieselben Avatar-Pulsparameter schreiben.

## Prüfung

Automatisiert geprüft: 8-/16-Bit-Bluetoothdaten, optionale Kontakt-/Energie-/RR-Felder, beschädigte Pakete, alle neun Parametertypen, Ziffernformat, Mittelwert, Glättung, beide Beat-Modi, veraltete Werte, Wiederaufnahme, ungültige Adressen sowie tatsächliche OSC-Busnachrichten und Abschalten. Ein Windows-Build und Oberflächenvorschau ersetzen keinen Test mit dem physischen H9Z. Geräteerkennung, Windows-Zugriff, GATT-Verbindung und bestehendes Avatar-Prefab müssen noch praktisch geprüft werden.

Protokollabgleich: [VRCOSC HeartrateModule, Version 2026.807.0](https://github.com/VolcanicArts/VRCOSC/blob/2026.807.0/VRCOSC.App/SDK/Modules/Heartrate/HeartrateModule.cs). Eigene Implementierung; kein kopierter VRCOSC- oder Bluetooth-Heartrate-Modulcode und keine mitgelieferten Drittanbieter-Prefabs.

[Coospo H9Z](https://www.coospo.com/products/h9z-chest-heart-rate-monitor) · [Microsoft GATT-Client](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/gatt-client)
