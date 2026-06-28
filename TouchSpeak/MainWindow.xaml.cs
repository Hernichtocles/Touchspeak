using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;
using TouchSpeak.Models;
using TouchSpeak.Services;

namespace TouchSpeak;

public partial class MainWindow : Window
{
    private readonly SpeechService _speech = new();
    private readonly WordPredictor _predictor = new();
    private AppSettings _settings = SettingsService.Load();
    private DwellClicker? _dwell;

    private bool _loaded;
    private bool _selectMode;   // when true, navigation tiles extend the selection

    public MainWindow()
    {
        InitializeComponent();

        // Verweil-Klick (Kopfsteuerung); wird über die Einstellungen aktiviert.
        _dwell = new DwellClicker(this, DwellOverlay);

        // Wire up the on-screen keyboard.
        Keyboard.CharInput += OnChar;
        Keyboard.SpacePressed += OnSpace;
        Keyboard.BackspacePressed += OnBackspace;
        Keyboard.EnterPressed += OnEnter;
        Keyboard.PhraseInput += OnPhrase;
        Keyboard.LayoutChanged += _ => UpdateLayoutButton();

        _speech.Completed += (_, _) => Dispatcher.Invoke(() => { /* manual navigation */ });

        PopulateVoices();
        ApplySettings();

        _loaded = true;
        RefreshSuggestions();
    }

    // ---------------- Settings / voices ----------------

    private void PopulateVoices()
    {
        var de = SpeechService.GetVoicesForLanguage("de").ToList();
        var en = SpeechService.GetVoicesForLanguage("en").ToList();

        GermanVoiceBox.DisplayMemberPath = "DisplayName";
        GermanVoiceBox.SelectedValuePath = "Id";
        GermanVoiceBox.ItemsSource = de;

        EnglishVoiceBox.DisplayMemberPath = "DisplayName";
        EnglishVoiceBox.SelectedValuePath = "Id";
        EnglishVoiceBox.ItemsSource = en;

        GermanVoiceBox.SelectedValue = _settings.GermanVoiceId;
        if (GermanVoiceBox.SelectedItem == null && de.Count > 0) GermanVoiceBox.SelectedIndex = 0;

        EnglishVoiceBox.SelectedValue = _settings.EnglishVoiceId;
        if (EnglishVoiceBox.SelectedItem == null && en.Count > 0) EnglishVoiceBox.SelectedIndex = 0;
    }

    private void ApplySettings()
    {
        RateSlider.Value = _settings.SpeechRate;
        VolumeSlider.Value = _settings.SpeechVolume;

        Keyboard.UseFrequencyLayout = _settings.UseFrequencyLayout;
        UpdateLayoutButton();

        if (_settings.SpeakLanguage == SpeakLanguage.English) LangEnglish.IsChecked = true;
        else LangGerman.IsChecked = true;

        DwellSlider.Value = Math.Clamp(_settings.DwellSeconds, DwellSlider.Minimum, DwellSlider.Maximum);
        DwellEnabledBox.IsChecked = _settings.DwellEnabled;
        if (_dwell != null)
        {
            _dwell.DwellSeconds = DwellSlider.Value;
            _dwell.Enabled = _settings.DwellEnabled;
        }
        UpdateDwellLabel();
    }

    private void GatherSettings()
    {
        _settings.SpeechRate = RateSlider.Value;
        _settings.SpeechVolume = VolumeSlider.Value;
        _settings.UseFrequencyLayout = Keyboard.UseFrequencyLayout;
        _settings.SpeakLanguage = LangEnglish.IsChecked == true ? SpeakLanguage.English : SpeakLanguage.German;
        _settings.GermanVoiceId = GermanVoiceBox.SelectedValue as string;
        _settings.EnglishVoiceId = EnglishVoiceBox.SelectedValue as string;
        _settings.DwellEnabled = DwellEnabledBox.IsChecked == true;
        _settings.DwellSeconds = DwellSlider.Value;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        GatherSettings();
        SettingsService.Save(_settings);
        _predictor.SaveUserDictionary();
        _speech.Dispose();
        _dwell?.Dispose();
        base.OnClosing(e);
    }

    // ---------------- Keyboard input ----------------

    private static bool IsWordChar(char c) => char.IsLetter(c) || c == '-' || c == '\'';

    private void OnChar(string s)
    {
        if (s.Length == 1 && !IsWordChar(s[0])) LearnCurrentWord();
        InsertText(s);
        RefreshSuggestions();
    }

    private void OnSpace()
    {
        LearnCurrentWord();
        InsertText(" ");
        RefreshSuggestions();
    }

    private void OnBackspace()
    {
        Editor.Focus();
        EditingCommands.Backspace.Execute(null, Editor);
        RefreshSuggestions();
    }

    private void OnEnter()
    {
        LearnCurrentWord();
        Editor.Focus();
        EditingCommands.EnterParagraphBreak.Execute(null, Editor);
        RefreshSuggestions();
    }

    private void OnPhrase(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return;
        InsertText(phrase + " ");
        RefreshSuggestions();
        SpeakText(phrase);
    }

    private void InsertText(string s)
    {
        Editor.Focus();
        var sel = Editor.Selection;
        sel.Text = s;
        Editor.CaretPosition = sel.End;
    }

    private void DeleteBack(int n)
    {
        var caret = Editor.CaretPosition;
        var start = caret;
        for (int i = 0; i < n; i++)
        {
            var p = start.GetNextInsertionPosition(LogicalDirection.Backward);
            if (p == null) break;
            start = p;
        }
        new TextRange(start, caret).Text = string.Empty;
        Editor.CaretPosition = start;
    }

    private string GetCurrentWord()
    {
        var before = Editor.CaretPosition.GetTextInRun(LogicalDirection.Backward) ?? "";
        int i = before.Length;
        while (i > 0 && IsWordChar(before[i - 1])) i--;
        return before[i..];
    }

    private void LearnCurrentWord()
    {
        var w = GetCurrentWord();
        if (w.Length >= 2) _predictor.Learn(w);
    }

    private void RefreshSuggestions()
    {
        var list = _predictor.Predict(GetCurrentWord(), 5);
        var buttons = new[] { Sug0, Sug1, Sug2, Sug3, Sug4 };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < list.Count)
            {
                buttons[i].Content = list[i];
                buttons[i].Visibility = Visibility.Visible;
            }
            else
            {
                buttons[i].Visibility = Visibility.Hidden;
            }
        }
    }

    private void Suggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Content is not string word) return;
        var w = GetCurrentWord();
        if (w.Length > 0) DeleteBack(w.Length);
        InsertText(word + " ");
        _predictor.Learn(word);
        RefreshSuggestions();
    }

    // ---------------- File toolbar ----------------

    private void New_Click(object sender, RoutedEventArgs e)
    {
        Editor.Document.Blocks.Clear();
        Editor.Document.Blocks.Add(new Paragraph(new Run(string.Empty)));
        Editor.CaretPosition = Editor.Document.ContentStart;
        RefreshSuggestions();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = DocumentIoService.ImportFilter };
        if (dlg.ShowDialog() == true)
        {
            try { DocumentIoService.Load(Editor, dlg.FileName); }
            catch (Exception ex) { MessageBox.Show("Datei konnte nicht geladen werden:\n" + ex.Message); }
            RefreshSuggestions();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = DocumentIoService.ExportFilter,
            FileName = "Text",
            DefaultExt = ".txt"
        };
        if (dlg.ShowDialog() == true)
        {
            try { DocumentIoService.Save(Editor, dlg.FileName); }
            catch (Exception ex) { MessageBox.Show("Datei konnte nicht gespeichert werden:\n" + ex.Message); }
        }
    }

    private void ToggleLayout_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.UseFrequencyLayout = !Keyboard.UseFrequencyLayout;
        UpdateLayoutButton();
    }

    private void UpdateLayoutButton()
    {
        LayoutButton.Content = Keyboard.UseFrequencyLayout
            ? "\u2328  Tastatur: H\u00e4ufigkeit"
            : "\u2328  Tastatur: QWERTZ";
    }

    private void Speak_Click(object sender, RoutedEventArgs e)
    {
        Tabs.SelectedItem = ReadingTab; // switch to reading tab
    }

    // ---------------- Reading tab: editor hosting ----------------

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return; // ignore inner controls

        if (ReferenceEquals(Tabs.SelectedItem, ReadingTab))
        {
            MoveEditorTo(ReadingEditorHost);
        }
        else
        {
            _speech.Stop();
            MoveEditorTo(WritingEditorHost);
        }
    }

    /// <summary>Re-parents the single editor between the writing and reading pages so the
    /// real caret/selection stays available for navigation and speaking commands.</summary>
    private void MoveEditorTo(Border host)
    {
        if (ReferenceEquals(Editor.Parent, host)) return;
        if (Editor.Parent is Border old) old.Child = null;
        host.Child = Editor;
        Editor.Focus();
    }

    // ---------------- Reading tab: edit & navigation tiles ----------------

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        ApplicationCommands.Copy.Execute(null, Editor);
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        ApplicationCommands.Paste.Execute(null, Editor);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        if (Editor.CanUndo) Editor.Undo();
    }

    private void ToggleSelect_Click(object sender, RoutedEventArgs e)
    {
        _selectMode = !_selectMode;
        SelectModeButton.Tag = _selectMode ? "on" : null;
        Editor.Focus();
    }

    /// <summary>Runs the move command, or its selection-extending variant when select mode is on.</summary>
    private void Nav(RoutedUICommand move, RoutedUICommand select)
    {
        Editor.Focus();
        (_selectMode ? select : move).Execute(null, Editor);
    }

    private void Left_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveLeftByCharacter, EditingCommands.SelectLeftByCharacter);
    private void Right_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveRightByCharacter, EditingCommands.SelectRightByCharacter);
    private void Up_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveUpByLine, EditingCommands.SelectUpByLine);
    private void Down_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveDownByLine, EditingCommands.SelectDownByLine);
    private void PrevWord_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveLeftByWord, EditingCommands.SelectLeftByWord);
    private void NextWord_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveRightByWord, EditingCommands.SelectRightByWord);
    private void DocStart_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveToDocumentStart, EditingCommands.SelectToDocumentStart);
    private void DocEnd_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveToDocumentEnd, EditingCommands.SelectToDocumentEnd);
    private void LineStart_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveToLineStart, EditingCommands.SelectToLineStart);
    private void LineEnd_Click(object sender, RoutedEventArgs e)
        => Nav(EditingCommands.MoveToLineEnd, EditingCommands.SelectToLineEnd);

    private void PrevSentence_Click(object sender, RoutedEventArgs e) => MoveSentence(-1);
    private void NextSentence_Click(object sender, RoutedEventArgs e) => MoveSentence(1);

    // ---------------- Reading tab: speaking tiles ----------------

    private void SpeakParagraph_Click(object sender, RoutedEventArgs e)
    {
        var para = Editor.CaretPosition.Paragraph;
        if (para != null) SpeakText(new TextRange(para.ContentStart, para.ContentEnd).Text);
    }

    private void SpeakSentence_Click(object sender, RoutedEventArgs e) => SpeakText(GetSentenceAtCaret());
    private void SpeakWord_Click(object sender, RoutedEventArgs e) => SpeakText(GetWordAtCaret());
    private void SpeakAll_Click(object sender, RoutedEventArgs e) => SpeakText(DocumentIoService.GetPlainText(Editor));
    private void SpeakSelection_Click(object sender, RoutedEventArgs e) => SpeakText(Editor.Selection.Text);

    private async void SpeakText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var voiceId = LangEnglish.IsChecked == true
            ? EnglishVoiceBox.SelectedValue as string
            : GermanVoiceBox.SelectedValue as string;
        try
        {
            await _speech.SpeakAsync(text, voiceId, RateSlider.Value, VolumeSlider.Value);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Sprachausgabe nicht m\u00f6glich:\n" + ex.Message);
        }
    }

    private void StopSpeak_Click(object sender, RoutedEventArgs e) => _speech.Stop();

    // ---------------- Reading tab: word / sentence extraction ----------------

    private static bool IsSentenceEnd(char c) => c is '.' or '!' or '?' or '\n';

    /// <summary>The word the caret currently sits in (or just after).</summary>
    private string GetWordAtCaret()
    {
        var (text, map) = BuildTextMap();
        if (text.Length == 0) return "";
        int off = Math.Clamp(CaretOffset(Editor.CaretPosition, map), 0, text.Length);

        string Word(int o)
        {
            int s = o, e = o;
            while (s > 0 && IsWordChar(text[s - 1])) s--;
            while (e < text.Length && IsWordChar(text[e])) e++;
            return text[s..e];
        }

        var w = Word(off);
        if (w.Length == 0 && off > 0) w = Word(off - 1);
        return w;
    }

    /// <summary>The sentence the caret currently sits in.</summary>
    private string GetSentenceAtCaret()
    {
        var (text, map) = BuildTextMap();
        if (text.Length == 0) return "";
        int off = Math.Clamp(CaretOffset(Editor.CaretPosition, map), 0, Math.Max(0, text.Length - 1));

        int s = off;
        while (s > 0 && !IsSentenceEnd(text[s - 1])) s--;
        while (s < text.Length && char.IsWhiteSpace(text[s])) s++;

        int e = off;
        while (e < text.Length && !IsSentenceEnd(text[e])) e++;
        if (e < text.Length && text[e] != '\n') e++; // include the terminator (. ! ?)

        return text[Math.Min(s, e)..e].Trim();
    }

    /// <summary>Moves the caret to the start of the previous/next sentence.</summary>
    private void MoveSentence(int dir)
    {
        Editor.Focus();
        var (text, map) = BuildTextMap();
        if (text.Length == 0) return;

        var starts = new List<int>();
        bool atStart = true;
        for (int i = 0; i < text.Length; i++)
        {
            if (atStart && !char.IsWhiteSpace(text[i])) { starts.Add(i); atStart = false; }
            if (IsSentenceEnd(text[i])) atStart = true;
        }
        if (starts.Count == 0) return;

        int off = Math.Clamp(CaretOffset(Editor.CaretPosition, map), 0, text.Length);
        int cur = 0;
        for (int i = 0; i < starts.Count; i++) if (starts[i] <= off) cur = i;

        int target = dir < 0
            ? (off > starts[cur] ? cur : cur - 1)   // first go to start of current sentence
            : cur + 1;
        target = Math.Clamp(target, 0, starts.Count - 1);

        Editor.CaretPosition = map[Math.Clamp(starts[target], 0, map.Count - 1)];
        Editor.Focus();
    }

    /// <summary>Builds the document's plain text together with a per-character TextPointer map.
    /// <c>map[i]</c> is the position before character <c>i</c>; <c>map[Length]</c> is the end.</summary>
    private (string text, List<TextPointer> map) BuildTextMap()
    {
        var sb = new StringBuilder();
        var map = new List<TextPointer>();
        TextPointer? p = Editor.Document.ContentStart;

        while (p != null)
        {
            var ctx = p.GetPointerContext(LogicalDirection.Forward);
            if (ctx == TextPointerContext.Text)
            {
                string run = p.GetTextInRun(LogicalDirection.Forward);
                TextPointer rp = p;
                foreach (char c in run)
                {
                    sb.Append(c);
                    map.Add(rp);
                    rp = rp.GetPositionAtOffset(1, LogicalDirection.Forward) ?? rp;
                }
                p = p.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
            }
            else
            {
                if (ctx == TextPointerContext.ElementEnd && p.Parent is Paragraph)
                {
                    sb.Append('\n');
                    map.Add(p);
                }
                p = p.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        map.Add(Editor.Document.ContentEnd);
        return (sb.ToString(), map);
    }

    private static int CaretOffset(TextPointer caret, List<TextPointer> map)
    {
        for (int i = 0; i < map.Count; i++)
            if (map[i].CompareTo(caret) >= 0) return i;
        return Math.Max(0, map.Count - 1);
    }

    private void Lang_Changed(object sender, RoutedEventArgs e) { /* used at speak time */ }
    private void Voice_Changed(object sender, SelectionChangedEventArgs e) { /* persisted on close */ }
    private void Rate_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { /* persisted on close */ }
    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { /* persisted on close */ }

    // ---------------- Head control / dwell click ----------------

    private void Dwell_Changed(object sender, RoutedEventArgs e)
    {
        if (_dwell != null) _dwell.Enabled = DwellEnabledBox.IsChecked == true;
    }

    private void DwellTime_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_dwell != null) _dwell.DwellSeconds = DwellSlider.Value;
        UpdateDwellLabel();
    }

    private void DwellMinus_Click(object sender, RoutedEventArgs e)
        => DwellSlider.Value = Math.Max(DwellSlider.Minimum, DwellSlider.Value - 0.1);

    private void DwellPlus_Click(object sender, RoutedEventArgs e)
        => DwellSlider.Value = Math.Min(DwellSlider.Maximum, DwellSlider.Value + 0.1);

    private void UpdateDwellLabel()
    {
        if (DwellTimeLabel != null)
            DwellTimeLabel.Text = $"Verweilzeit: {DwellSlider.Value:0.0} s";
    }
}
