using System.IO;
using System.Text.Json;

namespace TouchSpeak.Services;

/// <summary>
/// Prefix-based word prediction. Combines a bundled German base dictionary
/// (ranked by frequency = line order) with a learned per-user frequency map.
/// </summary>
public sealed class WordPredictor
{
    // word -> base rank (lower = more frequent). Stored lower-case.
    private readonly Dictionary<string, int> _baseRank = new(StringComparer.OrdinalIgnoreCase);
    // word -> how often the user has typed it (stored as typed).
    private Dictionary<string, int> _userFreq = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _userDictPath =
        Path.Combine(SettingsService.AppFolder, "userdict.json");

    private int _learnsSinceSave;

    public WordPredictor()
    {
        LoadBaseDictionary();
        LoadUserDictionary();
    }

    private void LoadBaseDictionary()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "de_words.txt");
            if (!File.Exists(path)) return;

            int rank = 0;
            foreach (var raw in File.ReadLines(path))
            {
                var word = raw.Trim();
                if (word.Length == 0 || word.StartsWith('#')) continue;
                if (!_baseRank.ContainsKey(word))
                    _baseRank[word] = rank++;
            }
        }
        catch
        {
            // Missing dictionary just disables base suggestions.
        }
    }

    private void LoadUserDictionary()
    {
        try
        {
            if (File.Exists(_userDictPath))
            {
                var json = File.ReadAllText(_userDictPath);
                var map = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (map != null)
                    _userFreq = new Dictionary<string, int>(map, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _userFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SaveUserDictionary()
    {
        try
        {
            var json = JsonSerializer.Serialize(_userFreq,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_userDictPath, json);
            _learnsSinceSave = 0;
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>Records a completed word so future predictions favour it.</summary>
    public void Learn(string word)
    {
        word = word.Trim();
        if (word.Length < 2) return;
        if (!word.Any(char.IsLetter)) return;

        _userFreq.TryGetValue(word, out var count);
        _userFreq[word] = count + 1;

        if (++_learnsSinceSave >= 5)
            SaveUserDictionary();
    }

    /// <summary>Returns up to <paramref name="max"/> completions for the given prefix.</summary>
    public List<string> Predict(string prefix, int max = 5)
    {
        prefix = prefix.Trim();
        if (prefix.Length == 0) return new List<string>();

        bool firstUpper = char.IsUpper(prefix[0]);

        // Gather candidate words from both sources.
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in _userFreq.Keys)
            if (StartsWith(w, prefix)) candidates.Add(w);
        foreach (var w in _baseRank.Keys)
            if (StartsWith(w, prefix)) candidates.Add(w);

        // Rank: higher user frequency first, then lower base rank, then shorter, then alpha.
        var ranked = candidates
            .Where(w => !string.Equals(w, prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(w => _userFreq.TryGetValue(w, out var f) ? f : 0)
            .ThenBy(w => _baseRank.TryGetValue(w, out var r) ? r : int.MaxValue)
            .ThenBy(w => w.Length)
            .ThenBy(w => w, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(w => firstUpper ? Capitalize(w) : w)
            .ToList();

        return ranked;
    }

    private static bool StartsWith(string word, string prefix)
        => word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static string Capitalize(string w)
        => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..];
}
