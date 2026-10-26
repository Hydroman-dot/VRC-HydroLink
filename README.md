# VRC HydroLink

**Unified VRChat OSC, fitness, haptics and hardware bridge.**

Native Windows-App mit WPF und .NET 8. **Der Quellstand 0.14.2 ist eine Entwicklungsvorschau. Vollständige Funktionsgleichheit mit den Originaltools ist noch nicht erreicht.** Alle offenen Originalfunktionen bleiben Ziel der Migration: [Funktionsvergleich](docs/FEATURE-PARITY.md).

## Projekt und Entwicklungsstand

HydroLink führt VR Fitness und VRC WindForce als unabhängig steuerbare Module in einer gemeinsamen Windows-Anwendung weiter. Die bisherigen Einzel-Repositories bleiben als historische Referenzen erhalten. Integration bedeutet noch keine vollständige Funktionsgleichheit oder abgeschlossene Hardware-Abnahme; Details stehen im [Funktionsvergleich](docs/FEATURE-PARITY.md).

Stand 26. Oktober 2026: [Windows-Version 0.14.2](https://github.com/Hydroman-dot/VRC-HydroLink/releases/tag/v0.14.2) ist veröffentlicht. Das Unity-Wizard-Paket hat die eigenständige Version 0.14.1. Die folgende Beschreibung bezieht sich auf den aktuellen Quellcode.

## KI-Hinweis / AI disclosure

Dieses Projekt wird mit Unterstützung künstlicher Intelligenz entwickelt und dokumentiert. Der Hinweis beschreibt die Entstehung des Projekts und bedeutet nicht, dass die Anwendung selbst KI-Funktionen verwendet.

This project is developed and documented with assistance from artificial intelligence. This disclosure concerns the development process; it does not imply that the application itself uses AI features.

## Avatar-Toolkit und Haptics

- [Unity-Paket herunterladen](packages/HydroLink-Wizard-0.14.1.unitypackage)
- [Einrichtung und mehrere Avatare](docs/AVATAR-TOOLKIT.md)
- [Unity-Quellcode und Prefabs](unity/Assets/VRC%20HydroLink%20Avatar%20Toolkit)

Jeden Avatar einmal im Unity-Wizard einrichten und übergeben. HydroLink speichert die Profile getrennt nach Upload-ID und wählt beim Avatarwechsel automatisch das passende Profil. Am PC das Toy freigeben, anschließend im Radialmenü **Haptik einschalten**. **Haptik-Staerken** und **Manuell** besitzen separate Regler; das manuelle Untermenü hat einen eigenen Einschalter.

Max 2: Vibration und Pump. Ridge: Vibration, Rotation und Richtung, soweit Intiface die Funktionen meldet. Physische Geräte sind noch nicht abschließend getestet. Avatar- und SPS-Dateien anderer Anbieter sind nicht enthalten; die benötigten Pakete separat installieren.

Windows-App aus Quellcode bauen: `./build.ps1 -Publish`. GitHub Actions stellt nach erfolgreichem Lauf ein Windows-ZIP als Build-Artefakt bereit.

## Starten

Das portable ZIP entpacken und `VRC HydroLink.exe` öffnen. Windows x64; keine separate Python- oder .NET-Installation nötig. Jedes Tool hat eine eigene Seite mit Start/Stop. „Alles stoppen“ auf der Module-Seite stoppt alle HydroLink-Komponenten. Gemeinsam genutzte Quellen laufen weiter, bis ihr letztes Tool gestoppt wird.

### Eigenes Dashboard

„Karte hinzufügen“ → Titel, Topic, Einheit, Darstellung und Breite wählen. Verfügbar sind Wert, Text, Balken (0–100) und ein kurzer Live-Verlauf. Auch eigene Plugin-Topics sind möglich. Im Bearbeitungsmodus Karten am Titel ziehen oder mit Pfeilen verschieben. Rechtsklick öffnet Bearbeiten/Entfernen. Layouts bleiben gespeichert und können als JSON importiert/exportiert werden. Es werden bis zu 60 Karten unterstützt. Alte Messwerte sind gedimmt und mit ihrem Alter gekennzeichnet; Live-Verläufe sind keine dauerhafte Historie.

### Programme automatisch starten

Unter **Programme** für VRTI, FitOSC, VRCOSC oder Intiface eine EXE auswählen, bei Bedarf Argumente eintragen und „Bei Bedarf automatisch starten“ aktivieren. VRTI kann auch über seine Steam-URI starten. Danach speichern. HydroLink startet das Programm erst, wenn eine aktivierte Quelle es benötigt. Bereits laufende EXEs werden über ihren Pfad erkannt; ein Steam-Start wird pro HydroLink-Lauf nur einmal angefordert. Externe Programme werden beim Stoppen eines Tools nicht beendet.

Optional „Ausgewählte Module mit HydroLink starten“ aktivieren; die Auswahl erfolgt auf **Module**. Das ist kein Windows-/SteamVR-Autostarteintrag.

### Unabhängige Tools

- **VR Fitness:** eigene Puls-/Bewegungsquelle, Live-Metriken, Health-Status, Sessions als CSV/JSON mit Notiz.
- **Sessions:** neue und originale VR-Fitness-CSV-Dateien öffnen, Statistiken und Diagramme ansehen, Textbericht exportieren oder über den Windows-Druckdialog als PDF ausgeben.
- **WindForce:** eigene Quellenwahl unabhängig von Fitness, manuelle Steuerung, Pause, Quellenpriorität, Kalibrierung, Ausklang, Geräte- und Fanwerte. Bewegungs-/Pulsquelle und Regeln direkt auf WindForce wählen, mit ↑/↓ priorisieren und „Speichern und anwenden“ drücken. Dabei wird nur WindForce neu gestartet; andere Tools laufen weiter. Die Regel `Direct OSC` ist aus Kompatibilitätsgründen standardmäßig aktiv; andere Regeln ausdrücklich aktivieren. Für FitOSC Walking gilt die eigene normalisierte 0–1-Regel, für VRTI AutoWalk km/h.
- **OSC Hub:** Live-Parameterliste mit Suche, OSCQuery-Werten sowie getrennten Empfangs-/Sendewerten, Dashboard-Übernahme und Zuordnungen pro Avatar.
- **Haptics:** eigene Max-2-/Ridge-Reiter, automatische SPS-Plug-/Socket-Erkennung, getrennte SPS- und manuelle Radialregler, Hauptschalter und Stop pro Toy sowie automatische Einrichtung für mehrere Avatare.

Verbindungseinstellungen werden beim Erzeugen des Hosts übernommen. Für Änderungen derzeit **Alles stoppen**, speichern, Tools erneut starten. Externe Dienste verbinden sich bei Unterbrechungen erneut.

### Fitness-Profil und Session-Daten

Auf **VR Fitness** lässt sich „Profil, automatische Sessions, Ziele und Health-Synchronisierung“ aufklappen. Dort stehen Datenordner, Alters-/Gewichtsprofil, Keytel-Kalorienschätzung, Signalbereinigung, Wochenziele und automatische Aufzeichnung zur Verfügung. Die Option SteamVR-Ende verhindert automatische Aufzeichnung, solange SteamVR nicht läuft. Nach einem manuellen Stop wartet die automatische Aufzeichnung zunächst auf Ruhe.

Bei aktiviertem Companion werden beendete Sessions im Datenordner unter `health-queue` vorgemerkt. Übertragung erfolgt an dessen `/session`-Schnittstelle; nur ein bestätigter Empfang entfernt den Warteschlangeneintrag. Schritte und Kalorien sind einzeln auswählbar. CSV und Auswertung bleiben erhalten. Unter **Sessions** lassen sich Daten als ZIP sichern und in einen separaten Importordner zurückholen. Wochenziele berücksichtigen vorhandene Summary-Dateien; gleichnamige Sicherungskopien werden nicht doppelt gezählt.

## OSCQuery als Standard

OSC in VRChat einschalten. HydroLink sucht `_oscjson._tcp` über mDNS und liest `OSC_IP`/`OSC_PORT` aus `?HOST_INFO`. Der HTTP-Port wird nicht als UDP-Port verwendet. Es gibt keinen stillen Rückfall auf 9000. Ohne gefundenes Ziel pausiert die Ausgabe. Manuelle Ziele sind ausdrücklich auswählbar.

HydroLink reserviert einen freien lokalen UDP-Empfangsport und veröffentlicht ihn mit seiner OSCQuery-Schnittstelle. VRChat kann es neben anderen OSC-Anwendungen finden. Das aktuelle Ziel wird regelmäßig geprüft. Die Firewall muss mDNS/OSCQuery zulassen. Diese Vorschau richtet sich an VRChat auf demselben Windows-PC.

## Voreinstellungen und Daten

VRTI: `ws://127.0.0.1:47126`, VRCOSC HeartRate: `ws://127.0.0.1:36210`, FitOSC: `ws://127.0.0.1:6547/`, Intiface: `ws://127.0.0.1:12345`. WindForce: Befehle UDP 4210, Discovery 4211; leere Geräte-IP verwendet das erste gefundene ESP32-Gerät. Cloudprovider benötigen gültige Zugangsdaten.

Einstellungen und Sessions liegen unter `%LOCALAPPDATA%\VRC HydroLink`. Tokens und Pairing-Code sind mit Windows DPAPI für das aktuelle Benutzerkonto verschlüsselt. Für Layout-Export werden keine Zugangsdaten exportiert. Beim Öffnen alter CSVs bleiben Originaldateien unverändert; neue Notizen werden in einer eigenen `.hydrolink-note.txt`-Datei gespeichert.

## Dokumentation

- [OSC-Explorer, Zuordnungen und Haptics einrichten](docs/OSC-HAPTICS.md)

- [Funktionsvergleich und offene Originalfunktionen](docs/FEATURE-PARITY.md)
- [Build-Anleitung](docs/BUILD.md)
- [Plugin-SDK](docs/PLUGIN-SDK.md)
- [Architektur und Migration](docs/MIGRATION.md)
- [Prüfstand](docs/VERIFICATION.md)
- [Lizenzen](THIRD-PARTY-NOTICES.md)

Repository: `Hydroman-dot/VRC-HydroLink`. Entwicklungsvorschau; kein signierter Installer. Inno-Setup-Definition und CI-Workflow sind enthalten. .NET 8 wird nach Microsofts Supportplan bis 10. November 2026 unterstützt; vor einer späteren stabilen Veröffentlichung auf ein unterstütztes LTS aktualisieren.

Solace Pro besitzt einen eigenen App-Reiter und ein Unity-Prefab: [Funktionsumfang und Gerätevoraussetzungen](docs/SOLACE-PRO.md).

## Updates

Seit 0.14.1 prüft HydroLink beim Start auf eine neue öffentliche Version und fragt vor der Installation nach Zustimmung. Der portable Windows-Updater prüft die SHA256-Prüfsumme und pflegt eine einzelne Desktop-Verknüpfung; 0.14.2 korrigiert deren Erstellung auf OneDrive-Desktops. Er aktualisiert keine Unity-Projekte. Einrichtung und Rückkehr zur vorherigen Version: [Updater-Anleitung](docs/UPDATER.md). Release-Entwürfe sind noch nicht öffentlich verfügbar.
