using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;
using Dapper;
using System.Globalization;

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
    InterestedService, AnnualRevenue, Score, PriorityCode, SourceCode, NurturingStageCode, AssignedToUserId,
    StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @LeadId, @TenantId, @LeadNumber, @AccountName, @FirstName, @LastName, @Email, @Phone,
    @InterestedService, @AnnualRevenue, @Score, @PriorityCode, @SourceCode, @NurturingStageCode, @AssignedToUserId,
    1, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var calculatedScore = await CalculateLeadScoreAsync(cn, new LeadScoringInput(
            request.TenantId,
            request.AccountName,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.InterestedService,
            request.AnnualRevenue,
            request.SourceCode,
            DateTime.UtcNow), request.Score, cancellationToken);

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
            request.AnnualRevenue,
            Score = calculatedScore,
            request.PriorityCode,
            request.SourceCode,
            request.NurturingStageCode,
            request.AssignedToUserId,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc FROM CRM.Lead WHERE LeadId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var lead = await cn.QuerySingleOrDefaultAsync<LeadDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        if (lead is not null)
        {
            lead.Score = await CalculateLeadScoreAsync(cn, ToScoringInput(lead), lead.Score, cancellationToken);
        }

        return lead;
    }

    public async Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "CRM.Lead",
            "LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc",
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
        foreach (var item in items)
        {
            item.Score = await CalculateLeadScoreAsync(cn, ToScoringInput(item), item.Score, cancellationToken);
        }

        return new PagedResult<LeadDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<LeadScoreFactorDto>> GetScoreFactorsAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT LeadId, TenantId, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, SourceCode, CreatedDateUtc FROM CRM.Lead WHERE LeadId = @LeadId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var lead = await cn.QuerySingleOrDefaultAsync<LeadDto>(new CommandDefinition(sql, new { LeadId = leadId }, cancellationToken: cancellationToken));
        if (lead is null)
        {
            return [];
        }

        var input = ToScoringInput(lead);
        var rules = await GetActiveScoringRulesAsync(cn, lead.TenantId, cancellationToken);
        return rules
            .Select(rule =>
            {
                var actualValue = GetFieldValue(input, rule.Field);
                return new LeadScoreFactorDto
                {
                    LeadScoringRuleId = rule.LeadScoringRuleId,
                    RuleName = rule.RuleName,
                    Field = rule.Field,
                    Operator = rule.Operator,
                    Value = rule.Value,
                    Points = rule.PointValue,
                    Matched = MatchesRule(input, rule),
                    ActualValue = actualValue
                };
            })
            .OrderByDescending(f => f.Matched)
            .ThenByDescending(f => f.Points)
            .ThenBy(f => f.RuleName)
            .ToList();
    }

    public async Task<LeadEngagementSummaryDto?> GetEngagementSummaryAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string leadSql = "SELECT LeadId, TenantId, CreatedDateUtc FROM CRM.Lead WHERE LeadId = @LeadId AND IsDeleted = 0;";
        var lead = await cn.QuerySingleOrDefaultAsync<LeadDto>(new CommandDefinition(leadSql, new { LeadId = leadId }, cancellationToken: cancellationToken));
        if (lead is null)
        {
            return null;
        }

        var metrics = await GetEngagementMetricsAsync(cn, leadId, lead.CreatedDateUtc, cancellationToken);
        var factors = await GetActiveEngagementFactorsAsync(cn, lead.TenantId, cancellationToken);
        var contributions = factors.Select(factor =>
        {
            var actual = GetEngagementMetricValue(metrics, factor.Metric);
            return new LeadEngagementFactorContributionDto
            {
                EngagementFactorId = factor.EngagementFactorId,
                FactorName = factor.FactorName,
                Metric = factor.Metric,
                Operator = factor.Operator,
                Value = factor.Value,
                Points = factor.Points,
                Matched = MatchesEngagementFactor(actual, factor),
                ActualValue = actual
            };
        }).ToList();

        var score = Math.Clamp(contributions.Where(f => f.Matched).Sum(f => f.Points), 0, 100);
        return new LeadEngagementSummaryDto
        {
            Score = score,
            Level = score >= 70 ? "High" : score >= 40 ? "Medium" : "Low",
            EmailsSent = metrics.EmailsSent,
            EmailsOpened = metrics.EmailsOpened,
            Clicks = metrics.Clicks,
            PortalVisits = metrics.PortalVisits,
            ActivityCount = metrics.ActivityCount,
            DaysSinceTouch = metrics.DaysSinceTouch,
            Factors = contributions.OrderByDescending(f => f.Matched).ThenByDescending(f => f.Points).ThenBy(f => f.FactorName).ToList()
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
    AnnualRevenue = @AnnualRevenue,
    Score = @Score,
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
        const string currentSql = @"
SELECT LeadId, TenantId, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, SourceCode, CreatedDateUtc
FROM CRM.Lead
WHERE LeadId = @LeadId AND IsDeleted = 0;";
        var current = await cn.QuerySingleOrDefaultAsync<LeadDto>(new CommandDefinition(currentSql, new { request.LeadId }, cancellationToken: cancellationToken));

        var merged = new LeadScoringInput(
            current?.TenantId ?? Guid.Empty,
            request.AccountName ?? current?.AccountName,
            request.FirstName ?? current?.FirstName,
            request.LastName ?? current?.LastName,
            request.Email,
            request.Phone,
            request.InterestedService,
            request.AnnualRevenue,
            request.SourceCode,
            current?.CreatedDateUtc ?? DateTime.UtcNow);
        var calculatedScore = current is null
            ? request.Score
            : await CalculateLeadScoreAsync(cn, merged, request.Score ?? current.Score, cancellationToken);

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.LeadId,
            request.AccountName,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.InterestedService,
            request.AnnualRevenue,
            Score = calculatedScore,
            request.PriorityCode,
            request.SourceCode,
            request.NurturingStageCode,
            request.QualifiedDate,
            request.StatusCode,
            request.AssignedToUserId,
            request.UpdatedByUserId
        }, cancellationToken: cancellationToken));
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
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasNewSchema = await HasNewLeadScoringSchemaAsync(cn, cancellationToken);
        var sql = hasNewSchema
            ? @"
SELECT 
    ScoringRuleId AS LeadScoringRuleId,
    TenantId,
    RuleName,
    CONCAT(Field, ' ', Operator, CASE WHEN NULLIF(Value, '') IS NULL THEN '' ELSE CONCAT(' ', Value) END) AS RuleDescription,
    Field,
    Operator,
    Value,
    Points AS PointValue,
    IsActive,
    SortOrder,
    CreatedDateUtc
FROM CRM.LeadScoringRule
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY SortOrder, Points DESC, RuleName;"
            : @"
SELECT
    LeadScoringRuleId,
    TenantId,
    RuleName,
    RuleDescription,
    CASE
        WHEN RuleName LIKE '%Company%' THEN 'CompanySize'
        WHEN RuleName LIKE '%Email%' THEN 'EmailOpened'
        WHEN RuleName LIKE '%Website%' OR RuleName LIKE '%Web%' THEN 'WebsiteVisits'
        WHEN RuleName LIKE '%Title%' THEN 'Title'
        WHEN RuleName LIKE '%Stale%' THEN 'StaleDays'
        ELSE RuleName
    END AS Field,
    CASE
        WHEN RuleDescription LIKE '%>%' OR RuleName LIKE '%Stale%' THEN 'GreaterThan'
        WHEN RuleDescription LIKE '%contains%' THEN 'Contains'
        ELSE 'Equals'
    END AS Operator,
    '' AS Value,
    PointValue,
    IsActive,
    0 AS SortOrder,
    CreatedDateUtc
FROM CRM.LeadScoringRule
WHERE TenantId = @TenantId AND IsActive = 1
ORDER BY PointValue DESC, RuleName;";

        var rules = await cn.QueryAsync<LeadScoringRuleDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rules.ToList();
    }

    public async Task<Guid> CreateScoringRuleAsync(CreateLeadScoringRuleRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasNewSchema = await HasNewLeadScoringSchemaAsync(cn, cancellationToken);
        var sql = hasNewSchema
            ? @"
INSERT INTO CRM.LeadScoringRule
(
    ScoringRuleId, TenantId, RuleName, Field, Operator, Value, Points, IsActive, SortOrder, CreatedDateUtc, IsDeleted
)
VALUES
(
    @ScoringRuleId, @TenantId, @RuleName, @Field, @Operator, @Value, @Points, @IsActive, @SortOrder, SYSUTCDATETIME(), 0
);"
            : @"
INSERT INTO CRM.LeadScoringRule
(
    LeadScoringRuleId, TenantId, RuleName, RuleDescription, PointValue, IsActive, CreatedDateUtc
)
VALUES
(
    @ScoringRuleId, @TenantId, @RuleName, @RuleDescription, @Points, @IsActive, SYSUTCDATETIME()
);";

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ScoringRuleId = id,
            request.TenantId,
            request.RuleName,
            request.Field,
            request.Operator,
            request.Value,
            RuleDescription = BuildScoringRuleDescription(request.Field, request.Operator, request.Value),
            request.Points,
            request.IsActive,
            request.SortOrder
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<Guid?> GetScoringRuleTenantIdAsync(Guid scoringRuleId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasNewSchema = await HasNewLeadScoringSchemaAsync(cn, cancellationToken);
        var sql = hasNewSchema
            ? "SELECT TenantId FROM CRM.LeadScoringRule WHERE ScoringRuleId = @ScoringRuleId;"
            : "SELECT TenantId FROM CRM.LeadScoringRule WHERE LeadScoringRuleId = @ScoringRuleId;";

        return await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { ScoringRuleId = scoringRuleId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateScoringRuleAsync(UpdateLeadScoringRuleRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasNewSchema = await HasNewLeadScoringSchemaAsync(cn, cancellationToken);
        var sql = hasNewSchema
            ? @"
UPDATE CRM.LeadScoringRule
SET RuleName = @RuleName,
    Field = @Field,
    Operator = @Operator,
    Value = @Value,
    Points = @Points,
    IsActive = @IsActive,
    SortOrder = @SortOrder
WHERE ScoringRuleId = @ScoringRuleId AND TenantId = @TenantId AND IsDeleted = 0;"
            : @"
UPDATE CRM.LeadScoringRule
SET RuleName = @RuleName,
    RuleDescription = @RuleDescription,
    PointValue = @Points,
    IsActive = @IsActive
WHERE LeadScoringRuleId = @ScoringRuleId AND TenantId = @TenantId;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.ScoringRuleId,
            request.TenantId,
            request.RuleName,
            request.Field,
            request.Operator,
            request.Value,
            RuleDescription = BuildScoringRuleDescription(request.Field, request.Operator, request.Value),
            request.Points,
            request.IsActive,
            request.SortOrder
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteScoringRuleAsync(Guid scoringRuleId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasNewSchema = await HasNewLeadScoringSchemaAsync(cn, cancellationToken);
        var sql = hasNewSchema
            ? @"
UPDATE CRM.LeadScoringRule
SET IsDeleted = 1
WHERE ScoringRuleId = @ScoringRuleId AND IsDeleted = 0;"
            : @"
UPDATE CRM.LeadScoringRule
SET IsActive = 0
WHERE LeadScoringRuleId = @ScoringRuleId;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new { ScoringRuleId = scoringRuleId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<LeadEngagementFactorDto>> GetEngagementFactorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await GetEngagementFactorsTableExistsAsync(cn, cancellationToken)
            ? (await cn.QueryAsync<LeadEngagementFactorDto>(new CommandDefinition(@"
SELECT EngagementFactorId, TenantId, FactorName, Metric, Operator, Value, Points, IsActive, SortOrder, CreatedDateUtc
FROM CRM.LeadEngagementFactor
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY SortOrder, Points DESC, FactorName;", new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList()
            : [];
    }

    public async Task<Guid> CreateEngagementFactorAsync(CreateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO CRM.LeadEngagementFactor (EngagementFactorId, TenantId, FactorName, Metric, Operator, Value, Points, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
VALUES (@EngagementFactorId, @TenantId, @FactorName, @Metric, @Operator, @Value, @Points, @IsActive, @SortOrder, SYSUTCDATETIME(), 0);", new
        {
            EngagementFactorId = id,
            request.TenantId,
            request.FactorName,
            request.Metric,
            request.Operator,
            request.Value,
            request.Points,
            request.IsActive,
            request.SortOrder
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateEngagementFactorAsync(UpdateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(@"
UPDATE CRM.LeadEngagementFactor
SET FactorName = @FactorName,
    Metric = @Metric,
    Operator = @Operator,
    Value = @Value,
    Points = @Points,
    IsActive = @IsActive,
    SortOrder = @SortOrder,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE EngagementFactorId = @EngagementFactorId AND TenantId = @TenantId AND IsDeleted = 0;", request, cancellationToken: cancellationToken));
    }

    public async Task DeleteEngagementFactorAsync(Guid engagementFactorId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.LeadEngagementFactor SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE EngagementFactorId = @EngagementFactorId AND IsDeleted = 0;", new { EngagementFactorId = engagementFactorId }, cancellationToken: cancellationToken));
    }

    private static async Task<bool> HasNewLeadScoringSchemaAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN COL_LENGTH(N'CRM.LeadScoringRule', N'ScoringRuleId') IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;";
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task<bool> GetEngagementFactorsTableExistsAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID(N'CRM.LeadEngagementFactor', N'U') IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;";
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<LeadEngagementFactorDto>> GetActiveEngagementFactorsAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await GetEngagementFactorsTableExistsAsync(connection, cancellationToken))
        {
            return [];
        }

        var factors = await connection.QueryAsync<LeadEngagementFactorDto>(new CommandDefinition(@"
SELECT EngagementFactorId, TenantId, FactorName, Metric, Operator, Value, Points, IsActive, SortOrder, CreatedDateUtc
FROM CRM.LeadEngagementFactor
WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0
ORDER BY SortOrder, Points DESC, FactorName;", new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return factors.AsList();
    }

    private static async Task<LeadEngagementMetrics> GetEngagementMetricsAsync(System.Data.IDbConnection connection, Guid leadId, DateTime createdDateUtc, CancellationToken cancellationToken)
    {
        var campaigns = await connection.QuerySingleAsync<LeadEngagementMetrics>(new CommandDefinition(@"
SELECT
    COALESCE(SUM(EmailsSent), 0) AS EmailsSent,
    COALESCE(SUM(EmailsOpen), 0) AS EmailsOpened,
    COALESCE(SUM(Clicks), 0) AS Clicks,
    COALESCE(MAX(LastTouch), NULL) AS LastTouch
FROM CRM.LeadCampaignEnrollment
WHERE LeadId = @LeadId AND IsDeleted = 0;", new { LeadId = leadId }, cancellationToken: cancellationToken));

        var communication = await connection.QuerySingleAsync<LeadEngagementMetrics>(new CommandDefinition(@"
SELECT
    COALESCE(COUNT(1), 0) AS EmailsSent,
    COALESCE(SUM(CASE WHEN Opened = 1 THEN 1 ELSE 0 END), 0) AS EmailsOpened,
    COALESCE(SUM(CASE WHEN Clicked = 1 THEN 1 ELSE 0 END), 0) AS Clicks,
    COALESCE(MAX(SentAt), NULL) AS LastTouch
FROM CRM.LeadCommunication
WHERE LeadId = @LeadId AND IsDeleted = 0;", new { LeadId = leadId }, cancellationToken: cancellationToken));

        var activity = await connection.QuerySingleAsync<LeadEngagementMetrics>(new CommandDefinition(@"
SELECT
    COALESCE(COUNT(1), 0) AS ActivityCount,
    COALESCE(MAX(ActivityDate), NULL) AS LastTouch
FROM CRM.LeadActivity
WHERE LeadId = @LeadId AND IsDeleted = 0;", new { LeadId = leadId }, cancellationToken: cancellationToken));

        var lastTouch = new[] { campaigns.LastTouch, communication.LastTouch, activity.LastTouch }.Where(d => d.HasValue).Max();
        return new LeadEngagementMetrics
        {
            EmailsSent = campaigns.EmailsSent + communication.EmailsSent,
            EmailsOpened = campaigns.EmailsOpened + communication.EmailsOpened,
            Clicks = campaigns.Clicks + communication.Clicks,
            PortalVisits = 0,
            ActivityCount = activity.ActivityCount,
            DaysSinceTouch = lastTouch.HasValue ? Math.Max(0, (int)(DateTime.UtcNow - lastTouch.Value).TotalDays) : Math.Max(0, (int)(DateTime.UtcNow - createdDateUtc).TotalDays),
            LastTouch = lastTouch
        };
    }

    private static decimal GetEngagementMetricValue(LeadEngagementMetrics metrics, string metric) => metric switch
    {
        "EmailsSent" => metrics.EmailsSent,
        "EmailsOpened" => metrics.EmailsOpened,
        "Clicks" => metrics.Clicks,
        "PortalVisits" => metrics.PortalVisits,
        "ActivityCount" => metrics.ActivityCount,
        "DaysSinceTouch" => metrics.DaysSinceTouch,
        _ => 0
    };

    private static bool MatchesEngagementFactor(decimal actual, LeadEngagementFactorDto factor)
    {
        _ = decimal.TryParse(factor.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var expected);
        return factor.Operator switch
        {
            "GreaterThan" => actual > expected,
            "GreaterThanOrEqual" => actual >= expected,
            "LessThan" => actual < expected,
            "LessThanOrEqual" => actual <= expected,
            "Equals" => actual == expected,
            "IsNotZero" => actual != 0,
            _ => false
        };
    }

    private static async Task<int?> CalculateLeadScoreAsync(System.Data.IDbConnection connection, LeadScoringInput lead, int? fallbackScore, CancellationToken cancellationToken)
    {
        if (lead.TenantId == Guid.Empty)
        {
            return fallbackScore;
        }

        var rules = (await GetActiveScoringRulesAsync(connection, lead.TenantId, cancellationToken)).ToList();
        if (rules.Count == 0)
        {
            return fallbackScore;
        }

        var score = rules.Where(rule => MatchesRule(lead, rule)).Sum(rule => rule.PointValue);
        return Math.Clamp(score, 0, 100);
    }

    private static async Task<IReadOnlyList<LeadScoringRuleDto>> GetActiveScoringRulesAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        var hasNewSchema = await HasNewLeadScoringSchemaAsync(connection, cancellationToken);
        var sql = hasNewSchema
            ? @"
SELECT ScoringRuleId AS LeadScoringRuleId, RuleName, Field, Operator, Value, Points AS PointValue, IsActive, SortOrder
FROM CRM.LeadScoringRule
WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0
ORDER BY SortOrder, Points DESC, RuleName;"
            : @"
SELECT
    LeadScoringRuleId,
    RuleName,
    CASE
        WHEN RuleName LIKE '%Company%' THEN 'CompanySize'
        WHEN RuleName LIKE '%Email%' THEN 'EmailOpened'
        WHEN RuleName LIKE '%Website%' OR RuleName LIKE '%Web%' THEN 'WebsiteVisits'
        WHEN RuleName LIKE '%Title%' THEN 'Title'
        WHEN RuleName LIKE '%Stale%' THEN 'StaleDays'
        WHEN RuleName LIKE '%Source%' THEN 'Source'
        WHEN RuleName LIKE '%Revenue%' OR RuleName LIKE '%Premium%' THEN 'AnnualRevenue'
        ELSE RuleName
    END AS Field,
    CASE
        WHEN RuleDescription LIKE '%>%' OR RuleName LIKE '%Stale%' THEN 'GreaterThan'
        WHEN RuleDescription LIKE '%contains%' THEN 'Contains'
        ELSE 'Equals'
    END AS Operator,
    '' AS Value,
    PointValue,
    IsActive,
    0 AS SortOrder
FROM CRM.LeadScoringRule
WHERE TenantId = @TenantId AND IsActive = 1
ORDER BY PointValue DESC, RuleName;";

        return (await connection.QueryAsync<LeadScoringRuleDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    private static bool MatchesRule(LeadScoringInput lead, LeadScoringRuleDto rule)
    {
        var fieldValue = GetFieldValue(lead, rule.Field);
        var expected = rule.Value ?? string.Empty;

        return rule.Operator switch
        {
            "IsNotEmpty" => !string.IsNullOrWhiteSpace(fieldValue),
            "Contains" => !string.IsNullOrWhiteSpace(fieldValue) && fieldValue.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "Equals" => string.Equals(fieldValue, expected, StringComparison.OrdinalIgnoreCase),
            "GreaterThan" => TryDecimal(fieldValue, out var actualGreater) && TryDecimal(expected, out var expectedGreater) && actualGreater > expectedGreater,
            "LessThan" => TryDecimal(fieldValue, out var actualLess) && TryDecimal(expected, out var expectedLess) && actualLess < expectedLess,
            "OlderThanDays" => TryDecimal(expected, out var days) && (DateTime.UtcNow - lead.CreatedDateUtc).TotalDays > (double)days,
            _ => false
        };
    }

    private static string GetFieldValue(LeadScoringInput lead, string field) => field switch
    {
        "CompanySize" => lead.AnnualRevenue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        "AnnualRevenue" => lead.AnnualRevenue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        "EmailOpened" => lead.Email,
        "WebsiteVisits" => string.Empty,
        "Title" => lead.InterestedService,
        "StaleDays" => lead.CreatedDateUtc.ToString("O", CultureInfo.InvariantCulture),
        "Source" => lead.SourceCode,
        "Email" => lead.Email,
        "Phone" => lead.Phone,
        "LineOfBusiness" => lead.InterestedService,
        "EstPremium" => lead.AnnualRevenue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        _ => string.Empty
    } ?? string.Empty;

    private static bool TryDecimal(string? value, out decimal result) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static LeadScoringInput ToScoringInput(LeadDto lead) => new(
        lead.TenantId,
        lead.AccountName,
        lead.FirstName,
        lead.LastName,
        lead.Email,
        lead.Phone,
        lead.InterestedService,
        lead.AnnualRevenue,
        lead.SourceCode,
        lead.CreatedDateUtc);

    private static string BuildScoringRuleDescription(string field, string @operator, string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"{field} {@operator}"
            : $"{field} {@operator} {value}";
    }

    private sealed record LeadScoringInput(
        Guid TenantId,
        string? AccountName,
        string? FirstName,
        string? LastName,
        string? Email,
        string? Phone,
        string? InterestedService,
        decimal? AnnualRevenue,
        string? SourceCode,
        DateTime CreatedDateUtc);

    private sealed class LeadEngagementMetrics
    {
        public int EmailsSent { get; set; }
        public int EmailsOpened { get; set; }
        public int Clicks { get; set; }
        public int PortalVisits { get; set; }
        public int ActivityCount { get; set; }
        public int DaysSinceTouch { get; set; }
        public DateTime? LastTouch { get; set; }
    }
}
