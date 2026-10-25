# Zusätzliche Toy-Profile

Dolce (auch Quake) und Gemini: zwei getrennte Vibrationskanäle, sofern Intiface zwei Funktionen meldet. Motor 1/2 entsprechen aufsteigenden Feature-IDs, nicht einer ungeprüften anatomischen Zuordnung.
Nora: Vibration, Rotation und Richtung (nur bei gemeldetem Richtungswechsel).
Gravity: Vibration und Hubgeschwindigkeit (Oscillate). Keine erfundene Hubweg- oder Positionssteuerung.
Lush Mini: Vibration. LED-Farben und gespeicherte Hersteller-Programme werden über diese Intiface-Anbindung nicht bereitgestellt.

Je Toy: Aktivieren, SPS-Stärken, getrennte manuelle Werte, Stop und Akku bei verfügbarer Meldung. Fehlende Funktionen bleiben deaktiviert. Mehrere Geräte laufen unabhängig; ein Gerät kann nur in einem aktiven Toy-Reiter verwendet werden.

Unity: aktualisierte Wizard-Skripte importieren, gewünschte Toys wählen, Prefabs anlegen und übergeben. Vorhandene Controller und SPS-Sockets bleiben erhalten. Neue Profile erfordern ihr eigenes Prefab für den Avatar-Abgleich.

Hardware-Praxistests für diese fünf Modelle stehen aus. Verfügbarkeit hängt von Intiface-Geräteunterstützung und Firmware ab. Alle Parameter bedeutet die von dieser Schnittstelle bereitgestellten Funktionen, nicht jede Funktion der Hersteller-App.

## Wizard 0.14.1: gemeinsames Radialmenue
Alle eingerichteten Toy-Menues werden unter HydroLink zusammengefasst. Installieren richtet dies automatisch ein; fuer vorhandene Instanzen gibt es den Knopf zum Zusammenfassen. Max 2, Ridge und Solace Pro sind im Rindo MAX FT PC+ eingerichtet und die Szene gespeichert.
In Unity 2022.3.22f1 geprueft: fehlerfreie Kompilierung, wiederholte Gruppierung ohne Duplikate, tatsaechliche Modular-Avatar-Menueerzeugung mit genau einem HydroLink-Eintrag und allen acht Toys. Die fuenf zusaetzlichen Toys wurden auf einem temporaeren Testobjekt geprueft. Fremde Menues und Controller werden nicht ersetzt. Ein VRChat-Praxistest steht aus.
