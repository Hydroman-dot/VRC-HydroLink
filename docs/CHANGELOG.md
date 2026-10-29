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

# 0.3.0

- OSC-Explorer: Parameterbaum, Typ/Zugriff, Suche, Richtungsfilter, getrennte Query-/Empfangs-/Sendewerte und Dashboard-Übernahme.
- Persistente Zuordnungen mit Avatar-Profilen, Skalierung, Begrenzung, Invertierung und expliziten OSC-Ausgabetypen.
- Haptics-Bridge aus dem vorhandenen VrcHapticsBridge-Projekt integriert: Gerätewahl, Vibrate/Constrict, Live-Werte, Freigabe, zeitbegrenzte Tests und Stop.
- Gemeinsamer OSCQuery-Empfänger für alle Tools; keine zweite feste Portbelegung.
- Weiterhin eigenständige EXE; Umstellung auf separat installierte Runtime ausdrücklich noch nicht vorgenommen.

# 0.2.0

- Fitness-Profil, Keytel-Kalorienschätzung, konfigurierbare Bereinigung, Wochenziele und Datenordner.
- Automatische Sessions bei Bewegung/Ruhe sowie Ende mit SteamVR; kein sofortiger Neustart nach manuellem Stop.
- Health-Companion-Upload mit beständiger Warteschlange und bestätigter Übertragung.
- Session-Backup und Wiederherstellung in separaten Importordner.

- Eigene Seiten und unabhängig schaltbare Module, geteilte Verbindungen bleiben erhalten.
- Eigenes Dashboard mit Karteneditor, Drag-and-drop, Kartengrößen, vier Darstellungen, Import/Export und Speicherung.
- Dunkle, explizite Dropdown-/Kontextmenü-/Button-Styles; geöffnete Menüs visuell geprüft.
- Programmstart je benötigtem Provider, EXE-Auswahl, VRTI-Steam-URI, Duplikatvermeidung und optionaler Modulstart mit der App.
- Mehr Originalmetriken, getrennte Fitness-/WindForce-Quellen, VRTI AutoWalk und FitOSC Walking.
- WindForce-Regeln mit Quellenpriorität, Kennlinien, PWM-Limits, Timeout/Ausklang, Manual/Pause, Discovery/Status und FANS.
- Session-CSV, Notizen, Statistik, CSV-Rückwärtskompatibilität, Verlauf und Text-/Druckexport.
- Vollständiger Originalfunktionsvergleich als fortlaufende Abnahmeliste. Noch keine vollständige Funktionsgleichheit.

## 0.3.1 — WindForce-Einstellungen

Quellen und Prioritäten sind direkt auf der WindForce-Seite sichtbar und werden nicht mehr gesperrt, wenn andere Tools laufen. Regeln werden in einer unabhängigen Arbeitskopie bearbeitet. „Speichern und anwenden“ validiert und übernimmt die Auswahl; laufendes WindForce wird kurz gestoppt und neu gestartet. Pause und manuelle Steuerung bleiben erhalten. Gemeinsam genutzte Quellen anderer Tools bleiben aktiv. Ein unmittelbarer Startfehler stellt die vorige Konfiguration wieder her; ein offline erreichbarer Anbieter verwendet weiterhin seinen normalen Wiederverbindungsablauf.

## 0.3.2 — Aufklappbereiche

Fitness sperrt während laufender Tools nur noch die Eingaben, nicht mehr den aufklappbaren Bereich. Alle Aufklappköpfe haben eine große, kontrastreiche Klickfläche. Ein Hinweis erklärt die weiterhin notwendige Bearbeitungssperre und verweist auf Module → Alles stoppen.

## 0.3.3 — 8 October 2026

Added the HydroLink droplet/link icon for the executable, window and installer template. Transparent PNG master and seven-size Windows ICO are included in the source.
