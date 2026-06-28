using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace TouchSpeak.Services;

/// <summary>Lightweight info about an installed voice.</summary>
public sealed record VoiceInfo(string Id, string DisplayName, string Language);

/// <summary>
/// Text-to-speech using the modern Windows (WinRT) voices. Audio is played through
/// a headless <see cref="MediaPlayer"/> so it handles the synthesizer's audio format
/// natively and gives us pause / resume / stop and a completion event.
/// </summary>
public sealed class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private MediaPlayer? _player;

    /// <summary>Raised (on a background thread) when the current utterance finishes.</summary>
    public event EventHandler? Completed;

    public static IReadOnlyList<VoiceInfo> GetVoices()
    {
        var list = new List<VoiceInfo>();
        foreach (var v in SpeechSynthesizer.AllVoices)
            list.Add(new VoiceInfo(v.Id, v.DisplayName, v.Language));
        return list;
    }

    public static IEnumerable<VoiceInfo> GetVoicesForLanguage(string prefix)
        => GetVoices().Where(v => v.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public async Task SpeakAsync(string text, string? voiceId, double rate, double volume)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (!string.IsNullOrEmpty(voiceId))
        {
            var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == voiceId);
            if (voice != null) _synth.Voice = voice;
        }

        _synth.Options.SpeakingRate = Math.Clamp(rate, 0.5, 6.0);
        _synth.Options.AudioVolume = Math.Clamp(volume, 0.0, 1.0);

        var stream = await _synth.SynthesizeTextToStreamAsync(text);

        _player = new MediaPlayer { AutoPlay = false };
        _player.MediaEnded += OnMediaEnded;
        _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
        _player.Play();
    }

    public void Pause() => _player?.Pause();

    public void Resume() => _player?.Play();

    public void Stop()
    {
        if (_player != null)
        {
            _player.MediaEnded -= OnMediaEnded;
            try { _player.Pause(); } catch { /* ignore */ }
            _player.Source = null;
            _player.Dispose();
            _player = null;
        }
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
        => Completed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        Stop();
        _synth.Dispose();
    }
}
