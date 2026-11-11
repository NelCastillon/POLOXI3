using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Dapper;
using System.Text.Json;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper persistence for Chat Judz enterprise conversation history (POLOXI.Legal_ChatJudzMessage).
// Tenant/user scoped, soft-delete aware, self-contained (MatterId is an optional tag only).
public sealed class ChatJudzRepository(ISqlConnectionFactory connectionFactory) : IChatJudzRepository
{
    public async Task AppendMessageAsync(
        Guid tenantId,
        Guid? userId,
        Guid conversationId,
        int sequence,
        string role,
        string content,
        string? modelCode,
        Guid? matterId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT POLOXI.Legal_ChatJudzMessage
    (ChatJudzMessageId,ConversationId,Sequence,Role,Content,ModelCodeUsed,MatterId,
     TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES
    (NEWID(),@ConversationId,@Sequence,@Role,@Content,@ModelCode,@MatterId,
     @TenantId,SYSUTCDATETIME(),@UserId,0);
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            ConversationId = conversationId,
            Sequence = sequence,
            Role = role,
            Content = content,
            ModelCode = modelCode,
            MatterId = matterId,
            TenantId = tenantId,
            UserId = userId
        }, cancellationToken: cancellationToken));
    }

    public async Task<int> GetMessageCountAsync(Guid tenantId, Guid? userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT COUNT(1)
FROM POLOXI.Legal_ChatJudzMessage
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId)
  AND ConversationId=@ConversationId AND IsDeleted=0;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ConversationId = conversationId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ChatJudzTurnDto>> GetConversationAsync(Guid tenantId, Guid? userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT Role,Content,ModelCodeUsed AS ModelCode,CreatedDateUtc
FROM POLOXI.Legal_ChatJudzMessage
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId)
  AND ConversationId=@ConversationId AND IsDeleted=0
ORDER BY Sequence, CreatedDateUtc;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ChatJudzTurnDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ConversationId = conversationId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<ChatJudzConversationSummaryDto>> GetConversationListAsync(Guid tenantId, Guid? userId, Guid? matterId, int take, CancellationToken cancellationToken = default)
    {
        if (take < 1) take = 50;
        if (take > 200) take = 200;
        // One row per conversation: title from the first user message, plus activity + counts.
        const string sql = """
SELECT TOP(@Take)
       g.ConversationId,
       COALESCE(NULLIF(LTRIM(RTRIM(LEFT(firstUser.Content,120))),N''),N'New conversation') AS Title,
       g.MessageCount,
       g.LastActivityUtc,
       g.MatterId,
       NULLIF(LTRIM(RTRIM(LEFT(lastReply.Content,160))),N'') AS Preview
FROM (
    SELECT ConversationId,
           COUNT(1) AS MessageCount,
           MAX(CreatedDateUtc) AS LastActivityUtc,
           MAX(MatterId) AS MatterId,
           MIN(CASE WHEN Role=N'user' THEN Sequence END) AS FirstUserSeq
    FROM POLOXI.Legal_ChatJudzMessage
    WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId) AND IsDeleted=0
      AND (@MatterId IS NULL OR MatterId=@MatterId)
    GROUP BY ConversationId
) g
OUTER APPLY (
    SELECT TOP(1) m.Content
    FROM POLOXI.Legal_ChatJudzMessage m
    WHERE m.TenantId=@TenantId AND (@UserId IS NULL OR m.CreatedByUserId=@UserId)
      AND m.ConversationId=g.ConversationId AND m.IsDeleted=0 AND m.Role=N'user'
    ORDER BY m.Sequence, m.CreatedDateUtc
) firstUser
OUTER APPLY (
    SELECT TOP(1) m.Content
    FROM POLOXI.Legal_ChatJudzMessage m
    WHERE m.TenantId=@TenantId AND (@UserId IS NULL OR m.CreatedByUserId=@UserId)
      AND m.ConversationId=g.ConversationId AND m.IsDeleted=0 AND m.Role=N'assistant'
    ORDER BY m.Sequence DESC, m.CreatedDateUtc DESC
) lastReply
ORDER BY g.LastActivityUtc DESC;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ChatJudzConversationSummaryDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, MatterId = matterId, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task DeleteConversationAsync(Guid tenantId, Guid? userId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE POLOXI.Legal_ChatJudzMessage
SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId)
  AND ConversationId=@ConversationId AND IsDeleted=0;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ConversationId = conversationId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ChatJudzModelOptionDto>> GetModelOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT model.ModelCode,model.DeploymentName,model.ModelFamily
FROM AI.Legal_ModelDeployment model
JOIN AI.Legal_Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
WHERE model.CapabilityCode=N'CHAT' AND model.IsActive=1 AND model.IsDeleted=0 AND (model.TenantId=@TenantId OR model.TenantId IS NULL)
ORDER BY model.Priority,model.ModelCode;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ChatJudzModelOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<string?> GetSelectedModelAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP(1) ModelCode
FROM POLOXI.Legal_ChatJudzModelSetting
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId) AND IsDeleted=0
ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task SaveSelectedModelAsync(Guid tenantId, Guid? userId, string modelCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE POLOXI.Legal_ChatJudzModelSetting
SET ModelCode=@ModelCode, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId) AND IsDeleted=0;

IF @@ROWCOUNT=0
    INSERT POLOXI.Legal_ChatJudzModelSetting
        (ChatJudzModelSettingId,ModelCode,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES
        (NEWID(),@ModelCode,@TenantId,SYSUTCDATETIME(),@UserId,0);
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, ModelCode = modelCode }, cancellationToken: cancellationToken));
    }

    // ── Citation Passages history ──────────────────────────────────────────────────────────────────

    public async Task AppendCitationBatchAsync(Guid tenantId, Guid? userId, ChatJudzCitationBatchRequest batch, CancellationToken cancellationToken = default)
    {
        if (batch.Passages is not { Count: > 0 })
            return;

        const string sql = """
INSERT POLOXI.Legal_ChatJudzCitationPassage
    (ChatJudzCitationPassageId,CitationBatchId,ConversationId,BatchTitle,
     BranchDisplayName,BranchConfidence,BranchInterpretation,
     RankNumber,AuthorityName,MatchScore,FacetsJson,FitDetail,LinkedMatterPassage,LinkedFactorCount,
     MatterId,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES
    (NEWID(),@CitationBatchId,@ConversationId,@BatchTitle,
     @BranchDisplayName,@BranchConfidence,@BranchInterpretation,
     @RankNumber,@AuthorityName,@MatchScore,@FacetsJson,@FitDetail,@LinkedMatterPassage,@LinkedFactorCount,
     @MatterId,@TenantId,SYSUTCDATETIME(),@UserId,0);
""";
        var rows = batch.Passages.Select(p => new
        {
            CitationBatchId = batch.CitationBatchId,
            ConversationId = batch.ConversationId,
            BatchTitle = Trim(batch.BatchTitle, 200),
            BranchDisplayName = Trim(p.BranchDisplayName, 400),
            BranchConfidence = p.BranchConfidence,
            BranchInterpretation = p.BranchInterpretation,
            RankNumber = p.RankNumber,
            AuthorityName = Trim(p.AuthorityName, 600) ?? string.Empty,
            MatchScore = p.MatchScore,
            FacetsJson = p.Facets is { Count: > 0 } ? JsonSerializer.Serialize(p.Facets) : null,
            FitDetail = p.FitDetail,
            LinkedMatterPassage = p.LinkedMatterPassage,
            LinkedFactorCount = p.LinkedFactorCount,
            MatterId = batch.MatterId,
            TenantId = tenantId,
            UserId = userId
        }).ToArray();

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, rows, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ChatJudzCitationBatchSummaryDto>> GetCitationBatchesAsync(Guid tenantId, Guid? userId, Guid? matterId, int take, CancellationToken cancellationToken = default)
    {
        if (take < 1) take = 50;
        if (take > 200) take = 200;
        const string sql = """
SELECT TOP(@Take)
       CitationBatchId,
       COALESCE(NULLIF(LTRIM(RTRIM(MAX(BatchTitle))),N''),N'Citation result') AS Title,
       COUNT(1) AS PassageCount,
       COUNT(DISTINCT BranchDisplayName) AS BranchCount,
       MAX(CreatedDateUtc) AS CreatedDateUtc,
       MAX(ConversationId) AS ConversationId,
       MAX(MatterId) AS MatterId
FROM POLOXI.Legal_ChatJudzCitationPassage
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId) AND IsDeleted=0
  AND (@MatterId IS NULL OR MatterId=@MatterId)
GROUP BY CitationBatchId
ORDER BY CreatedDateUtc DESC;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ChatJudzCitationBatchSummaryDto>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, MatterId = matterId, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ChatJudzCitationBatchDto?> GetCitationBatchAsync(Guid tenantId, Guid? userId, Guid citationBatchId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT CitationBatchId,BatchTitle,ConversationId,MatterId,CreatedDateUtc,
       BranchDisplayName,BranchConfidence,BranchInterpretation,
       RankNumber,AuthorityName,MatchScore,FacetsJson,FitDetail,LinkedMatterPassage,LinkedFactorCount
FROM POLOXI.Legal_ChatJudzCitationPassage
WHERE TenantId=@TenantId AND (@UserId IS NULL OR CreatedByUserId=@UserId)
  AND CitationBatchId=@CitationBatchId AND IsDeleted=0
ORDER BY BranchDisplayName, RankNumber;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<CitationPassageRow>(new CommandDefinition(sql, new { TenantId = tenantId, UserId = userId, CitationBatchId = citationBatchId }, cancellationToken: cancellationToken))).AsList();
        if (rows.Count == 0)
            return null;

        var first = rows[0];
        var passages = rows.Select(r => new ChatJudzCitationPassageDto(
            r.BranchDisplayName ?? string.Empty,
            r.BranchConfidence,
            r.BranchInterpretation,
            r.RankNumber,
            r.AuthorityName,
            r.MatchScore,
            DeserializeFacets(r.FacetsJson),
            r.FitDetail,
            r.LinkedMatterPassage,
            r.LinkedFactorCount)).ToArray();

        return new ChatJudzCitationBatchDto(
            first.CitationBatchId, first.BatchTitle, first.CreatedDateUtc, first.ConversationId, first.MatterId, passages);
    }

    private static string? Trim(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null : (value.Length > max ? value[..max] : value);

    private static IReadOnlyList<ChatJudzCitationFacetDto> DeserializeFacets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try { return JsonSerializer.Deserialize<List<ChatJudzCitationFacetDto>>(json) ?? []; }
        catch { return []; }
    }

    private sealed record CitationPassageRow(
        Guid CitationBatchId,
        string? BatchTitle,
        Guid? ConversationId,
        Guid? MatterId,
        DateTime CreatedDateUtc,
        string? BranchDisplayName,
        decimal? BranchConfidence,
        string? BranchInterpretation,
        int RankNumber,
        string AuthorityName,
        decimal? MatchScore,
        string? FacetsJson,
        string? FitDetail,
        string? LinkedMatterPassage,
        int LinkedFactorCount);
}
