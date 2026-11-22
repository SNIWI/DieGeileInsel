# Spezifikation – Die Geile Insel (Dynamic Island für Windows)

> Stand: 16.11.2026 (aktualisiert) · Sprache der App: Deutsch · Plattform: Windows 10 (19041+) / Windows 11

---

## 1. Überblick

**Die Geile Insel** bildet die „Dynamic Island“ des iPhones auf dem Windows-Desktop nach: eine schwarze Pille oben auf dem Bildschirm, die mit Federanimationen wächst und schrumpft, Live-Aktivitäten zeigt (Musik, Timer, Claude Code, Downloads …) und Hinweise (Nachrichten, „fertig“-Meldungen) einblendet. Aufgeklappt wird sie zu einer „großen Island“ mit wischbaren Seiten (Übersicht, Player, Heute, To Do, KI …).

### Ziele
- **Unaufdringlich:** Die Island ist fast nur sichtbar, wenn ein Hinweis kommt oder der Nutzer sie hervorholt.
- **Apple-Gefühl:** Federanimationen, Liquid-Glass-Regler, ruhige Typografie, Animationen „App im Vordergrund“.
- **Alles lokal:** keine eigene Cloud; Dienste (Spotify, Discord, Gemini, Open-Meteo, Microsoft To Do) nur, wenn der Nutzer sie einrichtet.
- **Persönlich:** eigene Katzen, Akzentfarbe, Größe, Seitenreihenfolge, Animationen pro Ereignis.

---

## 2. Technik

| Punkt | Festlegung |
|---|---|
| Sprache / UI | C# · WPF (`UseWPF`), keine XAML-Dateien – die Oberfläche wird im Code aufgebaut |
| Laufzeit | .NET 8 (`net8.0-windows10.0.19041.0`, `RollForward=Major` → läuft auch mit .NET 9/10) |
| Windows-APIs | WinRT (Medien-Sitzungen, Bluetooth, Benachrichtigungen, Geolocation, OCR), Win32 per P/Invoke, UI Automation |
| Einzelinstanz | Mutex `DynamicIsland-Einzelinstanz`; zweiter Start zeigt einen Hinweis und beendet sich |
| Datenordner | `%APPDATA%\DynamicIsland` – `einstellungen.json`, `fehler.log`, `Katzen\` (Fotos) |
| Fehler | Globale Handler fangen Ausnahmen ab und schreiben sie nach `fehler.log` (gleiche Meldung nur einmal) |
| Takt | `DispatcherTimer` mit 50 ms (20×/s) – aktualisiert Aktivitäten, Auto-Ausblenden, Abfragen |

---

## 3. Architektur

```mermaid
flowchart LR
    subgraph Fenster
        IW[IslandWindow<br/>Fenster · Layout · Takt · Gesten]
        SW[SetupWindow<br/>Einstellungen]
    end
    subgraph Aktivitäten
        A[Activity<br/>kompakt · Kreis · groß]
        GA[GenericActivity]
        PL[MediaActivity / SpotifyActivity]
        CL[ClaudeActivity]
        SP[SpotlightActivity]
        PG[Seiten: Heute, To Do, KI, Ton …]
    end
    subgraph Dienste
        MS[MediaService]
        SPS[SpotifyService]
        DS[DiscordService + DiscordUi]
        CT[ClaudeTracker]
        WS[WeatherService]
        CS[CalendarService]
        AP[AirPodsEngine]
        DL[DownloadWatcher / GameDownloadWatcher]
        API[LocalApi :7843]
    end
    IW --> A
    A --> GA & PL & CL & SP & PG
    IW --> MS & SPS & DS & CT & WS & CS & AP & DL & API
    API --> CT
    SW --> IW
```

### 3.1 Hauptbausteine

| Baustein | Aufgabe |
|---|---|
| `IslandWindow` | Transparentes, randloses Topmost-Fenster (1000×580, aktiviert sich nicht). Enthält Pille, Kreis („Bubble“), Kamera/Mikro-Punkte und das Katzen-Panel. Verwaltet die Aktivitätsliste, wählt aus, was angezeigt wird (`Relayout`), animiert Größe/Inhalt, Gesten, Auto-Ausblenden, Menüs, Hotkeys. |
| `Activity` | Basisklasse jeder Anzeige mit drei Ansichten: **kompakt** (Pille), **minimal** (Kreis neben der Pille), **erweitert** (große Island). Eigenschaften: `Priority`, `IsAlert`, `ExpiresAt`, `Finished`, `CanExpand`, `PreferBubble`, `BubbleHoverWidth`. |
| `GenericActivity` | Universelle Aktivität (Symbol, Texte, Fortschritt, Knöpfe, Banner) für Hinweise, Demos und die lokale API. |
| `SetupWindow` | Einstellungsfenster im Stil der Apple-Systemeinstellungen (Seitenleiste + Karten). Seitenleiste in Gruppen: **Island** (Allgemein, Übersicht, Seiten & Widgets, Tastenkürzel) · **Hinweise & Effekte** (Hinweise: Design, Erscheinen, was angezeigt wird, Mitteilungen, Reihenfolge · Animationen: Licht-Effekt, pro Ereignis) · **Persönlich** (Katzen, Kurzbefehle) · **Medien & Geräte** (Spotify, Discord, AirPods) · **Planen** (Kalender, To Do, Wetter) · **KI** (KI, Claude). |
| `Ui` | Farben, Schriften, Bausteine (Text, Glyph, RoundButton, DragBar, Bar, Row3 …), `SpringEase`. |
| `LiquidGlass` | Pixel-Shader für die gläsernen Reglerknöpfe (abschaltbar → „Flach“). |
| `Settings` | JSON-Einstellungen inkl. Versions-Migrationen (`LayoutVersion`, `PagesVersion`, `TilesVersion`). |

### 3.2 Anzeige-Auswahl (`Relayout`)
1. Abgelaufene (`ExpiresAt`) und beendete Aktivitäten werden entfernt (Hinweise bleiben, solange die Maus darauf liegt).
2. **Hinweis** = neueste Aktivität mit `IsAlert`. **Live** = übrige, sortiert nach `Priority` (angepinnte zuerst).
3. Aktivitäten mit `PreferBubble` (Claude) wohnen bevorzugt im **Kreis**, außer sie wurden angetippt (angepinnt).
4. Hauptpille = Hinweis oder wichtigste Live-Aktivität; Kreis = Bubble-Aktivität oder zweitwichtigste.
5. Ist eine Seite aufgeklappt, zeigt die Pille die große Island (Seitengröße einheitlich).

---

## 4. Darstellung

| Zustand | Beschreibung |
|---|---|
| Leerlauf | Pille mit **Uhrzeit links und Wetter rechts** (ohne Wetter: Akku/Datum) oder dünner Strich (Einstellung) |
| Kompakt | Pille mit Symbol links, Inhalt rechts (z. B. Cover + Visualizer, Timer-Ring + Restzeit) |
| Kreis | Zweite Aktivität als 36-px-Kreis neben der Pille; Claude zeigt Ring + Dauer, beim Drüberfahren breit mit Text |
| Banner | Pille mit Titel + Untertitel (Nachrichten, Geräte, Termine); „klein“: 50 px hoch, max. 360 px breit |
| Spotlight | „App im Vordergrund“: App-Symbol im leuchtenden Kreis, Inhalt daneben; „klein“ (Standard): eine Zeile in Pillenhöhe, „groß“: zwei Zeilen, großes Symbol |
| Große Island | Seiten mit Seitensymbolen oben, Katzen-Kratzbaum rechts (optional) |

**Größe der großen Island:** Grundmaß 420×232, skaliert mit `PageScale` (0,7–1) und verbreitert mit `PageWide` (1–1,45). Inhalt bekommt mehr Breite, wird nicht verzerrt. `Scale` vergrößert/verkleinert alles.

**Mitte:** Mit „Island bleibt genau in der Mitte“ (`IslandFixedCenter`, Standard an) hält links ein unsichtbarer Platzhalter so breit wie rechts Kreis + Kamera-/Mikro-Punkte (jedes Bild nachgeführt) – die Pille rutscht nicht mehr ~22 px nach links, wenn der Kreis erscheint. Aus: Pille und Kreis werden zusammen zentriert.

**Schatten:** liegt auf einer eigenen Fläche hinter der Pille (nicht auf dem Inhalt), damit Wischen und Animationen flüssig bleiben.

---

## 5. Bedienung

| Geste | Wirkung |
|---|---|
| Antippen | Aktivität aufklappen · nochmal: Übersicht · nochmal: zu |
| Gedrückt halten | Direkt zur Übersicht |
| Seitlich wischen (groß) | Seite wechseln, im Kreis (nach der letzten kommt wieder die erste, keine Kante); die Nachbarseite schaut beim Ziehen herein (Vorschau) |
| Seitlich wischen (klein) | ← Aktivität ausblenden · → zurückholen |
| Mausrad (groß) | Seite wechseln (Listen scrollen zuerst bis zum Ende, Regler behalten das Rad) |
| Rechte Maustaste ziehen | Island frei verschieben, auch auf andere Bildschirme; rastet nur in der Mitte ein |
| Rechtsklick | Menü mit allen Funktionen |
| Klick daneben | Große Island klappt zu |
| Maus an den oberen Rand | Versteckte Island kommt hervor |
| Kreis antippen | Pille und Kreis tauschen (anpinnen) |

### Tastenkürzel (global)

| Kürzel | Funktion |
|---|---|
| Strg + Alt + Leertaste | Übersicht |
| Strg + Alt + K | Kurzbefehle |
| Strg + Alt + D | Downloads |
| Strg + Alt + M | Discord-Mikro an/aus |
| Strg + Alt + V | Zwischenablage-Verlauf |
| Strg + Alt + A | App-Wechsler |
| Strg + Alt + G | Gemini-Chat |
| Strg + Alt + T | Text vom Bildschirm erkennen (OCR) |
| Strg + Alt + N | Neue Aufgabe (To Do) |

---

## 6. Ein- und Ausblenden

Die Island fährt nach `HideDelay` Sekunden (Standard 3 s) **komplett** nach oben weg, **außer**:
- ein **Hinweis** läuft (Nachricht, „fertig“-Meldung, Spotlight, Sprachmemo),
- die Maus liegt darauf oder am oberen Rand, eine Taste ist gedrückt, ein Menü ist offen,
- das Einstellungsfenster ist **aktiv** (im Hintergrund hält es die Island nicht mehr fest),
- die große Island ist aufgeklappt – sie klappt nach **6 s ohne Maus darauf** von selbst zu und fährt weg.

Nur **Hinweise** und der Nutzer selbst holen die Island hervor. Laufende Aktivitäten (Claude arbeitet, Timer, Musik, Songwechsel, Seitenaufrufe) tun das **nicht**. Bei Vollbild (Spiel/Video) auf demselben Bildschirm blendet sie sich ganz aus (abschaltbar). Hängende Zustände (verpasstes MouseLeave, Menü ohne „Closed“) werden alle 0,5 s bereinigt.

### 6.1 Mehrere Hinweise gleichzeitig
Hinweise werden **gestaffelt** gezeigt statt sich zu überdecken:
- Läuft schon ein Hinweis, stellt sich ein neuer in eine **Warteschlange** und erscheint danach mit seiner vollen Anzeigezeit.
- Ein **wichtigerer** Hinweis (laut Prioritätsliste) unterbricht einen weniger wichtigen; der unterbrochene kommt mit seiner Restzeit (min. 2 s) zurück.
- Gleicher Hinweis-Typ ersetzt sich (z. B. zweimal „Kopiert“); Einträge älter als 90 s verfallen.
- Kurze Systemanzeigen (Lautstärke, Feststelltaste, Discord-Mikro) laufen außerhalb der Schlange.
- **Prioritätsliste** (einstellbar, ↑/↓): Anrufe · Nachrichten · Claude · Termine & Erinnerungen · Timer · Downloads & Spiele · Geräte · Kopieren · Sonstiges.

---

## 7. Seiten der großen Island

Reihenfolge und Auswahl sind einstellbar (*Einstellungen → Seiten & Widgets*). Laufende Aktivitäten (Timer, Claude, Discord …) werden automatisch hinter Übersicht/Player eingefügt.

| Schlüssel | Seite |
|---|---|
| `dashboard` | Übersicht: Uhr, CPU/RAM/Akku, Kacheln (bis 12), Mini-Player |
| `calendar` | **Heute**: Wetter links, nächste 3 Termine rechts |
| `todo` | Microsoft To Do |
| `ai` | Gemini-Chat, Rechner, Text-Drop-Zone |
| `player` | Spotify- oder Medien-Player |
| `w:<id>` | Eigene Widget-Seite (bis 4 Widgets) |
| `sound` | Lautstärke, Mikrofon (schmaler Regler + Pegel) |
| `bluetooth` | AirPods-, Kopfhörer- oder Bluetooth-Seite |
| `downloads`, `clipboard`, `apps`, `brightness`, `shortcuts` | Panels |

**Seitensymbole** oben: farbig oder alle weiß (Einstellung); Claude und Discord mit eigenen gezeichneten Logos.
**Wisch-Vorschau:** zuletzt angezeigte Fassung jeder Seite wird zwischengespeichert und beim Ziehen daneben gezeigt; unbekannte Seiten als Symbol + Name. Beim Loslassen gleitet die neue Seite nahtlos weiter (Federanimation, kein Ausblenden).

---

## 8. Funktionen

### 8.1 Musik
- **MediaService:** liest Windows-Mediensitzungen (Spotify, Browser, VLC, Apple Music …), steuert Play/Pause, Spulen, Zufall, Wiederholen; Akzentfarbe aus dem Cover.
- **SpotifyService:** eigener Player über die Spotify Web API (OAuth, Redirect `http://127.0.0.1:8898/callback`): Gefällt mir, Geräte wechseln, Playlists, Lautstärke (Steuern nur mit Premium).
- **Player-Wechsler:** listet alle Quellen mit Status und Titel; Auswahl wird angeheftet, bis „Automatisch“ gewählt wird.
- **Player-Design (`PlayerUi`):** großes Cover (124 px) mit Farb-Leuchten links; rechts Titel, Künstler, Gerät/App, Spulleiste, Steuerung. Animationen: Cover schrumpft bei Pause (0,88), Songwechsel mit Überblenden + Titel-Einschub, Play-Knopf federt.
- „Zurück zum Player“ öffnet den Player immer (auch pausiert, 2 Minuten erreichbar).

### 8.2 Heute: Wetter + Kalender
- **Wetter:** Open-Meteo (ohne Konto), alle 15 min; Ort aus Einstellungen oder – nur auf Wunsch – Windows-Standort (im Hintergrund, 15 s Zeitlimit). Jetzt, 4–6 Stunden, 3 Tage.
- **Kalender:** iCal-Adressen (verschlüsselt gespeichert), Erinnerung X Minuten vorher, Ansage per Sprachausgabe, „Beitreten“ bei Online-Meetings, Countdown-Aktivität.

### 8.3 Claude Code
- **Hooks** in `%USERPROFILE%\.claude\settings.json` (UserPromptSubmit, PreToolUse, PostToolUse, Notification, Stop, SessionEnd, PreCompact) senden das Hook-JSON per `curl` asynchron an `http://localhost:7843/claude`.
- **ClaudeTracker** führt pro Sitzung eine `ClaudeActivity`: Zustände *arbeitet* (orange, drehender Ring), *wartet* (gelb, „!“), *fertig* (grün, ✓). Werkzeuge werden in deutsche Sätze übersetzt („Bearbeitet Cats.cs“, „Sucht …“, „Denkt nach …“).
- **„Claude ist fertig“:** Der Kreis gleitet in einer Bewegung hinter die Pille und verschmilzt mit ihr, zeitgleich mit dem Strecken (440 ms). Sein ✓ (ohne Ring, in der Ereignisfarbe) liegt auf einer eigenen Ebene über der Pille und gleitet dabei mit dem Kreis an den rechten Rand der Meldung – mit demselben Randabstand wie das Symbol links (`AlertLook.SpotPadLeft`). Am Ende der Meldung löst sich der Kreis federnd wieder heraus und nimmt das ✓ wieder mit; nach insgesamt 6 s ist er weg.
- Titel, Logo und ✓-Abzeichen der Meldung „Claude ist fertig“ nehmen die eingestellte Ereignisfarbe an (sonst Weiß bzw. Claude-Orange).
- **Zeit der Aufgabe:** zählt ab deiner Nachricht. Startet die Island neu oder macht Claude ohne neue Nachricht weiter (kein `UserPromptSubmit`, z. B. Fortsetzung in der Claude-App), wird der Beginn aus dem Zeitstempel der letzten echten Nachricht im Verlauf (`transcript_path`, UTC → Ortszeit, max. 12 h alt) übernommen – nicht „jetzt“.
- Schutz gegen sich überholende Signale: ein „Werkzeug startet“ bis 4 s nach „fertig“ wird ignoriert; kommt es später, beginnt eine neue Aufgabe. Ein `/compact` von Hand legt keinen neuen Kreis an.
- Läuft Claude nicht mehr, verschwindet der Kreis auch ohne „Stop“: Abbruch mit Esc (Vermerk `[Request interrupted by user]` im Verlauf aus `transcript_path`), kein `claude`-Prozess mehr, oder Stille (6 min; 12 min während ein Werkzeug läuft; 30 min beim Warten).
- Einrichtung per Knopf in den Einstellungen; die Seite „Claude“ erscheint nur, wenn Claude installiert ist.

### 8.4 Discord
- RPC über Named Pipe (`discord-ipc-N`) mit eigener Anwendung (Client-ID/Secret).
- Sprachkanal-Ansicht mit Teilnehmern, Sprech-Ringen, Stumm/Ton-aus; „Ton aus“ zeigt das Mikro automatisch als aus.
- **Stream beenden:** über UI Automation wird der Knopf „Streaming beenden“ im Discord-Fenster gedrückt (RPC kann das nicht).

### 8.5 Nachrichten & Antworten
- Windows-Benachrichtigungen werden gespiegelt (WhatsApp, Teams …), eingehende Anrufe größer.
- **Antworten:** Eingabefeld in der Island → App wird aktiviert, Chat geöffnet (WhatsApp: Suche; Discord-DM: Direktlink; Server-Kanal: Schnellsuche), Text eingefügt und – wenn erlaubt – gesendet. Andere Apps: nur einfügen.

### 8.6 Geräte
- **Bluetooth-Seite (Apple-Stil):** runder Bluetooth-Knopf als Schalter (blau = an), Status „An · 1 Gerät verbunden“, Zahnrad zu den Einstellungen; Geräte als Kacheln in zwei Spalten (verbunden = blauer Kreis + „Verbunden · 80 %“).
- **Kopfhörer-Seite:** großer Akku-Ring links, Name + Status rechts, Kapsel „Ton läuft hier“ / „Ton hierher holen“, Lautstärkeregler; keine Wiedergabe-Knöpfe.
- **AirPods:** BLE-Erkennung (Akku, Case, Ohrerkennung), Auto-Pause/-Weiter, Auto-Umschalten, AAP-Verbindung (ANC/Transparenz). Case-Popup nur, wenn deine AirPods wirklich mit dem PC verbunden sind (bis zu 7,5 s nach dem Öffnen gewartet) und die Island gerade nicht groß ist – fremde oder flackernde Cases klappen sie nicht mehr auf.
- **Bluetooth/USB/Controller:** Meldung mit Akku-Ring; pro Gerät höchstens alle 3 h und nur nach ≥ 20 min Abwesenheit (verhindert Flackern bei Standby-Kopfhörern).

### 8.7 Downloads
- Browser-Downloads über Temp-Dateien (`.crdownload`, `.part` …) mit Größe und Geschwindigkeit.
- **Spiele:** Steam (`appmanifest_*.acf`: Bytes, StateFlags) mit Prozent, Geschwindigkeit, Restzeit; Epic ohne Prozent.

### 8.8 Werkzeuge
**Timer-Auswahl** wie in der iPhone-Uhr: drei Drehräder (Std · Min · Sek) mit Auswahlband (Mausrad, Ziehen, Antippen), Schnellwahl (1–30 Min, Pomodoro), runde Knöpfe „Abbrechen“/„Start“; letzte Dauer wird gemerkt.
**Laufender Timer:** Ring, der sich leert (Pille, Kreis und groß), ruhige Ziffern, „Endet um …“, +1/+5/Pause/Beenden.
Pomodoro (25/5/15), Stoppuhr mit Runden, Sprachmemos, Zwischenablage-Verlauf (25 Einträge, nur im Speicher, Passwörter verdeckt), App-Wechsler, Helligkeit (DDC/CI + intern), Kurzbefehle, Texterkennung (OCR) per Mittelklick oder Hotkey, Gemini-Chat.

### 8.9 Katzen
- Bis zu 4 Katzen: Vorlage (9 Rassen), Fell-/Augenfarbe, Muster, Flauschig, Zubehör (+Farbe), Größe, Charakter; optional eigenes Foto.
- Blinzeln, Schwanzwedeln, Umschauen, Hüpfen, Schnurren beim Streicheln, Einschlafen nach Ruhe (je nach Charakter).
- In der großen Island auf einem Kratzbaum (Plätze: oben, Mitte links, Boden links/rechts). Alle 4–9 s passiert etwas: eine wache Katze **springt im Bogen auf einen freien Platz** (nur mit „Herumlaufen“), **schläft ein** (12–32 s, rollt sich ein) oder **zwei springen nach oben und schlafen zusammen** (Köpfe zueinander, 18–36 s, danach höchstens alle 40 s wieder). Beim Aufwachen springt eine weg, die andere rutscht in die Mitte. Streicheln weckt eine Katze. Die Verteilung bleibt beim nächsten Aufklappen erhalten; war die Island über 40 s zu, haben sich die Katzen neu verteilt (manchmal liegen dann zwei schon zusammen oben).
- **Zusammenrollen** (`CatsCurl`, an/aus): Im Schlaf sinkt die Katze zusammen und liegt eingerollt da (Kopf auf den Pfoten, Schwanz um den Körper); beim Aufwachen streckt sie sich wieder hoch. Nur gezeichnete Katzen, Foto-Katzen schlafen wie bisher.
- **Herumlaufen** (`CatsWalk`, an/aus): Ab und zu trippeln die Katzen ein Stück zur Seite (auf und ab, Pfoten abwechselnd, Schwanz nach hinten) und meist wieder zurück. Neben der Island bis zur Island und ~56 px nach rechts; auf dem Kratzbaum die Katzen am Boden über die Bodenplatte. Verspielte laufen öfter, verschlafene seltener; im Schlaf bleiben sie, wo sie sind.
- Eine Vorlage zu wählen setzt eigene Farben/Muster zurück.

### 8.10 Animationen
Pro Ereignis wählbar (*Einstellungen → Animationen*): **Aus · App · App + Licht · Licht · Hüpfen · Licht + Hüpfen**, mit Tester (▶) und „Schnell einstellen“.

- **Farbe pro Ereignis:** Farbpunkt anklicken → Standard oder eine von 10 Farben; gilt für Symbol, Kreis und Licht.
- **Licht-Effekte** (Standard + pro Ereignis überschreibbar): **Umlauf** (Lichtstreifen läuft einmal um die Island), **Schein** (weicher Schimmer dahinter), **Glanz** (Reflex zieht über die Island), **Rahmen** (Rand leuchtet zweimal). Das Licht hat immer die Farbe des Ereignisses.

| Ereignis | Spotlight-Inhalt |
|---|---|
| Claude ist fertig | Claude-Logo · Projekt · Dauer, ✓ am rechten Rand |
| Timer abgelaufen | Timer-Symbol · Uhrzeit |
| Download / Spiel fertig | Datei bzw. Spiel · Größe/Store |
| Kopieren | Zwischenablage-Symbol · Textanfang |
| Neue Nachricht / Gerät | Banner + Hüpfer |

Katzen hüpfen bei jeder Animation mit. „Bewegungen reduzieren“ (Windows) schaltet Animationen ab.

### 8.11 Hinweise (Einstellungen → Hinweise)
- **Aussehen:** Design (`AlertSize`, `AlertLook.cs`), gilt für Kurz-Meldungen und Banner: **Klein** (Standard, eine Zeile, kleines Symbol im getönten Kreis) · **Groß** (großes Symbol mit Leuchten, zwei Zeilen) · **Mitteilung** (iPhone-Stil: Symbol im abgerundeten Quadrat, Titel + „jetzt“, darunter der Text) · **Minimal** (nur farbiges Symbol, schmale Zeile) · **Leuchtend** (Symbol im leuchtenden Kreis, farbiger Schimmer von links, Titel in der Ereignisfarbe). Erscheinen: **Schieben** (Standard), **Tropfen** (federnder Apple-Morph), **Ohne**. Vorschau-Knöpfe für Claude (Kurz-Meldung), Gerät und Nachricht (Banner) – unabhängig von der Animation pro Ereignis.
- **Kurz in der Island anzeigen:** Lautstärke, Kopieren, Feststelltaste, Geräte, Kamera-/Mikro-Punkte, Downloads.
- **Mitteilungen & Anrufe:** Windows-Mitteilungen erscheinen als Banner in der Farbe von „Neue Nachricht“ (nicht in der Akzentfarbe). Mitteilungen der **Claude-App** werden nicht als Chat-Nachricht gezeigt: Kam gerade (≤ 45 s) ein Hook-Signal, gar nicht (die Island zeigt es schon), sonst als Claude-Meldung in der eingestellten Farbe (fertig) bzw. gelb (wartet).
- **Reihenfolge bei mehreren Hinweisen** (`AlertPriority`): oben zuerst; ein wichtigerer Hinweis unterbricht einen weniger wichtigen.
- **App-Logo:** Mitteilungen zeigen das Logo der App, von der sie kommen (aus Windows, `AppIcons` in `Notifications.cs`, einmal pro App geladen); Discord ohne Logo → Discord-Zeichen. Teams-Mitteilungen in Teams-Lila.
- **Anrufe annehmen/ablehnen** (WhatsApp, Teams, Discord ohne RPC, Skype, Telegram, Signal): grüner/roter Knopf im Anruf-Banner. `CallUi.cs` sucht per UI Automation den Knopf „Annehmen“/„Ablehnen“ zuerst in der Windows-Mitteilung (ShellExperienceHost), dann in den Fenstern der App (bevorzugt „nur Audio“ statt Video). Nicht gefunden → App öffnen + kurzer Hinweis.

### 8.12 Obsidian (Einstellungen → Obsidian)
- Auf die Island gezogener **Text, Link oder Dateien** landen im Vault (`Obsidian.cs`). Standard **Inbox-Ordner** (`ObsidianFolder`, automatisch der Ordner mit „Inbox“/„Eingang“ im Namen, z. B. „00 Inbox“): Text/Link wird je eine neue Notiz (Name aus den ersten Worten, Frontmatter `erstellt`, `quelle`), Dateien werden direkt in den Ordner kopiert. Alternativ an eine Tagesnotiz (Ordner + Datumsformat aus `.obsidian/daily-notes.json`) oder feste Notiz (`ObsidianNote`, Standard „Eingang“). Optional Uhrzeit vor jedem Eintrag.
- Vault: aus `%APPDATA%\obsidian\obsidian.json` (zuletzt geöffneter) oder selbst gewählt. Dateien werden in den Anhang-Ordner (`attachmentFolderPath` aus `app.json`, sonst Vault-Ordner) kopiert und verlinkt; Bilder/PDF/Audio eingebettet (`![[…]]`), Dateien im Vault nur verlinkt.
- Dateien gehen immer direkt nach Obsidian; Text nur, wenn die KI-Drop-Zone aus ist – sonst erscheint dort die Karte „In Obsidian“. Bestätigung „… gespeichert“, Antippen öffnet die Notiz.

### 8.13 Claude: Limits & Spracheingabe
- **Nutzungs-Limits** (Pro/Max) gibt es nur von **Claude Code im Terminal** – die Claude-App selbst schickt keine. Angezeigt werden nur echte, höchstens 12 h alte Werte; `used_percentage` wird auf 0–100 begrenzt, `resets_at` als Unix-Sekunden, Millisekunden oder ISO-Datum gelesen. „Verbinden“ trägt zusätzlich eine Statuszeile in Claude Code ein (nur wenn noch keine eigene existiert), die das Statuszeilen-JSON an `/claude-limits` schickt und selbst nichts anzeigt. Werte (`rate_limits.five_hour` / `seven_day`: Prozent + Reset) stehen in den Details der Claude-Pille, auf der Kachel „Claude-Limits“ und in Einstellungen → Claude. Warnung bei 80 % und 95 % je Fenster (`ClaudeLimitWarn`), pro Fenster nur einmal je Stufe.
- **Claude diktieren** (Kachel): holt das Claude-Fenster nach vorn (sonst bleibt das aktive Fenster, z. B. Terminal) und startet die Windows-Spracheingabe (Win+H).

### 8.14 KI-Anbieter
- Einstellungen → KI → Anbieter: **Gemini, ChatGPT (OpenAI), Grok (xAI), Claude (Anthropic), Mistral, DeepSeek, OpenRouter**. Je Anbieter eigener Schlüssel (DPAPI) und Modell (leer = Standard). OpenAI-kompatible Anbieter über `chat/completions`, Claude über `messages`. Gibt es das Modell nicht, wird aus der Modell-Liste des Anbieters ein passendes gewählt und gespeichert.
- Chat, Drop-Zone und Texterkennung zeigen den Namen des gewählten Anbieters.

### 8.15 Weitere Feinheiten
- **Katzen-Laune** (`CatsMoodAuto`): Charakter + Tageszeit (nachts/mittags müde, nachmittags verspielt) + Wetter (Sonne verspielt; Regen, Schnee, Gewitter, Hitze, Kälte müde) ergeben die aktuelle Laune (Schlafen, Laufen, Bewegungen).
- **Licht „Umlauf“** nimmt die Zielgröße der Island und folgt ihr (vorher zu kurz, weil die Island beim Hinweis gerade erst wächst).
- **Position nach Monitor-Wechsel:** DPI fest aus dem Fenster statt aus der Hauptbildschirm-Breite; nach `DisplaySettingsChanged` mehrfach neu platzieren (0,4 / 1,5 / 4 / 9 s), Wächter alle 2 s; die gespeicherte Position wird dabei nicht verändert.
- **SoundCloud** im Browser oder als App wird als Quelle „SoundCloud“ erkannt (Browserfenster mit „SoundCloud“ und dem Songtitel im Titel).
- **Logitech-Akku** (`Logitech.cs`, Einstellung `LogitechEnabled`): liest per HID++ 2.0 direkt über Windows (ohne G HUB/Options+) Name, Typ und Akku kabelloser Logitech-Geräte – an Lightspeed-/Bolt-/Unifying-Empfängern (Geräte 1–6, Schnittstelle FF00/0002) und per Bluetooth (FF43/0202); Funktionen 0x1004, 0x1000 oder 0x1001 (Spannung → %). Alle 2 Min im Hintergrund. Schläft ein Gerät (antwortet nicht), bleibt der letzte Wert bis zu 24 h mit „Ruhezustand“ stehen – ohne neue Meldung. Hinweis nur bei wenig Akku: unter der eingestellten Grenze (`LogitechWarnAt`, 10/15/20/30 %, Standard 15 %) und nochmal bei 5 % – je einmal, bis geladen wird. Keine Meldung beim Verbinden oder Vollladen. Anzeige auf der Bluetooth-Seite (zuerst), Kachel „Maus & Tastatur“ und `/logitech` (API).
---

## 9. Schnittstellen

### 9.1 Lokale API (`http://localhost:7843`, nur dieser PC)

| Pfad | Methode | Zweck |
|---|---|---|
| `/show` | GET | Eigene Live-Aktivität anzeigen/aktualisieren. Parameter: `id`, `title`, `sub`, `left`, `right`, `detail`, `icon` (Hex-Glyph), `emoji`, `color`, `progress` (0–1), `banner=1`, `width`, `seconds` (als Hinweis), `glow` (orbit/halo/shine/ring) |
| `/hide` | GET | Aktivität `id` entfernen |
| `/claude` | POST | Claude-Code-Hook-JSON entgegennehmen |
| `/claude-limits` | POST | Statuszeilen-JSON von Claude Code (Nutzungs-Limits) |
| `/logitech` | GET | Akku von Logitech-Maus/-Tastatur groß zeigen |

Beispiel: `http://localhost:7843/show?id=fussball&title=FCB%202:1%20BVB&right=67'&emoji=⚽`

### 9.2 Externe Dienste (nur wenn eingerichtet)
Spotify Web API · Discord RPC (lokal) · Microsoft Graph (To Do, Gerätecode-Anmeldung) · KI-Anbieter (Gemini, OpenAI, xAI, Anthropic, Mistral, DeepSeek, OpenRouter) · Open-Meteo (Wetter + Geocoding) · iCal-Adressen des Nutzers.

---

## 10. Einstellungen

- **Suche** oben in der Seitenleiste (Strg+F, Esc leert, Enter öffnet den ersten Treffer): durchsucht Seitennamen und alle Texte der Seiten (Überschriften, Schalter, Hinweise; Umlaute egal). Treffer zeigen die Seite mit bis zu 3 Fundstellen; Antippen öffnet die Seite und hebt die Stelle kurz blau hervor. Lokale API: `/settings?page=…&q=…`.
Gespeichert in `%APPDATA%\DynamicIsland\einstellungen.json`. Gruppen (Auswahl):

| Gruppe | Schlüssel |
|---|---|
| Größe & Position | `PageScale`, `PageWide`, `Scale`, `PosX`, `PosY`, `MonitorName` |
| Aussehen | `AccentColor`, `NavIconsColor`, `IdlePill`, `Material`, `GlassTint`, `SpotifyColor` |
| Ausblenden | `AutoHide`, `HideWithActivities`, `HideDelay`, `HideInFullscreen` |
| Animationen | `AnimModes` (Ereignis → Art), `AnimColors`, `GlowStyle`, `GlowStyles` |
| Timer | `LastTimerSeconds` |
| Seiten | `Pages`, `WidgetPages`, `DashboardTiles` |
| Hinweise | `AlertSize` (Design: `small`/`normal`/`ios`/`minimal`/`glow`), `AlertMotion`, `AlertPriority`, `ShowVolume`, `ShowClipboard`, `ShowCapsLock`, `ShowBluetooth`, `ShowPrivacy`, `MirrorNotifications`, `ReplyAutoSend`, `ShowDownloads` |
| Katzen | `CatsMode` (`expanded`/`always`/`off`), `CatsStayVisible`, `CatsCurl`, `CatsWalk`, `Cats` |
| Dienste | Spotify-, Discord-, To-Do-, Gemini-, Kalender-, Wetter-, AirPods-, Claude-Einstellungen |

Migrationen laufen beim Laden automatisch (z. B. neue Seiten einfügen, „Wetter“ in „Heute“ zusammenführen, alte Animations-Schalter übernehmen).

---

## 11. Datenschutz & Sicherheit

- Die App sendet nichts an eigene Server; Telemetrie gibt es nicht.
- Die lokale API lauscht nur auf `localhost`.
- **Verschlüsselt (DPAPI, nur im eigenen Windows-Konto lesbar):** Gemini-Schlüssel, To-Do-Token, Kalender-Adressen.
- **Derzeit unverschlüsselt** in `einstellungen.json`: Spotify- und Discord-Tokens sowie das Discord-Client-Secret (→ offener Punkt).
- Zwischenablage-Verlauf nur im Arbeitsspeicher; passwortähnliche Einträge werden verdeckt.
- Wetter: nur Ortsname bzw. ungefähre Koordinaten an Open-Meteo.

---

### 12.1 Robustheit (Stand Fehlerprüfung)
- **Vollbild-Erkennung:** Maximierte Fenster ohne Titelleiste (Claude, Discord, VS Code – Electron) gelten nicht als Vollbild; die Island versteckt sich nur bei echten Vollbild-Spielen/-Videos.
- **Einstellungen** werden sicher gespeichert (Hilfsdatei → Austausch, vorige Fassung als `.bak`). Ist die Datei kaputt, wird `.bak` geladen und die kaputte als `.bad` aufgehoben.
- **Windows-Mitteilungen:** einzelne Lesefehler werden übersprungen; nach mehreren Fehlern oder einem Startfehler verbindet sich die Island mit wachsendem Abstand (30 s … 10 min) neu, statt für immer aus zu bleiben.
- **Discord:** normales Trennen (Discord startet neu/wird beendet) landet nicht mehr im Fehlerprotokoll und wird erneut versucht; nur ein echter CLOSE-Rahmen gilt als „abgelehnt“.
- **Claude:** „Esc“ zählt nur, wenn die neueste Nutzer-Zeile im Verlauf der Abbruch-Vermerk ist; die Prozessregel greift nur, wenn während der Aufgabe ein `claude`-Prozess lief (npm-Installationen laufen als node.exe). Antippen von „Claude ist fertig“ pinnt die Sitzung 30 s; der Pin endet mit der Aktivität.
- **Kratzbaum** liegt als feste Ebene über den Seiten (wird beim Wischen/Neuaufbau nicht neu gebaut); die Katzen verteilen sich neu, wenn die Island über 40 s zu war.

## 12. Bauen & Veröffentlichen

| Weg | Befehl |
|---|---|
| Starten (Entwicklung) | `Starten.bat` → `dotnet build -c Release` und Start |
| Einzelne EXE (benötigt .NET) | `Als-EXE-bauen.bat` → `dotnet publish -r win-x64 --self-contained false -p:PublishSingleFile=true` |
| Eigenständige EXE (GitHub-Releases) | `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none` → ca. 75 MB, läuft ohne .NET |

Releases: v1.0.0 (erste EXE), v1.1.0 (Logitech, Obsidian, KI-Anbieter, Claude-Limits, Anrufe), v1.2.0 (Claude-Zeiten/-Limits korrigiert, Logitech-Ruhezustand, mehr Animationen in der README).

Autostart: *Einstellungen → System → Mit Windows starten* (Registry `HKCU\…\Run`).

---

## 13. Projektstruktur

```
DynamicIsland/
├─ Program.cs              Einstieg, Einzelinstanz, Fehlerprotokoll
├─ IslandWindow.cs         Fenster, Layout, Gesten, Takt, Menüs, Ereignisse
├─ SetupWindow.cs          Einstellungsfenster
├─ Ui.cs                   Design-Bausteine, Farben, Federanimation
├─ Activities/
│  ├─ Activity.cs          Basisklasse + GenericActivity
│  ├─ Activities.cs        Medien-Player, Timer + Ring, Stoppuhr, Sprachmemo, Uhr, Übersicht
│  ├─ SpotifyPlayer.cs     Spotify-Player, Geräte-/Playlist-Liste
│  ├─ PlayerUi.cs          Gemeinsames Player-Layout + Animationen
│  ├─ Spotlight.cs         „App im Vordergrund“
│  ├─ TimerPicker.cs       Timer-Auswahl (Drehräder wie iPhone-Uhr)
│  ├─ CalendarActivity.cs  Seite „Heute“ (Wetter + Termine)
│  ├─ WeatherActivity.cs   Wetter-Widget
│  ├─ TodoActivity.cs, AiActivity.cs, Panels.cs, Widgets.cs
│  ├─ DiscordVoice.cs, ReplyActivity.cs
│  ├─ AirPodsActivity.cs, HeadphonesActivity.cs, BluetoothActivity.cs
│  ├─ Cats.cs              Katzen, Kratzbaum
│  ├─ Logos.cs             Gezeichnete Claude-/Discord-Logos
│  └─ Demos.cs             Vorführ-Aktivitäten
├─ Services/
│  ├─ MediaService.cs, Spotify.cs, AudioService.cs
│  ├─ Discord.cs, DiscordUi.cs, Notifications.cs
│  ├─ ClaudeTracker.cs, LocalApi.cs
│  ├─ Weather.cs, CalendarService.cs, TodoService.cs, Gemini.cs
│  ├─ AirPods.cs, AirPodsAap.cs, SystemInfo.cs (Bluetooth, Werte)
│  ├─ Downloads.cs, GameDownloads.cs
│  ├─ ScreenOcr.cs, SystemTools.cs, Native.cs, LiquidGlass.cs
│  └─ Settings.cs
└─ docs/                   Grafiken für die README: Standbilder (PNG) und Animationen (GIF: claude-fertig, licht-effekte, lichtring, kopieren, claude-limits, logitech)
```

---

## 14. Bekannte Einschränkungen & offene Punkte

- **Windows-Mitteilungen** mitlesen braucht die Freigabe von Windows; ohne sie gibt es auch kein App-Logo und keine Anruf-Knöpfe.
- **Eine Island pro Bildschirm** ist nicht umgesetzt (derzeit eine Island, frei auf jeden Bildschirm verschiebbar).
- **Tokens** für Spotify/Discord sollten wie die übrigen Zugangsdaten mit DPAPI verschlüsselt werden.
- **WhatsApp-Antworten** öffnen den Chat über die Suche – bei ähnlichen Namen kann der erste Treffer falsch sein (deshalb „direkt absenden“ abschaltbar).
- **Hotkeys:** beim Neu-Registrieren werden nur die IDs 1–6 abgemeldet, registriert werden 1–9.
- **Epic-Downloads** ohne Prozentangabe; Steam aktualisiert den Fortschritt teils sprunghaft.
- **Emojis** (Wetter) werden von WPF einfarbig gezeichnet.
- Discord „Stream beenden“ hängt von der Beschriftung im Discord-Fenster ab (deutsch/englisch erkannt).
