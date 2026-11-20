using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace PhoneMirror.UI;

/// <summary>
/// Affiche une onde à l'endroit d'un clic, pour que les personnes qui regardent l'écran
/// (partage d'écran, démonstration) voient où l'utilisateur a touché le téléphone.
/// Purement visuel, posé au-dessus de l'image : aucun effet sur la vidéo ni sur le contrôle.
/// </summary>
public sealed class ClickRippleLayer(Canvas canvas)
{
    private const double Radius = 30;
    private static readonly Duration WaveDuration = new(TimeSpan.FromMilliseconds(550));
    private static readonly Duration DotDuration = new(TimeSpan.FromMilliseconds(350));
    private static readonly Brush WaveStroke = Frozen(new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush WaveFill = Frozen(new SolidColorBrush(Color.FromArgb(0x50, 0x67, 0xE8, 0xF9)));
    private static readonly Brush DotFill = Frozen(new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)));
    private static readonly Effect Shadow = CreateShadow();

    /// <summary>Onde qui s'élargit (clic gauche, toucher) ; <paramref name="position"/> est relative au calque.</summary>
    public void Show(Point position)
    {
        // Onde : cercle qui s'agrandit en s'effaçant.
        Animate(CreateCircle(position, WaveFill, WaveStroke), from: 0.15, to: 1.0, WaveDuration);
        // Point central plus bref, qui marque l'endroit exact du toucher.
        Animate(CreateCircle(position, DotFill, null, 0.28), from: 1.0, to: 0.8, DotDuration);
    }

    /// <summary>Onde qui se referme vers le point (clic droit, retour) : l'inverse de <see cref="Show"/>.</summary>
    public void ShowInward(Point position)
    {
        var duration = WaveDuration.TimeSpan;

        // Le cercle apparaît grand, se resserre en se renforçant, puis s'éteint au centre.
        Ellipse wave = CreateCircle(position, WaveFill, WaveStroke);
        var shrink = new DoubleAnimation(1.0, 0.15, WaveDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var fade = new DoubleAnimationUsingKeyFrames { Duration = WaveDuration };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration * 0.3)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration * 0.75)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(duration)));
        Run(wave, shrink, fade);

        // Le point central s'allume quand l'onde arrive au centre.
        Ellipse dot = CreateCircle(position, DotFill, null, 0.28);
        dot.Opacity = 0;
        TimeSpan delay = duration * 0.6;
        var pop = new DoubleAnimation(0.6, 1.0, DotDuration) { BeginTime = delay, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var dotFade = new DoubleAnimationUsingKeyFrames { BeginTime = delay, Duration = DotDuration };
        dotFade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        dotFade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(DotDuration.TimeSpan)));
        Run(dot, pop, dotFade);
    }

    private Ellipse CreateCircle(Point center, Brush fill, Brush? stroke, double sizeFactor = 1.0)
    {
        double radius = Radius * sizeFactor;
        var circle = new Ellipse
        {
            Width = 2 * radius,
            Height = 2 * radius,
            Fill = fill,
            Stroke = stroke,
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

    private void Animate(Ellipse circle, double from, double to, Duration duration)
    {
        // Expansion rapide au début, puis fondu tardif : l'onde reste bien visible pendant qu'elle grandit.
        var scale = new DoubleAnimation(from, to, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var fade = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
        Run(circle, scale, fade);
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

    /// <summary>Légère ombre pour que l'onde reste visible sur un fond clair.</summary>
    private static Effect CreateShadow()
    {
        var shadow = new DropShadowEffect
        {
            Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.45,
        };
        shadow.Freeze();
        return shadow;
    }

    private static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
