# TouchSpeak – Kommunikationshilfe (Windows, WPF / .NET 8)

Eine Windows-Desktop-App zum Schreiben von Texten über eine On-Screen-Tastatur,
mit Wortvorhersage, Dateiimport/-export und moderner Sprachausgabe, die Texte
satz- bzw. abschnittsweise vorliest und den aktuell gesprochenen Abschnitt
farbig hervorhebt.

## Funktionen
- **Touch-Tastatur**, umschaltbar zwischen **QWERTZ** und einem
  **häufigkeitsbasierten Layout** (häufigste deutsche Buchstaben zuerst).
- **Wortvorhersage** mit eingebautem deutschem Wörterbuch, das zusätzlich
  **dazulernt**, was du häufig tippst (gespeichert pro Benutzer).
- **Sprachausgabe** mit der natürlichen Offline-Stimme **Thorsten** (Piper) sowie
  den modernen Windows-Stimmen (Deutsch + Englisch), einstellbarer Geschwindigkeit
  und Lautstärke.
- **Vorlesen-Seite als Befehlskacheln** (orientiert an gängiger AAC-Software):
  Der Text steht oben, darunter große Kacheln zum **Bearbeiten/Navigieren**
  (Kopieren, Einfügen, Rückgängig, Text auswählen, Cursor links/rechts/rauf/
  runter, Wort/Satz/Zeilen-/Textanfang & -ende) und zum **Sprechen**
  (Absatz, Satz, Wort, Alles, Auswahl, Beenden) – jeweils bezogen auf die
  aktuelle Cursorposition bzw. Auswahl. **Absatz sprechen** und **Satz sprechen**
  setzen den Cursor danach an den Anfang des nächsten Absatzes bzw. Satzes, sodass
  wiederholtes Drücken den Text Stück für Stück vorliest. Ein Absatz reicht von
  Leerzeile zu Leerzeile; enthält der Text keine Leerzeilen, zählt jede Zeile als Absatz.
- **Löschen-Tasten** auf der Schreiben-Seite: **⌫ Löschen** (ein Zeichen),
  **⌫ Wort** (das Wort vor dem Cursor) und **Alles löschen** (mit 10-Sekunden-
  Sicherheitsabfrage, damit die Kopfsteuerung nichts versehentlich löscht).
- **Pause (Bediensperre):** Die Taste oben rechts sperrt alle anderen Tasten,
  damit z. B. beim Ausruhen nichts aus Versehen ausgelöst wird. Solange die Sperre
  aktiv ist, leuchtet die Taste rot; ein weiterer Klick darauf gibt alles wieder frei.
- **Einstellungen-Seite** für Stimmen, Sprache, Geschwindigkeit, Lautstärke
  und die Kopfsteuerung.
- **Import:** `.txt`, `.rtf`, `.docx`  •  **Export:** `.txt`, `.rtf`, `.docx`, `.pdf`
  (RTF behält die Formatierung vollständig; txt/docx/pdf sind textbasiert.)
- **Kopfsteuerung (Verweil-Klick):** Bedienung per Kopfmaus (z. B. Orin
  HeadMouse Nano). Verweilt der Cursor über einer Schaltfläche, füllt sich ein
  Ring und löst nach Ablauf der einstellbaren Verweilzeit den Klick aus – ganz
  ohne Taster oder Zusatzsoftware.

## Steuerung per Kopfmaus (Orin HeadMouse Nano)
Die HeadMouse Nano verhält sich gegenüber Windows wie eine normale USB-Maus und
bewegt nur den Cursor. Für das **Klicken** bietet TouchSpeak einen eingebauten
**Verweil-Klick**:

1. Reiter **Einstellungen** → Bereich **Kopfsteuerung (Verweil-Klick)**.
2. **Aktiviert** anhaken und mit **− / +** bzw. dem Schieberegler die
   **Verweilzeit** (0,4–3,0 s) einstellen.
3. Den Cursor per Kopf über eine Taste/Schaltfläche halten – der Ring füllt
   sich, dann wird geklickt. Erst wenn der Cursor das Element verlässt, kann
   erneut ausgelöst werden (kein versehentliches Mehrfachklicken).

Der Verweil-Klick wirkt auf alle Schaltflächen der App (Tastatur, Vorschläge,
Werkzeugleiste, Vorlese-Tasten, Auswahlfelder). Die Einstellung wird gespeichert.
Alternativ funktioniert weiterhin ein **externer Taster** am HeadMouse-Eingang
oder eine separate Dwell-Software.

## Voraussetzungen
- **Windows 10** (Build 19041 / Version 2004 oder neuer) oder **Windows 11**
- **.NET 8 SDK** – https://dotnet.microsoft.com/download
- Zum Bauen: **Visual Studio 2022** mit Workload „.NET-Desktopentwicklung",
  oder die Kommandozeile (`dotnet`).

## Bauen & Starten
**Mit Visual Studio:** `TouchSpeak.sln` öffnen → F5.

**Mit der Kommandozeile:**
```
cd TouchSpeak
dotnet build -c Release
dotnet run --project TouchSpeak
```

> Hinweis: Die App lässt sich nur **unter Windows** bauen und ausführen
> (WPF + Windows-Sprachausgabe). Linux/macOS werden nicht unterstützt.

**Tablet-Ansicht am PC prüfen:** `TouchSpeak.exe --fenster 1280x853` startet die
App statt im Vollbild in einem Fenster dieser logischen Größe (Dell Latitude 7200
2-in-1: 1920×1280 Pixel bei 150 % Skalierung ergeben 1280×853). Die Oberfläche
passt sich jeder Bildschirmhöhe an – Tasten und Kacheln teilen sich den Platz,
statt unten abgeschnitten zu werden.

## Natürliche Stimme „Thorsten" (Piper, offline)
TouchSpeak bringt die neuronale deutsche Stimme **Thorsten** mit. Sie läuft über das
freie Programm **Piper** komplett offline und klingt deutlich natürlicher als die
eingebauten Windows-Stimmen. Auswahl: **Einstellungen → Deutsche Stimme →
„Thorsten – natürliche Stimme (Piper medium)"**. Sie steht oben in der Liste und ist
bei einer Neuinstallation (noch keine gespeicherte Auswahl) vorausgewählt.

Die Dateien liegen in `TouchSpeak/Piper/` (`piper.exe`, Laufzeit-DLLs, `espeak-ng-data/`
für die Aussprache, `de_DE-thorsten-medium.onnx` + `.json`) und werden beim Bauen und
Veröffentlichen 1:1 mitkopiert (rund 90 MB). Weitere Piper-Stimmen (`*.onnx` +
`*.onnx.json` von huggingface.co/rhasspy/piper-voices) einfach in denselben Ordner legen;
sie erscheinen nach dem nächsten Start in der Liste.

Technik: Die App startet `piper.exe` pro Äußerung als Hilfsprogramm, übergibt den Text
und spielt die zurückgelieferte WAV über den vorhandenen Player ab. Geschwindigkeit und
Lautstärke wirken wie bei den Windows-Stimmen. Lizenzen: Piper MIT, Stimme Thorsten CC0,
espeak-ng (`espeak-ng.dll`) GPLv3 – als eigenständiges Programm eingebunden, nicht in
TouchSpeak hineingelinkt.

## Weitere Windows-Stimmen installieren
Die natürlich klingenden Stimmen werden bei Bedarf über Windows nachgeladen:
**Einstellungen → Zeit und Sprache → Sprache & Region** (bzw. **Sprache → Stimmen**)
→ gewünschte Sprache/Stimme hinzufügen. Neue Stimmen erscheinen nach einem
Neustart der App automatisch in den Auswahllisten.

## Wo werden meine Daten gespeichert?
Im Ordner `%AppData%\TouchSpeak`:
- `settings.json` – Stimme, Geschwindigkeit, Layout usw.
- `userdict.json` – die dazugelernten Wörter der Wortvorhersage.

## Wörterbuch anpassen
`TouchSpeak/Resources/de_words.txt` – eine Zeile pro Wort, Reihenfolge =
Häufigkeit (oben = häufiger). Die Datei kann beliebig erweitert oder durch eine
größere Wortliste ersetzt werden.

## Bekannte Einschränkungen (erste Version)
- DOCX-Import/-Export ist textbasiert (Absätze ja, komplexe Formatierung nein).
  Für volle Formatierung `.rtf` verwenden.
- Sollte der PDF-Export einen Schriftart-Fehler melden, im Projekt das NuGet-Paket
  `PDFsharp-MigraDoc-gdi` gegen `PDFsharp-MigraDoc-wpf` tauschen.
- Die Satzerkennung berücksichtigt gängige deutsche Abkürzungen; sehr ungewöhnliche
  Fälle können abweichen.
