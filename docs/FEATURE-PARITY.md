# Funktionsgleichheit ist das Ziel

**Verbindliche Anforderung:** HydroLink soll gegenüber VR Fitness und VRC WindForce keine Funktionen verlieren. Eigene Seiten innerhalb der Host-App genügen; separate Fenster sind nicht gewünscht. Die folgende Liste ist der Abnahmestand, keine Streichliste. Offene Punkte bleiben Bestandteil der Migration.

Referenzen: VR Fitness V11.17 und der aus `bootstrap/appgz.00` bis `.04` entpackte vollständige WindForce-PC-Code 1.8.2. Commitstände siehe SOURCE-REVISIONS.txt. „Implementiert“ bedeutet nicht automatisch mit echter Hardware abgenommen.

## Host und Oberfläche

| Anforderung | 0.3.0 |
|---|---|
| Eigene Tool-Seiten | Implementiert: Fitness, WindForce, OSC Hub, Haptics |
| Tool unabhängig starten/stoppen | Implementiert, mit Tests für geteilte Quellen und Startfehler |
| Gemeinsame Quelle bleibt für andere Tools aktiv | Implementiert und geprüft |
| Unterschiedliche Quellen pro Fitness/WindForce | Implementiert, getrennte `sources/{Provider}/…`-Topics |
| Lesbare Dropdowns, Kontextmenüs, deaktivierte Buttons | Eigene dunkle Templates; geöffnete Menüs visuell geprüft |
| Dashboard selber bauen | Werte/Text/Balken/Verlauf, frei wählbare Topics, Kartengröße, Drag-and-drop, Pfeile, Löschen, JSON-Import/-Export, Speicherung |
| Programme automatisch starten | EXE/Argumente je Provider, VRTI-Steam-Start, nur bei benötigter Quelle, laufende EXE erkennen |
| Module beim App-Start | Optional; Auswahl auf der Seite Module, Aktivierung unter Programme |
| Quellen/Prioritäten ändern, andere Tools laufen weiter | WindForce ab 0.3.1 implementiert; übrige Verbindungseinstellungen weiterhin nach Alles stoppen |
| Plugin-Abhängigkeiten auf Built-in-Provider | Noch offen; eigener Plugin-Lebenszyklus und generischer Bus sind vorhanden |

## VR Fitness

| Originalfunktion | 0.3.0 |
|---|---|
| VRTI/Puls dauerhafte WebSockets | Implementiert |
| FitOSC inklusive Einheiten | Implementiert, Fixture-Tests |
| Pulsoid Token, HypeRate | Implementiert; echte Konten noch nicht geprüft |
| Experimentelle Pulsoid Widget-URL | Offen |
| Puls, Geschwindigkeit, Distanz, Schritte, aktive Zeit | Eigene Live-Seite und Dashboard-Karten |
| Zieltempo, Geschwindigkeitslimit, AutoWalk, Laufbandstatus | Implementiert, abhängig von Providerfeldern |
| Health Schritte/SpO₂/Gewicht | Authentifiziertes `/status`, einzelne Topics und Live-Karten |
| Session starten/stoppen, CSV, Notiz | Implementiert, einsekündige Aufzeichnung und Summary-Datei |
| Originales CSV-Schema / alte Spalten | Semikolon-Schema; Rückwärtskompatibilität und relative Gesamtwerte getestet |
| Pulsbereinigung und Geschwindigkeitsschwelle | Konfigurierbare Grenzwerte und Signalalter; Standards 35 bpm / 8 km/h |
| Pulsstatistik, Geschwindigkeit, Datenqualität, Korrelation, Zonen | Analyse implementiert; Session-Datenqualität nutzt Verbindungsstatus |
| Diagramme / letzte Sessions | Implementiert in eigener Sessions-Seite |
| Bericht / PDF | Text-Export und Druckdialog (Microsoft Print to PDF); Original-PDF-Gestaltung noch offen |
| Kalorienschätzung, Gesundheitsprofil | Implementiert, Keytel-Referenzwert und ungültige Profile getestet |
| Wöchentliche Ziele, aggregierte Historie | Konfigurierbare Ziele, Aggregation vorhandener Summary-Dateien |
| Automatische Sessions und SteamVR-Ende | Implementiert; Live-Abnahme mit SteamVR noch offen |
| Health `/session`, Warteschlange, Wiederholung | Implementiert; Payload und Bestätigungsverhalten getestet, Android-Abnahme offen |
| Backup/Wiederherstellung in der UI | ZIP-Sicherung und Import in separaten Ordner; Archivpfadprüfung, praktische Abnahme offen |
| VRCOSC/VRTI Programmstart | Implementiert unter Programme |
| SteamVR-Autostartregistrierung | Offen; App-interner Modulautostart ist vorhanden |
| Updateprüfung, Sprachwechsel, OSC-Reset | Offen |

## VRC WindForce

| Originalfunktion | 0.3.0 |
|---|---|
| ESP32 FAN/FANS UDP | Implementiert; Zero-Kommando beim Stop |
| Automatische Discovery 4211 und ACK/Status | Implementiert; feste IP funktioniert ohne Discovery-Socket |
| Geräte-IP, Fan-Kanäle, RPM, RSSI, PWM | Eigene Karten; weitere Statusfelder auf generischem Bus |
| Manuell, Pause/Resume | Implementiert; Pause lässt Quellen laufen |
| Quellenpriorität | Geordnete, aktivierbare Regeln; Manuell/Pause übersteuern |
| PhysBone-Signal, Totzone, Vollwert | Implementiert; Standardname WinForceSensor_Angle. Frei einstellbarer Name noch offen |
| VRChat Magnitude/XYZ, InStation, Avatarwechsel | Implementiert; Echt-VRChat-Abnahme noch offen |
| VRTI Laufband / AutoWalk | Implementiert; reale Event-Sequenzen noch abnehmen |
| FitOSC Walking | Eigene normalisierte Regel 0–1, getrennt von km/h |
| Herzfrequenz SOURCE/MINIMUM/OFF | Implementiert; automatische Auswahl aus mehreren gleichzeitigen Pulsprovidern noch offen |
| Kalibrierung, Max PWM, Quellen-Ausklang | Regel-Editor, Originalkennlinien, Fade-Endpunkte getestet |
| Anstiegsglättung | Implementiert, Standard 120 ms; noch ohne eigenen UI-Regler |
| „Aktuellen Wert übernehmen“, Presets | Offen |
| Filterbare Diagnose, alle Original-Zähler | Basisaktivität vorhanden, vollständige Migration offen |
| Firmware-/Webdiagnose-Verknüpfungen | Offen |

## Ergänzte Referenz: VRC Haptics Bridge / Max 2

Referenz: [Lovense Max 2 Windows-App bauen](https://chatgpt.com/s/cx_6ac61faa8afc8191ae8c4404a3e7e059), abgeglichen am 7. Oktober 2026. Das lokale Quellprojekt VrcHapticsBridge wurde in 0.3 integriert:

- Auswahl eines konkreten Intiface-Geräts und Anzeige seiner Vibrate-/Constrict-Fähigkeiten.
- Unabhängige OSC-Kanäle `/avatar/parameters/MaxVibrate` und `/avatar/parameters/MaxPump`, mit konfigurierbaren Adressen und eigenen Live-Eingangs-/Ausgangswerten.
- Vibration und Pump gleichzeitig; Pump nur bei tatsächlich gemeldeter Constrict-Unterstützung.
- Separate Testregler mit zeitbegrenztem Test, explizite Ausgabeaktivierung und gemeinsamer Stop.
- Übernahme der vorhandenen Fehler-/Verbindungswechsel-/Avatarwechsel-Behandlung; keine automatische erneute Ausgabeaktivierung nach Verbindungsverlust.
- Integration in HydroLinks gemeinsamen OSCQuery-Empfänger ohne zusätzlichen festen OSC-Port.
- Kompatibilität mit dem bestehenden MA-Zusatz-Prefab: gemeinsames Kontaktsignal, getrennte Stärken, Hauptschalter, zusätzlicher Pump-Schalter und zwei unabhängige manuelle Overrides im Avatar-Menü. Diese Logik liegt im Unity-Prefab; HydroLink verarbeitet dessen fertige OSC-Ausgänge.

Stand: implementiert und mit simuliertem Intiface geprüft. Das bestehende MA-Prefab wird über seine Ausgänge unterstützt; Unity wurde nicht verändert. Vollständige Geräteprüfung innerhalb von HydroLink steht aus.

## OSC-Explorer und Zuordnungen

In 0.3 implementiert: OSCQuery-Parameterbaum, Live-Empfang, getrennte HydroLink-Ausgaben, Typ/Zugriff, Suche, Filter, Dashboard-Karten und gespeicherte Zuordnungen pro Avatar. Transformationen und Profilwechsel sind getestet. Details und Grenzen siehe [OSC-HAPTICS.md](OSC-HAPTICS.md).

## Noch keine vollständige Ablösung

Die Originale bleiben bis zur Abnahme der offenen Zeilen die Referenz. Bestehende Original-Konfigurationen und Trainingsdateien werden nicht automatisch verändert oder gelöscht. Keine Versionsbezeichnung oder README darf 0.3.0 als vollständig funktionsgleich bezeichnen. Hardwaretests, Installer-Abnahme und Signierung stehen zusätzlich aus.
