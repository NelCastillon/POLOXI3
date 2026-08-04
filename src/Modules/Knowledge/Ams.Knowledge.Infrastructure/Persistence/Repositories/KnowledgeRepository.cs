using System.Data;
using System.Text.Json;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Common.Models;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Knowledge.Contracts.Concepts;
using Ams.Knowledge.Contracts.Hierarchy;
using Ams.Knowledge.Contracts.Mappings;
using Ams.Knowledge.Domain.Governance;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Ams.Knowledge.Infrastructure.Persistence.Repositories;

public sealed partial class KnowledgeRepository :
    IKnowledgeQueryRepository,
    IKnowledgeCommandRepository,
    IConceptResolutionRepository,
    IKnowledgeResolutionPolicyProvider,
    IKnowledgeHierarchyRepository,
    IExternalMappingRepository,
    IKnowledgeValidationRuleRepository,
    IKnowledgeValidationPolicyProvider
{
    private readonly KnowledgeSqlConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public KnowledgeRepository(KnowledgeSqlConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    private async Task WriteAuditAndOutboxAsync(SqlConnection connection, IDbTransaction transaction, KnowledgeAuditFact audit, CancellationToken cancellationToken)
    {
        audit.Validate();
        const string sql = """
IF OBJECT_ID(N'Audit.AuditEvent', N'U') IS NOT NULL
BEGIN
    INSERT INTO Audit.AuditEvent
    (AuditEventId, TenantId, ActorUserId, ActorType, ActionType, ActionCategory, ModuleName, EntityName, EntityId, OldValue, NewValue, CorrelationId, SourceSystem, Severity, StatusCode, ChangeReason, VersionNumber, CreatedUtc)
    VALUES
    (NEWID(), @TenantId, @ActorUserId, N'User', @ActionTypeCode, N'KnowledgeGovernance', N'Knowledge', @EntityTypeCode, @EntityId, @OldValueJson, @NewValueJson, @CorrelationId, @SourceCode, N'Info', N'Success', @ChangeReason, @VersionNumber, @OccurredUtc);
END;

INSERT INTO knowledge.SemanticOutboxMessage
(SemanticOutboxMessageId, TenantId, EventTypeCode, AggregateTypeCode, AggregateId, PayloadJson, StatusCode, CorrelationId, OccurredDateUtc, AvailableDateUtc, RetryCount)
VALUES
(NEWID(), @TenantId, @ActionTypeCode, @EntityTypeCode, @EntityId,
 (SELECT @TenantId AS TenantId, @ActionTypeCode AS EventTypeCode, @EntityTypeCode AS AggregateTypeCode, @EntityId AS AggregateId, @NewValueJson AS DataJson, @CorrelationId AS CorrelationId, @OccurredUtc AS OccurredUtc FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
 N'PENDING', @CorrelationId, @OccurredUtc, @OccurredUtc, 0);
""";
        await connection.ExecuteAsync(new CommandDefinition(sql, audit, transaction, cancellationToken: cancellationToken));
    }

    private static void EnsureSingleRow(int affectedRows, string entityName)
    {
        if (affectedRows == 0)
            throw new DBConcurrencyException($"The {entityName} was changed or removed by another user.");
    }

    private void InvalidateTenant(Guid tenantId)
    {
        _cache.Remove($"knowledge:resolution-policy:{tenantId}");
        _cache.Remove($"knowledge:validation-policy:{tenantId}");
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value);

    private sealed record ConceptPersistenceRow(
        Guid KnowledgeConceptId,
        Guid ConceptSchemeId,
        string ConceptCode,
        string ConceptTypeCode,
        string PreferredLabel,
        string? Definition,
        Guid? ParentConceptId,
        bool IsAbstract,
        bool IsSelectable,
        string StatusCode,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        int VersionNumber,
        Guid? SupersedesConceptId,
        Guid? TenantId,
        bool IsSystemDefined,
        Guid OwnerUserId,
        Guid BusinessStewardUserId,
        Guid? TechnicalStewardUserId,
        string DefinitionSource,
        string? LicensingNotes,
        Guid CreatedByUserId,
        DateTime CreatedDateUtc);
}
