using Microsoft.Win32;

namespace ClienteConsulta.App.Infrastructure;

/// <summary>Liga/desliga a inicialização automática com o Windows via chave Run do usuário atual (não requer administrador).</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ConsultaRapida";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string existing
               && string.Equals(existing.Trim('"'), ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
            key.SetValue(ValueName, $"\"{ExecutablePath}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string ExecutablePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável.");
}
