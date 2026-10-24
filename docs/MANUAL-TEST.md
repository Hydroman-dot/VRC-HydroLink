# Manueller Gerätetest ab 0.12.3

1. Alte HydroLink-App schließen und 0.12.3 starten.
2. Haptics starten, verbundenes Toy auswählen und Toy aktivieren.
3. „Manuellen Gerätetest starten (ohne Radialmenü / SPS)“ einschalten. Das Gerät startet mit den manuellen Werten (nach Aktivierung zunächst 25 %).
4. Die Regler steuern jetzt die manuellen Werte des gewählten Toys.
5. Haken entfernen oder Toy stoppen beendet den Test.

Der manuelle Test benötigt keine Radialmenü-Freigabe. Der bestehende Avatar-/Unity-Profilabgleich beim Aktivieren bleibt erhalten. Für SPS muss Haptik weiterhin im Radialmenü eingeschaltet sein; nach dem Test wird auf frische Kontakte gewartet. Avatarwechsel, Verbindungsverlust und Stop beenden die Ausgabe. Ein eingehendes Haptik-Aus aus dem Radialmenü beendet auch den Desktop-Test.
