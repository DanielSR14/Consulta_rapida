using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClienteConsulta.App.Infrastructure;
using ClienteConsulta.App.Infrastructure.Branding;
using ClienteConsulta.App.ViewModels;

namespace ClienteConsulta.App.Views;

public partial class SearchWindow : Window
{
    public SearchViewModel ViewModel { get; }

    /// <summary>Só a rotina de encerramento da aplicação pode realmente fechar esta janela; do contrário Esc/Alt+F4 apenas a escondem.</summary>
    public bool AllowClose { get; set; }

    public SearchWindow(SearchViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();

        ApplyBranding();
        BrandingProvider.LogoChanged += (_, _) => Dispatcher.Invoke(ApplyBranding);

        ViewModel.SearchBoxFocusRequested += (_, _) => FocusSearchBox();
        ViewModel.DetailFocusRequested += (_, _) => FocusDetailList();
        ViewModel.FullDetailFocusRequested += (_, _) => FocusFullDetailList();
        ViewModel.CloseRequested += (_, _) => Hide();
    }

    private void ApplyBranding()
    {
        BrandLogo.Source = BrandingProvider.CurrentLogo;
        BrandName.Text = AppInfo.DisplayName;
        BrandName.Visibility = BrandingProvider.HasCustomLogo ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Centraliza a janela no monitor onde está o cursor e a exibe em primeiro plano com foco na pesquisa.</summary>
    public void ShowNearCursor()
    {
        ViewModel.PrepareForShow();
        PositionOnCurrentScreen();

        Show();
        Activate();
        FocusSearchBox();
        AnimateOpen();
    }

    /// <summary>
    /// Efeito estilo macOS: a janela "se estica" verticalmente a partir do topo até o tamanho
    /// final, com uma leve sobra elástica no final (em vez de um fade/scale uniforme, que lia
    /// como um "piscar").
    /// </summary>
    private void AnimateOpen()
    {
        var stretchEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
        var settleEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.32, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = stretchEase });
        CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = settleEase });

        var fastFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(90));
        CardBorder.BeginAnimation(OpacityProperty, fastFade);
    }

    /// <summary>Margem estética entre o topo da área útil da tela e o topo da janela, em DIP.</summary>
    private const double TopMarginDip = 88;

    /// <summary>
    /// Centraliza horizontalmente e ancora no topo (com margem) do monitor onde está o cursor,
    /// em pixels FÍSICOS via SetWindowPos — não pelas propriedades Left/Top (DIP) do WPF.
    /// </summary>
    /// <remarks>
    /// O app é PerMonitorV2 DPI-aware (ver ApplicationHighDpiMode no .csproj). Misturar
    /// <see cref="System.Windows.Forms.Screen.WorkingArea"/> (pixels físicos) com Left/Top do WPF
    /// (DIP) deixava a janela deslocada em qualquer monitor com escala ≠ 100% — o Left/Top do WPF
    /// só é convertido corretamente para físico usando o DPI ATUAL da janela (de onde ela já
    /// está), não o do monitor de destino, então mover para um monitor com DPI diferente sempre
    /// calculava errado. SetWindowPos evita essa conversão por completo: tanto os valores de
    /// SetWindowPos quanto os do Screen.WorkingArea já estão em pixels físicos.
    /// </remarks>
    private void PositionOnCurrentScreen()
    {
        var cursorPosition = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(cursorPosition);
        var area = screen.WorkingArea;

        var scale = GetDpiScaleForPoint(cursorPosition);
        var widthPhysical = Width * scale;
        var topMarginPhysical = TopMarginDip * scale;

        var x = (int)Math.Round(area.Left + (area.Width - widthPhysical) / 2.0);
        var y = (int)Math.Round(area.Top + topMarginPhysical);

        // Garante que o HWND já existe (sem mostrar a janela) para poder posicionar antes do
        // Show() — evita qualquer "pulo" visível da posição antiga para a nova.
        new WindowInteropHelper(this).EnsureHandle();
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private static double GetDpiScaleForPoint(System.Drawing.Point point)
    {
        var monitorPoint = new NativeMethods.POINT { X = point.X, Y = point.Y };
        var hMonitor = NativeMethods.MonitorFromPoint(monitorPoint, NativeMethods.MONITOR_DEFAULTTONEAREST);

        if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0)
            return dpiX / 96.0;

        return 1.0;
    }

    private static class NativeMethods
    {
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    }

    private void FocusSearchBox()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
            SearchBox.SelectAll();
        }), DispatcherPriority.Input);
    }

    private void FocusDetailList()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            DetailFieldsList.Focus();
            Keyboard.Focus(DetailFieldsList);
            if (DetailFieldsList.ItemContainerGenerator.ContainerFromIndex(ViewModel.SelectedFieldIndex) is ListBoxItem item)
                item.Focus();
        }), DispatcherPriority.Input);
    }

    private void FocusFullDetailList()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            FullDetailFieldsList.Focus();
            Keyboard.Focus(FullDetailFieldsList);
            if (FullDetailFieldsList.ItemContainerGenerator.ContainerFromIndex(ViewModel.SelectedFullDetailFieldIndex) is ListBoxItem item)
                item.Focus();
        }), DispatcherPriority.Input);
    }

    private void SearchBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ViewModel.SelectNext();
                e.Handled = true;
                break;
            case Key.Up:
                ViewModel.SelectPrevious();
                e.Handled = true;
                break;
            case Key.Enter:
                ViewModel.ConfirmSelection();
                e.Handled = true;
                break;
        }
    }

    private void DetailFieldsList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.ConfirmSelectedDetailField();
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.ConfirmSelectedDetailField();
            e.Handled = true;
        }
    }

    private void DetailFieldsList_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var field = (e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>()?.DataContext as ViewModels.CopyableField;
        if (field is { IsMoreInfo: true })
            ViewModel.OpenFullDetailCommand.Execute(null);
    }

    private void FullDetailFieldsList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.CopySelectedFullDetailField();
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ViewModel.CopySelectedFullDetailField();
            e.Handled = true;
        }
    }

    private void SearchWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.GoBackOrClose();
            e.Handled = true;
        }
    }

    private void ResultsListBox_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject)?.FindAncestor<ListBoxItem>() is { DataContext: Core.Models.Customer customer })
        {
            ViewModel.SelectResultCommand.Execute(customer);
        }
    }

    private void SearchWindow_OnDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible)
            Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
