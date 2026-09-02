using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClienteConsulta.App.Infrastructure.Branding;

/// <summary>
/// Fornece a logo exibida no cabeçalho da janela de busca: uma imagem personalizada escolhida
/// pelo usuário (Configurações → Aparência), ou a logo padrão (um badge com lupa, desenhada em
/// código) quando não há nenhuma.
///
/// A logo padrão é desenhada com a cor principal do tema — um <c>DynamicResource</c> dentro de
/// um <see cref="DrawingImage"/> não atualiza de forma confiável em runtime, então ela é
/// redesenhada em C# sempre que a cor muda (<see cref="ThemeManager.AccentChanged"/>).
///
/// A janela de busca é persistente (criada uma vez), então quem a exibe assina
/// <see cref="LogoChanged"/> para reatribuir <see cref="CurrentLogo"/> quando algo muda.
/// </summary>
public static class BrandingProvider
{
    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

    /// <summary>Nome base do arquivo de logo copiado para <see cref="AppSettings.SettingsDirectory"/>.</summary>
    private const string LogoFileStem = "logo";

    public static ImageSource CurrentLogo { get; private set; } = BuildDefaultLogo();

    /// <summary>Verdadeiro quando há uma logo personalizada em uso (o cabeçalho esconde o nome do app nesse caso).</summary>
    public static bool HasCustomLogo { get; private set; }

    public static event EventHandler? LogoChanged;

    static BrandingProvider()
    {
        // Quando a cor principal muda e estamos com a logo padrão, redesenha na cor nova.
        ThemeManager.AccentChanged += () =>
        {
            if (HasCustomLogo)
                return;
            CurrentLogo = BuildDefaultLogo();
            LogoChanged?.Invoke(null, EventArgs.Empty);
        };
    }

    /// <summary>Chamado no startup, depois de carregar as configurações.</summary>
    public static void Initialize(string? logoPath)
    {
        CurrentLogo = Resolve(logoPath);
    }

    /// <summary>
    /// Copia <paramref name="sourceFilePath"/> para %AppData%\ConsultaRapida\logo&lt;ext&gt;,
    /// atualiza <see cref="CurrentLogo"/> e devolve o caminho salvo (para gravar em
    /// <see cref="AppSettings.LogoPath"/>). Lança <see cref="InvalidOperationException"/> se o
    /// arquivo não for uma imagem suportada/legível.
    /// </summary>
    public static string SetCustomLogo(string sourceFilePath)
    {
        var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException("Formato não suportado. Use PNG, JPG ou BMP.");

        ImageSource image;
        try
        {
            image = LoadBitmap(sourceFilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Não foi possível ler a imagem selecionada.", ex);
        }

        Directory.CreateDirectory(AppSettings.SettingsDirectory);

        // Remove qualquer logo anterior (extensão pode ter mudado).
        foreach (var stale in AllowedExtensions.Select(e => Path.Combine(AppSettings.SettingsDirectory, LogoFileStem + e)))
        {
            if (File.Exists(stale))
                File.Delete(stale);
        }

        var destination = Path.Combine(AppSettings.SettingsDirectory, LogoFileStem + ext);
        File.Copy(sourceFilePath, destination, overwrite: true);

        CurrentLogo = image;
        HasCustomLogo = true;
        LogoChanged?.Invoke(null, EventArgs.Empty);
        return destination;
    }

    /// <summary>Volta para a logo padrão embutida e apaga a cópia personalizada.</summary>
    public static void ClearCustomLogo()
    {
        foreach (var stale in AllowedExtensions.Select(e => Path.Combine(AppSettings.SettingsDirectory, LogoFileStem + e)))
        {
            if (File.Exists(stale))
                File.Delete(stale);
        }

        CurrentLogo = BuildDefaultLogo();
        HasCustomLogo = false;
        LogoChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Reaplica a partir de um caminho salvo (usado ao cancelar a tela de Configurações).</summary>
    public static void RevertTo(string? logoPath)
    {
        CurrentLogo = Resolve(logoPath);
        LogoChanged?.Invoke(null, EventArgs.Empty);
    }

    private static ImageSource Resolve(string? logoPath)
    {
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
        {
            try
            {
                var bitmap = LoadBitmap(logoPath);
                HasCustomLogo = true;
                return bitmap;
            }
            catch
            {
                // arquivo corrompido/ilegível: cai para o padrão
            }
        }

        HasCustomLogo = false;
        return BuildDefaultLogo();
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // não segura o arquivo aberto
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Desenha a logo padrão (badge quadrado com lupa e três linhas de velocidade) na cor
    /// principal atual do tema. Mesma forma que <c>tools/generate-branding-assets.ps1</c> usa
    /// para o ícone do <c>.exe</c>.
    /// </summary>
    private static DrawingImage BuildDefaultLogo()
    {
        var accent = ThemeManager.CurrentAccentColor;
        var group = new DrawingGroup();

        // Badge
        group.Children.Add(new GeometryDrawing(
            new SolidColorBrush(accent), null,
            new RectangleGeometry(new Rect(0, 0, 48, 48), 12, 12)));

        // Linhas de velocidade
        var speed = new GeometryGroup();
        speed.Children.Add(new LineGeometry(new Point(7, 17), new Point(17, 17)));
        speed.Children.Add(new LineGeometry(new Point(5, 24), new Point(15, 24)));
        speed.Children.Add(new LineGeometry(new Point(7, 31), new Point(17, 31)));
        group.Children.Add(new GeometryDrawing(null, RoundPen(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF), 3), speed));

        // Lupa
        var mag = new GeometryGroup();
        mag.Children.Add(new EllipseGeometry(new Point(26, 22), 9, 9));
        mag.Children.Add(new LineGeometry(new Point(32.5, 28.5), new Point(40, 36)));
        group.Children.Add(new GeometryDrawing(null, RoundPen(Colors.White, 4.5), mag));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static Pen RoundPen(Color color, double thickness) => new(new SolidColorBrush(color), thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    };
}
