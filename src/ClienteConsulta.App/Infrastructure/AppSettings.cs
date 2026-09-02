using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace ClienteConsulta.App.Infrastructure;

public sealed class AppSettings
{
    public static readonly ModifierKeys DefaultHotKeyModifiers = ModifierKeys.Control | ModifierKeys.Alt;
    public const Key DefaultHotKeyKey = Key.D;

    public string? DatabasePath { get; set; }
    public string? HotKeyModifiers { get; set; }
    public string? HotKeyKey { get; set; }

    /// <summary>Cor principal do tema, em hex (#RRGGBB). Nulo = cor padrão embutida (ver <see cref="Branding.ThemeManager"/>).</summary>
    public string? AccentColor { get; set; }

    /// <summary>Caminho absoluto de uma logo personalizada (dentro de <see cref="SettingsDirectory"/>).
    /// Nulo = logo padrão vetorial embutida (ver <see cref="Branding.BrandingProvider"/>).</summary>
    public string? LogoPath { get; set; }

    /// <summary>
    /// Pasta de configuração/dados do app em %AppData%. Nome próprio ("ConsultaRapida") para não
    /// colidir com outros forks/derivados que compartilhem os mesmos namespaces internos.
    /// </summary>
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConsultaRapida");

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    /// <summary>
    /// Cópia gravável do banco, usada em runtime. Fica em %AppData%, não na pasta de instalação
    /// (Program Files) — o instalador roda elevado, mas o app depois roda com o usuário comum,
    /// que não tem permissão de escrita em Program Files.
    /// </summary>
    public static string DefaultDatabasePath => Path.Combine(SettingsDirectory, "clientes.db");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch
        {
            // Configuração corrompida ou ilegível: seguimos com os valores padrão.
        }

        return new AppSettings { DatabasePath = DefaultDatabasePath };
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }

    public string ResolveDatabasePath() => DatabasePath ?? DefaultDatabasePath;

    public (ModifierKeys Modifiers, Key Key) ResolveHotKey()
    {
        try
        {
            if (HotKeyModifiers is { Length: > 0 } modifiersText && HotKeyKey is { Length: > 0 } keyText)
            {
                var modifiers = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), modifiersText);
                var key = (Key)Enum.Parse(typeof(Key), keyText);
                return (modifiers, key);
            }
        }
        catch
        {
            // Valor salvo inválido: cai para o padrão abaixo.
        }

        return (DefaultHotKeyModifiers, DefaultHotKeyKey);
    }

    public void SetHotKey(ModifierKeys modifiers, Key key)
    {
        HotKeyModifiers = modifiers.ToString();
        HotKeyKey = key.ToString();
    }
}
