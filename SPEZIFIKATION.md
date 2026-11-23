# Spezifikation – Die Geile Insel

- **Produkt:** Dynamic Island für den Windows-Desktop
- **Version:** 1.3.0
- **Stand:** 23.11.2026
- **Sprache der App:** Deutsch
- **Plattform:** Windows 10 (19041+) und Windows 11, x64

---

## 1. Überblick

- Schwarze Pille oben am Bildschirm, nach dem Vorbild der iPhone-Dynamic-Island
- Wächst und schrumpft mit Federanimationen
- Zeigt Live-Aktivitäten: Musik, Timer, Claude Code, Downloads, Anrufe
- Blendet Hinweise ein: Nachrichten, Fertig-Meldungen, Geräte, Termine
- Aufgeklappt: große Island mit wischbaren Seiten (Übersicht, Player, Wetter, Kalender, To Do, KI …)

### Ziele

- **Unaufdringlich:** nur sichtbar bei Hinweisen oder wenn der Nutzer sie hervorholt
- **Apple-Gefühl:** Federanimationen, Liquid-Glass-Regler, ruhige Typografie
- **Lokal:** keine eigene Cloud, externe Dienste nur nach Einrichtung durch den Nutzer
- **Persönlich:** Katzen, Farben, Größe, Schrift, Seiten und Animationen frei einstellbar

---

## 2. Technik

- **Sprache / UI:** C#, WPF (`UseWPF`), keine XAML-Dateien – Oberfläche komplett im Code
- **Laufzeit:** .NET 8 (`net8.0-windows10.0.19041.0`), `RollForward=Major` (läuft auch mit .NET 9/10)
- **Windows-APIs:**
  - WinRT: Medien-Sitzungen, Bluetooth, Benachrichtigungen, Geolocation, OCR
  - Win32 per P/Invoke
  - UI Automation (Discord-Stream, Anruf-Knöpfe)
  - HID (Logitech HID++ 2.0)
- **Einzelinstanz:** Mutex `DynamicIsland-Einzelinstanz`, zweiter Start zeigt Hinweis und beendet sich
- **Datenordner:** `%APPDATA%\DynamicIsland`
  - `einstellungen.json` (+ `.bak`, `.bad`)
  - `fehler.log`
  - `Katzen\` (Fotos)
- **Fehler:** globale Handler, Ausnahmen nach `fehler.log`, gleiche Meldung nur einmal
- **Takt:** `DispatcherTimer`, 50 ms (20×/s) – Aktivitäten, Auto-Ausblenden, Abfragen

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
        PG[Seiten: Wetter, Kalender, To Do, KI, Ton …]
    end
    subgraph Dienste
        MS[MediaService]
        SPS[SpotifyService]
        DS[DiscordService + DiscordUi]
        CT[ClaudeTracker]
        WS[WeatherService]
        CS[CalendarService]
        AP[AirPodsEngine]
        LG[Logitech]
        DL[DownloadWatcher / GameDownloadWatcher]
        API[LocalApi :7843]
    end
    IW --> A
    A --> GA & PL & CL & SP & PG
    IW --> MS & SPS & DS & CT & WS & CS & AP & LG & DL & API
    API --> CT
    SW --> IW
```

### 3.1 Hauptbausteine

- **`IslandWindow`**
  - transparentes, randloses Topmost-Fenster (1000×580), aktiviert sich nicht
  - enthält Pille, Kreis („Bubble“), Kamera-/Mikro-Punkte, Katzen-Panel
  - verwaltet Aktivitätsliste, Auswahl (`Relayout`), Größen-/Inhaltsanimation
  - Gesten, Auto-Ausblenden, Menüs, Hotkeys, lokale API
- **`Activity`** – Basisklasse jeder Anzeige
  - Ansichten: **kompakt** (Pille), **minimal** (Kreis), **erweitert** (große Island)
  - Eigenschaften: `Priority`, `IsAlert`, `ExpiresAt`, `Finished`, `CanExpand`, `PreferBubble`, `BubbleHoverWidth`, `IconFactory`
- **`GenericActivity`** – universell (Symbol, Texte, Fortschritt, Knöpfe, Banner) für Hinweise, Demos, API
- **`SetupWindow`** – Einstellungen im Stil der Apple-Systemeinstellungen (Seitenleiste + Karten, Suche)
- **`Ui`** – Farben, Schriften, Bausteine (Text, Glyph, RoundButton, Bar, Row3 …), `SpringEase`, `TextBoost`
- **`LiquidGlass`** – Pixel-Shader für gläserne Reglerknöpfe (abschaltbar: „Flach“)
- **`Settings`** – JSON-Einstellungen mit Migrationen (`LayoutVersion`, `PagesVersion`, `TilesVersion`)

### 3.2 Anzeige-Auswahl (`Relayout`)

1. Abgelaufene (`ExpiresAt`) und beendete Aktivitäten entfernen (Hinweise bleiben, solange die Maus darauf liegt)
2. **Hinweis** = neueste Aktivität mit `IsAlert`; **Live** = übrige, nach `Priority` (angepinnte zuerst)
3. `PreferBubble` (Claude) wohnt bevorzugt im Kreis, außer angepinnt
4. Hauptpille = Hinweis oder wichtigste Live-Aktivität; Kreis = Bubble- oder zweitwichtigste Aktivität
5. Aufgeklappte Seite: Pille zeigt die große Island (einheitliche Seitengröße)

---

## 4. Darstellung

### 4.1 Zustände

- **Leerlauf:** Uhrzeit links, Wetter rechts (farbiges Symbol, abschaltbar); ohne Wetter Akku oder Datum; alternativ dünner Strich
- **Kompakt:** Symbol links, Inhalt rechts (z. B. Cover + Visualizer, Timer-Ring + Restzeit)
- **Kreis:** zweite Aktivität als 36-px-Kreis neben der Pille; Claude mit Ring + Dauer, beim Drüberfahren breit mit Text
- **Banner:** Titel + Untertitel (Nachrichten, Geräte, Termine); „klein“: 50 px hoch, max. 360 px breit
- **Spotlight („App im Vordergrund“):** App-Symbol im leuchtenden Kreis, Inhalt daneben; klein (eine Zeile) oder groß (zwei Zeilen)
- **Große Island:** Seiten mit Seitensymbolen oben, optional Katzen-Kratzbaum rechts

### 4.2 Größe

- Grundmaß der großen Island: 420×232
- `PageScale` (0,7–1): verkleinert die große Island
- `PageWide` (1–1,45): verbreitert sie, Inhalt bekommt mehr Breite, keine Verzerrung
- `Scale`: skaliert alles
- `PageFont`: Schrift der Seiten, unabhängig von der Island-Größe (siehe 7.2)

### 4.3 Mitte und Schatten

- **Genau in der Mitte** (`IslandFixedCenter`, Standard an): unsichtbarer Platzhalter links, so breit wie Kreis + Kamera-/Mikro-Punkte rechts – Pille rutscht nicht zur Seite
- Aus: Pille und Kreis werden zusammen zentriert
- Schatten auf eigener Fläche hinter der Pille, damit Wischen und Animationen flüssig bleiben

---

## 5. Bedienung

### 5.1 Gesten

- **Antippen:** Aktivität aufklappen · erneut: Übersicht · erneut: zu
- **Gedrückt halten:** direkt zur Übersicht
- **Seitlich wischen (groß):** Seite wechseln, im Kreis (nach der letzten die erste); Nachbarseite schaut beim Ziehen herein
- **Seitlich wischen (klein):** links = Aktivität ausblenden · rechts = zurückholen
- **Mausrad (groß):** Seite wechseln; Listen scrollen zuerst, Regler behalten das Rad
- **Seitensymbol antippen:** direkt zur Seite
- **Rechte Maustaste ziehen:** Island verschieben, auch auf andere Bildschirme; rastet nur in der Mitte ein
- **Rechtsklick:** Menü mit allen Funktionen
- **Klick daneben:** große Island klappt zu
- **Maus an den oberen Rand:** versteckte Island kommt hervor
- **Kreis antippen:** Pille und Kreis tauschen (anpinnen)

### 5.2 Tastenkürzel (global)

- `Strg + Alt + Leertaste` – Übersicht
- `Strg + Alt + K` – Kurzbefehle
- `Strg + Alt + D` – Downloads
- `Strg + Alt + M` – Discord-Mikro an/aus
- `Strg + Alt + V` – Zwischenablage-Verlauf
- `Strg + Alt + A` – App-Wechsler
- `Strg + Alt + G` – KI-Chat
- `Strg + Alt + T` – Text vom Bildschirm erkennen (OCR)
- `Strg + Alt + N` – neue Aufgabe (To Do)

---

## 6. Ein- und Ausblenden

### 6.1 Auto-Ausblenden

- Island fährt nach `HideDelay` (Standard 3 s) komplett nach oben weg
- Ausnahmen:
  - ein Hinweis läuft (Nachricht, Fertig-Meldung, Spotlight, Sprachmemo)
  - Maus auf der Island oder am oberen Rand, Taste gedrückt, Menü offen
  - Einstellungsfenster ist aktiv (im Hintergrund nicht)
  - große Island aufgeklappt – klappt nach 6 s ohne Maus von selbst zu
- Nur Hinweise und der Nutzer holen die Island hervor; laufende Aktivitäten (Claude, Timer, Musik) nicht
- Vollbild (Spiel/Video) auf demselben Bildschirm: Island blendet sich aus (abschaltbar)
- Hängende Zustände (verpasstes MouseLeave, Menü ohne „Closed“) werden alle 0,5 s bereinigt

### 6.2 Mehrere Hinweise

- Laufender Hinweis: neuer Hinweis wartet in einer Schlange, danach volle Anzeigezeit
- Wichtigerer Hinweis unterbricht weniger wichtigen; der unterbrochene kehrt mit Restzeit (min. 2 s) zurück
- Gleicher Typ ersetzt sich (z. B. zweimal „Kopiert“); Einträge älter als 90 s verfallen
- Kurze Systemanzeigen (Lautstärke, Feststelltaste, Discord-Mikro) laufen außerhalb der Schlange
- Prioritätsliste (einstellbar): Anrufe · Nachrichten · Claude · Termine · Timer · Downloads & Spiele · Geräte · Kopieren · Sonstiges

---

## 7. Seiten der großen Island

### 7.1 Seiten

- Reihenfolge und Auswahl: Einstellungen → Seiten & Widgets
- Laufende Aktivitäten (Timer, Claude, Discord …) automatisch hinter Übersicht/Player
- Schlüssel:
  - `dashboard` – Übersicht im Sperrbildschirm-Stil: Datum über großer Uhr, rechts Player (wenn Musik läuft) oder Wetter, unten runde Knöpfe gleichmäßig über die Breite
  - `weather` – Wetter: jetzt, Stunden mit Tagesname, Tage (nur „Ausführlich“)
  - `calendar` – Kalender: nächste Termine, optional Wetter links
  - `todo` – Microsoft To Do
  - `ai` – KI-Chat, Rechner, Text-Drop-Zone
  - `player` – Spotify- oder Medien-Player
  - `bluetooth` – AirPods-, Kopfhörer- oder Bluetooth-Seite
  - `sound` – Lautstärke, Mikrofon
  - `w:<id>` – eigene Widget-Seite (bis 4 Widgets)
  - `downloads`, `clipboard`, `apps`, `brightness`, `shortcuts` – Panels

### 7.2 Schrift und Stil

- **Schrift** (`PageFont`): Normal 1,0 · Groß 1,2 (Standard) · Größer 1,4 · Sehr groß 1,6
  - wirkt beim Aufbau einer Seite (`Ui.TextBoost`), Symbole wachsen zu 60 % mit
  - Island bleibt gleich groß
- **Stil** (`PageStyle`):
  - `clean` (aufgeräumt, Standard): weniger Einzelheiten, größer
  - `full` (ausführlich): alle Details
- **Pro Seite** (`PageLooks[key] = { Font, Style }`): Knopf „Aa“ in Seiten & Widgets, leer = wie alle
- **Aufgeräumt** im Detail:
  - Wetter: ohne Gefühlt/Wind/Feuchte, ohne Regen-%, ohne Tageszeile, 5 statt 6 Stunden
  - Kalender: größere Termine, max. 3
  - Bluetooth: größere Kacheln, ohne Linkzeile

### 7.3 Einheitliches Design

- Alle Seiten im Stil der Übersicht: gleicher Rand (`Ui.PageMargin`), gedämpfte Schrift (`Ui.Soft`)
- Kopf (`Ui.PageHead`): optionale kleine Zeile über dem Titel (19 pt), rechts runde Knöpfe (`Ui.CircleButton`) statt Text-Links
- Listen (Downloads, Zwischenablage) als gruppierte Karte (`Ui.Group`)

### 7.4 Navigation

- **Seitensymbole** oben: Symbol 13 px, Feld 21 px hoch; farbig oder alle weiß
- Claude und Discord mit eigenen gezeichneten Logos
- **Wisch-Vorschau:** letzte Fassung jeder Seite zwischengespeichert, beim Ziehen daneben gezeigt; unbekannte Seiten als Symbol + Name
- Beim Loslassen gleitet die neue Seite nahtlos weiter (Federanimation, kein Ausblenden)

---

## 8. Funktionen

### 8.1 Musik

- **MediaService:** Windows-Mediensitzungen (Spotify, Browser, VLC, Apple Music, SoundCloud …)
  - Play/Pause, Spulen, Zufall, Wiederholen
  - Akzentfarbe aus dem Cover
  - SoundCloud im Browser oder als App als eigene Quelle erkannt
- **SpotifyService:** Spotify Web API (OAuth, Redirect `http://127.0.0.1:8898/callback`)
  - Gefällt mir, Geräte, Playlists, Lautstärke (Steuern nur mit Premium)
- **Player-Wechsler:** alle Quellen mit Status und Titel; Auswahl angeheftet bis „Automatisch“
- **Player-Design (`PlayerUi`):**
  - Cover 124 px mit Farbleuchten links; Titel, Künstler, Gerät, Spulleiste, Steuerung rechts
  - Cover schrumpft bei Pause (0,88), Songwechsel mit Überblenden + Titel-Einschub, Play-Knopf federt
- „Zurück zum Player“ öffnet den Player immer (auch pausiert, 2 Minuten erreichbar)

### 8.2 Wetter

- Quelle: Open-Meteo (ohne Konto), alle 15 min
- Ort: aus den Einstellungen oder – nur auf Wunsch – Windows-Standort (15 s Zeitlimit)
- **Wetter-Seite:**
  - Kopf: Symbol, Temperatur, Ort, Beschreibung · Max/Min
  - Stunden mit Tagesname („So 19“)
  - 3 Tage mit vollem Tagesnamen (nur „Ausführlich“)
- **Farbige Symbole** (`WeatherIcon`, Vektor, 24×24):
  - Sonne gelb, Mond hell, Wolken weiß/grau, Regen blau, Eisregen hellblau, Schnee hellblau, Gewitter mit gelbem Blitz, Nebel grau
  - in Leerlauf-Pille, Wetter-Seite, Kalender-Seite und Widget
- **Einstellungen:** eigene Seite an/aus, Wetter in der kleinen Ansicht an/aus
- Temperaturen ohne „-0°“

### 8.3 Kalender

- iCal-Adressen (Google, Outlook, iCloud), verschlüsselt gespeichert, alle 10 min geladen
- Erinnerung X Minuten vorher, Meldung bei Beginn, Ansage per Sprachausgabe
- „Beitreten“ bei Online-Meetings, Countdown-Aktivität
- Ladefehler (z. B. 401) werden auf der Seite angezeigt statt „Wird geladen …“
- **Einstellungen:** eigene Seite an/aus, Wetter links auf der Kalender-Seite (`CalendarWithWeather`)
- Migration v8 trennt die frühere Seite „Heute“ in Wetter und Kalender

### 8.4 Claude Code

- **Hooks** in `%USERPROFILE%\.claude\settings.json`
  - Ereignisse: UserPromptSubmit, PreToolUse, PostToolUse, Notification, Stop, SessionEnd, PreCompact
  - senden das Hook-JSON per `curl` asynchron an `http://localhost:7843/claude`
- **ClaudeTracker:** eine `ClaudeActivity` pro Sitzung
  - Zustände: arbeitet (orange, Ring), wartet (gelb, „!“), fertig (grün, Haken)
  - Werkzeuge als deutsche Sätze („Bearbeitet Cats.cs“, „Sucht …“, „Denkt nach …“)
- **„Claude ist fertig“:**
  - Kreis gleitet hinter die Pille und verschmilzt mit ihr (440 ms)
  - Haken gleitet an den rechten Rand der Meldung
  - danach löst sich der Kreis federnd heraus, nach 6 s weg
  - Titel, Logo und Haken in der Ereignisfarbe
- **Laufzeit:**
  - zählt ab der Nachricht des Nutzers
  - nach Neustart der Island oder Fortsetzung ohne neue Nachricht: Beginn aus dem letzten echten Nachrichten-Zeitstempel im Verlauf (`transcript_path`, UTC → Ortszeit, max. 12 h)
  - „Werkzeug startet“ bis 4 s nach „fertig“ ignoriert, später = neue Aufgabe
  - `/compact` legt keinen neuen Kreis an
- **Kreis verschwindet ohne „Stop“:**
  - Abbruch mit Esc (Vermerk `[Request interrupted by user]` im Verlauf)
  - kein `claude`-Prozess mehr
  - Stille: 6 min, 12 min bei laufendem Werkzeug, 30 min beim Warten
- Einrichtung per Knopf in den Einstellungen; Seite „Claude“ nur, wenn Claude installiert ist

### 8.5 Claude: Limits und Spracheingabe

- **Nutzungs-Limits** (Pro/Max) nur von Claude Code im Terminal (die Claude-App sendet keine)
  - nur echte Werte, höchstens 12 h alt
  - `used_percentage` auf 0–100 begrenzt; `resets_at` als Unix-Sekunden, Millisekunden oder ISO
  - „Verbinden“ trägt eine Statuszeile ein (nur wenn keine eigene existiert), die das JSON an `/claude-limits` sendet
  - Anzeige: Details der Claude-Pille, Kachel „Claude-Limits“, Einstellungen → Claude
  - Warnung bei 80 % und 95 % je Fenster (`ClaudeLimitWarn`), je Stufe einmal
- **Claude diktieren** (Kachel): holt das Claude-Fenster nach vorn, startet Windows-Spracheingabe (Win+H)

### 8.6 Nachrichten, Antworten, Anrufe

- Windows-Mitteilungen werden gespiegelt, Farbe von „Neue Nachricht“
- **App-Logo** aus Windows (`AppIcons`, einmal pro App geladen); Discord ohne Logo: Discord-Zeichen; Teams in Teams-Lila
- **Claude-App-Mitteilungen:** nicht als Chat; bei frischem Hook-Signal (≤ 45 s) gar nicht, sonst als Claude-Meldung
- **Antworten:**
  - Knopf nur bei Chat-Apps: WhatsApp, Discord, Teams, Telegram, Signal, Messenger, Slack, Instagram, Skype, Threema, Element, Smartphone-Link
  - Eingabefeld in der Island → App aktivieren, Chat öffnen (WhatsApp: Suche; Discord-DM: Direktlink; Server-Kanal: Schnellsuche), Text einfügen
  - WhatsApp und Discord-DMs: optional direkt absenden (`ReplyAutoSend`)
- **Anrufe** (WhatsApp, Teams, Discord ohne RPC, Skype, Telegram, Signal):
  - grüner/roter Knopf im Anruf-Banner
  - `CallUi` sucht per UI Automation „Annehmen“/„Ablehnen“ in der Windows-Mitteilung, dann in den App-Fenstern (Audio vor Video)
  - nicht gefunden: App öffnen + Hinweis

### 8.7 Discord

- RPC über Named Pipe (`discord-ipc-N`) mit eigener Anwendung (Client-ID/Secret)
- Seite im Discord-Look:
  - Kopf: Discord-Logo, grünes „Sprachverbindung“ / „Anruf verbunden“ mit Dauer, Kanalname
  - Steuerung rechts im Kopf: Mikro, Ton aus, Stream beenden (dunkelgraue Kreise, rot wenn aus), Auflegen als rote Kapsel
  - Teilnehmer als dunkle Kacheln (Avatar mittig, Name unten links in dunkler Kapsel), grüner Rahmen beim Sprechen, rotes Abzeichen bei stumm/Ton aus
  - Kacheln werden mit mehr Teilnehmern kleiner (bis 4 / 8 / mehr)
- „Stream beenden“ per UI Automation im Discord-Fenster (RPC kann das nicht)

### 8.8 Geräte

- **Bluetooth-Seite:** runder Schalter (blau = an), Status, Zahnrad; Geräte als Kacheln in zwei Spalten
- **Kopfhörer-Seite:** Akku-Ring, Name + Status, „Ton läuft hier“ / „Ton hierher holen“, Lautstärke
- **AirPods:**
  - BLE: Akku, Case, Ohrerkennung
  - Auto-Pause/-Weiter, Auto-Umschalten, AAP (ANC/Transparenz)
  - Case-Popup nur bei wirklich verbundenen eigenen AirPods (bis 7,5 s gewartet), nicht bei großer Island
- **Bluetooth/USB/Controller:** Meldung mit Akku-Ring, pro Gerät max. alle 3 h und nur nach ≥ 20 min Abwesenheit
- **Logitech** (`Logitech.cs`, `LogitechEnabled`):
  - HID++ 2.0 direkt über Windows, ohne G HUB/Options+
  - Empfänger (Lightspeed, Bolt, Unifying; Geräte 1–6, FF00/0002) und Bluetooth (FF43/0202)
  - Funktionen 0x1004, 0x1000, 0x1001 (Spannung → %)
  - Abfrage alle 2 min im Hintergrund
  - schlafendes Gerät: letzter Wert bis 24 h mit „Ruhezustand“
  - Warnung nur unter `LogitechWarnAt` (10/15/20/30 %, Standard 15 %) und bei 5 %, je einmal bis geladen wird
  - Anzeige: Bluetooth-Seite (zuerst), Kachel „Maus & Tastatur“, API `/logitech`

### 8.9 Downloads

- Browser-Downloads über Temp-Dateien (`.crdownload`, `.part` …) mit Größe und Geschwindigkeit
- Steam (`appmanifest_*.acf`): Prozent, Geschwindigkeit, Restzeit
- Epic: ohne Prozent

### 8.10 Werkzeuge

- **Timer-Auswahl:** drei Drehräder (Std · Min · Sek), Schnellwahl (1–30 min, Pomodoro), letzte Dauer gemerkt
- **Laufender Timer:** Ring leert sich (Pille, Kreis, groß), „Endet um …“, +1/+5/Pause/Beenden
- Pomodoro (25/5/15), Stoppuhr mit Runden, Sprachmemos
- Zwischenablage-Verlauf: 25 Einträge, nur im Arbeitsspeicher, Passwörter verdeckt
- App-Wechsler, Helligkeit (DDC/CI + intern), Kurzbefehle
- Texterkennung (OCR) per Mittelklick oder Hotkey

### 8.11 KI-Anbieter

- Gemini, ChatGPT (OpenAI), Grok (xAI), Claude (Anthropic), Mistral, DeepSeek, OpenRouter
- je Anbieter eigener Schlüssel (DPAPI) und Modell (leer = Standard)
- OpenAI-kompatibel über `chat/completions`, Claude über `messages`
- unbekanntes Modell: passendes aus der Modell-Liste des Anbieters wählen und speichern
- Chat, Drop-Zone und Texterkennung zeigen den Namen des Anbieters

### 8.12 Obsidian

- Text, Links und Dateien auf die Island ziehen → Vault (`Obsidian.cs`)
- **Ziel:**
  - Inbox-Ordner (Standard; automatisch der Ordner mit „Inbox“/„Eingang“): je Text eine neue Notiz mit Frontmatter `erstellt`, `quelle`; Dateien direkt in den Ordner
  - Tagesnotiz (Ordner + Format aus `.obsidian/daily-notes.json`)
  - feste Notiz (`ObsidianNote`, Standard „Eingang“)
  - optional Uhrzeit vor jedem Eintrag
- Vault aus `%APPDATA%\obsidian\obsidian.json` oder selbst gewählt
- Anhänge in den Anhang-Ordner (`attachmentFolderPath`), Bilder/PDF/Audio eingebettet
- Dateien immer nach Obsidian; Text nur, wenn die KI-Drop-Zone aus ist (sonst Karte „In Obsidian“)

### 8.13 Katzen

- Bis zu 4 Katzen: Vorlage (9 Rassen), Fell-/Augenfarbe, Muster, Flauschig, Zubehör, Größe, Charakter, optional Foto
- Blinzeln, Schwanzwedeln, Umschauen, Hüpfen, Schnurren beim Streicheln, Einschlafen
- **Kratzbaum** in der großen Island (Plätze: oben, Mitte links, Boden links/rechts)
  - alle 4–9 s: Sprung auf freien Platz, Einschlafen (12–32 s) oder zu zweit oben schlafen (18–36 s)
  - Streicheln weckt; Verteilung bleibt, nach > 40 s zu neu verteilt
- **Zusammenrollen** (`CatsCurl`): im Schlaf eingerollt, nur gezeichnete Katzen
- **Herumlaufen** (`CatsWalk`): trippeln ab und zu zur Seite und zurück
- **Laune** (`CatsMoodAuto`): Charakter + Tageszeit + Wetter (Sonne verspielt; Regen, Schnee, Gewitter, Hitze, Kälte müde)
- Vorlage wählen setzt eigene Farben/Muster zurück

### 8.14 Animationen

- Pro Ereignis: Aus · App · App + Licht · Licht · Hüpfen · Licht + Hüpfen, mit Tester und „Schnell einstellen“
- Farbe pro Ereignis: Standard oder eine von 10 Farben (Symbol, Kreis, Licht)
- **Licht-Effekte** (Standard + pro Ereignis):
  - Umlauf: Lichtstreifen läuft einmal um die Island (folgt der Zielgröße)
  - Schein: weicher Schimmer dahinter
  - Glanz: Reflex zieht über die Island
  - Rahmen: Rand leuchtet zweimal
- **Spotlight-Inhalte:**
  - Claude fertig: Logo · Projekt · Dauer, Haken rechts
  - Timer abgelaufen: Symbol · Uhrzeit
  - Download/Spiel fertig: Datei bzw. Spiel · Größe/Store
  - Kopieren: Symbol · Textanfang
  - Nachricht/Gerät: Banner + Hüpfer
- Katzen hüpfen mit; „Bewegungen reduzieren“ (Windows) schaltet Animationen ab

### 8.15 Hinweise

- **Design** (`AlertSize`): Klein (Standard) · Groß · Mitteilung (iPhone-Stil) · Minimal · Leuchtend
- **Erscheinen** (`AlertMotion`): Schieben (Standard) · Tropfen · Ohne
- Vorschau-Knöpfe für Claude, Gerät, Nachricht
- **Kurz anzeigen:** Lautstärke, Kopieren, Feststelltaste, Geräte, Kamera-/Mikro-Punkte, Downloads
- **Reihenfolge** (`AlertPriority`): oben zuerst, wichtigere unterbrechen

### 8.16 Position und Monitore

- DPI aus dem Fenster statt aus der Hauptbildschirm-Breite
- nach `DisplaySettingsChanged` mehrfach neu platzieren (0,4 / 1,5 / 4 / 9 s), Wächter alle 2 s
- gespeicherte Position bleibt unverändert

---

## 9. Schnittstellen

### 9.1 Lokale API (`http://localhost:7843`, nur dieser PC)

- **`/show`** (GET) – eigene Aktivität anzeigen/aktualisieren
  - Parameter: `id`, `title`, `sub`, `left`, `right`, `detail`, `icon` (Hex-Glyph), `emoji`, `color`, `progress` (0–1), `banner=1`, `width`, `seconds` (als Hinweis), `glow` (orbit/halo/shine/ring)
  - Beispiel: `/show?id=spiel&title=FCB%202:1%20BVB&right=67'&icon=E7FC`
- **`/hide`** (GET) – Aktivität `id` entfernen
- **`/claude`** (POST) – Claude-Code-Hook-JSON
- **`/claude-limits`** (POST) – Statuszeilen-JSON (Nutzungs-Limits)
- **`/logitech`** (GET) – Akku von Logitech-Maus/-Tastatur zeigen
- **`/page`** (GET)
  - `?key=` – Seite öffnen (weather, calendar, todo …), `close` = zuklappen
  - `?dir=1|-1` – wischen (mit Animation)
- **`/settings`** (GET) – Einstellungen öffnen, `?page=…&q=…` (Seite, Suche)
- **`/demo`** (GET) – `?on=1|0`: Demo-Modus für Aufnahmen, blendet Medien-Titel, Cover und Player-Seite aus

### 9.2 Externe Dienste (nur wenn eingerichtet)

- Spotify Web API
- Discord RPC (lokal)
- Microsoft Graph (To Do, Gerätecode-Anmeldung)
- KI-Anbieter (Gemini, OpenAI, xAI, Anthropic, Mistral, DeepSeek, OpenRouter)
- Open-Meteo (Wetter + Geocoding)
- iCal-Adressen des Nutzers

---

## 10. Einstellungen

- Datei: `%APPDATA%\DynamicIsland\einstellungen.json`
- **Suche** in der Seitenleiste (Strg+F, Esc leert, Enter öffnet ersten Treffer)
  - durchsucht Seitennamen und alle Texte, Umlaute egal
  - bis zu 3 Fundstellen je Seite, Antippen hebt die Stelle hervor
- **Schlüssel (Auswahl):**
  - Größe & Position: `PageScale`, `PageWide`, `Scale`, `PosX`, `PosY`, `MonitorName`, `IslandFixedCenter`
  - Seiten: `Pages`, `WidgetPages`, `DashboardTiles`, `PageFont`, `PageStyle`, `PageLooks`, `NavIconsColor`
  - Aussehen: `AccentColor`, `IdlePill`, `Material`, `GlassTint`, `SpotifyColor`
  - Uhr: `ClockDate`, `ClockBattery`, `ClockSeconds`, `ClockStyle`, `ClockSize`
  - Ausblenden: `AutoHide`, `HideWithActivities`, `HideDelay`, `HideInFullscreen`
  - Animationen: `AnimModes`, `AnimColors`, `GlowStyle`, `GlowStyles`
  - Hinweise: `AlertSize`, `AlertMotion`, `AlertPriority`, `ShowVolume`, `ShowClipboard`, `ShowCapsLock`, `ShowBluetooth`, `ShowPrivacy`, `MirrorNotifications`, `ReplyAutoSend`, `ShowDownloads`
  - Wetter: `WeatherEnabled` (kleine Ansicht), `WeatherCity`, `WeatherUseLocation`
  - Kalender: `CalendarUrls`, `CalendarEnabled`, `CalendarRemind`, `CalendarAtStart`, `CalendarSpeak`, `CalendarWithWeather`
  - Katzen: `CatsMode`, `CatsStayVisible`, `CatsCurl`, `CatsWalk`, `CatsMoodAuto`, `Cats`
  - KI: `AiEnabled`, `AiProvider`, `AiKeysProtected`, `AiModelFor`
  - Geräte: `LogitechEnabled`, `LogitechWarnAt`
  - Obsidian: `ObsidianEnabled`, `ObsidianVault`, `ObsidianTarget`, `ObsidianFolder`, `ObsidianNote`, `ObsidianTime`
  - Claude: `ClaudeEnabled`, `ClaudeLimitWarn`
  - Timer: `LastTimerSeconds`
- **Migrationen** beim Laden: neue Seiten einfügen, Wetter/Kalender trennen (v8), alte Schalter übernehmen

---

## 11. Datenschutz und Sicherheit

- Keine eigenen Server, keine Telemetrie
- Lokale API nur auf `localhost`
- **Verschlüsselt (DPAPI):** KI-Schlüssel, To-Do-Token, Kalender-Adressen
- **Unverschlüsselt** in `einstellungen.json`: Spotify- und Discord-Tokens, Discord-Client-Secret (offener Punkt)
- Zwischenablage-Verlauf nur im Arbeitsspeicher, passwortähnliche Einträge verdeckt
- Wetter: nur Ortsname bzw. ungefähre Koordinaten an Open-Meteo
- Demo-Modus (`/demo`) für Aufnahmen ohne private Medien-Titel

---

## 12. Robustheit

- **Vollbild-Erkennung:** maximierte Fenster ohne Titelleiste (Electron: Claude, Discord, VS Code) gelten nicht als Vollbild
- **Einstellungen:** sicher gespeichert (Hilfsdatei → Austausch, vorige Fassung `.bak`); kaputte Datei → `.bak` laden, kaputte als `.bad`
- **Windows-Mitteilungen:** Lesefehler übersprungen; nach Fehlern Neuverbindung mit wachsendem Abstand (30 s … 10 min)
- **Discord:** normales Trennen nicht im Fehlerprotokoll, erneuter Versuch; nur echter CLOSE-Rahmen = „abgelehnt“
- **Claude:** Esc zählt nur als neueste Nutzer-Zeile; Prozessregel nur, wenn während der Aufgabe ein `claude`-Prozess lief; Antippen von „fertig“ pinnt 30 s
- **Kratzbaum:** feste Ebene über den Seiten, wird beim Wischen nicht neu gebaut

---

## 13. Bauen und Veröffentlichen

- **Entwicklung:** `Starten.bat` → `dotnet build -c Release` und Start
- **Einzelne EXE (mit .NET):** `Als-EXE-bauen.bat` → `dotnet publish -r win-x64 --self-contained false -p:PublishSingleFile=true`
- **Eigenständige EXE (Releases):**
  - `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none`
  - ca. 78 MB, läuft ohne .NET
- **Autostart:** Einstellungen → System → Mit Windows starten (`HKCU\…\Run`)
- **Releases:**
  - v1.0.0 – erste EXE
  - v1.1.0 – Logitech, Obsidian, KI-Anbieter, Claude-Limits, Anrufe
  - v1.2.0 – Claude-Zeiten/-Limits korrigiert, Logitech-Ruhezustand
  - v1.3.0 – Wetter/Kalender getrennt, farbige Wetter-Symbole, Schrift und Stil der Seiten, neue Animationen

---

## 14. Projektstruktur

```
DynamicIsland/
├─ Program.cs              Einstieg, Einzelinstanz, Fehlerprotokoll
├─ IslandWindow.cs         Fenster, Layout, Gesten, Takt, Menüs, Ereignisse, API-Routen
├─ SetupWindow.cs          Einstellungsfenster
├─ Ui.cs                   Design-Bausteine, Farben, Federanimation, TextBoost
├─ Activities/
│  ├─ Activity.cs          Basisklasse + GenericActivity
│  ├─ Activities.cs        Medien-Player, Timer, Stoppuhr, Sprachmemo, Uhr, Übersicht
│  ├─ AlertLook.cs         Hinweis-Designs
│  ├─ SpotifyPlayer.cs     Spotify-Player, Geräte-/Playlist-Liste
│  ├─ PlayerUi.cs          gemeinsames Player-Layout
│  ├─ Spotlight.cs         „App im Vordergrund“
│  ├─ TimerPicker.cs       Timer-Auswahl (Drehräder)
│  ├─ WeatherActivity.cs   Seite „Wetter“ + Widget
│  ├─ WeatherIcon.cs       farbige Wetter-Symbole (Vektor)
│  ├─ CalendarActivity.cs  Seite „Kalender“
│  ├─ TodoActivity.cs, AiActivity.cs, Panels.cs, Widgets.cs
│  ├─ DiscordVoice.cs, ReplyActivity.cs
│  ├─ AirPodsActivity.cs, HeadphonesActivity.cs, BluetoothActivity.cs
│  ├─ Cats.cs              Katzen, Kratzbaum
│  ├─ Logos.cs             gezeichnete Claude-/Discord-Logos
│  └─ Demos.cs             Vorführ-Aktivitäten
├─ Services/
│  ├─ MediaService.cs, Spotify.cs, AudioService.cs
│  ├─ Discord.cs, DiscordUi.cs, Notifications.cs, CallUi.cs
│  ├─ ClaudeTracker.cs, LocalApi.cs
│  ├─ Weather.cs, CalendarService.cs, TodoService.cs, Gemini.cs, Obsidian.cs
│  ├─ AirPods.cs, AirPodsAap.cs, Logitech.cs, SystemInfo.cs
│  ├─ Downloads.cs, GameDownloads.cs
│  ├─ ScreenOcr.cs, SystemTools.cs, Native.cs, LiquidGlass.cs
│  └─ Settings.cs
└─ docs/                   README-Grafiken
                           PNG: banner, player, claude, animationen, katzen
                           GIF: seiten, wetter, claude-fertig, licht-effekte, lichtring, kopieren, claude-limits, logitech
```

---

## 15. Bekannte Einschränkungen und offene Punkte

- Windows-Mitteilungen brauchen die Freigabe von Windows; ohne sie kein App-Logo und keine Anruf-Knöpfe
- Eine Island pro Bildschirm nicht umgesetzt (eine Island, frei verschiebbar)
- Spotify-/Discord-Tokens sollten mit DPAPI verschlüsselt werden
- WhatsApp-Antworten öffnen den Chat über die Suche – bei ähnlichen Namen evtl. falscher Treffer (Direkt-Senden abschaltbar)
- Hotkeys: beim Neu-Registrieren werden nur IDs 1–6 abgemeldet, registriert werden 1–9
- Epic-Downloads ohne Prozent; Steam-Fortschritt teils sprunghaft
- WPF zeichnet Emojis einfarbig – Wetter nutzt deshalb eigene Vektor-Symbole
- Discord „Stream beenden“ hängt von der Beschriftung im Discord-Fenster ab (deutsch/englisch)
- Antworten-Knopf per App-Namensliste (Windows verrät nicht, ob eine Mitteilung eine Antwort-Funktion hat)
- Logitech-Ruhezustand nur im Speicher: nach Neustart der Island bis zur ersten Antwort „kein Gerät“
