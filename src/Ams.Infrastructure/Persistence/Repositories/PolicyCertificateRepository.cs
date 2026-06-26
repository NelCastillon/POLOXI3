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
           COALESCE(c.PolicyId, p.PolicyId) AS PolicyId,
           c.CertificateNumber, c.PolicyNumber, c.AccountName, c.HolderName, c.HolderAddress,
           c.CertificateType, c.IssuedDate, c.ExpirationDate, c.LineOfBusiness, c.IssuedBy,
           c.Status, c.AdditionalInsured, c.WaiverSubrogation, c.Description,
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

    public async Task<PolicyCertificateDto?> GetByIdAsync(Guid certificateId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.CertificateId, c.TenantId,
       COALESCE(c.PolicyId, p.PolicyId) AS PolicyId,
       c.CertificateNumber, c.PolicyNumber, c.AccountName, c.HolderName, c.HolderAddress,
       c.CertificateType, c.IssuedDate, c.ExpirationDate, c.LineOfBusiness, c.IssuedBy,
       c.Status, c.AdditionalInsured, c.WaiverSubrogation, c.Description,
       c.LastDeliveredDateUtc, c.RevokedDateUtc, c.RevokedByUserId, c.RevokeReason,
       c.CreatedDateUtc, c.CreatedByUserId, c.ModifiedDateUtc, c.ModifiedByUserId
FROM Policy.PolicyCertificate c
LEFT JOIN Submissions.BoundPolicy p ON p.TenantId = c.TenantId AND p.PolicyNumber = c.PolicyNumber AND p.IsDeleted = 0
WHERE c.CertificateId = @CertificateId AND c.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyCertificateDto>(new CommandDefinition(sql, new { CertificateId = certificateId }, cancellationToken: cancellationToken));
    }

    public async Task<PolicyCertificateDto?> GetByNumberAsync(Guid tenantId, string certificateNumber, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 c.CertificateId, c.TenantId,
       COALESCE(c.PolicyId, p.PolicyId) AS PolicyId,
       c.CertificateNumber, c.PolicyNumber, c.AccountName, c.HolderName, c.HolderAddress,
       c.CertificateType, c.IssuedDate, c.ExpirationDate, c.LineOfBusiness, c.IssuedBy,
       c.Status, c.AdditionalInsured, c.WaiverSubrogation, c.Description,
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
DECLARE @PolicyId UNIQUEIDENTIFIER = @RequestedPolicyId;
IF @PolicyId IS NULL
    SELECT TOP 1 @PolicyId = PolicyId FROM Submissions.BoundPolicy WHERE TenantId = @TenantId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0;

DECLARE @CertificateNumber NVARCHAR(40) = CONCAT(N'CERT-', FORMAT(SYSUTCDATETIME(), 'yyyy'), N'-', RIGHT(N'0000' + CAST((SELECT COUNT(1) + 1001 FROM Policy.PolicyCertificate WHERE TenantId = @TenantId) AS NVARCHAR(10)), 4));

INSERT INTO Policy.PolicyCertificate
    (CertificateId, TenantId, PolicyId, CertificateNumber, PolicyNumber, AccountName, HolderName, HolderAddress,
     CertificateType, IssuedDate, ExpirationDate, LineOfBusiness, IssuedBy, Status, AdditionalInsured,
     WaiverSubrogation, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@CertificateId, @TenantId, @PolicyId, @CertificateNumber, @PolicyNumber, @AccountName, @HolderName, @HolderAddress,
     @CertificateType, @IssuedDate, @ExpirationDate, @LineOfBusiness, @IssuedBy, @Status, @AdditionalInsured,
     @WaiverSubrogation, @Description, SYSUTCDATETIME(), @CreatedByUserId, 0);";
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
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid certificateId, UpdatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
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
    Description = @Description,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;";
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
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAsync(Guid certificateId, RevokePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCertificate
SET Status = N'Revoked',
    RevokedDateUtc = SYSUTCDATETIME(),
    RevokedByUserId = @RevokedByUserId,
    RevokeReason = @Reason,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RevokedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, request.TenantId, request.RevokedByUserId, request.Reason }, cancellationToken: cancellationToken));
    }

    public async Task RestoreAsync(Guid certificateId, RestorePolicyCertificateRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCertificate
SET Status = CASE WHEN ExpirationDate < CAST(SYSUTCDATETIME() AS date) THEN N'Expired' ELSE N'Issued' END,
    RevokedDateUtc = NULL,
    RevokedByUserId = NULL,
    RevokeReason = NULL,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, request.TenantId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task MarkDeliveredAsync(Guid certificateId, PolicyCertificateActionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCertificate
SET LastDeliveredDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, request.TenantId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid certificateId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCertificate
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CertificateId = @CertificateId AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CertificateId = certificateId, TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
