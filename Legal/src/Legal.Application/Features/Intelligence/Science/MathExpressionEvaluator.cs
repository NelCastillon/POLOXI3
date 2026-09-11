using System.Globalization;
using System.Numerics;

namespace Legal.Application.Features.Intelligence.Science;

// ── Exact rational arithmetic + a tiny deterministic expression engine ──────────────────────────
// POLOXI verification is C# only (no external CAS). To decide claims like "2 + 2 == 4" or
// "6/8 == 3/4" without floating-point drift, we parse the NormalizedClaim into a small AST and
// evaluate it over exact BigInteger fractions. Anything that cannot be parsed returns null so the
// verifier can safely leave the obligation OPEN rather than guess.
internal readonly struct BigRational : IEquatable<BigRational>, IComparable<BigRational>
{
    public BigInteger Numerator { get; }

    public BigInteger Denominator { get; }

    public BigRational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException("Rational denominator cannot be zero.");
        }

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        if (gcd > BigInteger.One)
        {
            numerator /= gcd;
            denominator /= gcd;
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    public static BigRational FromInteger(BigInteger value) => new(value, BigInteger.One);

    public static BigRational operator +(BigRational a, BigRational b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static BigRational operator -(BigRational a, BigRational b) =>
        new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static BigRational operator *(BigRational a, BigRational b) =>
        new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    public static BigRational operator /(BigRational a, BigRational b)
    {
        if (b.Numerator.IsZero)
        {
            throw new DivideByZeroException("Division by zero rational.");
        }

        return new BigRational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    }

    public static BigRational operator -(BigRational a) => new(-a.Numerator, a.Denominator);

    public BigRational Pow(int exponent)
    {
        if (exponent == 0)
        {
            return FromInteger(BigInteger.One);
        }

        if (exponent < 0)
        {
            var positive = Pow(-exponent);
            return FromInteger(BigInteger.One) / positive;
        }

        return new BigRational(BigInteger.Pow(Numerator, exponent), BigInteger.Pow(Denominator, exponent));
    }

    public int CompareTo(BigRational other) =>
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(BigRational other) => Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is BigRational other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public double ToDouble() => (double)Numerator / (double)Denominator;

    public override string ToString() => Denominator.IsOne
        ? Numerator.ToString(CultureInfo.InvariantCulture)
        : $"{Numerator.ToString(CultureInfo.InvariantCulture)}/{Denominator.ToString(CultureInfo.InvariantCulture)}";
}

// Result of evaluating a relational claim: null means "could not decide deterministically".
internal readonly record struct ClaimEvaluation(bool Holds, string Description);

// Deterministic domain sampler for counterexample search (gap #7). Produces a fixed, reproducible
// set of exact rational probe points so refutation search does not depend on caller-supplied inputs.
internal static class MathDomainSampler
{
    // A broad but bounded deterministic set: negatives, zero, positives, and simple fractions.
    // Ordered so small-magnitude and sign-boundary cases are probed first.
    public static IReadOnlyList<BigRational> DefaultProbes { get; } = BuildDefaultProbes();

    // A smaller deterministic set used for multi-variable cross-product sampling (gap #3) so the
    // combinatorial search stays bounded (e.g. 9^2 = 81, 9^3 = 729 evaluations).
    public static IReadOnlyList<BigRational> CompactProbes { get; } = BuildCompactProbes();

    private static BigRational[] BuildDefaultProbes()
    {
        var integers = new[] { 0, 1, -1, 2, -2, 3, -3, 5, -5, 10, -10, 100, -100 };
        var fractions = new (int Num, int Den)[] { (1, 2), (-1, 2), (1, 3), (-1, 3), (3, 2), (-3, 2), (1, 10), (-1, 10) };

        var probes = new List<BigRational>(integers.Length + fractions.Length);
        foreach (var value in integers)
        {
            probes.Add(BigRational.FromInteger(value));
        }

        foreach (var (num, den) in fractions)
        {
            probes.Add(new BigRational(num, den));
        }

        return [.. probes];
    }

    private static BigRational[] BuildCompactProbes()
    {
        var integers = new[] { 0, 1, -1, 2, -2, 3, -3 };
        var fractions = new (int Num, int Den)[] { (1, 2), (-1, 2) };

        var probes = new List<BigRational>(integers.Length + fractions.Length);
        foreach (var value in integers)
        {
            probes.Add(BigRational.FromInteger(value));
        }

        foreach (var (num, den) in fractions)
        {
            probes.Add(new BigRational(num, den));
        }

        return [.. probes];
    }
}

internal static class MathExpressionEvaluator
{
    // Try to evaluate a full relational claim (e.g. "2 + 2 == 4", "6/8 == 3/4", "3 < 4").
    // Returns null when the text is not a decidable rational relation.
    public static ClaimEvaluation? TryEvaluateRelation(string claim)
    {
        if (string.IsNullOrWhiteSpace(claim))
        {
            return null;
        }

        if (!TrySplitRelation(claim, out var leftText, out var op, out var rightText))
        {
            return null;
        }

        if (!TryEvaluate(leftText, out var left) || !TryEvaluate(rightText, out var right))
        {
            return null;
        }

        var holds = Apply(op, left, right);
        if (holds is null)
        {
            return null;
        }

        return new ClaimEvaluation(holds.Value, $"{left} {op} {right}");
    }

    // Try to evaluate a bare arithmetic expression to an exact rational.
    public static bool TryEvaluate(string expression, out BigRational value) =>
        TryEvaluate(expression, null, out value);

    // Try to evaluate a bare arithmetic expression, binding a single free variable to a value.
    public static bool TryEvaluate(string expression, string? variableName, BigRational variableValue, out BigRational value)
    {
        var bindings = variableName is null
            ? null
            : new Dictionary<string, BigRational>(StringComparer.Ordinal) { [variableName] = variableValue };
        return TryEvaluate(expression, bindings, out value);
    }

    // Try to evaluate a bare arithmetic expression, binding any number of free variables.
    public static bool TryEvaluate(string expression, IReadOnlyDictionary<string, BigRational>? bindings, out BigRational value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            var parser = new Parser(expression, bindings);
            var result = parser.ParseExpression();
            if (!parser.AtEnd)
            {
                return false;
            }

            value = result;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or InvalidOperationException or OverflowException)
        {
            return false;
        }
    }

    // Evaluate a relational claim at a specific value of the given free variable (gap #7 sampling).
    // Returns null when the claim is not a decidable rational relation in that variable.
    public static ClaimEvaluation? TryEvaluateRelationAt(string claim, string variableName, BigRational variableValue)
    {
        if (string.IsNullOrWhiteSpace(claim) || string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        return TryEvaluateRelationAt(
            claim,
            new Dictionary<string, BigRational>(StringComparer.Ordinal) { [variableName] = variableValue });
    }

    // Evaluate a relational claim at a specific assignment of multiple free variables (gap #3 sampling).
    // Returns null when the claim is not a decidable rational relation under those bindings.
    public static ClaimEvaluation? TryEvaluateRelationAt(string claim, IReadOnlyDictionary<string, BigRational> bindings)
    {
        if (string.IsNullOrWhiteSpace(claim) || bindings is null || bindings.Count == 0)
        {
            return null;
        }

        if (!TrySplitRelation(claim, out var leftText, out var op, out var rightText))
        {
            return null;
        }

        if (!TryEvaluate(leftText, bindings, out var left)
            || !TryEvaluate(rightText, bindings, out var right))
        {
            return null;
        }

        var holds = Apply(op, left, right);
        if (holds is null)
        {
            return null;
        }

        var assignment = string.Join(",", bindings.OrderBy(b => b.Key, StringComparer.Ordinal).Select(b => $"{b.Key}={b.Value}"));
        return new ClaimEvaluation(holds.Value, $"{assignment}: {left} {op} {right}");
    }

    // Detect the single free variable (a bare letter identifier) referenced in a claim, if exactly one
    // distinct identifier is present. Returns null when there are zero or multiple distinct variables.
    public static string? TryDetectSingleVariable(string claim)
    {
        if (string.IsNullOrWhiteSpace(claim))
        {
            return null;
        }

        string? found = null;
        var i = 0;
        while (i < claim.Length)
        {
            var c = claim[i];
            if (char.IsLetter(c))
            {
                var start = i;
                while (i < claim.Length && (char.IsLetterOrDigit(claim[i]) || claim[i] == '_'))
                {
                    i++;
                }

                var name = claim[start..i];
                if (found is null)
                {
                    found = name;
                }
                else if (!string.Equals(found, name, StringComparison.Ordinal))
                {
                    return null;
                }
            }
            else
            {
                i++;
            }
        }

        return found;
    }

    // Collect all distinct free-variable identifiers referenced in a claim, in first-seen order.
    // Used by multi-variable counterexample sampling (gap #3).
    public static IReadOnlyList<string> DetectVariables(string claim)
    {
        if (string.IsNullOrWhiteSpace(claim))
        {
            return [];
        }

        var found = new List<string>();
        var i = 0;
        while (i < claim.Length)
        {
            var c = claim[i];
            if (char.IsLetter(c))
            {
                var start = i;
                while (i < claim.Length && (char.IsLetterOrDigit(claim[i]) || claim[i] == '_'))
                {
                    i++;
                }

                var name = claim[start..i];
                if (!found.Contains(name, StringComparer.Ordinal))
                {
                    found.Add(name);
                }
            }
            else
            {
                i++;
            }
        }

        return found;
    }

    private static bool TrySplitRelation(string claim, out string left, out string op, out string right)
    {
        left = string.Empty;
        op = string.Empty;
        right = string.Empty;

        // Two-character operators first so "==" isn't split as "=".
        string[] twoChar = ["==", "!=", "<=", ">="];
        foreach (var candidate in twoChar)
        {
            var idx = claim.IndexOf(candidate, StringComparison.Ordinal);
            if (idx > 0)
            {
                left = claim[..idx];
                op = candidate;
                right = claim[(idx + candidate.Length)..];
                return true;
            }
        }

        for (var i = 1; i < claim.Length; i++)
        {
            var c = claim[i];
            if (c is '=' or '<' or '>')
            {
                left = claim[..i];
                op = c.ToString();
                right = claim[(i + 1)..];
                return true;
            }
        }

        return false;
    }

    private static bool? Apply(string op, BigRational left, BigRational right) => op switch
    {
        "==" or "=" => left.Equals(right),
        "!=" => !left.Equals(right),
        "<" => left.CompareTo(right) < 0,
        ">" => left.CompareTo(right) > 0,
        "<=" => left.CompareTo(right) <= 0,
        ">=" => left.CompareTo(right) >= 0,
        _ => null,
    };

    // Recursive-descent parser over: additive → term → power → unary → primary.
    private sealed class Parser
    {
        private readonly string _text;
        private readonly IReadOnlyDictionary<string, BigRational>? _bindings;
        private int _pos;

        public Parser(string text)
            : this(text, null)
        {
        }

        public Parser(string text, IReadOnlyDictionary<string, BigRational>? bindings)
        {
            _text = text;
            _bindings = bindings;
        }

        public bool AtEnd
        {
            get
            {
                SkipWhitespace();
                return _pos >= _text.Length;
            }
        }

        public BigRational ParseExpression() => ParseAdditive();

        private BigRational ParseAdditive()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '+')
                {
                    _pos++;
                    value += ParseTerm();
                }
                else if (Peek() == '-')
                {
                    _pos++;
                    value -= ParseTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private BigRational ParseTerm()
        {
            var value = ParsePower();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '*')
                {
                    _pos++;
                    value *= ParsePower();
                }
                else if (Peek() == '/')
                {
                    _pos++;
                    value /= ParsePower();
                }
                else
                {
                    return value;
                }
            }
        }

        private BigRational ParsePower()
        {
            var baseValue = ParseUnary();
            SkipWhitespace();
            if (Peek() == '^')
            {
                _pos++;
                var exponent = ParsePower();
                if (!exponent.Denominator.IsOne || exponent.Numerator > int.MaxValue || exponent.Numerator < int.MinValue)
                {
                    throw new InvalidOperationException("Only integer exponents are supported.");
                }

                return baseValue.Pow((int)exponent.Numerator);
            }

            return baseValue;
        }

        private BigRational ParseUnary()
        {
            SkipWhitespace();
            if (Peek() == '+')
            {
                _pos++;
                return ParseUnary();
            }

            if (Peek() == '-')
            {
                _pos++;
                return -ParseUnary();
            }

            return ParsePrimary();
        }

        private BigRational ParsePrimary()
        {
            SkipWhitespace();
            if (Peek() == '(')
            {
                _pos++;
                var value = ParseAdditive();
                SkipWhitespace();
                if (Peek() != ')')
                {
                    throw new FormatException("Missing closing parenthesis.");
                }

                _pos++;
                return value;
            }

            if (char.IsLetter(Peek()))
            {
                var start = _pos;
                while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
                {
                    _pos++;
                }

                var name = _text[start.._pos];
                if (_bindings is not null && _bindings.TryGetValue(name, out var bound))
                {
                    return bound;
                }

                throw new InvalidOperationException($"Unbound identifier '{name}'.");
            }

            return ParseNumber();
        }

        private BigRational ParseNumber()
        {
            SkipWhitespace();
            var start = _pos;
            var hasDigit = false;
            var hasDot = false;

            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (char.IsDigit(c))
                {
                    hasDigit = true;
                    _pos++;
                }
                else if (c == '.' && !hasDot)
                {
                    hasDot = true;
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            if (!hasDigit)
            {
                throw new FormatException("Expected a number.");
            }

            var token = _text[start.._pos];
            return ParseNumericToken(token);
        }

        private static BigRational ParseNumericToken(string token)
        {
            var dot = token.IndexOf('.');
            if (dot < 0)
            {
                return BigRational.FromInteger(BigInteger.Parse(token, CultureInfo.InvariantCulture));
            }

            var digits = token.Replace(".", string.Empty);
            var numerator = digits.Length == 0
                ? BigInteger.Zero
                : BigInteger.Parse(digits, CultureInfo.InvariantCulture);
            var fractionDigits = token.Length - dot - 1;
            var denominator = BigInteger.Pow(10, fractionDigits);
            return new BigRational(numerator, denominator);
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }
    }
}
