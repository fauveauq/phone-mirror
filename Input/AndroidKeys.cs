using System.Windows.Input;

namespace PhoneMirror.Input;

/// <summary>Codes de touches et d'états Android (KeyEvent), et correspondance avec les touches WPF.</summary>
public static class AndroidKeys
{
    public const int Home = 3;
    public const int Back = 4;
    public const int VolumeUp = 24;
    public const int VolumeDown = 25;
    public const int Power = 26;
    public const int Menu = 82;
    public const int AppSwitch = 187;

    private const int LetterA = 29;

    public const int MetaShift = 0x01 | 0x40; // META_SHIFT_ON | META_SHIFT_LEFT_ON
    public const int MetaAlt = 0x02 | 0x10;   // META_ALT_ON | META_ALT_LEFT_ON
    public const int MetaCtrl = 0x1000 | 0x2000; // META_CTRL_ON | META_CTRL_LEFT_ON

    // Touches sans caractère : les caractères passent par l'injection de texte (accents compris).
    private static readonly Dictionary<Key, int> SpecialKeys = new()
    {
        [Key.Enter] = 66,      // KEYCODE_ENTER
        [Key.Back] = 67,       // KEYCODE_DEL (retour arrière)
        [Key.Delete] = 112,    // KEYCODE_FORWARD_DEL
        [Key.Tab] = 61,
        [Key.Escape] = 111,
        [Key.Up] = 19,
        [Key.Down] = 20,
        [Key.Left] = 21,
        [Key.Right] = 22,
        [Key.Home] = 122,      // KEYCODE_MOVE_HOME
        [Key.End] = 123,       // KEYCODE_MOVE_END
        [Key.PageUp] = 92,
        [Key.PageDown] = 93,
    };

    /// <summary>Code Android d'une touche spéciale, ou 0 si la touche produit du texte ou n'est pas gérée.</summary>
    public static int FromSpecialKey(Key key) => SpecialKeys.GetValueOrDefault(key);

    /// <summary>Code Android d'une lettre (pour Ctrl+lettre), ou 0.</summary>
    public static int FromLetter(Key key) => key is >= Key.A and <= Key.Z ? LetterA + (key - Key.A) : 0;

    public static int MetaState(ModifierKeys modifiers) =>
        (modifiers.HasFlag(ModifierKeys.Shift) ? MetaShift : 0)
        | (modifiers.HasFlag(ModifierKeys.Alt) ? MetaAlt : 0)
        | (modifiers.HasFlag(ModifierKeys.Control) ? MetaCtrl : 0);
}
