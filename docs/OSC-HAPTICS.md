# OSC Hub und Haptics · Version 0.3

## Live-Parameter

OSC Hub starten und OSC in VRChat einschalten. HydroLink meldet seinen freien Empfangsport über OSCQuery an. Der Explorer fragt VRChats Parameterbaum alle drei Sekunden ab und aktualisiert empfangene UDP-Werte laufend. Name, OSC-Typ und Zugriff stehen neben drei bewusst getrennten Werten:

- OSCQuery: zuletzt abgefragter Wert aus VRChat.
- Empfangen: letztes am HydroLink-Empfänger eingetroffenes OSC-Paket.
- Gesendet: letzte von HydroLink gesendete UDP-Nachricht; keine Bestätigung des Avatar-Zustands.

Die Suche filtert Namen. Der Richtungsfilter zeigt alle, empfangene oder gesendete Parameter. Spalten lassen sich sortieren und horizontal verschieben. Letztes UDP zeigt die letzte Empfangs- oder Sendezeit. Beim Avatarwechsel werden alte Werte entfernt. Angezeigt werden höchstens 8192 Adressen. Nur von VRChat veröffentlichte Parameter sind sichtbar; interner Unity-Zustand und OSC-Ausgaben anderer Apps werden nicht mitgeschnitten. Manuelle OSC-Ziele liefern ohne automatische Erkennung keinen OSCQuery-Parameterbaum.

Mit „Empfang als Karte“ wird der markierte Empfangswert als `osc/value/<OSC-Adresse>` hinzugefügt; „Ausgabe als Karte“ nutzt `osc/sent-value/<OSC-Adresse>`. OSC Hub oder ein anderes OSC benötigendes Tool muss dafür laufen. Query-Werte lösen keine Hardwaresteuerung aus. Sendewert-Topics können angezeigt, aber nicht als Routing-Quelle verwendet werden, um lokale Sendeschleifen zu verhindern.

## Zuordnen

Parameter markieren und „Parameter zuordnen“ wählen. Alternativ „Neue Zuordnung“ für HydroLink-Daten, zum Beispiel `fitness/heart/bpm`. Eine Quelle ist eine vollständige `/avatar/parameters/…`-Adresse oder ein skalares Bus-Topic. Als Ziel stehen Avatar-Adresse, `haptics/input/vibrate`, `haptics/input/pump` oder `windforce/input/mapped` zur Verfügung.

Bei Zahlen: optional Eingang mit `1 − Wert` invertieren, mit Faktor multiplizieren, Offset addieren und auf Minimum/Maximum begrenzen. Float, Integer und Bool werden ausdrücklich typisiert (Bool ab 0,5 wahr). Texte lassen sich mit Typ `s` unverändert an Avatar-Parameter weitergeben. Tool-Kanäle verwenden Zahlen; WindForce nutzt den Bereich 0–1 und seine aktivierte Regel „Direct OSC“.

Beispiel: Quelle `fitness/heart/bpm`, Ziel `/avatar/parameters/HeartRate`, Typ `i`, Faktor 1, Offset 0, Minimum 0, Maximum 240. Fitness und OSC Hub starten. Das Avatar-Ziel muss existieren und beschreibbar sein.

Neue Zuordnungen sind zunächst ausgeschaltet. Nach Aktivierung greifen Änderungen sofort und werden gespeichert. „Aktuellen Avatar übernehmen“ setzt dessen ID als Profil; `*` gilt für alle. Ein Profil wird erst bei bekannter passender Avatar-ID angewendet. Rückkopplungen über Avatar-Ausgänge als erneute Zuordnungs-Eingänge werden abgewiesen. Mehrere unabhängige Quellen für dasselbe Ziel verwenden den jeweils zuletzt eintreffenden Wert. Quelle und Ziel-Tool müssen separat gestartet werden; eine Zuordnung startet keine fremden Programme oder Hardware allein.

Änderungen an Zuordnungen deaktivieren vorsorglich die Haptics-Ausgabe; danach bewusst erneut freigeben. WindForce erhält dabei für den zugeordneten Eingang null. Avatar-Ausgänge werden beim Entfernen einer Zuordnung nicht pauschal überschrieben, weil andere Apps sie verwenden können.

## Haptics

Intiface-Adresse und optionalen Programmstart wie bisher einstellen. Haptics starten, Gerät in der Liste wählen und „Ausgewähltes Gerät übernehmen“ drücken. Die tatsächlichen Fähigkeiten entscheiden: Vibrate für Vibration, Constrict für Pump. Ohne Constrict bleibt der Pump-Test deaktiviert.

Die Standardadressen sind `/avatar/parameters/MaxVibrate` und `/avatar/parameters/MaxPump`. Andere Adressen und ein optionales Inaktivitätslimit lassen sich im aufklappbaren Bereich vor dem Start konfigurieren. Das Limit ist standardmäßig aus, weil VRChat konstante Werte nicht kontinuierlich sendet.

„OSC-Ausgabe aktivieren“ akzeptiert erst danach eintreffende Werte. Beide Kanäle können gleichzeitig arbeiten. Die bestehenden Avatar-Menüs, Stärken und manuellen Overrides des MA-Prefabs werden über dessen fertige OSC-Ausgänge übernommen; das Unity-Projekt wird von HydroLink nicht verändert.

Regler wählen nur die nächste Teststärke. Ein Test schaltet den OSC-Betrieb aus, läuft ungefähr drei Sekunden und stoppt anschließend. Stop, Avatarwechsel, Gerätewechsel und Verbindungsverlust deaktivieren die Ausgabe. Bei Wiederverbindung Gerät neu auswählen und Ausgabe erneut freigeben. Angezeigte Ausgangswerte sind bestätigte Befehlsstärken, keine gemessenen Gerätezustände. Bei Verbindungsverlust kann ein physischer Stop nicht bestätigt werden.

## Technischer Prüfstand

Die Portwahl und `/avatar`-Anmeldung entsprechen der [offiziellen VRChat-OSCQuery-Beschreibung](https://github.com/vrchat-community/osc/wiki/OSCQuery). Getrennte Kanäle, tatsächlich angebotene Feature-Indizes, fehlende Pump-Fähigkeit und Verbindungsabbruch wurden mit einem lokalen simulierten Intiface-WebSocket-Server geprüft. Reale Hardware und der vollständige Avatar-Signalweg benötigen noch den praktischen Test.
