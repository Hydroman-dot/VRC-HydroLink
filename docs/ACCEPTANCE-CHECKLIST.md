# HydroLink Praxistestliste — 0.16.2

Je Test Datum, App-Version, Avatar/Gerät, Ergebnis (bestanden/fehlgeschlagen/offen) und Fehlerbeschreibung notieren. Automatisierte Tests ersetzen die folgenden realen Geräteprüfungen nicht. Vor jedem Toy-Test niedrige Stärke wählen und Stop erreichbar halten.

## 1. Zeitkorrektur — zuerst

- [ ] Unter Einstellungen → Verbindungen ist „VRTI liefert die aktive Zeit in Millisekunden“ aktiv. Module neu starten.
- [ ] Dieselbe aktive Zeit in VRTI und HydroLink vergleichen: Beispiel 730364761 muss 202:52:44 ergeben, nicht 202879:06:01. Kleine Unterschiede durch Aktualisierung sind zulässig.
- [ ] Eine Minute gehen: Zeit steigt um rund 00:01:00; beim Anhalten bleibt die aktive Zeit entsprechend VRTI stehen.
- [ ] Dashboard und VR-Fitness-Karte stimmen überein. Neue CSV-Spalte active_time_total_s enthält Sekunden. Historische CSVs bleiben unverändert.
- [ ] Distanz und Schritte zusätzlich mit VRTI vergleichen; die Zeitkorrektur ändert deren bisherige Umrechnung nicht.

## 2. Bluetooth-Puls / Coospo H9Z — neu, noch nicht hardwaregeprüft

- [ ] H9Z tragen; Windows-Bluetooth aktiv; VRCOSC-Bluetooth-Modul für den Test beenden.
- [ ] Einstellungen → Bluetooth-Puls: suchen, eigenen H9Z auswählen, Bluetooth als Pulsquelle wählen, speichern.
- [ ] VR Fitness starten: echter Puls erscheint und stimmt ungefähr mit einer Referenzanzeige überein. Keine Doppelverbindung erzwingen, falls der Sensor nur eine Verbindung erlaubt.
- [ ] Sensor abnehmen/außer Reichweite: nach spätestens fünf Sekunden ohne gültige Messung wird die Avatar-Ausgabe ungültig/0; Beat stoppt. Nach Rückkehr verbindet HydroLink erneut.
- [ ] HydroLink neu starten: Geräteauswahl bleibt erhalten. Bluetooth aus/an und Sensor aus/an erholen sich ohne Programmabsturz.
- [ ] Optional WindForce ebenfalls auf Bluetooth stellen; Pulsquelle wird gemeinsam genutzt, Fitness-Stopp darf WindForce-Quelle nicht ungewollt beenden.

## 3. VRCOSC-kompatible Avatar-Anzeige

- [ ] Passendes Heartrate-Prefab auf einem Testavatar vorhanden; OSC in VRChat eingeschaltet; „alle neun VRCOSC-Pulsparameter“ in HydroLink aktiv.
- [ ] Connected und Enabled werden bei gültigem Puls true, bei Verlust false.
- [ ] Value und Average sind Ganzzahlen; Normalised ist Float im eingestellten Bereich 0–1.
- [ ] Legacy-Anzeige: bei 123 bpm entsprechen Units/Tens/Hundreds 0,3/0,2/0,1. Falls realer Puls anders ist, passende Ziffern prüfen.
- [ ] Beat mit Umschaltmodus und Impulsmodus testen. Er ist ein errechneter Animationstakt.
- [ ] Glättung an/aus, Mittelwert-Zeitraum und eigene Parameternamen speichern und prüfen.
- [ ] Beide Startreihenfolgen (VRChat zuerst / HydroLink zuerst), Avatarwechsel, OSC aus/an. Keine konkurrierende VRCOSC-Ausgabe auf denselben Parametern.

## 4. Aufzeichnung und Auswertung

- [ ] Manuelle Session starten, 2–3 Minuten aufzeichnen, stoppen: CSV/JSON sichtbar, Auswertung öffnet sich und Werte plausibel.
- [ ] Automatik startet bei Bewegung, stoppt nach konfigurierter Ruhe; manuelles Stoppen startet nicht sofort erneut.
- [ ] Datenordner und Einstellungen bleiben nach Neustart erhalten. Alte VR-Fitness-Sessions öffnen sich unverändert.
- [ ] Notiz, Wochenziele, Export, Backup und Wiederherstellung in separaten Importordner prüfen.
- [ ] Schreibfehler bei einem Testordner werden sichtbar gemeldet; die App behauptet keine erfolgreiche Aufzeichnung.

## 5. Home Assistant / Companion

- [ ] Eigener MQTT-Broker korrekt eingetragen; Live-Sensoren erscheinen und zeigen plausible Werte.
- [ ] Session-Empfänger bestätigt gespeicherte session_id/sequence; Puffer wird danach kleiner. MQTT-Broker allein ist kein Session-Empfänger.
- [ ] Broker offline: Session bleibt lokal; nach Wiederkehr wird sie einmal logisch gespeichert, Wiederholungen werden dedupliziert.
- [ ] App-Neustart mit offenem Puffer verliert nichts. Keine Zugangsdaten in Export/Diagnose veröffentlichen.
- [ ] Falls Android Companion genutzt: eigene Verbindung, Session-Empfang, Schritte/Kalorien-Optionen separat prüfen.

## 6. Haptics und Unity — je tatsächlich verfügbarem Toy

- [ ] Modelle: Max 2, Ridge, Solace Pro, Dolce, Gemini, Nora, Gravity, Lush Mini. Nicht vorhandene Geräte als „offen“ markieren.
- [ ] Manuell: jeder angebotene Kanal einzeln, Richtung/Hubbereich soweit unterstützt, Stop und deaktivierte nicht unterstützte Kanäle.
- [ ] SPS: Plug und Socket getrennt; mehrere Kontakte, stärkster Kontakt; Kontaktende stoppt; Manueller Modus verdrängt SPS korrekt.
- [ ] Radialmenü: Haptik-Einschalter, SPS-Stärken, eigenes Manuell-Untermenü und getrennte Toy-Regler.
- [ ] Mehrere Toys gleichzeitig und mehrere Avatare: korrekte Profilwahl; Avatarwechsel übernimmt keine alten Kontaktwerte.
- [ ] Intiface-/Toy-Verlust, Stop, Modulneustart, Akkuanzeige (falls gemeldet).
- [ ] Wizard auf Testkopie: vorhandene Controller/Sockets erhalten, erneuter Durchlauf erzeugt keine Duplikate, gemeinsames HydroLink-Untermenü, automatische Übergabe.

## 7. WindForce, Verbindungen und Bedienung

- [ ] Manuell, Quellenpriorität, Kalibrierung, Anstieg/Ausklang und Pause am Lüfter prüfen. Verlorenes Eingangssignal/Stop beendet Ausgabe.
- [ ] HydroLink und VRCOSC starten in beiden Reihenfolgen ohne Portfehler; andere OSC-Apps bleiben nutzbar.
- [ ] Einzelne Module neu starten: andere Module und gemeinsam genutzte Quellen bleiben korrekt aktiv.
- [ ] Einstellungen-Unterreiter einschließlich OSCQuery vollständig lesbar, keine weißen Flächen, alle Felder erreichbar; kleineres Fenster und Windows-Skalierung prüfen.
- [ ] Ausblenden inaktiver Module, Dashboard-Layout, Programmeinstellungen und Pluginseiten bleiben nach Neustart erhalten.
- [ ] OSC-Cache-Funktion nur mit gesicherten Testdaten prüfen; erwartete Avatar-Konfiguration wird anschließend neu erzeugt.

## 8. Update und längerer Lauf

- [ ] Startabfrage erkennt neue Version; Ablehnen lässt aktuelle Version laufen, Zustimmen erhält Einstellungen und eine Desktop-Verknüpfung.
- [ ] Rückkehr zur vorherigen installierten Version prüfen, ohne persönliche Daten zu löschen.
- [ ] 30–60 Minuten realer Betrieb: keine stetig wachsende Speichernutzung, keine Aussetzer, keine mehrfachen CSV-Sessions oder Hintergrundprozesse nach Beenden.

Noch offen außerhalb des Praxistests: Hauptlizenz wählen, Icon-Herkunft dokumentieren und Windows-SDK-Weiterverteilung abschließend prüfen; siehe LICENSE-AUDIT.md.

## Heartrate als eigenes Modul
- [ ] Heartrate ohne Fitness starten: Puls und Avatar-Parameter kommen an; keine Fitness-Aufzeichnung wird gestartet.
- [ ] Fitness starten und stoppen: Heartrate bleibt aktiv.
- [ ] Heartrate stoppen: Avatar-Ausgabe wird zurückgesetzt; aktive Fitness/WindForce behalten ihren Sensor.
- [ ] Heartrate neu starten und zuletzt alle Verbraucher stoppen: Sensor wird erst beim letzten Verbraucher freigegeben.
