using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCoverages;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyCoverageRepository : IPolicyCoverageRepository
{
    private readonly ISqlConnectionFactory _cf;

    public PolicyCoverageRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string DetailCols = @"CoverageDetailId, TenantId, PolicyId, PolicyNumber, CoverageTypeId, CoverageCode, CoverageName,
LineOfBusinessCode, CoverageCategoryCode, CoverageFormCode, CoverageTriggerCode, ValuationBasisCode, TerritoryCode,
OccurrenceLimit, AggregateLimit, Sublimit, Deductible, Retention, Premium, Rate, ExposureBase, ExposureBasisCode,
EffectiveDate, ExpirationDate, CarrierName, WritingCompanyName, UnderwriterName, StatusCode, IsIncluded, IsAuditable,
IsClaimsMade, RequiresSchedule, RequiresCertificateReview, FormsAndEndorsements, CoinsuranceClause, BlanketOrSpecificCode,
CoveredOperations, Exclusions, Conditions, RatingNotes, ServiceInstructions, AuditInstructions, CertificateInstructions,
CreatedDateUtc, ModifiedDateUtc";

    private const string FieldCols = @"FieldId, CoverageDetailId, FieldGroupCode, FieldCode, FieldLabel, FieldValue, FieldValueTypeCode,
UnitOfMeasureCode, IsRequired, IsRatingField, IsScheduleField, SortOrder";

    private const string TemplateCols = @"TemplateId, TenantId, CoverageTypeId, CoverageCode, CoverageName, LineOfBusinessCode,
CoverageCategoryCode, CoverageFormCode, CoverageTriggerCode, ValuationBasisCode, TerritoryCode, DefaultOccurrenceLimit,
DefaultAggregateLimit, DefaultSublimit, DefaultDeductible, DefaultRetention, DefaultPremium, DefaultRate, DefaultExposureBase,
ExposureBasisCode, CarrierName, WritingCompanyName, UnderwriterName, StatusCode, IsIncluded, IsAuditable, IsClaimsMade,
RequiresSchedule, RequiresCertificateReview, FormsAndEndorsements, CoinsuranceClause, BlanketOrSpecificCode, CoveredOperations,
Exclusions, Conditions, RatingNotes, ServiceInstructions, AuditInstructions, CertificateInstructions, SortOrder";

    private const string TemplateFieldCols = @"TemplateFieldId, TemplateId, FieldGroupCode, FieldCode, FieldLabel, DefaultValue,
FieldValueTypeCode, UnitOfMeasureCode, IsRequired, IsRatingField, IsScheduleField, SortOrder";

    public async Task<IReadOnlyList<PolicyCoverageDetailDto>> GetByPolicyAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        var details = (await cn.QueryAsync<PolicyCoverageDetailDto>(new CommandDefinition($@"
SELECT {DetailCols}
FROM Policy.PolicyCoverageDetail
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY LineOfBusinessCode, CoverageCategoryCode, CoverageName;", new { TenantId = tenantId, PolicyId = policyId }, cancellationToken: cancellationToken))).AsList();

        await HydrateFieldsAsync(cn, details, cancellationToken);
        return details;
    }

    public async Task<IReadOnlyList<PolicyCoverageDetailTemplateDto>> GetTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        var templates = (await cn.QueryAsync<PolicyCoverageDetailTemplateDto>(new CommandDefinition($@"
SELECT {TemplateCols}
FROM Policy.PolicyCoverageDetailTemplate
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder, CoverageName;", new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();

        if (templates.Count == 0)
        {
            return templates;
        }

        var fields = (await cn.QueryAsync<PolicyCoverageDetailTemplateFieldDto>(new CommandDefinition($@"
SELECT {TemplateFieldCols}
FROM Policy.PolicyCoverageDetailTemplateField
WHERE TemplateId IN @Ids AND IsDeleted = 0
ORDER BY FieldGroupCode, SortOrder, FieldLabel;", new { Ids = templates.Select(x => x.TemplateId).ToArray() }, cancellationToken: cancellationToken))).AsList();

        var lookup = fields.GroupBy(x => x.TemplateId).ToDictionary(x => x.Key, x => x.ToList());
        foreach (var template in templates)
        {
            template.Fields = lookup.GetValueOrDefault(template.TemplateId) ?? [];
        }

        return templates;
    }

    public async Task<PolicyCoverageDetailDto?> GetByCodeAsync(Guid tenantId, Guid policyId, string coverageCode, CancellationToken cancellationToken = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        var detail = await cn.QuerySingleOrDefaultAsync<PolicyCoverageDetailDto>(new CommandDefinition($@"
SELECT {DetailCols}
FROM Policy.PolicyCoverageDetail
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND CoverageCode = @CoverageCode AND IsDeleted = 0;", new { TenantId = tenantId, PolicyId = policyId, CoverageCode = coverageCode }, cancellationToken: cancellationToken));

        if (detail is not null)
        {
            await HydrateFieldsAsync(cn, [detail], cancellationToken);
        }

        return detail;
    }

    public async Task<Guid> CreateAsync(CreatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Policy.PolicyCoverageDetail
(CoverageDetailId, TenantId, PolicyId, PolicyNumber, CoverageTypeId, CoverageCode, CoverageName, LineOfBusinessCode,
 CoverageCategoryCode, CoverageFormCode, CoverageTriggerCode, ValuationBasisCode, TerritoryCode, OccurrenceLimit,
 AggregateLimit, Sublimit, Deductible, Retention, Premium, Rate, ExposureBase, ExposureBasisCode, EffectiveDate,
 ExpirationDate, CarrierName, WritingCompanyName, UnderwriterName, StatusCode, IsIncluded, IsAuditable, IsClaimsMade,
 RequiresSchedule, RequiresCertificateReview, FormsAndEndorsements, CoinsuranceClause, BlanketOrSpecificCode,
 CoveredOperations, Exclusions, Conditions, RatingNotes, ServiceInstructions, AuditInstructions, CertificateInstructions,
 CreatedByUserId)
VALUES
(@CoverageDetailId, @TenantId, @PolicyId, @PolicyNumber, @CoverageTypeId, @CoverageCode, @CoverageName, @LineOfBusinessCode,
 @CoverageCategoryCode, @CoverageFormCode, @CoverageTriggerCode, @ValuationBasisCode, @TerritoryCode, @OccurrenceLimit,
 @AggregateLimit, @Sublimit, @Deductible, @Retention, @Premium, @Rate, @ExposureBase, @ExposureBasisCode, @EffectiveDate,
 @ExpirationDate, @CarrierName, @WritingCompanyName, @UnderwriterName, @StatusCode, @IsIncluded, @IsAuditable, @IsClaimsMade,
 @RequiresSchedule, @RequiresCertificateReview, @FormsAndEndorsements, @CoinsuranceClause, @BlanketOrSpecificCode,
 @CoveredOperations, @Exclusions, @Conditions, @RatingNotes, @ServiceInstructions, @AuditInstructions, @CertificateInstructions,
 @CreatedByUserId);";

        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CoverageDetailId = id,
            request.TenantId,
            request.PolicyId,
            request.PolicyNumber,
            request.CoverageTypeId,
            request.CoverageCode,
            request.CoverageName,
            request.LineOfBusinessCode,
            request.CoverageCategoryCode,
            request.CoverageFormCode,
            request.CoverageTriggerCode,
            request.ValuationBasisCode,
            request.TerritoryCode,
            request.OccurrenceLimit,
            request.AggregateLimit,
            request.Sublimit,
            request.Deductible,
            request.Retention,
            request.Premium,
            request.Rate,
            request.ExposureBase,
            request.ExposureBasisCode,
            request.EffectiveDate,
            request.ExpirationDate,
            request.CarrierName,
            request.WritingCompanyName,
            request.UnderwriterName,
            request.StatusCode,
            request.IsIncluded,
            request.IsAuditable,
            request.IsClaimsMade,
            request.RequiresSchedule,
            request.RequiresCertificateReview,
            request.FormsAndEndorsements,
            request.CoinsuranceClause,
            request.BlanketOrSpecificCode,
            request.CoveredOperations,
            request.Exclusions,
            request.Conditions,
            request.RatingNotes,
            request.ServiceInstructions,
            request.AuditInstructions,
            request.CertificateInstructions,
            request.CreatedByUserId
        }, tx, cancellationToken: cancellationToken));

        foreach (var field in request.Fields)
        {
            await cn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO Policy.PolicyCoverageDetailField
(FieldId, TenantId, CoverageDetailId, FieldGroupCode, FieldCode, FieldLabel, FieldValue, FieldValueTypeCode, UnitOfMeasureCode, IsRequired, IsRatingField, IsScheduleField, SortOrder, CreatedByUserId)
VALUES
(@FieldId, @TenantId, @CoverageDetailId, @FieldGroupCode, @FieldCode, @FieldLabel, @FieldValue, @FieldValueTypeCode, @UnitOfMeasureCode, @IsRequired, @IsRatingField, @IsScheduleField, @SortOrder, @CreatedByUserId);",
                new
                {
                    FieldId = Guid.NewGuid(),
                    request.TenantId,
                    CoverageDetailId = id,
                    field.FieldGroupCode,
                    field.FieldCode,
                    field.FieldLabel,
                    field.FieldValue,
                    field.FieldValueTypeCode,
                    field.UnitOfMeasureCode,
                    field.IsRequired,
                    field.IsRatingField,
                    field.IsScheduleField,
                    field.SortOrder,
                    CreatedByUserId = field.CreatedByUserId ?? request.CreatedByUserId
                }, tx, cancellationToken: cancellationToken));
        }

        tx.Commit();
        return id;
    }

    public async Task<PolicyCoverageDetailDto?> GetByIdAsync(Guid tenantId, Guid coverageDetailId, CancellationToken cancellationToken = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        var detail = await cn.QuerySingleOrDefaultAsync<PolicyCoverageDetailDto>(new CommandDefinition($@"
SELECT {DetailCols}
FROM Policy.PolicyCoverageDetail
WHERE TenantId = @TenantId AND CoverageDetailId = @CoverageDetailId AND IsDeleted = 0;", new { TenantId = tenantId, CoverageDetailId = coverageDetailId }, cancellationToken: cancellationToken));

        if (detail is not null)
        {
            await HydrateFieldsAsync(cn, [detail], cancellationToken);
        }

        return detail;
    }

    public async Task UpdateAsync(Guid coverageDetailId, UpdatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCoverageDetail
SET CoverageName = @CoverageName,
    CoverageCategoryCode = @CoverageCategoryCode,
    CoverageFormCode = @CoverageFormCode,
    CoverageTriggerCode = @CoverageTriggerCode,
    ValuationBasisCode = @ValuationBasisCode,
    TerritoryCode = @TerritoryCode,
    OccurrenceLimit = @OccurrenceLimit,
    AggregateLimit = @AggregateLimit,
    Sublimit = @Sublimit,
    Deductible = @Deductible,
    Retention = @Retention,
    Premium = @Premium,
    Rate = @Rate,
    ExposureBase = @ExposureBase,
    ExposureBasisCode = @ExposureBasisCode,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    CarrierName = @CarrierName,
    WritingCompanyName = @WritingCompanyName,
    UnderwriterName = @UnderwriterName,
    StatusCode = @StatusCode,
    IsIncluded = @IsIncluded,
    IsAuditable = @IsAuditable,
    IsClaimsMade = @IsClaimsMade,
    RequiresSchedule = @RequiresSchedule,
    RequiresCertificateReview = @RequiresCertificateReview,
    FormsAndEndorsements = @FormsAndEndorsements,
    CoinsuranceClause = @CoinsuranceClause,
    BlanketOrSpecificCode = @BlanketOrSpecificCode,
    CoveredOperations = @CoveredOperations,
    Exclusions = @Exclusions,
    Conditions = @Conditions,
    RatingNotes = @RatingNotes,
    ServiceInstructions = @ServiceInstructions,
    AuditInstructions = @AuditInstructions,
    CertificateInstructions = @CertificateInstructions,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CoverageDetailId = @CoverageDetailId AND TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CoverageDetailId = coverageDetailId,
            request.TenantId,
            request.CoverageName,
            request.CoverageCategoryCode,
            request.CoverageFormCode,
            request.CoverageTriggerCode,
            request.ValuationBasisCode,
            request.TerritoryCode,
            request.OccurrenceLimit,
            request.AggregateLimit,
            request.Sublimit,
            request.Deductible,
            request.Retention,
            request.Premium,
            request.Rate,
            request.ExposureBase,
            request.ExposureBasisCode,
            request.EffectiveDate,
            request.ExpirationDate,
            request.CarrierName,
            request.WritingCompanyName,
            request.UnderwriterName,
            request.StatusCode,
            request.IsIncluded,
            request.IsAuditable,
            request.IsClaimsMade,
            request.RequiresSchedule,
            request.RequiresCertificateReview,
            request.FormsAndEndorsements,
            request.CoinsuranceClause,
            request.BlanketOrSpecificCode,
            request.CoveredOperations,
            request.Exclusions,
            request.Conditions,
            request.RatingNotes,
            request.ServiceInstructions,
            request.AuditInstructions,
            request.CertificateInstructions,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid coverageDetailId, DeletePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCoverageDetail
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE TenantId = @TenantId AND CoverageDetailId = @CoverageDetailId AND IsDeleted = 0;

UPDATE Policy.PolicyCoverageDetailField
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE TenantId = @TenantId AND CoverageDetailId = @CoverageDetailId AND IsDeleted = 0;";
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CoverageDetailId = coverageDetailId, request.TenantId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateFieldAsync(CreatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Policy.PolicyCoverageDetailField
(FieldId, TenantId, CoverageDetailId, FieldGroupCode, FieldCode, FieldLabel, FieldValue, FieldValueTypeCode, UnitOfMeasureCode, IsRequired, IsRatingField, IsScheduleField, SortOrder, CreatedByUserId)
VALUES
(@FieldId, @TenantId, @CoverageDetailId, @FieldGroupCode, @FieldCode, @FieldLabel, @FieldValue, @FieldValueTypeCode, @UnitOfMeasureCode, @IsRequired, @IsRatingField, @IsScheduleField, @SortOrder, @CreatedByUserId);";
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { FieldId = id, request.TenantId, request.CoverageDetailId, request.FieldGroupCode, request.FieldCode, request.FieldLabel, request.FieldValue, request.FieldValueTypeCode, request.UnitOfMeasureCode, request.IsRequired, request.IsRatingField, request.IsScheduleField, request.SortOrder, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateFieldAsync(Guid fieldId, UpdatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCoverageDetailField
SET FieldGroupCode = @FieldGroupCode,
    FieldCode = @FieldCode,
    FieldLabel = @FieldLabel,
    FieldValue = @FieldValue,
    FieldValueTypeCode = @FieldValueTypeCode,
    UnitOfMeasureCode = @UnitOfMeasureCode,
    IsRequired = @IsRequired,
    IsRatingField = @IsRatingField,
    IsScheduleField = @IsScheduleField,
    SortOrder = @SortOrder,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE FieldId = @FieldId AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            FieldId = fieldId,
            request.TenantId,
            request.FieldGroupCode,
            request.FieldCode,
            request.FieldLabel,
            request.FieldValue,
            request.FieldValueTypeCode,
            request.UnitOfMeasureCode,
            request.IsRequired,
            request.IsRatingField,
            request.IsScheduleField,
            request.SortOrder,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteFieldAsync(Guid tenantId, Guid fieldId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCoverageDetailField
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE TenantId = @TenantId AND FieldId = @FieldId;";
        using var cn = await _cf.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, FieldId = fieldId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    private static async Task HydrateFieldsAsync(System.Data.IDbConnection cn, List<PolicyCoverageDetailDto> details, CancellationToken cancellationToken)
    {
        if (details.Count == 0)
        {
            return;
        }

        var fields = (await cn.QueryAsync<PolicyCoverageDetailFieldDto>(new CommandDefinition($@"
SELECT {FieldCols}
FROM Policy.PolicyCoverageDetailField
WHERE CoverageDetailId IN @Ids AND IsDeleted = 0
ORDER BY FieldGroupCode, SortOrder, FieldLabel;", new { Ids = details.Select(x => x.CoverageDetailId).ToArray() }, cancellationToken: cancellationToken))).AsList();

        var lookup = fields.GroupBy(x => x.CoverageDetailId).ToDictionary(x => x.Key, x => x.ToList());
        foreach (var detail in details)
        {
            detail.Fields = lookup.GetValueOrDefault(detail.CoverageDetailId) ?? [];
        }
    }
}
