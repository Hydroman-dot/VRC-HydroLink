# Gush 2

Desktop ab 0.16.5: eigenes Profil mit getrennten Reglern für Vibration und Oszillation. Manuelle Steuerung, SPS-Kontakte und Stop verwenden die bestehende gemeinsame Steuerung. Nicht vom Backend gemeldete Kanäle bleiben deaktiviert. Der originale Gush wird nicht automatisch als Gush 2 ausgewählt.

Intiface muss Gush 2 erkennen und seine Funktionen melden. Herstellerprogramme werden nicht übertragen. Backend-Unterstützung: https://github.com/intiface/intiface-central/blob/main/CHANGELOG.md (ab 2.6.5); Hersteller: https://www.lovense.com/guide/gush2.

Unity-Wizard-Quellcode: Auswahl Gush 2 erzeugt Parameter unter HydroLink/Gush2 mit Enabled, Vibration, Oscillation, Manual, ManualVibration, ManualOscillation und Stop. Paket 0.16.5 wurde aus Unity 2022.3.22f1 exportiert; alle neun Prefabs wurden validiert, keine Kompilierungsfehler. Der echte Gerätetest steht aus; automatische Tests verwenden ein simuliertes Backend und bewegen kein Gerät.
