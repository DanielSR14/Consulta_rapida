using System.Windows;
using System.Windows.Media;

namespace ClienteConsulta.App.Infrastructure.Branding;

/// <summary>
/// Aplica a cor principal escolhida pelo usuário (Configurações → Aparência).
///
/// O WPF <b>congela</b> (<c>Freeze</c>) os <see cref="SolidColorBrush"/> definidos num
/// <see cref="ResourceDictionary"/> compilado assim que são lidos, então não dá para mutar
/// <c>brush.Color</c> em runtime. Em vez disso, este método <b>substitui</b> as entradas dos
/// brushes derivados da cor principal em <see cref="Application.Resources"/>. Para a troca
/// aparecer na hora (inclusive em janelas já abertas), toda a UI referencia esses brushes por
/// <c>DynamicResource</c> — ver <see cref="AccentBrushKeys"/> e a nota em Theme.xaml.
/// </summary>
public static class ThemeManager
{
    public const string DefaultAccentHex = "#2563EB";

    /// <summary>
    /// Chaves de brush recalculadas a partir da cor principal. Todo <c>{DynamicResource ...}</c>
    /// dessas chaves na UI depende de <see cref="ApplyAccent"/> ser chamado; qualquer uso com
    /// <c>{StaticResource}</c> dessas chaves <b>não</b> vai atualizar em runtime.
    /// </summary>
    public static readonly IReadOnlyList<string> AccentBrushKeys = new[]
    {
        "PrimaryBrush", "PrimaryDarkBrush", "PrimaryLightBrush",
        "PrimarySoftBrush", "SelectedBrush", "TextOnPrimaryBrush",
    };

    /// <summary>Presets oferecidos na tela de Configurações (rótulo + hex).</summary>
    public static readonly IReadOnlyList<(string Name, string Hex)> Presets = new[]
    {
        ("Azul", "#2563EB"),
        ("Índigo", "#4F46E5"),
        ("Verde", "#15803D"),
        ("Teal", "#0D9488"),
        ("Grafite", "#334155"),
        ("Vinho", "#8A1627"),
        ("Laranja", "#C2410C"),
    };

    public static string CurrentAccentHex { get; private set; } = DefaultAccentHex;

    /// <summary>A cor principal em vigor (já resolvida — nunca nula).</summary>
    public static Color CurrentAccentColor { get; private set; } =
        TryParse(DefaultAccentHex, out var c) ? c : Colors.RoyalBlue;

    /// <summary>Disparado ao fim de <see cref="ApplyAccent"/>. A logo padrão (desenhada em código)
    /// escuta isso para se redesenhar na cor nova — ver <see cref="BrandingProvider"/>.</summary>
    public static event Action? AccentChanged;

    /// <summary>Devolve o hex normalizado (#RRGGBB) se válido, senão <c>null</c>.</summary>
    public static string? NormalizeHex(string? hex)
    {
        if (TryParse(hex, out var color))
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        return null;
    }

    /// <summary>Aplica <paramref name="hex"/> (ou o padrão, se nulo/ inválido) à paleta em runtime.</summary>
    public static void ApplyAccent(string? hex)
    {
        var accent = TryParse(hex, out var parsed) ? parsed : TryParse(DefaultAccentHex, out var d) ? d : Colors.RoyalBlue;
        CurrentAccentColor = accent;
        CurrentAccentHex = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";

        var resources = Application.Current?.Resources;
        if (resources is not null)
        {
            SetBrush(resources, "PrimaryBrush", accent);
            SetBrush(resources, "PrimaryDarkBrush", Adjust(accent, lightnessDelta: -0.10));
            SetBrush(resources, "PrimaryLightBrush", Adjust(accent, lightnessDelta: +0.16));
            SetBrush(resources, "PrimarySoftBrush", MixWithWhite(accent, whiteAmount: 0.90));
            SetBrush(resources, "SelectedBrush", MixWithWhite(accent, whiteAmount: 0.88));
            SetBrush(resources, "TextOnPrimaryBrush", Luminance(accent) > 0.55 ? Color.FromRgb(0x1A, 0x1D, 0x21) : Colors.White);
        }

        AccentChanged?.Invoke();
    }

    /// <summary>
    /// Substitui a entrada do brush em <see cref="Application.Resources"/>. Não muta o brush
    /// existente (o WPF o congela), então a UI precisa referenciá-lo por <c>DynamicResource</c>
    /// para pegar a troca.
    /// </summary>
    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var text = hex.Trim();
        if (!text.StartsWith('#'))
            text = "#" + text;

        try
        {
            if (ColorConverter.ConvertFromString(text) is Color c)
            {
                color = c;
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }

    // --- Manipulação de cor via HSL -------------------------------------------------------------

    private static Color Adjust(Color color, double lightnessDelta)
    {
        var (h, s, l) = ToHsl(color);
        return FromHsl(h, s, Math.Clamp(l + lightnessDelta, 0, 1));
    }

    private static Color MixWithWhite(Color color, double whiteAmount)
    {
        whiteAmount = Math.Clamp(whiteAmount, 0, 1);
        byte Mix(byte c) => (byte)Math.Round(c + (255 - c) * whiteAmount);
        return Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
    }

    /// <summary>Luminância relativa perceptual (0 = preto, 1 = branco).</summary>
    private static double Luminance(Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static (double H, double S, double L) ToHsl(Color color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double h = 0, s, l = (max + min) / 2.0;
        double delta = max - min;

        if (delta == 0)
        {
            s = 0;
        }
        else
        {
            s = l < 0.5 ? delta / (max + min) : delta / (2.0 - max - min);
            if (max == r) h = (g - b) / delta + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / delta + 2;
            else h = (r - g) / delta + 4;
            h /= 6.0;
        }

        return (h, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        return Color.FromRgb(ToByte(r), ToByte(g), ToByte(b));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v * 255.0), 0, 255);
}
