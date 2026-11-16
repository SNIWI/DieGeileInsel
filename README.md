# Dynamic Island für Windows

Die schwarze Pille vom iPhone, oben in der Mitte deines Bildschirms. Sie wächst und schrumpft mit Federanimationen, zeigt Live-Aktivitäten und lässt sich antippen, gedrückt halten und wegwischen.

## Starten

1. **Doppelklick auf `Starten.bat`.** Beim ersten Mal lädt .NET ein paar Pakete und baut die App. Das dauert etwa eine Minute.
2. Oben in der Bildschirmmitte erscheint die Island mit „Dynamic Island ist aktiv“.

Wenn `Starten.bat` meldet, dass .NET fehlt (oder „No .NET SDKs were found“), musst du das **.NET 8 SDK** (oder neuer) installieren: <https://dotnet.microsoft.com/download>

**Als normale App benutzen:** Starte `Als-EXE-bauen.bat`. Danach liegt im Ordner `Fertige App` eine einzelne `DynamicIsland.exe`. Unter *Rechtsklick → Einstellungen → Mit Windows starten* startet sie automatisch mit Windows.

## Bedienung

| Geste | Was passiert |
|---|---|
| **Antippen** | Entfaltet die Island (z. B. den Musik-Player). **Nochmal tippen** öffnet die Übersicht mit allen Funktionen, ein weiteres Mal schließt sie. |
| **Gedrückt halten** | Springt direkt zur Übersicht mit allen Funktionen |
| **Wischen nach links** | Aktivität ausblenden |
| **Wischen nach rechts** | Ausgeblendete Aktivität zurückholen |
| **Kleinen Kreis antippen** | Hauptpille und Kreis tauschen |
| **Mausrad über der großen Island** | Seite wechseln (runter = nächste, hoch = vorige) |
| **Antippen im Leerlauf** | Übersicht mit **allen Funktionen**: Timer, Stoppuhr, Memo, Pomodoro, Sperren, Demos, Einstellungen, Position, Hilfe, Beenden |
| **Rechtsklick** | Menü mit denselben Funktionen |
| **Rechte Maustaste halten + ziehen** | Island verschieben (auch zur Seite oder nach unten). Rastet in der Mitte und am oberen Rand ein. |
| **Maus an den oberen Bildschirmrand** | Holt die Island zurück, wenn sie sich versteckt hat |

**Automatisch ausblenden:** Wenn nichts läuft, fährt die Island nach 3 Sekunden nach oben weg. Bei einem Hinweis, Musik, Timer usw. kommt sie von selbst zurück. Abschalten: *Einstellungen → Automatisch nach oben ausblenden*. Die Wartezeit steht in `IslandWindow.cs` (`HideAfterSeconds`).

**Beenden:** Rechtsklick auf die Island → *Beenden*, oder Island antippen → roter Button *Beenden*. Notfalls im Task-Manager `DynamicIsland.exe` beenden.

Laufen zwei Aktivitäten gleichzeitig, zeigt die Hauptpille die wichtigere. Die andere sitzt als kleiner Kreis daneben.

## Übersicht & Einstellungen

- **Übersicht** (Island antippen oder halten): Uhr, CPU/RAM/Akku und deine Kacheln, 4 pro Reihe. Welche Kacheln erscheinen und in welcher Reihenfolge, stellst du unter *Einstellungen → Übersicht* ein (bis zu 12, mit Vorschau).
- **Einstellungen** (Kachel „Einstellungen“ oder Rechtsklick → *Einstellungen …*): Seitenleiste wie in den Apple-Systemeinstellungen mit *Allgemein* (Ausblenden nach 2/3/5/10 s, Größe Klein bis Sehr groß, Position), *Übersicht*, *Hinweise*, *Katzen*, *Kurzbefehle*, *Discord* und *Tastenkürzel*.

## Spotify (eingebauter Player)

Unter *Einstellungen → Spotify* verbindest du dein Spotify-Konto (einmalig eine kostenlose App im Spotify Developer Dashboard anlegen, Redirect-URI `http://127.0.0.1:8898/callback`). Danach zeigt die Island den eingebauten Player: Cover, „Gefällt mir“, Spulen, Zufall, Wiederholen, Lautstärke, **Gerät wechseln** (PC, Handy, Lautsprecher) und **Playlists starten**. Das funktioniert auch, wenn die Musik auf dem Handy läuft. Steuern erlaubt Spotify nur mit Premium, Anzeigen und Liken gehen mit jedem Konto.

## Seiten wischen

In der großen Island wischst du seitlich zwischen den Seiten: Übersicht · Player · laufende Aktivitäten (Timer, Discord …) · deine Panels (Kurzbefehle, Apps, Zwischenablage, Ton, Helligkeit, Downloads – in der Reihenfolge deiner Kacheln). Die Punkte unten zeigen, wo du bist.

## Neu: Zwischenablage, Apps, Helligkeit, Geräte

- **Zwischenablage-Verlauf** (Kachel oder **Strg + Alt + V**): die letzten 25 kopierten Texte, Bilder und Dateien. Antippen fügt den Eintrag direkt in die App ein, in der du gerade bist. Der Verlauf liegt nur im Arbeitsspeicher und wird nie gespeichert. Was nach einem Passwort aussieht, wird verdeckt angezeigt.
- **App-Switcher** (Kachel oder **Strg + Alt + A**): alle offenen Fenster als Symbole, Antippen holt das Fenster nach vorne.
- **Helligkeit** (Kachel): Regler für jeden Bildschirm. Externe Monitore laufen über DDC/CI (muss im Monitor-Menü an sein), der Laptop-Bildschirm über Windows.
- **Geräte verbinden**: Bluetooth-Kopfhörer, Mäuse, Tastaturen, Xbox-Controller, USB-Sticks und externe Festplatten erscheinen mit einem Ring, der sich füllt und bis zum exakten Akkustand (bzw. belegten Speicher) hochzählt. Bei Xbox-Controllern meldet Windows nur 4 Akku-Stufen, deshalb „ca.“.
- **Katzen in der Island**: Machst du die Island groß, sitzen deine Katzen seitlich auf einem Kratzbaum mit schwingendem Spielball.

## Musik-Player & Ton

- **Player** (Island antippen, während Musik läuft): großes Cover, Titel, Interpret, Spulen per Klick oder Ziehen, Zufall und Wiederholen (wenn die App das unterstützt, z. B. Spotify), Lautstärkeregler und das aktuelle Ausgabegerät. Cover oder „In Spotify öffnen“ öffnet die App.
- **Ton-Panel** (Übersicht → *Ton*, Lautstärke-Hinweis antippen oder Lautsprecher-Button im Player): Lautstärke, **Ausgabegerät wechseln** (z. B. Lautsprecher ↔ Kopfhörer), Mikrofon stummschalten, Mikrofon-Lautstärke mit Live-Pegel, Lautstärkemixer.
- Spielt Spotify, wird es bevorzugt (auch wenn nebenbei ein Video läuft). Beim Song-Überspringen klappt der Player nicht mehr zu. In der Übersicht gibt es einen schlichten Mini-Player, der immer erreichbar ist.
- Läuft Musik, blendet sich die Island trotzdem nach 3 Sekunden aus und zeigt sich bei jedem neuen Song kurz. Abschalten: *Einstellungen → … auch bei Musik, Timer und Downloads*.

## Katzen 🐱

Island antippen → **Katzen** (oder Rechtsklick → *Katzen, Discord & mehr …*). Dort suchst du dir bis zu 4 Katzen aus: Schwarz, Smoking, Getigert, Grau, Siam, Glückskatze, Weiß, Kartäuser oder Creme. Du kannst sie benennen oder ein **eigenes Foto** nehmen. Standardmäßig tauchen die Katzen auf, sobald du die Island groß machst, und setzen sich unten neben sie. Unter *Einstellungen → Katzen* wählst du stattdessen „Immer“ oder „Aus“. Sie blinzeln, wedeln mit dem Schwanz, schauen sich um, schlafen nach einer Minute Ruhe ein (mit „z“) und wachen auf, sobald etwas passiert. **Anklicken = streicheln** (Herz + Schnurr-Hinweis).

## Für deinen PC

| Funktion | So geht's |
|---|---|
| ⬇️ **Downloads live** | Chrome, Edge, Firefox, Opera: Name, Größe, Geschwindigkeit. Fertig → Tippen öffnet die Datei, Ordner-Button zeigt sie im Explorer. Liste der letzten Downloads: Island antippen → *Downloads* oder **Strg + Alt + D** |
| ⚡ **Kurzbefehle** | Deine Programme, Ordner und Webseiten als App-Symbole. Island antippen → *Kurzbefehle* oder **Strg + Alt + K**. Bearbeiten im Einstellungsfenster |
| 💬 **Mitteilungen & Anrufe** | Neue Nachrichten von WhatsApp, Discord, Teams usw. erscheinen oben. Anrufe größer mit grünem Hörer. Windows muss den Zugriff erlauben (*Einstellungen → Datenschutz → Mitteilungen*) |
| 🎧 **Discord-Overlay** | Nach dem Verbinden: neue Nachrichten und Erwähnungen. Im Sprachkanal oder Anruf: Profilbilder mit **grünem Ring, wer gerade spricht**, Name des Sprechers in der Pille, erweitert die ganze Liste mit Stumm-Symbolen und Buttons für Mikro, Ton und Auflegen. **Strg + Alt + M** schaltet dein Mikro |
| ⌨️ **Tastenkürzel** | **Strg + Alt + Leertaste** öffnet die Übersicht von überall |

### Discord einrichten (einmalig)

1. Im Einstellungsfenster auf **Developer Portal** → *New Application* (Name z. B. „Island“).
2. Links **OAuth2** → bei *Redirects* `http://localhost` eintragen, speichern.
3. **Client ID** und (über *Reset Secret*) das **Client Secret** kopieren und im Einstellungsfenster einfügen.
4. **Verbinden** klicken und in Discord **Autorisieren**.

Das funktioniert mit deinem eigenen Discord-Account. Secret und Anmeldung werden nur in `%AppData%\DynamicIsland\einstellungen.json` gespeichert. Ob es gerade *klingelt*, gibt Discord über diese Schnittstelle nicht heraus. Sobald du im Anruf bist, erscheint das Overlay. Klingel-Mitteilungen von Windows werden aber als Anruf angezeigt.

## Apple-Design

Abstände im 8-pt-Raster, Buttons mindestens 44 pt, Schrift mindestens 11 pt, Apple-Systemfarben, iOS-Schalter und gruppierte Listen im Einstellungsfenster. Ist in Windows *Animationen anzeigen* aus, verzichtet die Island auf Federn und Katzen-Animationen (wie „Bewegung reduzieren“ beim iPhone).

## Was echt funktioniert

| Funktion | Woher die Daten kommen |
|---|---|
| 🎵 **Musik & Podcasts** mit Cover, Titel, Fortschritt, Play/Pause/Vor/Zurück, Spulen per Klick | Windows-Medienschnittstelle. Funktioniert mit Spotify, YouTube im Browser, Apple Music, VLC usw. |
| 🌈 **Visualizer** in der Farbe des Albumcovers | Echter Ausgabepegel des Lautsprechers |
| 🔊 **Lautstärke-Anzeige**, 🔇 **Stumm**, 🎙️ **Mikrofon stumm** | Windows Core Audio |
| 🔋 **Laden / Akkubetrieb / Akku schwach** | Nur bei Laptops |
| 🎧 **Bluetooth verbunden / getrennt** inkl. Akkustand | Den Akkustand gibt es nur, wenn das Gerät ihn an Windows meldet |
| 🟢🟠 **Datenschutz-Punkte** für Kamera und Mikrofon, mit Name der App | Dieselbe Quelle wie das Kamera-Symbol in der Windows-Taskleiste |
| ⏱️ **Timer**, **Stoppuhr** (mit Runden), 🍅 **Pomodoro** (25/5, jede 4. Pause 15 Min) | Laufen in der App |
| 🎙️ **Sprachmemos** mit Live-Wellenform | Werden in `Dokumente\Sprachmemos` gespeichert |

### Weitere Ideen, die schon drin sind

- 📋 **„Kopiert“-Hinweis** mit Vorschau. Was nach einem Passwort aussieht, wird als •••• angezeigt.
- ⇪ **Feststelltaste an/aus**
- 💾 **USB-Stick eingesteckt**: Antippen öffnet ihn im Explorer
- 🌐 **Online/Offline**, wenn das Netzwerk wegfällt
- 🎮 **Blendet sich automatisch aus**, wenn ein Spiel oder Video im Vollbild läuft
- 🖥️ **Dashboard** mit Uhr, Datum, CPU, RAM und Akku sowie Buttons für Timer, Stoppuhr, Memo, Pomodoro und PC sperren
- 🔌 **Eigene Live-Aktivitäten über eine lokale API** (siehe unten)

## Demos

Für Navigation, Flüge, Uber, Lieferdienste, Sport, Anrufe, Bildschirmaufnahme, Hotspot, AirDrop, Face ID und Apple Pay gibt es unter Windows keine Schnittstelle. Diese Aktivitäten findest du deshalb unter **Rechtsklick → Demo-Aktivitäten**. Sie zeigen das komplette Design und alle Animationen mit simulierten Daten. Den Anruf kannst du zum Beispiel annehmen, stummschalten und auflegen.

## Eigene Live-Aktivitäten (lokale API)

Solange die Island läuft, hört sie auf `http://localhost:7843`. Das geht nur von deinem eigenen PC aus. Damit kannst du echte Daten selbst einspeisen, etwa Fußballergebnisse aus einer API, deinen Klausurkalender oder den Status eines Downloads.

**Im Browser ausprobieren:**

```
http://localhost:7843/show?id=test&title=Hallo&right=42&icon=E709&color=FF9F0A
http://localhost:7843/hide?id=test
```

**Mit Python:**

```python
import urllib.parse, urllib.request

def island(**params):
    url = "http://localhost:7843/show?" + urllib.parse.urlencode(params)
    urllib.request.urlopen(url)

island(id="essen", emoji="🍔", title="Lieferung unterwegs", sub="Burger Palast",
       left="Essen", right="12 Min", progress=0.6, color="FF9F0A")
```

| Parameter | Bedeutung |
|---|---|
| `id` | Name der Aktivität. Mit derselben id wird sie aktualisiert. |
| `title`, `sub`, `detail` | Texte im erweiterten Widget (`title` erscheint auch links in der Pille) |
| `left`, `right` | Texte links und rechts in der kleinen Pille |
| `icon` | Segoe-Icon als Hex-Code, z. B. `E709` (Flugzeug), `E804` (Auto), `E916` (Stoppuhr) |
| `emoji` | Stattdessen ein Emoji |
| `color` | Akzentfarbe, z. B. `30D158` (grün) |
| `progress` | Fortschritt von `0` bis `1` |
| `banner=1` | Größere Pille mit Titel und Untertitel |
| `seconds=5` | Nur ein kurzer Hinweis, verschwindet nach 5 Sekunden |
| `width` | Breite der Pille in Pixeln |

## Anpassen

- **Farben, Schriften, Buttons:** `Ui.cs`
- **Größe, Position, Animationen, Gesten, Menü:** `IslandWindow.cs` (die Federanimation heißt `SpringEase` und steht in `Ui.cs`)
- **Neue Aktivität:** Am einfachsten nimmst du eine `GenericActivity` (Beispiele in `Activities/Demos.cs`). Für ganz eigene Layouts leitest du von `Activity` ab (siehe `Activities/Activities.cs`).
- Die Einstellungen landen in `%AppData%\DynamicIsland\einstellungen.json`.

## Wenn etwas nicht klappt

- Fehler werden in `%AppData%\DynamicIsland\fehler.log` protokolliert.
- **Keine Musik zu sehen?** Die App muss die Windows-Mediensteuerung unterstützen. Ein guter Test: Wenn beim Drücken der Lautstärketasten Titel und Cover im Windows-Lautstärke-Popup erscheinen, klappt es auch hier.
- **Die Island verdeckt etwas?** Unter *Rechtsklick → Einstellungen* schaltest du „Im Leerlauf als Pille zeigen“ aus. Dann ist sie im Leerlauf nur ein dünner Strich.

## Claude Code in der Island

Unter *Einstellungen → Claude → Mit Claude Code verbinden* trägt die App kleine Hooks in `%USERPROFILE%\.claude\settings.json` ein. Claude Code meldet dann jeden Schritt an `http://localhost:7843/claude` (nur lokal). Die Pille zeigt, was Claude gerade macht (z. B. „Bearbeitet Cats.cs“) und wie lange die Aufgabe schon läuft; antippen zeigt Schritt-Dauer, Anzahl der Aktionen und deine Nachricht. Gelb = Claude wartet auf dich, grün = fertig.
