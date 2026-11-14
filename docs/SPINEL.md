# Lovense Spinel · Windows 0.17.1

Eigenes Profil unter Haptics → Spinel. Intiface muss das Gerät als Lovense Spinel erkennen und seine Fähigkeiten melden.

## Verfügbare Funktionen

- Vibration des Aufsatzes, unabhängig regelbar.
- Hubgeschwindigkeit (Buttplug Oscillate), unabhängig regelbar.
- Manueller Gerätetest ohne VRChat über „Manuell ohne VRChat starten“.
- SPS-Plug-/Socket-Verarbeitung mit getrennten Kanalstärken im Desktop-Profil.
- Stop und bestehende Schutzregeln bei Geräteverlust.
- Akkustand, sofern vom Backend gemeldet.

Die Regler verwenden 0–100 % des vom Backend angebotenen Bereichs. Buttplug definiert beim Spinel zwei Ausgänge mit Bereich 0–20 und einen Batterieeingang. Fehlende Ausgänge bleiben deaktiviert. Keine feste Geräte-ID oder geratene Kanalnummer wird verwendet; die vorhandene Feature-Zuordnung übernimmt gemeldete Indizes.

## Grenzen

Das Gerät besitzt laut Hersteller auch eine Heizfunktion. Das derzeitige Buttplug-Profil bietet jedoch weder Heizung noch einen separaten Turbo-Schalter, Hubposition oder Hubgrenzen an. Diese Funktionen werden nicht als funktionierende Regler vorgetäuscht. 100 % bedeutet das Backend-Maximum, nicht eine bestätigte separate Turbo-Freigabe. Ein anderer Geräteprovider wäre dafür gesondert zu untersuchen.

**Unity bleibt auf ausdrücklichen Nutzerwunsch unverändert.** Der bisherige Wizard 0.16.5 erzeugt noch kein Spinel-Prefab und keine Spinel-Radialparameter. Bei bestehendem Wizard-Profil ohne Spinel bleibt deshalb der profilgebundene SPS-Start gesperrt; der eigenständige manuelle Gerätetest funktioniert unabhängig davon. Die vollständige Avatar-Einrichtung folgt später.

## Quellen (geprüft 14. November 2026)

- Herstellerfunktionen: https://www.lovense.com/faq/Hardware/spinel
- Offizielle Buttplug-Definition, Eintrag Lovense Spinel / Kennung WD: https://github.com/buttplugio/buttplug/blob/master/crates/buttplug_server_device_config/device-config/protocols/lovense.yml
- Offizieller Protokolltest: https://github.com/buttplugio/buttplug/blob/master/crates/buttplug_tests/tests/util/device_test/device_test_case/test_lovense_spinel.yaml

Es wurden Fähigkeiten abgeglichen, kein zusätzlicher Drittanbieter-Quellcode übernommen. Tests mit simuliertem Backend prüfen beide Kanäle, Modellzuordnung, SPS, manuelle Regelung und Stop. Echter Spinel-Praxistest steht aus.
