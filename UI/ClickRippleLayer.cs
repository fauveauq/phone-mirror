using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using PhoneMirror.Core;

namespace PhoneMirror.UI;

/// <summary>Apparence des indicateurs de clic, issue des réglages.</summary>
public readonly record struct ClickAppearance(ClickColor Hue, ClickSize Size, ClickStyle Style)
{
    public static ClickAppearance From(UserSettings settings) =>
        new(settings.ClickColor, settings.ClickSize, settings.ClickStyle);

    public bool ShowWave => Style != ClickStyle.PointSeul;

    public bool ShowDot => Style != ClickStyle.OndeSeule;

    public Color ToColor() => ToColor(Hue);

    public static Color ToColor(ClickColor color) => color switch
    {
        ClickColor.Rouge => Color.FromRgb(0xEF, 0x44, 0x44),
        ClickColor.Orange => Color.FromRgb(0xF9, 0x73, 0x16),
        ClickColor.Jaune => Color.FromRgb(0xFA, 0xCC, 0x15),
        ClickColor.Vert => Color.FromRgb(0x22, 0xC5, 0x5E),
        ClickColor.Violet => Color.FromRgb(0xA8, 0x55, 0xF7),
        ClickColor.Blanc => Color.FromRgb(0xFF, 0xFF, 0xFF),
        ClickColor.Gris => Color.FromRgb(0x9C, 0xA3, 0xAF),
        _ => Color.FromRgb(0x22, 0xD3, 0xEE), // turquoise
    };

    /// <summary>Rayon de l'onde, en unités WPF.</summary>
    public double Radius => Size switch
    {
        ClickSize.Petite => 20,
        ClickSize.Grande => 44,
        _ => 30,
    };
}

/// <summary>
/// Indicateurs de clic, pour que les personnes qui regardent l'écran (partage d'écran, démonstration)
/// voient où l'utilisateur touche le téléphone : onde qui s'élargit pour un toucher (clic gauche),
/// qui se referme pour un retour (clic droit), ou simple point.
/// Purement visuel, posé au-dessus de l'image : aucun effet sur la vidéo ni sur le contrôle.
/// </summary>
public sealed class ClickRippleLayer(Canvas canvas)
{
    private const double DotFactor = 0.28;     // rayon du point par rapport à l'onde
    private const double DotOnlyFactor = 0.36; // point un peu plus grand quand il est seul
    private static readonly Duration WaveDuration = new(TimeSpan.FromMilliseconds(550));
    private static readonly Duration DotDuration = new(TimeSpan.FromMilliseconds(350));
    private static readonly Effect Shadow = CreateShadow();

    /// <summary>Toucher (clic gauche) ; <paramref name="position"/> est relative au calque.</summary>
    public void Show(Point position, ClickAppearance look)
    {
        if (!look.ShowWave)
        {
            AnimateDot(position, look, DotOnlyFactor, from: 1.0, to: 0.8, TimeSpan.Zero);
            return;
        }

        // Onde : cercle qui s'agrandit en s'effaçant, et point bref à l'endroit exact du toucher.
        Ellipse wave = CreateCircle(position, look, look.Radius, isDot: false);
        var grow = new DoubleAnimation(0.15, 1.0, WaveDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var fade = new DoubleAnimation(1.0, 0.0, WaveDuration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        Run(wave, grow, fade);
        if (look.ShowDot)
            AnimateDot(position, look, DotFactor, from: 1.0, to: 0.8, TimeSpan.Zero);
    }

    /// <summary>Retour (clic droit) : l'onde se referme vers le point, l'inverse de <see cref="Show"/>.</summary>
    public void ShowInward(Point position, ClickAppearance look)
    {
        if (!look.ShowWave)
        {
            // Point qui se contracte au lieu de grossir.
            AnimateDot(position, look, DotOnlyFactor, from: 1.3, to: 0.6, TimeSpan.Zero);
            return;
        }

        TimeSpan duration = WaveDuration.TimeSpan;

        // Le cercle apparaît grand, se resserre en se renforçant, puis s'éteint au centre.
        Ellipse wave = CreateCircle(position, look, look.Radius, isDot: false);
        var shrink = new DoubleAnimation(1.0, 0.15, WaveDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var fade = new DoubleAnimationUsingKeyFrames { Duration = WaveDuration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration * 0.3)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration * 0.75)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(duration)));
        Run(wave, shrink, fade);

        // Le point central s'allume quand l'onde arrive au centre.
        if (look.ShowDot)
            AnimateDot(position, look, DotFactor, from: 0.6, to: 1.0, duration * 0.6);
    }

    /// <summary>Point central : change d'échelle et s'efface, éventuellement après un délai.</summary>
    private void AnimateDot(Point position, ClickAppearance look, double factor, double from, double to, TimeSpan delay)
    {
        Ellipse dot = CreateCircle(position, look, look.Radius * factor, isDot: true);
        dot.Opacity = delay > TimeSpan.Zero ? 0 : 1; // invisible jusqu'au début de son animation

        var scale = new DoubleAnimation(from, to, DotDuration) { BeginTime = delay, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = DotDuration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(DotDuration.TimeSpan)));
        Run(dot, scale, fade);
    }

    private Ellipse CreateCircle(Point center, ClickAppearance look, double radius, bool isDot)
    {
        Color color = look.ToColor();
        var circle = new Ellipse
        {
            Width = 2 * radius,
            Height = 2 * radius,
            // Onde : contour franc et intérieur transparent ; point : presque opaque.
            Fill = Frozen(Color.FromArgb(isDot ? (byte)0xD0 : (byte)0x45, color.R, color.G, color.B)),
            Stroke = isDot ? null : Frozen(Color.FromArgb(0xF0, color.R, color.G, color.B)),
            StrokeThickness = 2.5,
            Effect = Shadow,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(circle, center.X - radius);
        Canvas.SetTop(circle, center.Y - radius);
        canvas.Children.Add(circle);
        return circle;
    }

    /// <summary>Applique l'échelle et l'opacité ; le cercle est retiré à la fin du fondu.</summary>
    private void Run(Ellipse circle, AnimationTimeline scale, AnimationTimeline fade)
    {
        fade.Completed += (_, _) => canvas.Children.Remove(circle);

        var transform = (ScaleTransform)circle.RenderTransform;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        circle.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Légère ombre pour que l'indicateur reste visible sur un fond de même couleur.</summary>
    private static Effect CreateShadow()
    {
        var shadow = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.45 };
        shadow.Freeze();
        return shadow;
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
