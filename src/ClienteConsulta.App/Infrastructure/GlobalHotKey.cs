using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ClienteConsulta.App.Infrastructure;

/// <summary>
/// Registra um atalho de teclado global no Windows (funciona mesmo com outro
/// aplicativo em primeiro plano), usando a janela informada apenas como "dona"
/// nativa da mensagem — a janela pode permanecer oculta o tempo todo.
/// </summary>
public sealed class GlobalHotKey : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly HwndSource _source;
    private readonly int _id;
    private bool _registered;

    public event Action? Pressed;

    public GlobalHotKey(Window window, int id, ModifierKeys modifiers, Key key)
    {
        _id = id;

        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("Janela sem HWND.");
        _source.AddHook(WndProc);

        var modFlags = ToModifierFlags(modifiers);
        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _registered = RegisterHotKey(handle, _id, modFlags, vk);
        if (!_registered)
        {
            throw new InvalidOperationException(
                "Não foi possível registrar o atalho global (Ctrl+Alt+D). " +
                "Outro aplicativo já pode estar usando essa combinação.");
        }
    }

    private static uint ToModifierFlags(ModifierKeys modifiers)
    {
        uint flags = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) flags |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) flags |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) flags |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) flags |= MOD_WIN;
        return flags;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            handled = true;
            Pressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, _id);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
    }
}
