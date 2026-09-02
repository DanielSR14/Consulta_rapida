using ClienteConsulta.Core.Models;

namespace ClienteConsulta.Data.Excel;

/// <summary>
/// Monta um <see cref="Customer"/> a partir de um "leitor" por chave normalizada de
/// <see cref="ColumnMap"/> — reaproveitado tanto por <see cref="ExcelCustomerRepository"/>
/// (uma linha de planilha = uma empresa) quanto por <see cref="DominioReportImporter"/>
/// (um bloco rótulo/valor do relatório da Domínio = uma empresa), para não duplicar o
/// mapeamento dos ~30 campos do <see cref="Customer"/>.
/// </summary>
internal static class CustomerRowMapper
{
    /// <summary>Retorna null quando Código ou Razão social (com fallback para Nome) estiverem vazios.</summary>
    public static Customer? TryMap(Func<string, string> read)
    {
        var codigo = read(ColumnMap.Codigo).Trim();
        var razaoSocial = read(ColumnMap.RazaoSocial).Trim();
        var nome = read(ColumnMap.Nome).Trim();
        if (string.IsNullOrWhiteSpace(razaoSocial))
            razaoSocial = nome;
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(razaoSocial))
            return null;

        return new Customer
        {
            Codigo = codigo,
            RazaoSocial = razaoSocial,
            Nome = NullIfEmpty(nome),
            Apelido = NullIfEmpty(read(ColumnMap.Apelido)),
            NomeFantasia = NullIfEmpty(read(ColumnMap.NomeFantasia)),
            NaturezaJuridica = NullIfEmpty(read(ColumnMap.NaturezaJuridica)),
            Contador = NullIfEmpty(read(ColumnMap.Contador)),
            TipoEndereco = NullIfEmpty(read(ColumnMap.TipoEndereco)),
            InicioAtividades = NullIfEmpty(read(ColumnMap.InicioAtividades)),
            Endereco = NullIfEmpty(read(ColumnMap.Endereco)),
            Numero = NullIfEmpty(read(ColumnMap.Numero)),
            Bairro = NullIfEmpty(read(ColumnMap.Bairro)),
            Complemento = NullIfEmpty(read(ColumnMap.Complemento)),
            Municipio = read(ColumnMap.Municipio).Trim(),
            UF = read(ColumnMap.UF).Trim(),
            CEP = NullIfEmpty(read(ColumnMap.CEP)),
            Pais = NullIfEmpty(read(ColumnMap.Pais)),
            DataInscricao = NullIfEmpty(read(ColumnMap.DataInscricao)),
            ClienteDesde = NullIfEmpty(read(ColumnMap.ClienteDesde)),
            DataGenerica = NullIfEmpty(read(ColumnMap.DataGenerica)),
            Cnae = NullIfEmpty(read(ColumnMap.Cnae)),
            RamoAtividade = NullIfEmpty(read(ColumnMap.RamoAtividade)),
            ResponsavelLegal = NullIfEmpty(read(ColumnMap.ResponsavelLegal)),
            Telefone = NullIfEmpty(read(ColumnMap.Telefone)),
            Email = NullIfEmpty(read(ColumnMap.Email)),
            CnpjCpf = read(ColumnMap.CnpjCpf).Trim(),
            InscricaoEstadual = NullIfEmpty(read(ColumnMap.InscricaoEstadual)),
            InscricaoMunicipal = NullIfEmpty(read(ColumnMap.InscricaoMunicipal)),
            InscricaoJuntaComercial = NullIfEmpty(read(ColumnMap.InscricaoJuntaComercial)),
            CapitalSocial = NullIfEmpty(read(ColumnMap.CapitalSocial)),
        };
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
