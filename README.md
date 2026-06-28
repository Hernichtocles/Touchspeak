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
- **Vorlesen** Satz für Satz mit **Weiter/Zurück**; der aktuelle Satz wird
  hervorgehoben. **Umschaltbar** zwischen Satz- und Abschnittsmodus.
- **Import:** `.txt`, `.rtf`, `.docx`  •  **Export:** `.txt`, `.rtf`, `.docx`, `.pdf`
  (RTF behält die Formatierung vollständig; txt/docx/pdf sind textbasiert.)

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
