using System.Text.RegularExpressions;

namespace PhoneMirror.Core;

/// <summary>Taille et densité effectives de l'écran du téléphone, pour créer un écran virtuel identique.</summary>
public readonly record struct DisplayGeometry(int Width, int Height, int Density)
{
    /// <summary>Format attendu par l'option new_display du serveur scrcpy.</summary>
    public override string ToString() => $"{Width}x{Height}/{Density}";

    /// <summary>
    /// Lit les sorties de « wm size » et « wm density ». Les valeurs « Override » priment.
    /// Si seule la taille est réduite (mode FHD+ des Samsung), la densité est réduite dans la même proportion.
    /// </summary>
    internal static DisplayGeometry? Parse(string sizeOutput, string densityOutput)
    {
        (int W, int H)? physical = ReadSize(sizeOutput, "Physical");
        (int W, int H)? size = ReadSize(sizeOutput, "Override") ?? physical;
        int? physicalDensity = ReadNumber(densityOutput, "Physical");
        int? density = ReadNumber(densityOutput, "Override");

        if (size is not { } s || (density ?? physicalDensity) is not { } d)
            return null;

        if (density == null && physical is { } p && p.W > 0 && p.W != s.W)
            d = (int)Math.Round(d * (double)s.W / p.W);

        return new DisplayGeometry(s.W, s.H, d);
    }

    private static (int, int)? ReadSize(string output, string label)
    {
        Match m = Regex.Match(output, label + @" size:\s*(\d+)x(\d+)");
        return m.Success ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : null;
    }

    private static int? ReadNumber(string output, string label)
    {
        Match m = Regex.Match(output, label + @" density:\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }
}
