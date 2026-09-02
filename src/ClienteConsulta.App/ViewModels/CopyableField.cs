namespace ClienteConsulta.App.ViewModels;

/// <summary>Um campo do painel de detalhes que pode ser copiado (CNPJ/CPF, Inscrição Estadual, etc.).</summary>
public sealed class CopyableField : ViewModelBase
{
    private bool _isCopied;

    public required string Label { get; init; }
    public required string Value { get; init; }

    /// <summary>Texto realmente copiado para a área de transferência — igual a <see cref="Value"/>
    /// por padrão, mas pode diferir (ex: CNPJ/IE copiam só os dígitos, sem a máscara exibida).</summary>
    public string CopyValue { get; init; } = string.Empty;

    /// <summary>Marca o item especial "+ Informações": não copia nada, abre a tela de detalhes completos.</summary>
    public bool IsMoreInfo { get; init; }

    /// <summary>Verdadeiro pros campos comuns (CNPJ, Cidade, Empresa...) — tudo que não é o item "+ Informações".</summary>
    public bool IsNormalField => !IsMoreInfo;

    /// <summary>Verdadeiro por um curto período após a cópia, para dar feedback visual ("Copiado!").</summary>
    public bool IsCopied
    {
        get => _isCopied;
        set => SetField(ref _isCopied, value);
    }
}
