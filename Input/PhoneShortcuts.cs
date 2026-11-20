using System.Windows.Input;
using PhoneMirror.Core;

namespace PhoneMirror.Input;

/// <summary>Raccourcis Alt+touche (inspirés de scrcpy) pour les boutons et fonctions du téléphone.</summary>
public static class PhoneShortcuts
{
    /// <summary>Exécute le raccourci Alt+<paramref name="key"/> ; renvoie false si la touche n'en est pas un.</summary>
    public static bool TryRun(Key key, bool shift, ScrcpyControlChannel channel)
    {
        switch (key)
        {
            case Key.H: channel.PressKey(AndroidKeys.Home); return true;
            case Key.B or Key.Back:
                channel.BackOrScreenOn(KeyAction.Down);
                channel.BackOrScreenOn(KeyAction.Up);
                return true;
            case Key.S: channel.PressKey(AndroidKeys.AppSwitch); return true;
            case Key.M: channel.PressKey(AndroidKeys.Menu); return true;
            case Key.Up: channel.PressKey(AndroidKeys.VolumeUp); return true;
            case Key.Down: channel.PressKey(AndroidKeys.VolumeDown); return true;
            case Key.P: channel.PressKey(AndroidKeys.Power); return true;
            case Key.R: channel.Send(PhoneCommand.RotateDevice); return true;
            case Key.N:
                channel.Send(shift ? PhoneCommand.CollapsePanels : PhoneCommand.ExpandNotificationPanel);
                return true;
            case Key.O: channel.SetDisplayPower(on: shift); return true;
            default: return false;
        }
    }
}
