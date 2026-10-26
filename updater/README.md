# HydroLink starten und aktualisieren

Der Updater erstellt genau eine Desktop-Verknüpfung **HydroLink**. Sie startet die installierte Windows-App. Nach Updates und Rückkehr zur vorherigen Version wird dieselbe Verknüpfung angepasst.

Ab Windows-Version 0.14.1 prüft HydroLink beim Start einmal im Hintergrund das neueste öffentliche GitHub-Release. Nur für eine neuere vollständige Version erscheint **Jetzt aktualisieren?**. **Nein** startet kein Update. Bei fehlendem Internet bleibt die App nutzbar; die Prüfung endet nach spätestens acht Sekunden.

Bei **Ja** beendet HydroLink seine Module geordnet. Der separate Updater wartet auf das Prozessende, prüft den Download mit SHA256 und installiert die Version in einen eigenen Ordner. Danach wird HydroLink neu gestartet. Ein nach der Zustimmung geändertes Release wird nicht ungefragt installiert. Bei Fehlern bleibt die bisherige Installation erhalten.

## Einmalige Einrichtung oder Umstieg von 0.14.0

1. HydroLink-Updater.zip aus dem neuesten GitHub-Release entpacken.
2. HydroLink schließen und **Update starten.cmd** ausführen.
3. Künftig über **HydroLink** auf dem Desktop starten; Updates werden in der App angeboten.

Es wird keine zweite Update-Verknüpfung auf dem Desktop angelegt. **HydroLink starten.cmd** im Updater-Ordner bleibt ein alternativer Starter. **Vorherige Version.cmd** wählt die vorherige vom Updater installierte Version aus. Diese Rückkehr ist erst ab zwei installierten Versionen verfügbar.

Installationen: `%LOCALAPPDATA%\VRC HydroLink Updates`. Einstellungen: `%LOCALAPPDATA%\VRC HydroLink`. Unity-Projekte werden nicht aktualisiert; das Wizard-Paket separat importieren. Quellcode oder Unity-Pakete allein lösen kein Windows-Update aus: erforderlich sind ein veröffentlichtes Release, das Windows-ZIP und die passende SHA256-Datei.

SHA256 ist eine Integritätsprüfung, keine unabhängige Codesignatur. Die Anwendung ist noch nicht signiert. Entwürfe und Vorab-Releases werden nicht automatisch angeboten. Der Updater beendet keine Prozesse zwangsweise und installiert nur nach Zustimmung oder ausdrücklichem Aufruf von Update starten.cmd.
