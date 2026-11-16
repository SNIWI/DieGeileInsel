<p align="center">
  <img src="docs/banner.png" alt="Die Geile Insel – Dynamic Island für Windows" width="100%">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0A84FF?style=for-the-badge&logo=windows&logoColor=white">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white">
  <img src="https://img.shields.io/badge/WPF-C%23-239120?style=for-the-badge&logo=csharp&logoColor=white">
</p>

<p align="center">
  Die schwarze Pille vom iPhone – oben auf deinem Bildschirm. Sie wächst und schrumpft mit Federanimationen,
  zeigt Live-Aktivitäten und lässt sich antippen, wischen und frei verschieben.
</p>

---

## ✨ Highlights

### 🎵 Player für Spotify und alle anderen Medien
Großes Cover mit Farb-Leuchten, Spulen, Zufall, Wiederholen. Das Cover schrumpft beim Pausieren, beim Songwechsel gleitet der Titel herein.
Mit dem **Player-Wechsler** springst du zwischen Spotify, YouTube im Browser, VLC & Co.

<img src="docs/player.png" alt="Player" width="100%">

### 📅 Heute: Wetter + Kalender auf einen Blick
Links das Wetter (Open-Meteo, ohne Konto), rechts deine nächsten Termine – mit „Beitreten“ für Online-Meetings.

<img src="docs/heute.png" alt="Heute: Wetter und Kalender" width="100%">

### ✳️ Claude Code live verfolgen
Ein kleiner Kreis neben der Island zeigt, dass Claude arbeitet und wie lange schon. Drüberfahren zeigt, was Claude gerade tut.
Ist Claude fertig, wird die Island länger und meldet es mit Logo.

<img src="docs/claude.png" alt="Claude Code in der Island" width="100%">

### 🎬 Animationen „App im Vordergrund“
Kopieren, Timer, Nachrichten, Downloads: Die Island streckt sich und zeigt groß das Symbol der App mit dem Inhalt daneben.
Pro Ereignis wählbar: **App · Leuchten · Hüpfen · Beides · Aus** – mit eingebautem Animations-Tester.

<img src="docs/animationen.png" alt="Animationen" width="100%">

### 🐱 Katzen!
Bis zu vier eigene Katzen – Fell, Augen, Muster, Zubehör, Größe und Charakter. Sie blinzeln, schnurren beim Streicheln
und sitzen in der großen Island auf ihrem Kratzbaum. Ganz oben wird geschlafen.

<img src="docs/katzen.png" alt="Katzen" width="100%">

---

## 🧩 Alles, was drin ist

| | |
|---|---|
| 🎧 **AirPods & Kopfhörer** | Akku, Ohrerkennung, automatisches Umschalten |
| 💬 **Nachrichten** | WhatsApp, Discord & Co. – direkt aus der Island antworten |
| 🎙️ **Discord** | Sprachkanal mit Teilnehmern, Stumm/Ton aus, Stream beenden |
| 🎮 **Spiele-Downloads** | Steam & Epic mit Fortschritt und Restzeit |
| ⬇️ **Downloads** | Browser-Downloads live, fertig → antippen öffnet |
| 📋 **Zwischenablage** | Verlauf der letzten 25 Einträge (nur im Arbeitsspeicher) |
| ⏱️ **Timer & Pomodoro** | Countdown in der Pille, Animation wenn fertig |
| 🤖 **KI** | Gemini-Chat und Texterkennung vom Bildschirm |
| 🖥️ **Mehrere Bildschirme** | Frei verschiebbar, auch auf den zweiten Monitor |

## 🚀 Installation

1. Unter **[Releases](../../releases)** die `DynamicIsland.exe` herunterladen *(oder selbst bauen, siehe unten)*.
2. Starten – die Island erscheint oben in der Mitte.
3. *Rechtsklick → Einstellungen → Mit Windows starten* für Autostart.

### Selbst bauen
Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download)
- **`Starten.bat`** – baut und startet die App
- **`Als-EXE-bauen.bat`** – erzeugt eine einzelne `DynamicIsland.exe` im Ordner `Fertige App`

## 🖱️ Bedienung

| Geste | Was passiert |
|---|---|
| **Antippen** | Island entfalten · nochmal: Übersicht |
| **Gedrückt halten** | Direkt zur Übersicht |
| **Seitlich wischen / Mausrad** | Seiten wechseln (Vorschau der Nachbarseite) |
| **Rechte Maustaste ziehen** | Island verschieben (rastet nur in der Mitte ein) |
| **Rechtsklick** | Menü mit allen Funktionen |

## 🔒 Datenschutz
Alles läuft lokal. Einstellungen liegen in `%APPDATA%\DynamicIsland`, Zugangsdaten sind mit Windows verschlüsselt.
Die Claude-Anzeige spricht nur mit `localhost`. Fürs Wetter wird nur der Ort an Open-Meteo übertragen.
