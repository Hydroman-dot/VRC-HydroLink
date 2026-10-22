# Startreihenfolge – HydroLink 0.12.2

HydroLink bevorzugt den normalen VRChat-Empfangsport UDP 9001. Ist dieser bereits belegt, verwendet es einen freien Port mit OSCQuery-Anmeldung. Der vollständige Empfangsbaum steht vor der Anmeldung bereit.

Nach einem ausdrücklichen Klick auf „Toy aktivieren“ fragt HydroLink den Haptik-Schalter des passenden Avatars frisch über OSCQuery ab. Ist er bereits eingeschaltet, muss er nicht mehr aus- und eingeschaltet werden. Die Abfrage erfolgt im normalen Drei-Sekunden-Takt. Eine ältere Abfrage oder ein anderer Avatar wird nicht übernommen. Ein neu eingehendes Ein-/Ausschalten im Radialmenü hat Vorrang. Kontaktwerte und manuelle Aktivierung werden nicht aus der Abfrage wiederhergestellt.

Zum Prüfen: Alte HydroLink-Version schließen, 0.12.2 öffnen, OSC in VRChat einschalten, passenden Avatar abwarten und das Toy in HydroLink aktivieren. Der bereits eingeschaltete Haptik-Schalter sollte nach wenigen Sekunden erkannt werden. Anschließend einen neuen Kontakt testen. Kein neuer Unity-Import oder Avatar-Upload ist für diese Korrektur erforderlich.

212 automatische Prüfungen bestanden. Die Prüfung mit echtem Gerät und verschiedenen VRChat-Startreihenfolgen steht noch aus. Bei belegtem Standardport hängt der Empfang weiterhin von der OSCQuery-Anmeldung in VRChat ab.
