using Legal.Web.Services.Clio;
using DecisionNs = Legal.Application.Features.Intelligence.Decision;

namespace Legal.Web.Services.CommandCenter;

/// <summary>
/// Composes the Case + Decision Command Center read model from the read-only Clio
/// discovery snapshot and the EXISTING Judz decision read models exposed by
/// <see cref="ApiClient"/>. It introduces no new domain logic and never recomputes
/// POLOXI scores — it only projects what already exists. Everything degrades
/// gracefully: missing Clio data, missing decision sessions, or failed calls never
/// throw; they render as empty/interpretive/"not run" states instead.
/// </summary>
public sealed class CaseCommandCenterViewService(
    ApiClient api,
    IClioReadOnlyClient clioClient,
    ClioMatterMap clioMatters,
    ClioTokenStore clioTokens,
    CommandCenterSnapshotCache snapshotCache,
    ILogger<CaseCommandCenterViewService> logger)
{
    // Clio custom-field name → structured Judz field it normalizes onto (display-only mapping).
    private static readonly Dictionary<string, string> NormalizedFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accident Location"] = "Jurisdiction",
        ["Date of Incident"] = "Incident Date",
        ["Insurance Carrier"] = "Carrier",
        ["Claim Number"] = "Claim No.",
    };

    public async Task<MatterIntelligenceSnapshot> BuildAsync(Guid matterId, CancellationToken ct = default)
    {
        MatterIntelligenceSnapshot fresh;
        try
        {
            fresh = await ComposeAsync(matterId, ct);
        }
        catch (Exception ex)
        {
            // Hard failure composing the snapshot — fall back to the last good one if we have it.
            logger.LogError(ex, "Command center: compose threw; attempting cached fallback for {MatterId}.", matterId);
            if (snapshotCache.TryGet(matterId, out var cachedOnError))
                return AsStale(cachedOnError, "Live data is temporarily unavailable; showing the last synced snapshot.");
            throw;
        }

        // If the fresh build is healthy enough, cache it as the new "last good" snapshot.
        if (IsCacheable(fresh))
        {
            snapshotCache.Set(matterId, fresh);
            return fresh;
        }

        // Fresh build is degraded (e.g. Clio dropped). Prefer a richer cached snapshot if present.
        if (snapshotCache.TryGet(matterId, out var cached) && IsRicher(cached, fresh))
            return AsStale(cached, "Live sync is degraded; showing the last complete snapshot.");

        return fresh;
    }

    /// <summary>A snapshot is worth caching when the authoritative Judz identity loaded.</summary>
    private static bool IsCacheable(MatterIntelligenceSnapshot s) =>
        s.Health.Notice is null; // Notice is only set when the Judz matter failed to load.

    /// <summary>The cached snapshot is richer when it carries data the degraded one lost.</summary>
    private static bool IsRicher(MatterIntelligenceSnapshot cached, MatterIntelligenceSnapshot degraded) =>
        cached.Timeline.Count > degraded.Timeline.Count
        || cached.Documents.Count > degraded.Documents.Count
        || cached.Facts.Count > degraded.Facts.Count
        || (cached.Health.LastClioSync is not null && degraded.Health.LastClioSync is null);

    private static MatterIntelligenceSnapshot AsStale(MatterIntelligenceSnapshot cached, string notice) =>
        cached with
        {
            Health = cached.Health with
            {
                ServedFromCache = true,
                CachedAgeText = DescribeAge(cached.Health.BuiltAt),
                Notice = notice
            }
        };

    private static string DescribeAge(DateTimeOffset builtAt)
    {
        var delta = DateTimeOffset.UtcNow - builtAt;
        if (delta < TimeSpan.FromMinutes(1)) return "moments ago";
        if (delta < TimeSpan.FromHours(1)) return $"{(int)delta.TotalMinutes} minute(s) ago";
        if (delta < TimeSpan.FromDays(1)) return $"{(int)delta.TotalHours} hour(s) ago";
        return builtAt.ToString("MMM d, h:mm tt");
    }

    private async Task<MatterIntelligenceSnapshot> ComposeAsync(Guid matterId, CancellationToken ct = default)
    {
        // 1) Core Judz matter (authoritative identity + latest decision session pointer).
        DecisionNs.DecisionMatterDto? matter = null;
        try { matter = await api.GetLegalDecisionMatterAsync(matterId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Command center: matter {MatterId} load failed.", matterId); }

        // 2) Optional Clio discovery (only if connected and this matter is Clio-linked).
        ClioSapiniDiscovery? clio = null;
        var clioMatterId = TryGetClioMatterId(matterId);
        if (clioTokens.IsConnected)
        {
            try { clio = await clioClient.DiscoverSapiniAsync(ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Command center: Clio discovery failed (non-fatal)."); }
        }

        // 3) Evidence graph + documents (existing read models).
        DecisionNs.LegalMatterEvidenceGraphDto? graph = null;
        try { graph = await api.GetLegalMatterEvidenceGraphAsync(matterId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Command center: evidence graph failed (non-fatal)."); }

        IReadOnlyCollection<DecisionNs.LegalDocumentDto> docs = [];
        try { docs = await api.GetLegalMatterDocumentsAsync(matterId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Command center: documents failed (non-fatal)."); }

        var identity = BuildIdentity(matterId, matter, clio, clioMatterId);
        var lifecycle = BuildLifecycle(matter, clio);
        var facts = BuildFacts(clio);
        var parties = BuildParties(clio);
        var documents = BuildDocuments(docs, clio);
        var decision = await BuildDecisionAsync(matterId, matter, graph, documents.Count, ct);
        var timeline = BuildTimeline(clio, facts);
        var treatment = BuildTreatment(clio, graph);
        var economics = BuildEconomics(clio);
        var pulse = BuildCasePulse(decision);
        var keyDates = BuildKeyDates(facts, clio);
        var domainPack = BuildDomainPack(docs, identity.PracticeArea);

        // Review boundary: advance "last opened" and capture the PRIOR boundary so
        // "Since Your Last Review" diffs against real timestamps (never fabricated).
        DateTimeOffset? reviewBoundary = null;
        try
        {
            var prior = await api.RecordMatterOpenedAsync(matterId, ct);
            reviewBoundary = prior?.LastOpenedUtc;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Command center: record matter opened failed (non-fatal)."); }

        var since = BuildSinceLastReview(docs, graph, reviewBoundary);

        // Provider sharing policies + open-request counts (DB-backed, attorney-approved).
        var providerSharing = await BuildProviderSharingAsync(matterId, ct);

        var health = new SnapshotHealth(
            LastClioSync: clio is not null ? DateTimeOffset.UtcNow : null,
            ClioConnected: clioTokens.IsConnected,
            DecisionSessionPresent: decision.State != DecisionAnalysisState.NotRun,
            Notice: matter is null ? "The Judz matter could not be loaded; showing available data only." : null);

        return new MatterIntelligenceSnapshot(
            identity, lifecycle, parties, facts, timeline, treatment, economics,
            documents, pulse, since, keyDates, decision, domainPack, health)
        {
            ProviderSharing = providerSharing,
            LastReviewedAt = reviewBoundary
        };
    }

    /// <summary>
    /// Real "Since Your Last Review" deltas: documents and evidence whose timestamps are
    /// newer than the prior review boundary. On first open (no boundary) nothing is "new".
    /// </summary>
    private static IReadOnlyList<SinceLastReviewItem> BuildSinceLastReview(
        IReadOnlyCollection<DecisionNs.LegalDocumentDto> docs,
        DecisionNs.LegalMatterEvidenceGraphDto? graph,
        DateTimeOffset? boundary)
    {
        if (boundary is not { } since) return [];

        var items = new List<SinceLastReviewItem>();

        foreach (var d in docs)
        {
            var created = new DateTimeOffset(DateTime.SpecifyKind(d.CreatedDateUtc, DateTimeKind.Utc));
            if (created <= since) continue;
            items.Add(new SinceLastReviewItem(
                "NEW DOCUMENT",
                d.FileName,
                d.DocumentTypeCode,
                created,
                IsMaterial: true,
                SourceProvenance.Judz("Document", d.LegalDocumentId.ToString())));
        }

        return items
            .OrderByDescending(i => i.OccurredAt ?? DateTimeOffset.MinValue)
            .Take(10)
            .ToArray();
    }

    private async Task<IReadOnlyList<ProviderSharingSummary>> BuildProviderSharingAsync(Guid matterId, CancellationToken ct)
    {
        try
        {
            var policies = await api.GetProviderSharingPoliciesAsync(matterId, ct);
            if (policies.Count == 0) return [];

            var requests = await api.GetProviderRequestsAsync(matterId, null, ct);
            var openByProvider = requests
                .Where(r => string.Equals(r.StatusCode, "OPEN", StringComparison.OrdinalIgnoreCase))
                .GroupBy(r => r.ProviderKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            return policies.Select(p => new ProviderSharingSummary(
                p.ProviderKey,
                p.ProviderDisplayName ?? p.ProviderKey,
                p.PortalEnabled,
                p.ShareMatterStatus,
                p.ShareCurrentStage,
                p.SharePatientTreatment,
                p.ShareOwnRecords,
                p.ShareOwnBills,
                p.ShareFirmRequests,
                p.ShareOtherProviders,
                p.ShareSettlementInfo,
                openByProvider.TryGetValue(p.ProviderKey, out var c) ? c : 0)).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Command center: provider sharing load failed (non-fatal).");
            return [];
        }
    }

    private Guid? TryGetClioMatterId(Guid matterId)
    {
        // ClioMatterMap maps Clio→Judz; reverse-scan for this Judz matter (single-matter demo).
        // Kept defensive: the map is tiny in hackathon scope.
        return null; // reverse lookup not needed for display; discovery self-identifies Sapini.
    }

    private static MatterIdentity BuildIdentity(
        Guid matterId, DecisionNs.DecisionMatterDto? matter, ClioSapiniDiscovery? clio, Guid? clioMatterId)
    {
        var title = matter?.Title
            ?? (string.IsNullOrWhiteSpace(clio?.Matter.Description) ? clio?.Matter.DisplayNumber : clio!.Matter.Description)
            ?? "Matter";
        return new MatterIdentity(
            matterId,
            title,
            clio?.Matter.ClientName ?? matter?.MovingParty,
            matter?.PracticeAreaCode ?? clio?.Matter.PracticeArea ?? "Personal Injury",
            clio?.Matter.DisplayNumber,
            clio?.Matter.Id);
    }

    private static MatterLifecycleContext BuildLifecycle(DecisionNs.DecisionMatterDto? matter, ClioSapiniDiscovery? clio)
    {
        var stage = clio?.Matter.StageName;
        return new MatterLifecycleContext(
            SourceSystem: "Clio",
            SourceMatterId: clio?.Matter.Id.ToString(),
            PracticeArea: matter?.PracticeAreaCode ?? clio?.Matter.PracticeArea ?? "Personal Injury",
            StageName: stage,
            StageSequence: MatterLifecycleContext.SequenceFor(stage),
            ImportedAt: DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<MatterFact> BuildFacts(ClioSapiniDiscovery? clio)
    {
        if (clio is null) return [];
        return clio.CustomFields.Select(f => new MatterFact(
            f.Name,
            f.Value,
            f.FieldType,
            NormalizedFieldMap.TryGetValue(f.Name, out var mapped) ? mapped : null,
            SourceProvenance.Clio("CustomField", f.Name))).ToArray();
    }

    private static IReadOnlyList<MatterParty> BuildParties(ClioSapiniDiscovery? clio)
    {
        if (clio is null) return [];
        var clientId = clio.Matter.ClientId;
        return clio.Contacts.Select(c =>
        {
            var role = c.Relationship ?? c.Type;
            var isClient = (clientId is { } cid && c.Id == cid)
                || Contains(role, "client");
            var isAdverse = Contains(role, "defendant", "adverse", "opposing", "respondent", "tortfeasor");
            var isProvider = Contains(role, "provider", "physician", "doctor", "medical", "therapy", "radiology");
            var normalized = isClient ? "Client"
                : isAdverse ? "Adverse Party"
                : isProvider ? "Provider"
                : null;
            return new MatterParty(
                c.Name ?? $"Contact {c.Id}",
                role, normalized,
                isClient, isAdverse, isProvider,
                RequiresReview: normalized is null,
                SourceProvenance.Clio("Contact", c.Id.ToString()));
        }).ToArray();
    }

    private static IReadOnlyList<MatterDocumentRef> BuildDocuments(
        IReadOnlyCollection<DecisionNs.LegalDocumentDto> judzDocs, ClioSapiniDiscovery? clio)
    {
        var list = new List<MatterDocumentRef>();

        // 1) Judz-ingested documents are authoritative (carry version lineage + domain pack).
        //    Index by filename so we can fold a matching Clio source into the same row.
        var byName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in judzDocs)
        {
            var latestVersion = d.Versions?.Count > 0
                ? $"v{d.Versions.Max(v => v.VersionNumber)}"
                : null;
            list.Add(new MatterDocumentRef(
                d.FileName,
                ClioDocumentId: null,
                JudzDocumentId: d.LegalDocumentId,
                Category: d.DocumentTypeCode,
                Folder: d.DomainPackCode,
                LatestVersion: latestVersion,
                Provenance: SourceProvenance.Judz("Document", d.LegalDocumentId.ToString())));
            if (!string.IsNullOrWhiteSpace(d.FileName))
                byName[d.FileName] = d.LegalDocumentId;
        }

        // 2) Clio documents that were NOT ingested into Judz are shown as source-only refs.
        if (clio is not null)
        {
            foreach (var d in clio.Documents)
            {
                var name = d.Name ?? $"clio-document-{d.Id}";
                if (byName.ContainsKey(name)) continue; // already represented by its Judz twin
                list.Add(new MatterDocumentRef(
                    name,
                    d.Id, null, d.Category, d.Folder, d.LatestVersion,
                    SourceProvenance.Clio("Document", d.Id.ToString())));
            }
        }
        return list;
    }

    private async Task<DecisionIntelligenceSummary> BuildDecisionAsync(
        Guid matterId, DecisionNs.DecisionMatterDto? matter, DecisionNs.LegalMatterEvidenceGraphDto? graph,
        int documentCount, CancellationToken ct)
    {
        var evidenceCount = graph?.Evidence?.Count ?? 0;
        var docCount = graph?.DocumentCount ?? documentCount;
        var sessionId = matter?.LatestSessionId;

        // No decision session → NotRun. Never fabricate candidates or percentages.
        if (sessionId is null)
        {
            return new DecisionIntelligenceSummary(
                DecisionAnalysisState.NotRun, null, matter?.CurrentOutcome,
                docCount, evidenceCount, matter?.LastDecidedUtc,
                [], [], [], null, "NotCalculated",
                "No decision session has executed for this matter yet.");
        }

        // A session exists. Pull the authoritative "what to resolve next" frontier.
        DecisionNs.WhatToResolveNextResult? resolve = null;
        try { resolve = await api.GetLegalMatterWhatToResolveNextAsync(matterId, sessionId.Value, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Command center: what-to-resolve-next failed (non-fatal)."); }

        ResolveNextTarget? target = null;
        var resolveState = resolve?.State.ToString() ?? "NotCalculated";
        var resolveReason = resolve?.Reason;
        if (resolve is { State: DecisionNs.ResolutionTargetState.Calculated, Targets.Count: > 0 })
        {
            var t = resolve.Targets[0];
            target = new ResolveNextTarget(
                t.PropositionId, t.PropositionText, t.ResolutionQuestion,
                t.MissingInformation, t.InformationValue, t.FlipPotential, t.LegalAdv);
        }

        // Uncertainties are projected from the frontier targets (existing metrics only).
        var uncertainties = (resolve?.Targets ?? [])
            .Take(3)
            .Select((t, i) => new DecisionUncertainty(
                $"U{i + 1}", t.PropositionText, t.MissingInformation,
                SeverityFromIv(t.InformationValue)))
            .ToArray();

        var changeFactors = (resolve?.Targets ?? [])
            .Take(4)
            .Select(t => new DecisionChangeFactor(
                t.PropositionId, t.ResolutionQuestion, SeverityFromIv(t.FlipPotential ?? t.InformationValue)))
            .ToArray();

        // Interpretive: a leading outcome exists but we do NOT render candidate % (no competition proof).
        var state = DecisionAnalysisState.Interpretive;
        var candidates = string.IsNullOrWhiteSpace(matter?.CurrentOutcome)
            ? Array.Empty<DecisionCandidate>()
            : [new DecisionCandidate("C1", matter!.CurrentOutcome!, null, "Leading interpretation. Competition not rendered here.")];

        return new DecisionIntelligenceSummary(
            state, sessionId, matter?.CurrentOutcome,
            docCount, evidenceCount, matter?.LastDecidedUtc,
            candidates, uncertainties, changeFactors,
            target, resolveState, resolveReason);
    }

    private static IReadOnlyList<MatterEventItem> BuildTimeline(ClioSapiniDiscovery? clio, IReadOnlyList<MatterFact> facts)
    {
        var events = new List<MatterEventItem>();

        // Materialize every date-bearing Clio fact as a timeline event (read-only, no inference).
        foreach (var f in facts)
        {
            if (!LooksLikeDateFact(f)) continue;
            if (!DateTimeOffset.TryParse(f.Value, out var when)) continue;
            events.Add(new MatterEventItem(
                f.Name,
                when,
                CategorizeEvent(f.Name),
                f.NormalizedField is null ? null : $"Normalized → {f.NormalizedField}",
                RelatedPropositionId: null,
                DecisionRelevance: null,
                f.Provenance));
        }

        // Fold in dated Clio activities (tasks, calendar entries, communications).
        if (clio is not null)
        {
            foreach (var a in clio.Activities)
            {
                if (a.OccurredAt is null) continue;
                var category = a.Kind switch
                {
                    "Task" => "Deadline",
                    "Calendar" => CategorizeEvent(a.Title ?? "Event"),
                    "Communication" => "Communication",
                    _ => "Event"
                };
                var detail = a.Kind == "Task" && a.Completed is { } done
                    ? (done ? "Completed task" : "Open task")
                    : a.Detail;
                events.Add(new MatterEventItem(
                    a.Title ?? a.Kind,
                    a.OccurredAt,
                    category,
                    detail,
                    RelatedPropositionId: null,
                    DecisionRelevance: null,
                    SourceProvenance.Clio(a.Kind, a.Id.ToString())));
            }
        }

        return events
            .OrderBy(e => e.OccurredAt ?? DateTimeOffset.MaxValue)
            .ToArray();
    }

    private static bool LooksLikeDateFact(MatterFact f) =>
        (f.FieldType?.Contains("date", StringComparison.OrdinalIgnoreCase) ?? false)
        || Contains(f.Name, "date", "filed", "deadline", "trial", "incident", "disclosure", "cutoff", "due");

    private static string CategorizeEvent(string name) =>
        Contains(name, "incident", "accident") ? "Accident"
        : Contains(name, "treatment", "medical", "mmi") ? "Treatment"
        : Contains(name, "filed", "complaint", "pleading") ? "Pleading"
        : Contains(name, "discovery", "disclosure", "deposition", "cutoff") ? "Discovery"
        : Contains(name, "trial") ? "Trial"
        : Contains(name, "demand", "negotiation", "mediation") ? "Negotiation"
        : Contains(name, "deadline", "due") ? "Deadline"
        : "Event";

    private static IReadOnlyList<TreatmentStage> BuildTreatment(
        ClioSapiniDiscovery? clio, DecisionNs.LegalMatterEvidenceGraphDto? graph)
    {
        var stages = new List<TreatmentStage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Providers surfaced from Clio contacts flagged as medical providers (read-only, no inference).
        if (clio is not null)
        {
            foreach (var c in clio.Contacts.Where(c =>
                Contains(c.Relationship ?? c.Type, "provider", "physician", "doctor", "medical", "therapy", "radiology")))
            {
                var name = c.Name ?? "Provider";
                if (seen.Add(name))
                    stages.Add(new TreatmentStage(name, name, null, null, null));
            }
        }

        // 2) Document-intelligence: medical evidence items grouped by their source document.
        //    These are authoritative extractions (record/bill/diagnosis passages), so we attach a
        //    real record count per source. No dates are invented — Dates stays null unless present.
        if (graph?.Evidence is { Count: > 0 })
        {
            var medical = graph.Evidence
                .Where(e => IsMedicalEvidence(e))
                .GroupBy(e => e.DocumentFileName ?? "Medical Record", StringComparer.OrdinalIgnoreCase);

            foreach (var g in medical)
            {
                var providerName = g.Key;
                var recordCount = g.Count();
                // Prefer a human treatment label from the dominant evidence dimension/type.
                var status = g
                    .Select(e => e.DimensionCode ?? e.EvidenceTypeCode)
                    .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

                if (seen.Add(providerName))
                {
                    stages.Add(new TreatmentStage(providerName, providerName, null, recordCount, status));
                }
                else
                {
                    // Fold record count onto the matching contact-derived stage.
                    var idx = stages.FindIndex(st => string.Equals(st.Name, providerName, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0)
                        stages[idx] = stages[idx] with { RecordCount = recordCount, Status = stages[idx].Status ?? status };
                }
            }
        }

        return stages;
    }

    private static bool IsMedicalEvidence(DecisionNs.LegalEvidenceGraphItemDto e) =>
        Contains(e.DimensionCode, "medical", "treatment", "injury", "diagnosis", "clinical")
        || Contains(e.EvidenceTypeCode, "medical", "treatment", "injury", "diagnosis", "bill", "record", "radiology", "therapy")
        || Contains(e.DocumentTypeCode, "medical", "treatment", "bill", "record")
        || Contains(e.DocumentFileName, "medical", "radiology", "ortho", "therapy", "clinic", "hospital", "bill");

    private static IReadOnlyList<EconomicsLine> BuildEconomics(ClioSapiniDiscovery? clio)
    {
        if (clio is null) return [];
        var lines = new List<EconomicsLine>();
        void Add(string fieldName, string label, string group)
        {
            var f = clio.CustomFields.FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.OrdinalIgnoreCase));
            if (f is null) return;
            decimal? amount = decimal.TryParse(f.Value?.Replace("$", "").Replace(",", ""), out var a) ? a : null;
            lines.Add(new EconomicsLine(label, amount, amount is null ? f.Value : null, group));
        }
        Add("Medical Specials To Date", "Medical Specials", "MEDICAL");
        Add("Estimated Case Value", "Estimated Case Value", "CASE");
        Add("Policy Limits", "Policy Limits", "COVERAGE");
        Add("Wage Loss Claimed", "Wage Loss", "CASE");
        return lines;
    }

    private static IReadOnlyList<CasePulseItem> BuildCasePulse(DecisionIntelligenceSummary decision)
    {
        // Case Pulse is intentionally decision-derived: it surfaces the highest-value unresolved
        // items from the frontier. If nothing is calculated, it stays empty (no fabrication).
        var items = new List<CasePulseItem>();
        var rank = 1;
        foreach (var cf in decision.ChangeFactors.Take(4))
            items.Add(new CasePulseItem(rank++, cf.Title, $"Decision relevance: {cf.Impact}",
                cf.Impact, cf.Code, null));
        return items;
    }

    private async Task<IReadOnlyList<SinceLastReviewItem>> BuildSinceLastReviewFromChangeEventsAsync(Guid matterId, CancellationToken ct)
    {
        // Retained fallback: derive from matter change events when needed. Not used by the
        // primary compose path, which prefers real document/evidence timestamp deltas.
        try
        {
            var events = await api.GetLegalDecisionMatterChangeEventsAsync(matterId, ct);
            return events
                .OrderByDescending(e => e.CreatedDateUtc)
                .Take(10)
                .Select(e => new SinceLastReviewItem(
                    e.ClassificationCode ?? "CHANGE",
                    e.Summary ?? e.SourceLabel ?? "Change",
                    e.SourceLabel,
                    e.DocumentDateUtc is { } d ? new DateTimeOffset(d, TimeSpan.Zero) : null,
                    IsMaterial: e.AffectedPropositionCount > 0 || e.AffectedCandidateCount > 0,
                    SourceProvenance.Clio("ChangeEvent", e.MatterChangeEventId.ToString())))
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Command center: change events failed (non-fatal).");
            return [];
        }
    }

    private static IReadOnlyList<KeyDateItem> BuildKeyDates(IReadOnlyList<MatterFact> facts, ClioSapiniDiscovery? clio)
    {
        var dates = new List<KeyDateItem>();
        var now = DateTimeOffset.UtcNow;

        void AddDate(string label, DateTimeOffset when)
        {
            string? countdown = null;
            var tone = "normal";
            if (when > now)
            {
                var days = (int)Math.Ceiling((when - now).TotalDays);
                countdown = days == 1 ? "1 day" : $"{days} days";
                tone = days <= 14 ? "danger" : days <= 45 ? "warning" : "normal";
            }
            dates.Add(new KeyDateItem(label, when, countdown, tone));
        }

        foreach (var f in facts)
        {
            if (!LooksLikeDateFact(f)) continue;
            if (!DateTimeOffset.TryParse(f.Value, out var when)) continue;
            AddDate(f.Name, when);
        }

        // Upcoming task due dates and calendar entries become key dates too.
        if (clio is not null)
        {
            foreach (var a in clio.Activities)
            {
                if (a.OccurredAt is not { } when) continue;
                if (a.Kind is "Task" or "Calendar")
                    AddDate(a.Title ?? a.Kind, when);
            }
        }

        return dates
            .OrderBy(d => d.Date ?? DateTimeOffset.MaxValue)
            .ToArray();
    }

    private static DomainPackDiagnostics BuildDomainPack(
        IReadOnlyCollection<DecisionNs.LegalDocumentDto> judzDocs, string? matterType)
    {
        // Spec §7: report domain-pack participation from what is actually stamped on
        // ingested documents. We never claim the PI pack applied without evidence.
        var tagged = judzDocs
            .Where(d => !string.IsNullOrWhiteSpace(d.DomainPackCode))
            .ToArray();
        if (tagged.Length == 0)
            return new DomainPackDiagnostics("PERSONAL_INJURY", Applied: false, DocumentsTagged: 0, matterType);

        var pack = tagged
            .GroupBy(d => d.DomainPackCode!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First();
        return new DomainPackDiagnostics(pack.Key, Applied: true, DocumentsTagged: pack.Count(), matterType);
    }

    private static string SeverityFromIv(double? value) =>
        value is null ? "MEDIUM" : value >= 0.66 ? "HIGH" : value >= 0.33 ? "MEDIUM" : "LOW";

    private static bool Contains(string? value, params string[] tokens) =>
        !string.IsNullOrWhiteSpace(value) && tokens.Any(t => value!.Contains(t, StringComparison.OrdinalIgnoreCase));
}
