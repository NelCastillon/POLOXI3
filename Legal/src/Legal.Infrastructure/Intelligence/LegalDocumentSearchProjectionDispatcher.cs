using System.Text.Json;
using System.Data;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class LegalDocumentSearchProjectionDispatcher(
    ISqlConnectionFactory connectionFactory,
    IOptions<DocumentIntelligenceOptions> options) : ILegalDocumentSearchProjectionDispatcher
{
    private readonly DocumentIntelligenceOptions _options = options.Value;
    private bool _searchIndexEnsured;

    public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var claimTransaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var items = (await connection.QueryAsync<ProjectionItem>(new CommandDefinition(
            """
            ;WITH claim AS
            (
                SELECT TOP (@BatchSize) *
                FROM POLOXI.Legal_DocumentSearchProjectionOutbox WITH (UPDLOCK,READPAST,ROWLOCK,READCOMMITTEDLOCK)
                WHERE IsDeleted=0 AND AttemptCount<12 AND
                      ((StatusCode IN (N'PENDING',N'FAILED') AND NextAttemptDateUtc<=SYSUTCDATETIME()) OR
                       (StatusCode=N'PROCESSING' AND ProcessingStartedDateUtc<DATEADD(minute,-10,SYSUTCDATETIME())))
                ORDER BY NextAttemptDateUtc,CreatedDateUtc,LegalDocumentSearchProjectionOutboxId
            )
            UPDATE claim SET StatusCode=N'PROCESSING',AttemptCount=AttemptCount+1,ProcessingStartedDateUtc=SYSUTCDATETIME()
            OUTPUT inserted.LegalDocumentSearchProjectionOutboxId,inserted.TenantId,inserted.LegalDocumentVersionId,inserted.AttemptCount;
            """, new { BatchSize = Math.Clamp(batchSize, 1, 100) }, claimTransaction, cancellationToken: cancellationToken))).ToArray();
        claimTransaction.Commit();

        foreach (var item in items)
        {
            try
            {
                var documents = (await connection.QueryAsync<SearchDocument>(new CommandDefinition(
                    """
                    SELECT CONCAT(CONVERT(nvarchar(32),document.TenantId,2),N'-',CONVERT(nvarchar(32),passage.LegalDocumentPassageId,2)) [Id],
                           document.TenantId,document.DecisionMatterId AS MatterId,document.LegalDocumentId,version.LegalDocumentVersionId,
                           passage.LegalDocumentPassageId AS PassageId,document.FileName AS Title,passage.PassageText AS Content,
                           passage.PageNumber,document.DocumentTypeCode,passage.ExtractionMethodCode,passage.EpistemicStateCode,
                           version.Sha256Hash,version.StorageReference,document.CreatedByUserId
                    FROM POLOXI.Legal_MatterDocumentVersion version
                    JOIN POLOXI.Legal_MatterDocument document ON document.LegalDocumentId=version.LegalDocumentId AND document.IsDeleted=0
                    JOIN POLOXI.Legal_DocumentPassage passage ON passage.LegalDocumentVersionId=version.LegalDocumentVersionId AND passage.IsDeleted=0
                    WHERE version.LegalDocumentVersionId=@VersionId AND version.TenantId=@TenantId AND version.IsDeleted=0 AND document.StatusCode<>N'QUARANTINED';
                    """, new { VersionId = item.LegalDocumentVersionId, item.TenantId }, cancellationToken: cancellationToken))).ToArray();

                await UpsertSqlProjectionAsync(connection, documents, cancellationToken);
                if (_options.SearchProjectionEnabled && documents.Length > 0)
                {
                    await EnsureSearchIndexAsync(cancellationToken);
                    await SearchClient().MergeOrUploadDocumentsAsync(documents, cancellationToken: cancellationToken);
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE POLOXI.Legal_DocumentSearchProjectionOutbox SET StatusCode=N'COMPLETED',ProcessedDateUtc=SYSUTCDATETIME(),ProcessingStartedDateUtc=NULL,LastError=NULL WHERE LegalDocumentSearchProjectionOutboxId=@Id;",
                    new { Id = item.LegalDocumentSearchProjectionOutboxId }, cancellationToken: cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE POLOXI.Legal_DocumentSearchProjectionOutbox SET StatusCode=CASE WHEN AttemptCount>=12 THEN N'DEAD_LETTER' ELSE N'FAILED' END,ProcessingStartedDateUtc=NULL,LastError=LEFT(@Error,4000),NextAttemptDateUtc=DATEADD(second,POWER(2,CASE WHEN AttemptCount>10 THEN 10 ELSE AttemptCount END),SYSUTCDATETIME()) WHERE LegalDocumentSearchProjectionOutboxId=@Id;",
                    new { Id = item.LegalDocumentSearchProjectionOutboxId, Error = ex.Message }, cancellationToken: CancellationToken.None));
            }
        }
        return items.Length;
    }

    private static async Task UpsertSqlProjectionAsync(System.Data.IDbConnection connection, IReadOnlyCollection<SearchDocument> documents, CancellationToken cancellationToken)
    {
        foreach (var item in documents)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                MERGE AI.Legal_SearchDocument target USING (SELECT @TenantId TenantId,@PassageId EntityId) source
                ON target.TenantId=source.TenantId AND target.EntityTypeCode=N'LEGAL_DOCUMENT_PASSAGE' AND target.EntityId=source.EntityId AND target.IsDeleted=0
                WHEN MATCHED THEN UPDATE SET ModuleCode=N'LEGAL',Title=@Title,ContentText=@Content,Keywords=CONCAT_WS(N' ',@DocumentTypeCode,@EpistemicStateCode,@ExtractionMethodCode),SecurityScopeJson=@SecurityScopeJson,ContentHash=CONVERT(char(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),@Content)),2),IndexedDateUtc=SYSUTCDATETIME(),SourceModifiedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT(SearchDocumentId,TenantId,EntityTypeCode,EntityId,ModuleCode,Title,ContentText,Keywords,ConceptIdsJson,SecurityScopeJson,ContentHash,IndexedDateUtc,SourceModifiedDateUtc,SourceCreatedDateUtc,CreatedDateUtc,IsDeleted)
                VALUES(NEWID(),@TenantId,N'LEGAL_DOCUMENT_PASSAGE',@PassageId,N'LEGAL',@Title,@Content,CONCAT_WS(N' ',@DocumentTypeCode,@EpistemicStateCode,@ExtractionMethodCode),N'[]',@SecurityScopeJson,CONVERT(char(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),@Content)),2),SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                DECLARE @SearchDocumentId uniqueidentifier=(SELECT SearchDocumentId FROM AI.Legal_SearchDocument WHERE TenantId=@TenantId AND EntityTypeCode=N'LEGAL_DOCUMENT_PASSAGE' AND EntityId=@PassageId AND IsDeleted=0);
                IF @CreatedByUserId IS NOT NULL AND NOT EXISTS
                (
                    SELECT 1 FROM AI.Legal_SearchPermission
                    WHERE TenantId=@TenantId AND SearchDocumentId=@SearchDocumentId AND PrincipalTypeCode=N'USER'
                      AND PrincipalId=@CreatedByUserId AND PermissionCode=N'READ' AND IsDeleted=0
                )
                    INSERT AI.Legal_SearchPermission (SearchPermissionId,TenantId,SearchDocumentId,PrincipalTypeCode,PrincipalId,PermissionCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
                    VALUES (NEWID(),@TenantId,@SearchDocumentId,N'USER',@CreatedByUserId,N'READ',SYSUTCDATETIME(),@CreatedByUserId,0);
                """, new
                {
                    item.TenantId, item.PassageId, item.Title, item.Content, item.DocumentTypeCode, item.EpistemicStateCode, item.ExtractionMethodCode, item.CreatedByUserId,
                    SecurityScopeJson = JsonSerializer.Serialize(new { permissionCode = "Intelligence.Search", matterId = item.MatterId })
                }, cancellationToken: cancellationToken));
    }

    private SearchClient SearchClient()
    {
        var endpoint = new Uri(_options.SearchEndpoint);
        return string.IsNullOrWhiteSpace(_options.SearchApiKey)
            ? new SearchClient(endpoint, _options.SearchIndexName, new DefaultAzureCredential())
            : new SearchClient(endpoint, _options.SearchIndexName, new AzureKeyCredential(_options.SearchApiKey));
    }

    private async Task EnsureSearchIndexAsync(CancellationToken cancellationToken)
    {
        if (_searchIndexEnsured)
            return;
        var endpoint = new Uri(_options.SearchEndpoint);
        var client = string.IsNullOrWhiteSpace(_options.SearchApiKey)
            ? new SearchIndexClient(endpoint, new DefaultAzureCredential())
            : new SearchIndexClient(endpoint, new AzureKeyCredential(_options.SearchApiKey));
        var fields = new FieldBuilder().Build(typeof(SearchDocument));
        await client.CreateOrUpdateIndexAsync(new SearchIndex(_options.SearchIndexName, fields), allowIndexDowntime: false, cancellationToken: cancellationToken);
        _searchIndexEnsured = true;
    }

    private sealed record ProjectionItem(Guid LegalDocumentSearchProjectionOutboxId, Guid TenantId, Guid LegalDocumentVersionId, int AttemptCount);

    private sealed record SearchDocument(
        [property: SimpleField(IsKey = true, IsFilterable = true)] string Id,
        [property: SimpleField(IsFilterable = true)] Guid TenantId,
        [property: SimpleField(IsFilterable = true)] Guid MatterId,
        [property: SimpleField(IsFilterable = true)] Guid LegalDocumentId,
        [property: SimpleField(IsFilterable = true)] Guid LegalDocumentVersionId,
        [property: SimpleField(IsFilterable = true)] Guid PassageId,
        [property: SearchableField] string Title,
        [property: SearchableField] string Content,
        [property: SimpleField(IsFilterable = true)] int? PageNumber,
        [property: SimpleField(IsFilterable = true)] string? DocumentTypeCode,
        [property: SimpleField(IsFilterable = true)] string ExtractionMethodCode,
        [property: SimpleField(IsFilterable = true)] string EpistemicStateCode,
        [property: SimpleField(IsFilterable = true)] string Sha256Hash,
        [property: SimpleField] string StorageReference,
        [property: SimpleField(IsFilterable = true)] Guid? CreatedByUserId);
}
