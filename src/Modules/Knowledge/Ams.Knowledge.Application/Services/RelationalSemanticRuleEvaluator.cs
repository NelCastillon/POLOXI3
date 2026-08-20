using System.Globalization;
using System.Text.Json;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Contracts.Validation;

namespace Ams.Knowledge.Application.Services;

public sealed class RelationalSemanticRuleEvaluator : ISemanticRuleEvaluator
{
    public SemanticValidationIssue? Evaluate(SemanticValidationRuleDefinition rule, SemanticValidationRequest request)
    {
        var values = request.Properties
            .FirstOrDefault(property => string.Equals(property.PropertyPath, rule.PropertyPath, StringComparison.OrdinalIgnoreCase))?
            .Values ?? [];
        var violated = rule.RuleTypeCode.ToUpperInvariant() switch
        {
            "REQUIREDPROPERTY" or "DOCUMENTREQUIRED" or "ROLEREQUIRED" or "RELATIONSHIPREQUIRED" => values.Count < (rule.MinimumCount ?? 1),
            "MINIMUMCOUNT" => values.Count < (rule.MinimumCount ?? 0),
            "MAXIMUMCOUNT" => rule.MaximumCount.HasValue && values.Count > rule.MaximumCount.Value,
            "ALLOWEDVALUE" => values.Any(value => !ExpectedValues(rule.ExpectedValue).Contains(value)),
            "PROHIBITEDVALUE" => values.Any(value => ExpectedValues(rule.ExpectedValue).Contains(value)),
            "DATECONSTRAINT" => ViolatesDateConstraint(values, rule.OperatorCode, rule.ExpectedValue),
            "NUMERICRANGE" => ViolatesNumericRange(values, rule.ExpectedValue),
            _ => throw new InvalidOperationException($"The active semantic rule type '{rule.RuleTypeCode}' for rule '{rule.RuleCode}' is not supported.")
        };

        return violated
            ? new SemanticValidationIssue(rule.RuleId, rule.RuleCode, rule.SeverityCode, rule.Message, rule.PropertyPath)
            : null;
    }

    private static HashSet<string> ExpectedValues(string? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(expectedValue))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(expectedValue);
            if (values is not null)
                return values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
        }

        return new HashSet<string>([expectedValue], StringComparer.OrdinalIgnoreCase);
    }

    private static bool ViolatesDateConstraint(IReadOnlyCollection<string> values, string operatorCode, string? expectedValue)
    {
        if (!DateTime.TryParse(expectedValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expected))
            throw new InvalidOperationException("A date constraint has an invalid configured ExpectedValue.");

        return values.Any(value => !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var actual) || !Compare(actual, expected, operatorCode));
    }

    private static bool ViolatesNumericRange(IReadOnlyCollection<string> values, string? expectedValue)
    {
        NumericRange? range;
        try
        {
            range = JsonSerializer.Deserialize<NumericRange>(expectedValue ?? string.Empty);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("A numeric range rule has invalid configured JSON.", ex);
        }

        if (range is null || (!range.Minimum.HasValue && !range.Maximum.HasValue))
            throw new InvalidOperationException("A numeric range rule must configure a minimum or maximum.");

        return values.Any(value => !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var actual)
            || range.Minimum.HasValue && actual < range.Minimum.Value
            || range.Maximum.HasValue && actual > range.Maximum.Value);
    }

    private static bool Compare(DateTime actual, DateTime expected, string operatorCode)
        => operatorCode.ToUpperInvariant() switch
        {
            "EQUALS" => actual == expected,
            "BEFORE" => actual < expected,
            "BEFORE_OR_EQUAL" => actual <= expected,
            "AFTER" => actual > expected,
            "AFTER_OR_EQUAL" => actual >= expected,
            _ => throw new InvalidOperationException($"The date operator '{operatorCode}' is not supported.")
        };

    private sealed record NumericRange(decimal? Minimum, decimal? Maximum);
}
