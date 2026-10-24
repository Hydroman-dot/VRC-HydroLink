# Robustheit – 0.12.4

Diese Version sichert drei konkrete Fehlerstellen ab:

- Hintergrunddienste: Gleichzeitige Startaufrufe starten nur eine Instanz. Gleichzeitiges Beenden wird koordiniert; ein späterer Neustart erhält eine neue Abbruchsteuerung.
- Einstellungen: Schreibvorgänge erfolgen über eine separate temporäre Datei, werden auf den Datenträger geschrieben und ersetzen erst danach die aktuelle Datei. Die vorherige gültige Datei bleibt als settings.json.bak erhalten. Bei ungültigem JSON versucht HydroLink die Sicherung zu laden und meldet die Wiederherstellung in der Aktivität. Das Laden überschreibt die beschädigte Originaldatei nicht. Eine spätere Speicherung ersetzt keine gültige Sicherung durch beschädigtes JSON.
- OSC: Nicht-endliche Zahlen (NaN und Unendlich) werden als ungültige Pakete verworfen, bevor sie Anzeige oder Datenbus erreichen.

Keine Änderung an Unity-Prefabs erforderlich. Dies ist eine gezielte Absicherung, keine Garantie gegen sämtliche Fehler. Langzeittests mit realen Geräten, Netzwerkwechseln und hartem Beenden bleiben nötig. Fehlende oder ebenfalls beschädigte Sicherungen können keine Einstellungen wiederherstellen.
