# Module neu starten und OSC-Cache leeren – 0.12.5

Unter Module hat VR Fitness, WindForce, OSC Hub und Haptics jeweils einen Knopf „Neu starten“. Ein gestopptes Modul wird dabei gestartet. Gemeinsam genutzte Quellen bleiben beim Neustart eines Verbrauchers bestehen, damit andere Module weiterlaufen. OSC neu starten ersetzt dagegen bewusst die gemeinsame OSC-Verbindung; danach Toys wieder aktivieren. Haptics-Neustart verbindet Intiface neu, startet aber keine Toy-Ausgabe automatisch.

Unter OSC Hub befindet sich „VRChat-OSC-Cache leeren …“. Die Vorschau zeigt Anzahl, Ordner und Dateinamen. Nach Bestätigung werden ausschließlich OSC/ usr_UUID /Avatars/avtr_UUID.json-Dateien gesichert und entfernt. Andere Dateien bleiben erhalten; Pfade über Dateisystem-Verknüpfungen werden abgelehnt. Haptics-Ausgabe wird vor dem Leeren gestoppt.

Sicherungen liegen unter %LOCALAPPDATA%/VRC HydroLink/OscCacheBackups in einem eigenen Ordner je Aktion. Die Erfolgsmeldung zeigt den genauen Pfad. Zum Wiederherstellen VRChat schließen und die gesicherten Benutzer-Unterordner in den ursprünglichen OSC-Ordner zurückkopieren. Eigene OSC-Zuordnungen befinden sich ebenfalls in diesen Dateien und werden beim Leeren zurückgesetzt.

Nach dem Leeren den Avatar in VRChat neu laden. VRChat erzeugt fehlende OSC-Konfigurationen für veröffentlichte Avatare automatisch. Bei gesperrten oder zwischenzeitlich geänderten Dateien meldet HydroLink den Fehler; bereits gesicherte Dateien bleiben in der Sicherung erhalten.

Quelle: https://docs.vrchat.com/docs/osc-avatar-parameters
