using System.Windows;
using Microsoft.Win32;

namespace TouchSpeak;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SuppressWindowsTouchKeyboard();
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
