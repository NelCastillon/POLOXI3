using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

public sealed class LegalDocumentCorpusRepository(ISqlConnectionFactory connectionFactory) : ILegalDocumentCorpusRepository
{
    public async Task<DecisionRetrievalArchitectureSettings> GetRetrievalArchitectureSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string SettingKey, string SettingValue)>(new CommandDefinition(
            "SELECT SettingKey, SettingValue FROM POLOXI.Legal_DecisionSetting WHERE IsDeleted=0;",
            cancellationToken: cancellationToken));
        var settings = rows.ToDictionary(row => row.SettingKey, row => row.SettingValue, StringComparer.OrdinalIgnoreCase);
        bool B(string key, bool fallback) => settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
        int I(string key, int fallback) => settings.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
        return new(
            B("Decision.DocumentIntelligence.SemanticEnrichment.Enabled", false),
            B("Decision.MatterContext.Enabled", false),
            Math.Clamp(I("Decision.MatterContext.MaximumItems", 12), 1, 50),
            Math.Clamp(I("Decision.MatterContext.MaximumCharacters", 18000), 1000, 100000),
            B("Decision.MatterContext.LegacyProjectionFallback.Enabled", true),
            B("Decision.Research.AuthoritativeRouting.Enabled", true),
            B("Decision.LegacyUnconditionalRetrieval.Enabled", false),
            B("Decision.RetrievalTelemetry.Enabled", true));
    }

    public async Task<IReadOnlyCollection<LegalMatterContextItem>> SearchRoutedMatterContextAsync(Guid tenantId, Guid matterId, string query, IReadOnlyCollection<string> documentTypeCodes, int maximumItems, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterContextRow>(new CommandDefinition(
            """
            SELECT TOP(@ScanLimit) document.DecisionMatterId AS MatterId, document.LegalDocumentId,
                   version.LegalDocumentVersionId, passage.LegalDocumentPassageId AS PassageId,
                   evidence.LegalEvidenceItemId AS EvidenceItemId, proposition.LegalFactPropositionId AS FactPropositionId,
                   document.FileName AS Title, COALESCE(proposition.PropositionText,evidence.Summary,passage.PassageText) AS Text,
                   CONCAT(N'legal-document:',CONVERT(nvarchar(36),document.LegalDocumentId),N':version:',version.VersionNumber,
                          COALESCE(N':page:'+CONVERT(nvarchar(20),passage.PageNumber),N'')) AS SourceReference,
                   passage.PageNumber, passage.ExtractionMethodCode,
                   COALESCE(evidence.EvidenceStateCode,passage.EpistemicStateCode,N'PROPOSED') AS EvidenceStateCode,
                   COALESCE(proposition.FactStateCode,N'ALLEGED') AS FactStateCode,
                   COALESCE(proposition.IsDecisionAuthoritative,0) AS IsDecisionAuthoritative,
                   document.DocumentTypeCode, evidence.DimensionCode
            FROM POLOXI.Legal_MatterDocument document
            INNER JOIN POLOXI.Legal_MatterDocumentVersion version ON version.LegalDocumentId=document.LegalDocumentId AND version.IsDeleted=0
            INNER JOIN POLOXI.Legal_DocumentPassage passage ON passage.LegalDocumentVersionId=version.LegalDocumentVersionId AND passage.IsDeleted=0
            LEFT JOIN POLOXI.Legal_MatterEvidenceItem evidence ON evidence.LegalDocumentPassageId=passage.LegalDocumentPassageId AND evidence.IsDeleted=0
            LEFT JOIN POLOXI.Legal_MatterPropositionSupport support ON support.LegalEvidenceItemId=evidence.LegalEvidenceItemId AND support.IsDeleted=0
            LEFT JOIN POLOXI.Legal_MatterFactProposition proposition ON proposition.LegalFactPropositionId=support.LegalFactPropositionId AND proposition.IsDeleted=0
            WHERE document.TenantId=@TenantId AND document.DecisionMatterId=@MatterId AND document.IsDeleted=0
              AND document.StatusCode<>N'QUARANTINED' AND version.MalwareStatusCode IN (N'CLEAN',N'NOT_DETECTED',N'PASSED')
              AND (NOT EXISTS (SELECT 1 FROM OPENJSON(@DocumentTypesJson)) OR document.DocumentTypeCode IN (SELECT [value] FROM OPENJSON(@DocumentTypesJson)))
            ORDER BY COALESCE(proposition.IsDecisionAuthoritative,0) DESC,
                     CASE COALESCE(evidence.EvidenceStateCode,passage.EpistemicStateCode) WHEN N'VERIFIED' THEN 0 WHEN N'DISPUTED' THEN 1 ELSE 2 END,
                     passage.SequenceNumber;
            """, new
            {
                TenantId = tenantId,
                MatterId = matterId,
                Query = query.Trim(),
                ScanLimit = Math.Clamp(maximumItems * 20, 20, 1000),
                DocumentTypesJson = JsonSerializer.Serialize(documentTypeCodes)
            }, cancellationToken: cancellationToken));
        var terms = SearchTerms(query);
        return rows.Select(row => new LegalMatterContextItem(row.MatterId, row.LegalDocumentId, row.LegalDocumentVersionId,
                row.PassageId, row.EvidenceItemId, row.FactPropositionId, row.Title, row.Text ?? string.Empty,
                row.SourceReference, row.PageNumber, row.ExtractionMethodCode, row.EvidenceStateCode, row.FactStateCode,
                row.IsDecisionAuthoritative, Score(row, terms), row.DocumentTypeCode, row.DimensionCode))
            .Where(item => terms.Count == 0 || item.RelevanceScore > 0m)
            .OrderByDescending(item => item.RelevanceScore)
            .Take(Math.Clamp(maximumItems, 1, 50))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<LegalDocumentDto>> GetMatterDocumentsAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT document.LegalDocumentId, document.DecisionMatterId AS MatterId, document.FileName, document.ContentType,
                   document.StatusCode, document.DocumentTypeCode, pack.PackCode AS DomainPackCode, document.CreatedDateUtc
            FROM POLOXI.Legal_MatterDocument document
            LEFT JOIN POLOXI.Legal_DecisionDomainPack pack ON pack.DecisionDomainPackId=document.DecisionDomainPackId
            WHERE document.TenantId=@TenantId AND document.DecisionMatterId=@MatterId AND document.IsDeleted=0
            ORDER BY document.CreatedDateUtc DESC;
            SELECT version.LegalDocumentVersionId, version.LegalDocumentId, version.VersionNumber, version.Sha256Hash,
                   version.StorageReference, version.FileSizeBytes, version.MalwareStatusCode, version.ProcessingStatusCode,
                   version.ExtractionProviderCode, version.ExtractionModelCode, version.ExtractionModelVersion, version.CreatedDateUtc
            FROM POLOXI.Legal_MatterDocumentVersion version
            INNER JOIN POLOXI.Legal_MatterDocument document ON document.LegalDocumentId=version.LegalDocumentId
            WHERE version.TenantId=@TenantId AND document.DecisionMatterId=@MatterId AND version.IsDeleted=0 AND document.IsDeleted=0
            ORDER BY version.LegalDocumentId, version.VersionNumber DESC;
            """, new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken));
        var documents = (await multi.ReadAsync<DocumentRow>()).ToArray();
        var versions = (await multi.ReadAsync<DocumentVersionRow>()).ToArray();
        return documents.Select(document => new LegalDocumentDto(
            document.LegalDocumentId, document.MatterId, document.FileName, document.ContentType, document.StatusCode,
            document.DocumentTypeCode, document.DomainPackCode, document.CreatedDateUtc,
            versions.Where(version => version.LegalDocumentId == document.LegalDocumentId)
                .Select(ToDto).ToArray())).ToArray();
    }

    public async Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetDocumentPassagesAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PassageRow>(new CommandDefinition(
            """
            SELECT LegalDocumentPassageId, LegalDocumentVersionId, PageNumber, SectionPath, SequenceNumber,
                   PassageText AS Text, ExtractionMethodCode, ExtractionConfidence, BoundingRegionJson, SourceSpanJson, EpistemicStateCode
            FROM POLOXI.Legal_DocumentPassage
            WHERE TenantId=@TenantId AND LegalDocumentVersionId=@DocumentVersionId AND IsDeleted=0
            ORDER BY SequenceNumber;
            """, new { TenantId = tenantId, DocumentVersionId = documentVersionId }, cancellationToken: cancellationToken));
        return rows.Select(ToDto).ToArray();
    }

    public async Task<Guid?> GetDocumentMatterIdAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT document.DecisionMatterId
            FROM POLOXI.Legal_MatterDocumentVersion version
            INNER JOIN POLOXI.Legal_MatterDocument document ON document.LegalDocumentId=version.LegalDocumentId
            WHERE version.LegalDocumentVersionId=@DocumentVersionId AND version.TenantId=@TenantId
              AND document.TenantId=@TenantId AND version.IsDeleted=0 AND document.IsDeleted=0;
            """, new { TenantId = tenantId, DocumentVersionId = documentVersionId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetRetrievalTelemetryAsync(Guid tenantId, Guid? matterId, Guid? decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionRetrievalTelemetryDto>(new CommandDefinition(
            """
            SELECT TOP(100) DecisionRetrievalTelemetryId, DecisionSessionId, DecisionMatterId AS MatterId,
                   StageCode, EventCode, RouteCode, Enabled, CandidateCount, FilteredCount, ReturnedCount,
                    ResearchNeedTypeCode, SourceClassCode, Jurisdiction, DetailJson, DurationMilliseconds, CreatedDateUtc
            FROM POLOXI.Legal_DecisionRetrievalTelemetry
            WHERE TenantId=@TenantId AND IsDeleted=0
              AND (@MatterId IS NULL OR DecisionMatterId=@MatterId)
              AND (@DecisionSessionId IS NULL OR DecisionSessionId=@DecisionSessionId)
            ORDER BY CreatedDateUtc DESC;
            """, new { TenantId = tenantId, MatterId = matterId, DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task MarkProcessingFailedAsync(Guid tenantId, Guid userId, Guid documentVersionId, string errorCode, string errorMessage, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            UPDATE version SET ProcessingStatusCode=N'FAILED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
            FROM POLOXI.Legal_MatterDocumentVersion version
            WHERE version.LegalDocumentVersionId=@DocumentVersionId AND version.TenantId=@TenantId AND version.IsDeleted=0;
            UPDATE document SET StatusCode=N'FAILED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
            FROM POLOXI.Legal_MatterDocument document
            INNER JOIN POLOXI.Legal_MatterDocumentVersion version ON version.LegalDocumentId=document.LegalDocumentId
            WHERE version.LegalDocumentVersionId=@DocumentVersionId AND document.TenantId=@TenantId AND document.IsDeleted=0;
            INSERT POLOXI.Legal_DocumentProcessingRun
                (LegalDocumentProcessingRunId,LegalDocumentVersionId,StageCode,StatusCode,AttemptCount,StartedDateUtc,CompletedDateUtc,DurationMilliseconds,ErrorCode,ErrorMessage,CorrelationId,TenantId,CreatedByUserId)
            VALUES (NEWID(),@DocumentVersionId,N'EXTRACTION',N'FAILED',1,SYSUTCDATETIME(),SYSUTCDATETIME(),0,@ErrorCode,LEFT(@ErrorMessage,4000),N'PROCESSING_FAILURE',@TenantId,@UserId);
            COMMIT;
            """, new { TenantId = tenantId, UserId = userId, DocumentVersionId = documentVersionId, ErrorCode = errorCode, ErrorMessage = errorMessage }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<LegalMatterContextItem>> SearchMatterContextAsync(Guid tenantId, Guid userId, Guid matterId, string query, int maximumItems, int maximumCharacters, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var terms = SearchTerms(query);
        var rows = (await connection.QueryAsync<MatterContextRow>(new CommandDefinition(
            """
            SELECT TOP(@ScanLimit) document.DecisionMatterId AS MatterId, document.LegalDocumentId,
                   version.LegalDocumentVersionId, passage.LegalDocumentPassageId AS PassageId,
                   evidence.LegalEvidenceItemId AS EvidenceItemId, proposition.LegalFactPropositionId AS FactPropositionId,
                   document.FileName AS Title,
                   COALESCE(proposition.PropositionText, evidence.Summary, passage.PassageText) AS Text,
                   CONCAT(N'legal-document:',CONVERT(nvarchar(36),document.LegalDocumentId),N':version:',version.VersionNumber,
                          COALESCE(N':page:'+CONVERT(nvarchar(20),passage.PageNumber),N'')) AS SourceReference,
                   passage.PageNumber, passage.ExtractionMethodCode,
                   COALESCE(evidence.EvidenceStateCode, passage.EpistemicStateCode, N'PROPOSED') AS EvidenceStateCode,
                   COALESCE(proposition.FactStateCode, N'ALLEGED') AS FactStateCode,
                   COALESCE(proposition.IsDecisionAuthoritative,0) AS IsDecisionAuthoritative,
                   document.DocumentTypeCode, evidence.DimensionCode
            FROM POLOXI.Legal_MatterDocument document
            INNER JOIN POLOXI.Legal_MatterDocumentVersion version ON version.LegalDocumentId=document.LegalDocumentId AND version.IsDeleted=0
            INNER JOIN POLOXI.Legal_DocumentPassage passage ON passage.LegalDocumentVersionId=version.LegalDocumentVersionId AND passage.IsDeleted=0
            LEFT JOIN POLOXI.Legal_MatterEvidenceItem evidence ON evidence.LegalDocumentPassageId=passage.LegalDocumentPassageId AND evidence.IsDeleted=0
            LEFT JOIN POLOXI.Legal_MatterPropositionSupport support ON support.LegalEvidenceItemId=evidence.LegalEvidenceItemId AND support.IsDeleted=0
            LEFT JOIN POLOXI.Legal_MatterFactProposition proposition ON proposition.LegalFactPropositionId=support.LegalFactPropositionId AND proposition.IsDeleted=0
            WHERE document.TenantId=@TenantId AND document.DecisionMatterId=@MatterId AND document.IsDeleted=0
              AND document.StatusCode<>N'QUARANTINED' AND version.MalwareStatusCode IN (N'CLEAN',N'NOT_DETECTED',N'PASSED')
            ORDER BY COALESCE(proposition.IsDecisionAuthoritative,0) DESC,
                     CASE COALESCE(evidence.EvidenceStateCode, passage.EpistemicStateCode) WHEN N'VERIFIED' THEN 0 WHEN N'DISPUTED' THEN 1 ELSE 2 END,
                     passage.SequenceNumber;
            """, new { TenantId = tenantId, MatterId = matterId, ScanLimit = Math.Clamp(maximumItems * 20, 20, 1000) }, cancellationToken: cancellationToken))).ToArray();

        var ranked = rows.Select(row => (Row: row, Score: Score(row, terms)))
            .Where(item => item.Score > 0m || terms.Count == 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Row.PageNumber)
            .Take(Math.Clamp(maximumItems, 1, 50));
        var result = new List<LegalMatterContextItem>();
        var usedCharacters = 0;
        foreach (var item in ranked)
        {
            if (usedCharacters >= maximumCharacters)
                break;
            var text = item.Row.Text ?? string.Empty;
            var remaining = maximumCharacters - usedCharacters;
            if (text.Length > remaining)
                text = text[..remaining];
            usedCharacters += text.Length;
            result.Add(new LegalMatterContextItem(item.Row.MatterId, item.Row.LegalDocumentId, item.Row.LegalDocumentVersionId,
                item.Row.PassageId, item.Row.EvidenceItemId, item.Row.FactPropositionId, item.Row.Title, text,
                item.Row.SourceReference, item.Row.PageNumber, item.Row.ExtractionMethodCode,
                item.Row.EvidenceStateCode, item.Row.FactStateCode, item.Row.IsDecisionAuthoritative,
                item.Score, item.Row.DocumentTypeCode, item.Row.DimensionCode));
        }
        return result;
    }

    public async Task<IReadOnlyCollection<LegalMatterContextItem>> SearchLegacyProjectionAsync(Guid tenantId, Guid userId, Guid matterId, string query, int maximumItems, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LegacyProjectionRow>(new CommandDefinition(
            """
            SELECT TOP(@MaximumItems) search.SearchDocumentId, search.EntityId, search.Title, LEFT(search.ContentText,4000) AS Text
            FROM AI.Legal_SearchDocument search
            WHERE search.TenantId=@TenantId AND search.IsDeleted=0
              AND search.EntityId=@MatterId
              AND (@Query=N'' OR search.Title LIKE N'%'+@Query+N'%' OR search.ContentText LIKE N'%'+@Query+N'%' OR search.Keywords LIKE N'%'+@Query+N'%')
              AND EXISTS
              (
                  SELECT 1 FROM AI.Legal_SearchPermission permission
                  WHERE permission.TenantId=search.TenantId AND permission.SearchDocumentId=search.SearchDocumentId
                    AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0
                    AND permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId
              )
            ORDER BY search.SourceModifiedDateUtc DESC, search.IndexedDateUtc DESC;
            """, new { TenantId = tenantId, UserId = userId, MatterId = matterId, Query = query.Trim(), MaximumItems = Math.Clamp(maximumItems, 1, 50) }, cancellationToken: cancellationToken));
        return rows.Select(row => new LegalMatterContextItem(matterId, null, null, null, null, null, row.Title, row.Text,
            $"ai-search-document:{row.SearchDocumentId}", null, LegalDocumentExtractionMethods.LegacySearchProjection,
            LegalEvidenceStates.Proposed, LegalFactStates.Alleged, false, 0.25m, null, null)).ToArray();
    }

    public async Task<Guid> CreateDocumentAsync(Guid documentId, LegalDocumentIntakeRequest request, string sha256Hash, string storageReference, string malwareStatusCode, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId=@MatterId AND TenantId=@TenantId AND IsDeleted=0)
                THROW 50010, 'Legal matter was not found for this tenant.', 1;
            DECLARE @PackId uniqueidentifier=(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack WHERE PackCode=@DomainPackCode AND (TenantId=@TenantId OR TenantId IS NULL) AND IsDeleted=0 ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END);
            INSERT POLOXI.Legal_MatterDocument
                (LegalDocumentId,DecisionMatterId,DecisionDomainPackId,DocumentControlNumber,FileName,ContentType,DocumentTypeCode,StatusCode,CurrentVersionNumber,TenantId,CreatedByUserId)
            VALUES (@DocumentId,@MatterId,@PackId,CONCAT(N'LD-',REPLACE(CONVERT(nvarchar(36),@DocumentId),N'-',N'')),@FileName,@ContentType,@DocumentTypeCode,CASE WHEN @MalwareStatusCode IN (N'CLEAN',N'NOT_DETECTED',N'PASSED') THEN N'RECEIVED' ELSE N'QUARANTINED' END,1,@TenantId,@UserId);
            INSERT POLOXI.Legal_MatterDocumentVersion
                (LegalDocumentVersionId,LegalDocumentId,VersionNumber,Sha256Hash,StorageReference,FileSizeBytes,MalwareStatusCode,ProcessingStatusCode,TenantId,CreatedByUserId)
            VALUES (NEWID(),@DocumentId,1,@Sha256Hash,@StorageReference,@FileSizeBytes,@MalwareStatusCode,CASE WHEN @MalwareStatusCode IN (N'CLEAN',N'NOT_DETECTED',N'PASSED') THEN N'RECEIVED' ELSE N'QUARANTINED' END,@TenantId,@UserId);
            COMMIT; SELECT @DocumentId;
            """;
        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(sql, new
        {
            DocumentId = documentId, request.TenantId, request.UserId, request.MatterId, request.DomainPackCode, request.FileName,
            request.ContentType, request.DocumentTypeCode, request.FileSizeBytes, Sha256Hash = sha256Hash,
            StorageReference = storageReference, MalwareStatusCode = malwareStatusCode
        }, cancellationToken: cancellationToken));
    }

    public async Task SaveExtractionAsync(Guid tenantId, Guid userId, Guid documentVersionId, string correlationId, DocumentExtractionResult extraction, IReadOnlyCollection<LegalDocumentPassageDto> passages, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocumentVersion WHERE LegalDocumentVersionId=@DocumentVersionId AND TenantId=@TenantId AND IsDeleted=0)
                THROW 50011, 'Legal document version was not found for this tenant.', 1;
            INSERT POLOXI.Legal_DocumentPage
                (LegalDocumentPageId,LegalDocumentVersionId,PageNumber,PageText,ExtractionMethodCode,ExtractionConfidence,Width,Height,MeasurementUnit,IsNativeTextReliable,ContentHash,TenantId,CreatedByUserId)
            SELECT LegalDocumentPageId,@DocumentVersionId,PageNumber,PageText,ExtractionMethodCode,ExtractionConfidence,Width,Height,MeasurementUnit,IsNativeTextReliable,ContentHash,@TenantId,@UserId
            FROM OPENJSON(@PagesJson) WITH
            (LegalDocumentPageId uniqueidentifier,PageNumber int,PageText nvarchar(max),ExtractionMethodCode nvarchar(60),ExtractionConfidence decimal(5,4),Width decimal(18,6),Height decimal(18,6),MeasurementUnit nvarchar(30),IsNativeTextReliable bit,ContentHash char(64)) source
            WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPage existing WHERE existing.LegalDocumentVersionId=@DocumentVersionId AND existing.PageNumber=source.PageNumber AND existing.IsDeleted=0);
            INSERT POLOXI.Legal_DocumentLayoutArtifact
                (LegalDocumentLayoutArtifactId,LegalDocumentVersionId,LegalDocumentPageId,ArtifactTypeCode,SequenceNumber,RoleCode,ArtifactText,Confidence,BoundingRegionJson,SourceSpanJson,ContentJson,ContentHash,TenantId,CreatedByUserId)
            SELECT LegalDocumentLayoutArtifactId,@DocumentVersionId,page.LegalDocumentPageId,ArtifactTypeCode,SequenceNumber,RoleCode,ArtifactText,Confidence,BoundingRegionJson,SourceSpanJson,ContentJson,ContentHash,@TenantId,@UserId
            FROM OPENJSON(@ArtifactsJson) WITH
            (LegalDocumentLayoutArtifactId uniqueidentifier,PageNumber int,ArtifactTypeCode nvarchar(40),SequenceNumber int,RoleCode nvarchar(80),ArtifactText nvarchar(max),Confidence decimal(5,4),BoundingRegionJson nvarchar(max),SourceSpanJson nvarchar(max),ContentJson nvarchar(max),ContentHash char(64)) source
            LEFT JOIN POLOXI.Legal_DocumentPage page ON page.LegalDocumentVersionId=@DocumentVersionId AND page.PageNumber=source.PageNumber AND page.IsDeleted=0
            WHERE NOT EXISTS
            (
                SELECT 1 FROM POLOXI.Legal_DocumentLayoutArtifact existing
                WHERE existing.LegalDocumentVersionId=@DocumentVersionId AND existing.ArtifactTypeCode=source.ArtifactTypeCode
                  AND ((existing.LegalDocumentPageId=page.LegalDocumentPageId) OR (existing.LegalDocumentPageId IS NULL AND page.LegalDocumentPageId IS NULL))
                  AND existing.SequenceNumber=source.SequenceNumber AND existing.IsDeleted=0
            );
            INSERT POLOXI.Legal_DocumentPassage
                (LegalDocumentPassageId,LegalDocumentVersionId,PageNumber,SectionPath,SequenceNumber,PassageText,ExtractionMethodCode,ExtractionConfidence,BoundingRegionJson,SourceSpanJson,EpistemicStateCode,ContentHash,TenantId,CreatedByUserId)
            SELECT LegalDocumentPassageId,@DocumentVersionId,PageNumber,SectionPath,SequenceNumber,PassageText,ExtractionMethodCode,ExtractionConfidence,BoundingRegionJson,SourceSpanJson,EpistemicStateCode,ContentHash,@TenantId,@UserId
            FROM OPENJSON(@PassagesJson) WITH
            (LegalDocumentPassageId uniqueidentifier,PageNumber int,SectionPath nvarchar(1000),SequenceNumber int,PassageText nvarchar(max),ExtractionMethodCode nvarchar(60),ExtractionConfidence decimal(5,4),BoundingRegionJson nvarchar(max),SourceSpanJson nvarchar(max),EpistemicStateCode nvarchar(40),ContentHash char(64)) source
            WHERE NOT EXISTS
            (
                SELECT 1 FROM POLOXI.Legal_DocumentPassage existing
                WHERE existing.LegalDocumentVersionId=@DocumentVersionId AND existing.SequenceNumber=source.SequenceNumber AND existing.IsDeleted=0
            );
            UPDATE version SET ProcessingStatusCode=N'PROCESSED',NativeTextAvailable=@NativeTextAvailable,ExtractionProviderCode=@ProviderCode,ExtractionModelCode=@ModelCode,ExtractionModelVersion=@ModelVersion,ProcessedDateUtc=@ProcessedDateUtc,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
            FROM POLOXI.Legal_MatterDocumentVersion version WHERE version.LegalDocumentVersionId=@DocumentVersionId AND version.TenantId=@TenantId;
            UPDATE document SET StatusCode=N'PROCESSED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
            FROM POLOXI.Legal_MatterDocument document
            INNER JOIN POLOXI.Legal_MatterDocumentVersion version ON version.LegalDocumentId=document.LegalDocumentId
            WHERE version.LegalDocumentVersionId=@DocumentVersionId AND document.TenantId=@TenantId;
            IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentProcessingRun WHERE LegalDocumentVersionId=@DocumentVersionId AND StageCode=N'EXTRACTION' AND StatusCode=N'COMPLETED' AND OutputHash=@OutputHash AND IsDeleted=0)
                INSERT POLOXI.Legal_DocumentProcessingRun
                    (LegalDocumentProcessingRunId,LegalDocumentVersionId,StageCode,StatusCode,ProviderCode,ModelCode,ModelVersion,OutputHash,ArtifactReference,AttemptCount,StartedDateUtc,CompletedDateUtc,DurationMilliseconds,CorrelationId,TenantId,CreatedByUserId)
                VALUES (NEWID(),@DocumentVersionId,N'EXTRACTION',N'COMPLETED',@ProviderCode,@ModelCode,@ModelVersion,@OutputHash,@RawResultReference,1,@ProcessedDateUtc,SYSUTCDATETIME(),DATEDIFF_BIG(millisecond,@ProcessedDateUtc,SYSUTCDATETIME()),@CorrelationId,@TenantId,@UserId);
            IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentSearchProjectionOutbox WHERE LegalDocumentVersionId=@DocumentVersionId AND IsDeleted=0)
                INSERT POLOXI.Legal_DocumentSearchProjectionOutbox (LegalDocumentSearchProjectionOutboxId,LegalDocumentVersionId,StatusCode,AttemptCount,NextAttemptDateUtc,TenantId,CreatedByUserId)
                VALUES (NEWID(),@DocumentVersionId,N'PENDING',0,SYSUTCDATETIME(),@TenantId,@UserId);
            COMMIT;
            """;
        var pageIds = extraction.Pages.ToDictionary(page => page.PageNumber, _ => Guid.NewGuid());
        var pageValues = extraction.Pages.Select(page => new
        {
            LegalDocumentPageId = pageIds[page.PageNumber], page.PageNumber, PageText = page.Text, page.ExtractionMethodCode,
            ExtractionConfidence = page.Confidence, page.Width, page.Height, MeasurementUnit = page.Unit, page.IsNativeTextReliable,
            ContentHash = Hash(page.Text)
        });
        var artifacts = CreateLayoutArtifacts(extraction);
        var values = passages.Select(p => new
        {
            p.LegalDocumentPassageId, p.PageNumber, p.SectionPath, p.SequenceNumber, PassageText = p.Text,
            p.ExtractionMethodCode, p.ExtractionConfidence, p.BoundingRegionJson, p.SourceSpanJson, p.EpistemicStateCode,
            ContentHash = Hash(p.Text)
        });
        var outputHash = Hash(string.Join('\n', extraction.Pages.OrderBy(page => page.PageNumber).Select(page => page.Text)));
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId, UserId = userId, DocumentVersionId = documentVersionId,
            extraction.ProviderCode, extraction.ModelCode, extraction.ModelVersion, extraction.ProcessedDateUtc,
            extraction.RawResultReference,
            CorrelationId = correlationId,
            OutputHash = outputHash,
            NativeTextAvailable = extraction.Pages.Any(page => page.ExtractionMethodCode == LegalDocumentExtractionMethods.NativeText),
            PagesJson = JsonSerializer.Serialize(pageValues),
            ArtifactsJson = JsonSerializer.Serialize(artifacts),
            PassagesJson = JsonSerializer.Serialize(values)
        }, cancellationToken: cancellationToken));
    }

    private static IReadOnlyCollection<object> CreateLayoutArtifacts(DocumentExtractionResult extraction)
    {
        var artifacts = new List<object>();
        foreach (var page in extraction.Pages)
        {
            artifacts.AddRange(page.Paragraphs.Select(item => Artifact(page.PageNumber, "PARAGRAPH", item.SequenceNumber, item.Role, item.Text, item.Confidence, item.BoundingRegionJson, item.SourceSpanJson, null)));
            artifacts.AddRange(page.ExtractedLines.Select(item => Artifact(page.PageNumber, "LINE", item.SequenceNumber, null, item.Text, null, item.PolygonJson, item.SourceSpanJson, null)));
            artifacts.AddRange(page.ExtractedWords.Select(item => Artifact(page.PageNumber, "WORD", item.SequenceNumber, null, item.Text, item.Confidence, item.PolygonJson, item.SourceSpanJson, null)));
            artifacts.AddRange(page.ExtractedSelectionMarks.Select(item => Artifact(page.PageNumber, "SELECTION_MARK", item.SequenceNumber, item.StateCode, null, item.Confidence, item.PolygonJson, item.SourceSpanJson, null)));
        }
        artifacts.AddRange(extraction.Sections.Select((item, index) => Artifact(item.StartPageNumber, "SECTION", index + 1, item.Title, item.Text, null, null, item.SourceSpanJson, JsonSerializer.Serialize(item))));
        artifacts.AddRange(extraction.Tables.Select((item, index) => Artifact(item.PageNumber, "TABLE", index + 1, null, null, null, item.BoundingRegionJson, item.SourceSpanJson, item.ContentJson)));
        artifacts.AddRange(extraction.ExtractedFigures.Select((item, index) => Artifact(item.PageNumber, "FIGURE", index + 1, item.FigureId, item.Caption, null, item.BoundingRegionJson, item.SourceSpanJson, item.ContentJson)));
        return artifacts;
    }

    private static object Artifact(int? pageNumber, string type, int sequence, string? role, string? text, decimal? confidence, string? bounds, string? span, string? content) => new
    {
        LegalDocumentLayoutArtifactId = Guid.NewGuid(), PageNumber = pageNumber, ArtifactTypeCode = type, SequenceNumber = sequence,
        RoleCode = role, ArtifactText = text, Confidence = confidence, BoundingRegionJson = bounds, SourceSpanJson = span, ContentJson = content,
        ContentHash = Hash(string.Join('|', type, sequence, role, text, bounds, span, content))
    };

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task SaveSemanticProposalAsync(Guid tenantId, Guid userId, Guid matterId, Guid documentId, Guid documentVersionId, LegalDocumentSemanticProposal proposal, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            var ownsDocument = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM POLOXI.Legal_MatterDocument document
                INNER JOIN POLOXI.Legal_MatterDocumentVersion version ON version.LegalDocumentId=document.LegalDocumentId
                WHERE document.LegalDocumentId=@DocumentId AND document.DecisionMatterId=@MatterId
                  AND version.LegalDocumentVersionId=@DocumentVersionId
                  AND document.TenantId=@TenantId AND version.TenantId=@TenantId
                  AND document.IsDeleted=0 AND version.IsDeleted=0;
                """, new { DocumentId = documentId, MatterId = matterId, DocumentVersionId = documentVersionId, TenantId = tenantId }, transaction, cancellationToken: cancellationToken));
            if (ownsDocument != 1)
                throw new InvalidOperationException("The document, version, and matter do not belong to the requested tenant scope.");

            var alreadyEnriched = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM POLOXI.Legal_MatterEvidenceItem WHERE LegalDocumentVersionId=@DocumentVersionId AND TenantId=@TenantId AND GenerationOriginCode=N'DYNAMIC_LLM' AND IsDeleted=0;",
                new { DocumentVersionId = documentVersionId, TenantId = tenantId }, transaction, cancellationToken: cancellationToken));
            if (alreadyEnriched > 0)
            {
                transaction.Commit();
                return;
            }

            var evidenceIds = proposal.EvidenceItems.ToDictionary(item => item.ProposalKey, _ => Guid.NewGuid(), StringComparer.OrdinalIgnoreCase);
            var factIds = proposal.FactPropositions.ToDictionary(item => item.ProposalKey, _ => Guid.NewGuid(), StringComparer.OrdinalIgnoreCase);
            foreach (var item in proposal.EvidenceItems)
                await connection.ExecuteAsync(new CommandDefinition(
                    """INSERT POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId,DecisionMatterId,LegalDocumentVersionId,LegalDocumentPassageId,EvidenceTypeCode,DimensionCode,Summary,EvidenceStateCode,Confidence,GenerationOriginCode,DomainConceptCode,VerificationProfileCode,TenantId,CreatedByUserId) VALUES (@Id,@MatterId,@VersionId,@PassageId,@EvidenceTypeCode,@DimensionCode,@Summary,N'PROPOSED',@Confidence,N'DYNAMIC_LLM',@DomainConceptCode,@VerificationProfileCode,@TenantId,@UserId);""",
                    new { Id = evidenceIds[item.ProposalKey], MatterId = matterId, VersionId = documentVersionId, PassageId = item.PassageId, item.EvidenceTypeCode, item.DimensionCode, item.Summary, item.Confidence, item.DomainConceptCode, item.VerificationProfileCode, TenantId = tenantId, UserId = userId }, transaction, cancellationToken: cancellationToken));
            foreach (var fact in proposal.FactPropositions)
                await connection.ExecuteAsync(new CommandDefinition(
                    """INSERT POLOXI.Legal_MatterFactProposition (LegalFactPropositionId,DecisionMatterId,PropositionText,FactStateCode,GenerationOriginCode,Confidence,IsDecisionAuthoritative,TenantId,CreatedByUserId) VALUES (@Id,@MatterId,@Text,@FactStateCode,N'DYNAMIC_LLM',@Confidence,0,@TenantId,@UserId);""",
                    new { Id = factIds[fact.ProposalKey], MatterId = matterId, Text = fact.PropositionText, fact.FactStateCode, fact.Confidence, TenantId = tenantId, UserId = userId }, transaction, cancellationToken: cancellationToken));
            foreach (var relationship in proposal.Relationships)
            {
                if (!factIds.TryGetValue(relationship.TargetProposalKey, out var factId) || !evidenceIds.TryGetValue(relationship.SourceProposalKey, out var evidenceId))
                    continue;
                await connection.ExecuteAsync(new CommandDefinition(
                    """INSERT POLOXI.Legal_MatterPropositionSupport (LegalPropositionSupportId,LegalFactPropositionId,LegalEvidenceItemId,RelationshipTypeCode,AssessmentReason,TenantId,CreatedByUserId) VALUES (NEWID(),@FactId,@EvidenceId,@RelationshipTypeCode,@Rationale,@TenantId,@UserId);""",
                    new { FactId = factId, EvidenceId = evidenceId, relationship.RelationshipTypeCode, relationship.Rationale, TenantId = tenantId, UserId = userId }, transaction, cancellationToken: cancellationToken));
            }
            await connection.ExecuteAsync(new CommandDefinition(
                """UPDATE POLOXI.Legal_MatterDocument SET DocumentTypeCode=COALESCE(@DocumentTypeCode,DocumentTypeCode),StatusCode=N'ENRICHED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE LegalDocumentId=@DocumentId AND DecisionMatterId=@MatterId AND TenantId=@TenantId AND IsDeleted=0;""",
                new { proposal.DocumentTypeCode, DocumentId = documentId, MatterId = matterId, TenantId = tenantId, UserId = userId }, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task PersistRetrievalTelemetryAsync(Guid tenantId, Guid userId, DecisionRetrievalTelemetry telemetry, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT POLOXI.Legal_DecisionRetrievalTelemetry
                (DecisionRetrievalTelemetryId,DecisionSessionId,DecisionMatterId,StageCode,EventCode,RouteCode,Enabled,CandidateCount,FilteredCount,ReturnedCount,ResearchNeedTypeCode,SourceClassCode,Jurisdiction,DetailJson,DurationMilliseconds,TenantId,CreatedByUserId)
            VALUES (@DecisionRetrievalTelemetryId,@DecisionSessionId,@MatterId,@StageCode,@EventCode,@RouteCode,@Enabled,@CandidateCount,@FilteredCount,@ReturnedCount,@ResearchNeedTypeCode,@SourceClassCode,@Jurisdiction,@DetailJson,@DurationMilliseconds,@TenantId,@UserId);
            """, new { telemetry.DecisionRetrievalTelemetryId, telemetry.DecisionSessionId, telemetry.MatterId, telemetry.StageCode, telemetry.EventCode, telemetry.RouteCode, telemetry.Enabled, telemetry.CandidateCount, telemetry.FilteredCount, telemetry.ReturnedCount, telemetry.ResearchNeedTypeCode, telemetry.SourceClassCode, telemetry.Jurisdiction, telemetry.DetailJson, telemetry.DurationMilliseconds, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    private static IReadOnlySet<string> SearchTerms(string query) => query
        .Split([' ', '\t', '\r', '\n', '-', '/', '.', ',', ';', ':', '(', ')', '?'], StringSplitOptions.RemoveEmptyEntries)
        .Where(term => term.Length >= 3)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static decimal Score(MatterContextRow row, IReadOnlySet<string> terms)
    {
        if (terms.Count == 0)
            return 0.5m;
        var text = $"{row.Title} {row.Text} {row.DocumentTypeCode} {row.DimensionCode}";
        var matches = terms.Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
        var score = (decimal)matches / terms.Count;
        if (row.IsDecisionAuthoritative)
            score += 0.05m;
        if (string.Equals(row.EvidenceStateCode, LegalEvidenceStates.Verified, StringComparison.OrdinalIgnoreCase))
            score += 0.05m;
        return Math.Min(score, 1m);
    }

    private static LegalDocumentVersionDto ToDto(DocumentVersionRow row) => new(row.LegalDocumentVersionId, row.VersionNumber,
        row.Sha256Hash, row.StorageReference, row.FileSizeBytes, row.MalwareStatusCode, row.ProcessingStatusCode,
        row.ExtractionProviderCode, row.ExtractionModelCode, row.ExtractionModelVersion, row.CreatedDateUtc);
    private static LegalDocumentPassageDto ToDto(PassageRow row) => new(row.LegalDocumentPassageId, row.LegalDocumentVersionId,
        row.PageNumber, row.SectionPath, row.SequenceNumber, row.Text, row.ExtractionMethodCode, row.ExtractionConfidence,
        row.BoundingRegionJson, row.SourceSpanJson, row.EpistemicStateCode);

    private sealed record DocumentRow(Guid LegalDocumentId, Guid MatterId, string FileName, string ContentType, string StatusCode, string? DocumentTypeCode, string? DomainPackCode, DateTime CreatedDateUtc);
    private sealed record DocumentVersionRow(Guid LegalDocumentVersionId, Guid LegalDocumentId, int VersionNumber, string Sha256Hash, string StorageReference, long FileSizeBytes, string MalwareStatusCode, string ProcessingStatusCode, string? ExtractionProviderCode, string? ExtractionModelCode, string? ExtractionModelVersion, DateTime CreatedDateUtc);
    private sealed record PassageRow(Guid LegalDocumentPassageId, Guid LegalDocumentVersionId, int? PageNumber, string? SectionPath, int SequenceNumber, string Text, string ExtractionMethodCode, decimal? ExtractionConfidence, string? BoundingRegionJson, string? SourceSpanJson, string EpistemicStateCode);
    private sealed record MatterContextRow(Guid MatterId, Guid LegalDocumentId, Guid LegalDocumentVersionId, Guid PassageId, Guid? EvidenceItemId, Guid? FactPropositionId, string Title, string? Text, string SourceReference, int? PageNumber, string ExtractionMethodCode, string EvidenceStateCode, string FactStateCode, bool IsDecisionAuthoritative, string? DocumentTypeCode, string? DimensionCode);
    private sealed record LegacyProjectionRow(Guid SearchDocumentId, Guid EntityId, string Title, string Text);
}
