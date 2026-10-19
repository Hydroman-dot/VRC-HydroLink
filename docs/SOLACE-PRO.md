# Solace Pro in HydroLink 0.12.0

Der eigene Solace-Pro-Reiter enthält folgende Steuerungen, jeweils mit getrennten Werten für SPS und Manuell:

- Hubgeschwindigkeit.
- Untere und obere Grenze des Hubwegs (0–100 %).
- Umschalter zwischen wiederholtem Hub und Zielposition.
- Zielposition innerhalb des eingestellten Hubwegs; bei SPS die maximale genutzte Kontakttiefe.
- Fahrzeit zur Zielposition, 0,1 bis 5 Sekunden, zusätzlich begrenzt durch die vom Gerät gemeldeten Werte.
- Haptik-Freigabe, manueller Modus und eigener Stopp.

## Betriebsarten

**Voller Hubweg:** Im Hubmodus mit Grenzen 0 % und 100 % wird die native Oscillate-Geschwindigkeit verwendet. SPS skaliert die Geschwindigkeit anhand des stärksten Plug- oder Socket-Kontakts.

**Begrenzter Hubweg:** Im Hubmodus mit engeren Grenzen erzeugt HydroLink die Hübe abwechselnd durch Positionsbefehle an beide Grenzen. Das ist eine Softwaresteuerung, kein nativer Lovense-Hubweg-Befehl. Die Geschwindigkeit bestimmt eine angeforderte Fahrzeit von 2 bis 0,2 Sekunden je Richtung. Bei Geschwindigkeit null wird gestoppt. Ohne SPS-Kontakt bleibt der SPS-Modus still.

**Zielposition:** Im manuellen Modus bestimmt der Positionsregler das Ziel innerhalb des Hubwegs. Im SPS-Modus wird die Kontakttiefe mit dem Positionsregler als Obergrenze auf den Hubweg abgebildet. Fahrzeit bestimmt die angeforderte Bewegungsdauer. Der Geschwindigkeitsregler ist in diesem Modus ohne Funktion und deaktiviert. Neue Ziele werden frühestens nach der vorherigen angeforderten Fahrzeit gesendet. Die tatsächlich ausgeführte Geschwindigkeit hängt von Gerät und Firmware ab.

SPS und Manuell behalten unabhängige Einstellungen. Beim Wechsel der Betriebsart oder Hubgrenzen wird zuerst gestoppt. Eine untere Grenze größer oder gleich der oberen verhindert Bewegung. Stopp, Verlust der Freigabe und Kontaktverlust im SPS-Positionsmodus senden einen Stoppbefehl, keine automatische Fahrt zur Position null. Position null kann im manuellen Positionsmodus ausdrücklich als Ziel gewählt werden.

## Anbindung und Stand

Intiface muss Oscillate und für erweiterte Steuerung HwPositionWithDuration am selben Motor melden. Fehlende Positionssteuerung sperrt diese Funktion; es erfolgt kein automatischer Ersatz durch uneingeschränkte Hübe. Native Lovense-EventsAPI und Bluetooth-Direktbefehle sind nicht integriert.

Unity-Quellen sind für 15 lokale Parameter vorbereitet: Enabled, Motion, StrokeStart, StrokeEnd, Position, TravelTime, PositionMode, Manual, ManualMotion, ManualStrokeStart, ManualStrokeEnd, ManualPosition, ManualTravelTime, ManualPositionMode und Stop, jeweils unter HydroLink/SolacePro/. Die Untermenüs für SPS und Manuell bleiben getrennt.

Die Unity-Änderung ist noch nicht im Editor eingerichtet oder geprüft. Das bestehende Unity-Paket 0.10.0 enthält kein Solace-Pro-Prefab. Bei bisherigen Avatar-Profilen ohne Solace Pro bleibt die Ausgabe gesperrt. Das neue Paket und der Avatar-Upload folgen bei der gemeinsamen Unity-Einrichtung. Bestehende Unity-Projekte wurden in dieser Vorbereitung nicht verändert.

Die App wurde mit simulierten Geräten geprüft. Der Test am echten Solace Pro und die Prüfung seiner tatsächlichen Bewegung und Fahrzeit stehen noch aus.
