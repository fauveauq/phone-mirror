using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PhoneMirror.Core;

namespace PhoneMirror.Input;

/// <summary>
/// Transmet la souris au téléphone : clic gauche et glisser = doigt, molette = défilement,
/// clic droit = retour, clic du milieu = accueil, boutons latéraux = applications récentes / notifications.
/// </summary>
public sealed class MouseForwarder
{
    // Pointeur « souris » de scrcpy : sans bouton secondaire, le serveur l'injecte comme un doigt.
    private const long PointerIdMouse = -1;
    private const int ButtonPrimary = 1;
    private const float WheelNotch = 120f;

    // Un cran de molette Android ne fait défiler que ~5 % de l'écran : trop lent pour un téléphone.
    private const float ScrollSpeed = 3f;

    private readonly Image _image;
    private readonly Func<ScrcpyControlChannel?> _channel;
    private bool _touching;
    private VideoPoint _lastPoint;

    public MouseForwarder(Image image, Func<ScrcpyControlChannel?> channel)
    {
        _image = image;
        _channel = channel;
        image.MouseDown += OnMouseDown;
        image.MouseMove += OnMouseMove;
        image.MouseUp += OnMouseUp;
        image.MouseWheel += OnMouseWheel;
        image.LostMouseCapture += (_, _) => EndTouch();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        ScrcpyControlChannel? channel = _channel();
        if (channel == null) return;
        _image.Focus(); // pour recevoir le clavier

        switch (e.ChangedButton)
        {
            case MouseButton.Left when TryGetPoint(e, out VideoPoint point):
                _touching = true;
                _lastPoint = point;
                _image.CaptureMouse();
                channel.InjectTouch(MotionAction.Down, PointerIdMouse, point, 1f, ButtonPrimary, ButtonPrimary);
                break;
            case MouseButton.Right:
                channel.BackOrScreenOn(KeyAction.Down);
                break;
            case MouseButton.Middle:
                channel.PressKey(AndroidKeys.Home);
                break;
            case MouseButton.XButton1:
                channel.PressKey(AndroidKeys.AppSwitch);
                break;
            case MouseButton.XButton2:
                channel.Send(PhoneCommand.ExpandNotificationPanel);
                break;
        }
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_touching || !TryGetPoint(e, out VideoPoint point) || point == _lastPoint) return;
        _lastPoint = point;
        _channel()?.InjectTouch(MotionAction.Move, PointerIdMouse, point, 1f, 0, ButtonPrimary);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _touching)
        {
            if (TryGetPoint(e, out VideoPoint point)) _lastPoint = point;
            _image.ReleaseMouseCapture(); // déclenche EndTouch
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            _channel()?.BackOrScreenOn(KeyAction.Up);
        }
        e.Handled = true;
    }

    /// <summary>Fin du toucher : bouton relâché, ou capture perdue (changement de fenêtre…).</summary>
    private void EndTouch()
    {
        if (!_touching) return;
        _touching = false;
        _channel()?.InjectTouch(MotionAction.Up, PointerIdMouse, _lastPoint, 0f, ButtonPrimary, 0);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrcpyControlChannel? channel = _channel();
        if (channel == null || !TryGetPoint(e, out VideoPoint point)) return;

        float notches = e.Delta / WheelNotch * ScrollSpeed;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            channel.InjectScroll(point, -notches, 0f); // Maj + molette : défilement horizontal
        else
            channel.InjectScroll(point, 0f, notches);
        e.Handled = true;
    }

    /// <summary>Convertit la position de la souris en pixels de la vidéo (bornés à l'image).</summary>
    private bool TryGetPoint(MouseEventArgs e, out VideoPoint point)
    {
        point = default;
        if (_image.Source is not BitmapSource bitmap || _image.ActualWidth <= 0 || _image.ActualHeight <= 0)
            return false;

        Point position = e.GetPosition(_image);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int x = Math.Clamp((int)(position.X / _image.ActualWidth * width), 0, width - 1);
        int y = Math.Clamp((int)(position.Y / _image.ActualHeight * height), 0, height - 1);
        point = new VideoPoint(x, y, width, height);
        return true;
    }
}
