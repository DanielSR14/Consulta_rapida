using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClienteConsulta.App.Infrastructure;
using ClienteConsulta.App.Infrastructure.Branding;
using ClienteConsulta.Core.Models;
using Microsoft.Win32;

namespace ClienteConsulta.App.Views;

public partial class SettingsWindow : Window
{
    private readonly Func<Customer, Task> _registerAction;
    private readonly Func<IReadOnlyList<Customer>, Task> _replaceAllAction;
    private readonly ModifierKeys _originalModifiers;
    private readonly Key _originalKey;
    private ModifierKeys? _pendingModifiers;
    private Key? _pendingKey;
    private int _customerCount;

    private string? _pendingAccentColor;
    private string? _pendingLogoPath;
    private bool _initializing = true;

    public ModifierKeys? ChosenHotKeyModifiers { get; private set; }
    public Key? ChosenHotKeyKey { get; private set; }

    /// <summary>Hex normalizado da cor principal, ou <c>null</c> para a cor padrão embutida.</summary>
    public string? ChosenAccentColor { get; private set; }

    /// <summary>Caminho da logo personalizada, ou <c>null</c> para a logo padrão.</summary>
    public string? ChosenLogoPath { get; private set; }

    public SettingsWindow(string dbPath, int customerCount, ModifierKeys currentModifiers, Key currentKey,
        string? currentAccentColor, string? currentLogoPath,
        Func<Customer, Task> registerAction, Func<IReadOnlyList<Customer>, Task> replaceAllAction)
    {
        InitializeComponent();
        _registerAction = registerAction;
        _replaceAllAction = replaceAllAction;

        AppNameText.Text = AppInfo.Version.Length == 0 ? AppInfo.DisplayName : $"{AppInfo.DisplayName} · v{AppInfo.Version}";
        DbPathText.Text = dbPath;
        UpdateCustomerCount(customerCount);

        _originalModifiers = currentModifiers;
        _originalKey = currentKey;
        _pendingModifiers = currentModifiers;
        _pendingKey = currentKey;
        HotKeyBox.Text = HotKeyDisplay.Format(currentModifiers, currentKey);

        // --- Aparência ---
        _pendingAccentColor = ThemeManager.NormalizeHex(currentAccentColor);
        _pendingLogoPath = currentLogoPath;

        PresetSwatches.ItemsSource = ThemeManager.Presets
            .Select(p => new AccentPreset(p.Name, p.Hex, new SolidColorBrush(ParseColor(p.Hex))))
            .ToList();

        AccentHexBox.Text = _pendingAccentColor ?? ThemeManager.CurrentAccentHex;
        LogoPreview.Source = BrandingProvider.CurrentLogo;

        _initializing = false;
    }

    private static Color ParseColor(string hex) =>
        ColorConverter.ConvertFromString(hex) is Color c ? c : Colors.Gray;

    /// <summary>Item da faixa de cores predefinidas (tipo público para o binding do WPF).</summary>
    public sealed record AccentPreset(string Name, string Hex, Brush Brush);

    private void UpdateCustomerCount(int count)
    {
        _customerCount = count;
        CustomerCountText.Text = count switch
        {
            0 => "Nenhuma empresa cadastrada ainda",
            1 => "1 empresa cadastrada",
            _ => $"{count} empresas cadastradas"
        };
    }

    private void RegisterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new NewCustomerWindow(_registerAction) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        UpdateCustomerCount(_customerCount + 1);
        ShowStatus("Empresa cadastrada com sucesso.", isError: false);
    }

    private void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ImportCustomersWindow(_replaceAllAction) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        UpdateCustomerCount(dialog.ImportedCount);
        ShowStatus("Base de clientes atualizada com sucesso.", isError: false);
    }

    // ===================== Aparência =====================

    private void AccentHexBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_initializing)
            return;

        var normalized = ThemeManager.NormalizeHex(AccentHexBox.Text);
        if (normalized is null)
        {
            ShowAppearanceStatus("Cor inválida — use um hex como #2563EB.");
            return;
        }

        ShowAppearanceStatus("");
        _pendingAccentColor = normalized;
        ThemeManager.ApplyAccent(normalized);         // preview ao vivo da paleta
        LogoPreview.Source = BrandingProvider.CurrentLogo; // e da logo padrão, se for a que está em uso
    }

    private void PresetSwatch_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hex })
            AccentHexBox.Text = hex; // dispara AccentHexBox_OnTextChanged
    }

    private void ChooseLogoButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar logo",
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _pendingLogoPath = BrandingProvider.SetCustomLogo(dialog.FileName);
            LogoPreview.Source = BrandingProvider.CurrentLogo;
            ShowAppearanceStatus("");
        }
        catch (InvalidOperationException ex)
        {
            ShowAppearanceStatus(ex.Message);
        }
    }

    private void DefaultLogoButton_OnClick(object sender, RoutedEventArgs e)
    {
        BrandingProvider.ClearCustomLogo();
        _pendingLogoPath = null;
        LogoPreview.Source = BrandingProvider.CurrentLogo;
        ShowAppearanceStatus("");
    }

    // ===================== Atalho =====================

    private void HotKeyBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        HotKeyBox.Text = "Pressione a nova combinação...";
        HotKeyBox.Foreground = (Brush)FindResource("TextMutedBrush");
    }

    private void HotKeyBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        RestoreHotKeyBoxDisplay();
    }

    private void HotKeyBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System)
            return;

        if (key == Key.Escape)
        {
            RestoreHotKeyBoxDisplay();
            Keyboard.ClearFocus();
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            HotKeyBox.Text = "Use Ctrl, Alt ou Shift junto com a tecla";
            HotKeyBox.Foreground = (Brush)FindResource("PrimaryBrush");
            return;
        }

        _pendingModifiers = modifiers;
        _pendingKey = key;
        HotKeyBox.Foreground = (Brush)FindResource("TextPrimaryBrush");
        HotKeyBox.Text = HotKeyDisplay.Format(modifiers, key);
    }

    private void ResetHotKeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        _pendingModifiers = AppSettings.DefaultHotKeyModifiers;
        _pendingKey = AppSettings.DefaultHotKeyKey;
        RestoreHotKeyBoxDisplay();
    }

    private void RestoreHotKeyBoxDisplay()
    {
        var modifiers = _pendingModifiers ?? _originalModifiers;
        var key = _pendingKey ?? _originalKey;
        HotKeyBox.Text = HotKeyDisplay.Format(modifiers, key);
        HotKeyBox.Foreground = (Brush)FindResource("TextPrimaryBrush");
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_pendingModifiers is null || _pendingKey is null)
        {
            ShowStatus("Defina um atalho global válido.", isError: true);
            return;
        }

        ChosenHotKeyModifiers = _pendingModifiers;
        ChosenHotKeyKey = _pendingKey;
        ChosenAccentColor = _pendingAccentColor;
        ChosenLogoPath = _pendingLogoPath;

        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        // O preview ao vivo de aparência é revertido pelo chamador (App.OpenSettings).
        DialogResult = false;
    }

    private void ShowStatus(string message, bool isError)
    {
        RegisterStatusText.Text = message;
        RegisterStatusText.Foreground = (Brush)FindResource(isError ? "PrimaryBrush" : "TextMutedBrush");
    }

    private void ShowAppearanceStatus(string message)
    {
        AppearanceStatusText.Text = message;
    }
}
