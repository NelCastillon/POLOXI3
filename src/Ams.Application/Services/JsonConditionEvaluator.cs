using System.Globalization;
using System.Text.Json;

namespace Ams.Application.Services;

public static class JsonConditionEvaluator
{
    private static readonly HashSet<string> AllowedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "EQUALS", "NOT_EQUALS", "GREATER_THAN", "GREATER_THAN_OR_EQUAL", "LESS_THAN", "LESS_THAN_OR_EQUAL", "IS_EMPTY", "IS_NOT_EMPTY", "CONTAINS", "IN"
    };

    public static bool Evaluate(JsonElement condition, JsonElement facts)
    {
        if (condition.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("A condition must be a JSON object.");

        if (condition.TryGetProperty("all", out var all))
            return all.ValueKind == JsonValueKind.Array && all.EnumerateArray().All(item => Evaluate(item, facts));
        if (condition.TryGetProperty("any", out var any))
            return any.ValueKind == JsonValueKind.Array && any.EnumerateArray().Any(item => Evaluate(item, facts));
        if (condition.TryGetProperty("not", out var not))
            return !Evaluate(not, facts);

        var field = GetString(condition, "field", "fieldCode", "property")
            ?? throw new InvalidOperationException("A condition field is required.");
        var operation = GetString(condition, "operator", "operation")?.ToUpperInvariant()
            ?? throw new InvalidOperationException("A condition operator is required.");
        if (!AllowedOperators.Contains(operation))
            throw new InvalidOperationException($"Condition operator '{operation}' is not allowed.");

        var actualExists = TryResolve(facts, field, out var actual);
        condition.TryGetProperty("value", out var expected);
        return operation switch
        {
            "IS_EMPTY" => !actualExists || IsEmpty(actual),
            "IS_NOT_EMPTY" => actualExists && !IsEmpty(actual),
            "EQUALS" => actualExists && Compare(actual, expected) == 0,
            "NOT_EQUALS" => !actualExists || Compare(actual, expected) != 0,
            "GREATER_THAN" => actualExists && Compare(actual, expected) > 0,
            "GREATER_THAN_OR_EQUAL" => actualExists && Compare(actual, expected) >= 0,
            "LESS_THAN" => actualExists && Compare(actual, expected) < 0,
            "LESS_THAN_OR_EQUAL" => actualExists && Compare(actual, expected) <= 0,
            "CONTAINS" => actualExists && Contains(actual, expected),
            "IN" => actualExists && expected.ValueKind == JsonValueKind.Array && expected.EnumerateArray().Any(item => Compare(actual, item) == 0),
            _ => false
        };
    }

    private static string? GetString(JsonElement node, params string[] names)
    {
        foreach (var name in names)
            if (node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static bool TryResolve(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return false;
        }
        return true;
    }

    private static bool IsEmpty(JsonElement value) => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString())
        || value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0;

    private static bool Contains(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind == JsonValueKind.Array)
            return actual.EnumerateArray().Any(item => Compare(item, expected) == 0);
        return actual.ValueKind == JsonValueKind.String && expected.ValueKind == JsonValueKind.String
            && actual.GetString()!.Contains(expected.GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(JsonElement left, JsonElement right)
    {
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
            return leftNumber.CompareTo(rightNumber);
        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False && right.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return left.GetBoolean().CompareTo(right.GetBoolean());
        return string.CompareOrdinal(Scalar(left), Scalar(right));
    }

    private static bool TryDecimal(JsonElement value, out decimal number)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out number)) return true;
        return decimal.TryParse(value.ValueKind == JsonValueKind.String ? value.GetString() : null, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText()
    };
}
