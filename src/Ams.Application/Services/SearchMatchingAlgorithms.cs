using System.Globalization;
using System.Text;
using Ams.Application.Features.SearchMatching;

namespace Ams.Application.Services;

public static class SearchMatchingAlgorithms
{
    public static string Normalize(string? value, string entityTypeCode, string fieldCode, IReadOnlyList<NormalizationTermPolicy> terms)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        var tokens = builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var applicable = terms.Where(term =>
            (term.EntityTypeCode.Equals(entityTypeCode, StringComparison.OrdinalIgnoreCase) || term.EntityTypeCode.Equals("Global", StringComparison.OrdinalIgnoreCase))
            && (term.FieldCode.Equals(fieldCode, StringComparison.OrdinalIgnoreCase) || term.FieldCode.Equals("Global", StringComparison.OrdinalIgnoreCase)));

        foreach (var term in applicable)
        {
            for (var index = tokens.Count - 1; index >= 0; index--)
            {
                if (!tokens[index].Equals(term.SourceValue, StringComparison.OrdinalIgnoreCase)) continue;
                if (term.TermKindCode.Equals("STOP_WORD", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(term.NormalizedValue))
                    tokens.RemoveAt(index);
                else
                    tokens[index] = term.NormalizedValue.Trim().ToLowerInvariant();
            }
        }

        return string.Join(' ', tokens);
    }

    public static decimal Similarity(string algorithmCode, string? left, string? right, string entityTypeCode, string fieldCode, IReadOnlyList<NormalizationTermPolicy> terms)
    {
        var normalizedLeft = Normalize(left, entityTypeCode, fieldCode, terms);
        var normalizedRight = Normalize(right, entityTypeCode, fieldCode, terms);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0) return 0;

        return algorithmCode.ToUpperInvariant() switch
        {
            "EXACT" => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase) ? 100 : 0,
            "NORMALIZED_EXACT" => normalizedLeft == normalizedRight ? 100 : 0,
            "SOUNDEX" => Soundex(normalizedLeft) == Soundex(normalizedRight) ? 100 : 0,
            "DAMERAU_LEVENSHTEIN" => EditSimilarity(normalizedLeft, normalizedRight),
            "TOKEN_JACCARD" => TokenJaccard(normalizedLeft, normalizedRight),
            "SEMANTIC_ADVISORY" => TokenJaccard(normalizedLeft, normalizedRight),
            _ => throw new InvalidOperationException($"Matching algorithm '{algorithmCode}' is not supported by the constrained runtime.")
        };
    }

    public static string Soundex(string value)
    {
        var letters = new string(value.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());
        if (letters.Length == 0) return string.Empty;

        var result = new StringBuilder().Append(letters[0]);
        var previous = Code(letters[0]);
        foreach (var letter in letters.Skip(1))
        {
            var code = Code(letter);
            if (code != '0' && code != previous) result.Append(code);
            previous = code;
            if (result.Length == 4) break;
        }
        while (result.Length < 4) result.Append('0');
        return result.ToString();
    }

    public static decimal EditSimilarity(string left, string right)
    {
        var maximumLength = Math.Max(left.Length, right.Length);
        if (maximumLength == 0) return 100;
        var distance = DamerauLevenshteinDistance(left, right);
        return Math.Round(Math.Max(0, 1m - (decimal)distance / maximumLength) * 100m, 4);
    }

    public static int DamerauLevenshteinDistance(string left, string right)
    {
        var distances = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) distances[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) distances[0, j] = j;

        for (var i = 1; i <= left.Length; i++)
        for (var j = 1; j <= right.Length; j++)
        {
            var cost = left[i - 1] == right[j - 1] ? 0 : 1;
            distances[i, j] = Math.Min(Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1), distances[i - 1, j - 1] + cost);
            if (i > 1 && j > 1 && left[i - 1] == right[j - 2] && left[i - 2] == right[j - 1])
                distances[i, j] = Math.Min(distances[i, j], distances[i - 2, j - 2] + cost);
        }

        return distances[left.Length, right.Length];
    }

    public static decimal TokenJaccard(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (leftTokens.Count == 0 || rightTokens.Count == 0) return 0;
        var intersection = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        return Math.Round((decimal)intersection / union * 100m, 4);
    }

    private static char Code(char character) => character switch
    {
        'B' or 'F' or 'P' or 'V' => '1',
        'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z' => '2',
        'D' or 'T' => '3',
        'L' => '4',
        'M' or 'N' => '5',
        'R' => '6',
        _ => '0'
    };
}
