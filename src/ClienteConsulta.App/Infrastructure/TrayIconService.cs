using System.Drawing;
using System.Windows.Forms;

namespace ClienteConsulta.App.Infrastructure;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startupMenuItem;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService(Icon icon, string hotKeyText)
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Abrir", null, (_, _) => OpenRequested?.Invoke()) { Font = new Font(menu.Font, FontStyle.Bold) };
        var settingsItem = new ToolStripMenuItem("Configurações...", null, (_, _) => SettingsRequested?.Invoke());
        _startupMenuItem = new ToolStripMenuItem("Iniciar com o Windows", null, OnToggleStartup) { CheckOnClick = false };
        var exitItem = new ToolStripMenuItem("Sair", null, (_, _) => ExitRequested?.Invoke());

        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(_startupMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        menu.Opening += (_, _) => _startupMenuItem.Checked = StartupRegistration.IsEnabled();

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = $"{AppInfo.DisplayName}  •  {hotKeyText}",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled());
    }

    public void ShowBalloon(string title, string message)
        => _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);

    public void UpdateTooltip(string hotKeyText)
        => _notifyIcon.Text = $"{AppInfo.DisplayName}  •  {hotKeyText}";

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
