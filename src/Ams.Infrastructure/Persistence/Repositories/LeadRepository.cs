using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;
using Dapper;
using System.Data;
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
        const string sql = @"SELECT LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AccountId, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc FROM CRM.Lead WHERE LeadId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureLeadAccountIdColumnAsync(cn, cancellationToken);
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
            "LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AccountId, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc",
            "FirstName LIKE '%' + @SearchTerm + '%' OR LastName LIKE '%' + @SearchTerm + '%' OR Email LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%' OR LeadNumber LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            true);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureLeadAccountIdColumnAsync(cn, cancellationToken);
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

    public Task<IReadOnlyList<LeadEngagementOptionDto>> GetEngagementOptionsAsync(Guid tenantId, string? optionType = null, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadEngagementOptionDto>(@"
SELECT OptionId, TenantId, OptionType, Code, Label, Description, SortOrder, IsActive
FROM CRM.LeadEngagementOption
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1 AND (@OptionType IS NULL OR OptionType = @OptionType)
ORDER BY OptionType, SortOrder, Label;", new { TenantId = tenantId, OptionType = EmptyToNull(optionType) }, cancellationToken);

    public Task<IReadOnlyList<LeadCampaignOptionDto>> GetCampaignOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadCampaignOptionDto>(@"
SELECT CampaignId, TenantId, Name, Type, Status, Segment, StartDate, Reached, OpenRate, Conversions, Revenue
FROM Comms.Campaign
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY CASE WHEN Status = N'Active' THEN 0 WHEN Status = N'Scheduled' THEN 1 ELSE 2 END, StartDate DESC, Name;", new { TenantId = tenantId }, cancellationToken);

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
    AccountId = COALESCE(@AccountId, AccountId),
    AssignedToUserId = @AssignedToUserId,
    ModifiedByUserId = @UpdatedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE LeadId = @LeadId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureLeadAccountIdColumnAsync(cn, cancellationToken);
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
            request.AccountId,
            request.AssignedToUserId,
            request.UpdatedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<LeadConversionResultDto> ConvertAsync(ConvertLeadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant is required to convert a lead.");
        }

        if (request.LeadId == Guid.Empty)
        {
            throw new InvalidOperationException("Lead is required for conversion.");
        }

        if (request.ConvertedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Converted by user is required.");
        }

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureLeadAccountIdColumnAsync(cn, cancellationToken);
        using var tx = cn.BeginTransaction();

        try
        {
            var lead = await cn.QuerySingleOrDefaultAsync<LeadDto>(new CommandDefinition(@"
SELECT LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, AnnualRevenue, Score, PriorityCode, SourceCode, NurturingStageCode, QualifiedDate, StatusCodeId AS StatusCode, AccountId, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc
FROM CRM.Lead WITH (UPDLOCK, HOLDLOCK)
WHERE LeadId = @LeadId AND TenantId = @TenantId AND IsDeleted = 0;", new { request.LeadId, request.TenantId }, transaction: tx, cancellationToken: cancellationToken));

            if (lead is null)
            {
                throw new InvalidOperationException("Lead was not found for this tenant.");
            }

            var existingConversion = await cn.QuerySingleOrDefaultAsync<LeadConversionResultDto>(new CommandDefinition(@"
SELECT TOP 1 lc.LeadConversionId, lc.LeadId, lc.AccountId, lc.OpportunityId, lc.ContactId, lc.AccountActionCode,
       a.AccountName, o.OpportunityName, o.OpportunityNumber, lc.LineOfBusiness, lc.EstimatedAmount,
       CAST(CASE WHEN lc.SubmissionNextStepCode IN (N'StartSubmission', N'DraftCreated') OR lc.SubmissionId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS SubmissionDraftRequested,
       lc.SubmissionId, lc.SubmissionNumber,
       CAST(CASE WHEN lc.SubmissionId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS SubmissionDraftCreated
FROM CRM.LeadConversion lc
LEFT JOIN Client.Account a ON a.AccountId = lc.AccountId
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = lc.OpportunityId
WHERE lc.LeadId = @LeadId AND lc.IsDeleted = 0
ORDER BY lc.ConvertedDateUtc DESC;", new { request.LeadId }, transaction: tx, cancellationToken: cancellationToken));

            if (existingConversion is not null)
            {
                if (request.CreateSubmissionDraft && !existingConversion.SubmissionId.HasValue)
                {
                    var draft = await CreateSubmissionDraftAsync(cn, tx, request.TenantId, existingConversion.AccountId, existingConversion.OpportunityId, existingConversion.LineOfBusiness, existingConversion.EstimatedAmount, lead.PriorityCode, lead.AssignedToUserId ?? request.ConvertedByUserId, request.ConvertedByUserId, request.CloseDate, cancellationToken);
                    await cn.ExecuteAsync(new CommandDefinition(@"
UPDATE CRM.LeadConversion
SET SubmissionId = @SubmissionId,
    SubmissionNumber = @SubmissionNumber,
    SubmissionNextStepCode = N'DraftCreated',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE LeadConversionId = @LeadConversionId AND IsDeleted = 0;", new
                    {
                        existingConversion.LeadConversionId,
                        draft.SubmissionId,
                        draft.SubmissionNumber,
                        ModifiedByUserId = request.ConvertedByUserId
                    }, transaction: tx, cancellationToken: cancellationToken));

                    existingConversion.SubmissionId = draft.SubmissionId;
                    existingConversion.SubmissionNumber = draft.SubmissionNumber;
                    existingConversion.SubmissionDraftRequested = true;
                    existingConversion.SubmissionDraftCreated = true;
                }

                tx.Commit();
                return existingConversion;
            }

            var ownerUserId = request.OwnerUserId ?? lead.AssignedToUserId ?? request.ConvertedByUserId;
            var accountName = Clean(request.AccountName) ?? Clean(lead.AccountName) ?? Clean($"{lead.FirstName} {lead.LastName}") ?? $"Converted Lead {lead.LeadNumber}";
            var lineOfBusiness = Clean(request.LineOfBusiness) ?? await GetPrimaryLeadLineOfBusinessAsync(cn, tx, lead.LeadId, cancellationToken) ?? Clean(lead.InterestedService);
            var estimatedAmount = request.EstimatedAmount ?? await GetPrimaryLeadEstimatedPremiumAsync(cn, tx, lead.LeadId, cancellationToken) ?? lead.AnnualRevenue ?? 0m;
            var opportunityName = Clean(request.OpportunityName) ?? $"{accountName} - {Clean(lineOfBusiness) ?? "Opportunity"}";
            var accountActionCode = request.ExistingAccountId.HasValue ? "Linked" : "Created";
            var accountId = request.ExistingAccountId ?? lead.AccountId;

            if (accountId.HasValue)
            {
                var linkedAccountName = await cn.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(@"
SELECT AccountName
FROM Client.Account
WHERE AccountId = @AccountId AND TenantId = @TenantId AND IsDeleted = 0;", new { AccountId = accountId.Value, request.TenantId }, transaction: tx, cancellationToken: cancellationToken));

                if (linkedAccountName is null)
                {
                    throw new InvalidOperationException("Selected account was not found for this tenant.");
                }

                accountName = linkedAccountName;
                accountActionCode = "Linked";
            }
            else
            {
                accountId = await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 AccountId
FROM Client.Account
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (
       AccountName = @AccountName
       OR (@Email IS NOT NULL AND MainEmail = @Email)
       OR (@Phone IS NOT NULL AND MainPhone = @Phone)
  )
ORDER BY CASE WHEN AccountName = @AccountName THEN 0 ELSE 1 END, CreatedDateUtc DESC;", new { request.TenantId, AccountName = accountName, Email = Clean(lead.Email), Phone = Clean(lead.Phone) }, transaction: tx, cancellationToken: cancellationToken));

                if (accountId.HasValue)
                {
                    accountActionCode = "Linked";
                }
                else
                {
                    accountId = Guid.NewGuid();
                    var accountNumber = await GenerateAccountNumberAsync(cn, tx, lead.LeadNumber, cancellationToken);
                    var accountDefaults = await GetConvertedAccountDefaultsAsync(cn, tx, request.TenantId, cancellationToken);
                    await cn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO Client.Account
(AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode, MainEmail, MainPhone, StatusCode, StatusCodeId, SegmentCode, OwnerUserId, LifecycleStageCode, Industry, AnnualRevenue, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@AccountId, @TenantId, @AccountNumber, @AccountName, @AccountTypeCode, @MainEmail, @MainPhone, @StatusCode, @StatusCodeId, @SegmentCode, @OwnerUserId, @LifecycleStageCode, @Industry, @AnnualRevenue, SYSUTCDATETIME(), @CreatedByUserId, 0);", new
                    {
                        AccountId = accountId.Value,
                        request.TenantId,
                        AccountNumber = accountNumber,
                        AccountName = accountName,
                        AccountTypeCode = accountDefaults.AccountTypeCode,
                        MainEmail = Clean(lead.Email),
                        MainPhone = Clean(lead.Phone),
                        StatusCode = accountDefaults.StatusCode,
                        StatusCodeId = accountDefaults.StatusCodeId,
                        SegmentCode = estimatedAmount >= 150000m ? "Enterprise" : estimatedAmount >= 75000m ? "Mid-Market" : "Standard",
                        OwnerUserId = ownerUserId,
                        LifecycleStageCode = accountDefaults.LifecycleStageCode,
                        Industry = lineOfBusiness,
                        AnnualRevenue = estimatedAmount > 0 ? estimatedAmount : lead.AnnualRevenue,
                        CreatedByUserId = request.ConvertedByUserId
                    }, transaction: tx, cancellationToken: cancellationToken));
                }
            }

            var contactId = await UpsertConvertedContactAsync(cn, tx, request.TenantId, accountId.Value, lead, request.ConvertedByUserId, cancellationToken);
            contactId = await CopyLeadContactsToAccountAsync(cn, tx, request.TenantId, lead.LeadId, accountId.Value, request.ConvertedByUserId, contactId, cancellationToken) ?? contactId;
            var opportunityId = Guid.NewGuid();
            var opportunityNumber = await GenerateOpportunityNumberAsync(cn, tx, lead.LeadNumber, cancellationToken);
            var opportunityStage = await GetInitialOpportunityStageAsync(cn, tx, request.TenantId, cancellationToken);

            await cn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO CRM.Opportunity
(OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName, EstimatedAmount, OwnerUserId, CloseDate, LeadId, WinProbability, StageName, OpportunityStageId, StatusCodeId, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@OpportunityId, @TenantId, @OpportunityNumber, @AccountId, @OpportunityName, @EstimatedAmount, @OwnerUserId, @CloseDate, @LeadId, @WinProbability, @StageName, @OpportunityStageId, 1, @Description, SYSUTCDATETIME(), @CreatedByUserId, 0);", new
            {
                OpportunityId = opportunityId,
                request.TenantId,
                OpportunityNumber = opportunityNumber,
                AccountId = accountId.Value,
                OpportunityName = opportunityName,
                EstimatedAmount = estimatedAmount,
                OwnerUserId = ownerUserId,
                CloseDate = request.CloseDate,
                LeadId = lead.LeadId,
                WinProbability = lead.Score.HasValue ? Math.Clamp(lead.Score.Value, 25, 85) : 40,
                StageName = opportunityStage.StageName,
                OpportunityStageId = opportunityStage.OpportunityStageId,
                Description = Clean(request.Notes) ?? $"Converted from lead {lead.LeadNumber}.",
                CreatedByUserId = request.ConvertedByUserId
            }, transaction: tx, cancellationToken: cancellationToken));

            await CreateOpportunityLinesFromLeadAsync(cn, tx, request.TenantId, lead.LeadId, opportunityId, request.ConvertedByUserId, lineOfBusiness, estimatedAmount, cancellationToken);
            await CopyLeadActivitiesToOpportunityAsync(cn, tx, request.TenantId, lead.LeadId, opportunityId, request.ConvertedByUserId, cancellationToken);
            var submissionDraft = request.CreateSubmissionDraft
                ? await CreateSubmissionDraftAsync(cn, tx, request.TenantId, accountId.Value, opportunityId, lineOfBusiness, estimatedAmount, lead.PriorityCode, ownerUserId, request.ConvertedByUserId, request.CloseDate, cancellationToken)
                : null;

            await cn.ExecuteAsync(new CommandDefinition(@"
UPDATE CRM.Lead
SET AccountId = @AccountId,
    NurturingStageCode = N'Converted',
    QualifiedDate = COALESCE(QualifiedDate, SYSUTCDATETIME()),
    StatusCodeId = 4,
    ModifiedByUserId = @ModifiedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE LeadId = @LeadId AND TenantId = @TenantId AND IsDeleted = 0;", new { AccountId = accountId.Value, lead.LeadId, request.TenantId, ModifiedByUserId = request.ConvertedByUserId }, transaction: tx, cancellationToken: cancellationToken));

            var conversionId = Guid.NewGuid();
            var submissionNextStep = submissionDraft is not null ? "DraftCreated" : request.CreateSubmissionDraft ? "StartSubmission" : null;
            var conversionInsertSql = await BuildLeadConversionInsertSqlAsync(cn, tx, cancellationToken);
            await cn.ExecuteAsync(new CommandDefinition(conversionInsertSql, new
            {
                LeadConversionId = conversionId,
                request.TenantId,
                lead.LeadId,
                AccountId = accountId.Value,
                ConvertedAccountId = accountId.Value,
                OpportunityId = opportunityId,
                ConvertedOpportunityId = opportunityId,
                ContactId = contactId,
                ConvertedContactId = contactId,
                AccountActionCode = accountActionCode,
                SubmissionNextStepCode = submissionNextStep,
                SubmissionId = submissionDraft?.SubmissionId,
                SubmissionNumber = submissionDraft?.SubmissionNumber,
                SourceLeadNumber = lead.LeadNumber,
                AccountNameSnapshot = accountName,
                OpportunityNameSnapshot = opportunityName,
                EstimatedAmount = estimatedAmount,
                LineOfBusiness = lineOfBusiness,
                Notes = Clean(request.Notes),
                ConvertedByUserId = request.ConvertedByUserId,
                CreatedByUserId = request.ConvertedByUserId
            }, transaction: tx, cancellationToken: cancellationToken));

            await cn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO CRM.OpportunityWorkflowEvent
(WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @TenantId, @OpportunityId, N'Conversion', N'Lead converted', @Detail, N'Lead', @LeadId, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);", new
            {
                request.TenantId,
                OpportunityId = opportunityId,
                LeadId = lead.LeadId,
                Detail = $"Lead {lead.LeadNumber} converted to account {accountName} and opportunity {opportunityName}.",
                CreatedByUserId = request.ConvertedByUserId
            }, transaction: tx, cancellationToken: cancellationToken));

            tx.Commit();

            return new LeadConversionResultDto
            {
                LeadConversionId = conversionId,
                LeadId = lead.LeadId,
                AccountId = accountId.Value,
                OpportunityId = opportunityId,
                ContactId = contactId,
                AccountActionCode = accountActionCode,
                AccountName = accountName,
                OpportunityName = opportunityName,
                OpportunityNumber = opportunityNumber,
                LineOfBusiness = lineOfBusiness,
                EstimatedAmount = estimatedAmount,
                SubmissionDraftRequested = request.CreateSubmissionDraft,
                SubmissionId = submissionDraft?.SubmissionId,
                SubmissionNumber = submissionDraft?.SubmissionNumber,
                SubmissionDraftCreated = submissionDraft is not null
            };
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static Task EnsureLeadAccountIdColumnAsync(IDbConnection connection, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.Lead'))
AND COL_LENGTH('CRM.Lead', 'AccountId') IS NULL
    ALTER TABLE CRM.Lead ADD AccountId UNIQUEIDENTIFIER NULL;",
            cancellationToken: cancellationToken));

    private static async Task<string> BuildLeadConversionInsertSqlAsync(IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(@"
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(N'CRM.LeadConversion');", transaction: transaction, cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var insertColumns = new List<string>
        {
            "LeadConversionId",
            "TenantId",
            "LeadId",
            "AccountId",
            "OpportunityId",
            "ContactId",
            "ConversionTypeCode",
            "AccountActionCode",
            "SubmissionNextStepCode",
            "SubmissionId",
            "SubmissionNumber",
            "SourceLeadNumber",
            "AccountNameSnapshot",
            "OpportunityNameSnapshot",
            "EstimatedAmount",
            "LineOfBusiness",
            "Notes",
            "ConvertedDateUtc",
            "ConvertedByUserId",
            "CreatedDateUtc",
            "CreatedByUserId",
            "IsDeleted"
        }.Where(columns.Contains).ToList();

        if (columns.Contains("ConvertedAccountId") && !insertColumns.Contains("ConvertedAccountId", StringComparer.OrdinalIgnoreCase))
        {
            insertColumns.Insert(insertColumns.IndexOf("OpportunityId"), "ConvertedAccountId");
        }

        if (columns.Contains("ConvertedOpportunityId") && !insertColumns.Contains("ConvertedOpportunityId", StringComparer.OrdinalIgnoreCase))
        {
            insertColumns.Insert(insertColumns.IndexOf("ContactId"), "ConvertedOpportunityId");
        }

        if (columns.Contains("ConvertedContactId") && !insertColumns.Contains("ConvertedContactId", StringComparer.OrdinalIgnoreCase))
        {
            insertColumns.Insert(insertColumns.IndexOf("ConversionTypeCode"), "ConvertedContactId");
        }

        var values = insertColumns.Select(static column => column switch
        {
            "ConversionTypeCode" => "N'AccountOpportunity'",
            "ConvertedDateUtc" => "SYSUTCDATETIME()",
            "CreatedDateUtc" => "SYSUTCDATETIME()",
            "IsDeleted" => "0",
            _ => "@" + column
        });

        return $"""
INSERT INTO CRM.LeadConversion
({string.Join(", ", insertColumns)})
VALUES
({string.Join(", ", values)});
""";
    }

    private static async Task<SubmissionDraftLink> CreateSubmissionDraftAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid accountId, Guid opportunityId, string? lineOfBusiness, decimal? targetPremium, string? leadPriority, Guid assignedToUserId, Guid createdByUserId, DateTime? requestedEffectiveDate, CancellationToken cancellationToken)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<SubmissionDraftLink>(new CommandDefinition(@"
SELECT TOP 1 SubmissionId, SubmissionNumber
FROM Submissions.Submission
WHERE TenantId = @TenantId
  AND OpportunityId = @OpportunityId
  AND IsDeleted = 0
  AND Status = N'Draft'
ORDER BY CreatedDateUtc DESC;", new { TenantId = tenantId, OpportunityId = opportunityId }, transaction: transaction, cancellationToken: cancellationToken));

        if (existing is not null)
        {
            return existing;
        }

        var submissionId = Guid.NewGuid();
        var effectiveDate = requestedEffectiveDate?.Date ?? DateTime.UtcNow.Date.AddDays(30);
        var expirationDate = effectiveDate.AddYears(1);
        var premium = targetPremium.GetValueOrDefault() > 0 ? targetPremium : null;
        var draft = await connection.QuerySingleAsync<SubmissionDraftLink>(new CommandDefinition(@"
DECLARE @SubmissionNumber NVARCHAR(50) = N'SUB-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(N'0000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS NVARCHAR(20)), 4);

INSERT INTO Submissions.Submission
    (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority,
     AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount,
     CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@SubmissionId, @TenantId, @AccountId, @OpportunityId, @SubmissionNumber, @LineOfBusiness, N'Draft', @Priority,
     @AssignedToUserId, @EffectiveDate, @ExpirationDate, @TargetPremium, 0, 0,
     SYSUTCDATETIME(), @CreatedByUserId, 0);

SELECT @SubmissionId AS SubmissionId, @SubmissionNumber AS SubmissionNumber;", new
        {
            SubmissionId = submissionId,
            TenantId = tenantId,
            AccountId = accountId,
            OpportunityId = opportunityId,
            LineOfBusiness = Clean(lineOfBusiness) ?? "General Liability",
            Priority = SubmissionPriority(leadPriority, premium),
            AssignedToUserId = assignedToUserId,
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            TargetPremium = premium,
            CreatedByUserId = createdByUserId
        }, transaction: transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.SubmissionActionLog', N'U') IS NOT NULL
BEGIN
    INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
    VALUES (NEWID(), @SubmissionId, @TenantId, N'LeadConversionDraftCreated', N'Draft submission created atomically from lead conversion.', SYSUTCDATETIME(), 0);
END;

IF OBJECT_ID(N'CRM.OpportunityWorkflowEvent', N'U') IS NOT NULL
BEGIN
    INSERT INTO CRM.OpportunityWorkflowEvent
    (WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
    (NEWID(), @TenantId, @OpportunityId, N'Submission', N'Submission draft created', @Detail, N'Submission', @SubmissionId, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);
END;", new
        {
            draft.SubmissionId,
            TenantId = tenantId,
            OpportunityId = opportunityId,
            Detail = $"Draft submission {draft.SubmissionNumber} created from lead conversion.",
            CreatedByUserId = createdByUserId
        }, transaction: transaction, cancellationToken: cancellationToken));

        return draft;
    }

    private static string SubmissionPriority(string? leadPriority, decimal? premium)
    {
        var priority = Clean(leadPriority);
        if (priority is not null && priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase)) return "Urgent";
        if (priority is not null && priority.Equals("High", StringComparison.OrdinalIgnoreCase)) return "High";
        if (priority is not null && priority.Equals("Low", StringComparison.OrdinalIgnoreCase)) return "Low";
        return premium.GetValueOrDefault() >= 100000m ? "High" : "Standard";
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
        => QueryListAsync<LeadCommunicationDto>("SELECT c.CommunicationId, c.TenantId, c.LeadId, c.MessageThreadId, c.ThreadMessageId, c.Channel, c.Subject, c.Preview, c.Direction, c.DeliveryStatus, c.EngagementStatus, c.SentByUserId, COALESCE(u.DisplayName, u.FullName) AS SentByName, c.SentAt, c.Opened, c.Clicked, c.IsAutomated, c.CreatedDateUtc, c.ModifiedDateUtc FROM CRM.LeadCommunication c LEFT JOIN IAM.[User] u ON u.UserId = c.SentByUserId WHERE c.LeadId = @LeadId AND c.IsDeleted = 0 ORDER BY c.SentAt DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateCommunicationAsync(CreateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        const string sql = @"INSERT INTO CRM.LeadCommunication (CommunicationId,TenantId,LeadId,MessageThreadId,ThreadMessageId,Channel,Subject,Preview,Direction,DeliveryStatus,EngagementStatus,SentByUserId,SentAt,Opened,Clicked,IsAutomated,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES (@CommunicationId,@TenantId,@LeadId,@MessageThreadId,@ThreadMessageId,@Channel,@Subject,@Preview,@Direction,@DeliveryStatus,@EngagementStatus,@SentByUserId,@SentAt,@Opened,@Clicked,@IsAutomated,SYSUTCDATETIME(),@SentByUserId,0);";
        try
        {
            var sync = await UpsertCommunicationSyncAsync(cn, tx, request, id, null, null, cancellationToken);
            var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { CommunicationId = id, request.TenantId, request.LeadId, MessageThreadId = sync.ThreadId, ThreadMessageId = sync.MessageId, request.Channel, request.Subject, request.Preview, request.Direction, request.DeliveryStatus, request.EngagementStatus, request.SentByUserId, request.SentAt, request.Opened, request.Clicked, request.IsAutomated }, transaction: tx, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Lead communication was not created.");
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        return id;
    }

    public async Task UpdateCommunicationAsync(UpdateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        var current = await cn.QuerySingleOrDefaultAsync<LeadCommunicationDto>(new CommandDefinition("SELECT CommunicationId, MessageThreadId, ThreadMessageId FROM CRM.LeadCommunication WHERE CommunicationId = @CommunicationId AND IsDeleted = 0;", new { request.CommunicationId }, transaction: tx, cancellationToken: cancellationToken));
        const string sql = @"UPDATE CRM.LeadCommunication SET MessageThreadId=@MessageThreadId,ThreadMessageId=@ThreadMessageId,Channel=@Channel,Subject=@Subject,Preview=@Preview,Direction=@Direction,DeliveryStatus=@DeliveryStatus,EngagementStatus=@EngagementStatus,SentByUserId=@SentByUserId,SentAt=@SentAt,Opened=@Opened,Clicked=@Clicked,IsAutomated=@IsAutomated,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE CommunicationId=@CommunicationId AND IsDeleted=0;";
        try
        {
            var sync = await UpsertCommunicationSyncAsync(cn, tx, request, request.CommunicationId, current?.MessageThreadId, current?.ThreadMessageId, cancellationToken);
            var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { request.CommunicationId, MessageThreadId = sync.ThreadId, ThreadMessageId = sync.MessageId, request.Channel, request.Subject, request.Preview, request.Direction, request.DeliveryStatus, request.EngagementStatus, request.SentByUserId, request.SentAt, request.Opened, request.Clicked, request.IsAutomated, request.ModifiedByUserId }, transaction: tx, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Lead communication was not updated.");
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task DeleteCommunicationAsync(Guid communicationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        try
        {
            var current = await cn.QuerySingleOrDefaultAsync<LeadCommunicationDto>(new CommandDefinition("SELECT CommunicationId, MessageThreadId, ThreadMessageId FROM CRM.LeadCommunication WHERE CommunicationId = @CommunicationId AND IsDeleted = 0;", new { CommunicationId = communicationId }, transaction: tx, cancellationToken: cancellationToken));
            await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.LeadCommunication SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE CommunicationId = @CommunicationId AND IsDeleted = 0;", new { CommunicationId = communicationId, ModifiedByUserId = modifiedByUserId }, transaction: tx, cancellationToken: cancellationToken));
            if (current?.MessageThreadId is { } threadId)
            {
                await cn.ExecuteAsync(new CommandDefinition("UPDATE Comms.MessageThread SET Status = N'Resolved', ModifiedDateUtc = SYSUTCDATETIME() WHERE ThreadId = @ThreadId AND IsDeleted = 0;", new { ThreadId = threadId }, transaction: tx, cancellationToken: cancellationToken));
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public Task<IReadOnlyList<LeadCampaignEnrollmentDto>> GetCampaignEnrollmentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadCampaignEnrollmentDto>("SELECT EnrollmentId, TenantId, LeadId, CampaignId, CampaignName, CampaignType, Segment, Status, EnrolledAt, EmailsSent, EmailsOpen, Clicks, OpenRate, Conversions, Revenue, LastTouch, CreatedDateUtc, ModifiedDateUtc FROM CRM.LeadCampaignEnrollment WHERE LeadId = @LeadId AND IsDeleted = 0 ORDER BY EnrolledAt DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateCampaignEnrollmentAsync(CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        const string sql = @"INSERT INTO CRM.LeadCampaignEnrollment (EnrollmentId,TenantId,LeadId,CampaignId,CampaignName,CampaignType,Segment,Status,EnrolledAt,EmailsSent,EmailsOpen,Clicks,OpenRate,Conversions,Revenue,LastTouch,CreatedByUserId,CreatedDateUtc,IsDeleted) VALUES (@EnrollmentId,@TenantId,@LeadId,@CampaignId,@CampaignName,@CampaignType,@Segment,@Status,@EnrolledAt,@EmailsSent,@EmailsOpen,@Clicks,@OpenRate,@Conversions,@Revenue,@LastTouch,@CreatedByUserId,SYSUTCDATETIME(),0);";
        try
        {
            var campaign = await UpsertCampaignSyncAsync(cn, tx, request, cancellationToken);
            var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { EnrollmentId = id, request.TenantId, request.LeadId, CampaignId = campaign.CampaignId, CampaignName = campaign.Name, CampaignType = campaign.Type, Segment = campaign.Segment, request.Status, request.EnrolledAt, request.EmailsSent, request.EmailsOpen, request.Clicks, OpenRate = campaign.OpenRate, Conversions = campaign.Conversions, Revenue = campaign.Revenue, request.LastTouch, request.CreatedByUserId }, transaction: tx, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Lead campaign enrollment was not created.");
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        return id;
    }

    public async Task UpdateCampaignEnrollmentAsync(UpdateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        const string sql = @"UPDATE CRM.LeadCampaignEnrollment SET CampaignId=@CampaignId,CampaignName=@CampaignName,CampaignType=@CampaignType,Segment=@Segment,Status=@Status,EnrolledAt=@EnrolledAt,EmailsSent=@EmailsSent,EmailsOpen=@EmailsOpen,Clicks=@Clicks,OpenRate=@OpenRate,Conversions=@Conversions,Revenue=@Revenue,LastTouch=@LastTouch,ModifiedByUserId=@ModifiedByUserId,ModifiedDateUtc=SYSUTCDATETIME() WHERE EnrollmentId=@EnrollmentId AND IsDeleted=0;";
        try
        {
            var campaign = await UpsertCampaignSyncAsync(cn, tx, request, cancellationToken);
            var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { request.EnrollmentId, CampaignId = campaign.CampaignId, CampaignName = campaign.Name, CampaignType = campaign.Type, Segment = campaign.Segment, request.Status, request.EnrolledAt, request.EmailsSent, request.EmailsOpen, request.Clicks, OpenRate = campaign.OpenRate, Conversions = campaign.Conversions, Revenue = campaign.Revenue, request.LastTouch, request.ModifiedByUserId }, transaction: tx, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Lead campaign enrollment was not updated.");
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task DeleteCampaignEnrollmentAsync(Guid enrollmentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        try
        {
            var campaignId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition("SELECT CampaignId FROM CRM.LeadCampaignEnrollment WHERE EnrollmentId = @EnrollmentId AND IsDeleted = 0;", new { EnrollmentId = enrollmentId }, transaction: tx, cancellationToken: cancellationToken));
            await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.LeadCampaignEnrollment SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE EnrollmentId = @EnrollmentId AND IsDeleted = 0;", new { EnrollmentId = enrollmentId, ModifiedByUserId = modifiedByUserId }, transaction: tx, cancellationToken: cancellationToken));
            if (campaignId is { } id)
            {
                await cn.ExecuteAsync(new CommandDefinition("UPDATE Comms.Campaign SET ModifiedDateUtc = SYSUTCDATETIME() WHERE CampaignId = @CampaignId AND IsDeleted = 0;", new { CampaignId = id }, transaction: tx, cancellationToken: cancellationToken));
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public Task<IReadOnlyList<LeadDocumentDto>> GetDocumentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => QueryListAsync<LeadDocumentDto>("SELECT d.DocumentId, d.TenantId, d.LeadId, d.FileName, d.Extension, d.Category, d.SizeKb, d.UploadedByUserId, COALESCE(u.DisplayName, u.FullName) AS UploadedByName, d.UploadedAt, d.CreatedDateUtc, d.ModifiedDateUtc FROM CRM.LeadDocument d LEFT JOIN IAM.[User] u ON u.UserId = d.UploadedByUserId WHERE d.LeadId = @LeadId AND d.IsDeleted = 0 ORDER BY d.UploadedAt DESC;", new { LeadId = leadId }, cancellationToken);

    public async Task<Guid> CreateDocumentAsync(CreateLeadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var id = request.DocumentId.GetValueOrDefault(Guid.NewGuid());
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

    private static async Task<CommunicationSync> UpsertCommunicationSyncAsync(IDbConnection connection, IDbTransaction transaction, CreateLeadCommunicationRequest request, Guid communicationId, Guid? existingThreadId, Guid? existingMessageId, CancellationToken cancellationToken)
    {
        var threadId = existingThreadId ?? request.MessageThreadId ?? Guid.NewGuid();
        var messageId = existingMessageId ?? Guid.NewGuid();
        var preview = request.Preview.Length > 500 ? request.Preview[..500] : request.Preview;
        var senderName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT TOP 1 COALESCE(DisplayName, FullName, Email) FROM IAM.[User] WHERE UserId = @UserId AND IsDeleted = 0;", new { UserId = request.SentByUserId }, transaction: transaction, cancellationToken: cancellationToken)) ?? "System";

        if (existingThreadId is null)
        {
            const string insertThreadSql = @"
INSERT INTO Comms.MessageThread (ThreadId,TenantId,AccountName,AccountId,ContactName,ContactEmail,ContactPhone,Channel,Subject,BodyPreview,Status,Priority,AssignedTo,Producer,Branch,IsRead,IsEscalated,OptedOut,MessageCount,LastActivityAt,Sentiment,CsrOwner,AiSummary,CreatedDateUtc,IsDeleted)
SELECT @ThreadId, l.TenantId, COALESCE(l.AccountName, N'Lead engagement'), CONVERT(NVARCHAR(50), l.AccountId), LTRIM(RTRIM(COALESCE(l.FirstName, N'') + N' ' + COALESCE(l.LastName, N''))), l.Email, l.Phone, @Channel, @Subject, @Preview, @EngagementStatus, COALESCE(l.PriorityCode, N'Normal'), @SenderName, @SenderName, NULL, @Opened, 0, 0, 1, @SentAt, CASE WHEN @Clicked = 1 THEN N'Positive' ELSE N'Neutral' END, @SenderName, CONCAT(N'Lead communication synced from CRM engagement item ', CONVERT(NVARCHAR(36), @CommunicationId)), SYSUTCDATETIME(), 0
FROM CRM.Lead l
WHERE l.LeadId = @LeadId AND l.IsDeleted = 0;";
            var affected = await connection.ExecuteAsync(new CommandDefinition(insertThreadSql, new { ThreadId = threadId, request.LeadId, request.Channel, request.Subject, Preview = preview, request.EngagementStatus, SenderName = senderName, request.Opened, request.Clicked, request.SentAt, CommunicationId = communicationId }, transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Communication sync thread was not created.");
        }
        else
        {
            const string updateThreadSql = @"
UPDATE Comms.MessageThread
SET Channel = @Channel,
    Subject = @Subject,
    BodyPreview = @Preview,
    Status = @EngagementStatus,
    IsRead = @Opened,
    MessageCount = CASE WHEN MessageCount <= 0 THEN 1 ELSE MessageCount END,
    LastActivityAt = @SentAt,
    Sentiment = CASE WHEN @Clicked = 1 THEN N'Positive' ELSE Sentiment END,
    CsrOwner = COALESCE(CsrOwner, @SenderName),
    AiSummary = CONCAT(N'Lead communication synced from CRM engagement item ', CONVERT(NVARCHAR(36), @CommunicationId)),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;";
            var affected = await connection.ExecuteAsync(new CommandDefinition(updateThreadSql, new { ThreadId = threadId, request.Channel, request.Subject, Preview = preview, request.EngagementStatus, SenderName = senderName, request.Opened, request.Clicked, request.SentAt, CommunicationId = communicationId }, transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Communication sync thread was not updated.");
        }

        if (existingMessageId is null)
        {
            const string insertMessageSql = @"INSERT INTO Comms.ThreadMessage (MessageId,ThreadId,SenderName,Channel,Direction,Body,SentAt,DeliveryStatus,IsAutomated) VALUES (@MessageId,@ThreadId,@SenderName,@Channel,@Direction,@Body,@SentAt,@DeliveryStatus,@IsAutomated);";
            var affected = await connection.ExecuteAsync(new CommandDefinition(insertMessageSql, new { MessageId = messageId, ThreadId = threadId, SenderName = senderName, request.Channel, request.Direction, Body = request.Preview, request.SentAt, request.DeliveryStatus, request.IsAutomated }, transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Communication sync message was not created.");
        }
        else
        {
            const string updateMessageSql = @"UPDATE Comms.ThreadMessage SET SenderName=@SenderName, Channel=@Channel, Direction=@Direction, Body=@Body, SentAt=@SentAt, DeliveryStatus=@DeliveryStatus, IsAutomated=@IsAutomated WHERE MessageId=@MessageId;";
            var affected = await connection.ExecuteAsync(new CommandDefinition(updateMessageSql, new { MessageId = messageId, SenderName = senderName, request.Channel, request.Direction, Body = request.Preview, request.SentAt, request.DeliveryStatus, request.IsAutomated }, transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Communication sync message was not updated.");
        }

        return new(threadId, messageId);
    }

    private static async Task<CampaignSync> UpsertCampaignSyncAsync(IDbConnection connection, IDbTransaction transaction, CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var existing = request.CampaignId is { } campaignId
            ? await connection.QuerySingleOrDefaultAsync<CampaignSync>(new CommandDefinition("SELECT CampaignId, Name, Type, Status, Segment, OpenRate, Conversions, Revenue FROM Comms.Campaign WHERE CampaignId = @CampaignId AND IsDeleted = 0;", new { CampaignId = campaignId }, transaction: transaction, cancellationToken: cancellationToken))
            : await connection.QuerySingleOrDefaultAsync<CampaignSync>(new CommandDefinition("SELECT TOP 1 CampaignId, Name, Type, Status, Segment, OpenRate, Conversions, Revenue FROM Comms.Campaign WHERE TenantId = @TenantId AND Name = @Name AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;", new { request.TenantId, Name = request.CampaignName }, transaction: transaction, cancellationToken: cancellationToken));

        if (existing is not null)
        {
            const string updateSql = @"
UPDATE Comms.Campaign
SET Status = @Status,
    Reached = CASE WHEN @EmailsSent > Reached THEN @EmailsSent ELSE Reached END,
    OpenRate = @OpenRate,
    Conversions = @Conversions,
    Revenue = @Revenue,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CampaignId = @CampaignId AND IsDeleted = 0;";
            var affected = await connection.ExecuteAsync(new CommandDefinition(updateSql, new { existing.CampaignId, request.Status, request.EmailsSent, OpenRate = request.OpenRate > 0 ? request.OpenRate : existing.OpenRate, Conversions = Math.Max(request.Conversions, existing.Conversions), Revenue = Math.Max(request.Revenue, existing.Revenue) }, transaction: transaction, cancellationToken: cancellationToken));
            if (affected == 0) throw new InvalidOperationException("Campaign sync record was not updated.");
            return existing with
            {
                Status = request.Status,
                OpenRate = request.OpenRate > 0 ? request.OpenRate : existing.OpenRate,
                Conversions = Math.Max(request.Conversions, existing.Conversions),
                Revenue = Math.Max(request.Revenue, existing.Revenue)
            };
        }

        var newCampaignId = Guid.NewGuid();
        var type = request.CampaignType!.Trim();
        var segment = request.Segment!.Trim();
        const string insertSql = @"
INSERT INTO Comms.Campaign (CampaignId,TenantId,Name,Type,Status,Segment,StartDate,Reached,OpenRate,Conversions,Revenue,CreatedDateUtc,IsDeleted)
VALUES (@CampaignId,@TenantId,@Name,@Type,@Status,@Segment,@StartDate,@Reached,@OpenRate,@Conversions,@Revenue,SYSUTCDATETIME(),0);";
        var inserted = await connection.ExecuteAsync(new CommandDefinition(insertSql, new { CampaignId = newCampaignId, request.TenantId, Name = request.CampaignName, Type = type, request.Status, Segment = segment, StartDate = request.EnrolledAt, Reached = request.EmailsSent, request.OpenRate, request.Conversions, request.Revenue }, transaction: transaction, cancellationToken: cancellationToken));
        if (inserted == 0) throw new InvalidOperationException("Campaign sync record was not created.");
        return new(newCampaignId, request.CampaignName, type, request.Status, segment, request.OpenRate, request.Conversions, request.Revenue);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private static async Task<string?> GetPrimaryLeadLineOfBusinessAsync(IDbConnection connection, IDbTransaction transaction, Guid leadId, CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 LineOfBusiness
FROM CRM.LeadInterestLine
WHERE LeadId = @LeadId AND IsDeleted = 0
ORDER BY EstPremium DESC, CreatedDateUtc DESC;", new { LeadId = leadId }, transaction: transaction, cancellationToken: cancellationToken));
    }

    private static async Task<decimal?> GetPrimaryLeadEstimatedPremiumAsync(IDbConnection connection, IDbTransaction transaction, Guid leadId, CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(@"
SELECT NULLIF(SUM(EstPremium), 0)
FROM CRM.LeadInterestLine
WHERE LeadId = @LeadId AND IsDeleted = 0;", new { LeadId = leadId }, transaction: transaction, cancellationToken: cancellationToken));
    }

    private static async Task<string> GenerateAccountNumberAsync(IDbConnection connection, IDbTransaction transaction, string leadNumber, CancellationToken cancellationToken)
    {
        var seed = SanitizeNumberToken(leadNumber);
        var candidate = $"ACC-{seed}";
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM Client.Account WHERE AccountNumber = @Candidate AND IsDeleted = 0;", new { Candidate = candidate }, transaction: transaction, cancellationToken: cancellationToken));
        if (exists == 0)
        {
            return candidate;
        }

        return $"ACC-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private static async Task<string> GenerateOpportunityNumberAsync(IDbConnection connection, IDbTransaction transaction, string leadNumber, CancellationToken cancellationToken)
    {
        var seed = SanitizeNumberToken(leadNumber);
        var candidate = $"OPP-{seed}";
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM CRM.Opportunity WHERE OpportunityNumber = @Candidate AND IsDeleted = 0;", new { Candidate = candidate }, transaction: transaction, cancellationToken: cancellationToken));
        if (exists == 0)
        {
            return candidate;
        }

        return $"OPP-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    private static async Task<ConvertedAccountDefaults> GetConvertedAccountDefaultsAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, CancellationToken cancellationToken)
    {
        var defaults = await connection.QuerySingleAsync<ConvertedAccountDefaults>(new CommandDefinition(@"
SELECT
    AccountTypeCode = COALESCE(
        (SELECT TOP 1 AccountTypeCode FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountTypeCode IS NOT NULL AND UPPER(AccountTypeCode) IN (N'PROSPECT', N'CLIENT', N'CUSTOMER') ORDER BY CASE UPPER(AccountTypeCode) WHEN N'PROSPECT' THEN 0 WHEN N'CLIENT' THEN 1 WHEN N'CUSTOMER' THEN 2 ELSE 3 END, CreatedDateUtc DESC),
        (SELECT TOP 1 AccountTypeCode FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountTypeCode IS NOT NULL ORDER BY CreatedDateUtc DESC),
        (SELECT TOP 1 AccountTypeCode FROM Client.Account WHERE IsDeleted = 0 AND AccountTypeCode IS NOT NULL AND UPPER(AccountTypeCode) IN (N'PROSPECT', N'CLIENT', N'CUSTOMER') ORDER BY CASE UPPER(AccountTypeCode) WHEN N'PROSPECT' THEN 0 WHEN N'CLIENT' THEN 1 WHEN N'CUSTOMER' THEN 2 ELSE 3 END, CreatedDateUtc DESC),
        (SELECT TOP 1 AccountTypeCode FROM Client.Account WHERE IsDeleted = 0 AND AccountTypeCode IS NOT NULL ORDER BY CreatedDateUtc DESC)
    ),
    StatusCode = COALESCE(
        (SELECT TOP 1 StatusCode FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode IS NOT NULL AND StatusCodeId IS NOT NULL AND (UPPER(StatusCode) = N'ACTIVE' OR StatusCodeId = 1) ORDER BY CASE WHEN UPPER(StatusCode) = N'ACTIVE' THEN 0 ELSE 1 END, CreatedDateUtc DESC),
        (SELECT TOP 1 StatusCode FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCode IS NOT NULL AND StatusCodeId IS NOT NULL ORDER BY CreatedDateUtc DESC),
        (SELECT TOP 1 StatusCode FROM Client.Account WHERE IsDeleted = 0 AND StatusCode IS NOT NULL AND StatusCodeId IS NOT NULL AND (UPPER(StatusCode) = N'ACTIVE' OR StatusCodeId = 1) ORDER BY CASE WHEN UPPER(StatusCode) = N'ACTIVE' THEN 0 ELSE 1 END, CreatedDateUtc DESC),
        (SELECT TOP 1 StatusCode FROM Client.Account WHERE IsDeleted = 0 AND StatusCode IS NOT NULL AND StatusCodeId IS NOT NULL ORDER BY CreatedDateUtc DESC)
    ),
    StatusCodeId = COALESCE(
        (SELECT TOP 1 StatusCodeId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId IS NOT NULL AND (UPPER(StatusCode) = N'ACTIVE' OR StatusCodeId = 1) ORDER BY CASE WHEN StatusCodeId = 1 THEN 0 ELSE 1 END, CreatedDateUtc DESC),
        (SELECT TOP 1 StatusCodeId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND StatusCodeId IS NOT NULL ORDER BY CreatedDateUtc DESC),
        (SELECT TOP 1 StatusCodeId FROM Client.Account WHERE IsDeleted = 0 AND StatusCodeId IS NOT NULL AND (UPPER(StatusCode) = N'ACTIVE' OR StatusCodeId = 1) ORDER BY CASE WHEN StatusCodeId = 1 THEN 0 ELSE 1 END, CreatedDateUtc DESC),
        (SELECT TOP 1 StatusCodeId FROM Client.Account WHERE IsDeleted = 0 AND StatusCodeId IS NOT NULL ORDER BY CreatedDateUtc DESC)
    ),
    LifecycleStageCode = COALESCE(
        (SELECT TOP 1 LifecycleStageCode FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND LifecycleStageCode IS NOT NULL AND UPPER(LifecycleStageCode) IN (N'PROSPECT', N'LEAD') ORDER BY CASE UPPER(LifecycleStageCode) WHEN N'PROSPECT' THEN 0 WHEN N'LEAD' THEN 1 ELSE 2 END, CreatedDateUtc DESC),
        (SELECT TOP 1 LifecycleStageCode FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND LifecycleStageCode IS NOT NULL ORDER BY CreatedDateUtc DESC),
        (SELECT TOP 1 LifecycleStageCode FROM Client.Account WHERE IsDeleted = 0 AND LifecycleStageCode IS NOT NULL AND UPPER(LifecycleStageCode) IN (N'PROSPECT', N'LEAD') ORDER BY CASE UPPER(LifecycleStageCode) WHEN N'PROSPECT' THEN 0 WHEN N'LEAD' THEN 1 ELSE 2 END, CreatedDateUtc DESC),
        (SELECT TOP 1 LifecycleStageCode FROM Client.Account WHERE IsDeleted = 0 AND LifecycleStageCode IS NOT NULL ORDER BY CreatedDateUtc DESC)
    );", new { TenantId = tenantId }, transaction: transaction, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(defaults.AccountTypeCode))
        {
            throw new InvalidOperationException("Lead conversion cannot create an account because no DB-backed account type is configured.");
        }

        if (string.IsNullOrWhiteSpace(defaults.StatusCode) || !defaults.StatusCodeId.HasValue)
        {
            throw new InvalidOperationException("Lead conversion cannot create an account because no DB-backed account status is configured.");
        }

        if (string.IsNullOrWhiteSpace(defaults.LifecycleStageCode))
        {
            throw new InvalidOperationException("Lead conversion cannot create an account because no DB-backed account lifecycle stage is configured.");
        }

        return defaults;
    }

    private static async Task<ConvertedOpportunityStage> GetInitialOpportunityStageAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, CancellationToken cancellationToken)
    {
        var stage = await connection.QuerySingleOrDefaultAsync<ConvertedOpportunityStage>(new CommandDefinition(@"
IF OBJECT_ID(N'CRM.OpportunityStage', N'U') IS NULL
BEGIN
    SELECT CAST(NULL AS UNIQUEIDENTIFIER) AS OpportunityStageId, CAST(NULL AS NVARCHAR(100)) AS StageName;
    RETURN;
END;

SELECT TOP 1 OpportunityStageId, StageName
FROM CRM.OpportunityStage
WHERE TenantId = @TenantId
  AND IsActive = 1
ORDER BY SortOrder, StageName;", new { TenantId = tenantId }, transaction: transaction, cancellationToken: cancellationToken));

        if (stage is null || stage.OpportunityStageId == Guid.Empty || string.IsNullOrWhiteSpace(stage.StageName))
        {
            throw new InvalidOperationException("Lead conversion cannot create an opportunity because no DB-backed opportunity stage is configured.");
        }

        return stage;
    }

    private sealed record ConvertedOpportunityStage(Guid OpportunityStageId, string StageName);

    private static async Task<Guid?> UpsertConvertedContactAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid accountId, LeadDto lead, Guid convertedByUserId, CancellationToken cancellationToken)
    {
        var firstName = Clean(lead.FirstName);
        var lastName = Clean(lead.LastName);
        var email = Clean(lead.Email);
        var phone = Clean(lead.Phone);
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var existing = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 ContactId
FROM Client.Contact
WHERE TenantId = @TenantId AND AccountId = @AccountId AND IsDeleted = 0
  AND ((@Email IS NOT NULL AND Email = @Email) OR (@Phone IS NOT NULL AND Phone = @Phone))
ORDER BY CreatedDateUtc DESC;", new { TenantId = tenantId, AccountId = accountId, Email = email, Phone = phone }, transaction: transaction, cancellationToken: cancellationToken));
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var contactId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO Client.Contact
(ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone, JobTitle, ContactTypeCode, IsBillingContact, IsPortalUser, IsKeyContact, IsServiceContact, PreferredContactMethod, StatusCode, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@ContactId, @TenantId, @AccountId, @FirstName, @LastName, @Email, @Phone, @JobTitle, N'Primary', 0, 0, 1, 1, @PreferredContactMethod, N'Active', 1, SYSUTCDATETIME(), @CreatedByUserId, 0);", new
        {
            ContactId = contactId,
            TenantId = tenantId,
            AccountId = accountId,
            FirstName = firstName ?? "Primary",
            LastName = lastName ?? "Contact",
            Email = email,
            Phone = phone,
            JobTitle = Clean(lead.InterestedService),
            PreferredContactMethod = email is not null ? "Email" : phone is not null ? "Phone" : null,
            CreatedByUserId = convertedByUserId
        }, transaction: transaction, cancellationToken: cancellationToken));

        return contactId;
    }

    private static async Task<Guid?> CopyLeadContactsToAccountAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid leadId, Guid accountId, Guid convertedByUserId, Guid? currentPrimaryContactId, CancellationToken cancellationToken)
    {
        var contacts = (await connection.QueryAsync<LeadContactDto>(new CommandDefinition(@"
SELECT ContactId, TenantId, LeadId, FirstName, LastName, Title, Email, Phone, IsPrimary, CreatedDateUtc, ModifiedDateUtc
FROM CRM.LeadContact
WHERE TenantId = @TenantId AND LeadId = @LeadId AND IsDeleted = 0
ORDER BY IsPrimary DESC, CreatedDateUtc;", new { TenantId = tenantId, LeadId = leadId }, transaction: transaction, cancellationToken: cancellationToken))).AsList();

        Guid? primaryContactId = currentPrimaryContactId;
        foreach (var leadContact in contacts)
        {
            var email = Clean(leadContact.Email);
            var phone = Clean(leadContact.Phone);
            var existingContactId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 ContactId
FROM Client.Contact
WHERE TenantId = @TenantId AND AccountId = @AccountId AND IsDeleted = 0
  AND ((@Email IS NOT NULL AND Email = @Email) OR (@Phone IS NOT NULL AND Phone = @Phone))
ORDER BY CreatedDateUtc DESC;", new { TenantId = tenantId, AccountId = accountId, Email = email, Phone = phone }, transaction: transaction, cancellationToken: cancellationToken));

            var contactId = existingContactId ?? Guid.NewGuid();
            if (!existingContactId.HasValue)
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO Client.Contact
(ContactId, TenantId, AccountId, FirstName, LastName, Email, Phone, JobTitle, ContactTypeCode, IsBillingContact, IsPortalUser, IsKeyContact, IsServiceContact, PreferredContactMethod, StatusCode, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@ContactId, @TenantId, @AccountId, @FirstName, @LastName, @Email, @Phone, @JobTitle, N'Primary', 0, 0, @IsKeyContact, @IsServiceContact, @PreferredContactMethod, N'Active', 1, SYSUTCDATETIME(), @CreatedByUserId, 0);", new
                {
                    ContactId = contactId,
                    TenantId = tenantId,
                    AccountId = accountId,
                    FirstName = Clean(leadContact.FirstName) ?? "Primary",
                    LastName = Clean(leadContact.LastName) ?? "Contact",
                    Email = email,
                    Phone = phone,
                    JobTitle = Clean(leadContact.Title),
                    IsKeyContact = leadContact.IsPrimary,
                    IsServiceContact = leadContact.IsPrimary,
                    PreferredContactMethod = email is not null ? "Email" : phone is not null ? "Phone" : null,
                    CreatedByUserId = convertedByUserId
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            if (leadContact.IsPrimary || primaryContactId is null)
            {
                primaryContactId = contactId;
            }
        }

        return primaryContactId;
    }

    private static async Task CreateOpportunityLinesFromLeadAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid leadId, Guid opportunityId, Guid createdByUserId, string? fallbackLineOfBusiness, decimal fallbackEstimatedAmount, CancellationToken cancellationToken)
    {
        var inserted = await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO CRM.OpportunityLine
(OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), TenantId, @OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority, SYSUTCDATETIME(), @CreatedByUserId, 0
FROM CRM.LeadInterestLine
WHERE TenantId = @TenantId AND LeadId = @LeadId AND IsDeleted = 0;", new { TenantId = tenantId, LeadId = leadId, OpportunityId = opportunityId, CreatedByUserId = createdByUserId }, transaction: transaction, cancellationToken: cancellationToken));

        if (inserted == 0 && !string.IsNullOrWhiteSpace(fallbackLineOfBusiness))
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO CRM.OpportunityLine
(OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, EstPremium, Priority, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @TenantId, @OpportunityId, @LineOfBusiness, @EstPremium, N'Medium', SYSUTCDATETIME(), @CreatedByUserId, 0);", new { TenantId = tenantId, OpportunityId = opportunityId, LineOfBusiness = fallbackLineOfBusiness, EstPremium = fallbackEstimatedAmount, CreatedByUserId = createdByUserId }, transaction: transaction, cancellationToken: cancellationToken));
        }
    }

    private static Task CopyLeadActivitiesToOpportunityAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid leadId, Guid opportunityId, Guid createdByUserId, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO CRM.OpportunityActivity
(ActivityId, TenantId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), TenantId, @OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, SYSUTCDATETIME(), COALESCE(CreatedByUserId, @CreatedByUserId), 0
FROM CRM.LeadActivity la
WHERE la.TenantId = @TenantId
  AND la.LeadId = @LeadId
  AND la.IsDeleted = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM CRM.OpportunityActivity oa
      WHERE oa.OpportunityId = @OpportunityId
        AND oa.IsDeleted = 0
        AND oa.Subject = la.Subject
        AND oa.ActivityDate = la.ActivityDate
  );

UPDATE CRM.LeadActivity
SET OpportunityId = @OpportunityId,
    ModifiedByUserId = @CreatedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND LeadId = @LeadId AND IsDeleted = 0 AND OpportunityId IS NULL;", new { TenantId = tenantId, LeadId = leadId, OpportunityId = opportunityId, CreatedByUserId = createdByUserId }, transaction: transaction, cancellationToken: cancellationToken));

    private static string SanitizeNumberToken(string? value)
    {
        var token = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N")[..10] : value.Trim();
        foreach (var c in new[] { ' ', '/', '\\', '#', ':', ';', ',', '.', '\t', '\r', '\n' })
        {
            token = token.Replace(c, '-');
        }

        return token.Length <= 42 ? token : token[..42];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    private sealed record SubmissionDraftLink(Guid SubmissionId, string SubmissionNumber);

    private sealed record ConvertedAccountDefaults(string? AccountTypeCode, string? StatusCode, int? StatusCodeId, string? LifecycleStageCode);

    private sealed record CommunicationSync(Guid ThreadId, Guid MessageId);

    private sealed record CampaignSync(Guid CampaignId, string Name, string Type, string Status, string Segment, decimal OpenRate, int Conversions, decimal Revenue);

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
