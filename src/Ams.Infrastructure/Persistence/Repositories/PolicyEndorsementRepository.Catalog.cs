using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class PolicyEndorsementRepository
{
    public async Task<PolicyEndorsementCatalogDto> GetCatalogAsync(Guid tenantId, string? lineOfBusinessCode = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT EndorsementTypeId,TenantId,TypeCode,TypeName,Description,IsActive,SortOrder
FROM Policy.EndorsementType
WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1
  AND (@LineOfBusinessCode IS NULL OR EXISTS
      (SELECT 1 FROM Policy.EndorsementTypeLineOfBusiness lob
       WHERE lob.TenantId=Policy.EndorsementType.TenantId AND lob.EndorsementTypeId=Policy.EndorsementType.EndorsementTypeId
         AND lob.IsDeleted=0 AND lob.IsActive=1 AND (lob.LineOfBusinessCode=N'*' OR lob.LineOfBusinessCode=@LineOfBusinessCode)))
ORDER BY SortOrder,TypeName;
SELECT profile.* FROM Policy.EndorsementTypeProfile profile
JOIN Policy.EndorsementType type ON type.TenantId=profile.TenantId AND type.EndorsementTypeId=profile.EndorsementTypeId
WHERE profile.TenantId=@TenantId AND profile.IsDeleted=0 AND profile.IsActive=1 AND type.IsDeleted=0 AND type.IsActive=1;
SELECT lob.* FROM Policy.EndorsementTypeLineOfBusiness lob
JOIN Policy.EndorsementType type ON type.TenantId=lob.TenantId AND type.EndorsementTypeId=lob.EndorsementTypeId
WHERE lob.TenantId=@TenantId AND lob.IsDeleted=0 AND lob.IsActive=1 AND type.IsDeleted=0 AND type.IsActive=1;
SELECT requirement.* FROM Policy.EndorsementTypeDocumentRequirement requirement
JOIN Policy.EndorsementType type ON type.TenantId=requirement.TenantId AND type.EndorsementTypeId=requirement.EndorsementTypeId
WHERE requirement.TenantId=@TenantId AND requirement.IsDeleted=0 AND requirement.IsActive=1 AND type.IsDeleted=0 AND type.IsActive=1;
SELECT workflowRule.* FROM Policy.EndorsementTypeWorkflowRule workflowRule
JOIN Policy.EndorsementType type ON type.TenantId=workflowRule.TenantId AND type.EndorsementTypeId=workflowRule.EndorsementTypeId
WHERE workflowRule.TenantId=@TenantId AND workflowRule.IsDeleted=0 AND workflowRule.IsActive=1 AND type.IsDeleted=0 AND type.IsActive=1;
SELECT method.* FROM Policy.EndorsementTypeCarrierMethod method
JOIN Policy.EndorsementType type ON type.TenantId=method.TenantId AND type.EndorsementTypeId=method.EndorsementTypeId
WHERE method.TenantId=@TenantId AND method.IsDeleted=0 AND method.IsActive=1 AND type.IsDeleted=0 AND type.IsActive=1;
SELECT OptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder
FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1 ORDER BY OptionGroupCode,SortOrder,DisplayName;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            LineOfBusinessCode = string.IsNullOrWhiteSpace(lineOfBusinessCode) ? null : lineOfBusinessCode.Trim()
        }, cancellationToken: cancellationToken));
        var types = (await multi.ReadAsync<PolicyEndorsementTypeCatalogDto>()).AsList();
        var profiles = (await multi.ReadAsync<PolicyEndorsementTypeProfileDto>()).ToDictionary(x => x.EndorsementTypeId);
        var lines = (await multi.ReadAsync<PolicyEndorsementTypeLineOfBusinessDto>()).ToLookup(x => x.EndorsementTypeId);
        var requirements = (await multi.ReadAsync<PolicyEndorsementTypeDocumentRequirementDto>()).ToLookup(x => x.EndorsementTypeId);
        var rules = (await multi.ReadAsync<PolicyEndorsementTypeWorkflowRuleDto>()).ToLookup(x => x.EndorsementTypeId);
        var methods = (await multi.ReadAsync<PolicyEndorsementTypeCarrierMethodDto>()).ToLookup(x => x.EndorsementTypeId);
        var options = (await multi.ReadAsync<PolicyEndorsementOptionDto>()).AsList();
        foreach (var type in types)
        {
            type.Profile = profiles.GetValueOrDefault(type.EndorsementTypeId);
            type.LinesOfBusiness = lines[type.EndorsementTypeId].OrderBy(x => x.SortOrder).ToList();
            type.DocumentRequirements = requirements[type.EndorsementTypeId].OrderBy(x => x.SortOrder).ToList();
            type.WorkflowRules = rules[type.EndorsementTypeId].OrderBy(x => x.SortOrder).ToList();
            type.CarrierMethods = methods[type.EndorsementTypeId].OrderBy(x => x.SortOrder).ToList();
        }
        return new PolicyEndorsementCatalogDto { Types = types, Options = options };
    }

    public async Task<PolicyEndorsementTypeCatalogDto?> GetTypeCatalogAsync(Guid tenantId, string typeCode, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(tenantId, null, cancellationToken);
        return catalog.Types.SingleOrDefault(x => string.Equals(x.TypeCode, typeCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpdateTypeProfileAsync(Guid endorsementTypeId, UpdatePolicyEndorsementTypeProfileRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE Policy.EndorsementTypeProfile
SET CategoryCode=@CategoryCode,DefaultOperationCode=@DefaultOperationCode,PremiumImpactCode=@PremiumImpactCode,
    BillingImpactCode=@BillingImpactCode,CommissionImpactCode=@CommissionImpactCode,AuthorityCode=@AuthorityCode,
    ApprovalLevelCode=@ApprovalLevelCode,CarrierMethodCode=@CarrierMethodCode,DocumentDeliveryCode=@DocumentDeliveryCode,
    RequiresCarrierApproval=@RequiresCarrierApproval,RequiresUnderwritingReview=@RequiresUnderwritingReview,
    RequiresSignedRequest=@RequiresSignedRequest,RequiresClientAuthorization=@RequiresClientAuthorization,
    RequiresCertificateReview=@RequiresCertificateReview,RequiresBrokerOfRecord=@RequiresBrokerOfRecord,
    RequiresAccountingWork=@RequiresAccountingWork,RequiresCommissionWork=@RequiresCommissionWork,
    RequiresDocumentWork=@RequiresDocumentWork,RequiresPolicyVersion=@RequiresPolicyVersion,SupportsBackdate=@SupportsBackdate,
    SupportsReversal=@SupportsReversal,IsHighRisk=@IsHighRisk,IsPremiumBearing=@IsPremiumBearing,
    IsCertificateRelated=@IsCertificateRelated,IsActive=@IsActive,SortOrder=@SortOrder,
    ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId
WHERE TenantId=@TenantId AND EndorsementTypeId=@EndorsementTypeId AND IsDeleted=0 AND RowVersion=@RowVersion;
IF @@ROWCOUNT<>1 THROW 52601,N'The endorsement type profile was changed or does not exist in the tenant.',1;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementTypeId = endorsementTypeId,
            request.TenantId,
            request.CategoryCode,
            request.DefaultOperationCode,
            request.PremiumImpactCode,
            request.BillingImpactCode,
            request.CommissionImpactCode,
            request.AuthorityCode,
            request.ApprovalLevelCode,
            request.CarrierMethodCode,
            request.DocumentDeliveryCode,
            request.RequiresCarrierApproval,
            request.RequiresUnderwritingReview,
            request.RequiresSignedRequest,
            request.RequiresClientAuthorization,
            request.RequiresCertificateReview,
            request.RequiresBrokerOfRecord,
            request.RequiresAccountingWork,
            request.RequiresCommissionWork,
            request.RequiresDocumentWork,
            request.RequiresPolicyVersion,
            request.SupportsBackdate,
            request.SupportsReversal,
            request.IsHighRisk,
            request.IsPremiumBearing,
            request.IsCertificateRelated,
            request.IsActive,
            request.SortOrder,
            request.RowVersion,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task ReplaceTypeConfigurationAsync(Guid endorsementTypeId, ReplacePolicyEndorsementTypeConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS(SELECT 1 FROM Policy.EndorsementType WHERE TenantId=@TenantId AND EndorsementTypeId=@EndorsementTypeId AND IsDeleted=0)
    THROW 52602,N'The endorsement type does not exist in the tenant.',1;
UPDATE Policy.EndorsementTypeLineOfBusiness SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND EndorsementTypeId=@EndorsementTypeId AND IsDeleted=0;
UPDATE Policy.EndorsementTypeDocumentRequirement SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND EndorsementTypeId=@EndorsementTypeId AND IsDeleted=0;
UPDATE Policy.EndorsementTypeWorkflowRule SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND EndorsementTypeId=@EndorsementTypeId AND IsDeleted=0;
UPDATE Policy.EndorsementTypeCarrierMethod SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND EndorsementTypeId=@EndorsementTypeId AND IsDeleted=0;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new { request.TenantId, EndorsementTypeId = endorsementTypeId, request.ModifiedByUserId }, transaction, cancellationToken: cancellationToken));
            await InsertConfigurationRowsAsync(connection, transaction, endorsementTypeId, request, cancellationToken);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task InsertConfigurationRowsAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Guid endorsementTypeId, ReplacePolicyEndorsementTypeConfigurationRequest request, CancellationToken cancellationToken)
    {
        const string lobSql = """INSERT Policy.EndorsementTypeLineOfBusiness(EndorsementTypeLineOfBusinessId,TenantId,EndorsementTypeId,LineOfBusinessCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@EndorsementTypeId,@LineOfBusinessCode,@IsDefault,@IsActive,@SortOrder,SYSUTCDATETIME(),@ModifiedByUserId,0);""";
        const string documentSql = """INSERT Policy.EndorsementTypeDocumentRequirement(EndorsementTypeDocumentRequirementId,TenantId,EndorsementTypeId,RequirementCode,DocumentGroupCode,DocumentKindCode,AcordFormNumber,IsRequired,AppliesWhenJson,IsActive,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@EndorsementTypeId,@RequirementCode,@DocumentGroupCode,@DocumentKindCode,@AcordFormNumber,@IsRequired,@AppliesWhenJson,@IsActive,@SortOrder,SYSUTCDATETIME(),@ModifiedByUserId,0);""";
        const string ruleSql = """INSERT Policy.EndorsementTypeWorkflowRule(EndorsementTypeWorkflowRuleId,TenantId,EndorsementTypeId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierDispatch,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresCertificateReview,RequiresPolicyVersion,RuleJson,IsActive,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@EndorsementTypeId,@FromStatusCode,@ToStatusCode,@RequiredPermissionCode,@RequiresApproval,@RequiresCarrierDispatch,@RequiresAccountingWork,@RequiresCommissionWork,@RequiresDocumentWork,@RequiresCertificateReview,@RequiresPolicyVersion,@RuleJson,@IsActive,@SortOrder,SYSUTCDATETIME(),@ModifiedByUserId,0);""";
        const string carrierSql = """INSERT Policy.EndorsementTypeCarrierMethod(EndorsementTypeCarrierMethodId,TenantId,EndorsementTypeId,CarrierId,LineOfBusinessCode,CarrierMethodCode,CarrierConfigurationId,PortalInstructions,EmailTemplateCode,PayloadTemplateCode,IsDefault,IsActive,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@EndorsementTypeId,@CarrierId,@LineOfBusinessCode,@CarrierMethodCode,@CarrierConfigurationId,@PortalInstructions,@EmailTemplateCode,@PayloadTemplateCode,@IsDefault,@IsActive,@SortOrder,SYSUTCDATETIME(),@ModifiedByUserId,0);""";
        foreach (var item in request.LinesOfBusiness) await connection.ExecuteAsync(new CommandDefinition(lobSql, new { request.TenantId, EndorsementTypeId = endorsementTypeId, item.LineOfBusinessCode, item.IsDefault, item.IsActive, item.SortOrder, request.ModifiedByUserId }, transaction, cancellationToken: cancellationToken));
        foreach (var item in request.DocumentRequirements) await connection.ExecuteAsync(new CommandDefinition(documentSql, new { request.TenantId, EndorsementTypeId = endorsementTypeId, item.RequirementCode, item.DocumentGroupCode, item.DocumentKindCode, item.AcordFormNumber, item.IsRequired, item.AppliesWhenJson, item.IsActive, item.SortOrder, request.ModifiedByUserId }, transaction, cancellationToken: cancellationToken));
        foreach (var item in request.WorkflowRules) await connection.ExecuteAsync(new CommandDefinition(ruleSql, new { request.TenantId, EndorsementTypeId = endorsementTypeId, item.FromStatusCode, item.ToStatusCode, item.RequiredPermissionCode, item.RequiresApproval, item.RequiresCarrierDispatch, item.RequiresAccountingWork, item.RequiresCommissionWork, item.RequiresDocumentWork, item.RequiresCertificateReview, item.RequiresPolicyVersion, item.RuleJson, item.IsActive, item.SortOrder, request.ModifiedByUserId }, transaction, cancellationToken: cancellationToken));
        foreach (var item in request.CarrierMethods) await connection.ExecuteAsync(new CommandDefinition(carrierSql, new { request.TenantId, EndorsementTypeId = endorsementTypeId, item.CarrierId, item.LineOfBusinessCode, item.CarrierMethodCode, item.CarrierConfigurationId, item.PortalInstructions, item.EmailTemplateCode, item.PayloadTemplateCode, item.IsDefault, item.IsActive, item.SortOrder, request.ModifiedByUserId }, transaction, cancellationToken: cancellationToken));
    }
}
