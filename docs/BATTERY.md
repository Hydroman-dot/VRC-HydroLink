# Akkuanzeige – 0.13.1

Unter Haptics zeigt jeder Toy-Reiter unter den Gerätefunktionen den Akkustand des ausgewählten Geräts. Haptics muss gestartet und Intiface verbunden sein; die Toy-Ausgabe muss dafür nicht aktiviert werden.

Der Wert wird über die von Intiface gemeldete Battery-Funktion etwa einmal pro Minute gelesen. Bei höchstens 20 Prozent erscheint „niedrig“. Ohne Battery-Funktion steht „vom Gerät nicht gemeldet“, nach einer fehlgeschlagenen Abfrage ein entsprechender Hinweis. Beim Trennen wird der Prozentwert nicht weiter als aktueller Wert angezeigt. HydroLink schätzt keinen Akkustand und zeigt keinen unbestätigten Ladestatus an.

Vier gezielte Tests mit einem simulierten Buttplug-v4-WebSocket-Server bestanden: Prozentwert, fehlende Unterstützung, begrenzte Abfragefrequenz ohne Ausgangsbefehle und Verhalten bei Trennung. Reale Geräteprüfung steht noch aus. Der vollständige zusätzliche OSC-Netzwerktest scheiterte bei dieser Prüfung an einem UDP-Empfangszeitlimit, unabhängig vor den Akkutests. Die Oberfläche wurde im Vorschaumodus geprüft.
