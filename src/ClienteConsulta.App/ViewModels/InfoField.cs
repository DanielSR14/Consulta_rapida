namespace ClienteConsulta.App.ViewModels;

/// <summary>Ícone exibido ao lado de um campo na tela "+ Informações".</summary>
public enum InfoIcon
{
    Document,
    Company,
    Person,
    MapPin,
    Building,
    Phone,
    Mail,
    Calendar,
    Hash,
    Money,
    Landmark,
    Globe,
    Briefcase,
    Tag
}

/// <summary>
/// Um campo (ou cabeçalho de seção) da tela "+ Informações". Diferente de
/// <see cref="CopyableField"/> porque essa tela agrupa campos em seções e
/// mostra um ícone por campo — nenhuma das duas coisas faz sentido na tela
/// de detalhes compacta.
/// </summary>
public sealed class InfoField : ViewModelBase
{
    private bool _isCopied;

    /// <summary>Verdadeiro para uma linha de cabeçalho de seção ("Endereço", "Contato"...) — sem valor, sem cópia.</summary>
    public bool IsSectionHeader { get; init; }

    public required string Label { get; init; }
    public string Value { get; init; } = string.Empty;
    public string CopyValue { get; init; } = string.Empty;
    public InfoIcon Icon { get; init; }

    /// <summary>Verdadeiro por um curto período após a cópia, para dar feedback visual ("Copiado!").</summary>
    public bool IsCopied
    {
        get => _isCopied;
        set => SetField(ref _isCopied, value);
    }
}
