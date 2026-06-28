namespace TouchSpeak.Models;

public enum ReadingMode
{
    Sentence,   // Satzweise
    Paragraph   // Abschnittsweise
}

public enum SpeakLanguage
{
    German,
    English
}

/// <summary>Persisted user settings (stored as JSON in %AppData%\TouchSpeak).</summary>
public sealed class AppSettings
{
    public string? GermanVoiceId { get; set; }
    public string? EnglishVoiceId { get; set; }
    public double SpeechRate { get; set; } = 1.0;   // 0.5 .. 3.0 (1.0 = normal)
    public double SpeechVolume { get; set; } = 1.0;  // 0.0 .. 1.0
    public bool UseFrequencyLayout { get; set; }     // false = QWERTZ
    public ReadingMode ReadingMode { get; set; } = ReadingMode.Sentence;
    public SpeakLanguage SpeakLanguage { get; set; } = SpeakLanguage.German;
}
