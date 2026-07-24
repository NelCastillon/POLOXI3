using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCertificates;
using Dapper;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CertificateWorkflowRepository : ICertificateWorkflowRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CertificateWorkflowRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CertificateWorkflowWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT CertificateWorkflowOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder
FROM Policy.CertificateWorkflowOption WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 ORDER BY OptionGroupCode, SortOrder, DisplayName;
SELECT CertificateHolderId, TenantId, HolderCode, LegalName, AddressLine1, AddressLine2, City, StateProvince, PostalCode, CountryCode, ContactName, EmailAddress, PhoneNumber, PreferredDeliveryMethodCode, DefaultWording, RequiresAdditionalInsured, RequiresWaiverOfSubrogation, RequiresPrimaryNonContributory, IsActive, CreatedDateUtc, ModifiedDateUtc
FROM Policy.CertificateHolder WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY LegalName;
SELECT DocumentTemplateDefinitionId, TenantId, TemplateCode, TemplateName, DocumentTypeCode, FormNumber, LineOfBusinessCode, Description, IsLicensedContent, IsActive, CurrentVersionNumber
FROM DMS.DocumentTemplateDefinition WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY TemplateName;
SELECT v.DocumentTemplateVersionId, v.TenantId, v.DocumentTemplateDefinitionId, v.VersionNumber, v.EditionCode, v.ContentFormatCode, v.TemplateContent, v.StoragePath, v.MergeFieldSchemaJson, v.ChangeSummary, v.StatusCode, v.EffectiveDateUtc, v.RetiredDateUtc, v.CreatedDateUtc
FROM DMS.DocumentTemplateVersion v INNER JOIN DMS.DocumentTemplateDefinition d ON d.DocumentTemplateDefinitionId = v.DocumentTemplateDefinitionId
WHERE v.TenantId = @TenantId AND v.IsDeleted = 0 AND d.IsDeleted = 0 ORDER BY v.DocumentTemplateDefinitionId, v.VersionNumber DESC;
SELECT r.CertificateRequestId, r.TenantId, r.RequestNumber, r.PolicyId, r.PolicyNumber, r.CertificateHolderId, h.LegalName AS HolderName, r.RequestedDocumentTypeCode, r.RequestedWording, r.AdditionalInsured, r.WaiverOfSubrogation, r.PrimaryNonContributory, r.SourceCode, r.StatusCode, r.PriorityCode, r.NeededByDateUtc, r.RequestedByUserId, r.RequestedByName, r.RequestedByEmail, r.AssignedToUserId, r.CompletedCertificateId, r.SubmittedDateUtc, r.CompletedDateUtc
FROM Policy.CertificateRequest r LEFT JOIN Policy.CertificateHolder h ON h.CertificateHolderId = r.CertificateHolderId AND h.IsDeleted = 0
WHERE r.TenantId = @TenantId AND r.IsDeleted = 0 ORDER BY r.SubmittedDateUtc DESC;
SELECT CertificateRenewalScheduleId, TenantId, CertificateId, CertificateHolderId, RenewalLeadDays, NextRunDateUtc, StatusCode, AutoGenerate, AutoDeliver, LastRunDateUtc, LastResultCode, LastError, LockedUntilDateUtc
FROM Policy.CertificateRenewalSchedule WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY NextRunDateUtc;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var options = (await multi.ReadAsync<CertificateWorkflowOptionDto>()).AsList();
        var holders = (await multi.ReadAsync<CertificateHolderDto>()).AsList();
        var templateRows = (await multi.ReadAsync<TemplateRow>()).AsList();
        var versions = (await multi.ReadAsync<DocumentTemplateVersionDto>()).AsList();
        var requests = (await multi.ReadAsync<CertificateRequestDto>()).AsList();
        var schedules = (await multi.ReadAsync<CertificateRenewalScheduleDto>()).AsList();
        var templates = templateRows.Select(row => new DocumentTemplateDefinitionDto(
            row.DocumentTemplateDefinitionId, row.TenantId, row.TemplateCode, row.TemplateName, row.DocumentTypeCode,
            row.FormNumber, row.LineOfBusinessCode, row.Description, row.IsLicensedContent, row.IsActive,
            row.CurrentVersionNumber, versions.Where(version => version.DocumentTemplateDefinitionId == row.DocumentTemplateDefinitionId).ToList())).ToList();
        return new(options, holders, templates, requests, schedules);
    }

    public async Task<IReadOnlyList<CertificateAuditEventDto>> GetAuditAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT CertificateAuditEventId, TenantId, CertificateId, CertificateRequestId, EventTypeCode, EventDescription, OldValueJson, NewValueJson, ActorUserId, ActorName, CorrelationId, CreatedDateUtc
FROM Policy.CertificateAuditEvent WHERE TenantId = @TenantId AND CertificateId = @CertificateId ORDER BY CreatedDateUtc DESC;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<CertificateAuditEventDto>(new CommandDefinition(sql, new { TenantId = tenantId, CertificateId = certificateId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<CertificateDeliveryDto>> GetDeliveriesAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT CertificateDeliveryId, TenantId, CertificateId, GeneratedDocumentVersionId, DeliveryMethodCode, RecipientName, RecipientAddress, StatusCode, ProviderMessageId, QueuedDateUtc, SentDateUtc, DeliveredDateUtc, FailedDateUtc, FailureReason, AttemptCount
FROM Policy.CertificateDelivery WHERE TenantId = @TenantId AND CertificateId = @CertificateId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<CertificateDeliveryDto>(new CommandDefinition(sql, new { TenantId = tenantId, CertificateId = certificateId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid?> GetLatestGeneratedDocumentVersionIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP 1 v.GeneratedDocumentVersionId
FROM DMS.GeneratedDocumentVersion v
INNER JOIN DMS.GeneratedDocument d ON d.GeneratedDocumentId=v.GeneratedDocumentId AND d.TenantId=v.TenantId
WHERE v.TenantId=@TenantId AND d.EntityTypeCode=N'PolicyCertificate' AND d.EntityId=@CertificateId AND v.IsDeleted=0 AND d.IsDeleted=0
ORDER BY v.VersionNumber DESC, v.CreatedDateUtc DESC;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { TenantId = tenantId, CertificateId = certificateId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertHolderAsync(UpsertCertificateHolderRequest request, CancellationToken cancellationToken = default)
    {
        var holderId = request.CertificateHolderId ?? Guid.NewGuid();
        const string sql = """
IF @CertificateHolderId IS NOT NULL AND EXISTS (SELECT 1 FROM Policy.CertificateHolder WHERE CertificateHolderId = @CertificateHolderId AND TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    UPDATE Policy.CertificateHolder SET HolderCode=@HolderCode, LegalName=@LegalName, AddressLine1=@AddressLine1, AddressLine2=@AddressLine2, City=@City, StateProvince=@StateProvince, PostalCode=@PostalCode, CountryCode=@CountryCode, ContactName=@ContactName, EmailAddress=@EmailAddress, PhoneNumber=@PhoneNumber, PreferredDeliveryMethodCode=@PreferredDeliveryMethodCode, DefaultWording=@DefaultWording, RequiresAdditionalInsured=@RequiresAdditionalInsured, RequiresWaiverOfSubrogation=@RequiresWaiverOfSubrogation, RequiresPrimaryNonContributory=@RequiresPrimaryNonContributory, IsActive=@IsActive, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@UserId
    WHERE CertificateHolderId=@CertificateHolderId AND TenantId=@TenantId AND IsDeleted=0;
END
ELSE
BEGIN
    INSERT INTO Policy.CertificateHolder (CertificateHolderId,TenantId,HolderCode,LegalName,AddressLine1,AddressLine2,City,StateProvince,PostalCode,CountryCode,ContactName,EmailAddress,PhoneNumber,PreferredDeliveryMethodCode,DefaultWording,RequiresAdditionalInsured,RequiresWaiverOfSubrogation,RequiresPrimaryNonContributory,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES (@ResolvedHolderId,@TenantId,@HolderCode,@LegalName,@AddressLine1,@AddressLine2,@City,@StateProvince,@PostalCode,@CountryCode,@ContactName,@EmailAddress,@PhoneNumber,@PreferredDeliveryMethodCode,@DefaultWording,@RequiresAdditionalInsured,@RequiresWaiverOfSubrogation,@RequiresPrimaryNonContributory,@IsActive,SYSUTCDATETIME(),@UserId,0);
END;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.CertificateHolderId,
            ResolvedHolderId = holderId,
            request.TenantId,
            HolderCode = request.HolderCode.Trim(),
            LegalName = request.LegalName.Trim(),
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateProvince,
            request.PostalCode,
            request.CountryCode,
            request.ContactName,
            request.EmailAddress,
            request.PhoneNumber,
            request.PreferredDeliveryMethodCode,
            request.DefaultWording,
            request.RequiresAdditionalInsured,
            request.RequiresWaiverOfSubrogation,
            request.RequiresPrimaryNonContributory,
            request.IsActive,
            request.UserId
        }, cancellationToken: cancellationToken));
        return holderId;
    }

    public async Task<Guid> CreateTemplateVersionAsync(CreateDocumentTemplateVersionRequest request, CancellationToken cancellationToken = default)
    {
        _ = JsonDocument.Parse(request.MergeFieldSchemaJson);
        const string sql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM DMS.DocumentTemplateDefinition WITH (UPDLOCK,HOLDLOCK) WHERE DocumentTemplateDefinitionId=@DocumentTemplateDefinitionId AND TenantId=@TenantId AND IsDeleted=0) THROW 51000, 'Template definition was not found for this tenant.', 1;
DECLARE @VersionNumber INT = (SELECT ISNULL(MAX(VersionNumber),0)+1 FROM DMS.DocumentTemplateVersion WITH (UPDLOCK,HOLDLOCK) WHERE DocumentTemplateDefinitionId=@DocumentTemplateDefinitionId AND IsDeleted=0);
DECLARE @Id UNIQUEIDENTIFIER=NEWID();
INSERT INTO DMS.DocumentTemplateVersion (DocumentTemplateVersionId,TenantId,DocumentTemplateDefinitionId,VersionNumber,EditionCode,ContentFormatCode,TemplateContent,StoragePath,MergeFieldSchemaJson,ChangeSummary,StatusCode,EffectiveDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@Id,@TenantId,@DocumentTemplateDefinitionId,@VersionNumber,@EditionCode,@ContentFormatCode,@TemplateContent,@StoragePath,@MergeFieldSchemaJson,@ChangeSummary,@StatusCode,@EffectiveDateUtc,SYSUTCDATETIME(),@CreatedByUserId,0);
UPDATE DMS.DocumentTemplateDefinition SET CurrentVersionNumber=@VersionNumber,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CreatedByUserId WHERE DocumentTemplateDefinitionId=@DocumentTemplateDefinitionId;
COMMIT;
SELECT @Id;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateRequestAsync(CreateCertificateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var number = $"CR-{DateTime.UtcNow:yyyyMMdd}-{id:N}"[..24].ToUpperInvariant();
        const string sql = """
SET XACT_ABORT ON; BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM Policy.CertificateHolder WHERE CertificateHolderId=@CertificateHolderId AND TenantId=@TenantId AND IsDeleted=0 AND IsActive=1) THROW 51001, 'Certificate holder was not found or is inactive.', 1;
INSERT INTO Policy.CertificateRequest (CertificateRequestId,TenantId,RequestNumber,PolicyId,PolicyNumber,CertificateHolderId,RequestedDocumentTypeCode,RequestedWording,AdditionalInsured,WaiverOfSubrogation,PrimaryNonContributory,SourceCode,StatusCode,PriorityCode,NeededByDateUtc,RequestedByUserId,RequestedByName,RequestedByEmail,SubmittedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@Id,@TenantId,@Number,@PolicyId,@PolicyNumber,@CertificateHolderId,@RequestedDocumentTypeCode,@RequestedWording,@AdditionalInsured,@WaiverOfSubrogation,@PrimaryNonContributory,@SourceCode,N'Submitted',@PriorityCode,@NeededByDateUtc,@RequestedByUserId,@RequestedByName,@RequestedByEmail,SYSUTCDATETIME(),SYSUTCDATETIME(),@RequestedByUserId,0);
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateRequestId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,ActorName,CreatedDateUtc)
VALUES (NEWID(),@TenantId,@Id,N'RequestSubmitted',N'Certificate request submitted through the enterprise workflow.',JSON_OBJECT(N'RequestNumber':@Number,N'Source':@SourceCode,N'DocumentType':@RequestedDocumentTypeCode),@RequestedByUserId,@RequestedByName,SYSUTCDATETIME());
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            Number = number,
            request.TenantId,
            request.PolicyId,
            request.PolicyNumber,
            request.CertificateHolderId,
            request.RequestedDocumentTypeCode,
            request.RequestedWording,
            request.AdditionalInsured,
            request.WaiverOfSubrogation,
            request.PrimaryNonContributory,
            request.SourceCode,
            request.PriorityCode,
            request.NeededByDateUtc,
            request.RequestedByUserId,
            request.RequestedByName,
            request.RequestedByEmail
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<CertificateGenerationResultDto> GenerateAsync(GenerateCertificateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        using var mergeData = JsonDocument.Parse(request.MergeDataJson);
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        const string templateSql = """
SELECT TOP 1 d.DocumentTemplateDefinitionId,d.TemplateCode,d.TemplateName,d.DocumentTypeCode,d.FormNumber,d.IsLicensedContent,v.DocumentTemplateVersionId,v.VersionNumber,v.ContentFormatCode,v.TemplateContent,v.StoragePath,v.StatusCode
FROM DMS.DocumentTemplateDefinition d
INNER JOIN DMS.DocumentTemplateVersion v ON v.DocumentTemplateDefinitionId=d.DocumentTemplateDefinitionId AND v.IsDeleted=0
WHERE d.TenantId=@TenantId AND d.DocumentTemplateDefinitionId=@DocumentTemplateDefinitionId AND d.IsDeleted=0 AND d.IsActive=1 AND (@DocumentTemplateVersionId IS NULL OR v.DocumentTemplateVersionId=@DocumentTemplateVersionId)
ORDER BY CASE WHEN @DocumentTemplateVersionId IS NOT NULL AND v.DocumentTemplateVersionId=@DocumentTemplateVersionId THEN 0 ELSE 1 END,v.VersionNumber DESC;
""";
        var template = await connection.QuerySingleOrDefaultAsync<RenderTemplate>(new CommandDefinition(templateSql, request, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("No published template version is available for this tenant.");
        if (!string.Equals(template.StatusCode, "Published", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only published template versions can generate documents.");
        if (template.IsLicensedContent && string.IsNullOrWhiteSpace(template.StoragePath))
            throw new InvalidOperationException("Licensed template artwork is not configured. Upload the licensed template before generating this form.");
        if (!string.IsNullOrWhiteSpace(template.StoragePath))
            throw new InvalidOperationException("Stored template rendering requires a configured document rendering provider.");
        var certificateExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM Policy.PolicyCertificate WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0) THEN 1 ELSE 0 END AS bit);",
            request, transaction, cancellationToken: cancellationToken));
        if (!certificateExists) throw new InvalidOperationException("Certificate was not found for this tenant.");

        var rendered = RenderTemplateContent(template, mergeData.RootElement);
        var content = Encoding.UTF8.GetBytes(rendered);
        var contentType = "text/html";
        var contentHash = Convert.ToHexString(SHA256.HashData(content));
        var existingDocumentId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT GeneratedDocumentId FROM Policy.PolicyCertificate WHERE CertificateId=@CertificateId AND TenantId=@TenantId;", request, transaction, cancellationToken: cancellationToken));
        var generatedDocumentId = existingDocumentId.GetValueOrDefault();
        var versionNumber = 1;
        if (generatedDocumentId == Guid.Empty)
        {
            generatedDocumentId = Guid.NewGuid();
            var documentNumber = $"DOC-{DateTime.UtcNow:yyyyMMdd}-{generatedDocumentId:N}"[..25].ToUpperInvariant();
            await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO DMS.GeneratedDocument (GeneratedDocumentId,TenantId,DocumentNumber,DocumentTypeCode,EntityTypeCode,EntityId,TemplateDefinitionId,CurrentVersionNumber,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@GeneratedDocumentId,@TenantId,@DocumentNumber,@DocumentTypeCode,N'PolicyCertificate',@CertificateId,@TemplateDefinitionId,1,N'Generated',SYSUTCDATETIME(),@UserId,0);
""", new { GeneratedDocumentId = generatedDocumentId, request.TenantId, DocumentNumber = documentNumber, template.DocumentTypeCode, request.CertificateId, TemplateDefinitionId = template.DocumentTemplateDefinitionId, request.UserId }, transaction, cancellationToken: cancellationToken));
        }
        else
        {
            versionNumber = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT ISNULL(MAX(VersionNumber),0)+1 FROM DMS.GeneratedDocumentVersion WITH (UPDLOCK,HOLDLOCK) WHERE GeneratedDocumentId=@GeneratedDocumentId AND IsDeleted=0;",
                new { GeneratedDocumentId = generatedDocumentId }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("UPDATE DMS.GeneratedDocument SET CurrentVersionNumber=@VersionNumber,TemplateDefinitionId=@TemplateDefinitionId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE GeneratedDocumentId=@GeneratedDocumentId AND TenantId=@TenantId;",
                new { VersionNumber = versionNumber, TemplateDefinitionId = template.DocumentTemplateDefinitionId, request.UserId, GeneratedDocumentId = generatedDocumentId, request.TenantId }, transaction, cancellationToken: cancellationToken));
        }
        var generatedVersionId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO DMS.GeneratedDocumentVersion (GeneratedDocumentVersionId,TenantId,GeneratedDocumentId,DocumentTemplateVersionId,VersionNumber,MergeDataJson,RenderedContent,ContentType,ContentHash,FileSizeBytes,ChangeSummary,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@GeneratedVersionId,@TenantId,@GeneratedDocumentId,@TemplateVersionId,@VersionNumber,@MergeDataJson,@Content,@ContentType,@ContentHash,@FileSize,@ChangeSummary,SYSUTCDATETIME(),@UserId,0);
UPDATE Policy.PolicyCertificate SET DocumentTemplateVersionId=@TemplateVersionId,GeneratedDocumentId=@GeneratedDocumentId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc)
VALUES (NEWID(),@TenantId,@CertificateId,N'DocumentGenerated',N'Certificate document version generated.',JSON_OBJECT(N'GeneratedDocumentId':@GeneratedDocumentId,N'Version':@VersionNumber,N'TemplateVersionId':@TemplateVersionId,N'ContentHash':@ContentHash),@UserId,SYSUTCDATETIME());
""", new { GeneratedVersionId = generatedVersionId, request.TenantId, GeneratedDocumentId = generatedDocumentId, TemplateVersionId = template.DocumentTemplateVersionId, VersionNumber = versionNumber, MergeDataJson = request.MergeDataJson, Content = content, ContentType = contentType, ContentHash = contentHash, FileSize = content.LongLength, request.ChangeSummary, request.UserId, request.CertificateId }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return new(request.CertificateId, generatedDocumentId, generatedVersionId, versionNumber, contentType, content);
    }

    public async Task<Guid> QueueDeliveryAsync(QueueCertificateDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
SET XACT_ABORT ON; BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM Policy.PolicyCertificate WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0) THROW 51002, 'Certificate was not found for this tenant.', 1;
IF NOT EXISTS (SELECT 1 FROM Policy.CertificateWorkflowOption WHERE TenantId=@TenantId AND OptionGroupCode=N'DeliveryMethod' AND OptionCode=@DeliveryMethodCode AND IsActive=1 AND IsDeleted=0) THROW 51003, 'Delivery method is not enabled for this tenant.', 1;
IF @GeneratedDocumentVersionId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DMS.GeneratedDocumentVersion v INNER JOIN DMS.GeneratedDocument d ON d.GeneratedDocumentId=v.GeneratedDocumentId AND d.TenantId=v.TenantId WHERE v.GeneratedDocumentVersionId=@GeneratedDocumentVersionId AND v.TenantId=@TenantId AND d.EntityTypeCode=N'PolicyCertificate' AND d.EntityId=@CertificateId AND v.IsDeleted=0 AND d.IsDeleted=0) THROW 51005, 'Generated document version does not belong to this certificate.', 1;
INSERT INTO Policy.CertificateDelivery (CertificateDeliveryId,TenantId,CertificateId,GeneratedDocumentVersionId,DeliveryMethodCode,RecipientName,RecipientAddress,StatusCode,QueuedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@Id,@TenantId,@CertificateId,@GeneratedDocumentVersionId,@DeliveryMethodCode,@RecipientName,@RecipientAddress,N'Queued',SYSUTCDATETIME(),SYSUTCDATETIME(),@UserId,0);
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc)
VALUES (NEWID(),@TenantId,@CertificateId,N'DeliveryQueued',N'Certificate delivery queued.',JSON_OBJECT(N'DeliveryId':@Id,N'Method':@DeliveryMethodCode,N'Recipient':@RecipientAddress),@UserId,SYSUTCDATETIME());
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CertificateId, request.GeneratedDocumentVersionId, request.DeliveryMethodCode, request.RecipientName, request.RecipientAddress, request.UserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> UpsertRenewalScheduleAsync(UpsertCertificateRenewalScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
SET XACT_ABORT ON; BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM Policy.PolicyCertificate WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0) THROW 51004, 'Certificate was not found for this tenant.', 1;
DECLARE @Existing UNIQUEIDENTIFIER=(SELECT TOP 1 CertificateRenewalScheduleId FROM Policy.CertificateRenewalSchedule WITH (UPDLOCK,HOLDLOCK) WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0);
IF @Existing IS NULL
BEGIN
 INSERT INTO Policy.CertificateRenewalSchedule (CertificateRenewalScheduleId,TenantId,CertificateId,CertificateHolderId,RenewalLeadDays,NextRunDateUtc,StatusCode,AutoGenerate,AutoDeliver,CreatedDateUtc,CreatedByUserId,IsDeleted)
 VALUES (@Id,@TenantId,@CertificateId,@CertificateHolderId,@RenewalLeadDays,@NextRunDateUtc,N'Scheduled',@AutoGenerate,@AutoDeliver,SYSUTCDATETIME(),@UserId,0);
END
ELSE
BEGIN
 SET @Id=@Existing;
 UPDATE Policy.CertificateRenewalSchedule SET CertificateHolderId=@CertificateHolderId,RenewalLeadDays=@RenewalLeadDays,NextRunDateUtc=@NextRunDateUtc,StatusCode=N'Scheduled',AutoGenerate=@AutoGenerate,AutoDeliver=@AutoDeliver,LastError=NULL,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE CertificateRenewalScheduleId=@Existing;
END;
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc)
VALUES (NEWID(),@TenantId,@CertificateId,N'RenewalScheduled',N'Certificate renewal schedule saved.',JSON_OBJECT(N'ScheduleId':@Id,N'NextRun':CONVERT(NVARCHAR(30),@NextRunDateUtc,126),N'LeadDays':@RenewalLeadDays,N'AutoGenerate':@AutoGenerate,N'AutoDeliver':@AutoDeliver),@UserId,SYSUTCDATETIME());
COMMIT; SELECT @Id;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id = id, request.TenantId, request.CertificateId, request.CertificateHolderId, request.RenewalLeadDays, request.NextRunDateUtc, request.AutoGenerate, request.AutoDeliver, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task<int> ProcessDueRenewalsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRANSACTION;
DECLARE @Claimed TABLE (ScheduleId UNIQUEIDENTIFIER,TenantId UNIQUEIDENTIFIER,CertificateId UNIQUEIDENTIFIER,HolderId UNIQUEIDENTIFIER,LeadDays INT,AutoGenerate BIT,AutoDeliver BIT);
;WITH Due AS
(
 SELECT TOP (@BatchSize) * FROM Policy.CertificateRenewalSchedule WITH (UPDLOCK,READPAST,ROWLOCK)
 WHERE IsDeleted=0 AND StatusCode=N'Scheduled' AND NextRunDateUtc<=SYSUTCDATETIME() AND (LockedUntilDateUtc IS NULL OR LockedUntilDateUtc<SYSUTCDATETIME())
 ORDER BY NextRunDateUtc
)
UPDATE Due SET LockedUntilDateUtc=DATEADD(minute,5,SYSUTCDATETIME()),LastRunDateUtc=SYSUTCDATETIME(),StatusCode=N'Processing'
OUTPUT inserted.CertificateRenewalScheduleId,inserted.TenantId,inserted.CertificateId,inserted.CertificateHolderId,inserted.RenewalLeadDays,inserted.AutoGenerate,inserted.AutoDeliver INTO @Claimed;
INSERT INTO Policy.CertificateRequest (CertificateRequestId,TenantId,RequestNumber,PolicyId,PolicyNumber,CertificateHolderId,RequestedDocumentTypeCode,RequestedWording,AdditionalInsured,WaiverOfSubrogation,PrimaryNonContributory,SourceCode,StatusCode,PriorityCode,SubmittedDateUtc,CreatedDateUtc,IsDeleted)
SELECT NEWID(),x.TenantId,CONCAT(N'REN-',CONVERT(char(8),SYSUTCDATETIME(),112),N'-',RIGHT(REPLACE(CONVERT(NVARCHAR(36),x.ScheduleId),N'-',N''),8)),c.PolicyId,c.PolicyNumber,x.HolderId,c.CertificateType,COALESCE(c.HolderSpecificWording,c.Description),c.AdditionalInsured,c.WaiverSubrogation,c.PrimaryNonContributory,N'ScheduledRenewal',CASE WHEN x.AutoGenerate=1 THEN N'ReadyToGenerate' ELSE N'Submitted' END,N'Normal',SYSUTCDATETIME(),SYSUTCDATETIME(),0
FROM @Claimed x INNER JOIN Policy.PolicyCertificate c ON c.CertificateId=x.CertificateId AND c.TenantId=x.TenantId AND c.IsDeleted=0
WHERE NOT EXISTS (SELECT 1 FROM Policy.CertificateRequest r WHERE r.TenantId=x.TenantId AND r.PolicyNumber=c.PolicyNumber AND r.CertificateHolderId=x.HolderId AND r.SourceCode=N'ScheduledRenewal' AND r.SubmittedDateUtc>=DATEADD(day,-1,SYSUTCDATETIME()) AND r.IsDeleted=0);
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,CreatedDateUtc)
SELECT NEWID(),x.TenantId,x.CertificateId,N'RenewalDue',N'Scheduled certificate renewal was processed.',JSON_OBJECT(N'ScheduleId':x.ScheduleId,N'AutoGenerate':x.AutoGenerate,N'AutoDeliver':x.AutoDeliver),SYSUTCDATETIME() FROM @Claimed x;
UPDATE s SET StatusCode=N'Scheduled',NextRunDateUtc=DATEADD(year,1,s.NextRunDateUtc),LastResultCode=CASE WHEN x.AutoGenerate=1 OR x.AutoDeliver=1 THEN N'AwaitingAutomation' ELSE N'RequestCreated' END,LastError=CASE WHEN x.AutoGenerate=1 OR x.AutoDeliver=1 THEN N'Renewal request created; document generation and delivery require configured processors.' ELSE NULL END,LockedUntilDateUtc=NULL,ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.CertificateRenewalSchedule s INNER JOIN @Claimed x ON x.ScheduleId=s.CertificateRenewalScheduleId;
DECLARE @Count INT=(SELECT COUNT(*) FROM @Claimed); COMMIT; SELECT @Count;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { BatchSize = Math.Clamp(batchSize, 1, 250) }, cancellationToken: cancellationToken));
    }

    private static string RenderTemplateContent(RenderTemplate template, JsonElement mergeData)
    {
        var content = template.TemplateContent;
        if (string.IsNullOrWhiteSpace(content))
        {
            var rows = mergeData.ValueKind == JsonValueKind.Object
                ? string.Join(string.Empty, mergeData.EnumerateObject().Select(property => $"<tr><th>{WebUtility.HtmlEncode(property.Name)}</th><td>{WebUtility.HtmlEncode(JsonValue(property.Value))}</td></tr>"))
                : $"<tr><th>Data</th><td>{WebUtility.HtmlEncode(mergeData.GetRawText())}</td></tr>";
            content = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(template.TemplateName)}</title></head><body><main><h1>{WebUtility.HtmlEncode(template.TemplateName)}</h1><p>Form reference: {WebUtility.HtmlEncode(template.FormNumber ?? template.TemplateCode)}. Render against the tenant's licensed artwork before external delivery.</p><table>{rows}</table></main></body></html>";
        }
        if (mergeData.ValueKind != JsonValueKind.Object) return content;
        foreach (var property in mergeData.EnumerateObject())
            content = content.Replace("{{" + property.Name + "}}", WebUtility.HtmlEncode(JsonValue(property.Value)), StringComparison.OrdinalIgnoreCase);
        return content;
    }

    private static string JsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null => string.Empty,
        _ => value.GetRawText()
    };

    private sealed record TemplateRow(Guid DocumentTemplateDefinitionId, Guid TenantId, string TemplateCode, string TemplateName, string DocumentTypeCode, string? FormNumber, string? LineOfBusinessCode, string? Description, bool IsLicensedContent, bool IsActive, int CurrentVersionNumber);
    private sealed record RenderTemplate(Guid DocumentTemplateDefinitionId, string TemplateCode, string TemplateName, string DocumentTypeCode, string? FormNumber, bool IsLicensedContent, Guid DocumentTemplateVersionId, int VersionNumber, string ContentFormatCode, string? TemplateContent, string? StoragePath, string StatusCode);
}
