using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class LeadRepository : ILeadRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LeadRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO CRM.Lead
(
    LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone,
    InterestedService, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @LeadId, @TenantId, @LeadNumber, @AccountName, @FirstName, @LastName, @Email, @Phone,
    @InterestedService, 1, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            LeadId = id,
            request.TenantId,
            request.LeadNumber,
            request.AccountName,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.InterestedService,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc FROM CRM.Lead WHERE LeadId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<LeadDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "CRM.Lead",
            "LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc",
            "FirstName LIKE '%' + @SearchTerm + '%' OR LastName LIKE '%' + @SearchTerm + '%' OR Email LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%' OR LeadNumber LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<LeadDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<LeadDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(UpdateLeadRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.Lead
SET AccountName = COALESCE(@AccountName, AccountName),
    FirstName = COALESCE(@FirstName, FirstName),
    LastName = COALESCE(@LastName, LastName),
    Email = @Email,
    Phone = @Phone,
    InterestedService = @InterestedService,
    Score = COALESCE(@Score, Score),
    PriorityCode = @PriorityCode,
    SourceCode = @SourceCode,
    NurturingStageCode = @NurturingStageCode,
    QualifiedDate = @QualifiedDate,
    StatusCodeId = COALESCE(@StatusCode, StatusCodeId),
    AssignedToUserId = @AssignedToUserId,
    ModifiedByUserId = @UpdatedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE LeadId = @LeadId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task<IReadOnlyList<LeadContactDto>> GetContactsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadContactDto>("SELECT ContactId, TenantId, LeadId, FirstName, LastName, Title, Email, Phone, IsPrimary, CreatedDateUtc, ModifiedDateUtc FROM CRM.LeadContact WHERE LeadId = @LeadId AND IsDeleted = 0 ORDER BY IsPrimary DESC, CreatedDateUtc DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateContactAsync(CreateLeadContactRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO CRM.LeadContact (ContactId,TenantId,LeadId,FirstName,LastName,Title,Email,Phone,IsPrimary,CreatedByUserId,CreatedDateUtc,IsDeleted) VALUES (@ContactId,@TenantId,@LeadId,@FirstName,@LastName,@Title,@Email,@Phone,@IsPrimary,@CreatedByUserId,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ContactId = id, request.TenantId, request.LeadId, request.FirstName, request.LastName, request.Title, request.Email, request.Phone, request.IsPrimary, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateContactAsync(UpdateLeadContactRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE CRM.LeadContact SET FirstName=@FirstName,LastName=@LastName,Title=@Title,Email=@Email,Phone=@Phone,IsPrimary=@IsPrimary,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE ContactId=@ContactId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task DeleteContactAsync(Guid contactId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => SoftDeleteAsync("CRM.LeadContact", "ContactId", contactId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadInterestLineDto>> GetInterestLinesAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadInterestLineDto>("SELECT InterestLineId, TenantId, LeadId, LineOfBusiness, Carrier, CurrentCarrier, EstPremium, ExpiryDate, Priority, Notes, CreatedDateUtc, ModifiedDateUtc FROM CRM.LeadInterestLine WHERE LeadId = @LeadId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateInterestLineAsync(CreateLeadInterestLineRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO CRM.LeadInterestLine (InterestLineId,TenantId,LeadId,LineOfBusiness,Carrier,CurrentCarrier,EstPremium,ExpiryDate,Priority,Notes,CreatedByUserId,CreatedDateUtc,IsDeleted) VALUES (@InterestLineId,@TenantId,@LeadId,@LineOfBusiness,@Carrier,@CurrentCarrier,@EstPremium,@ExpiryDate,@Priority,@Notes,@CreatedByUserId,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { InterestLineId = id, request.TenantId, request.LeadId, request.LineOfBusiness, request.Carrier, request.CurrentCarrier, request.EstPremium, request.ExpiryDate, request.Priority, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateInterestLineAsync(UpdateLeadInterestLineRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE CRM.LeadInterestLine SET LineOfBusiness=@LineOfBusiness,Carrier=@Carrier,CurrentCarrier=@CurrentCarrier,EstPremium=@EstPremium,ExpiryDate=@ExpiryDate,Priority=@Priority,Notes=@Notes,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE InterestLineId=@InterestLineId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task DeleteInterestLineAsync(Guid interestLineId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => SoftDeleteAsync("CRM.LeadInterestLine", "InterestLineId", interestLineId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadCommunicationDto>> GetCommunicationsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadCommunicationDto>("SELECT c.CommunicationId, c.TenantId, c.LeadId, c.Channel, c.Subject, c.Preview, c.SentByUserId, COALESCE(u.DisplayName, u.FullName) AS SentByName, c.SentAt, c.Opened, c.Clicked, c.CreatedDateUtc, c.ModifiedDateUtc FROM CRM.LeadCommunication c LEFT JOIN IAM.[User] u ON u.UserId = c.SentByUserId WHERE c.LeadId = @LeadId AND c.IsDeleted = 0 ORDER BY c.SentAt DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateCommunicationAsync(CreateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO CRM.LeadCommunication (CommunicationId,TenantId,LeadId,Channel,Subject,Preview,SentByUserId,SentAt,Opened,Clicked,CreatedDateUtc,IsDeleted) VALUES (@CommunicationId,@TenantId,@LeadId,@Channel,@Subject,@Preview,@SentByUserId,@SentAt,@Opened,@Clicked,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CommunicationId = id, request.TenantId, request.LeadId, request.Channel, request.Subject, request.Preview, request.SentByUserId, request.SentAt, request.Opened, request.Clicked }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateCommunicationAsync(UpdateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE CRM.LeadCommunication SET Channel=@Channel,Subject=@Subject,Preview=@Preview,SentByUserId=@SentByUserId,SentAt=@SentAt,Opened=@Opened,Clicked=@Clicked,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE CommunicationId=@CommunicationId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task DeleteCommunicationAsync(Guid communicationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => SoftDeleteAsync("CRM.LeadCommunication", "CommunicationId", communicationId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadCampaignEnrollmentDto>> GetCampaignEnrollmentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadCampaignEnrollmentDto>("SELECT EnrollmentId, TenantId, LeadId, CampaignName, Status, EnrolledAt, EmailsSent, EmailsOpen, Clicks, LastTouch, CreatedDateUtc, ModifiedDateUtc FROM CRM.LeadCampaignEnrollment WHERE LeadId = @LeadId AND IsDeleted = 0 ORDER BY EnrolledAt DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateCampaignEnrollmentAsync(CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO CRM.LeadCampaignEnrollment (EnrollmentId,TenantId,LeadId,CampaignName,Status,EnrolledAt,EmailsSent,EmailsOpen,Clicks,LastTouch,CreatedByUserId,CreatedDateUtc,IsDeleted) VALUES (@EnrollmentId,@TenantId,@LeadId,@CampaignName,@Status,@EnrolledAt,@EmailsSent,@EmailsOpen,@Clicks,@LastTouch,@CreatedByUserId,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EnrollmentId = id, request.TenantId, request.LeadId, request.CampaignName, request.Status, request.EnrolledAt, request.EmailsSent, request.EmailsOpen, request.Clicks, request.LastTouch, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateCampaignEnrollmentAsync(UpdateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE CRM.LeadCampaignEnrollment SET CampaignName=@CampaignName,Status=@Status,EnrolledAt=@EnrolledAt,EmailsSent=@EmailsSent,EmailsOpen=@EmailsOpen,Clicks=@Clicks,LastTouch=@LastTouch,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE EnrollmentId=@EnrollmentId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task DeleteCampaignEnrollmentAsync(Guid enrollmentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => SoftDeleteAsync("CRM.LeadCampaignEnrollment", "EnrollmentId", enrollmentId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadDocumentDto>> GetDocumentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadDocumentDto>("SELECT d.DocumentId, d.TenantId, d.LeadId, d.FileName, d.Extension, d.Category, d.SizeKb, d.UploadedByUserId, COALESCE(u.DisplayName, u.FullName) AS UploadedByName, d.UploadedAt, d.CreatedDateUtc, d.ModifiedDateUtc FROM CRM.LeadDocument d LEFT JOIN IAM.[User] u ON u.UserId = d.UploadedByUserId WHERE d.LeadId = @LeadId AND d.IsDeleted = 0 ORDER BY d.UploadedAt DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateDocumentAsync(CreateLeadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO CRM.LeadDocument (DocumentId,TenantId,LeadId,FileName,Extension,Category,SizeKb,UploadedByUserId,UploadedAt,CreatedDateUtc,IsDeleted) VALUES (@DocumentId,@TenantId,@LeadId,@FileName,@Extension,@Category,@SizeKb,@UploadedByUserId,@UploadedAt,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DocumentId = id, request.TenantId, request.LeadId, request.FileName, request.Extension, request.Category, request.SizeKb, request.UploadedByUserId, request.UploadedAt }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateDocumentAsync(UpdateLeadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE CRM.LeadDocument SET FileName=@FileName,Extension=@Extension,Category=@Category,SizeKb=@SizeKb,UploadedByUserId=@UploadedByUserId,UploadedAt=@UploadedAt,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE DocumentId=@DocumentId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task DeleteDocumentAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => SoftDeleteAsync("CRM.LeadDocument", "DocumentId", documentId, modifiedByUserId, cancellationToken);

    private async Task<IReadOnlyList<T>> QueryListAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return items.AsList();
    }

    private async Task SoftDeleteAsync(string tableName, string keyName, Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        var sql = $"UPDATE {tableName} SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE {keyName} = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LeadScoringRuleDto>> GetScoringRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    LeadScoringRuleId,
    TenantId,
    RuleName,
    RuleDescription,
    PointValue,
    IsActive,
    CreatedDateUtc
FROM CRM.LeadScoringRule
WHERE TenantId = @TenantId AND IsActive = 1
ORDER BY PointValue DESC, RuleName";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rules = await cn.QueryAsync<LeadScoringRuleDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rules.ToList();
    }
}
