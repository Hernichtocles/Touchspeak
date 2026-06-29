using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TouchSpeak;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Globaler Schutz: Eine unerwartete Ausnahme (z. B. ausgelöst durch einen
        // Verweil-Klick der Kopfsteuerung im Hintergrund-Timer) soll die App nicht
        // abstürzen lassen. Stattdessen wird sie protokolliert und der Nutzer
        // informiert – ein Kopfmaus-Nutzer kann einen harten Absturz nicht abfangen.
        DispatcherUnhandledException += OnUnhandledException;

        SuppressWindowsTouchKeyboard();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // App am Leben halten

        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TouchSpeak", "error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Protokollieren darf selbst nie zum Absturz führen.
        }

        MessageBox.Show(
            "Es ist ein unerwarteter Fehler aufgetreten. Die App läuft weiter.\n\n" +
            e.Exception.Message,
            "TouchSpeak", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// Verhindert, dass Windows seine eigene Bildschirmtastatur automatisch einblendet,
    /// sobald unser <c>Editor</c> (RichTextBox) per Touch den Fokus bekommt. Da TouchSpeak
    /// eine eigene Bildschirmtastatur mitbringt, würde sonst eine zweite Tastatur erscheinen.
    ///
    /// Der Schlüssel liegt im aktuellen Benutzerprofil (kein Admin nötig); 0 = nicht
    /// automatisch einblenden. Wirkt auf Windows 10/11 im Desktop-Modus.
    /// </summary>
    private static void SuppressWindowsTouchKeyboard()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\TabletTip\1.7");
            if (key?.GetValue("EnableDesktopModeAutoInvoke") is not int v || v != 0)
                key?.SetValue("EnableDesktopModeAutoInvoke", 0, RegistryValueKind.DWord);
        }
        catch
        {
            // Registry nicht beschreibbar (z. B. eingeschränktes Profil): App läuft trotzdem
            // weiter; die zweite Tastatur lässt sich dann nur über die Windows-Einstellung
            // abschalten.
        }
    }
}
