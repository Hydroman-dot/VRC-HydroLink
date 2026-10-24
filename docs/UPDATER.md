# HydroLink Updater vorbereiten und verwenden

## Aktueller Stand

Dieser Updater ist fuer die spaetere oeffentliche Veroeffentlichung vorbereitet. Er liest regulaere Releases von Hydroman-dot/VRC-HydroLink. Solange das Repository privat ist oder kein regulaeres Release existiert, zeigt er eine Fehlermeldung und installiert nichts. Keine GitHub-Anmeldung, keine eingebauten Tokens und kein automatisches Oeffentlichstellen des Repositorys.

## Verwendung nach Veroeffentlichung

1. HydroLink-Updater.zip in einen eigenen Ordner entpacken. Die Dateien zusammenlassen.
2. HydroLink schliessen, dann **Update starten.cmd** doppelklicken. Der Updater laedt die neueste veroeffentlichte Version, prueft SHA256, installiert und startet sie.
3. **HydroLink starten.cmd** startet die bereits installierte Version auch ohne Internet.
4. **Vorherige Version.cmd** wechselt zur vorherigen vom Updater installierten Version und startet sie. HydroLink vorher schliessen.

Installationen liegen in `%LOCALAPPDATA%\VRC HydroLink Updates\<Version>`. Die bisherigen Einstellungen unter `%LOCALAPPDATA%\VRC HydroLink` und die Unity-Projekte werden nicht veraendert. Eine vorhandene manuell entpackte App wird nicht als erste Rueckfallversion importiert; sie bleibt an ihrem bisherigen Ort bestehen. Rueckkehr steht ab dem zweiten erfolgreichen Updater-Installationsvorgang zur Verfuegung.

Die Auswahl der aktuellen Version wird erst nach vollstaendigem Download, erfolgreicher Pruefung und Entpacken geaendert. Laufende HydroLink-Prozesse werden nicht beendet. Ein Fehlstart der neuen App wird nicht automatisch erkannt; dafuer gibt es den separaten Rueckkehr-Knopf. Rueckkehr betrifft die Programmdateien, nicht eine etwaige spaetere Migration von Benutzereinstellungen.

## Release-Ablauf fuer die Entwicklung

- Die App-Version in der csproj ist die Quelle fuer Paketnamen.
- `build.ps1 -Publish` prueft App und Updater und erzeugt App-ZIP, `.zip.sha256` und `HydroLink-Updater.zip`.
- Einen zur App-Version passenden Tag pushen, zum Beispiel `v0.13.0`.
- Der Workflow `Prepare GitHub release` baut und testet und legt einen **Entwurf** an. Kein automatisches Release aus jedem main-Commit.
- Entwurf und Hinweise pruefen, danach als regulaeres Release veroeffentlichen. Fuer Endnutzer muss das Repository oeffentlich zugaenglich sein.
- Der Updater ignoriert Entwuerfe und Vorab-Releases und bietet keinen unbeabsichtigten Versionsrueckschritt an.

## Grenzen und Pruefungen

SHA256 erkennt beschaedigte Downloads, ist aber keine unabhaengige Signatur: Paket und Pruefsumme stammen aus demselben GitHub-Release. Code-Signierung ist noch nicht eingerichtet. Es gibt noch keinen Update-Knopf innerhalb der WPF-App und keine automatische Hintergrundinstallation. Der Launcher aktualisiert seine eigenen Skripte nicht automatisch; aktualisierte Launcher liegen im neuen App-Paket unter `updater` und als separates Release-ZIP.

Alte Versionen und Diagnose-/Downloadordner werden nicht automatisch geloescht. Der Speicherbedarf kann deshalb wachsen. Die Tests verwenden temporaere Pakete, starten keine Hardware und keine Test-EXE. Ein echter Download aus einem oeffentlichen HydroLink-Release kann erst nach dessen Veroeffentlichung abschliessend geprueft werden.

GitHub-Referenz: https://docs.github.com/en/rest/releases/releases
