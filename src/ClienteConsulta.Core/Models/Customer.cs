namespace ClienteConsulta.Core.Models;

/// <summary>
/// Representa um cliente (empresa). Campos marcados como opcionais podem não
/// existir na fonte de dados atual (planilha Excel), mas são mantidos aqui
/// para que fontes futuras (SQL Server, PostgreSQL, API REST, etc.) possam
/// preenchê-los sem exigir mudanças no restante da aplicação.
/// </summary>
public sealed class Customer
{
    public required string Codigo { get; init; }
    public required string RazaoSocial { get; init; }
    public string? Nome { get; init; }
    public string? Apelido { get; init; }
    public string? NomeFantasia { get; init; }
    public string? NaturezaJuridica { get; init; }
    public string? Contador { get; init; }

    public string? TipoEndereco { get; init; }
    public string? InicioAtividades { get; init; }
    public string? Endereco { get; init; }
    public string? Numero { get; init; }
    public string? Bairro { get; init; }
    public string? Complemento { get; init; }
    public required string Municipio { get; init; }
    public required string UF { get; init; }
    public string? CEP { get; init; }
    public string? Pais { get; init; }

    public string? DataInscricao { get; init; }
    public string? ClienteDesde { get; init; }
    public string? DataGenerica { get; init; }

    public string? Cnae { get; init; }
    public string? RamoAtividade { get; init; }
    public string? ResponsavelLegal { get; init; }
    public string? Telefone { get; init; }
    public string? Email { get; init; }

    public required string CnpjCpf { get; init; }
    public string? InscricaoEstadual { get; init; }
    public string? InscricaoMunicipal { get; init; }
    public string? InscricaoJuntaComercial { get; init; }
    public string? CapitalSocial { get; init; }

    /// <summary>Identificador estável do registro dentro da fonte de dados.</summary>
    public string Id => Codigo;

    private static readonly System.Text.RegularExpressions.Regex NonDigits = new(@"\D+", System.Text.RegularExpressions.RegexOptions.Compiled);

    public string DocumentoSomenteDigitos => NonDigits.Replace(CnpjCpf, string.Empty);

    public bool IsCnpj => DocumentoSomenteDigitos.Length > 11;

    /// <summary>CNPJ/CPF formatado com máscara para exibição.</summary>
    public string DocumentoFormatado
    {
        get
        {
            var digits = DocumentoSomenteDigitos;
            return digits.Length switch
            {
                14 => $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..14]}",
                11 => $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..11]}",
                _ => CnpjCpf
            };
        }
    }
}
