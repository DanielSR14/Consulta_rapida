using System.Threading;
using System.Windows;

namespace ClienteConsulta.App.Infrastructure;

/// <summary>
/// A área de transferência do Windows às vezes está momentaneamente travada por
/// outro processo; tentamos algumas vezes antes de desistir.
/// </summary>
public static class ClipboardService
{
    public static bool TrySetText(string text)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(30);
            }
        }

        return false;
    }
}
