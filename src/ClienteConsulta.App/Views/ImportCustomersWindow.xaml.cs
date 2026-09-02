using System.Windows;
using System.Windows.Media;
using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;
using ClienteConsulta.Data.Excel;
using Microsoft.Win32;

namespace ClienteConsulta.App.Views;

public partial class ImportCustomersWindow : Window
{
    private readonly Func<IReadOnlyList<Customer>, Task> _replaceAllAction;
    private string? _selectedFilePath;

    /// <summary>Quantas empresas ficaram na base depois da importação — para o chamador atualizar a contagem exibida.</summary>
    public int ImportedCount { get; private set; }

    public ImportCustomersWindow(Func<IReadOnlyList<Customer>, Task> replaceAllAction)
    {
        InitializeComponent();
        _replaceAllAction = replaceAllAction;
    }

    private void SelectFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar relatório da Domínio",
            Filter = "Planilhas (*.xls;*.xlsx)|*.xls;*.xlsx|Todos os arquivos|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _selectedFilePath = dialog.FileName;
        FilePathText.Text = dialog.FileName;
        FilePathText.Foreground = (Brush)FindResource("TextPrimaryBrush");
        ImportButton.IsEnabled = true;
        ShowStatus("", isError: false);
        SummaryText.Text = "";
    }

    private async void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selectedFilePath is null)
            return;

        ImportButton.IsEnabled = false;
        ShowStatus("Lendo o arquivo...", isError: false);
        SummaryText.Text = "";

        DominioImportResult result;
        try
        {
            var path = _selectedFilePath;
            result = await Task.Run(() => DominioReportImporter.Import(path));
        }
        catch (DataSourceException ex)
        {
            ShowStatus(ex.Message, isError: true);
            ImportButton.IsEnabled = true;
            return;
        }

        var confirmMessage =
            $"{result.Customers.Count} empresas encontradas no arquivo.\n\n" +
            "Isso vai substituir toda a base atual de clientes, incluindo empresas cadastradas manualmente. Continuar?";

        var confirmed = MessageBox.Show(
            confirmMessage,
            "Importar clientes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (!confirmed)
        {
            ShowStatus("Importação cancelada.", isError: false);
            ImportButton.IsEnabled = true;
            return;
        }

        ShowStatus("Salvando na base interna...", isError: false);

        try
        {
            await _replaceAllAction(result.Customers);
        }
        catch (DataSourceException ex)
        {
            ShowStatus(ex.Message, isError: true);
            ImportButton.IsEnabled = true;
            return;
        }

        ImportedCount = result.Customers.Count;
        SummaryText.Text = BuildSummary(result);
        DialogResult = true;
    }

    private static string BuildSummary(DominioImportResult result)
    {
        var parts = new List<string>();
        if (result.RegistrosModeloDominio > 0)
            parts.Add($"{result.RegistrosModeloDominio} registro(s) padrão da Domínio ignorado(s)");
        if (result.RegistrosIgnorados > 0)
            parts.Add($"{result.RegistrosIgnorados} registro(s) incompleto(s) ignorado(s)");

        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusText.Text = message;
        StatusText.Foreground = (Brush)FindResource(isError ? "PrimaryBrush" : "TextMutedBrush");
    }
}
