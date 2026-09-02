using System.Reflection;

namespace ClienteConsulta.App.Infrastructure;

/// <summary>
/// Nome de exibição do app — fonte única para títulos de janela, MessageBox, balões da bandeja
/// e tooltip. Lido de <see cref="AssemblyProductAttribute"/> (definido em &lt;Product&gt; no
/// .csproj) para não ficar espalhado em strings literais.
/// </summary>
public static class AppInfo
{
    private const string Fallback = "Consulta Rápida";

    public static string DisplayName { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product is { Length: > 0 } product
            ? product
            : Fallback;

    /// <summary>Versão "Major.Minor.Build" (sem revision), ou string vazia se indisponível.</summary>
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : string.Empty;
}
