using ClienteConsulta.Core.Abstractions;
using ClienteConsulta.Core.Models;

namespace ClienteConsulta.Core.Search;

/// <summary>
/// Pesquisa tolerante sobre a lista de clientes: ignora acentuação/caixa em campos de texto
/// e ignora pontuação em campos numéricos (CNPJ, CPF, telefone, inscrição estadual, código).
/// </summary>
public sealed class CustomerSearchService
{
    private readonly ICustomerRepository _repository;
    private IReadOnlyList<Customer>? _indexedSource;
    private IndexedCustomer[] _index = [];

    public CustomerSearchService(ICustomerRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<Customer>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        EnsureIndex(all);

        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return all.Take(maxResults).ToList();

        var normalizedText = TextNormalizer.NormalizeText(trimmed);
        var digitsQuery = TextNormalizer.DigitsOnly(trimmed);
        var isDigitsQuery = TextNormalizer.LooksLikeDigitsQuery(digitsQuery);

        var scored = new List<(Customer Customer, int Score)>(_index.Length);
        foreach (var entry in _index)
        {
            var score = Score(entry, normalizedText, digitsQuery, isDigitsQuery);
            if (score > 0)
                scored.Add((entry.Customer, score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Customer.RazaoSocial, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(s => s.Customer)
            .ToList();
    }

    private void EnsureIndex(IReadOnlyList<Customer> source)
    {
        if (ReferenceEquals(_indexedSource, source))
            return;

        _index = source.Select(IndexedCustomer.Build).ToArray();
        _indexedSource = source;
    }

    private static int Score(IndexedCustomer entry, string normalizedText, string digitsQuery, bool isDigitsQuery)
    {
        var best = 0;

        if (isDigitsQuery)
        {
            best = Math.Max(best, ScoreDigits(entry.DocumentDigits, digitsQuery));
            best = Math.Max(best, ScoreDigits(entry.InscricaoEstadualDigits, digitsQuery));
            best = Math.Max(best, ScoreDigits(entry.TelefoneDigits, digitsQuery));
            best = Math.Max(best, ScoreDigits(entry.CodigoDigits, digitsQuery));
        }

        if (normalizedText.Length > 0)
        {
            best = Math.Max(best, ScoreText(entry.RazaoSocial, normalizedText));
            best = Math.Max(best, ScoreText(entry.Nome, normalizedText));
            best = Math.Max(best, ScoreText(entry.Apelido, normalizedText));
            best = Math.Max(best, ScoreText(entry.NomeFantasia, normalizedText));
            best = Math.Max(best, ScoreText(entry.Municipio, normalizedText) / 2);
        }

        // Consultas mistas (ex: parte do nome + dígitos) ainda casam pelos dígitos mesmo
        // que não pareçam "puramente numéricas" à primeira vista.
        if (!isDigitsQuery && digitsQuery.Length >= 4)
        {
            best = Math.Max(best, ScoreDigits(entry.DocumentDigits, digitsQuery) / 2);
            best = Math.Max(best, ScoreDigits(entry.InscricaoEstadualDigits, digitsQuery) / 2);
            best = Math.Max(best, ScoreDigits(entry.TelefoneDigits, digitsQuery) / 2);
        }

        return best;
    }

    private static int ScoreText(string field, string normalizedQuery)
    {
        if (field.Length == 0) return 0;
        if (field == normalizedQuery) return 100;
        if (field.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 80;

        var wordStart = $" {normalizedQuery}";
        if (field.Contains(wordStart, StringComparison.Ordinal)) return 60;
        if (field.Contains(normalizedQuery, StringComparison.Ordinal)) return 40;

        return 0;
    }

    private static int ScoreDigits(string field, string digitsQuery)
    {
        if (field.Length == 0 || digitsQuery.Length == 0) return 0;
        if (field == digitsQuery) return 100;
        if (field.StartsWith(digitsQuery, StringComparison.Ordinal)) return 75;
        if (field.Contains(digitsQuery, StringComparison.Ordinal)) return 50;
        return 0;
    }

    private readonly record struct IndexedCustomer(
        Customer Customer,
        string RazaoSocial,
        string Nome,
        string Apelido,
        string NomeFantasia,
        string Municipio,
        string DocumentDigits,
        string InscricaoEstadualDigits,
        string TelefoneDigits,
        string CodigoDigits)
    {
        public static IndexedCustomer Build(Customer customer) => new(
            customer,
            TextNormalizer.NormalizeText(customer.RazaoSocial),
            TextNormalizer.NormalizeText(customer.Nome),
            TextNormalizer.NormalizeText(customer.Apelido),
            TextNormalizer.NormalizeText(customer.NomeFantasia),
            TextNormalizer.NormalizeText(customer.Municipio),
            customer.DocumentoSomenteDigitos,
            TextNormalizer.DigitsOnly(customer.InscricaoEstadual),
            TextNormalizer.DigitsOnly(customer.Telefone),
            TextNormalizer.DigitsOnly(customer.Codigo));
    }
}
