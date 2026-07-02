using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TouchSpeak.Controls;

public partial class TouchKeyboard : UserControl
{
    private bool _shift;
    private bool _useFrequency;

    /// <summary>Fired for printable characters (letters and punctuation).</summary>
    public event Action<string>? CharInput;
    public event Action? SpacePressed;
    public event Action? BackspacePressed;
    public event Action? EnterPressed;
    /// <summary>Fired for message tiles (e.g. "WC"); carries the message text.</summary>
    public event Action<string>? PhraseInput;

    public TouchKeyboard()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    public bool UseFrequencyLayout
    {
        get => _useFrequency;
        set { _useFrequency = value; Rebuild(); }
    }

    private List<List<KeyDef>> CurrentLayout()
        => _useFrequency ? KeyboardLayouts.Frequency() : KeyboardLayouts.Qwertz();

    private void Rebuild()
    {
        if (RowsHost == null) return;
        RowsHost.Children.Clear();

        foreach (var row in CurrentLayout())
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            for (int i = 0; i < row.Count; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(row[i].Width, GridUnitType.Star)
                });

            for (int i = 0; i < row.Count; i++)
            {
                var key = row[i];
                var button = CreateButton(key);
                Grid.SetColumn(button, i);
                grid.Children.Add(button);
            }
            RowsHost.Children.Add(grid);
        }
    }

    private Button CreateButton(KeyDef key)
    {
        var button = new Button { Style = (Style)Resources["KeyButtonStyle"] };

        switch (key.Type)
        {
            case KeyType.Char:
                button.Content = _shift ? key.Upper : key.Lower;
                button.Click += (_, _) => OnCharKey(key);
                break;

            case KeyType.Space:
                button.Content = key.Label;
                button.Click += (_, _) => SpacePressed?.Invoke();
                button.Background = (Brush)Application.Current.Resources["KeySpecialBackground"];
                break;

            case KeyType.Backspace:
                button.Content = key.Label;
                button.Click += (_, _) => BackspacePressed?.Invoke();
                button.Background = (Brush)Application.Current.Resources["KeySpecialBackground"];
                break;

            case KeyType.Enter:
                button.Content = key.Label;
                button.Click += (_, _) => EnterPressed?.Invoke();
                button.Background = (Brush)Application.Current.Resources["KeySpecialBackground"];
                break;

            case KeyType.Shift:
                button.Content = key.Label;
                button.FontSize = 34;
                button.Background = _shift
                    ? (Brush)Application.Current.Resources["AccentBackground"]
                    : (Brush)Application.Current.Resources["KeySpecialBackground"];
                button.Click += (_, _) => { _shift = !_shift; Rebuild(); };
                break;

            case KeyType.SwitchLayout:
                button.Content = key.Label;
                button.FontSize = 20;
                button.Background = (Brush)Application.Current.Resources["KeySpecialBackground"];
                button.Click += (_, _) => { _useFrequency = !_useFrequency; LayoutChanged?.Invoke(_useFrequency); Rebuild(); };
                break;

            case KeyType.Phrase:
                button.Content = key.Label;
                button.Background = (Brush)Application.Current.Resources["KeySpecialBackground"];
                button.Click += (_, _) => PhraseInput?.Invoke(key.Lower);
                break;
        }
        return button;
    }

    /// <summary>Raised when the user toggles the layout from the keyboard itself.</summary>
    public event Action<bool>? LayoutChanged;

    private void OnCharKey(KeyDef key)
    {
        var text = _shift ? key.Upper : key.Lower;
        CharInput?.Invoke(text);

        // One-shot shift: after typing a letter, drop back to lower case.
        if (_shift && IsLetter(key))
        {
            _shift = false;
            Rebuild();
        }
    }

    private static bool IsLetter(KeyDef key)
        => key.Lower.Length == 1 && char.IsLetter(key.Lower[0]);
}
