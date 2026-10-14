using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace TouchSpeak.Services;

/// <summary>Lightweight info about an installed voice.</summary>
public sealed record VoiceInfo(string Id, string DisplayName, string Language);

/// <summary>
/// Text-to-speech with two engines behind one interface:
/// <list type="bullet">
/// <item>the modern Windows (WinRT) voices, and</item>
/// <item><b>Piper</b> – free neural voices (e.g. the German "Thorsten") that run fully offline.
/// Piper ships as <c>Piper\piper.exe</c> next to the app and is started per utterance; the WAV it
/// returns is played like any other utterance.</item>
/// </list>
/// Audio is played through a headless <see cref="MediaPlayer"/> so it handles both audio formats
/// natively and gives us pause / resume / stop and a completion event. Rate and volume are applied
/// at playback time (not baked into the synthesized stream), so they can be changed while speech
/// is running – for either engine.
/// </summary>
public sealed class SpeechService : IDisposable
{
    // ---------------- Piper (neuronale Offline-Stimmen) ----------------

    /// <summary>Kennungs-Präfix von Piper-Stimmen (z. B. <c>piper:de_DE-thorsten-medium</c>);
    /// alles andere sind Windows-Stimmen (Registry-Pfade).</summary>
    public const string PiperPrefix = "piper:";

    /// <summary>Ordner mit <c>piper.exe</c>, Laufzeit-DLLs, <c>espeak-ng-data</c> und den
    /// Stimmmodellen (<c>*.onnx</c> + <c>*.onnx.json</c>).</summary>
    public static string PiperDirectory => Path.Combine(AppContext.BaseDirectory, "Piper");

    private static string PiperExe => Path.Combine(PiperDirectory, "piper.exe");

    public static bool IsPiperVoice(string? voiceId)
        => voiceId != null && voiceId.StartsWith(PiperPrefix, StringComparison.Ordinal);

    private MediaPlayer? _player;
    private string? _currentText;
    private string? _voiceId;
    private double _rate = 1.0;
    private double _volume = 1.0;
    private bool _speaking;

    // Laufende Nummer des aktuellen Sprechauftrags. Die Synthese ist asynchron; kommt
    // währenddessen ein neuer Auftrag oder ein Stop, darf das alte Ergebnis keinen
    // Player mehr starten – sonst spielen zwei Ausgaben gleichzeitig übereinander.
    private int _session;

    /// <summary>Raised (on a background thread) when the current utterance finishes.</summary>
    public event EventHandler? Completed;

    /// <summary>Raised (on a background thread) when playback could not start, e.g. because the
    /// audio data was unreadable. Carries a short message for the user – without this the
    /// failure would be silent.</summary>
    public event EventHandler<string>? Failed;

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

    /// <summary>Alle verfügbaren Stimmen. Piper-Stimmen stehen vorn: beste Qualität, und
    /// dadurch bei einer Neuinstallation (noch keine gespeicherte Auswahl) die Vorgabe.</summary>
    public static IReadOnlyList<VoiceInfo> GetVoices()
    {
        var list = new List<VoiceInfo>();
        list.AddRange(GetPiperVoices());
        foreach (var v in SpeechSynthesizer.AllVoices)
            list.Add(new VoiceInfo(v.Id, v.DisplayName, v.Language));
        return list;
    }

    public static IEnumerable<VoiceInfo> GetVoicesForLanguage(string prefix)
        => GetVoices().Where(v => v.Language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>Findet alle Stimmmodelle im Piper-Ordner. Weitere Stimmen lassen sich
    /// einfach als <c>*.onnx</c> + <c>*.onnx.json</c> dazulegen.</summary>
    private static IEnumerable<VoiceInfo> GetPiperVoices()
    {
        if (!File.Exists(PiperExe)) yield break;

        foreach (var onnx in Directory.EnumerateFiles(PiperDirectory, "*.onnx").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(onnx + ".json")) continue;
            var name = Path.GetFileNameWithoutExtension(onnx);      // z. B. de_DE-thorsten-medium
            yield return new VoiceInfo(PiperPrefix + name, PiperDisplayName(name), PiperLanguage(onnx + ".json", name));
        }
    }

    /// <summary>"de_DE-thorsten-medium" → "Thorsten – natürliche Stimme (Piper medium)".</summary>
    private static string PiperDisplayName(string modelName)
    {
        var parts = modelName.Split('-');
        var speaker = parts.Length > 1 ? parts[1] : modelName;
        var quality = parts.Length > 2 ? " " + parts[2] : "";
        if (speaker.Length > 0) speaker = char.ToUpperInvariant(speaker[0]) + speaker[1..];
        return $"{speaker} – natürliche Stimme (Piper{quality})";
    }

    /// <summary>Sprachcode aus der Modellbeschreibung ("de_DE" → "de-DE"); Rückfall auf den Dateinamen.</summary>
    private static string PiperLanguage(string jsonPath, string modelName)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (doc.RootElement.TryGetProperty("language", out var lang) &&
                lang.TryGetProperty("code", out var code) &&
                code.GetString() is { Length: > 0 } s)
                return s.Replace('_', '-');
        }
        catch { /* Beschreibung unlesbar: Dateiname reicht */ }

        var prefix = modelName.Split('-')[0];
        return prefix.Replace('_', '-');
    }

    // ---------------- Sprechen ----------------

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
        int session = ++_session;
        var text = _currentText;
        if (string.IsNullOrEmpty(text)) return;

        IRandomAccessStream stream;
        string contentType;

        if (IsPiperVoice(_voiceId))
        {
            var wav = await SynthesizeWithPiperAsync(text, _voiceId!);
            if (session != _session) return; // überholt durch neueren Auftrag oder Stop
            stream = await ToRandomAccessStreamAsync(wav);
            contentType = "audio/wav";
        }
        else
        {
            // Eigener Synthesizer pro Auftrag: kein geteilter Voice-Zustand zwischen
            // parallel angestoßenen Aufträgen.
            using var synth = new SpeechSynthesizer();
            if (!string.IsNullOrEmpty(_voiceId))
            {
                var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == _voiceId);
                if (voice != null) synth.Voice = voice;
            }

            var synthStream = await synth.SynthesizeTextToStreamAsync(text);
            if (session != _session) return;
            stream = synthStream;
            contentType = synthStream.ContentType;
        }

        _player = new MediaPlayer { AutoPlay = false, Volume = _volume };
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
        _player.Source = MediaSource.CreateFromStream(stream, contentType);
        _speaking = true;
        _player.Play();
    }

    /// <summary>Lässt <c>piper.exe</c> den Text sprechen und liefert die fertige WAV-Datei als Bytes.
    /// Der Text wird als EINE Zeile übergeben: Piper behandelt jede Eingabezeile als eigenen
    /// Auftrag und würde mit <c>--output_file</c> nur die letzte behalten. Satzgrenzen erkennt
    /// Piper selbst und legt dazwischen eine kurze Pause.
    /// <para>Die WAV geht über eine Temp-Datei, NICHT über die Standardausgabe („-"): Piper öffnet
    /// stdout unter Windows im Textmodus, der aus jedem 0x0A-Byte ein 0x0D 0x0A macht. Das
    /// verschiebt die 16-Bit-Abtastwerte, und aus der Sprache wird reines Rauschen.</para></summary>
    private static async Task<byte[]> SynthesizeWithPiperAsync(string text, string voiceId)
    {
        var model = Path.Combine(PiperDirectory, voiceId[PiperPrefix.Length..] + ".onnx");
        if (!File.Exists(PiperExe))
            throw new FileNotFoundException("Die Piper-Sprachausgabe fehlt (Ordner „Piper“ neben der App).", PiperExe);
        if (!File.Exists(model))
            throw new FileNotFoundException("Das Stimmmodell fehlt im Piper-Ordner.", model);

        var wavPath = Path.Combine(Path.GetTempPath(), $"touchspeak-piper-{Guid.NewGuid():N}.wav");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = PiperExe,
                WorkingDirectory = PiperDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(model);
            psi.ArgumentList.Add("--output_file");
            psi.ArgumentList.Add(wavPath);
            psi.ArgumentList.Add("--sentence_silence");
            psi.ArgumentList.Add("0.25");
            psi.ArgumentList.Add("--quiet");

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Piper konnte nicht gestartet werden.");

            // Fehlerausgabe sofort asynchron abholen, sonst blockiert der Prozess bei vollem Puffer.
            var stderrTask = proc.StandardError.ReadToEndAsync();

            await proc.StandardInput.WriteAsync(ToSingleLine(text));
            proc.StandardInput.Close();

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            try
            {
                await proc.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(); } catch { /* ignore */ }
                throw new TimeoutException("Piper hat zu lange für die Sprachausgabe gebraucht.");
            }

            var err = await stderrTask;
            if (proc.ExitCode != 0 || !File.Exists(wavPath))
                throw new InvalidOperationException("Piper-Fehler: " + (err.Trim().Length > 0 ? err.Trim() : $"Exit-Code {proc.ExitCode}"));

            var wav = await File.ReadAllBytesAsync(wavPath);
            if (wav.Length < 44)
                throw new InvalidOperationException("Piper hat keine Sprachdaten geliefert.");
            return wav;
        }
        finally
        {
            try { File.Delete(wavPath); } catch { /* Temp-Datei; Aufräumen ist Nebensache */ }
        }
    }

    /// <summary>Absätze/Zeilen zu einer Zeile zusammenfassen. Endet eine Zeile ohne Satzzeichen,
    /// bekommt sie einen Punkt, damit Piper dort eine Satzpause macht statt den Text zu verschleifen.</summary>
    private static string ToSingleLine(string text)
    {
        var sb = new StringBuilder(text.Length + 16);
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(line);
            if (!".!?…:;".Contains(line[^1])) sb.Append('.');
        }
        return sb.ToString();
    }

    private static async Task<IRandomAccessStream> ToRandomAccessStreamAsync(byte[] data)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(data);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        return stream;
    }

    public void Pause() => _player?.Pause();

    public void Resume() => _player?.Play();

    public void Stop()
    {
        _session++; // entwertet eine gerade noch laufende Synthese
        _speaking = false;
        _currentText = null;
        if (_player != null)
        {
            _player.MediaOpened -= OnMediaOpened;
            _player.MediaEnded -= OnMediaEnded;
            _player.MediaFailed -= OnMediaFailed;
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

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        _speaking = false;
        Failed?.Invoke(this, $"Wiedergabe fehlgeschlagen ({args.Error}): {args.ErrorMessage}");
    }

    public void Dispose() => Stop();
}
