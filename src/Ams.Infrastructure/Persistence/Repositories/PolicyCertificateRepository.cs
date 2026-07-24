using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyCertificates;
using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyCertificateRepository : IPolicyCertificateRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyCertificateRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PagedResult<PolicyCertificateDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? certificateType, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT c.CertificateId, c.TenantId,
           COALESCE(c.PolicyId, p.PolicyId) AS PolicyId, c.CertificateHolderId, c.CertificateRequestId,
           c.DocumentTemplateVersionId, c.GeneratedDocumentId,
           c.CertificateNumber, c.PolicyNumber, c.AccountName, c.HolderName, c.HolderAddress,
           c.CertificateType, c.IssuedDate, c.ExpirationDate, c.LineOfBusiness, c.IssuedBy,
           c.Status, c.AdditionalInsured, c.WaiverSubrogation, c.PrimaryNonContributory, c.Description, c.HolderSpecificWording,
           c.LastDeliveredDateUtc, c.RevokedDateUtc, c.RevokedByUserId, c.RevokeReason,
           c.CreatedDateUtc, c.CreatedByUserId, c.ModifiedDateUtc, c.ModifiedByUserId
    FROM Policy.PolicyCertificate c
    LEFT JOIN Submissions.BoundPolicy p ON p.TenantId = c.TenantId AND p.PolicyNumber = c.PolicyNumber AND p.IsDeleted = 0
    WHERE c.TenantId = @TenantId
      AND c.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR c.CertificateNumber LIKE N'%' + @SearchTerm + N'%' OR c.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR c.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.HolderName LIKE N'%' + @SearchTerm + N'%' OR c.HolderAddress LIKE N'%' + @SearchTerm + N'%' OR c.LineOfBusiness LIKE N'%' + @SearchTerm + N'%' OR c.IssuedBy LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT * FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@CertificateType IS NULL OR @CertificateType = N'' OR CertificateType = @CertificateType)
)
SELECT * FROM Filtered
ORDER BY CASE WHEN Status = N'Pending' THEN 0 WHEN Status = N'Issued' AND ExpirationDate BETWEEN CAST(SYSUTCDATETIME() AS date) AND DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date)) THEN 1 WHEN Status = N'Issued' THEN 2 ELSE 3 END,
         ExpirationDate ASC, CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT c.Status, c.CertificateType
    FROM Policy.PolicyCertificate c
    WHERE c.TenantId = @TenantId
      AND c.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR c.CertificateNumber LIKE N'%' + @SearchTerm + N'%' OR c.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR c.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.HolderName LIKE N'%' + @SearchTerm + N'%' OR c.HolderAddress LIKE N'%' + @SearchTerm + N'%' OR c.LineOfBusiness LIKE N'%' + @SearchTerm + N'%' OR c.IssuedBy LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT * FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@CertificateType IS NULL OR @CertificateType = N'' OR CertificateType = @CertificateType)
)
SELECT COUNT(1) FROM Filtered;";

        var safePageSize = Math.Clamp(pageSize, 1, 500);
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Status = status,
            CertificateType = certificateType,
            Offset = (Math.Max(pageNumber, 1) - 1) * safePageSize,
            PageSize = safePageSize,
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<PolicyCertificateDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyCertificateDto> { Items = items, TotalCount = total, PageNumber = Math.Max(pageNumber, 1), PageSize = safePageSize };
    }

    public async Task<PolicyCertificateDto?> GetByIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.CertificateId, c.TenantId,
       COALESCE(c.PolicyId, p.PolicyId) AS PolicyId, c.CertificateHolderId, c.CertificateRequestId,
       c.DocumentTemplateVersionId, c.GeneratedDocumentId,
       c.CertificateNumber, c.PolicyNumber, c.AccountName, c.HolderName, c.HolderAddress,
       c.CertificateType, c.IssuedDate, c.ExpirationDate, c.LineOfBusiness, c.IssuedBy,
       c.Status, c.AdditionalInsured, c.WaiverSubrogation, c.PrimaryNonContributory, c.Description, c.HolderSpecificWording,
       c.LastDeliveredDateUtc, c.RevokedDateUtc, c.RevokedByUserId, c.RevokeReason,
       c.CreatedDateUtc, c.CreatedByUserId, c.ModifiedDateUtc, c.ModifiedByUserId
FROM Policy.PolicyCertificate c
LEFT JOIN Submissions.BoundPolicy p ON p.TenantId = c.TenantId AND p.PolicyNumber = c.PolicyNumber AND p.IsDeleted = 0
WHERE c.TenantId = @TenantId AND c.CertificateId = @CertificateId AND c.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyCertificateDto>(new CommandDefinition(sql, new { TenantId = tenantId, CertificateId = certificateId }, cancellationToken: cancellationToken));
    }

    public async Task<PolicyCertificateDto?> GetByNumberAsync(Guid tenantId, string certificateNumber, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 c.CertificateId, c.TenantId,
       COALESCE(c.PolicyId, p.PolicyId) AS PolicyId, c.CertificateHolderId, c.CertificateRequestId,
       c.DocumentTemplateVersionId, c.GeneratedDocumentId,
       c.CertificateNumber, c.PolicyNumber, c.AccountName, c.HolderName, c.HolderAddress,
       c.CertificateType, c.IssuedDate, c.ExpirationDate, c.LineOfBusiness, c.IssuedBy,
       c.Status, c.AdditionalInsured, c.WaiverSubrogation, c.PrimaryNonContributory, c.Description, c.HolderSpecificWording,
       c.LastDeliveredDateUtc, c.RevokedDateUtc, c.RevokedByUserId, c.RevokeReason,
       c.CreatedDateUtc, c.CreatedByUserId, c.ModifiedDateUtc, c.ModifiedByUserId
FROM Policy.PolicyCertificate c
LEFT JOIN Submissions.BoundPolicy p ON p.TenantId = c.TenantId AND p.PolicyNumber = c.PolicyNumber AND p.IsDeleted = 0
WHERE c.TenantId = @TenantId AND c.CertificateNumber = @CertificateNumber AND c.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyCertificateDto>(new CommandDefinition(sql, new { TenantId = tenantId, CertificateNumber = certificateNumber }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @PolicyId UNIQUEIDENTIFIER = @RequestedPolicyId;
IF @PolicyId IS NULL
    SELECT TOP 1 @PolicyId = PolicyId FROM Submissions.BoundPolicy WHERE TenantId = @TenantId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0;

DECLARE @CertificateNumber NVARCHAR(40);
DECLARE @CertificateSequence INT;
SELECT @CertificateSequence = ISNULL(MAX(TRY_CONVERT(INT, RIGHT(CertificateNumber, 6))), 1000) + 1
FROM Policy.PolicyCertificate WITH (UPDLOCK, HOLDLOCK)
WHERE TenantId = @TenantId AND CertificateNumber LIKE CONCAT(N'CERT-', FORMAT(SYSUTCDATETIME(), 'yyyy'), N'-%');
SET @CertificateNumber = CONCAT(N'CERT-', FORMAT(SYSUTCDATETIME(), 'yyyy'), N'-', RIGHT(N'000000' + CAST(@CertificateSequence AS NVARCHAR(10)), 6));

INSERT INTO Policy.PolicyCertificate
    (CertificateId, TenantId, PolicyId, CertificateNumber, PolicyNumber, AccountName, HolderName, HolderAddress,
     CertificateType, IssuedDate, ExpirationDate, LineOfBusiness, IssuedBy, Status, AdditionalInsured,
     WaiverSubrogation, PrimaryNonContributory, Description, HolderSpecificWording, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@CertificateId, @TenantId, @PolicyId, @CertificateNumber, @PolicyNumber, @AccountName, @HolderName, @HolderAddress,
     @CertificateType, @IssuedDate, @ExpirationDate, @LineOfBusiness, @IssuedBy, @Status, @AdditionalInsured,
     @WaiverSubrogation, @PrimaryNonContributory, @Description, @HolderSpecificWording, SYSUTCDATETIME(), @CreatedByUserId, 0);

DECLARE @HolderId UNIQUEIDENTIFIER = @RequestedCertificateHolderId;
IF NOT EXISTS (SELECT 1 FROM Policy.CertificateHolder WHERE CertificateHolderId=@HolderId AND TenantId=@TenantId AND IsDeleted=0) SET @HolderId=NULL;
IF @HolderId IS NULL SET @HolderId=(SELECT TOP 1 CertificateHolderId FROM Policy.CertificateHolder WHERE TenantId=@TenantId AND LegalName=@HolderName AND ISNULL(AddressLine1,N'')=ISNULL(NULLIF(@HolderAddress,N''),N'') AND IsDeleted=0 ORDER BY CreatedDateUtc);
IF @HolderId IS NULL
BEGIN
    SET @HolderId=NEWID();
    INSERT INTO Policy.CertificateHolder (CertificateHolderId,TenantId,HolderCode,LegalName,AddressLine1,DefaultWording,RequiresAdditionalInsured,RequiresWaiverOfSubrogation,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES (@HolderId,@TenantId,CONCAT(N'H-',LEFT(REPLACE(CONVERT(NVARCHAR(36),@HolderId),N'-',N''),12)),@HolderName,NULLIF(@HolderAddress,N''),COALESCE(NULLIF(@HolderSpecificWording,N''),NULLIF(@Description,N'')),@AdditionalInsured,@WaiverSubrogation,1,SYSUTCDATETIME(),@CreatedByUserId,0);
END;
DECLARE @TemplateVersionId UNIQUEIDENTIFIER=(SELECT TOP 1 v.DocumentTemplateVersionId FROM DMS.DocumentTemplateDefinition d INNER JOIN DMS.DocumentTemplateVersion v ON v.DocumentTemplateDefinitionId=d.DocumentTemplateDefinitionId AND v.IsDeleted=0 WHERE d.TenantId=@TenantId AND d.IsDeleted=0 AND d.IsActive=1 AND (d.TemplateCode=REPLACE(UPPER(@CertificateType),N' ',N'') OR d.FormNumber=REPLACE(UPPER(@CertificateType),N'ACORD ',N'')) ORDER BY v.VersionNumber DESC);
UPDATE Policy.PolicyCertificate SET CertificateHolderId=@HolderId,DocumentTemplateVersionId=@TemplateVersionId,HolderSpecificWording=COALESCE(NULLIF(@HolderSpecificWording,N''),NULLIF(@Description,N''),(SELECT DefaultWording FROM Policy.CertificateHolder WHERE CertificateHolderId=@HolderId)) WHERE CertificateId=@CertificateId;
INSERT INTO Policy.CertificateRenewalSchedule (CertificateRenewalScheduleId,TenantId,CertificateId,CertificateHolderId,RenewalLeadDays,NextRunDateUtc,StatusCode,AutoGenerate,AutoDeliver,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (NEWID(),@TenantId,@CertificateId,@HolderId,30,DATEADD(day,-30,CONVERT(DATETIME2,@ExpirationDate)),N'Scheduled',0,0,SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,ActorName,CreatedDateUtc)
VALUES (NEWID(),@TenantId,@CertificateId,N'Created',N'Certificate created and synchronized with holder, template, and renewal records.',JSON_OBJECT(N'CertificateNumber':@CertificateNumber,N'HolderId':@HolderId,N'Status':@Status),@CreatedByUserId,@IssuedBy,SYSUTCDATETIME());
COMMIT;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CertificateId = id,
            request.TenantId,
            RequestedPolicyId = request.PolicyId,
            request.PolicyNumber,
            request.AccountName,
            request.HolderName,
            request.HolderAddress,
            request.CertificateType,
            request.IssuedDate,
            request.ExpirationDate,
            request.LineOfBusiness,
            request.IssuedBy,
            request.Status,
            request.AdditionalInsured,
            request.WaiverSubrogation,
            request.Description,
            RequestedCertificateHolderId = request.CertificateHolderId,
            request.PrimaryNonContributory,
            request.HolderSpecificWording,
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid certificateId, UpdatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @OldValue NVARCHAR(MAX)=(SELECT CertificateNumber,PolicyNumber,HolderName,CertificateType,Status,AdditionalInsured,WaiverSubrogation,Description FROM Policy.PolicyCertificate WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
DECLARE @PolicyId UNIQUEIDENTIFIER = @RequestedPolicyId;
IF @PolicyId IS NULL
    SELECT TOP 1 @PolicyId = PolicyId FROM Submissions.BoundPolicy WHERE TenantId = @TenantId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0;

UPDATE Policy.PolicyCertificate
SET PolicyId = @PolicyId,
    PolicyNumber = @PolicyNumber,
    AccountName = @AccountName,
    HolderName = @HolderName,
    HolderAddress = @HolderAddress,
    CertificateType = @CertificateType,
    IssuedDate = @IssuedDate,
    ExpirationDate = @ExpirationDate,
    LineOfBusiness = @LineOfBusiness,
    IssuedBy = @IssuedBy,
    Status = @Status,
    AdditionalInsured = @AdditionalInsured,
    WaiverSubrogation = @WaiverSubrogation,
    PrimaryNonContributory = @PrimaryNonContributory,
    Description = @Description,
    HolderSpecificWording = @HolderSpecificWording,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;
DECLARE @HolderId UNIQUEIDENTIFIER=@RequestedCertificateHolderId;
IF NOT EXISTS (SELECT 1 FROM Policy.CertificateHolder WHERE CertificateHolderId=@HolderId AND TenantId=@TenantId AND IsDeleted=0) SET @HolderId=NULL;
IF @HolderId IS NULL SET @HolderId=(SELECT TOP 1 CertificateHolderId FROM Policy.CertificateHolder WHERE TenantId=@TenantId AND LegalName=@HolderName AND ISNULL(AddressLine1,N'')=ISNULL(NULLIF(@HolderAddress,N''),N'') AND IsDeleted=0 ORDER BY CreatedDateUtc);
IF @HolderId IS NULL
BEGIN
 SET @HolderId=NEWID();
 INSERT INTO Policy.CertificateHolder (CertificateHolderId,TenantId,HolderCode,LegalName,AddressLine1,DefaultWording,RequiresAdditionalInsured,RequiresWaiverOfSubrogation,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
 VALUES (@HolderId,@TenantId,CONCAT(N'H-',LEFT(REPLACE(CONVERT(NVARCHAR(36),@HolderId),N'-',N''),12)),@HolderName,NULLIF(@HolderAddress,N''),COALESCE(NULLIF(@HolderSpecificWording,N''),NULLIF(@Description,N'')),@AdditionalInsured,@WaiverSubrogation,1,SYSUTCDATETIME(),@ModifiedByUserId,0);
END;
UPDATE Policy.PolicyCertificate SET CertificateHolderId=@HolderId,HolderSpecificWording=COALESCE(NULLIF(@Description,N''),(SELECT DefaultWording FROM Policy.CertificateHolder WHERE CertificateHolderId=@HolderId)) WHERE CertificateId=@CertificateId AND TenantId=@TenantId;
UPDATE Policy.CertificateRenewalSchedule SET CertificateHolderId=@HolderId,NextRunDateUtc=DATEADD(day,-RenewalLeadDays,CONVERT(DATETIME2,@ExpirationDate)),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0;
DECLARE @NewValue NVARCHAR(MAX)=(SELECT CertificateNumber,PolicyNumber,HolderName,CertificateType,Status,AdditionalInsured,WaiverSubrogation,Description FROM Policy.PolicyCertificate WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,OldValueJson,NewValueJson,ActorUserId,CreatedDateUtc) VALUES (NEWID(),@TenantId,@CertificateId,N'Updated',N'Certificate and synchronized workflow records updated.',@OldValue,@NewValue,@ModifiedByUserId,SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CertificateId = certificateId,
            request.TenantId,
            RequestedPolicyId = request.PolicyId,
            request.PolicyNumber,
            request.AccountName,
            request.HolderName,
            request.HolderAddress,
            request.CertificateType,
            request.IssuedDate,
            request.ExpirationDate,
            request.LineOfBusiness,
            request.IssuedBy,
            request.Status,
            request.AdditionalInsured,
            request.WaiverSubrogation,
            request.Description,
            RequestedCertificateHolderId = request.CertificateHolderId,
            request.PrimaryNonContributory,
            request.HolderSpecificWording,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAsync(Guid certificateId, RevokePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRANSACTION;
UPDATE Policy.PolicyCertificate
SET Status = N'Revoked',
    RevokedDateUtc = SYSUTCDATETIME(),
    RevokedByUserId = @RevokedByUserId,
    RevokeReason = @Reason,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RevokedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;
UPDATE Policy.CertificateRenewalSchedule SET StatusCode=N'Suspended',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@RevokedByUserId WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES (NEWID(),@TenantId,@CertificateId,N'Revoked',N'Certificate revoked and renewal processing suspended.',JSON_OBJECT(N'Reason':@Reason),@RevokedByUserId,SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, request.TenantId, request.RevokedByUserId, request.Reason }, cancellationToken: cancellationToken));
    }

    public async Task RestoreAsync(Guid certificateId, RestorePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRANSACTION;
UPDATE Policy.PolicyCertificate
SET Status = CASE WHEN ExpirationDate < CAST(SYSUTCDATETIME() AS date) THEN N'Expired' ELSE N'Issued' END,
    RevokedDateUtc = NULL,
    RevokedByUserId = NULL,
    RevokeReason = NULL,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;
UPDATE Policy.CertificateRenewalSchedule SET StatusCode=N'Scheduled',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES (NEWID(),@TenantId,@CertificateId,N'Restored',N'Certificate restored and renewal processing resumed.',@ModifiedByUserId,SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, request.TenantId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task MarkDeliveredAsync(Guid certificateId, PolicyCertificateActionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRANSACTION;
DECLARE @Recipient NVARCHAR(500)=(SELECT COALESCE(NULLIF(h.EmailAddress,N''),NULLIF(h.AddressLine1,N''),c.HolderName) FROM Policy.PolicyCertificate c LEFT JOIN Policy.CertificateHolder h ON h.CertificateHolderId=c.CertificateHolderId AND h.IsDeleted=0 WHERE c.CertificateId=@CertificateId AND c.TenantId=@TenantId AND c.IsDeleted=0);
IF @Recipient IS NULL THROW 51005, 'Certificate or delivery recipient was not found.', 1;
DECLARE @DeliveryId UNIQUEIDENTIFIER=NEWID();
INSERT INTO Policy.CertificateDelivery (CertificateDeliveryId,TenantId,CertificateId,DeliveryMethodCode,RecipientName,RecipientAddress,StatusCode,QueuedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT @DeliveryId,@TenantId,c.CertificateId,N'Email',c.HolderName,@Recipient,N'Queued',SYSUTCDATETIME(),SYSUTCDATETIME(),@ModifiedByUserId,0 FROM Policy.PolicyCertificate c WHERE c.CertificateId=@CertificateId AND c.TenantId=@TenantId AND c.IsDeleted=0;
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES (NEWID(),@TenantId,@CertificateId,N'DeliveryQueued',N'Certificate email delivery queued; delivery is not marked complete until provider confirmation.',JSON_OBJECT(N'DeliveryId':@DeliveryId,N'Recipient':@Recipient),@ModifiedByUserId,SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, request.TenantId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid certificateId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRANSACTION;
UPDATE Policy.PolicyCertificate
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;
UPDATE Policy.CertificateRenewalSchedule SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0;
UPDATE Policy.CertificateDelivery SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE CertificateId=@CertificateId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Policy.CertificateAuditEvent (CertificateAuditEventId,TenantId,CertificateId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES (NEWID(),@TenantId,@CertificateId,N'Deleted',N'Certificate soft-deleted with active schedules and delivery records.',@ModifiedByUserId,SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
