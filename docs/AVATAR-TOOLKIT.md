# Unity-Toolkit und mehrere Avatare

## Voraussetzungen

Windows x64, HydroLink 0.10, Intiface und aktiviertes OSC in VRChat. Das Unity-Projekt benötigt VRChat Avatar SDK und Modular Avatar. SPS-Steuerung setzt zusätzlich eine funktionierende VRCFury-SPS/OGB-Einrichtung voraus. Für manuelle Steuerung sind keine SPS-Kontakte nötig.

## Einrichten

1. Das Paket aus `packages/` importieren. Alternativ den Ordner `unity/Assets/VRC HydroLink Avatar Toolkit` einschließlich Metadateien in das Projekt übernehmen.
2. **Tools → HydroLink → Avatar Setup Wizard** öffnen und den Avatar auswählen.
3. Max 2 und/oder Ridge auswählen und **Toy-Prefabs anlegen und automatisch uebergeben** drücken.
4. Szene speichern und Avatar bauen/hochladen. Bei einer neuen Upload-ID danach erneut übergeben.
5. Intiface verbinden, HydroLink starten und den Avatar in VRChat laden. Gerät am PC aktivieren, dann im Radialmenü **Haptik einschalten**.

Vorhandene Avatar-Controller und SPS-Sockets werden nicht ersetzt. Erneutes Anlegen erzeugt keine doppelten Toy-Instanzen. Abgewählte vorhandene Instanzen werden nicht gelöscht; zum Entfernen die jeweilige Instanz entfernen und erneut übergeben.

## Radialmenü pro Toy

- **Haptik einschalten:** Hauptschalter, anfangs aus. Ausschalten stoppt auch manuelle Ausgabe.
- **Haptik-Staerken:** SPS-Obergrenzen, pro Kanal getrennt.
- **Manuell:** eigener Einschalter und unabhängige direkte Werte. Max 2: Vibration/Pump; Ridge: Vibration/Rotation/Richtung.
- **Toy stoppen:** entzieht die Gerätefreigabe; danach am PC erneut aktivieren.

Anfangsstärken: 25 %. Ausschalten des manuellen Modus kehrt zu SPS zurück und wartet auf einen neuen Kontakt. Werte bleiben innerhalb derselben Freigabe getrennt erhalten. Nach Stop, Verbindungs- oder Avatarwechsel erneut freigeben; gegebenenfalls Radialschalter aus- und wieder einschalten. PC-Regler wirken auf den aktuell gewählten Modus und werden nicht zurück in das Avatar-Menü geschrieben.

## Mehrere Avatare

Jeden Avatar einmal übergeben. Gespeichert wird lokal unter `%LOCALAPPDATA%/VRC HydroLink/WizardSync/Avatars/<avatar-id>.json`. Unterschiedliche Upload-IDs ergeben getrennte Einrichtungen; gleiche IDs teilen ein Profil. `active.json` bleibt als Startinformation und zur Kompatibilität erhalten. HydroLink wählt automatisch nach der in VRChat erkannten Avatar-ID. Unbekannte Avatare bleiben gesperrt. Eine neue Übergabe aktualisiert nur den zugehörigen Avatar. Die letzte alte Einzeleinrichtung wird übernommen; früher überschriebene Profile müssen erneut übergeben werden.

Unity und HydroLink müssen im selben Windows-Konto laufen. Nach manuellem Hinzufügen/Entfernen eines Prefabs erneut übergeben. Eindeutige Gerätenamen werden automatisch zugeordnet; bei mehreren gleichen Geräten einmal auswählen.

## Grenzen

SPS-Quellen müssen aktiv sein und unterstützte OGB/Pen- oder OGB/Orf-Signale senden. Der stärkste Kontakt zählt; die Socket-Auswertung nutzt 20 cm Referenzlänge. Nur von Intiface gemeldete Gerätefunktionen werden angesteuert. Aktuell enthalten: Max 2 und Ridge. Keine mitgelieferten Avatar-Modelle oder fremden SPS-Assets. Tests verwenden simulierte Hardware; echte Geräte bleiben zu prüfen.

## SPS-Kontakte bleiben bei null (Fix 0.10.1)

VRCFury aktiviert die OSC-Haptik-Kontaktempfänger erst mit `OGB_ENABLED = true`. Version 0.10.0 hat diese Freigabe beim Aktivieren eines Toys nicht gesendet. Seit 0.10.1 fordert HydroLink sie nach erfolgreicher, bewusster Toy-Aktivierung automatisch an. Die Gerätefreigabe und der Haptik-Schalter im Radialmenü bleiben erforderlich. Query-Werte starten keine Ausgabe.

Die Anzeige unterscheidet jetzt vorhandene Avatar-Parameter, die zuletzt abgefragte Kontaktfreigabe und tatsächlich empfangene Live-Quellen. Bei ausgeschalteter Kontaktfreigabe das Toy stoppen und erneut aktivieren. Ein manuelles Ausschalten der SPS-Freigabe wird weiterhin respektiert; HydroLink schaltet sie nicht periodisch zurück ein. Die globale Empfangsfreigabe wird beim Stoppen eines einzelnen Toys nicht ausgeschaltet, da andere Toys und OSC-Apps dieselben Empfänger verwenden können.

Für diesen Fix genügt der Austausch der Windows-App. Das Unity-Paket 0.10.0 bleibt kompatibel; kein erneuter Import oder Avatar-Upload erforderlich. Die Reaktion mit echten Geräten muss anschließend in VRChat geprüft werden.
