namespace TouchSpeak.Controls;

public enum KeyType
{
    Char,         // inserts a character (letter or punctuation)
    Shift,        // toggles upper/lower case
    Backspace,
    Enter,
    Space,
    SwitchLayout  // QWERTZ <-> frequency layout
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
        KeyDef.Special(KeyType.SwitchLayout, "ABC \u21c4 H\u00e4ufig", 1.8),
        KeyDef.Punct(","),
        KeyDef.Punct("."),
        KeyDef.Punct("?"),
        KeyDef.Punct("!"),
        KeyDef.Special(KeyType.Space, "Leertaste", 4.5),
        KeyDef.Special(KeyType.Backspace, "\u232b L\u00f6schen", 1.8),
        KeyDef.Special(KeyType.Enter, "\u21b5 Zeile", 1.6),
    };

    public static List<List<KeyDef>> Qwertz()
    {
        var rows = new List<List<KeyDef>>
        {
            Letters("q", "w", "e", "r", "t", "z", "u", "i", "o", "p", "\u00fc"),
            Letters("a", "s", "d", "f", "g", "h", "j", "k", "l", "\u00f6", "\u00e4"),
            new() { KeyDef.Special(KeyType.Shift, "\u21e7", 1.6) }
        };
        rows[2].AddRange(Letters("y", "x", "c", "v", "b", "n", "m"));
        rows[2].Add(KeyDef.Letter("\u00df"));
        rows.Add(CommandRow());
        return rows;
    }

    public static List<List<KeyDef>> Frequency()
    {
        var rows = new List<List<KeyDef>>
        {
            Letters("e", "n", "i", "s", "r", "a", "t"),
            Letters("d", "h", "u", "l", "c", "g", "m"),
            Letters("o", "b", "w", "f", "k", "z", "p"),
            new() { KeyDef.Special(KeyType.Shift, "\u21e7", 1.6) }
        };
        rows[3].AddRange(Letters("v", "j", "y", "x", "q", "\u00e4", "\u00f6", "\u00fc"));
        rows[3].Add(KeyDef.Letter("\u00df"));
        rows.Add(CommandRow());
        return rows;
    }
}
