using System.Windows;

namespace PhoneMirror.UI;

/// <summary>Bascule plein écran (F11) : fenêtre sans bordure et maximisée, puis retour à l'état précédent.</summary>
public sealed class FullScreenToggle(Window window)
{
    private WindowStyle _previousStyle;
    private WindowState _previousState;
    private ResizeMode _previousResizeMode;

    public bool IsFullScreen { get; private set; }

    public void Toggle()
    {
        if (IsFullScreen)
        {
            window.WindowStyle = _previousStyle;
            window.ResizeMode = _previousResizeMode;
            window.WindowState = _previousState;
        }
        else
        {
            _previousStyle = window.WindowStyle;
            _previousState = window.WindowState;
            _previousResizeMode = window.ResizeMode;

            // Repasser par Normal : sinon une fenêtre déjà maximisée garderait la barre des tâches visible.
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowState = WindowState.Maximized;
        }
        IsFullScreen = !IsFullScreen;
    }
}
