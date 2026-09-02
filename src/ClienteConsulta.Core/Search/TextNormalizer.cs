using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ClienteConsulta.Core.Search;

/// <summary>
/// Normalização de texto usada tanto para indexar os clientes quanto para
/// interpretar o que o usuário digita, permitindo pesquisa tolerante a
/// acentos, caixa e pontuação (ex: CNPJ com ou sem máscara).
/// </summary>
public static class TextNormalizer
{
    private static readonly Regex NonDigits = new(@"\D+", RegexOptions.Compiled);
    private static readonly Regex OnlyDigits = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>Maiúsculas, sem acento, espaços colapsados — para comparação de texto livre.</summary>
    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var formD = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        return Regex.Replace(result, @"\s+", " ").Trim();
    }

    /// <summary>Remove tudo que não for dígito — para comparar CNPJ/CPF/telefone/IE independente de máscara.</summary>
    public static string DigitsOnly(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : NonDigits.Replace(value, string.Empty);

    /// <summary>A consulta do usuário parece ser um documento/código numérico (só dígitos, 2+)?</summary>
    public static bool LooksLikeDigitsQuery(string digitsOnlyQuery)
        => digitsOnlyQuery.Length >= 2 && OnlyDigits.IsMatch(digitsOnlyQuery);
}
