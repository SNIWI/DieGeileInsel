<p align="center">
  <img src="docs/banner.png" alt="Die Geile Insel – Dynamic Island für Windows" width="100%">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0A84FF?style=for-the-badge&logo=windows&logoColor=white">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img src="https://img.shields.io/badge/WPF-C%23-239120?style=for-the-badge&logo=csharp&logoColor=white">
  <img src="https://img.shields.io/github/v/release/SNIWI/DieGeileInsel?style=for-the-badge&color=30D158">
</p>

<p align="center"><b>Dynamic Island für den Windows-Desktop</b> – Live-Aktivitäten, Hinweise und Schnellzugriffe in einer Pille oben am Bildschirm.</p>

---

## Überblick

- Schwarze Pille oben am Bildschirm, wächst und schrumpft mit Federanimationen
- Live-Aktivitäten: Musik, Timer, Downloads, Anrufe, Claude Code
- Große Island mit wischbaren Seiten: Übersicht, Wetter, Kalender, Player, Bluetooth, KI, To Do
- Komplett lokal, keine Konten nötig (außer für optionale Dienste wie Spotify oder To Do)
- Einzelne EXE, ohne Installation und ohne .NET

## Vorschau

<table>
<tr>
<td width="50%" align="center"><img src="docs/seiten.gif" alt="Seiten wischen" width="100%"><br><sub><b>Seiten</b> · Übersicht, Kalender, Wetter, Kopfhörer, KI</sub></td>
<td width="50%" align="center"><img src="docs/wetter.gif" alt="Wetter" width="100%"><br><sub><b>Wetter</b> · farbige Symbole, Stunden mit Tagesname</sub></td>
</tr>
<tr>
<td width="50%" align="center"><img src="docs/claude-fertig.gif" alt="Claude ist fertig" width="100%"><br><sub><b>Claude Code</b> · Laufzeit im Kreis, Meldung bei „fertig“</sub></td>
<td width="50%" align="center"><img src="docs/licht-effekte.gif" alt="Licht-Effekte" width="100%"><br><sub><b>Licht-Effekte</b> · Umlauf, Schein, Glanz, Rahmen</sub></td>
</tr>
<tr>
<td width="50%" align="center"><img src="docs/lichtring.gif" alt="Lichtring" width="100%"><br><sub><b>Lichtring</b> · läuft einmal um die ganze Island</sub></td>
<td width="50%" align="center"><img src="docs/kopieren.gif" alt="Kopieren" width="100%"><br><sub><b>Zwischenablage</b> · zeigt kurz den kopierten Inhalt</sub></td>
</tr>
<tr>
<td width="50%" align="center"><img src="docs/claude-limits.gif" alt="Claude-Limits" width="100%"><br><sub><b>Claude-Limits</b> · 5-Stunden- und Wochen-Limit, Warnung ab 80 %</sub></td>
<td width="50%" align="center"><img src="docs/logitech.gif" alt="Maus und Tastatur" width="100%"><br><sub><b>Logitech</b> · Akku von Maus und Tastatur</sub></td>
</tr>
</table>

## Neu in 1.3

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
| **AirPods & Kopfhörer** | Akku, Ohrerkennung, Wiedergabegerät |
| **Logitech** | Akku von Maus und Tastatur, Warnung nur bei wenig Akku |
| **Downloads** | Browser, Steam und Epic mit Fortschritt und Restzeit |
| **Zwischenablage** | Verlauf der letzten 25 Einträge, nur im Arbeitsspeicher |
| **Timer** | Countdown und Pomodoro in der Pille |
| **KI** | Chat mit mehreren Anbietern, Texterkennung vom Bildschirm |
| **Obsidian** | Inhalte per Drag & Drop in die Inbox |
| **Katzen** | Bis zu vier eigene Katzen mit Kratzbaum in der großen Island |

## Anpassen

- **Größe:** große Island, Breite, alles skalieren, Position
- **Seiten:** Reihenfolge, an/aus, eigene Widget-Seiten
- **Schrift & Stil:** global oder pro Seite
- **Aussehen:** Form, Design, Akzentfarbe, farbige oder weiße Seitensymbole
- **Hinweise:** welche Ereignisse, Dauer, Reihenfolge
- **Animationen:** pro Ereignis App, Leuchten, Hüpfen, Licht-Effekt oder aus
- **Uhr:** Format, Datum, Sekunden, Wetter in der kleinen Ansicht

## Installation

1. Unter [Releases](../../releases) die `DynamicIsland.exe` herunterladen
2. Starten – die Island erscheint oben in der Mitte
3. Rechtsklick → Einstellungen → „Mit Windows starten“ für Autostart

Hinweis: Beim ersten Start kann Windows SmartScreen warnen („Weitere Informationen“ → „Trotzdem ausführen“).

### Selbst bauen

- Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download)
- `Starten.bat` – bauen und starten
- `Als-EXE-bauen.bat` – einzelne `DynamicIsland.exe` im Ordner `Fertige App`

## Bedienung

| Geste | Aktion |
|---|---|
| Antippen | Island entfalten, erneut: Übersicht |
| Gedrückt halten | direkt zur Übersicht |
| Seitlich wischen / Mausrad | Seite wechseln |
| Seitensymbol antippen | direkt zur Seite |
| Rechte Maustaste ziehen | Island verschieben |
| Rechtsklick | Menü mit allen Funktionen |

## Datenschutz

- Alles läuft lokal
- Einstellungen in `%APPDATA%\DynamicIsland`, Zugangsdaten mit Windows verschlüsselt
- Claude-Anzeige nur über `localhost`
- Wetter: nur der Ort wird an Open-Meteo übertragen
- Zwischenablage-Verlauf wird nie gespeichert

## Technik

- WPF / .NET 8, Code-only UI ohne XAML
- Lokale API auf `localhost:7843` (Hinweise, Claude-Hooks, Seiten)
- Vollständige Beschreibung: [SPEZIFIKATION.md](SPEZIFIKATION.md)
