# Schrittweise Weiterentwicklung

## 0.13.0: Modulzustände und zentrale Diagnose

Umgesetzt: Worker melden Start, laufenden Dienst, normales Ende und unerwarteten Abbruch. Modulansichten unterscheiden Aus, Startet, fehlende Verbindung, fehlende/alte Daten, Bereit und Fehler. Diagnose zeigt Abhängigkeiten, Neustartaktionen und speicherbaren Bericht. Plugin-Dienste ohne Zustandsmeldung sind ausdrücklich unbekannt. Bereit bestätigt keine physische Gerätebewegung. Datenalter ist ein Hinweis; unveränderte OSC-Parameter müssen nicht ständig erneut gesendet werden.

## Weitere Schritte

1. Geführte Ersteinrichtung nach gewünschter Nutzung, mit einzeln prüfbaren Verbindungen.
2. Nutzungsprofile für Modulauswahl und Quellen, ohne automatische Gerätefreigabe.
3. Wiederherstellung von Einstellungen und unvollständigen Sessions sowie geregelter Update-/Rollback-Ablauf.
4. Längere gemeinsame Praxistests mit Netzwerkunterbrechung, Standby und echten Geräten.

Diese Folgeschritte sind noch nicht umgesetzt. Ein vollständiger automatischer Update-Dienst benötigt zusätzlich einen festgelegten Release- und Installationsweg.
