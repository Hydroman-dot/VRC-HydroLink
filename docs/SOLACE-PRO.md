# Solace Pro – Vorbereitung in HydroLink 0.11.0

Solace Pro hat einen eigenen Reiter. Angeschlossene Geräte werden anhand des Modellnamens vorgeschlagen; der normale Solace wird nicht automatisch als Solace Pro ausgewählt.

Umgesetzt:
- Hubgeschwindigkeit über die von Intiface gemeldete Oscillate-Funktion.
- SPS-Plugs und Sockets: stärkster Kontakt steuert die Geschwindigkeit bis zur eingestellten Obergrenze.
- Manueller Modus mit eigener Geschwindigkeit, unabhängig vom SPS-Wert.
- Eigener Stopp und gemeinsame Nutzung mit Max 2 und Ridge an unterschiedlichen Geräten.
- Bestehende Freigaben und automatische SPS-Empfängeraktivierung bleiben erhalten.

Noch nicht umgesetzt: alternative direkte Positionssteuerung sowie separate Hubweg-Grenzen. Die installierte Intiface-Konfiguration meldet Geschwindigkeit und Positionssteuerung als alternative Befehle desselben Motors; HydroLink verwendet in diesem Profil ausschließlich Geschwindigkeit. Separate Hubweg-Grenzen werden dort nicht angeboten.

Die Unity-Editor-Quellen sind für ein eigenes Solace-Pro-Prefab vorbereitet: HydroLink/SolacePro/Enabled, Motion, Manual, ManualMotion und Stop. Der Wizard kann das Profil automatisch übergeben. Diese Erweiterung wurde noch nicht im Unity-Editor ausgeführt oder geprüft; das bestehende Paket 0.10.0 enthält kein Solace-Pro-Prefab. Ein neues geprüftes Unity-Paket folgt bei der Einrichtung im geöffneten Projekt.

Bis dahin bleibt die neue Toy-Ausgabe bei bestehenden Avatar-Profilen ohne Solace Pro gesperrt. Der Reiter ist zur Vorbereitung sichtbar. Kein vorhandenes Unity-Projekt, Avatar-Controller oder SPS-Socket wurde geändert. Die Funktion am echten Solace Pro ist noch nicht bestätigt.

Für den späteren Einsatz: Unity-Prefab einrichten, Avatar hochladen, Intiface verbinden, Solace Pro am PC aktivieren und Haptik im Radialmenü einschalten. Anfangswert 25 %. Im Untermenü Manuell lässt sich der separate direkte Modus einschalten; sonst wirkt SPS.
