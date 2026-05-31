using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SubmissionIntake;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SubmissionIntakeRepository : ISubmissionIntakeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SubmissionIntakeRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        i.IntakeId, i.TenantId, i.IntakeNumber, i.Source, i.ReceivedDate,
        i.ApplicantName, i.BusinessName, i.Fein, i.Email, i.Phone,
        i.AddressLine, i.City, i.[State], i.PostalCode, i.ExistingPolicyNumber, i.ProducerCode,
        i.LineOfBusiness, i.RequestedEffectiveDate, i.EstimatedPremium, i.Attachments, i.Notes,
        i.IntakeStatus, i.MatchScore, i.MatchedAccountId, i.AccountId, i.OpportunityId, i.SubmissionId,
        i.AssignedToUserId, COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
        i.ProcessedDateUtc, i.CreatedDateUtc, i.ModifiedDateUtc";

    public async Task<PagedResult<SubmissionIntakeDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? source, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS
(
    SELECT {SelectColumns}
    FROM   Submissions.SubmissionIntake i
    LEFT JOIN IAM.[User] u ON u.UserId = i.AssignedToUserId
    WHERE  i.TenantId = @TenantId
      AND  i.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR i.BusinessName LIKE '%' + @SearchTerm + '%' OR i.ApplicantName LIKE '%' + @SearchTerm + '%' OR i.IntakeNumber LIKE '%' + @SearchTerm + '%' OR i.Email LIKE '%' + @SearchTerm + '%' OR i.LineOfBusiness LIKE '%' + @SearchTerm + '%')
      AND  (@Status IS NULL OR @Status = '' OR i.IntakeStatus = @Status)
      AND  (@Source IS NULL OR @Source = '' OR i.Source = @Source)
)
SELECT * FROM Cte
ORDER BY ReceivedDate DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM   Submissions.SubmissionIntake i
WHERE  i.TenantId = @TenantId
  AND  i.IsDeleted = 0
  AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR i.BusinessName LIKE '%' + @SearchTerm + '%' OR i.ApplicantName LIKE '%' + @SearchTerm + '%' OR i.IntakeNumber LIKE '%' + @SearchTerm + '%' OR i.Email LIKE '%' + @SearchTerm + '%' OR i.LineOfBusiness LIKE '%' + @SearchTerm + '%')
  AND  (@Status IS NULL OR @Status = '' OR i.IntakeStatus = @Status)
  AND  (@Source IS NULL OR @Source = '' OR i.Source = @Source);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId   = tenantId,
            SearchTerm = searchTerm,
            Status     = status,
            Source     = source,
            Offset     = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize   = pageSize,
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SubmissionIntakeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SubmissionIntakeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<SubmissionIntakeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM   Submissions.SubmissionIntake i
LEFT JOIN IAM.[User] u ON u.UserId = i.AssignedToUserId
WHERE  i.IntakeId = @Id AND i.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SubmissionIntakeDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @IntakeNumber NVARCHAR(50) = N'INT-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('0000' + CAST(NEXT VALUE FOR Submissions.IntakeSeq AS VARCHAR), 4);
INSERT INTO Submissions.SubmissionIntake
    (IntakeId, TenantId, IntakeNumber, Source, ReceivedDate, ApplicantName, BusinessName, Fein, Email, Phone,
     AddressLine, City, [State], PostalCode, ExistingPolicyNumber, ProducerCode, LineOfBusiness,
     RequestedEffectiveDate, EstimatedPremium, Attachments, RawPayload, Notes, IntakeStatus,
     AssignedToUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@IntakeId, @TenantId, @IntakeNumber, @Source, SYSUTCDATETIME(), @ApplicantName, @BusinessName, @Fein, @Email, @Phone,
     @AddressLine, @City, @State, @PostalCode, @ExistingPolicyNumber, @ProducerCode, @LineOfBusiness,
     @RequestedEffectiveDate, @EstimatedPremium, @Attachments, @RawPayload, @Notes, 'Pending',
     @AssignedToUserId, SYSUTCDATETIME(), @CreatedByUserId, 0);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            IntakeId = id,
            request.TenantId,
            request.Source,
            request.ApplicantName,
            request.BusinessName,
            request.Fein,
            request.Email,
            request.Phone,
            request.AddressLine,
            request.City,
            request.State,
            request.PostalCode,
            request.ExistingPolicyNumber,
            request.ProducerCode,
            request.LineOfBusiness,
            request.RequestedEffectiveDate,
            request.EstimatedPremium,
            request.Attachments,
            request.RawPayload,
            request.Notes,
            request.AssignedToUserId,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateSubmissionIntakeRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionIntake
SET    Source = @Source,
       ApplicantName = @ApplicantName,
       BusinessName = @BusinessName,
       Fein = @Fein,
       Email = @Email,
       Phone = @Phone,
       AddressLine = @AddressLine,
       City = @City,
       [State] = @State,
       PostalCode = @PostalCode,
       ExistingPolicyNumber = @ExistingPolicyNumber,
       ProducerCode = @ProducerCode,
       LineOfBusiness = @LineOfBusiness,
       RequestedEffectiveDate = @RequestedEffectiveDate,
       EstimatedPremium = @EstimatedPremium,
       Attachments = @Attachments,
       Notes = @Notes,
       AssignedToUserId = @AssignedToUserId,
       MatchScore = 0,
       MatchedAccountId = NULL,
       ModifiedDateUtc = SYSUTCDATETIME(),
       ModifiedByUserId = @ModifiedByUserId
WHERE  IntakeId = @Id
  AND  TenantId = @TenantId
  AND  IsDeleted = 0
  AND  IntakeStatus <> 'Processed';";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.TenantId,
            request.Source,
            request.ApplicantName,
            request.BusinessName,
            request.Fein,
            request.Email,
            request.Phone,
            request.AddressLine,
            request.City,
            request.State,
            request.PostalCode,
            request.ExistingPolicyNumber,
            request.ProducerCode,
            request.LineOfBusiness,
            request.RequestedEffectiveDate,
            request.EstimatedPremium,
            request.Attachments,
            request.Notes,
            request.AssignedToUserId,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid id, UpdateSubmissionIntakeStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionIntake
SET    IntakeStatus = @IntakeStatus,
       Notes = COALESCE(@Notes, Notes),
       ModifiedDateUtc = SYSUTCDATETIME(),
       ModifiedByUserId = @ModifiedByUserId
WHERE  IntakeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.IntakeStatus,
            request.Notes,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task MarkPromotedAsync(Guid id, int matchScore, Guid matchedAccountId, Guid accountId, Guid opportunityId, Guid submissionId, Guid? processedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionIntake
SET    IntakeStatus = 'Processed',
       MatchScore = @MatchScore,
       MatchedAccountId = @MatchedAccountId,
       AccountId = @AccountId,
       OpportunityId = @OpportunityId,
       SubmissionId = @SubmissionId,
       ProcessedDateUtc = SYSUTCDATETIME(),
       ModifiedDateUtc = SYSUTCDATETIME(),
       ModifiedByUserId = @ProcessedByUserId
WHERE  IntakeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            MatchScore = matchScore,
            MatchedAccountId = matchedAccountId == Guid.Empty ? (Guid?)null : matchedAccountId,
            AccountId = accountId,
            OpportunityId = opportunityId,
            SubmissionId = submissionId,
            ProcessedByUserId = processedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionIntake
SET    IsDeleted = 1,
       IntakeStatus = 'Archived',
       ModifiedDateUtc = SYSUTCDATETIME(),
       ModifiedByUserId = @UserId
WHERE  IntakeId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }
}
