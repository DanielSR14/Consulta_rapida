using System.Globalization;
using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;
using ClosedXML.Excel;
using OpenMcdf;

namespace ClienteConsulta.Data.Excel;

public sealed record DominioImportResult(
    IReadOnlyList<Customer> Customers,
    int RegistrosIgnorados,
    int RegistrosModeloDominio);

/// <summary>
/// Lê o relatório "Empresas" exportado pelo sistema contábil Domínio (Thomson Reuters) —
/// Relatórios → Cadastrais → Empresas → Modelo "Completo" → Exportar para excel — e converte
/// para <see cref="Customer"/>, sem depender de nenhuma preparação manual (Python, Excel etc.)
/// fora do app.
/// </summary>
/// <remarks>
/// Porta em C# a lógica que antes vivia em dois scripts Python usados manualmente
/// (clean_empresas.py + gerar_planilha_empresas.py): o relatório bruto não é uma tabela
/// normal — é um "relatório" com blocos delimitados por "DADOS CADASTRAIS", cada bloco
/// trazendo pares rótulo/valor. Dentro do bloco, cada rótulo termina em ":" e seu valor é
/// a primeira célula não vazia depois dele (a Domínio varia a quantidade de colunas de
/// espaçamento/indentação entre rótulo e valor, então não dá pra assumir uma posição fixa).
/// Os rótulos usados pela Domínio (ex. "Código", "Razão social", "Insc. estadual") já
/// batem com os cabeçalhos que <see cref="ColumnMap"/> espera de uma planilha .xlsx normal,
/// então a mesma normalização/mapeamento é reaproveitada aqui.
/// </remarks>
public static class DominioReportImporter
{
    private const string BlockDelimiter = "DADOS CADASTRAIS";

    private static readonly string[] NoiseLinePrefixes = ["MÓDULOS UTILIZADOS", "Módulos:", "EMPRESAS"];

    /// <summary>
    /// A Domínio inclui, além dos clientes reais, registros próprios de sistema: modelos de
    /// regime tributário para cadastro rápido (Código 9991 em diante — confirmado inspecionando
    /// um export real, todos com nomes como "EMPRESA EXEMPLO..."/"LUCRO PRESUMIDO - ...") e um
    /// registro padrão "BANCO DO BRASIL SA" (Código 1). Nenhum dos dois é cliente do escritório,
    /// então ficam de fora da importação — decisão confirmada com o usuário.
    /// </summary>
    private const int TemplateCodeThreshold = 9991;
    private const string SeedBancoDoBrasilCodigo = "1";

    public static DominioImportResult Import(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new DataSourceException($"Arquivo não encontrado:\n{filePath}");

        var grid = ReadGrid(filePath);
        var blocks = ParseBlocks(grid);

        if (blocks.Count == 0)
            throw new DataSourceException(
                "Não foi possível reconhecer o formato do arquivo.\n\n" +
                "Confirme que ele foi exportado da Domínio pelo caminho:\n" +
                "Relatórios → Cadastrais → Empresas → Modelo \"Completo\" → Exportar para excel");

        var customers = new List<Customer>(blocks.Count);
        var ignored = 0;
        var templates = 0;
        foreach (var block in blocks)
        {
            var customer = CustomerRowMapper.TryMap(key => block.TryGetValue(key, out var value) ? value : "");
            if (customer is null)
                ignored++;
            else if (IsDominioTemplateOrSeed(customer.Codigo))
                templates++;
            else
                customers.Add(customer);
        }

        return new DominioImportResult(customers, ignored, templates);
    }

    private static bool IsDominioTemplateOrSeed(string codigo)
    {
        if (codigo == SeedBancoDoBrasilCodigo)
            return true;

        return int.TryParse(codigo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            && n >= TemplateCodeThreshold;
    }

    // Assinaturas de arquivo (magic bytes) para detectar o formato real, já que a extensão
    // sozinha não é confiável (a Domínio sempre exporta .xls, mas o usuário pode ter salvo
    // como .xlsx em algum passo intermediário).
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    private static List<List<string>> ReadGrid(string filePath)
    {
        try
        {
            var header = new byte[8];
            using (var probe = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                probe.ReadExactly(header, 0, Math.Min(header.Length, (int)probe.Length));

            if (header.AsSpan(0, 4).SequenceEqual(ZipSignature))
                return ReadXlsxGrid(filePath);

            if (header.SequenceEqual(OleSignature))
                return ReadXlsGrid(filePath);

            throw new DataSourceException(
                $"O arquivo não parece ser uma planilha Excel (.xls/.xlsx) válida:\n{filePath}");
        }
        catch (DataSourceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataSourceException($"Não foi possível ler o arquivo:\n{filePath}\n\n{ex.Message}", ex);
        }
    }

    /// <summary>.xls legado (BIFF8) — ver <see cref="Biff8Reader"/> para o porquê de não usar uma biblioteca de terceiros aqui.</summary>
    private static List<List<string>> ReadXlsGrid(string filePath)
    {
        using var storage = RootStorage.OpenRead(filePath);
        using var workbookStream = storage.OpenStream("Workbook");
        using var buffer = new MemoryStream();
        workbookStream.CopyTo(buffer);

        return Biff8Reader.ReadFirstSheetGrid(buffer.ToArray());
    }

    private static List<List<string>> ReadXlsxGrid(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new DataSourceException("A planilha não contém nenhuma aba de dados.");

        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
            return [];

        var columnCount = usedRange.ColumnCount();
        var grid = new List<List<string>>(usedRange.RowCount());
        foreach (var row in usedRange.Rows())
        {
            var line = new List<string>(columnCount);
            for (var c = 1; c <= columnCount; c++)
                line.Add(row.Cell(c).GetString().Trim());
            grid.Add(line);
        }

        return grid;
    }

    /// <summary>
    /// Remove as linhas de ruído do relatório e agrupa o restante em blocos (um bloco =
    /// uma empresa), delimitados por "DADOS CADASTRAIS" na 1ª coluna. Dentro de um bloco,
    /// cada linha pode trazer mais de um par rótulo/valor lado a lado.
    /// </summary>
    private static List<Dictionary<string, string>> ParseBlocks(List<List<string>> grid)
    {
        var blocks = new List<Dictionary<string, string>>();
        Dictionary<string, string>? current = null;

        foreach (var row in grid)
        {
            var firstCell = row.Count > 0 ? row[0] : "";

            if (IsNoiseLine(firstCell))
                continue;

            if (firstCell == BlockDelimiter)
            {
                if (current is { Count: > 0 })
                    blocks.Add(current);
                current = new Dictionary<string, string>(StringComparer.Ordinal);
                continue;
            }

            if (current is null)
                continue; // linha antes do primeiro "DADOS CADASTRAIS" -> ignora

            // O relatório não usa colunas de rótulo/valor fixas (a Domínio insere colunas de
            // espaçamento/indentação variáveis entre elas) — em vez de assumir uma posição fixa,
            // qualquer célula não vazia terminada em ":" é um rótulo, e o valor é a primeira
            // célula não vazia depois dela, até achar o próximo rótulo (":") ou o fim da linha.
            for (var i = 0; i < row.Count; i++)
            {
                var cell = row[i].Trim();
                if (cell.Length == 0 || !cell.EndsWith(':'))
                    continue;

                var key = ColumnMap.NormalizeHeader(cell.TrimEnd(':').Trim());
                if (key.Length == 0)
                    continue;

                var value = "";
                for (var j = i + 1; j < row.Count; j++)
                {
                    var candidate = row[j].Trim();
                    if (candidate.Length == 0)
                        continue;
                    if (!candidate.EndsWith(':'))
                        value = candidate;
                    break;
                }

                current[key] = value;
            }
        }

        if (current is { Count: > 0 })
            blocks.Add(current);

        return blocks;
    }

    private static bool IsNoiseLine(string firstCell)
    {
        if (firstCell.Length == 0)
            return false;

        foreach (var prefix in NoiseLinePrefixes)
        {
            if (firstCell.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
