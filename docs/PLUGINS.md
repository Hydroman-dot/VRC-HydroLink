# Plugins ab HydroLink 0.17.2

## Bedienung

Unter **Plugins → Verwaltung** ein Plugin-ZIP hinzufügen, HydroLink neu starten und das gewünschte Plugin ausdrücklich freigeben. Anschließend einzeln starten, stoppen oder neu starten. Mehrere Plugins können gleichzeitig laufen. „Mit HydroLink starten“ gilt pro freigegebenem Plugin und unabhängig von der Autostart-Auswahl der eingebauten Module. Änderungen an diesen Häkchen werden sofort gespeichert. Jedes installierte Plugin erhält einen eigenen Unterreiter im Hauptreiter Plugins. Die Verwaltung bleibt separat. Bestehende Plugin-Ansichten werden beim Aktualisieren der Liste nicht neu erzeugt.

Plugins laufen im Prozess und mit den Benutzerrechten von HydroLink. Das ist keine Sandbox: fehlerhafter oder bösartiger Code kann die gesamte App beeinträchtigen. Freigabe ist eine Vertrauensentscheidung. Standardmäßig sind neue Plugins nicht freigegeben und nicht im Autostart.

Die dauerhafte Ablage ist `%LOCALAPPDATA%/VRC HydroLink/Plugins`. Vorhandene Plugins im `plugins`-Ordner neben der EXE werden weiterhin erkannt; verwaltete Installationen derselben ID haben Vorrang. Für alte Plugins ist der neue Autostart bewusst nicht vorausgewählt. Der Installer aktualisiert nur die Auswahl auf eine neue, separat gespeicherte Version. Geladene DLLs werden niemals überschrieben; Änderungen an Installation oder Versionsauswahl benötigen einen App-Neustart.

## GitHub-Updates

„Beim App-Start nach Plugin-Updates suchen“ ist voreingestellt. Die Prüfung lädt nur Metadaten; sie installiert und aktiviert keinen Code. „Updates prüfen“ ist jederzeit verfügbar. Ein verfügbarer Download bekommt einen Button an der Plugin-Karte; Quelle und Zielversion werden vor der Installation bestätigt. Offline-, Zeitlimit- und GitHub-Fehler werden angezeigt und lassen die installierte Version unverändert.

Das Plugin hinterlegt sein öffentliches Repository im Manifest (`gitHubRepository`: `owner/repository`). Unterstützt wird das neueste stabile GitHub Release, Tag `v1.2.3`, mit Assets:

- `<plugin-id>-<version>.zip`
- `<plugin-id>-<version>.zip.sha256` (SHA-256 als erster Eintrag)

SHA-256 erkennt beschädigte oder abweichende Downloads, bestätigt aber nicht die Vertrauenswürdigkeit des Herausgebers. ZIP und Prüfsumme stammen aus derselben Release-Quelle. Private Repositories, Signaturen, Beta-Kanäle und automatische Hintergrundinstallation sind noch nicht enthalten.

Das ZIP enthält `plugin.json`, Plugin-DLL und benötigte Abhängigkeiten direkt im Hauptordner. Maximal 64 MiB Download, 128 MiB entpackt und 1024 Einträge. Fremde Pfade, ZIP-Symlinks, doppelte Pfade, falsche ID/Version/Quelle, fehlende DLLs und unpassende API-/Mindestversion werden verworfen. Vor der Bestätigung wird kein Plugin-Code ausgeführt.

**Vorherige Version** wählt die erhaltene Vorgängerversion für den nächsten App-Start. Wenn ein Update nicht startet, Autostart abwählen, die vorherige Version auswählen und HydroLink neu starten. Es gibt keinen automatischen Gesundheitstest oder automatische Rückkehr bei beliebigen Laufzeitfehlern. Ungültige Pakete ändern die aktive Auswahl nicht. Alte Versionen werden für die Rückkehr aufbewahrt und noch nicht automatisch bereinigt.

## Plugin-Vertrag

Implementiere `IHydroLinkPlugin` aus `HydroLink.Sdk` (API 1). Beispiel unter `examples/ExamplePlugin`. `Configure` registriert Komponenten und optional `PluginPage`-Ansichten. Komponenten-IDs müssen der Plugin-ID entsprechen oder mit `<plugin-id>.` beginnen. `StartAsync` startet die Arbeit und kehrt zeitnah zurück; `DisposeAsync` beendet sie und gibt Ressourcen frei. Für jeden Neustart wird eine neue Instanz erstellt. Der Host entfernt über seinen Kontext registrierte Bus-Abonnements beim Stoppen auch dann, wenn das Plugin dies vergisst. Während `DisposeAsync` darf das Plugin noch abschließende Stop-Befehle veröffentlichen; danach wird die Bus-Ausgabe des alten Kontexts gesperrt. Eigene Threads, direkte Sockets und externe Ressourcen muss das Plugin selbst beenden.

Beispielmanifest:

```json
{
  "id": "example.counter",
  "name": "Example Counter",
  "assembly": "ExamplePlugin.dll",
  "version": "1.0.0",
  "apiVersion": 1,
  "minimumHydroLinkVersion": "0.17.2",
  "requiresOsc": false
}
```

Optional: `gitHubRepository` für die Updatequelle und `entryType` für den vollständigen Typnamen, wenn eine DLL mehrere Plugin-Klassen enthält. Für jedes separat aktualisierte Plugin ein eigenes Release-Repository verwenden.

## OSC gemeinsam nutzen

Ein Plugin mit `requiresOsc: true` erhält den bestehenden OSC-Dienst als gemeinsame Abhängigkeit. Er bleibt aktiv, solange ein laufendes Plugin ihn benötigt. Plugins abonnieren `osc/value/` am Datenbus und veröffentlichen auf `osc/send` ein Objekt mit `address` und `arguments`. Keine eigenen Empfänger auf den VRChat-Standardports öffnen. Bereits vorhandene Quellen und Module laufen unabhängig weiter. Netzwerk-Ausgaben und Hardware-Freigaben bleiben Aufgabe der jeweiligen Plugin-Implementierung.

## Testumfang

Automatisierte Tests prüfen Mehrfachbetrieb, unabhängigen Stop/Neustart, Abonnement-Aufräumen, unveränderte aktive Version nach abgewiesenem Paket, Versionswechsel/Rückkehr sowie GitHub-Release-Auswahl. Externe Plugins und deren Hardware benötigen zusätzliche Praxistests. OpenShock/PiShock ist nicht Bestandteil dieses Updates.

## App-Updates ab 0.17.3

Verwaltete Plugins liegen weiterhin dauerhaft außerhalb des App-Versionsordners. Freigabe, Autostart und weitere HydroLink-Einstellungen bleiben in der bestehenden settings.json erhalten. Der Updater kopiert zusätzlich alte `plugins`-Ordner neben der EXE vollständig mit ihren Unterordnern und lokalen Konfigurationen in die neue App-Version. Die bisherige Installation bleibt unverändert für eine Rückkehr erhalten. Unterschiedliche Dateien am gleichen Zielpfad führen zu einem Abbruch statt zum Überschreiben.

Für den Übergang von älteren Updatern prüft HydroLink beim Start die vom Updater verzeichnete vorherige Installation: Fehlt der gesamte `plugins`-Ordner in der neuen Version, wird er übernommen. Bereits vorhandene Plugin-Ordner werden durch diese Absicherung nicht überschrieben. Portable Installationen außerhalb der verwalteten Update-Ablage werden nicht automatisch durchsucht. Bei manuellen portablen Updates daher den bisherigen plugins-Ordner mitnehmen oder Plugins über die dauerhafte Plugin-Verwaltung installieren.

## OSC-Eingänge und Empfangsanzeige ab 0.17.4

Plugins können im Manifest `oscInputs` als Zuordnung von vollständiger OSC-Adresse zu Typ (`f`, `i`, `T`, `F`, `s`) angeben. Freigegebene Plugin-Eingänge werden beim Start des OSC-Dienstes vor der Bekanntgabe über OSCQuery registriert. Nach erstmaliger Freigabe oder Änderung der Parameternamen den OSC-Dienst neu starten. Für Hydro Haptic ESB 1.0.0 werden die fünf Eingänge kompatibel aus `hydro-haptic-parameters.json` gelesen; ohne diese Datei gelten die Standardnamen unter HydroHaptic/.

Die Verwaltung zeigt pro laufendem Plugin die Anzahl der an seine Abonnements zugestellten OSC-Liveparameter und den letzten Parameternamen. Das bestätigt die Zustellung im Plugin, nicht dessen Motorfreigabe oder physische Ausgabe. Ein OSCQuery-Wert allein gilt weiterhin nicht als Live-Ereignis und wird nicht als Motorwert oder Lebenszeichen wiedergegeben.

Bei Hydro Haptic ESB benötigt die Vorschau Links, Rechts und Intensität; für die Ausgabe benötigt das Plugin zusätzlich Aktivierung und wechselnde Lebenszeichen. Eine sichtbare Radialmenü-Freigabe alleine bestätigt daher keinen vollständigen Datenfluss. Die Ansichten selbst können beim Verlassen weiterhin eigene Stop-Regeln des jeweiligen Plugins anwenden.

OSCQuery-Abfragewerte werden zusätzlich getrennt als `osc/query-value` + vollständige Adresse veröffentlicht: `{ avatar, queryStarted, value }`, Quelle `osc-query`. Das ist kein Live-OSC-Ereignis. Hydro Haptic ESB 1.0.1 verwendet daraus ausschließlich eine bisher fehlende Intensitäts-Einstellung, mit Avatar-/Zeitprüfung. Kontakte, Lebenszeichen und Freigabe werden nicht aus Abfragen übernommen. Damit braucht ein unveränderter Radialregler nach dem Plugin-Start nicht mehr bewegt zu werden.
