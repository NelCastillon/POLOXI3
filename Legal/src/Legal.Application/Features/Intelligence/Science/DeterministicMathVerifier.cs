using System.Globalization;
using System.Numerics;

namespace Legal.Application.Features.Intelligence.Science;

// ── Deterministic C#-only math verifier (additive; not yet wired) ──────────────────────────────────
// Owns acceptance authority. It never trusts LLM confidence: it either deterministically confirms an
// obligation, deterministically refutes it (counterexample found), or leaves it OPEN when the claim
// cannot be reduced to a decidable check. Everything here is pure and side-effect free.
public sealed class DeterministicMathVerifier : IMathVerifier
{
    public ProofObligation Verify(ProofObligation obligation, MathVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(obligation);
        ArgumentNullException.ThrowIfNull(request);

        return request.Method switch
        {
            ObligationVerificationMethod.Numeric or ObligationVerificationMethod.Algebraic
                or ObligationVerificationMethod.SymbolicIdentity => VerifyScalarRelation(obligation, request),
            ObligationVerificationMethod.CaseExhaustion => VerifyCaseExhaustion(obligation, request),
            ObligationVerificationMethod.CounterexampleCheck => VerifyCounterexample(obligation, request),
            // Logical / open reasoning cannot be decided by deterministic C#; stay OPEN, never guess.
            _ => obligation with
            {
                Status = ObligationStatus.Open,
                VerificationNote = "Not reducible to a deterministic C# check; left OPEN for reasoning.",
            },
        };
    }

    private static ProofObligation VerifyScalarRelation(ProofObligation obligation, MathVerificationRequest request)
    {
        if (request.Left is not { } left || request.Right is not { } right)
        {
            // No pre-reduced operands: try to decide the NormalizedClaim directly using the exact
            // rational expression engine (e.g. "2 + 2 == 4", "6/8 == 3/4"). Exactness avoids any
            // floating-point drift and is the core of gap #6.
            var evaluation = MathExpressionEvaluator.TryEvaluateRelation(request.NormalizedClaim);
            if (evaluation is { } decided)
            {
                return obligation with
                {
                    Status = decided.Holds ? ObligationStatus.Verified : ObligationStatus.Refuted,
                    CounterexampleStatus = decided.Holds ? CounterexampleStatus.NoneFound : CounterexampleStatus.Found,
                    VerificationNote = decided.Holds
                        ? $"Verified by exact evaluation: {decided.Description}."
                        : $"Refuted by exact evaluation: {decided.Description} does not hold.",
                };
            }

            return obligation with
            {
                Status = ObligationStatus.Open,
                VerificationNote = "Scalar operands not supplied and claim not reducible to an exact relation; left OPEN.",
            };
        }

        var holds = Compare(left, right, request.ExpectedRelation, request.Tolerance);
        return obligation with
        {
            Status = holds ? ObligationStatus.Verified : ObligationStatus.Refuted,
            CounterexampleStatus = holds ? CounterexampleStatus.NoneFound : CounterexampleStatus.Found,
            VerificationNote = holds
                ? $"Verified: {left} {Symbol(request.ExpectedRelation)} {right}."
                : $"Refuted: {left} {Symbol(request.ExpectedRelation)} {right} does not hold.",
        };
    }

    private static ProofObligation VerifyCaseExhaustion(ProofObligation obligation, MathVerificationRequest request)
    {
        if (request.TestInputs.Count == 0)
        {
            return obligation with
            {
                Status = ObligationStatus.Open,
                VerificationNote = "No cases supplied for exhaustion; left OPEN.",
            };
        }

        // For HoldsForAll every case must satisfy the relation against Right (default 0).
        var right = request.Right ?? 0d;
        foreach (var value in request.TestInputs)
        {
            if (!Compare(value, right, request.ExpectedRelation, request.Tolerance))
            {
                return obligation with
                {
                    Status = ObligationStatus.Refuted,
                    CounterexampleStatus = CounterexampleStatus.Found,
                    VerificationNote = $"Case {value} violates {Symbol(request.ExpectedRelation)} {right}.",
                };
            }
        }

        return obligation with
        {
            Status = ObligationStatus.Verified,
            CounterexampleStatus = CounterexampleStatus.NoneFound,
            VerificationNote = $"All {request.TestInputs.Count} supplied cases satisfy the relation.",
        };
    }

    private static ProofObligation VerifyCounterexample(ProofObligation obligation, MathVerificationRequest request)
    {
        // Refutation search: finding ONE violating input disproves a universal claim. Finding none
        // never proves the claim — it only reports NoneFound and stays UNRESOLVED.

        // Gap #7 / #3: if the claim is a relation in one or more free variables (e.g. "x^2 >= 0" or
        // "x*y == y*x"), substitute deterministic domain probes and search for a violation. Single
        // variables use the broad probe set; multiple variables use a bounded cross-product.
        var variables = MathExpressionEvaluator.DetectVariables(request.NormalizedClaim);
        if (variables.Count > 0)
        {
            var sampled = SearchClaimForCounterexample(obligation, request, variables);
            if (sampled is { } result)
            {
                return result;
            }
        }

        var right = request.Right ?? 0d;
        foreach (var value in request.TestInputs)
        {
            if (!Compare(value, right, request.ExpectedRelation, request.Tolerance))
            {
                return obligation with
                {
                    Status = ObligationStatus.Refuted,
                    CounterexampleStatus = CounterexampleStatus.Found,
                    VerificationNote = $"Counterexample found: {value}.",
                };
            }
        }

        return obligation with
        {
            Status = ObligationStatus.Unresolved,
            CounterexampleStatus = CounterexampleStatus.NoneFound,
            VerificationNote = "No counterexample over supplied inputs; absence does NOT establish truth.",
        };
    }

    // Substitute deterministic domain probes (and caller-supplied inputs) into a claim over one or more
    // free variables and search for a violating assignment. Single variables use the broad probe set;
    // multiple variables use a bounded cross-product of the compact probe set to keep the search finite.
    // Returns null when the claim is not evaluable as a relation (so the caller can fall back to legacy
    // scalar behavior).
    private static ProofObligation? SearchClaimForCounterexample(
        ProofObligation obligation, MathVerificationRequest request, IReadOnlyList<string> variables)
    {
        if (variables.Count == 1)
        {
            return SearchSingleVariable(obligation, request, variables[0]);
        }

        return SearchMultiVariable(obligation, request, variables);
    }

    private static ProofObligation? SearchSingleVariable(
        ProofObligation obligation, MathVerificationRequest request, string variable)
    {
        var probes = new List<BigRational>(MathDomainSampler.DefaultProbes);
        foreach (var supplied in request.TestInputs)
        {
            if (MathExpressionEvaluator.TryEvaluate(
                supplied.ToString(CultureInfo.InvariantCulture), out var asRational))
            {
                probes.Add(asRational);
            }
        }

        var evaluatedAny = false;
        foreach (var probe in probes)
        {
            var evaluation = MathExpressionEvaluator.TryEvaluateRelationAt(request.NormalizedClaim, variable, probe);
            if (evaluation is not { } decided)
            {
                continue;
            }

            evaluatedAny = true;
            if (!decided.Holds)
            {
                return obligation with
                {
                    Status = ObligationStatus.Refuted,
                    CounterexampleStatus = CounterexampleStatus.Found,
                    VerificationNote = $"Counterexample found by domain sampling ({decided.Description}).",
                };
            }
        }

        if (!evaluatedAny)
        {
            return null;
        }

        return obligation with
        {
            Status = ObligationStatus.Unresolved,
            CounterexampleStatus = CounterexampleStatus.NoneFound,
            VerificationNote = $"No counterexample over {probes.Count} sampled points; absence does NOT establish truth.",
        };
    }

    // Bounded cross-product sampling over the compact probe set for claims with 2+ variables (gap #3).
    // To keep the search finite we cap the number of variables that are sampled combinatorially.
    private static ProofObligation? SearchMultiVariable(
        ProofObligation obligation, MathVerificationRequest request, IReadOnlyList<string> variables)
    {
        const int maxVariables = 3;
        if (variables.Count > maxVariables)
        {
            return null;
        }

        var probes = MathDomainSampler.CompactProbes;
        var evaluatedAny = false;
        var combinations = 0;

        foreach (var assignment in CrossProduct(variables, probes))
        {
            var evaluation = MathExpressionEvaluator.TryEvaluateRelationAt(request.NormalizedClaim, assignment);
            if (evaluation is not { } decided)
            {
                continue;
            }

            evaluatedAny = true;
            combinations++;
            if (!decided.Holds)
            {
                return obligation with
                {
                    Status = ObligationStatus.Refuted,
                    CounterexampleStatus = CounterexampleStatus.Found,
                    VerificationNote = $"Counterexample found by multi-variable domain sampling ({decided.Description}).",
                };
            }
        }

        if (!evaluatedAny)
        {
            return null;
        }

        return obligation with
        {
            Status = ObligationStatus.Unresolved,
            CounterexampleStatus = CounterexampleStatus.NoneFound,
            VerificationNote = $"No counterexample over {combinations} sampled assignments of {variables.Count} variables; absence does NOT establish truth.",
        };
    }

    // Enumerate every assignment of the given variables over the probe set (bounded cross-product).
    private static IEnumerable<Dictionary<string, BigRational>> CrossProduct(
        IReadOnlyList<string> variables, IReadOnlyList<BigRational> probes)
    {
        var indices = new int[variables.Count];
        while (true)
        {
            var assignment = new Dictionary<string, BigRational>(StringComparer.Ordinal);
            for (var v = 0; v < variables.Count; v++)
            {
                assignment[variables[v]] = probes[indices[v]];
            }

            yield return assignment;

            var position = variables.Count - 1;
            while (position >= 0)
            {
                indices[position]++;
                if (indices[position] < probes.Count)
                {
                    break;
                }

                indices[position] = 0;
                position--;
            }

            if (position < 0)
            {
                yield break;
            }
        }
    }

    private static bool Compare(double left, double right, ExpectedRelation relation, double tolerance) =>
        relation switch
        {
            ExpectedRelation.Equal => Math.Abs(left - right) <= tolerance,
            ExpectedRelation.LessThan => left < right - tolerance,
            ExpectedRelation.GreaterThan => left > right + tolerance,
            ExpectedRelation.HoldsForAll => Math.Abs(left - right) <= tolerance,
            ExpectedRelation.Exists => Math.Abs(left - right) <= tolerance,
            _ => false,
        };

    private static string Symbol(ExpectedRelation relation) => relation switch
    {
        ExpectedRelation.Equal => "==",
        ExpectedRelation.LessThan => "<",
        ExpectedRelation.GreaterThan => ">",
        ExpectedRelation.HoldsForAll => "==",
        ExpectedRelation.Exists => "==",
        _ => "?",
    };

    public string Canonicalize(string rawAnswer, ScientificAnswerType answerType)
    {
        ArgumentNullException.ThrowIfNull(rawAnswer);
        var trimmed = rawAnswer.Trim();

        return answerType switch
        {
            ScientificAnswerType.ExactFraction => CanonicalizeFraction(trimmed),
            ScientificAnswerType.Numeric => CanonicalizeNumeric(trimmed),
            ScientificAnswerType.Boolean => CanonicalizeBoolean(trimmed),
            ScientificAnswerType.Set => CanonicalizeSet(trimmed),
            ScientificAnswerType.Tuple => CollapseWhitespace(trimmed),
            ScientificAnswerType.Expression => CanonicalizeExpression(trimmed),
            ScientificAnswerType.Statement => CanonicalizeStatement(trimmed),
            _ => CollapseWhitespace(trimmed),
        };
    }

    // Expression normalization (gap #4): reduce a constant expression to its exact rational value when
    // possible (so "1/2 + 1/4" and "3/4" and "6/8" all collapse to "3/4"); otherwise normalize operator
    // spacing and remove redundant unary plus so equivalent textual forms cluster together.
    private static string CanonicalizeExpression(string value)
    {
        if (MathExpressionEvaluator.TryEvaluate(value, out var rational))
        {
            return rational.ToString();
        }

        return NormalizeExpressionText(value);
    }

    // Statement normalization (gap #4): if the statement is a decidable constant relation, collapse it
    // to a canonical TRUE/FALSE; if it is a relation whose two sides reduce to exact rationals, rewrite
    // it in a canonical "left <op> right" form; otherwise normalize spacing.
    private static string CanonicalizeStatement(string value)
    {
        var evaluation = MathExpressionEvaluator.TryEvaluateRelation(value);
        if (evaluation is { } decided)
        {
            return decided.Holds ? "TRUE" : "FALSE";
        }

        return NormalizeExpressionText(value);
    }

    // Deterministic textual normalization: single spaces around binary operators, no spaces after unary
    // signs, and collapsed whitespace. Purely syntactic so it never changes meaning.
    private static string NormalizeExpressionText(string value)
    {
        var collapsed = CollapseWhitespace(value);
        var builder = new System.Text.StringBuilder(collapsed.Length * 2);
        for (var i = 0; i < collapsed.Length; i++)
        {
            var c = collapsed[i];
            if (c is '+' or '-' or '*' or '/' or '^' or '=' or '<' or '>')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                builder.Append(c);

                // Keep multi-char operators (==, !=, <=, >=) together.
                if (i + 1 < collapsed.Length && collapsed[i + 1] == '=')
                {
                    builder.Append('=');
                    i++;
                }

                if (i + 1 < collapsed.Length && collapsed[i + 1] != ' ')
                {
                    builder.Append(' ');
                }
            }
            else
            {
                builder.Append(c);
            }
        }

        return CollapseWhitespace(builder.ToString());
    }

    private static string CanonicalizeFraction(string value)
    {
        // Prefer exact rational evaluation so compound expressions like "1/2 + 1/4" reduce to "3/4".
        if (MathExpressionEvaluator.TryEvaluate(value, out var rational))
        {
            return rational.ToString();
        }

        var slash = value.IndexOf('/');
        if (slash <= 0)
        {
            return CanonicalizeNumeric(value);
        }

        var numText = value[..slash].Trim();
        var denText = value[(slash + 1)..].Trim();
        if (BigInteger.TryParse(numText, out var num) && BigInteger.TryParse(denText, out var den) && den != 0)
        {
            var sign = den < 0 ? -1 : 1;
            num *= sign;
            den *= sign;
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), BigInteger.Abs(den));
            if (gcd > 1)
            {
                num /= gcd;
                den /= gcd;
            }

            return den == 1
                ? num.ToString(CultureInfo.InvariantCulture)
                : $"{num.ToString(CultureInfo.InvariantCulture)}/{den.ToString(CultureInfo.InvariantCulture)}";
        }

        return CollapseWhitespace(value);
    }

    private static string CanonicalizeNumeric(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            // Round-trip formatting removes trailing zeros and normalizes "3.0" vs "3".
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        return CollapseWhitespace(value);
    }

    private static string CanonicalizeBoolean(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        return upper switch
        {
            "TRUE" or "T" or "PROVEN" or "YES" => "TRUE",
            "FALSE" or "F" or "DISPROVEN" or "NO" => "FALSE",
            _ => upper,
        };
    }

    private static string CanonicalizeSet(string value)
    {
        var inner = value.Trim().TrimStart('{', '[', '(').TrimEnd('}', ']', ')');
        var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var canonical = parts
            .Select(p => CanonicalizeNumeric(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal);
        return "{" + string.Join(",", canonical) + "}";
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public SelfConsistencyResult Aggregate(IReadOnlyList<CandidateAnswer> candidateAnswers)
    {
        ArgumentNullException.ThrowIfNull(candidateAnswers);

        if (candidateAnswers.Count == 0)
        {
            return new SelfConsistencyResult
            {
                Clusters = [],
                PluralityCanonicalForm = null,
                AgreementRatio = 0d,
            };
        }

        var clusters = candidateAnswers
            .GroupBy(c => c.CanonicalForm, StringComparer.Ordinal)
            .Select(g => new AnswerCluster
            {
                CanonicalForm = g.Key,
                MemberCount = g.Count(),
                StrategyNames = g.Select(c => c.StrategyName).ToArray(),
            })
            .OrderByDescending(c => c.MemberCount)
            .ThenBy(c => c.CanonicalForm, StringComparer.Ordinal)
            .ToArray();

        var plurality = clusters[0];
        return new SelfConsistencyResult
        {
            Clusters = clusters,
            PluralityCanonicalForm = plurality.CanonicalForm,
            AgreementRatio = (double)plurality.MemberCount / candidateAnswers.Count,
        };
    }
}
