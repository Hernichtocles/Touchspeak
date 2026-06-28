using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    private bool _suppressSpeak;
    private bool _speakAfterRebuild;

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

        if (_settings.ReadingMode == ReadingMode.Paragraph) ModeParagraph.IsChecked = true;
        else ModeSentence.IsChecked = true;

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
        _settings.ReadingMode = ModeParagraph.IsChecked == true ? ReadingMode.Paragraph : ReadingMode.Sentence;
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
        _speakAfterRebuild = true;
        Tabs.SelectedIndex = 1; // switch to reading tab
    }

    // ---------------- Reading tab ----------------

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (!ReferenceEquals(e.OriginalSource, Tabs)) return; // ignore inner controls

        if (Tabs.SelectedIndex == 1)
        {
            RebuildUnits(_speakAfterRebuild);
            _speakAfterRebuild = false;
        }
        else
        {
            _speech.Stop();
        }
    }

    private void RebuildUnits(bool speakFirst = false)
    {
        var text = DocumentIoService.GetPlainText(Editor);
        var mode = ModeParagraph.IsChecked == true ? ReadingMode.Paragraph : ReadingMode.Sentence;
        var units = TextSegmenter.GetUnits(text, mode);

        _suppressSpeak = true;
        UnitsList.ItemsSource = units;
        UnitsList.SelectedIndex = units.Count > 0 ? 0 : -1;
        _suppressSpeak = false;

        UpdateStatus();
        if (speakFirst && units.Count > 0) SpeakCurrent();
    }

    private void UpdateStatus()
    {
        int n = UnitsList.Items.Count;
        int idx = UnitsList.SelectedIndex;
        var label = ModeParagraph.IsChecked == true ? "Abschnitt" : "Satz";
        StatusText.Text = $"{label} {(idx >= 0 ? idx + 1 : 0)} / {n}";
    }

    private void UnitsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatus();
        if (_suppressSpeak || UnitsList.SelectedItem == null) return;
        UnitsList.ScrollIntoView(UnitsList.SelectedItem);
        SpeakCurrent();
    }

    private void SpeakCurrent()
    {
        if (UnitsList.SelectedItem is string text) SpeakText(text);
    }

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

    private void Move(int delta)
    {
        int n = UnitsList.Items.Count;
        if (n == 0) return;
        int idx = Math.Clamp(UnitsList.SelectedIndex + delta, 0, n - 1);
        UnitsList.SelectedIndex = idx; // SelectionChanged speaks + highlights
    }

    private void Next_Click(object sender, RoutedEventArgs e) => Move(1);
    private void Prev_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void ReplaySpeak_Click(object sender, RoutedEventArgs e) => SpeakCurrent();
    private void Pause_Click(object sender, RoutedEventArgs e) => _speech.Pause();
    private void Resume_Click(object sender, RoutedEventArgs e) => _speech.Resume();
    private void StopSpeak_Click(object sender, RoutedEventArgs e) => _speech.Stop();

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        if (Tabs.SelectedIndex == 1) RebuildUnits();
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
