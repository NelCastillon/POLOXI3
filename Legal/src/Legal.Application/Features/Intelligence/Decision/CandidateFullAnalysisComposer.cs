using System.Globalization;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Pure, side-effect-free projector that assembles the Candidate Full Analysis read model from the
// authoritative persisted decision state (matter + latest session result + matter evidence graph +
// change events). It NEVER invents values: every number is derived from the supplied sources, and
// sections lacking a persisted per-candidate source are emitted empty so the UI renders honest
// empty-states. Keeping this pure makes it unit-testable without a database.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
internal static class CandidateFullAnalysisComposer
{
    public static CandidateFullAnalysisDto Compose(
        DecisionMatterDto matter,
        Guid? sessionId,
        int requestedIndex,
        DecisionSearchResponse? decision,
        LegalMatterEvidenceGraphDto? evidenceGraph,
        IReadOnlyCollection<MatterChangeEventDto> changeEvents)
    {
        var candidates = (decision?.Candidates ?? [])
            .OrderBy(c => c.RankOrder)
            .ToList();

        // Resolve the selected candidate by 1-based rank index; clamp into range when present.
        var total = candidates.Count;
        var index = total == 0 ? requestedIndex : Math.Clamp(requestedIndex, 1, total);
        var selected = total == 0 ? null : candidates[index - 1];

        var label = selected is not null
            ? (!string.IsNullOrWhiteSpace(selected.CandidateCode) ? selected.CandidateCode : $"C{index}")
            : $"C{index}";
        var title = selected?.Outcome ?? selected?.DisplayName ?? "Candidate";
        var description = selected?.DisplayName;
        var isLeader = selected?.IsWinner ?? false;

        var scorePct = selected is not null ? Pct(selected.CompositeScore) : 0;

        // Margin vs the next-ranked candidate (points of composite score).
        var marginPts = 0;
        if (selected is not null && index < total)
        {
            var next = candidates[index];
            marginPts = Pct(selected.CompositeScore) - Pct(next.CompositeScore);
        }

        var uncertaintyLabel = selected is not null
            ? UncertaintyBand(selected.Uncertainty)
            : "UNKNOWN";

        var readinessLabel = decision?.ReadinessVerdict?.Satisfied == true
            ? "READY"
            : decision is not null ? "PROVISIONAL" : "NO DECISION";

        // ── Multidimensional state bars ──
        var dimensions = selected is null
            ? new List<CandidateDimensionDto>()
            : new List<CandidateDimensionDto>
            {
                new("LEGAL", Pct(selected.LegalSupport), ToneForPct(Pct(selected.LegalSupport))),
                new("FACT", Pct(selected.FactSupport), ToneForPct(Pct(selected.FactSupport))),
                new("EVIDENCE", Pct(selected.EvidenceSupport), ToneForPct(Pct(selected.EvidenceSupport))),
                new("AUTHORITY", Pct(selected.AuthoritySupport), ToneForPct(Pct(selected.AuthoritySupport))),
                new("VERIFICATION", Pct(selected.Verification), ToneForPct(Pct(selected.Verification))),
            };

        // ── Strongest drivers (graph nodes grounded to the decision, highest support first) ──
        var graphNodes = (decision?.GraphNodes ?? []).ToList();
        var propositions = evidenceGraph?.Propositions ?? [];
        var drivers = BuildDrivers(selected, dimensions, graphNodes, propositions);

        // ── Strongest constraints (flip points / unresolved essential nodes) ──
        var constraints = BuildConstraints(decision, graphNodes);

        // ── Candidate hierarchy (CHR) ──
        var hierarchyNodes = BuildHierarchy(graphNodes);
        var resolvedCount = graphNodes.Count(n => n.IsSatisfied);
        var depthLabel = graphNodes.Count == 0
            ? "—"
            : $"L1 → L{Math.Max(1, graphNodes.Max(n => DepthOf(n)))}";
        var hierarchy = new CandidateHierarchySummaryDto(
            NodeCount: graphNodes.Count,
            ResolvedCount: resolvedCount,
            TotalForResolution: graphNodes.Count,
            CoverageLabel: graphNodes.Count == 0
                ? "No persisted hierarchy"
                : (graphNodes.Any(n => n.IsEssential && !n.IsSatisfied) ? "Unresolved essential nodes" : "No critical structural gaps"),
            DepthLabel: depthLabel);

        // ── Proposition inspector ──
        var inspectorProps = BuildPropositions(evidenceGraph);

        // ── Evidence analysis ──
        var evidenceBreakdown = BuildEvidenceBreakdown(evidenceGraph);
        var materialEvidence = BuildMaterialEvidence(evidenceGraph);

        // ── Authority ──
        var authority = BuildAuthority(decision, evidenceGraph);

        // ── Flip points ──
        var flipPoints = BuildFlipPoints(decision, selected);

        // ── ADV / next best information ──
        var adv = BuildAdv(decision);

        // ── Document intelligence ──
        var docIntel = BuildDocumentIntelligence(evidenceGraph, changeEvents);

        // ── Uncertainty map ──
        var uncertaintyMap = BuildUncertaintyMap(selected, dimensions);

        // ── Change history (DCI) ──
        var changeHistory = BuildChangeHistory(changeEvents);

        // ── Readiness ──
        var readiness = BuildReadiness(decision, readinessLabel);

        // ── Presentation gate ── derive the honest presentation state so the UI never calls a
        // non-competed interpretive ranking a "decision".
        var presentation = DecisionPresentationPolicy.Derive(decision, selected);

        return new CandidateFullAnalysisDto(
            MatterId: matter.DecisionMatterId,
            MatterTitle: matter.Title,
            Jurisdiction: matter.Jurisdiction,
            Posture: matter.Posture,
            DecisionSessionId: sessionId,
            CandidateIndex: index,
            CandidateLabel: label,
            CandidateTitle: title,
            CandidateDescription: description,
            IsLeader: isLeader,
            DecisionScorePct: scorePct,
            RankPosition: selected?.RankOrder ?? index,
            RankTotal: total,
            MarginVsNextPts: marginPts,
            UncertaintyLabel: uncertaintyLabel,
            ReadinessLabel: readinessLabel,
            LastRecalibratedUtc: matter.LastDecidedUtc ?? matter.ModifiedDateUtc,
            Dimensions: dimensions,
            StrongestDrivers: drivers,
            StrongestConstraints: constraints,
            Hierarchy: hierarchy,
            HierarchyNodes: hierarchyNodes,
            Propositions: inspectorProps,
            EvidenceBreakdown: evidenceBreakdown,
            MaterialEvidence: materialEvidence,
            Authority: authority,
            FlipPoints: flipPoints,
            NextBestInformation: adv,
            DocumentIntelligence: docIntel,
            UncertaintyMap: uncertaintyMap,
            ChangeHistory: changeHistory,
            Readiness: readiness,
            Narrative: decision?.FinalAnswer)
        {
            Presentation = presentation,
        };
    }

    // ── Builders ─────────────────────────────────────────────────────────────────────────────────

    private static List<CandidateDriverDto> BuildDrivers(
        DecisionCandidateDto? selected,
        IReadOnlyList<CandidateDimensionDto> dimensions,
        IReadOnlyList<DecisionGraphNodeDto> graphNodes,
        IReadOnlyCollection<LegalEvidenceGraphPropositionDto> propositions)
    {
        // Prefer grounded graph nodes (highest support). Fall back to the candidate's dimension bars.
        var fromGraph = graphNodes
            .Where(n => n.Support > 0)
            .OrderByDescending(n => n.Support)
            .Take(3)
            .Select(n => new CandidateDriverDto(
                Label: n.DisplayName,
                StrengthPct: Pct(n.Support),
                Description: n.Statement ?? (n.IsSatisfied ? "Resolved with supporting evidence." : "Partially supported."),
                PropositionCount: 0,
                EvidenceBindingCount: 0))
            .ToList();
        if (fromGraph.Count > 0)
            return fromGraph;

        if (selected is null)
            return [];

        return dimensions
            .OrderByDescending(d => d.ValuePct)
            .Take(3)
            .Select(d => new CandidateDriverDto(
                Label: DimensionDriverLabel(d.Label),
                StrengthPct: d.ValuePct,
                Description: DimensionDriverDescription(d.Label),
                PropositionCount: 0,
                EvidenceBindingCount: 0))
            .ToList();
    }

    private static List<CandidateConstraintDto> BuildConstraints(
        DecisionSearchResponse? decision, IReadOnlyList<DecisionGraphNodeDto> graphNodes)
    {
        var constraints = new List<CandidateConstraintDto>();

        foreach (var fp in (decision?.FlipPoints ?? []).OrderByDescending(f => f.WinnerChanges).ThenByDescending(f => f.ChangeCost).Take(3))
        {
            var sev = fp.WinnerChanges ? "HIGH" : "MODERATE";
            constraints.Add(new CandidateConstraintDto(fp.Description, sev, fp.WinnerChanges ? "danger" : "warn"));
        }

        if (constraints.Count == 0)
        {
            foreach (var n in graphNodes.Where(n => n.IsEssential && !n.IsSatisfied).Take(3))
                constraints.Add(new CandidateConstraintDto(n.DisplayName, "UNRESOLVED", "warn"));
        }

        return constraints;
    }

    private static List<CandidateHierarchyNodeDto> BuildHierarchy(IReadOnlyList<DecisionGraphNodeDto> graphNodes)
    {
        // The persisted decision graph is a typed node set; we project each node as a flat hierarchy row
        // (parent linkage is carried by edges, which the cockpit renders separately). Ordering by
        // SortOrder preserves the authoritative presentation order.
        return graphNodes
            .OrderBy(n => n.SortOrder)
            .Select(n => new CandidateHierarchyNodeDto(
                NodeKey: n.NodeCode,
                ParentNodeKey: null,
                Depth: DepthOf(n),
                Label: n.DisplayName,
                StrengthPct: n.Support > 0 ? Pct(n.Support) : null,
                StateCode: n.IsSatisfied ? "RESOLVED" : (n.Support > 0 ? "PARTIAL" : "UNRESOLVED"),
                StateTone: n.IsSatisfied ? "ok" : (n.Support > 0 ? "warn" : "muted"),
                PropositionId: null))
            .ToList();
    }

    private static List<CandidatePropositionDto> BuildPropositions(LegalMatterEvidenceGraphDto? graph)
    {
        if (graph is null || graph.Propositions.Count == 0)
            return [];

        var evidenceById = graph.Evidence.ToDictionary(e => e.LegalEvidenceItemId);
        var result = new List<CandidatePropositionDto>();
        var counter = 0;

        foreach (var p in graph.Propositions.OrderByDescending(p => p.IsDecisionAuthoritative).ThenByDescending(p => p.Confidence ?? 0))
        {
            counter++;
            var edges = new List<CandidatePropositionEvidenceDto>();
            foreach (var s in p.Support)
            {
                evidenceById.TryGetValue(s.LegalEvidenceItemId, out var ev);
                edges.Add(new CandidatePropositionEvidenceDto(
                    EvidenceItemId: s.LegalEvidenceItemId,
                    RelationshipTypeCode: s.RelationshipTypeCode,
                    RelationshipTone: RelationTone(s.RelationshipTypeCode),
                    DocumentFileName: ev?.DocumentFileName ?? "—",
                    PageNumber: ev?.PageNumber,
                    Summary: ev?.Summary,
                    VerificationPct: ev?.Confidence is { } c ? Pct(c) : null));
            }

            result.Add(new CandidatePropositionDto(
                PropositionId: p.LegalFactPropositionId,
                Code: $"P{counter}",
                Text: p.PropositionText,
                StatusCode: p.FactStateCode,
                StatusTone: FactStateTone(p.FactStateCode),
                AprLabel: p.IsDecisionAuthoritative ? "ATOMIC" : "SUPPORTING",
                FactorPath: null,
                Evidence: edges));
        }

        return result;
    }

    private static List<CandidateEvidenceCategoryDto> BuildEvidenceBreakdown(LegalMatterEvidenceGraphDto? graph)
    {
        if (graph is null || graph.Evidence.Count == 0)
            return [];

        // Group support edges by evidence dimension and tally relationship types.
        var evById = graph.Evidence.ToDictionary(e => e.LegalEvidenceItemId);
        var byDimension = new Dictionary<string, (int sup, int con, int qual, int ins)>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in graph.Propositions)
        {
            foreach (var s in p.Support)
            {
                if (!evById.TryGetValue(s.LegalEvidenceItemId, out var ev))
                    continue;
                var dim = string.IsNullOrWhiteSpace(ev.DimensionCode) ? "General" : ev.DimensionCode;
                byDimension.TryGetValue(dim, out var t);
                switch (s.RelationshipTypeCode.ToUpperInvariant())
                {
                    case LegalDocumentRelationshipTypes.Supports: t.sup++; break;
                    case LegalDocumentRelationshipTypes.Contradicts: t.con++; break;
                    case LegalDocumentRelationshipTypes.Qualifies: t.qual++; break;
                    default: t.ins++; break;
                }
                byDimension[dim] = t;
            }
        }

        return byDimension
            .OrderByDescending(kv => kv.Value.sup + kv.Value.con + kv.Value.qual + kv.Value.ins)
            .Select(kv => new CandidateEvidenceCategoryDto(kv.Key, kv.Value.sup, kv.Value.con, kv.Value.qual, kv.Value.ins))
            .ToList();
    }

    private static List<CandidateMaterialEvidenceDto> BuildMaterialEvidence(LegalMatterEvidenceGraphDto? graph)
    {
        if (graph is null)
            return [];

        var evById = graph.Evidence.ToDictionary(e => e.LegalEvidenceItemId);
        var result = new List<CandidateMaterialEvidenceDto>();

        foreach (var p in graph.Propositions)
        {
            foreach (var s in p.Support)
            {
                if (!evById.TryGetValue(s.LegalEvidenceItemId, out var ev))
                    continue;
                // Surface contradictions and qualifications first — they are the most decision-material.
                result.Add(new CandidateMaterialEvidenceDto(
                    EvidenceItemId: ev.LegalEvidenceItemId,
                    DocumentFileName: ev.DocumentFileName,
                    PageNumber: ev.PageNumber,
                    SourceAssertion: ev.PassageText ?? ev.Summary,
                    HrrPath: p.PropositionText,
                    AerRelationshipCode: s.RelationshipTypeCode,
                    AerTone: RelationTone(s.RelationshipTypeCode),
                    VerificationPct: ev.Confidence is { } c ? Pct(c) : null));
            }
        }

        return result
            .OrderBy(m => m.AerRelationshipCode.Equals(LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenByDescending(m => m.VerificationPct ?? 0)
            .Take(8)
            .ToList();
    }

    private static CandidateAuthoritySummaryDto BuildAuthority(
        DecisionSearchResponse? decision, LegalMatterEvidenceGraphDto? graph)
    {
        // Authority is projected from verified evidence items flagged as legal authority. When no
        // authority-typed evidence exists the counts are zero and the UI shows an empty-state.
        var authorityEvidence = (graph?.Evidence ?? [])
            .Where(e => (e.EvidenceTypeCode?.Contains("AUTHORITY", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (e.DimensionCode?.Contains("AUTHORITY", StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        var verified = authorityEvidence.Count(e => string.Equals(e.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase));
        var pending = authorityEvidence.Count - verified;
        var top = authorityEvidence
            .OrderByDescending(e => e.Confidence ?? 0)
            .FirstOrDefault();

        return new CandidateAuthoritySummaryDto(
            TotalCount: authorityEvidence.Count,
            VerifiedCount: verified,
            PendingCount: pending,
            TopAuthorityLabel: top?.Summary,
            TopAuthorityCitation: top?.DocumentFileName,
            RelevantProposition: null,
            CitationVerified: top is not null && string.Equals(top.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase),
            HoldingVerified: top is not null && string.Equals(top.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase),
            AuthorityVerified: top is not null && string.Equals(top.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase),
            PropositionFitLabel: top is not null ? "HIGH" : null,
            EffectLabel: null);
    }

    private static List<CandidateFlipPointDto> BuildFlipPoints(DecisionSearchResponse? decision, DecisionCandidateDto? selected)
    {
        if (decision is null)
            return [];

        return (decision.FlipPoints ?? [])
            .OrderByDescending(f => f.WinnerChanges)
            .ThenByDescending(f => f.ChangeCost)
            .Select(f => new CandidateFlipPointDto(
                Label: f.Description,
                CurrentStateLabel: f.PolarityCode,
                FlipPotentialLabel: f.WinnerChanges ? "HIGH" : "MODERATE",
                Tone: f.WinnerChanges ? "danger" : "warn",
                Rationale: null,
                AffectedCandidates: f.TargetCandidateCode is { } tc && selected is not null
                    ? $"{selected.CandidateCode} ↓  {tc} ↑"
                    : null))
            .ToList();
    }

    private static List<CandidateAdvItemDto> BuildAdv(DecisionSearchResponse? decision)
    {
        if (decision is null)
            return [];

        var items = new List<CandidateAdvItemDto>();
        var rank = 0;

        // The authoritative single next-best-action first (deterministically derived by Core).
        if (decision.NextBestAction is { } nba)
        {
            rank++;
            items.Add(new CandidateAdvItemDto(
                Rank: rank,
                Title: nba.Title,
                Adv: nba.ExpectedInformationValue,
                InformationValue: nba.ExpectedInformationValue,
                DecisionRelevance: nba.ExpectedInformationValue,
                FlipPotential: nba.FlipPotential,
                EstimatedCostLabel: ImpactToCost(nba.ImpactCode),
                WhyItMatters: nba.Rationale,
                AffectedHierarchyPath: null));
        }

        // Remaining frontier branches ordered by ADV score.
        foreach (var b in (decision.Branches ?? [])
            .Where(b => b.IsOnFrontier && b.AdvScore > 0)
            .OrderByDescending(b => b.AdvScore)
            .Take(5))
        {
            rank++;
            items.Add(new CandidateAdvItemDto(
                Rank: rank,
                Title: b.DisplayName,
                Adv: b.AdvScore,
                InformationValue: b.InformationValue,
                DecisionRelevance: b.DecisionRelevance,
                FlipPotential: b.FlipPotential,
                EstimatedCostLabel: "—",
                WhyItMatters: b.Interpretation,
                AffectedHierarchyPath: null));
        }

        return items;
    }

    private static CandidateDocumentIntelligenceDto BuildDocumentIntelligence(
        LegalMatterEvidenceGraphDto? graph, IReadOnlyCollection<MatterChangeEventDto> changeEvents)
    {
        var docCount = graph?.DocumentCount ?? 0;
        var evidence = graph?.Evidence ?? [];
        var propositions = graph?.Propositions ?? [];
        var relevantDocs = evidence.Select(e => e.LegalDocumentId).Distinct().Count();
        var sourceAssertions = graph?.SourceAssertions.Count ?? 0;
        var bindings = propositions.Sum(p => p.Support.Count);
        var verified = evidence.Count(e => string.Equals(e.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase));
        var contradictions = propositions.Sum(p => p.Support.Count(s =>
            string.Equals(s.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase)));

        var latest = changeEvents
            .Where(c => c.LegalDocumentId is not null)
            .OrderByDescending(c => c.CreatedDateUtc)
            .FirstOrDefault();

        return new CandidateDocumentIntelligenceDto(
            MatterDocumentCount: docCount,
            RelevantDocumentCount: relevantDocs,
            SourceAssertionCount: sourceAssertions,
            PropositionBindingCount: bindings,
            VerifiedEvidenceCount: verified,
            ContradictionCount: contradictions,
            LatestDocumentFileName: latest?.SourceLabel,
            LatestDocumentPage: null,
            LatestAssertionCode: null,
            LatestPropositionCode: null,
            LatestAerCode: latest?.ClassificationCode,
            LatestDecisionEffect: latest is not null
                ? $"{latest.AffectedCandidateCount} candidates · {latest.AffectedPropositionCount} propositions"
                : null);
    }

    private static List<CandidateUncertaintyBandDto> BuildUncertaintyMap(
        DecisionCandidateDto? selected, IReadOnlyList<CandidateDimensionDto> dimensions)
    {
        if (selected is null)
            return [];

        // Uncertainty per dimension is the inverse of support strength (lower support => higher uncertainty).
        return dimensions
            .Select(d =>
            {
                var unc = 100 - d.ValuePct;
                return new CandidateUncertaintyBandDto(d.Label, unc, UncertaintyWord(unc), ToneForUncertainty(unc));
            })
            .OrderByDescending(b => b.LevelPct)
            .ToList();
    }

    private static List<CandidateChangeHistoryDto> BuildChangeHistory(IReadOnlyCollection<MatterChangeEventDto> changeEvents)
    {
        return changeEvents
            .OrderBy(c => c.CreatedDateUtc)
            .Select(c => new CandidateChangeHistoryDto(
                WhenUtc: c.ProcessedDateUtc ?? c.CreatedDateUtc,
                Label: c.Summary ?? c.ClassificationCode ?? c.ChangeSourceCode,
                ScorePct: null,
                DeltaPts: null,
                Tone: c.AffectedCandidateCount > 0 ? "warn" : "muted"))
            .ToList();
    }

    private static CandidateReadinessDto BuildReadiness(DecisionSearchResponse? decision, string readinessLabel)
    {
        var metrics = new List<CandidateReadinessMetricDto>();
        var blockers = new List<string>();

        if (decision is not null)
        {
            foreach (var item in decision.Readiness)
            {
                metrics.Add(new CandidateReadinessMetricDto(
                    Label: item.Label,
                    Value: item.Satisfied ? "Satisfied" : "Attention",
                    Pct: null,
                    Tone: item.Satisfied ? "ok" : "warn"));
                if (!item.Satisfied)
                    blockers.Add(item.Detail ?? item.Label);
            }

            if (decision.ReadinessVerdict is { } verdict)
            {
                foreach (var b in verdict.Blockers)
                    if (!blockers.Contains(b))
                        blockers.Add(b);
            }
        }

        var satisfied = metrics.Count(m => m.Tone == "ok");
        var overall = metrics.Count == 0 ? 0 : (int)Math.Round(satisfied * 100.0 / metrics.Count);

        return new CandidateReadinessDto(readinessLabel, overall, metrics, blockers);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static int Pct(decimal value)
    {
        // Composite / support values are stored in [0,1]; project to a 0–100 percentage.
        var v = value <= 1m ? value * 100m : value;
        return (int)Math.Round(Math.Clamp(v, 0m, 100m), MidpointRounding.AwayFromZero);
    }

    private static int DepthOf(DecisionGraphNodeDto n)
        => string.IsNullOrEmpty(n.NodeCode) ? 1 : Math.Max(1, n.NodeCode.Count(c => c == '.') + 1);

    private static string UncertaintyBand(decimal uncertainty)
    {
        var v = uncertainty <= 1m ? uncertainty * 100m : uncertainty;
        return v switch
        {
            >= 66 => "HIGH",
            >= 33 => "MODERATE",
            _ => "LOW"
        };
    }

    private static string UncertaintyWord(int uncertaintyPct) => uncertaintyPct switch
    {
        >= 80 => "VERY HIGH",
        >= 60 => "HIGH",
        >= 40 => "MODERATE",
        >= 20 => "LOW",
        _ => "VERY LOW"
    };

    private static string ToneForPct(int pct) => pct switch
    {
        >= 75 => "ok",
        >= 50 => "warn",
        _ => "danger"
    };

    private static string ToneForUncertainty(int uncPct) => uncPct switch
    {
        >= 60 => "danger",
        >= 40 => "warn",
        _ => "ok"
    };

    private static string RelationTone(string relationshipTypeCode) => relationshipTypeCode.ToUpperInvariant() switch
    {
        LegalDocumentRelationshipTypes.Supports => "ok",
        LegalDocumentRelationshipTypes.Contradicts => "danger",
        LegalDocumentRelationshipTypes.Qualifies => "warn",
        _ => "muted"
    };

    private static string FactStateTone(string factStateCode) => factStateCode.ToUpperInvariant() switch
    {
        LegalFactStates.Established => "ok",
        LegalFactStates.Supported => "ok",
        LegalFactStates.Disputed => "danger",
        LegalFactStates.Invalidated => "danger",
        _ => "warn"
    };

    private static string ImpactToCost(string impactCode) => impactCode.ToUpperInvariant() switch
    {
        "VERY HIGH" or "HIGH" => "Low",
        "MEDIUM" => "Medium",
        _ => "—"
    };

    private static string DimensionDriverLabel(string dimension) => dimension switch
    {
        "LEGAL" => "Legal Strength",
        "FACT" => "Factual Support",
        "EVIDENCE" => "Evidence Support",
        "AUTHORITY" => "Authority Support",
        "VERIFICATION" => "Verification",
        _ => dimension
    };

    private static string DimensionDriverDescription(string dimension) => dimension switch
    {
        "LEGAL" => "Strong legal theory alignment.",
        "FACT" => "Factual record substantially supported.",
        "EVIDENCE" => "Supported by verified evidence.",
        "AUTHORITY" => "Supported by verified legal authority.",
        "VERIFICATION" => "Evidence independently verified.",
        _ => string.Empty
    };
}
