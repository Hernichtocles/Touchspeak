namespace TouchSpeak.Controls;

public enum KeyType
{
    Char,         // inserts a character (letter or punctuation)
    Shift,        // toggles upper/lower case
    Backspace,    // deletes one character
    DeleteWord,   // deletes the word before the caret
    Enter,
    Space,
    SwitchLayout, // QWERTZ <-> frequency layout
    Phrase        // inserts and speaks a fixed message (e.g. "WC")
}

public sealed class KeyDef
{
    public string Lower { get; init; } = "";
    public string Upper { get; init; } = "";
    public string Label { get; init; } = "";   // shown on special keys
    public KeyType Type { get; init; } = KeyType.Char;
    public double Width { get; init; } = 1.0;   // relative width within its row

    public static KeyDef Letter(string lower) =>
        new() { Lower = lower, Upper = lower.ToUpperInvariant(), Type = KeyType.Char };

    public static KeyDef Punct(string c, double w = 1.0) =>
        new() { Lower = c, Upper = c, Type = KeyType.Char, Width = w };

    public static KeyDef Special(KeyType type, string label, double w = 1.0) =>
        new() { Type = type, Label = label, Width = w };

    /// <summary>A tile that inserts and speaks a fixed message. <paramref name="text"/>
    /// is the spoken/inserted message, <paramref name="label"/> the caption on the key.</summary>
    public static KeyDef Phrase(string label, string text, double w = 1.0) =>
        new() { Type = KeyType.Phrase, Label = label, Lower = text, Upper = text, Width = w };
}

/// <summary>
/// Two layouts share one command row. The "frequency" layout orders letters by
/// German letter frequency (most frequent first, top-left), which suits scanning
/// and fast access in a communication aid.
/// </summary>
public static class KeyboardLayouts
{
    private static List<KeyDef> Letters(params string[] letters)
        => letters.Select(KeyDef.Letter).ToList();

    private static List<KeyDef> CommandRow() => new()
    {
        KeyDef.Special(KeyType.SwitchLayout, "ABC ⇄ Häufig", 1.8),
        KeyDef.Punct(","),
        KeyDef.Punct("."),
        KeyDef.Punct("?"),
        KeyDef.Punct("!"),
        KeyDef.Special(KeyType.Space, "Leertaste", 4.0),
        KeyDef.Special(KeyType.DeleteWord, "⌫ Wort", 1.6),
        KeyDef.Special(KeyType.Backspace, "⌫ Löschen", 1.8),
        KeyDef.Special(KeyType.Enter, "↵ Zeile", 1.6),
    };

    public static List<List<KeyDef>> Qwertz()
    {
        var rows = new List<List<KeyDef>>
        {
            Letters("q", "w", "e", "r", "t", "z", "u", "i", "o", "p", "ü"),
            Letters("a", "s", "d", "f", "g", "h", "j", "k", "l", "ö", "ä"),
            new() { KeyDef.Special(KeyType.Shift, "⇧", 1.6) }
        };
        rows[2].AddRange(Letters("y", "x", "c", "v", "b", "n", "m"));
        rows[2].Add(KeyDef.Letter("ß"));
        rows.Add(CommandRow());
        return rows;
    }

    public static List<List<KeyDef>> Frequency()
    {
        // 4x8 grid following the prescribed communication board.
        // Top-left cell is the Shift key; bottom-right is the "WC" message tile.
        var rows = new List<List<KeyDef>>
        {
            new() { KeyDef.Special(KeyType.Shift, "⇧") },
            new(),
            new(),
            new()
        };
        rows[0].AddRange(Letters("e", "i", "t", "u", "b", "w", "ß"));
        rows[1].AddRange(Letters("n", "r", "a", "g", "m", "k", "p", "q"));
        rows[2].AddRange(Letters("s", "h", "l", "f", "z", "ü", "j", "y"));
        rows[3].AddRange(Letters("d", "c", "o", "v", "ä", "ö", "x"));
        rows[3].Add(KeyDef.Phrase("WC", "Ich muss zur Toilette."));
        rows.Add(CommandRow());
        return rows;
    }
}
