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
- **Sprachausgabe** mit modernen Windows-Stimmen (Deutsch + Englisch),
  einstellbarer Geschwindigkeit und Lautstärke.
- **Vorlesen-Seite als Befehlskacheln** (orientiert an gängiger AAC-Software):
  Der Text steht oben, darunter große Kacheln zum **Bearbeiten/Navigieren**
  (Kopieren, Einfügen, Rückgängig, Text auswählen, Cursor links/rechts/rauf/
  runter, Wort/Satz/Zeilen-/Textanfang & -ende) und zum **Sprechen**
  (Absatz, Satz, Wort, Alles, Auswahl, Beenden) – jeweils bezogen auf die
  aktuelle Cursorposition bzw. Auswahl.
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

## Bessere/natürlichere Stimmen installieren
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
