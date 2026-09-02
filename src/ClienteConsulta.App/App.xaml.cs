using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClienteConsulta.App.Infrastructure;
using ClienteConsulta.App.Infrastructure.Branding;
using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;
using ClienteConsulta.App.ViewModels;
using ClienteConsulta.App.Views;
using ClienteConsulta.Core.Search;
using ClienteConsulta.Data.Excel;
using ClienteConsulta.Data.Sqlite;

namespace ClienteConsulta.App;

public partial class App : Application
{
    private const int HotKeyId = 1;

    private SingleInstanceGuard? _instanceGuard;
    private GlobalHotKey? _globalHotKey;
    private TrayIconService? _trayIcon;
    private SearchWindow? _searchWindow;
    private AppSettings? _settings;
    private SqliteCustomerRepository? _repository;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Modo utilitário: converte uma planilha .xlsx no banco interno e encerra sem abrir UI.
        // Existe só para automação/migração avançada (não é usado pelo instalador).
        if (e.Args.Length >= 3 && string.Equals(e.Args[0], "--import", StringComparison.OrdinalIgnoreCase))
        {
            RunImportAndExit(e.Args[1], e.Args[2]);
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _instanceGuard = new SingleInstanceGuard();
        if (!_instanceGuard.IsFirstInstance)
        {
            _instanceGuard.NotifyExistingInstance();
            _instanceGuard.Dispose();
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();
        ThemeManager.ApplyAccent(_settings.AccentColor);
        BrandingProvider.Initialize(_settings.LogoPath);

        _repository = new SqliteCustomerRepository(() => _settings.ResolveDatabasePath());
        var searchService = new CustomerSearchService(_repository);
        var viewModel = new SearchViewModel(searchService, _repository);
        viewModel.OpenSettingsRequested += (_, _) => OpenSettings(viewModel);

        _searchWindow = new SearchWindow(viewModel);

        var (modifiers, key) = _settings.ResolveHotKey();

        // Extraído do próprio executável (compilado via ApplicationIcon) em vez de um arquivo solto:
        // publish em arquivo único não copia Content/Assets para fora do .exe.
        using var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
            ?? throw new InvalidOperationException("Ícone da aplicação não encontrado.");
        _trayIcon = new TrayIconService((System.Drawing.Icon)appIcon.Clone(), HotKeyDisplay.Format(modifiers, key));
        _trayIcon.OpenRequested += () => _searchWindow.ShowNearCursor();
        _trayIcon.SettingsRequested += () => OpenSettings(viewModel);
        _trayIcon.ExitRequested += () => ShutdownApplication();

        if (!TryRegisterHotKey(modifiers, key))
        {
            // O atalho salvo não pôde ser registrado (ex: já está em uso); volta para o padrão.
            var fallback = (AppSettings.DefaultHotKeyModifiers, AppSettings.DefaultHotKeyKey);
            if (TryRegisterHotKey(fallback.Item1, fallback.Item2))
            {
                _settings.SetHotKey(fallback.Item1, fallback.Item2);
                _settings.Save();
                _trayIcon.ShowBalloon(AppInfo.DisplayName,
                    $"O atalho salvo estava indisponível. Usando {HotKeyDisplay.Format(fallback.Item1, fallback.Item2)}.");
            }
        }

        _instanceGuard.ListenForShowRequests(() => Dispatcher.Invoke(() => _searchWindow.ShowNearCursor()));

        _ = viewModel.InitializeAsync();
    }

    private static void RunImportAndExit(string xlsxPath, string dbPath)
    {
        try
        {
            var excelRepo = new ExcelCustomerRepository(() => xlsxPath);
            var customers = excelRepo.GetAllAsync().GetAwaiter().GetResult();

            var sqliteRepo = new SqliteCustomerRepository(() => dbPath);
            sqliteRepo.ReplaceAllAsync(customers).GetAwaiter().GetResult();

            Console.WriteLine($"Importados {customers.Count} clientes de \"{xlsxPath}\" para \"{dbPath}\".");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Falha ao importar: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private bool TryRegisterHotKey(ModifierKeys modifiers, Key key)
    {
        _globalHotKey?.Dispose();
        _globalHotKey = null;

        try
        {
            var hotKey = new GlobalHotKey(_searchWindow!, HotKeyId, modifiers, key);
            hotKey.Pressed += () => _searchWindow!.ShowNearCursor();
            _globalHotKey = hotKey;
            _trayIcon?.UpdateTooltip(HotKeyDisplay.Format(modifiers, key));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task AddCustomerAsync(Customer customer)
    {
        await _repository!.AddAsync(customer);
    }

    private async Task ReplaceAllCustomersAsync(IReadOnlyList<Customer> customers)
    {
        await _repository!.ReplaceAllAsync(customers);
    }

    private async void OpenSettings(SearchViewModel viewModel)
    {
        var (currentModifiers, currentKey) = _settings!.ResolveHotKey();

        int customerCount;
        try
        {
            customerCount = (await _repository!.GetAllAsync()).Count;
        }
        catch (DataSourceException)
        {
            // Banco interno ainda não existe (instalação nova) — a tela de Configurações
            // é justamente onde o usuário vai importar a base para criá-lo.
            customerCount = 0;
        }

        var originalAccent = _settings.AccentColor;
        var originalLogo = _settings.LogoPath;

        var dialog = new SettingsWindow(
            _settings.ResolveDatabasePath(),
            customerCount,
            currentModifiers,
            currentKey,
            _settings.AccentColor,
            _settings.LogoPath,
            AddCustomerAsync,
            ReplaceAllCustomersAsync);

        if (dialog.ShowDialog() != true)
        {
            // Cancelou: desfaz qualquer preview de aparência aplicado ao vivo.
            ThemeManager.ApplyAccent(originalAccent);
            BrandingProvider.RevertTo(originalLogo);
            return;
        }

        if (dialog.ChosenHotKeyModifiers is { } newModifiers && dialog.ChosenHotKeyKey is { } newKey
            && (newModifiers != currentModifiers || newKey != currentKey))
        {
            if (TryRegisterHotKey(newModifiers, newKey))
            {
                _settings.SetHotKey(newModifiers, newKey);
            }
            else
            {
                TryRegisterHotKey(currentModifiers, currentKey);
                MessageBox.Show(
                    "Não foi possível usar essa combinação — outro programa já pode estar usando esse atalho. O atalho anterior foi mantido.",
                    AppInfo.DisplayName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        _settings.AccentColor = dialog.ChosenAccentColor;
        _settings.LogoPath = dialog.ChosenLogoPath;
        _settings.Save();

        // Garante que a paleta/logo em runtime batem com o que foi salvo.
        ThemeManager.ApplyAccent(_settings.AccentColor);
        BrandingProvider.RevertTo(_settings.LogoPath);
    }

    private void ShutdownApplication()
    {
        _globalHotKey?.Dispose();
        _trayIcon?.Dispose();
        _instanceGuard?.Dispose();

        if (_searchWindow is not null)
        {
            _searchWindow.AllowClose = true;
            _searchWindow.Close();
        }

        Shutdown();
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}",
            AppInfo.DisplayName,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
