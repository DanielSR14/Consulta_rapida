using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;
using ClosedXML.Excel;

namespace ClienteConsulta.Data.Excel;

/// <summary>
/// Lê os clientes de uma planilha .xlsx. É a única classe do projeto que sabe
/// qualquer coisa sobre Excel/ClosedXML — para trocar por SQL Server, API REST
/// etc. basta implementar <see cref="ICustomerRepository"/> em outro projeto e
/// registrar essa implementação na composição da aplicação.
/// </summary>
public sealed class ExcelCustomerRepository : ICustomerRepository
{
    private static readonly string[] SheetNamesToTry = ["Empresas", "database"];

    private readonly Func<string> _filePathProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Customer>? _cache;

    public ExcelCustomerRepository(Func<string> filePathProvider)
    {
        _filePathProvider = filePathProvider;
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is { } cached)
            return cached;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache ??= await Task.Run(Load, cancellationToken).ConfigureAwait(false);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cache = await Task.Run(Load, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private IReadOnlyList<Customer> Load()
    {
        var path = _filePathProvider();

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new DataSourceException($"Arquivo de dados não encontrado:\n{path}");

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = new XLWorkbook(stream);

            var worksheet = SheetNamesToTry
                .Select(name => workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(w => w is not null)
                ?? workbook.Worksheets.FirstOrDefault()
                ?? throw new DataSourceException("A planilha não contém nenhuma aba de dados.");

            var usedRange = worksheet.RangeUsed()
                ?? throw new DataSourceException("A planilha está vazia.");

            var headerRow = usedRange.FirstRow();
            var columns = ColumnMap.FromHeaderRow(headerRow);

            var customers = new List<Customer>();
            foreach (var row in usedRange.RowsUsed().Skip(1))
            {
                if (CustomerRowMapper.TryMap(key => columns.Read(row, key)) is { } customer)
                    customers.Add(customer);
            }

            return customers;
        }
        catch (DataSourceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DataSourceException($"Não foi possível ler o arquivo de dados:\n{path}\n\n{ex.Message}", ex);
        }
    }
}
