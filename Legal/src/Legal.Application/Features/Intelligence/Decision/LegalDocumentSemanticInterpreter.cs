using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class LegalDocumentSemanticInterpreter(IAiProviderRouter aiRouter) : ILegalDocumentSemanticInterpreter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // Safe ceiling for the combined system+user prompt sent to a governed semantic-extraction call. It sits
    // below the configured Intelligence.Safety.MaximumInputCharacters guard (60000 as of migration 0194) with
    // headroom so batching never trips the safety violation, while staying inside the model input token budget.
    private const int InputCharacterCeiling = 58000;
    // Reserve for the JSON envelope around the passages section (matter/document ids, property names, braces).
    private const int PromptEnvelopeReserve = 2000;
    // Per-passage JSON overhead (passageId, page, sectionPath, field names, quoting) added to each passage's text.
    private const int PerPassageEnvelope = 200;
    // A single passage is never dropped even if it alone exceeds the computed budget; keep a sane floor so the
    // budget is always positive when the fixed sections are unusually large.
    private const int MinimumPassageCharacterBudget = 4000;
    public async Task<LegalDocumentSemanticProposal> InterpretAsync(
        Guid tenantId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        string? domainPackCode,
        IReadOnlyCollection<DecisionDomainConceptDto> domainConcepts,
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        string correlationId,
        string? modelCodeOverride = null,
        ResolvedDomainPack? resolvedPack = null,
        CancellationToken cancellationToken = default)
    {
        if (passages.Count == 0)
            return EmptyProposal();

        var concepts = domainConcepts
            .OrderBy(concept => concept.SortOrder)
            .Select(concept => new
            {
                concept.ConceptCode,
                concept.DimensionCode,
                concept.Name,
                concept.Description,
                concept.VerificationProfileCode
            })
            .ToArray();
        var entityTypes = (resolvedPack?.EntityTypes ?? [])
            .OrderBy(entity => entity.SortOrder)
            .Select(entity => new { entity.EntityTypeCode, entity.DimensionCode, entity.Name, entity.Description })
            .ToArray();
        var eventTypes = (resolvedPack?.EventTypes ?? [])
            .OrderBy(evt => evt.SortOrder)
            .Select(evt => new { evt.EventTypeCode, evt.DimensionCode, evt.Name, evt.Description })
            .ToArray();
        var resolvedModelOverride = string.IsNullOrWhiteSpace(modelCodeOverride) ? null : modelCodeOverride.Trim();

        // Input budget: the tenant AI safety guard (Intelligence.Safety.MaximumInputCharacters) rejects any
        // single request whose systemPrompt+userPrompt exceeds the configured maximum. A large medical/expert
        // PDF produces far more passage text than that ceiling, so sending all passages in one shot fails the
        // whole document ("The AI request exceeded the configured maximum input length."). Instead the passages
        // are split into batches that each stay under a safe input ceiling (headroom reserved for the system
        // prompt and the fixed concept/entity/event sections), extracted independently, then merged. Proposal
        // keys are namespaced per batch so keys never collide across batches, and Govern runs once against the
        // full passage set so passage-span admission and Domain Pack binding are unchanged.
        var fixedSections = JsonSerializer.Serialize(new { DomainConcepts = concepts, DomainEntityTypes = entityTypes, DomainEventTypes = eventTypes });
        var perRequestPassageBudget = Math.Max(
            MinimumPassageCharacterBudget,
            InputCharacterCeiling - SystemPrompt.Length - fixedSections.Length - PromptEnvelopeReserve);

        var batches = BatchPassages(passages, perRequestPassageBudget);
        var merged = EmptyProposal();
        var batchIndex = 0;
        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePassages = batch.Select(passage => new
            {
                PassageId = passage.LegalDocumentPassageId,
                passage.PageNumber,
                passage.SectionPath,
                passage.Text
            });
            var userPrompt = JsonSerializer.Serialize(new
            {
                MatterId = matterId,
                DocumentId = documentId,
                DocumentVersionId = documentVersionId,
                DomainPackCode = domainPackCode,
                DomainConcepts = concepts,
                DomainEntityTypes = entityTypes,
                DomainEventTypes = eventTypes,
                Passages = sourcePassages
            });
            var result = await aiRouter.GenerateAsync(
                tenantId,
                "LEGAL_DOCUMENT_SEMANTIC_EXTRACTION",
                SystemPrompt,
                userPrompt,
                OutputSchemaJson,
                correlationId,
                new AiExecutionContext("LEGAL_DOCUMENT_INTELLIGENCE", "LEGAL_DOCUMENT", documentId, null, "MATTER_DOCUMENT", documentVersionId, null, null),
                modelCodeOverride: resolvedModelOverride,
                cancellationToken: cancellationToken);
            var batchProposal = Parse(result.StructuredOutputJson ?? result.Content) ?? EmptyProposal();
            merged = Merge(merged, NamespaceProposalKeys(batchProposal, batchIndex));
            batchIndex++;
        }

        return Govern(merged, domainConcepts, passages, resolvedPack);
    }

    // A legal document version can carry far more passage text than one governed AI request may hold, so the
    // passages are grouped into batches that each stay under the per-request character budget. At least one
    // passage is always placed in a batch (even a single oversized passage) so no passage is silently dropped.
    private static List<List<LegalDocumentPassageDto>> BatchPassages(
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        int perRequestPassageBudget)
    {
        var batches = new List<List<LegalDocumentPassageDto>>();
        var current = new List<LegalDocumentPassageDto>();
        var currentLength = 0;
        foreach (var passage in passages)
        {
            var length = (passage.Text?.Length ?? 0) + PerPassageEnvelope;
            if (current.Count > 0 && currentLength + length > perRequestPassageBudget)
            {
                batches.Add(current);
                current = [];
                currentLength = 0;
            }
            current.Add(passage);
            currentLength += length;
        }
        if (current.Count > 0)
            batches.Add(current);
        return batches;
    }

    // Namespace a batch proposal's keys so identical keys emitted by different batches cannot collide when the
    // batches are merged. Passage IDs, concept codes, and all governance remain untouched.
    private static LegalDocumentSemanticProposal NamespaceProposalKeys(LegalDocumentSemanticProposal proposal, int batchIndex)
    {
        if (batchIndex == 0)
            return proposal;
        string Key(string value) => $"b{batchIndex}:{value}";
        return proposal with
        {
            EvidenceItems = (proposal.EvidenceItems ?? []).Select(item => item with { ProposalKey = Key(item.ProposalKey) }).ToArray(),
            FactPropositions = (proposal.FactPropositions ?? []).Select(item => item with { ProposalKey = Key(item.ProposalKey) }).ToArray(),
            Relationships = (proposal.Relationships ?? []).Select(item => item with { SourceProposalKey = Key(item.SourceProposalKey), TargetProposalKey = Key(item.TargetProposalKey) }).ToArray()
        };
    }

    // Merge two proposals (across passage batches of the same document). The first non-empty document type /
    // classification confidence wins; all collections are concatenated. Final de-duplication/governance is
    // performed by Govern against the full passage set.
    private static LegalDocumentSemanticProposal Merge(LegalDocumentSemanticProposal left, LegalDocumentSemanticProposal right)
        => new(
            left.DocumentTypeCode ?? right.DocumentTypeCode,
            left.ClassificationConfidence ?? right.ClassificationConfidence,
            [.. left.EvidenceItems ?? [], .. right.EvidenceItems ?? []],
            [.. left.FactPropositions ?? [], .. right.FactPropositions ?? []],
            [.. left.Relationships ?? [], .. right.Relationships ?? []],
            [.. left.Ambiguities ?? [], .. right.Ambiguities ?? []],
            [.. left.Unknowns ?? [], .. right.Unknowns ?? []])
        {
            DomainEntities = [.. left.DomainEntities ?? [], .. right.DomainEntities ?? []],
            DomainEvents = [.. left.DomainEvents ?? [], .. right.DomainEvents ?? []]
        };

    private static LegalDocumentSemanticProposal Govern(
        LegalDocumentSemanticProposal proposal,
        IReadOnlyCollection<DecisionDomainConceptDto> concepts,
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        ResolvedDomainPack? resolvedPack)
    {
        var allowedPassages = passages.Select(item => item.LegalDocumentPassageId).ToHashSet();
        var conceptsByCode = concepts.ToDictionary(item => item.ConceptCode, StringComparer.OrdinalIgnoreCase);
        // When a Domain Pack supplies concepts, evidence must bind to one of its concepts. On the intake
        // corpus-activation path no Domain Pack is provided (concepts is empty); requiring a concept match
        // there would discard every extracted evidence item, leaving documents perpetually "not activated".
        // So concept binding is enforced only when a concept set actually exists.
        var requireConceptBinding = conceptsByCode.Count > 0;
        var evidence = proposal.EvidenceItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ProposalKey) &&
                           !string.IsNullOrWhiteSpace(item.Summary) &&
                           item.PassageId.HasValue &&
                           allowedPassages.Contains(item.PassageId.Value) &&
                           (!requireConceptBinding ||
                            (!string.IsNullOrWhiteSpace(item.DomainConceptCode) &&
                             conceptsByCode.ContainsKey(item.DomainConceptCode))))
            .Select(item =>
            {
                var concept = item.DomainConceptCode is not null && conceptsByCode.TryGetValue(item.DomainConceptCode, out var match) ? match : null;
                return item with
                {
                    DimensionCode = concept?.DimensionCode ?? item.DimensionCode,
                    DomainConceptCode = concept?.ConceptCode,
                    VerificationProfileCode = concept?.VerificationProfileCode ?? item.VerificationProfileCode,
                    Confidence = Clamp(item.Confidence)
                };
            })
            .GroupBy(item => $"{item.PassageId:N}|{item.DomainConceptCode}|{Normalize(item.Summary)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var evidenceKeys = evidence.Select(item => item.ProposalKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supportedFactKeys = proposal.Relationships
            .Where(item => evidenceKeys.Contains(item.SourceProposalKey) && AllowedRelationship(item.RelationshipTypeCode))
            .Select(item => item.TargetProposalKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var facts = proposal.FactPropositions
            .Where(item => !string.IsNullOrWhiteSpace(item.ProposalKey) && !string.IsNullOrWhiteSpace(item.PropositionText))
            .Where(item => supportedFactKeys.Contains(item.ProposalKey))
            .Select(item => item with
            {
                FactStateCode = LegalFactStates.Alleged,
                Confidence = Clamp(item.Confidence)
            })
            .GroupBy(item => Normalize(item.PropositionText), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var factKeys = facts.Select(item => item.ProposalKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relationships = proposal.Relationships
            .Where(item => evidenceKeys.Contains(item.SourceProposalKey) && factKeys.Contains(item.TargetProposalKey))
            .Select(item => item with
            {
                RelationshipTypeCode = AllowedRelationship(item.RelationshipTypeCode)
                    ? item.RelationshipTypeCode.ToUpperInvariant()
                    : LegalDocumentRelationshipTypes.RelatedTo
            })
            .ToArray();
        // Govern extracted domain entities/events: they must cite a supplied passage and, when a resolved
        // Domain Pack is present, bind to one of its EntityType/EventType codes. With no pack (intake
        // corpus-activation path) they are dropped — there is no vocabulary to validate them against.
        var entityTypeCodes = (resolvedPack?.EntityTypes ?? [])
            .Select(entity => entity.EntityTypeCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var eventTypeCodes = (resolvedPack?.EventTypes ?? [])
            .Select(evt => evt.EventTypeCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var domainEntities = proposal.DomainEntities
            .Where(item => !string.IsNullOrWhiteSpace(item.EntityTypeCode) &&
                           !string.IsNullOrWhiteSpace(item.EntityText) &&
                           item.PassageId.HasValue &&
                           allowedPassages.Contains(item.PassageId.Value) &&
                           entityTypeCodes.Contains(item.EntityTypeCode))
            .Select(item => item with { Confidence = Clamp(item.Confidence) })
            .GroupBy(item => $"{item.PassageId:N}|{item.EntityTypeCode}|{Normalize(item.EntityText)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var domainEvents = proposal.DomainEvents
            .Where(item => !string.IsNullOrWhiteSpace(item.EventTypeCode) &&
                           !string.IsNullOrWhiteSpace(item.Summary) &&
                           item.PassageId.HasValue &&
                           allowedPassages.Contains(item.PassageId.Value) &&
                           eventTypeCodes.Contains(item.EventTypeCode))
            .Select(item => item with { Confidence = Clamp(item.Confidence) })
            .GroupBy(item => $"{item.PassageId:N}|{item.EventTypeCode}|{Normalize(item.Summary)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        return proposal with
        {
            ClassificationConfidence = Clamp(proposal.ClassificationConfidence),
            EvidenceItems = evidence,
            FactPropositions = facts,
            Relationships = relationships,
            DomainEntities = domainEntities,
            DomainEvents = domainEvents
        };
    }

    private static bool AllowedRelationship(string value) => value.Equals(LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(LegalDocumentRelationshipTypes.Qualifies, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(LegalDocumentRelationshipTypes.DerivedFrom, StringComparison.OrdinalIgnoreCase) ||
        value.Equals(LegalDocumentRelationshipTypes.RelatedTo, StringComparison.OrdinalIgnoreCase);

    private static decimal? Clamp(decimal? value) => value.HasValue ? Math.Clamp(value.Value, 0m, 1m) : null;

    private static string Normalize(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static LegalDocumentSemanticProposal? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return JsonSerializer.Deserialize<LegalDocumentSemanticProposal>(value, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static LegalDocumentSemanticProposal EmptyProposal() => new(null, null, [], [], [], [], []);

    private const string SystemPrompt = """
        You extract proposed legal-document semantics. Use only supplied passages and Domain Pack concepts.
        Never decide a matter, verify a fact, or invent missing content. Every evidence proposal should cite a supplied passageId.
        When DomainEntityTypes/DomainEventTypes are supplied, also extract domainEntities/domainEvents that occur in the
        passages, each bound to one supplied entityTypeCode/eventTypeCode and citing a supplied passageId; omit any you cannot bind.
        All facts are allegations pending independent verification. Return strict JSON matching the schema.
        """;

    private const string OutputSchemaJson = """
        {
          "type":"object",
          "additionalProperties":false,
          "required":["documentTypeCode","classificationConfidence","evidenceItems","factPropositions","relationships","ambiguities","unknowns","domainEntities","domainEvents"],
          "properties":{
            "documentTypeCode":{"type":["string","null"]},
            "classificationConfidence":{"type":["number","null"],"minimum":0,"maximum":1},
            "evidenceItems":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["proposalKey","passageId","evidenceTypeCode","dimensionCode","summary","confidence","domainConceptCode","verificationProfileCode"],"properties":{"proposalKey":{"type":"string"},"passageId":{"type":"string","format":"uuid"},"evidenceTypeCode":{"type":"string"},"dimensionCode":{"type":"string"},"summary":{"type":"string"},"confidence":{"type":["number","null"]},"domainConceptCode":{"type":"string"},"verificationProfileCode":{"type":["string","null"]}}}},
            "factPropositions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["proposalKey","propositionText","factStateCode","confidence"],"properties":{"proposalKey":{"type":"string"},"propositionText":{"type":"string"},"factStateCode":{"type":"string"},"confidence":{"type":["number","null"]}}}},
            "relationships":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["sourceProposalKey","targetProposalKey","relationshipTypeCode","rationale"],"properties":{"sourceProposalKey":{"type":"string"},"targetProposalKey":{"type":"string"},"relationshipTypeCode":{"type":"string"},"rationale":{"type":["string","null"]}}}},
            "domainEntities":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["passageId","entityTypeCode","dimensionCode","entityText","normalizedValue","confidence"],"properties":{"passageId":{"type":"string","format":"uuid"},"entityTypeCode":{"type":"string"},"dimensionCode":{"type":["string","null"]},"entityText":{"type":"string"},"normalizedValue":{"type":["string","null"]},"confidence":{"type":["number","null"]}}}},
            "domainEvents":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["passageId","eventTypeCode","dimensionCode","summary","eventDateUtc","confidence"],"properties":{"passageId":{"type":"string","format":"uuid"},"eventTypeCode":{"type":"string"},"dimensionCode":{"type":["string","null"]},"summary":{"type":"string"},"eventDateUtc":{"type":["string","null"],"format":"date-time"},"confidence":{"type":["number","null"]}}}},
            "ambiguities":{"type":"array","items":{"type":"string"}},
            "unknowns":{"type":"array","items":{"type":"string"}}
          }
        }
        """;
}
