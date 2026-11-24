<p align="center">
  <img src="docs/banner.png" alt="Die Geile Insel – Dynamic Island für Windows" width="100%">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0A84FF?style=for-the-badge&logo=windows&logoColor=white">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img src="https://img.shields.io/badge/WPF-C%23-239120?style=for-the-badge&logo=csharp&logoColor=white">
  <img src="https://img.shields.io/github/v/release/SNIWI/DieGeileInsel?style=for-the-badge&color=30D158&label=Version">
</p>

<h3 align="center">Dynamic Island für den Windows-Desktop</h3>
<p align="center">Live-Aktivitäten, Hinweise und Schnellzugriffe in einer schwarzen Pille oben am Bildschirm –<br>mit Federanimationen, wischbaren Seiten und ohne Installation.</p>

<p align="center">
  <a href="https://github.com/SNIWI/DieGeileInsel/releases/latest/download/DynamicIsland.exe"><img src="https://img.shields.io/badge/Download-DynamicIsland.exe-0A84FF?style=for-the-badge&logo=windows&logoColor=white" alt="Download"></a>
  &nbsp;
  <a href="https://github.com/SNIWI/DieGeileInsel/releases"><img src="https://img.shields.io/badge/Alle_Versionen-Releases-2C2C2E?style=for-the-badge&logo=github&logoColor=white" alt="Releases"></a>
</p>

<p align="center">
  <a href="#vorschau">Vorschau</a> ·
  <a href="#neu-in-14">Neu in 1.4</a> ·
  <a href="#funktionen">Funktionen</a> ·
  <a href="#so-funktioniert-es">So funktioniert es</a> ·
  <a href="#installation">Installation</a> ·
  <a href="#bedienung">Bedienung</a> ·
  <a href="#häufige-fragen">FAQ</a>
</p>

<br>

<p align="center"><img src="docs/uebersicht.gif" alt="Übersicht" width="100%"></p>

## Auf einen Blick

<table>
<tr>
<td width="33%" valign="top">
<img src="docs/icons/live.svg" width="44" alt=""><br>
<b>Live-Aktivitäten</b><br>
<sub>Musik, Timer, Downloads, Anrufe, Teams, Discord und Claude Code direkt in der Pille</sub>
</td>
<td width="33%" valign="top">
<img src="docs/icons/seiten.svg" width="44" alt=""><br>
<b>Große Island mit Seiten</b><br>
<sub>Übersicht, Wetter, Kalender, Player, Bluetooth, KI und To Do – seitlich wischen</sub>
</td>
<td width="33%" valign="top">
<img src="docs/icons/claude.svg" width="44" alt=""><br>
<b>Für Claude Code gemacht</b><br>
<sub>Laufzeit im Kreis, aktuelles Werkzeug, Meldung bei „fertig“, Nutzungs-Limits</sub>
</td>
</tr>
<tr>
<td width="33%" valign="top">
<img src="docs/icons/medien.svg" width="44" alt=""><br>
<b>Medien und Geräte</b><br>
<sub>Spotify und alle Windows-Player, AirPods-Akku, Logitech-Maus und -Tastatur</sub>
</td>
<td width="33%" valign="top">
<img src="docs/icons/lokal.svg" width="44" alt=""><br>
<b>Komplett lokal</b><br>
<sub>Keine Server, keine Telemetrie, Zugangsdaten mit Windows verschlüsselt</sub>
</td>
<td width="33%" valign="top">
<img src="docs/icons/exe.svg" width="44" alt=""><br>
<b>Eine einzige EXE</b><br>
<sub>Herunterladen, starten, fertig – ohne Installation und ohne .NET</sub>
</td>
</tr>
</table>

## Vorschau

### Große Island

<table>
<tr>
<td width="50%" align="center"><img src="docs/seiten.gif" alt="Seiten wischen" width="100%"><br><sub><b>Seiten</b> · Übersicht, Wetter, Kopfhörer, KI – einheitliches Design</sub></td>
<td width="50%" align="center"><img src="docs/wetter.gif" alt="Wetter" width="100%"><br><sub><b>Wetter</b> · farbige Symbole, Stunden mit Tagesname</sub></td>
</tr>
<tr>
<td width="50%" align="center"><img src="docs/teams.gif" alt="Teams-Besprechung" width="100%"><br><sub><b>Teams</b> · Mikro, Kamera, Hand, Reaktionen, Verlassen</sub></td>
<td width="50%" align="center"><img src="docs/logitech.gif" alt="Maus und Tastatur" width="100%"><br><sub><b>Logitech</b> · Akku von Maus und Tastatur</sub></td>
</tr>
</table>

### Hinweise und Live-Aktivitäten

<p align="center"><img src="docs/ereignisse.gif" alt="Ereignisse" width="100%"><br><sub><b>Ereignisse</b> · Nachricht, Claude fertig, Gerät verbunden – jeweils mit App-Logo</sub></p>

<table>
<tr>
<td width="50%" align="center"><img src="docs/claude-fertig.gif" alt="Claude ist fertig" width="100%"><br><sub><b>Claude Code</b> · Laufzeit im Kreis, Meldung bei „fertig“</sub></td>
<td width="50%" align="center"><img src="docs/claude-limits.gif" alt="Claude-Limits" width="100%"><br><sub><b>Claude-Limits</b> · 5-Stunden- und Wochen-Limit, Warnung ab 80 %</sub></td>
</tr>
<tr>
<td width="50%" align="center"><img src="docs/kopieren.gif" alt="Kopieren" width="100%"><br><sub><b>Zwischenablage</b> · zeigt kurz den kopierten Inhalt</sub></td>
<td width="50%" align="center"><img src="docs/animationen.png" alt="Hinweis-Stile" width="100%"><br><sub><b>Hinweise</b> · Farbe und Logo passend zur App</sub></td>
</tr>
</table>

### Licht-Effekte

<table>
<tr>
<td width="50%" align="center"><img src="docs/licht-effekte.gif" alt="Licht-Effekte" width="100%"><br><sub><b>Licht-Effekte</b> · Umlauf, Schein, Glanz, Rahmen</sub></td>
<td width="50%" align="center"><img src="docs/lichtring.gif" alt="Lichtring" width="100%"><br><sub><b>Lichtring</b> · läuft einmal um die ganze Island</sub></td>
</tr>
</table>

### Katzen

<p align="center"><img src="docs/katzen.png" alt="Katzen" width="85%"><br><sub><b>Bis zu vier eigene Katzen</b> · Fell, Augen, Muster, Zubehör – auf dem Kratzbaum in der großen Island</sub></p>

## Neu in 1.4

<table>
<tr>
<td width="33%" valign="top">
<b>Microsoft Teams</b>
<ul>
<li>Seite im Teams-Look, nur in einer Besprechung und nach Kopplung</li>
<li>Mikro, Kamera, Hand, Chat, Unschärfe, Reaktionen, Verlassen</li>
<li>Aufnahme- und Teilen-Anzeige</li>
<li><kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>M</kbd> schaltet das Teams-Mikro</li>
</ul>
</td>
<td width="33%" valign="top">
<b>Neues Design</b>
<ul>
<li>Übersicht im Apple-Stil: Datum über großer Uhr, Wetter oder Player, runde Knöpfe</li>
<li>alle Seiten mit einheitlicher Kopfzeile und Karten</li>
<li>Discord-Seite im Discord-Look mit grünem Sprechrahmen</li>
</ul>
</td>
<td width="33%" valign="top">
<b>Vorschau</b>
<ul>
<li>neue, größere Animationen</li>
<li>Übersicht, Teams und Ereignisse neu dabei</li>
</ul>
</td>
</tr>
</table>

<details>
<summary><b>Neu in 1.3</b></summary>

- **Wetter und Kalender als eigene Seiten**
  - einzeln an- und abschaltbar
  - Wetter optional zusätzlich auf der Kalender-Seite
- **Farbige Wetter-Symbole**
  - in der kleinen Uhr-Ansicht, auf der Wetter-Seite und im Widget
  - Stunden mit Tagesname („So 19“)
- **Schrift und Stil der Seiten einstellbar**
  - Schriftgröße: Normal, Groß, Größer, Sehr groß – die Island bleibt gleich groß
  - Stil: Aufgeräumt (weniger, größer) oder Ausführlich
  - global oder pro Seite (Knopf „Aa“ unter Seiten & Widgets)
- **Feinschliff**
  - größere Seitensymbole in der großen Island
  - Datum mit vollem Wochentag
  - Antworten-Knopf nur noch bei Chat-Apps
  - Kalender zeigt Ladefehler statt „Wird geladen …“
  - keine „-0°“-Anzeige mehr
</details>

<details>
<summary><b>Neu in 1.2</b></summary>

- Claude-Laufzeit zählt ab der eigenen Nachricht, auch nach Neustart der Island
- Claude-Limits nur noch mit echten Werten aus Claude Code
- Logitech: schlafende Maus behält den letzten Akkustand
</details>

<details>
<summary><b>Neu in 1.1</b></summary>

- Obsidian: Text, Links und Dateien auf die Island ziehen, landen in der Inbox
- Mehrere KI-Anbieter: Gemini, ChatGPT, Grok, Claude, Mistral, DeepSeek, OpenRouter
- Anrufe annehmen (WhatsApp, Teams, Discord), App-Logo bei jeder Mitteilung
- Claude per Spracheingabe, Katzen-Laune nach Tageszeit und Wetter, Suche in den Einstellungen
</details>

## Funktionen

| Bereich | Funktion |
|---|---|
| **Medien** | Player für Spotify und alle Windows-Medien, Cover mit Farbleuchten, Player-Wechsler |
| **Wetter** | Open-Meteo ohne Konto, jetzt, nächste Stunden, nächste Tage |
| **Kalender** | iCal-Adressen (Google, Outlook, iCloud), Erinnerung, Ansage, „Beitreten“ für Meetings |
| **Claude Code** | Laufzeit, aktuelles Werkzeug, Fertig-Meldung, Nutzungs-Limits, Spracheingabe |
| **Nachrichten** | Windows-Mitteilungen mit App-Logo, Antworten aus der Island (Chat-Apps) |
| **Anrufe** | WhatsApp, Teams, Discord annehmen oder ablehnen |
| **Discord** | Sprachkanal mit Teilnehmern, Stummschalten, Stream beenden |
| **Teams** | Besprechung steuern: Mikro, Kamera, Hand, Chat, Reaktionen, Verlassen |
| **AirPods & Kopfhörer** | Akku, Ohrerkennung, Wiedergabegerät |
| **Logitech** | Akku von Maus und Tastatur, Warnung nur bei wenig Akku |
| **Downloads** | Browser, Steam und Epic mit Fortschritt und Restzeit |
| **Zwischenablage** | Verlauf der letzten 25 Einträge, nur im Arbeitsspeicher |
| **Timer** | Countdown und Pomodoro in der Pille |
| **KI** | Chat mit mehreren Anbietern, Texterkennung vom Bildschirm |
| **Obsidian** | Inhalte per Drag & Drop in die Inbox |
| **Katzen** | Bis zu vier eigene Katzen mit Kratzbaum in der großen Island |

## So funktioniert es

```mermaid
flowchart LR
    subgraph Quellen
        W[Windows-Medien<br>und Mitteilungen]
        C[Claude Code<br>Hooks]
        T[Teams<br>lokale API]
        D[Discord<br>RPC]
        B[Bluetooth<br>und Logitech]
        O[Open-Meteo<br>und iCal]
    end
    W --> I
    C -- "localhost:7843" --> I
    T -- "127.0.0.1:8124" --> I
    D -- "Named Pipe" --> I
    B --> I
    O --> I
    I((Die Geile Insel)) --> P[Pille<br>Live-Aktivität]
    I --> H[Hinweis<br>mit Licht-Effekt]
    I --> G[Große Island<br>mit Seiten]
```

- Jede Quelle meldet sich als Live-Aktivität, die wichtigste steht in der Pille
- Ereignisse erscheinen kurz als Hinweis und verschwinden von selbst
- Alles läuft auf dem eigenen Rechner, nur Wetter und Kalender werden aus dem Netz geladen

## Anpassen

| | |
|---|---|
| **Größe** | große Island, Breite, alles skalieren, Position |
| **Seiten** | Reihenfolge, an/aus, eigene Widget-Seiten |
| **Schrift & Stil** | global oder pro Seite |
| **Aussehen** | Form, Design, Akzentfarbe, farbige oder weiße Seitensymbole |
| **Hinweise** | welche Ereignisse, Dauer, Reihenfolge |
| **Animationen** | pro Ereignis App, Leuchten, Hüpfen, Licht-Effekt oder aus |
| **Uhr** | Format, Datum, Sekunden, Wetter in der kleinen Ansicht |

## Installation

1. [`DynamicIsland.exe`](https://github.com/SNIWI/DieGeileInsel/releases/latest/download/DynamicIsland.exe) herunterladen
2. Starten – die Island erscheint oben in der Mitte
3. Rechtsklick → Einstellungen → „Mit Windows starten“ für Autostart

> [!NOTE]
> Beim ersten Start kann Windows SmartScreen warnen: „Weitere Informationen“ → „Trotzdem ausführen“.

| Voraussetzung | |
|---|---|
| **System** | Windows 10 (ab 19041) oder Windows 11, x64 |
| **Laufzeit** | keine – .NET ist in der EXE enthalten |
| **Optional** | Spotify-Konto, Discord, Microsoft Teams (neu), Claude Code, Microsoft To Do |

<details>
<summary><b>Selbst bauen</b></summary>

- Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download)
- `Starten.bat` – bauen und starten
- `Als-EXE-bauen.bat` – einzelne `DynamicIsland.exe` im Ordner `Fertige App`
</details>

## Bedienung

| Geste | Aktion |
|---|---|
| Antippen | Island entfalten, erneut: Übersicht |
| Gedrückt halten | direkt zur Übersicht |
| Seitlich wischen / Mausrad | Seite wechseln |
| Seitensymbol antippen | direkt zur Seite |
| Rechte Maustaste ziehen | Island verschieben |
| Rechtsklick | Menü mit allen Funktionen |

### Tastenkürzel

| Kürzel | Aktion |
|---|---|
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>Leertaste</kbd> | Übersicht |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>K</kbd> | Kurzbefehle |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>D</kbd> | Downloads |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>M</kbd> | Mikro an/aus (Teams in einer Besprechung, sonst Discord) |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>V</kbd> | Zwischenablage-Verlauf |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>A</kbd> | App-Wechsler |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>G</kbd> | KI-Chat |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>T</kbd> | Text vom Bildschirm erkennen |
| <kbd>Strg</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> | neue Aufgabe (To Do) |

## Häufige Fragen

<details>
<summary><b>Wie verbinde ich Claude Code?</b></summary>

- Einstellungen → Claude → „Mit Claude Code verbinden“
- Die Island trägt die Hooks in `%USERPROFILE%\.claude\settings.json` ein
- Ab der nächsten Nachricht an Claude erscheint der Kreis mit der Laufzeit
</details>

<details>
<summary><b>Wie verbinde ich Microsoft Teams?</b></summary>

- In Teams: Einstellungen → Datenschutz → „Drittanbieter-App-API“ aktivieren
- In der Island: Einstellungen → Teams → „Mit Teams verbinden“
- In der nächsten Besprechung „Mit Teams koppeln“ antippen und in Teams „Zulassen“ wählen
- Die Teams-Seite erscheint nur während einer Besprechung
</details>

<details>
<summary><b>Warum sehe ich keine Claude-Limits?</b></summary>

- Die Limits sendet nur Claude Code im Terminal (Pro/Max), die Claude-App nicht
- Angezeigt werden nur echte Werte, nie Schätzungen
</details>

<details>
<summary><b>Wo liegen meine Daten?</b></summary>

- Einstellungen in `%APPDATA%\DynamicIsland`
- KI-Schlüssel, To-Do-Token und Kalender-Adressen sind mit Windows (DPAPI) verschlüsselt
- Der Zwischenablage-Verlauf wird nie gespeichert
</details>

<details>
<summary><b>Wie bekomme ich die Island weg oder an eine andere Stelle?</b></summary>

- Mit der rechten Maustaste ziehen verschiebt sie
- Rechtsklick → Beenden schließt sie
- Unter Einstellungen → Allgemein → „Größe & Position“ lässt sie sich skalieren
</details>

## Datenschutz

- Keine eigenen Server, keine Telemetrie
- Lokale API nur auf `localhost`
- Wetter: nur der Ort wird an Open-Meteo übertragen
- Zwischenablage-Verlauf nur im Arbeitsspeicher, passwortähnliche Einträge verdeckt

## Technik

- WPF / .NET 8, Code-only UI ohne XAML
- Lokale API auf `localhost:7843` (Hinweise, Claude-Hooks, Seiten)
- Vollständige Beschreibung: [SPEZIFIKATION.md](SPEZIFIKATION.md)

<br>

<p align="center"><sub>Die Geile Insel · gebaut für Windows · <a href="https://github.com/SNIWI/DieGeileInsel/releases">Releases</a> · <a href="SPEZIFIKATION.md">Spezifikation</a></sub></p>
