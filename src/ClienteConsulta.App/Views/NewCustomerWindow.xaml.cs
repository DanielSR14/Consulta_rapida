using System.Windows;
using System.Windows.Media;
using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;

namespace ClienteConsulta.App.Views;

public partial class NewCustomerWindow : Window
{
    private readonly Func<Customer, Task> _registerAction;

    public NewCustomerWindow(Func<Customer, Task> registerAction)
    {
        InitializeComponent();
        _registerAction = registerAction;
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var razaoSocial = RazaoSocialBox.Text.Trim();
        var codigo = CodigoBox.Text.Trim();
        var cnpjCpf = CnpjCpfBox.Text.Trim();
        var inscricaoEstadual = InscricaoEstadualBox.Text.Trim();
        var municipio = MunicipioBox.Text.Trim();
        var uf = UFBox.Text.Trim();

        if (razaoSocial.Length == 0 || codigo.Length == 0 || cnpjCpf.Length == 0
            || inscricaoEstadual.Length == 0 || municipio.Length == 0)
        {
            ShowStatus("Preencha todos os campos obrigatórios (*).", isError: true);
            return;
        }

        var customer = new Customer
        {
            Codigo = codigo,
            RazaoSocial = razaoSocial,
            CnpjCpf = cnpjCpf,
            InscricaoEstadual = inscricaoEstadual,
            Municipio = municipio,
            UF = uf,
        };

        SaveButton.IsEnabled = false;
        ShowStatus("Cadastrando...", isError: false);

        try
        {
            await _registerAction(customer);
            DialogResult = true;
        }
        catch (DataSourceException ex)
        {
            ShowStatus(ex.Message, isError: true);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
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
