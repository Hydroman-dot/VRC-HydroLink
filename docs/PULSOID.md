# Pulsoid — 0.16.6

Unter Einstellungen → Puls & Avatar „Pulsoid verwenden und VRCOSC-Ausgabe aktivieren“ wählen. Zugangstoken unter Verbindungen eintragen. Speichern und Heartrate neu starten (Konfigurationsänderungen bei gestoppten Modulen). Bestehende eigene Parameteradressen bleiben erhalten; „VRCOSC-Standardadressen einsetzen“ setzt die neun Adressen ausdrücklich zurück.

Standardpräfix: /avatar/parameters/VRCOSC/Heartrate/. Parameter: Connected/Enabled/Beat (Bool), Value/Average (Int), Normalised/Units/Tens/Hundreds (Float). Pulsoid verwendet bei VRCOSC dasselbe Heartrate-Profil wie Bluetooth. Alle Adressen, Normalisierungsgrenzen und Glättungen bleiben einstellbar.

Pulsoid wartet maximal 90 Sekunden pro WebSocket-Empfang und verbindet bei ausbleibenden Nachrichten neu. Fehlversuche erhalten eine wachsende Pause bis 30 Sekunden. Der numerische Server-Abschlusscode wird protokolliert; Token und vollständige URL werden nicht protokolliert. Bei tatsächlicher Trennung werden Avatarwerte sofort ungültig. Kurze Messpausen können bis zur einstellbaren Signalwartezeit (Standard 15 Sekunden, Bereich 5–60) überbrückt werden; die gehaltene Messung ist kein neuer Messwert. Bluetooth behält seine bisherigen fünf Sekunden.

Die gemeldeten häufigen Abbrüche sind noch nicht mit einem Live-Protokoll reproduziert. Tokenablauf, Sensor-/Telefonverbindung und Serververhalten müssen beim Praxistest unterschieden werden. Der Timeout verbessert die Erholung festhängender Verbindungen, verhindert jedoch keine serverseitige Schließung.

Protokollreferenzen (keine GPL-Implementierung übernommen):
- https://github.com/VolcanicArts/VRCOSC-Modules/blob/main/VRCOSC.Modules/Pulsoid/PulsoidModule.cs
- https://github.com/VolcanicArts/VRCOSC/blob/main/VRCOSC.App/SDK/Modules/Heartrate/HeartrateModule.cs
- https://docs.pulsoid.net/
