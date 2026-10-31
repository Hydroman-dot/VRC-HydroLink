# 0.16.3

- Autostart-Auswahl direkt unter Module sichtbar: gewünschte Module markieren, Autostart ein-/ausschalten und speichern.
- Bestehende Startauswahl bleibt erhalten; gemeinsam benötigte Dienste starten weiterhin automatisch mit.

# 0.16.2

- Eigenes Heartrate-Modul: Sensor und Avatar-Puls unabhängig von VR Fitness starten, stoppen und neu starten.
- Fitness und WindForce behalten gemeinsam benötigte Pulsquellen; Avatar-Ausgabe gehört ausschließlich zu Heartrate.
- Sieben Regressionstests für unabhängige Modulstarts und geteilte Sensoren.

## 0.14.5

## 0.16.1 — 2026-10-29
- VRTI-Millisekunden am Eingang in Sekunden umrechnen: 730364761 → 202:52:44 statt 202879:06:01.
- Quelleinheit unter Einstellungen → Verbindungen auswählbar; historische CSVs bleiben unverändert.
- Enthält Bluetooth-Pulsvorbereitung aus 0.16.0, Herkunfts-/Lizenzinventar und Praxistestliste.


## 0.16.0 — 2026-10-29
- Direkte Bluetooth-Heart-Rate-Quelle für Windows, für Coospo H9Z vorbereitet; manueller Gerätescan und gespeicherte Auswahl.
- Alle neun VRCOSC-Heartrate-Parameter, einstellbare Adressen, Glättung, Mittelwert, Normalisierung und Beat-Modus.
- Signalverlust setzt Avatar-Ausgaben zurück; automatische Neuverbindung. Kein Wechsel vorhandener Pulsquellen ohne Auswahl.
- Windows 10 ab Version 2004 / Windows 11. Physischer H9Z-/Avatar-Praxistest ausstehend.


## 0.15.0 — 2026-10-29
- Dashboard- und Fitness-/Session-Zeiten einheitlich als h:mm:ss ohne Stundenumbruch.
- Verbindungen, OSCQuery, Programme und Plugins als Unterreiter in Einstellungen.
- Home-Assistant-MQTT: Live-Discovery, Windows-geschütztes Passwort, Session-Puffer mit v1-Bestätigung, automatische Wiederverbindung und Wiederherstellung nach Abbruch.
- Alte VR-Fitness-SQLite-Warteschlange bleibt unverändert; echter HA-Praxistest steht aus.


- Eigener dynamischer OSC-Empfangsport statt UDP 9001: VRCOSC kann auch nach HydroLink starten. OSCQuery meldet den tatsächlichen Empfangsport.

## 0.14.4

- Laufband-Zeit in VR Fitness als Stunden:Minuten anzeigen (z. B. 01:35). CSV-Werte bleiben Sekunden.
- Laufende CSV-Schreibvorgaenge beim Stoppen abschliessen, statt sie abzubrechen.

## 0.14.3

- Fitness-Konfiguration direkt im Einstellungen-Reiter sichtbar; Aufzeichnungsstatus und Dateipfad im Fitness-Reiter.
- Schreibfehler werden sichtbar gemeldet; CSV-Datei wird vor erfolgreichem Start angelegt.
- Session-Liste beim Oeffnen und nach dem Speichern aktualisieren.

## 0.14.2

- Desktop-Verknuepfung auch bei OneDrive-Desktop erlauben; Installationspfade bleiben streng geprueft.

## Windows 0.14.1
- Eine Desktop-Verknüpfung HydroLink; beim App-Start Update-Abfrage mit Zustimmung.
- Geordnetes Beenden vor Update, begrenztes Warten und Prüfung der bestätigten Version.

## 0.14.0
- Dolce, Gemini, Nora, Gravity und Lush Mini: eigene Toy-Profile, SPS/Manuell-Regler und Wizard-Auswahl.
- Mehrmotor-Vibration getrennt nach gemeldeten Feature-IDs; fehlende Funktionen bleiben deaktiviert.
- 226 automatisierte Tests bestanden. Neue Hardware und Unity-Editor-Praxistest stehen aus.

# Changelog

## 0.10.0
- Separate saved configurations for multiple avatars; automatic selection by VRChat avatar ID.
- Existing single-avatar handoff migration and isolated profile updates.

## 0.9.1
- Separate Unity, VRChat avatar, device and output status messages.

## 0.9.0
- Per-toy main switch and separate SPS/manual radial submenus with independent values.

## 0.8.0
- Direct manual control without SPS and automatic local Unity-wizard handoff.

## 0.6.0
- Separate Max 2/Ridge prefabs and automatic SPS plug/socket routing.

Earlier app changes: [historical changelog](docs/CHANGELOG.md).
