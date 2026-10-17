# VRC HydroLink

**Unified VRChat OSC, fitness, haptics and hardware bridge.**

Native Windows-App mit WPF und .NET 8. **Version 0.10.0 ist eine Entwicklungsvorschau. VollstÃ¤ndige Funktionsgleichheit mit den Originaltools ist noch nicht erreicht.** Alle offenen Originalfunktionen bleiben Ziel der Migration: [Funktionsvergleich](docs/FEATURE-PARITY.md).

## Avatar-Toolkit und Haptics (0.10)

- [Unity-Paket herunterladen](packages/VRC-HydroLink-Toy-Prefabs-0.10.0.unitypackage)
- [Einrichtung und mehrere Avatare](docs/AVATAR-TOOLKIT.md)
- [Unity-Quellcode und Prefabs](unity/Assets/VRC%20HydroLink%20Avatar%20Toolkit)

Jeden Avatar einmal im Unity-Wizard einrichten und übergeben. HydroLink speichert die Profile getrennt nach Upload-ID und wählt beim Avatarwechsel automatisch das passende Profil. Am PC das Toy freigeben, anschließend im Radialmenü **Haptik einschalten**. **Haptik-Staerken** und **Manuell** besitzen separate Regler; das manuelle Untermenü hat einen eigenen Einschalter.

Max 2: Vibration und Pump. Ridge: Vibration, Rotation und Richtung, soweit Intiface die Funktionen meldet. Physische Geräte sind noch nicht abschließend getestet. Avatar- und SPS-Dateien anderer Anbieter sind nicht enthalten; die benötigten Pakete separat installieren.

Windows-App aus Quellcode bauen: `./build.ps1 -Publish`. GitHub Actions stellt nach erfolgreichem Lauf ein Windows-ZIP als Build-Artefakt bereit.

## Starten

Das portable ZIP entpacken und `VRC HydroLink.exe` Ã¶ffnen. Windows x64; keine separate Python- oder .NET-Installation nÃ¶tig. Jedes Tool hat eine eigene Seite mit Start/Stop. â€žAlles stoppenâ€œ auf der Module-Seite stoppt alle HydroLink-Komponenten. Gemeinsam genutzte Quellen laufen weiter, bis ihr letztes Tool gestoppt wird.

### Eigenes Dashboard

â€žKarte hinzufÃ¼genâ€œ â†’ Titel, Topic, Einheit, Darstellung und Breite wÃ¤hlen. VerfÃ¼gbar sind Wert, Text, Balken (0â€“100) und ein kurzer Live-Verlauf. Auch eigene Plugin-Topics sind mÃ¶glich. Im Bearbeitungsmodus Karten am Titel ziehen oder mit Pfeilen verschieben. Rechtsklick Ã¶ffnet Bearbeiten/Entfernen. Layouts bleiben gespeichert und kÃ¶nnen als JSON importiert/exportiert werden. Es werden bis zu 60 Karten unterstÃ¼tzt. Alte Messwerte sind gedimmt und mit ihrem Alter gekennzeichnet; Live-VerlÃ¤ufe sind keine dauerhafte Historie.

### Programme automatisch starten

Unter **Programme** fÃ¼r VRTI, FitOSC, VRCOSC oder Intiface eine EXE auswÃ¤hlen, bei Bedarf Argumente eintragen und â€žBei Bedarf automatisch startenâ€œ aktivieren. VRTI kann auch Ã¼ber seine Steam-URI starten. Danach speichern. HydroLink startet das Programm erst, wenn eine aktivierte Quelle es benÃ¶tigt. Bereits laufende EXEs werden Ã¼ber ihren Pfad erkannt; ein Steam-Start wird pro HydroLink-Lauf nur einmal angefordert. Externe Programme werden beim Stoppen eines Tools nicht beendet.

Optional â€žAusgewÃ¤hlte Module mit HydroLink startenâ€œ aktivieren; die Auswahl erfolgt auf **Module**. Das ist kein Windows-/SteamVR-Autostarteintrag.

### UnabhÃ¤ngige Tools

- **VR Fitness:** eigene Puls-/Bewegungsquelle, Live-Metriken, Health-Status, Sessions als CSV/JSON mit Notiz.
- **Sessions:** neue und originale VR-Fitness-CSV-Dateien Ã¶ffnen, Statistiken und Diagramme ansehen, Textbericht exportieren oder Ã¼ber den Windows-Druckdialog als PDF ausgeben.
- **WindForce:** eigene Quellenwahl unabhÃ¤ngig von Fitness, manuelle Steuerung, Pause, QuellenprioritÃ¤t, Kalibrierung, Ausklang, GerÃ¤te- und Fanwerte. Bewegungs-/Pulsquelle und Regeln direkt auf WindForce wÃ¤hlen, mit â†‘/â†“ priorisieren und â€žSpeichern und anwendenâ€œ drÃ¼cken. Dabei wird nur WindForce neu gestartet; andere Tools laufen weiter. Die Regel `Direct OSC` ist aus KompatibilitÃ¤tsgrÃ¼nden standardmÃ¤ÃŸig aktiv; andere Regeln ausdrÃ¼cklich aktivieren. FÃ¼r FitOSC Walking gilt die eigene normalisierte 0â€“1-Regel, fÃ¼r VRTI AutoWalk km/h.
- **OSC Hub:** Live-Parameterliste mit Suche, OSCQuery-Werten sowie getrennten Empfangs-/Sendewerten, Dashboard-Ãœbernahme und Zuordnungen pro Avatar.
- **Haptics:** eigene Max-2-/Ridge-Reiter, automatische SPS-Plug-/Socket-Erkennung, getrennte SPS- und manuelle Radialregler, Hauptschalter und Stop pro Toy sowie automatische Einrichtung für mehrere Avatare.

Verbindungseinstellungen werden beim Erzeugen des Hosts Ã¼bernommen. FÃ¼r Ã„nderungen derzeit **Alles stoppen**, speichern, Tools erneut starten. Externe Dienste verbinden sich bei Unterbrechungen erneut.

### Fitness-Profil und Session-Daten

Auf **VR Fitness** lÃ¤sst sich â€žProfil, automatische Sessions, Ziele und Health-Synchronisierungâ€œ aufklappen. Dort stehen Datenordner, Alters-/Gewichtsprofil, Keytel-KalorienschÃ¤tzung, Signalbereinigung, Wochenziele und automatische Aufzeichnung zur VerfÃ¼gung. Die Option SteamVR-Ende verhindert automatische Aufzeichnung, solange SteamVR nicht lÃ¤uft. Nach einem manuellen Stop wartet die automatische Aufzeichnung zunÃ¤chst auf Ruhe.

Bei aktiviertem Companion werden beendete Sessions im Datenordner unter `health-queue` vorgemerkt. Ãœbertragung erfolgt an dessen `/session`-Schnittstelle; nur ein bestÃ¤tigter Empfang entfernt den Warteschlangeneintrag. Schritte und Kalorien sind einzeln auswÃ¤hlbar. CSV und Auswertung bleiben erhalten. Unter **Sessions** lassen sich Daten als ZIP sichern und in einen separaten Importordner zurÃ¼ckholen. Wochenziele berÃ¼cksichtigen vorhandene Summary-Dateien; gleichnamige Sicherungskopien werden nicht doppelt gezÃ¤hlt.

## OSCQuery als Standard

OSC in VRChat einschalten. HydroLink sucht `_oscjson._tcp` Ã¼ber mDNS und liest `OSC_IP`/`OSC_PORT` aus `?HOST_INFO`. Der HTTP-Port wird nicht als UDP-Port verwendet. Es gibt keinen stillen RÃ¼ckfall auf 9000. Ohne gefundenes Ziel pausiert die Ausgabe. Manuelle Ziele sind ausdrÃ¼cklich auswÃ¤hlbar.

HydroLink reserviert einen freien lokalen UDP-Empfangsport und verÃ¶ffentlicht ihn mit seiner OSCQuery-Schnittstelle. VRChat kann es neben anderen OSC-Anwendungen finden. Das aktuelle Ziel wird regelmÃ¤ÃŸig geprÃ¼ft. Die Firewall muss mDNS/OSCQuery zulassen. Diese Vorschau richtet sich an VRChat auf demselben Windows-PC.

## Voreinstellungen und Daten

VRTI: `ws://127.0.0.1:47126`, VRCOSC HeartRate: `ws://127.0.0.1:36210`, FitOSC: `ws://127.0.0.1:6547/`, Intiface: `ws://127.0.0.1:12345`. WindForce: Befehle UDP 4210, Discovery 4211; leere GerÃ¤te-IP verwendet das erste gefundene ESP32-GerÃ¤t. Cloudprovider benÃ¶tigen gÃ¼ltige Zugangsdaten.

Einstellungen und Sessions liegen unter `%LOCALAPPDATA%\VRC HydroLink`. Tokens und Pairing-Code sind mit Windows DPAPI fÃ¼r das aktuelle Benutzerkonto verschlÃ¼sselt. FÃ¼r Layout-Export werden keine Zugangsdaten exportiert. Beim Ã–ffnen alter CSVs bleiben Originaldateien unverÃ¤ndert; neue Notizen werden in einer eigenen `.hydrolink-note.txt`-Datei gespeichert.

## Dokumentation

- [OSC-Explorer, Zuordnungen und Haptics einrichten](docs/OSC-HAPTICS.md)

- [Funktionsvergleich und offene Originalfunktionen](docs/FEATURE-PARITY.md)
- [Build-Anleitung](docs/BUILD.md)
- [Plugin-SDK](docs/PLUGIN-SDK.md)
- [Architektur und Migration](docs/MIGRATION.md)
- [PrÃ¼fstand](docs/VERIFICATION.md)
- [Lizenzen](THIRD-PARTY-NOTICES.md)

Repository: `Hydroman-dot/VRC-HydroLink`. Entwicklungsvorschau; kein signierter Installer. Inno-Setup-Definition und CI-Workflow sind enthalten. .NET 8 wird nach Microsofts Supportplan bis 10. November 2026 unterstÃ¼tzt; vor einer spÃ¤teren stabilen VerÃ¶ffentlichung auf ein unterstÃ¼tztes LTS aktualisieren.
