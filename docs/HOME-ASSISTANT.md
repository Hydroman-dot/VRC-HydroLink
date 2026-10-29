# Home Assistant über MQTT

Ab HydroLink 0.15.0 unter **Einstellungen → Home Assistant**. Die Android-Companion-Verbindung ist unabhängig davon.

## Einrichtung

1. In Home Assistant einen MQTT-Broker einrichten und die MQTT-Integration verbinden.
2. In HydroLink Broker-Host, Port, Benutzer und Passwort eintragen. Für TLS das entsprechende Häkchen aktivieren; das Serverzertifikat muss vertrauenswürdig sein.
3. „MQTT mit dem Fitness-Modul aktivieren“ einschalten, speichern und VR Fitness starten. Zum Ändern laufender Einstellungen zuerst unter Module „Alles stoppen“ verwenden.
4. Der Status zeigt Verbindung und unbestätigte Session-Meldungen. Bei Ausfällen verbindet HydroLink sich automatisch erneut.

Zugangsdaten werden nicht automatisch aus VR Fitness übernommen. Das MQTT-Passwort wird durch Windows für den aktuellen Benutzer verschlüsselt. Es werden ausschließlich Fitness-Daten versendet, keine Haptics- oder Avatar-Werte.

## Live-Sensoren

MQTT Discovery legt unter **HydroLink Fitness** Sensoren für Puls, Geschwindigkeit, Gesamtdistanz, Gesamtschritte und aktive Zeit an. Nachrichten erscheinen alle zwei Sekunden auf `vr_fitness/v1/live`. Messwerte älter als zehn Sekunden werden `null`. Sensoren verfallen nach 15 Sekunden ohne Nachricht. Verfügbarkeit: `vr_fitness/v1/availability` (`online` / `offline`, retained, Last Will).

Discovery: `homeassistant/sensor/hydrolink/<sensor>/config`. Für eine HydroLink-Installation pro Broker vorgesehen. Zeitwerte auf der Leitung bleiben Sekunden; die Oberfläche zeigt h:mm:ss.

## Sessions und bestehender VR-Fitness-Empfänger

MQTT-Sensoren sind kein Session-Archiv. Für Sessions wird der bereits für VR Fitness verwendete Empfänger benötigt, der das folgende v1-Protokoll speichert und bestätigt. Ohne ihn funktionieren Live-Sensoren, Session-Meldungen bleiben lokal gepuffert.

- Topic `vr_fitness/v1/session`, QoS 1, nicht retained.
- Felder: `schema_version: 1`, `session_id`, `sequence`, `event_type` (`start`, `update`, `end`), `active`, `timestamp`, `started_at`, `source`, `heart_rate`, `steps_session`, `distance_session` (km), `calories_session` (Schätzung, kcal), `speed` (km/h), `session_duration` (Sekunden), `steps_today`, `calories_today`.
- Eine Session startet mit einer tatsächlichen HydroLink-Aufzeichnung. Updates werden alle 15 Sekunden gepuffert; Stoppen erzeugt das Endereignis.
- Nach dauerhafter Speicherung antwortet der Empfänger auf `vr_fitness/v1/ack`:

```json
{"status":"stored","test":false,"session_id":"<empfangene session_id>","sequence":0}
```

`sequence` muss der empfangenen Meldung entsprechen. Retained-, Test- und unpassende Bestätigungen werden ignoriert. Der Empfänger muss anhand von `(session_id, sequence)` deduplizieren: Wiederholungen sind normal. Broker-PUBACK allein löscht keine Session. `vr_fitness/v1/error` meldet abgelehnte Daten; der Puffer bleibt bestehen.

Puffer: `<Fitness-Datenordner>/home-assistant/outbox.json`, atomar gespeichert. Nach Programmabbruch werden offene Sessions am letzten dauerhaft gespeicherten Messpunkt beendet; bis zu 15 Sekunden zwischen MQTT-Snapshots können fehlen. CSV wird unabhängig sekündlich geschrieben. MQTT-Puffer-Schreibfehler werden angezeigt und stoppen die CSV-Aufzeichnung nicht.

Ein Brokerwechsel wird mit offenen Meldungen abgelehnt. Den Fitness-Datenordner bei offenem Puffer nicht wechseln: Ein alter Puffer bleibt im alten Ordner. Tageszähler beziehen sich nur auf HydroLink-Aufzeichnungen. Die frühere SQLite-Warteschlange und alte VR-Fitness-Tageszähler werden nicht automatisch importiert.

## Verifikation

Mit isoliertem lokalen MQTT-Broker geprüft: Discovery, Live-Daten, fehlende/korrekte Bestätigungen, Puffer, Broker-Ausfall und Wiederverbindung. Zusätzlich Absturzwiederherstellung, Brokerwechsel-Sperre und Windows-Passwortspeicherung getestet. Die Prüfung mit deiner Home-Assistant-Installation und deren Session-Empfänger steht noch aus.

Grundlagen: [MQTTnet](https://github.com/dotnet/MQTTnet) und [Home Assistant MQTT Discovery](https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery).
