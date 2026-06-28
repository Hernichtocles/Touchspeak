using System.Text;
using TouchSpeak.Models;

namespace TouchSpeak.Services;

/// <summary>Splits text into reading units (sentences or paragraphs).</summary>
public static class TextSegmenter
{
    // Common German abbreviations that end with a dot but do NOT end a sentence.
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "z.b.", "bzw.", "usw.", "etc.", "u.a.", "d.h.", "vgl.", "ca.", "nr.", "evtl.",
        "ggf.", "inkl.", "max.", "min.", "bspw.", "sog.", "z.t.", "u.u.", "i.d.r.",
        "o.\u00e4.", "u.\u00e4.", "tel.", "str.", "dr.", "prof.", "hr.", "fr.", "abk.",
        "mr.", "mrs.", "ms.", "geb.", "bzgl.", "zzgl.", "ggü.", "jr.", "vol."
    };

    public static List<string> GetUnits(string text, ReadingMode mode)
        => mode == ReadingMode.Paragraph ? GetParagraphs(text) : GetSentences(text);

    public static List<string> GetParagraphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var result = new List<string>();
        // A paragraph break is one or more blank/newline boundaries.
        var raw = text.Replace("\r\n", "\n").Replace('\r', '\n')
                      .Split('\n', StringSplitOptions.None);

        var current = new StringBuilder();
        foreach (var line in raw)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush(result, current);
            }
            else
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(line.Trim());
            }
        }
        Flush(result, current);
        return result;
    }

    private static void Flush(List<string> result, StringBuilder current)
    {
        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
            current.Clear();
        }
    }

    public static List<string> GetSentences(string text)
    {
        var sentences = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return sentences;

        // Normalise newlines to spaces so a sentence can span line wraps,
        // but keep explicit paragraph breaks as sentence boundaries.
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // Hard break on a blank line (paragraph boundary).
            if (c == '\n' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                AddSentence(sentences, sb);
                i++; // skip the second newline
                continue;
            }

            if (c == '\n') c = ' ';
            sb.Append(c);

            if (c is '.' or '!' or '?' or '\u2026')
            {
                // Look at the word ending here to skip abbreviations.
                if (c == '.' && EndsWithAbbreviation(sb))
                    continue;

                // Peek ahead: a sentence ends when followed by whitespace + (capital/quote/end).
                int j = i + 1;
                while (j < text.Length && text[j] is '.' or '!' or '?' or '\u2026' or '"' or '\'' or ')')
                {
                    sb.Append(text[j]);
                    j++;
                }
                i = j - 1;

                bool atEnd = j >= text.Length;
                bool followedBySpace = !atEnd && char.IsWhiteSpace(text[j]);
                if (atEnd || followedBySpace)
                    AddSentence(sentences, sb);
            }
        }
        AddSentence(sentences, sb);
        return sentences;
    }

    private static bool EndsWithAbbreviation(StringBuilder sb)
    {
        // Extract the last token (letters and dots) and test it.
        int end = sb.Length;            // points just after the '.'
        int start = end;
        while (start > 0)
        {
            char ch = sb[start - 1];
            if (char.IsWhiteSpace(ch)) break;
            start--;
        }
        var token = sb.ToString(start, end - start).Trim();
        return Abbreviations.Contains(token);
    }

    private static void AddSentence(List<string> list, StringBuilder sb)
    {
        var s = sb.ToString().Trim();
        if (s.Length > 0) list.Add(s);
        sb.Clear();
    }
}
