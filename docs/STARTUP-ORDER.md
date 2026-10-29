# Startreihenfolge – HydroLink 0.14.5

HydroLink reserviert immer einen eigenen freien lokalen UDP-Port und meldet ihn über OSCQuery an. UDP 9001 bleibt für VRCOSC oder andere OSC-Empfänger frei. Bis einschließlich 0.14.4 bevorzugte HydroLink selbst Port 9001; beim späteren Start von VRCOSC konnte dadurch SocketException 10048 auftreten.

Nach dem Update HydroLink und VRCOSC neu starten. OSC muss in VRChat eingeschaltet sein. Die tatsächliche HydroLink-Empfangsadresse steht im OSC-Status. Keine feste Weiterleitung an HydroLink auf Port 9001 verwenden. Der Heartbeat-WebSocket (standardmäßig Port 36210) ist eine separate Verbindung und bleibt unverändert.

Der vollständige Empfangsbaum steht vor der OSCQuery-Anmeldung bereit. Der Empfang hängt von der Erkennung durch VRChat ab. Ein neuer Unity-Import oder Avatar-Upload ist für diese Korrektur nicht erforderlich.

Nach „Toy aktivieren“ liest HydroLink den Haptik-Schalter des passenden Avatars frisch über OSCQuery. Kontaktwerte und manuelle Aktivierung werden nicht aus Abfragen wiederhergestellt. Ein neu eingehender Radialmenü-Schalter hat Vorrang.
