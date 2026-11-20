using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using PhoneMirror.Core;

namespace PhoneMirror.Input;

/// <summary>
/// Transmet le clavier au téléphone : le texte (accents compris) est injecté tel quel, les touches
/// spéciales et Ctrl+lettre comme touches Android. Ctrl+C / Ctrl+X / Ctrl+V synchronisent
/// les presse-papiers, Alt+touche déclenche les raccourcis de <see cref="PhoneShortcuts"/>.
/// </summary>
public sealed class KeyboardForwarder
{
    private readonly Func<ScrcpyControlChannel?> _channel;
    private readonly Dictionary<Key, int> _pressed = new();

    public KeyboardForwarder(Window window, Func<ScrcpyControlChannel?> channel)
    {
        _channel = channel;
        window.PreviewKeyDown += OnKeyDown;
        window.PreviewKeyUp += OnKeyUp;
        window.PreviewTextInput += OnTextInput;
        window.Deactivated += (_, _) => ReleaseAll();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        ScrcpyControlChannel? channel = _channel();
        if (channel == null) return;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;
        bool ctrl = modifiers.HasFlag(ModifierKeys.Control);
        bool alt = modifiers.HasFlag(ModifierKeys.Alt);
        bool shift = modifiers.HasFlag(ModifierKeys.Shift);

        // Ctrl+Alt = AltGr : la touche produit un caractère, transmis par OnTextInput.
        if (alt && !ctrl)
        {
            if (!e.IsRepeat && PhoneShortcuts.TryRun(key, shift, channel)) e.Handled = true;
            return;
        }

        int keycode = AndroidKeys.FromSpecialKey(key);
        if (ctrl && !alt)
        {
            if (TryClipboardShortcut(key, channel))
            {
                e.Handled = true;
                return;
            }
            if (keycode == 0) keycode = AndroidKeys.FromLetter(key); // Ctrl+A, Ctrl+Z…
        }

        if (keycode == 0) return; // caractère : arrive par OnTextInput

        channel.InjectKeycode(KeyAction.Down, keycode, e.IsRepeat ? 1 : 0, AndroidKeys.MetaState(modifiers));
        _pressed[key] = keycode;
        e.Handled = true; // empêche aussi Tab ou les flèches de déplacer le focus
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!_pressed.Remove(key, out int keycode)) return;
        _channel()?.InjectKeycode(KeyAction.Up, keycode, 0, AndroidKeys.MetaState(Keyboard.Modifiers));
        e.Handled = true;
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        ScrcpyControlChannel? channel = _channel();
        if (channel == null) return;

        // Entrée, retour arrière, Ctrl+lettre… produisent des caractères de contrôle déjà envoyés comme touches.
        string text = new(e.Text.Where(c => !char.IsControl(c)).ToArray());
        if (text.Length == 0) return;

        channel.InjectText(text);
        e.Handled = true;
    }

    /// <summary>Ctrl+V colle le presse-papiers du PC ; Ctrl+C / Ctrl+X copient la sélection du téléphone vers le PC.</summary>
    private static bool TryClipboardShortcut(Key key, ScrcpyControlChannel channel)
    {
        switch (key)
        {
            case Key.V:
                string? text = ReadPcClipboard();
                if (!string.IsNullOrEmpty(text)) channel.SetClipboard(text, paste: true);
                return true;
            case Key.C:
                channel.GetClipboard(CopyKey.Copy);
                return true;
            case Key.X:
                channel.GetClipboard(CopyKey.Cut);
                return true;
            default:
                return false;
        }
    }

    private static string? ReadPcClipboard()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (ExternalException)
        {
            return null; // presse-papiers occupé par une autre application
        }
    }

    /// <summary>Relâche les touches encore enfoncées (la fenêtre a perdu le focus).</summary>
    private void ReleaseAll()
    {
        ScrcpyControlChannel? channel = _channel();
        foreach (int keycode in _pressed.Values)
            channel?.InjectKeycode(KeyAction.Up, keycode, 0, 0);
        _pressed.Clear();
    }
}
