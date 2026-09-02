using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using ClienteConsulta.App.Infrastructure;
using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;
using ClienteConsulta.Core.Search;

namespace ClienteConsulta.App.ViewModels;

public enum ViewMode
{
    List,
    Detail,
    FullDetail,
    Error
}

public sealed class SearchViewModel : ViewModelBase
{
    private readonly CustomerSearchService _searchService;
    private readonly ICustomerRepository _repository;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<Customer> Results { get; } = new();
    public ObservableCollection<CopyableField> DetailFields { get; } = new();
    public ObservableCollection<InfoField> FullDetailFields { get; } = new();

    public event EventHandler? SearchBoxFocusRequested;
    public event EventHandler? DetailFocusRequested;
    public event EventHandler? FullDetailFocusRequested;
    public event EventHandler? CloseRequested;

    /// <summary>Disparado pelo botão "Abrir Configurações" do estado de erro/base vazia.</summary>
    public event EventHandler? OpenSettingsRequested;

    public SearchViewModel(
        CustomerSearchService searchService,
        ICustomerRepository repository)
    {
        _searchService = searchService;
        _repository = repository;

        CopyFieldCommand = new RelayCommand(p => CopyField(p as CopyableField));
        CopyFullDetailFieldCommand = new RelayCommand(p => CopyFullDetailField(p as InfoField));
        BackCommand = new RelayCommand(_ => GoBackOrClose());
        SelectResultCommand = new RelayCommand(p => { if (p is Customer c) ShowDetail(c); });
        OpenFullDetailCommand = new RelayCommand(_ => ShowFullDetail());
        FocusSearchCommand = new RelayCommand(FocusSearch);
        RetryCommand = new RelayCommand(_ => _ = ReloadAsync());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));

        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasResults));
    }

    public bool HasResults => Results.Count > 0;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                _ = RunSearchAsync(value);
        }
    }

    private int _selectedIndex = -1;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetField(ref _selectedIndex, value);
    }

    private int _selectedFieldIndex = -1;
    public int SelectedFieldIndex
    {
        get => _selectedFieldIndex;
        set => SetField(ref _selectedFieldIndex, value);
    }

    private int _selectedFullDetailFieldIndex = -1;
    public int SelectedFullDetailFieldIndex
    {
        get => _selectedFullDetailFieldIndex;
        set => SetField(ref _selectedFullDetailFieldIndex, value);
    }

    private ViewMode _mode = ViewMode.List;
    public ViewMode Mode
    {
        get => _mode;
        private set
        {
            if (SetField(ref _mode, value))
                OnPropertyChanged(nameof(FooterHintText));
        }
    }

    public string FooterHintText => Mode switch
    {
        ViewMode.List => "↑↓ Navegar    ⏎ Selecionar    Esc Fechar",
        ViewMode.Detail => "↑↓ Navegar campos    ⏎ Copiar    Esc Voltar",
        ViewMode.FullDetail => "↑↓ Navegar campos    ⏎ Copiar    Esc Voltar",
        ViewMode.Error => "Esc Fechar",
        _ => string.Empty
    };

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    private Customer? _currentCustomer;
    public Customer? CurrentCustomer
    {
        get => _currentCustomer;
        private set => SetField(ref _currentCustomer, value);
    }

    public RelayCommand CopyFieldCommand { get; }
    public RelayCommand CopyFullDetailFieldCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand SelectResultCommand { get; }
    public RelayCommand OpenFullDetailCommand { get; }
    public RelayCommand FocusSearchCommand { get; }
    public RelayCommand RetryCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }

    /// <summary>Chamado sempre que a janela vai ser exibida (atalho global, bandeja, segunda instância).</summary>
    public void PrepareForShow()
    {
        Mode = ViewMode.List;
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        _ = RunSearchAsync(string.Empty);
        SearchBoxFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task InitializeAsync()
    {
        await RunSearchAsync(string.Empty);
    }

    public async Task ReloadAsync()
    {
        try
        {
            await _repository.ReloadAsync();
            await RunSearchAsync(SearchText);
        }
        catch (DataSourceException ex)
        {
            ErrorMessage = ex.Message;
            Mode = ViewMode.Error;
        }
    }

    private async Task RunSearchAsync(string query)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            var results = await _searchService.SearchAsync(query, 50, cts.Token).ConfigureAwait(true);
            if (cts.IsCancellationRequested) return;

            Results.Clear();
            foreach (var customer in results)
                Results.Add(customer);

            SelectedIndex = Results.Count > 0 ? 0 : -1;
            ErrorMessage = null;
            Mode = ViewMode.List;
        }
        catch (OperationCanceledException)
        {
            // uma busca mais recente já está em andamento
        }
        catch (DataSourceException ex)
        {
            ErrorMessage = ex.Message;
            Mode = ViewMode.Error;
        }
    }

    public void SelectNext()
    {
        if (Mode != ViewMode.List || Results.Count == 0) return;
        SelectedIndex = Math.Min(SelectedIndex + 1, Results.Count - 1);
    }

    public void SelectPrevious()
    {
        if (Mode != ViewMode.List || Results.Count == 0) return;
        SelectedIndex = Math.Max(SelectedIndex - 1, 0);
    }

    public void MoveDetailNext()
    {
        if (Mode != ViewMode.Detail || DetailFields.Count == 0) return;
        SelectedFieldIndex = Math.Min(SelectedFieldIndex + 1, DetailFields.Count - 1);
    }

    public void MoveDetailPrevious()
    {
        if (Mode != ViewMode.Detail || DetailFields.Count == 0) return;
        SelectedFieldIndex = Math.Max(SelectedFieldIndex - 1, 0);
    }

    public void MoveFullDetailNext()
    {
        if (Mode != ViewMode.FullDetail) return;
        var next = SelectedFullDetailFieldIndex + 1;
        while (next < FullDetailFields.Count && FullDetailFields[next].IsSectionHeader)
            next++;
        if (next < FullDetailFields.Count)
            SelectedFullDetailFieldIndex = next;
    }

    public void MoveFullDetailPrevious()
    {
        if (Mode != ViewMode.FullDetail) return;
        var prev = SelectedFullDetailFieldIndex - 1;
        while (prev >= 0 && FullDetailFields[prev].IsSectionHeader)
            prev--;
        if (prev >= 0)
            SelectedFullDetailFieldIndex = prev;
    }

    public void ConfirmSelection()
    {
        if (Mode == ViewMode.List)
        {
            if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
                ShowDetail(Results[SelectedIndex]);
        }
        else if (Mode == ViewMode.Detail)
        {
            ConfirmSelectedDetailField();
        }
        else if (Mode == ViewMode.FullDetail)
        {
            CopySelectedFullDetailField();
        }
    }

    public void GoBackOrClose()
    {
        if (Mode == ViewMode.FullDetail)
        {
            Mode = ViewMode.Detail;
            DetailFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (Mode == ViewMode.Detail)
        {
            Mode = ViewMode.List;
            SearchBoxFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void FocusSearch()
    {
        if (Mode is ViewMode.Detail or ViewMode.FullDetail)
            Mode = ViewMode.List;
        SearchBoxFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Enter no campo selecionado da tela de detalhes: copia, exceto no item "+ Informações", que abre a tela completa.</summary>
    public void ConfirmSelectedDetailField()
    {
        if (SelectedFieldIndex < 0 || SelectedFieldIndex >= DetailFields.Count) return;

        var field = DetailFields[SelectedFieldIndex];
        if (field.IsMoreInfo)
            ShowFullDetail();
        else
            CopyField(field);
    }

    public void CopySelectedFullDetailField()
    {
        if (SelectedFullDetailFieldIndex < 0 || SelectedFullDetailFieldIndex >= FullDetailFields.Count) return;
        CopyFullDetailField(FullDetailFields[SelectedFullDetailFieldIndex]);
    }

    private void ShowDetail(Customer customer)
    {
        CurrentCustomer = customer;
        BuildDetailFields(customer);
        SelectedFieldIndex = DetailFields.Count > 0 ? 0 : -1;
        Mode = ViewMode.Detail;
        DetailFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowFullDetail()
    {
        if (CurrentCustomer is not { } customer) return;

        BuildFullDetailFields(customer);
        var firstSelectable = FullDetailFields.Select((f, i) => (f, i)).FirstOrDefault(x => !x.f.IsSectionHeader).i;
        SelectedFullDetailFieldIndex = FullDetailFields.Count > 0 ? firstSelectable : -1;
        Mode = ViewMode.FullDetail;
        FullDetailFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuildDetailFields(Customer customer)
    {
        DetailFields.Clear();

        DetailFields.Add(new CopyableField
        {
            Label = customer.IsCnpj ? "CNPJ" : "CPF",
            Value = customer.DocumentoFormatado,
            CopyValue = customer.DocumentoSomenteDigitos
        });

        if (!string.IsNullOrWhiteSpace(customer.InscricaoEstadual))
            DetailFields.Add(new CopyableField
            {
                Label = "Inscrição Estadual",
                Value = customer.InscricaoEstadual,
                CopyValue = StripMaskPunctuation(customer.InscricaoEstadual)
            });

        var cidade = string.IsNullOrWhiteSpace(customer.UF) ? customer.Municipio : $"{customer.Municipio} - {customer.UF}";
        DetailFields.Add(new CopyableField { Label = "Cidade", Value = cidade, CopyValue = cidade });

        DetailFields.Add(new CopyableField { Label = "Empresa", Value = customer.Codigo, CopyValue = customer.Codigo });

        DetailFields.Add(new CopyableField { Label = "+ Informações", Value = string.Empty, IsMoreInfo = true });
    }

    private void BuildFullDetailFields(Customer customer)
    {
        FullDetailFields.Clear();

        void AddSection(string title) => FullDetailFields.Add(new InfoField { IsSectionHeader = true, Label = title });

        void Add(string label, string? value, InfoIcon icon, string? copyValue = null)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            FullDetailFields.Add(new InfoField { Label = label, Value = value, CopyValue = copyValue ?? value, Icon = icon });
        }

        var identificacaoStart = FullDetailFields.Count;
        AddSection("Identificação");
        Add("Razão social", customer.RazaoSocial, InfoIcon.Company);
        Add("Nome", customer.Nome, InfoIcon.Company);
        Add("Apelido", customer.Apelido, InfoIcon.Tag);
        Add("Nome fantasia", customer.NomeFantasia, InfoIcon.Tag);
        Add("Natureza jurídica", customer.NaturezaJuridica, InfoIcon.Landmark);
        Add("Código", customer.Codigo, InfoIcon.Hash);
        Add(customer.IsCnpj ? "CNPJ" : "CPF", customer.DocumentoFormatado, InfoIcon.Document, customer.DocumentoSomenteDigitos);
        Add("Contador", customer.Contador, InfoIcon.Person);
        RemoveSectionIfEmpty(identificacaoStart);

        var enderecoStart = FullDetailFields.Count;
        AddSection("Endereço");
        var logradouro = string.Join(", ", new[] { customer.TipoEndereco, customer.Endereco }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(customer.Numero))
            logradouro = string.IsNullOrWhiteSpace(logradouro) ? customer.Numero : $"{logradouro}, {customer.Numero}";
        Add("Logradouro", logradouro, InfoIcon.MapPin);
        Add("Bairro", customer.Bairro, InfoIcon.MapPin);
        Add("Complemento", customer.Complemento, InfoIcon.MapPin);
        var municipioUf = string.IsNullOrWhiteSpace(customer.Municipio) ? null : $"{customer.Municipio} - {customer.UF}";
        Add("Município / UF", municipioUf, InfoIcon.Building);
        Add("CEP", customer.CEP, InfoIcon.MapPin);
        Add("País", customer.Pais, InfoIcon.Globe);
        RemoveSectionIfEmpty(enderecoStart);

        var contatoStart = FullDetailFields.Count;
        AddSection("Contato");
        Add("Telefone", customer.Telefone, InfoIcon.Phone);
        Add("E-mail", customer.Email, InfoIcon.Mail);
        Add("Responsável legal", customer.ResponsavelLegal, InfoIcon.Person);
        RemoveSectionIfEmpty(contatoStart);

        var atividadeStart = FullDetailFields.Count;
        AddSection("Atividade e registros");
        Add("CNAE", customer.Cnae, InfoIcon.Briefcase);
        Add("Ramo de atividade", customer.RamoAtividade, InfoIcon.Briefcase);
        Add("Início das atividades", customer.InicioAtividades, InfoIcon.Calendar);
        Add("Data da inscrição", customer.DataInscricao, InfoIcon.Calendar);
        Add("Cliente desde", customer.ClienteDesde, InfoIcon.Calendar);
        Add("Data", customer.DataGenerica, InfoIcon.Calendar);
        Add("Inscrição estadual", customer.InscricaoEstadual, InfoIcon.Hash,
            customer.InscricaoEstadual is null ? null : StripMaskPunctuation(customer.InscricaoEstadual));
        Add("Inscrição municipal", customer.InscricaoMunicipal, InfoIcon.Hash);
        Add("Inscrição Junta Comercial", customer.InscricaoJuntaComercial, InfoIcon.Hash);
        Add("Capital social", customer.CapitalSocial, InfoIcon.Money);
        RemoveSectionIfEmpty(atividadeStart);
    }

    /// <summary>Se nada foi adicionado depois do cabeçalho de seção (índice sectionIndex), remove o cabeçalho.</summary>
    private void RemoveSectionIfEmpty(int sectionIndex)
    {
        if (sectionIndex == FullDetailFields.Count - 1)
            FullDetailFields.RemoveAt(sectionIndex);
    }

    private static readonly Regex MaskPunctuation = new(@"[.\-/]", RegexOptions.Compiled);

    /// <summary>Remove só a pontuação de máscara (. / -), preservando letras — a Inscrição
    /// Estadual de alguns estados é alfanumérica, então não dá para usar dígitos-apenas aqui.</summary>
    private static string StripMaskPunctuation(string value) => MaskPunctuation.Replace(value, string.Empty);

    private void CopyField(CopyableField? field)
    {
        if (field is null || field.IsMoreInfo) return;
        if (!ClipboardService.TrySetText(field.CopyValue)) return;

        field.IsCopied = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            field.IsCopied = false;
            timer.Stop();
        };
        timer.Start();
    }

    private void CopyFullDetailField(InfoField? field)
    {
        if (field is null || field.IsSectionHeader) return;
        if (!ClipboardService.TrySetText(field.CopyValue)) return;

        field.IsCopied = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            field.IsCopied = false;
            timer.Stop();
        };
        timer.Start();
    }
}
