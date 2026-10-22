# Startreihenfolge – HydroLink 0.12.1

HydroLink richtet zuerst seinen vollständigen OSC-Empfangsbaum ein und macht sich erst danach für VRChat sichtbar. Bisher konnte ein schon laufendes VRChat die App während ihrer Einrichtung entdecken und noch keine Avatar-Empfangsadressen vorfinden. Jede gestartete OSC-Sitzung erhält außerdem einen eigenen Dienstnamen, damit beim Neustart des Tools keine alte Anmeldung wiederverwendet wird.

Geprüft mit einem OSCQuery-Gegenstück über lokale Netzwerkerkennung und UDP:
- Gegenstück läuft zuerst, HydroLink startet danach.
- HydroLink läuft zuerst, Gegenstück startet danach.
- Die erste Abfrage nach Entdeckung enthält bereits die Avatar-Empfangsadressen.
- Empfang und Erkennung des ausgehenden Ziels funktionieren in beiden Reihenfolgen.

Gegencheck in VRChat: OSC einschalten, VRChat geöffnet lassen, die alte HydroLink-App schließen und 0.12.1 öffnen. Haptics starten und die Avatar-Erkennung prüfen. Erst zum Gerätetest das Toy aktivieren und im Radialmenü Haptik einschalten; einen bereits eingeschalteten Haptik-Schalter gegebenenfalls einmal aus- und einschalten, damit eine neue Freigabe ankommt. Gespeicherte Kontakt- oder Freigabewerte starten keine Toy-Ausgabe.

Es ist kein neuer Unity-Import oder Avatar-Upload nötig. Der Startreihenfolgen-Fix wurde noch nicht vom Nutzer im echten VRChat bestätigt.
