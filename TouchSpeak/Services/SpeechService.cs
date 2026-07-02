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
/// Rate and volume are applied at playback time (not baked into the synthesized
/// stream), so they can be changed while speech is running.
/// </summary>
public sealed class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private MediaPlayer? _player;
    private string? _currentText;
    private string? _voiceId;
    private double _rate = 1.0;
    private double _volume = 1.0;
    private bool _speaking;

    /// <summary>Raised (on a background thread) when the current utterance finishes.</summary>
    public event EventHandler? Completed;

    /// <summary>Speaking rate; takes effect immediately, also mid-utterance.</summary>
    public double Rate
    {
        get => _rate;
        set
        {
            _rate = Math.Clamp(value, 0.5, 3.0);
            if (_player != null)
                try { _player.PlaybackSession.PlaybackRate = _rate; } catch { /* ignore */ }
        }
    }

    /// <summary>Volume (0..1); takes effect immediately, also mid-utterance.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            if (_player != null) _player.Volume = _volume;
        }
    }

    public bool IsSpeaking => _speaking;

    public static IReadOnlyList<VoiceInfo> GetVoices()
    {
        var list = new List<VoiceInfo>();
        foreach (var v in SpeechSynthesizer.AllVoices)
            list.Add(new VoiceInfo(v.Id, v.DisplayName, v.Language));
        return list;
    }

    public static IEnumerable<VoiceInfo> GetVoicesForLanguage(string prefix)
        => GetVoices().Where(v => v.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    public async Task SpeakAsync(string text, string? voiceId)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(text)) return;

        _currentText = text;
        _voiceId = voiceId;
        await StartPlaybackAsync();
    }

    /// <summary>
    /// Switches the voice. A running utterance is restarted with the new voice
    /// so the change is audible immediately.
    /// </summary>
    public async Task SetVoiceAsync(string? voiceId)
    {
        if (voiceId == _voiceId) return;
        _voiceId = voiceId;

        if (_speaking && _currentText != null)
        {
            var text = _currentText;
            Stop();
            _currentText = text;
            await StartPlaybackAsync();
        }
    }

    private async Task StartPlaybackAsync()
    {
        if (!string.IsNullOrEmpty(_voiceId))
        {
            var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == _voiceId);
            if (voice != null) _synth.Voice = voice;
        }

        var stream = await _synth.SynthesizeTextToStreamAsync(_currentText);

        _player = new MediaPlayer { AutoPlay = false, Volume = _volume };
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
        _speaking = true;
        _player.Play();
    }

    public void Pause() => _player?.Pause();

    public void Resume() => _player?.Play();

    public void Stop()
    {
        _speaking = false;
        _currentText = null;
        if (_player != null)
        {
            _player.MediaOpened -= OnMediaOpened;
            _player.MediaEnded -= OnMediaEnded;
            try { _player.Pause(); } catch { /* ignore */ }
            _player.Source = null;
            _player.Dispose();
            _player = null;
        }
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        // PlaybackRate only sticks once the media is opened.
        try { sender.PlaybackSession.PlaybackRate = _rate; } catch { /* ignore */ }
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        _speaking = false;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _synth.Dispose();
    }
}
