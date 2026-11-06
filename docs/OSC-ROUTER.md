# OSC Router · 0.17.0

Unter **OSC Hub → OSC Router**. Separates Modul mit Start, Stopp und Neustart. Die Auswahl **Module → OSC Router** berücksichtigt den vorhandenen Autostart-Schalter. Der Router ist anfangs ausgeschaltet und hat keine Regeln. Andere Module behalten ihre gemeinsam benötigte OSC-Verbindung, wenn der Router gestoppt wird.

## Beispiel: Programm ohne OSCQuery

1. Nur den Router stoppen, falls er läuft; andere Module dürfen weiterlaufen.
2. Endpunkt hinzufügen, etwa Name `Legacy`, Programm-IP `127.0.0.1`, HydroLink-Empfangsport `9101`, Programm-Empfangsport `9102`.
3. Im anderen Programm das Sendeziel auf `127.0.0.1:9101` setzen und den Empfang auf `9102` stellen. Die Portnummern müssen frei sein.
4. Regel anlegen: An, Profil `Standard`, Avatar `*`, Von `Legacy`, Nach `Hub`, Filter `/avatar/parameters/*`. Neue Adresse leer lassen, Umrechnung aus.
5. Speichern und Router starten. Hub verwendet das vorhandene VRChat-Ziel aus OSCQuery bzw. den manuellen OSC-Einstellungen.
6. Für die Gegenrichtung eine zweite Regel Von `Hub`, Nach `Legacy` anlegen, möglichst mit einem getrennten Adressfilter. Für weitere Ziele weitere Regeln anlegen.

**Sendeport und Empfangsport sind aus der Sicht der jeweiligen Seite beschriftet.** Empfangsport 0 schaltet einen Legacy-Eingang aus. Empfangs-IP 127.0.0.1 erlaubt nur diesen PC. Für LAN kann eine lokale IPv4-Adresse eingetragen werden; eingehende Pakete werden zusätzlich auf die Programm-IP eingeschränkt. Keine Firewall-Regeln werden automatisch geändert. HydroLink bindet keine Legacy-Eingänge auf 9000 oder 9001 und konfiguriert VRChat nicht um. Ein Port-9000-Proxy ist nicht enthalten.

## Regeln und Status

- Profile werden über den Namen ausgewählt; Regeln können zusätzlich auf eine Avatar-ID beschränkt sein. `*` bedeutet alle Avatare.
- Exakte Adressen oder ein Präfix mit abschließendem `*`; keine weiteren Platzhalter.
- Umbenennen setzt eine feste Ausgabeadresse. Ohne neue Adresse bleibt die ursprüngliche erhalten.
- Optionale Umrechnung des ersten Arguments: `(invertiert ? 1 - Wert : Wert) * Faktor + Offset`, anschließend Minimum/Maximum. Ergebnis ist Float; weitere Argumente bleiben erhalten.
- Float, Int, Bool und String sowie deren OSC-Bundles werden verarbeitet. Bundle-Zeitmarken werden nicht zeitgesteuert wiedergegeben, sondern Nachrichten unmittelbar weitergeleitet. Blob/MIDI und weitere OSC-Typen sind nicht enthalten.
- Der Verlauf zeigt bis zu 100 Weiterleitungen/Fehler mit Zeit, Quelle, Ziel, Adresse und Werten. UDP gesendet ist keine Empfangsbestätigung des Zielprogramms. „An OSC Hub übergeben“ bedeutet ebenfalls nicht, dass VRChat den Wert angenommen hat; dafür Hub-Verbindungsanzeige prüfen.
- Exakt zurückgespiegelte Nachrichten vom jeweiligen Ziel werden zwei Sekunden unterdrückt. Der Schutz kann dadurch gewollte identische Rückmeldungen unterdrücken. Mehr als 500 Eingänge/s führen zu fünf Sekunden Weiterleitungspause. Warteschlange und Echo-Speicher sind begrenzt. Schleifen mit veränderten Werten oder langsamen Echos deshalb auch durch getrennte Ein-/Ausgangsfilter vermeiden.
- Belegte Ports und fehlerhafte Konfiguration werden angezeigt; stoppen, korrigieren, speichern und neu starten. Neustart spielt keine alten Pakete ab.

## Haptik und nächste Schritte

Ein Fehler der OSCQuery-Parameterbaum-Abfrage veröffentlicht nur noch eine Warnung. Er wird nicht mit dem Verlust der UDP-Verbindung gleichgesetzt. Tatsächliche Zielwechsel und Geräteverluste behalten ihre bisherigen Stop-Regeln; keine automatische Wiederfreigabe von Toys.

Die integrierte Buttplug-Geräte-Engine, direkte Gerätetreiber, eine vollständige Haptik-Geräteabstraktion und die .NET-Migration aus dem Architekturentwurf sind weitere Arbeitspakete. Diese Version ersetzt Intiface nicht und bleibt auf .NET 8.

## Prüfung

Lokale UDP-Tests: Mehrfachverteilung, Umrechnung, Hub-Anbindung, ungültige Pakete, Echo-Schutz, Portfreigabe und unabhängiger Modul-Lifecycle. Praxistest mit den tatsächlich verwendeten Fremdprogrammen und VRChat steht aus. Die Darstellung wurde im Vorschaumodus geprüft. Weitere Regressionstests werden im Build ausgeführt.

### Haptik-Menüfreigabe

Die Synchronisierung bleibt während einer freigegebenen Toy-Sitzung aktiv, auch wenn die erste Abfrage „aus“ meldet. Neuere Live-OSC-Werte haben Vorrang vor älteren Abfragen. Nach einer Live-Ausschaltmeldung sind mindestens drei Sekunden Abstand und zwei unterschiedliche frische Einschalt-Abfragen nötig, bevor ein später verlorenes Live-Einschalten übernommen wird. Ein solcher Abgleich aktiviert weder gestoppte Toys noch den manuellen Modus und übernimmt keine alten SPS-Kontakte. Die Oberfläche nennt Quelle und Zeitpunkt. Ohne gültige Query-Daten und ohne Live-OSC kann keine Freigabe erkannt werden; dann bleibt der Status wartend. Die Erkennung im echten VRChat-Betrieb muss noch bestätigt werden.
