using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;

namespace TouchSpeak;

/// <summary>Pause-Sperre, „Wort löschen", Absatz-/Satz-Vorlesen mit Weiterspringen und die
/// Test-Fenstergröße. Ausgelagert, damit MainWindow.xaml.cs überschaubar bleibt.</summary>
public partial class MainWindow
{
    // ---------------- Fenstergröße zum Testen ----------------

    /// <summary>Start mit <c>--fenster 1280x853</c> öffnet statt des Vollbilds ein Fenster dieser
    /// logischen Größe. So lässt sich am PC prüfen, wie die Oberfläche auf einem Tablet aussieht
    /// (Dell Latitude 7200 2-in-1: 1920×1280 Pixel bei 150 % Skalierung = 1280×853 logisch).</summary>
    private void ApplyWindowSizeOverride()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            string? value = null;
            if (args[i].StartsWith("--fenster=", StringComparison.OrdinalIgnoreCase))
                value = args[i]["--fenster=".Length..];
            else if (args[i].Equals("--fenster", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                value = args[++i];
            if (value == null) continue;

            var m = Regex.Match(value, @"^(\d{3,5})x(\d{3,5})$", RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            WindowState = WindowState.Normal;
            Width = double.Parse(m.Groups[1].Value);
            Height = double.Parse(m.Groups[2].Value);
            return;
        }
    }

    // ---------------- Pause (Bediensperre) ----------------

    private bool _locked;

    /// <summary>Schaltet die Bediensperre um: Ein Overlay deckt alle Seiten ab und schluckt jede
    /// Eingabe, nur die Pause-Taste selbst bleibt bedienbar und leuchtet rot. Ein weiterer Klick
    /// darauf hebt die Sperre wieder auf. Zusätzlich wird der Seiteninhalt deaktiviert, damit auch
    /// eine physische Tastatur nichts in den Text schreiben kann.</summary>
    private void Pause_Click(object sender, RoutedEventArgs e) => SetLocked(!_locked);

    private void SetLocked(bool locked)
    {
        _locked = locked;
        LockOverlay.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
        Tabs.IsEnabled = !locked;
        PauseButton.Tag = locked ? "on" : null;
        PauseButton.Content = locked ? "\U0001F4A4  Pause aktiv" : "\U0001F4A4  Pause";
        if (!locked) Editor.Focus();
    }

    // ---------------- Wort löschen ----------------

    /// <summary>Löscht das Wort vor der Einfügemarke (samt dem Leerzeichen dahinter), bleibt aber
    /// innerhalb des aktuellen Absatzes – wie Strg+Rücktaste.</summary>
    private void OnDeleteWord()
    {
        Editor.Focus();
        EditingCommands.DeletePreviousWord.Execute(null, Editor);
        RefreshSuggestions();
    }

    // ---------------- Absatz / Satz sprechen mit Weiterspringen ----------------

    /// <summary>Spricht den Absatz an der Einfügemarke und setzt sie danach an den Anfang des
    /// nächsten Absatzes, sodass wiederholtes Drücken den Text Absatz für Absatz vorliest.
    /// Ein Absatz reicht von Leerzeile zu Leerzeile. Enthält der Text gar keine Leerzeilen,
    /// gilt jede mit „Zeile" begonnene Zeile als eigener Absatz.</summary>
    private void SpeakParagraph_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        var (text, map) = BuildTextMap();
        if (text.Length == 0) return;

        var lines = LineRanges(text);
        bool blankLineMode = lines.Any(l => IsBlankLine(text, l));

        int off = Math.Clamp(CaretOffset(Editor.CaretPosition, map), 0, text.Length);
        int cur = lines.FindIndex(l => off >= l.Start && off <= l.End);
        if (cur < 0) cur = lines.Count - 1;

        int first = cur, last = cur;
        if (blankLineMode)
        {
            if (IsBlankLine(text, lines[cur]))
            {
                first = -1; // Marke steht in einer Leerzeile: nichts sprechen, nur weiterspringen
            }
            else
            {
                while (first > 0 && !IsBlankLine(text, lines[first - 1])) first--;
                while (last < lines.Count - 1 && !IsBlankLine(text, lines[last + 1])) last++;
            }
        }

        string spoken = first < 0
            ? string.Empty
            : text[lines[first].Start..lines[last].End].Replace('\n', ' ').Trim();

        // Zum Anfang des nächsten Absatzes springen (Leerzeilen überspringen);
        // am Textende bleibt die Marke stehen.
        int next = last + 1;
        while (next < lines.Count && IsBlankLine(text, lines[next])) next++;
        if (next < lines.Count)
        {
            int s = lines[next].Start;
            while (s < lines[next].End && char.IsWhiteSpace(text[s])) s++;
            Editor.CaretPosition = map[Math.Min(s, map.Count - 1)];
        }

        SpeakText(spoken);
    }

    /// <summary>Spricht den Satz an der Einfügemarke und springt danach zum nächsten Satz.</summary>
    private void SpeakSentence_Click(object sender, RoutedEventArgs e)
    {
        var sentence = GetSentenceAtCaret();
        MoveSentence(1);
        SpeakText(sentence);
    }

    /// <summary>Zeile im Klartext von <see cref="BuildTextMap"/>: Start inklusiv, End exklusiv
    /// (Position des Zeilenumbruchs bzw. das Textende).</summary>
    private readonly record struct LineRange(int Start, int End);

    private static List<LineRange> LineRanges(string text)
    {
        var list = new List<LineRange>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            list.Add(new LineRange(start, i));
            start = i + 1;
        }
        // BuildTextMap schließt jeden Absatz mit '\n' ab – der Rest nach dem letzten Umbruch
        // ist dann leer und keine echte Zeile.
        if (start < text.Length) list.Add(new LineRange(start, text.Length));
        if (list.Count == 0) list.Add(new LineRange(0, 0));
        return list;
    }

    private static bool IsBlankLine(string text, LineRange l)
    {
        for (int i = l.Start; i < l.End; i++)
            if (!char.IsWhiteSpace(text[i])) return false;
        return true;
    }
}
