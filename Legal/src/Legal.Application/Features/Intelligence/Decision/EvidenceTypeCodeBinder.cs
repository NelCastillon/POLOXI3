using System.Text;
using System.Text.RegularExpressions;

namespace Legal.Application.Features.Intelligence.Decision;

// Deterministic binder from a model-emitted evidence type code to the Domain Pack canonical vocabulary
// (Legal_DecisionDomainPackEvidenceType.EvidenceTypeCode). Resolution order: exact (case-insensitive)
// -> normalized (PascalCase/camelCase/kebab/space -> UPPER_SNAKE, trailing plural trimmed, compared against
// the equally-normalized canonical set) -> UNCLASSIFIED. Never performs an LLM call; the LLM already
// received the vocabulary in the extraction prompt and this only guards the contract.
public sealed class EvidenceTypeCodeBinder
{
    public const string Unclassified = "UNCLASSIFIED";

    private static readonly Regex CamelBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly EvidenceTypeCodeBinder Passthrough = new(new Dictionary<string, string>(), new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _exact;
    private readonly IReadOnlyDictionary<string, string> _normalized;

    private EvidenceTypeCodeBinder(IReadOnlyDictionary<string, string> exact, IReadOnlyDictionary<string, string> normalized)
    {
        _exact = exact;
        _normalized = normalized;
    }

    public bool HasVocabulary => _exact.Count > 0;

    public static EvidenceTypeCodeBinder Create(IReadOnlyCollection<DecisionDomainPackEvidenceTypeDto> evidenceTypes)
    {
        if (evidenceTypes.Count == 0)
            return Passthrough;

        var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var evidenceType in evidenceTypes)
        {
            if (string.IsNullOrWhiteSpace(evidenceType.EvidenceTypeCode))
                continue;
            var code = evidenceType.EvidenceTypeCode.Trim();
            exact.TryAdd(code, code);
            normalized.TryAdd(Normalize(code), code);
            var singular = TrimPlural(Normalize(code));
            normalized.TryAdd(singular, code);
        }
        return new EvidenceTypeCodeBinder(exact, normalized);
    }

    // Returns the canonical code when the vocabulary is present, otherwise the input unchanged.
    public string Bind(string? emitted)
    {
        var value = emitted?.Trim() ?? string.Empty;
        if (!HasVocabulary)
            return value;
        if (value.Length == 0)
            return Unclassified;
        if (_exact.TryGetValue(value, out var exactMatch))
            return exactMatch;
        if (string.Equals(value, Unclassified, StringComparison.OrdinalIgnoreCase))
            return Unclassified;
        var normalizedValue = Normalize(value);
        if (_normalized.TryGetValue(normalizedValue, out var normalizedMatch))
            return normalizedMatch;
        if (_normalized.TryGetValue(TrimPlural(normalizedValue), out var singularMatch))
            return singularMatch;
        return Unclassified;
    }

    // "MedicalRecord" / "medical-record" / "Medical Record" / "MEDICAL_RECORD" -> "MEDICAL_RECORD".
    public static string Normalize(string value)
    {
        var spaced = CamelBoundary.Replace(value.Trim(), "$1_$2");
        var builder = new StringBuilder(spaced.Length);
        var previousUnderscore = false;
        foreach (var character in spaced)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousUnderscore = false;
            }
            else if (!previousUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }
        return builder.ToString().TrimEnd('_');
    }

    private static string TrimPlural(string normalized)
    {
        if (normalized.EndsWith("IES", StringComparison.Ordinal) && normalized.Length > 3)
            return string.Concat(normalized.AsSpan(0, normalized.Length - 3), "Y");
        if (normalized.EndsWith("SES", StringComparison.Ordinal) && normalized.Length > 3)
            return normalized[..^2];
        if (normalized.EndsWith('S') && !normalized.EndsWith("SS", StringComparison.Ordinal) && normalized.Length > 1)
            return normalized[..^1];
        return normalized;
    }
}
