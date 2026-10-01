using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class LegalDocumentSemanticInterpreter(IAiProviderRouter aiRouter) : ILegalDocumentSemanticInterpreter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
            });
        var sourcePassages = passages.Take(200).Select(passage => new
        {
            PassageId = passage.LegalDocumentPassageId,
            passage.PageNumber,
            passage.SectionPath,
            passage.Text
        });
        var entityTypes = (resolvedPack?.EntityTypes ?? [])
            .OrderBy(entity => entity.SortOrder)
            .Select(entity => new { entity.EntityTypeCode, entity.DimensionCode, entity.Name, entity.Description });
        var eventTypes = (resolvedPack?.EventTypes ?? [])
            .OrderBy(evt => evt.SortOrder)
            .Select(evt => new { evt.EventTypeCode, evt.DimensionCode, evt.Name, evt.Description });
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
            modelCodeOverride: string.IsNullOrWhiteSpace(modelCodeOverride) ? null : modelCodeOverride.Trim(),
            cancellationToken: cancellationToken);
        var proposal = Parse(result.StructuredOutputJson ?? result.Content) ?? EmptyProposal();
        return Govern(proposal, domainConcepts, passages, resolvedPack);
    }

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
