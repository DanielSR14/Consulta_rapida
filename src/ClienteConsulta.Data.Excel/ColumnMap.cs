using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Search;
using ClosedXML.Excel;

namespace ClienteConsulta.Data.Excel;

/// <summary>
/// Resolve, pelo texto do cabeçalho, em qual coluna está cada campo — assim a
/// planilha pode ter as colunas em qualquer ordem, desde que os títulos batam.
/// </summary>
internal sealed class ColumnMap
{
    public const string Codigo = "CODIGO";
    public const string DataInscricao = "DATA DA INSCRICAO";
    public const string Apelido = "APELIDO";
    public const string Nome = "NOME";
    public const string RazaoSocial = "RAZAO SOCIAL";
    public const string NaturezaJuridica = "NATUREZA JURIDICA";
    public const string NomeFantasia = "NOME FANTASIA";
    public const string Contador = "CONTADOR";
    public const string TipoEndereco = "TIPO DE ENDERECO";
    public const string InicioAtividades = "INICIO ATIVIDADES";
    public const string Endereco = "ENDERECO";
    public const string ClienteDesde = "CLIENTE DESDE";
    public const string Numero = "NUMERO";
    public const string Bairro = "BAIRRO";
    public const string Complemento = "COMPLEMENTO";
    public const string Municipio = "MUNICIPIO";
    public const string UF = "UF";
    public const string DataGenerica = "DATA";
    public const string CEP = "CEP";
    public const string Cnae = "CNAE 23";
    public const string Pais = "PAIS";
    public const string Telefone = "TELEFONE";
    public const string RamoAtividade = "RAMO DE ATIVIDADE";
    public const string ResponsavelLegal = "RESPONSAVEL LEGAL";
    public const string Email = "E-MAIL";
    public const string CnpjCpf = "CNPJ/CPF/CEI/CAEPF";
    public const string InscricaoEstadual = "INSC ESTADUAL";
    public const string CapitalSocial = "CAPITAL SOCIAL";
    public const string InscricaoMunicipal = "INSC MUNICIPAL";
    public const string InscricaoJuntaComercial = "INSC JUNTA COMERCIAL";

    internal static readonly string[] RequiredColumns = [Codigo, RazaoSocial, Municipio, UF, CnpjCpf];

    private readonly Dictionary<string, int> _columnsByHeader;

    private ColumnMap(Dictionary<string, int> columnsByHeader)
    {
        _columnsByHeader = columnsByHeader;
    }

    public static ColumnMap FromHeaderRow(IXLRangeRow headerRow)
    {
        var map = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (header.Length > 0)
                map[header] = cell.Address.ColumnNumber;
        }

        var missing = RequiredColumns.Where(c => !map.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new DataSourceException(
                "A planilha não tem as colunas esperadas.\n" +
                $"Faltando: {string.Join(", ", missing)}\n" +
                $"Encontradas: {string.Join(", ", map.Keys)}");

        return new ColumnMap(map);
    }

    public string Read(IXLRangeRow row, string columnKey)
    {
        if (!_columnsByHeader.TryGetValue(columnKey, out var columnNumber))
            return string.Empty;

        // Cell(int) em IXLRangeRow é relativo ao início do range; usamos a linha
        // "real" da planilha para indexar pelo número de coluna absoluto do cabeçalho.
        return row.WorksheetRow().Cell(columnNumber).GetString().Trim();
    }

    internal static string NormalizeHeader(string header)
        => TextNormalizer.NormalizeText(header).Replace(".", string.Empty).Trim();
}
