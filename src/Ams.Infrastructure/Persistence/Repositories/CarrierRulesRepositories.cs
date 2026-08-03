using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierRules;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MarketAccessRuleRepository : IMarketAccessRuleRepository
{
    private readonly ISqlConnectionFactory _cf;
    public MarketAccessRuleRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "MarketAccessRuleId, TenantId, RuleName, CarrierNaic, StateCode, LobCode, AccessLevel, Requirements, Priority, IsActive, CreatedDateUtc";
    public async Task<MarketAccessRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) { using var cn = await _cf.CreateOpenConnectionAsync(ct); return await cn.QuerySingleOrDefaultAsync<MarketAccessRuleDto>(new CommandDefinition($"SELECT {Cols} FROM Agency.MarketAccessRule WHERE MarketAccessRuleId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct)); }
    public async Task<PagedResult<MarketAccessRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) { var sql = RepositorySql.BuildPagedSearchSql("Agency.MarketAccessRule", Cols, "RuleName LIKE '%'+@SearchTerm+'%' OR StateCode LIKE '%'+@SearchTerm+'%' OR LobCode LIKE '%'+@SearchTerm+'%'", "Priority ASC, RuleName ASC"); using var cn = await _cf.CreateOpenConnectionAsync(ct); using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct)); return new() { Items = (await multi.ReadAsync<MarketAccessRuleDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize }; }
    public async Task<Guid> CreateAsync(CreateMarketAccessRuleRequest r, CancellationToken ct = default) { const string sql = "INSERT INTO Agency.MarketAccessRule (MarketAccessRuleId,TenantId,RuleName,CarrierNaic,StateCode,LobCode,AccessLevel,Requirements,Priority,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@RuleName,@CarrierNaic,@StateCode,@LobCode,@AccessLevel,@Requirements,@Priority,1,0,GETUTCDATE());"; var id = Guid.NewGuid(); using var cn = await _cf.CreateOpenConnectionAsync(ct); await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.RuleName, r.CarrierNaic, r.StateCode, r.LobCode, r.AccessLevel, r.Requirements, r.Priority }, cancellationToken: ct)); return id; }
    public async Task UpdateAsync(Guid id, UpdateMarketAccessRuleRequest r, CancellationToken ct = default) { const string sql = "UPDATE Agency.MarketAccessRule SET RuleName=@RuleName,CarrierNaic=@CarrierNaic,StateCode=@StateCode,LobCode=@LobCode,AccessLevel=@AccessLevel,Requirements=@Requirements,Priority=@Priority,IsActive=@IsActive,ModifiedDateUtc=GETUTCDATE() WHERE MarketAccessRuleId=@Id AND IsDeleted=0;"; using var cn = await _cf.CreateOpenConnectionAsync(ct); await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.RuleName, r.CarrierNaic, r.StateCode, r.LobCode, r.AccessLevel, r.Requirements, r.Priority, r.IsActive }, cancellationToken: ct)); }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default) { using var cn = await _cf.CreateOpenConnectionAsync(ct); await cn.ExecuteAsync(new CommandDefinition("UPDATE Agency.MarketAccessRule SET IsDeleted=1 WHERE MarketAccessRuleId=@Id;", new { Id = id }, cancellationToken: ct)); }
}

public sealed class CarrierDownloadMappingRepository : ICarrierDownloadMappingRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CarrierDownloadMappingRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "DownloadMappingId, TenantId, MappingCode, CarrierNaic, TransactionType, SourceField, TargetField, TransformRule, IsActive, SortOrder, CreatedDateUtc";
    public async Task<CarrierDownloadMappingDto?> GetByIdAsync(Guid id, CancellationToken ct = default) { using var cn = await _cf.CreateOpenConnectionAsync(ct); return await cn.QuerySingleOrDefaultAsync<CarrierDownloadMappingDto>(new CommandDefinition($"SELECT {Cols} FROM Agency.CarrierDownloadMapping WHERE DownloadMappingId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct)); }
    public async Task<PagedResult<CarrierDownloadMappingDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) { var sql = RepositorySql.BuildPagedSearchSql("Agency.CarrierDownloadMapping", Cols, "MappingCode LIKE '%'+@SearchTerm+'%' OR TransactionType LIKE '%'+@SearchTerm+'%' OR SourceField LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, MappingCode ASC"); using var cn = await _cf.CreateOpenConnectionAsync(ct); using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct)); return new() { Items = (await multi.ReadAsync<CarrierDownloadMappingDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize }; }
    public async Task<Guid> CreateAsync(CreateCarrierDownloadMappingRequest r, CancellationToken ct = default) { const string sql = "INSERT INTO Agency.CarrierDownloadMapping (DownloadMappingId,TenantId,MappingCode,CarrierNaic,TransactionType,SourceField,TargetField,TransformRule,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@MappingCode,@CarrierNaic,@TransactionType,@SourceField,@TargetField,@TransformRule,@SortOrder,1,0,GETUTCDATE());"; var id = Guid.NewGuid(); using var cn = await _cf.CreateOpenConnectionAsync(ct); await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.MappingCode, r.CarrierNaic, r.TransactionType, r.SourceField, r.TargetField, r.TransformRule, r.SortOrder }, cancellationToken: ct)); return id; }
    public async Task UpdateAsync(Guid id, UpdateCarrierDownloadMappingRequest r, CancellationToken ct = default) { const string sql = "UPDATE Agency.CarrierDownloadMapping SET MappingCode=@MappingCode,CarrierNaic=@CarrierNaic,TransactionType=@TransactionType,SourceField=@SourceField,TargetField=@TargetField,TransformRule=@TransformRule,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE DownloadMappingId=@Id AND IsDeleted=0;"; using var cn = await _cf.CreateOpenConnectionAsync(ct); await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.MappingCode, r.CarrierNaic, r.TransactionType, r.SourceField, r.TargetField, r.TransformRule, r.IsActive, r.SortOrder }, cancellationToken: ct)); }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default) { using var cn = await _cf.CreateOpenConnectionAsync(ct); await cn.ExecuteAsync(new CommandDefinition("UPDATE Agency.CarrierDownloadMapping SET IsDeleted=1 WHERE DownloadMappingId=@Id;", new { Id = id }, cancellationToken: ct)); }
}

public sealed class CarrierRuleCategoryRepository : ICarrierRuleCategoryRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CarrierRuleCategoryRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<IReadOnlyList<CarrierRuleCategoryDto>> GetActiveAsync(CancellationToken ct = default)
    {
        const string sql = @"
SELECT CarrierRuleCategoryId, RuleCategoryCode, DisplayName, Description, IconCssClass, SortOrder, IsActive
FROM Agency.CarrierRuleCategory
WHERE IsActive = 1 AND IsDeleted = 0
ORDER BY SortOrder ASC, DisplayName ASC;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return (await cn.QueryAsync<CarrierRuleCategoryDto>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }
}

public sealed class CarrierRuleLookupRepository : ICarrierRuleLookupRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CarrierRuleLookupRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<IReadOnlyList<CarrierRuleOptionDto>> GetOptionsAsync(Guid tenantId, string optionType, CancellationToken ct = default)
    {
        const string sql = @"SELECT CarrierRuleOptionId,TenantId,OptionType,OptionCode,DisplayName,OptionValue,Description,SortOrder
FROM Agency.CarrierRuleOption
WHERE TenantId=@TenantId AND OptionType=@OptionType AND IsActive=1 AND IsDeleted=0
ORDER BY SortOrder,DisplayName;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return (await cn.QueryAsync<CarrierRuleOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId, OptionType = optionType }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<CarrierProductCatalogDto>> GetProductsAsync(Guid tenantId, Guid? carrierId, Guid? lineOfBusinessId, CancellationToken ct = default)
    {
        const string sql = @"SELECT CarrierProductCatalogId,TenantId,CarrierId,LineOfBusinessId,ProductCode,ProductName,Description,SortOrder
FROM Agency.CarrierProductCatalog
WHERE TenantId=@TenantId AND IsActive=1 AND IsDeleted=0
  AND (@CarrierId IS NULL OR CarrierId=@CarrierId OR CarrierId IS NULL)
  AND (@LineOfBusinessId IS NULL OR LineOfBusinessId=@LineOfBusinessId OR LineOfBusinessId IS NULL)
ORDER BY SortOrder,ProductName;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return (await cn.QueryAsync<CarrierProductCatalogDto>(new CommandDefinition(sql, new { TenantId = tenantId, CarrierId = carrierId, LineOfBusinessId = lineOfBusinessId }, cancellationToken: ct))).AsList();
    }
}

public sealed class CarrierProductRuleRepository : ICarrierProductRuleRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CarrierProductRuleRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = @"CarrierProductRuleId, TenantId, CarrierId, CarrierName, CarrierNaic, CarrierProductCode, CarrierProductName, LineOfBusinessId, LineOfBusinessCode, StateCode, RuleCategoryCode, RuleCode, RuleName, RuleDescription, EffectiveDate, ExpirationDate, Priority, BillingType, MinimumDownPaymentPercent, MinimumDownPaymentAmount, MaximumInstallments, RequirePaymentBeforeBinding, AllowPremiumFinance, AllowAgencyBill, AllowDirectBill, AllowZeroDown, RequireSignedApplication, RequirePayment, RequireInspection, RequirePhotos, RequireLossRuns, AllowSameDayBind, MaximumAdvanceBindDays, AllowWeekendBinding, BindingTimeCutoff, RequireUnderwriterApproval, RequireACORD125, RequireACORD126, RequireACORD127, RequireStatementOfValues, RequireFinancialStatement, RequireSupplementalForm, NewBusinessRate, RenewalRate, BrokerFeeAllowed, MaximumBrokerFee, CommissionSchedule, CommissionPaymentMethod, ValidateVIN, ValidateFEIN, ValidateRoofAge, ValidateDriverAge, ValidatePayroll, ValidateSquareFootage, ValidateClaimsHistory, RulePayloadJson, Notes, IsActive, CreatedDateUtc, ModifiedDateUtc";

    public async Task<CarrierProductRuleDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<CarrierProductRuleDto>(new CommandDefinition($"SELECT {Cols} FROM Agency.CarrierProductRule WHERE TenantId=@TenantId AND CarrierProductRuleId=@Id AND IsDeleted=0;", new { TenantId = tenantId, Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<CarrierProductRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, string? categoryCode, bool? isActive, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
SELECT {0}
FROM Agency.CarrierProductRule
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@CategoryCode IS NULL OR RuleCategoryCode = @CategoryCode)
  AND (@IsActive IS NULL OR IsActive = @IsActive)
  AND (
      @SearchTerm = N''
      OR RuleName LIKE N'%' + @SearchTerm + N'%'
      OR RuleCode LIKE N'%' + @SearchTerm + N'%'
      OR RuleCategoryCode LIKE N'%' + @SearchTerm + N'%'
      OR CarrierProductName LIKE N'%' + @SearchTerm + N'%'
      OR CarrierName LIKE N'%' + @SearchTerm + N'%'
      OR CarrierNaic LIKE N'%' + @SearchTerm + N'%'
      OR LineOfBusinessCode LIKE N'%' + @SearchTerm + N'%'
      OR StateCode LIKE N'%' + @SearchTerm + N'%'
  )
ORDER BY Priority ASC, RuleCategoryCode ASC, RuleName ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Agency.CarrierProductRule
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@CategoryCode IS NULL OR RuleCategoryCode = @CategoryCode)
  AND (@IsActive IS NULL OR IsActive = @IsActive)
  AND (
      @SearchTerm = N''
      OR RuleName LIKE N'%' + @SearchTerm + N'%'
      OR RuleCode LIKE N'%' + @SearchTerm + N'%'
      OR RuleCategoryCode LIKE N'%' + @SearchTerm + N'%'
      OR CarrierProductName LIKE N'%' + @SearchTerm + N'%'
      OR CarrierName LIKE N'%' + @SearchTerm + N'%'
      OR CarrierNaic LIKE N'%' + @SearchTerm + N'%'
      OR LineOfBusinessCode LIKE N'%' + @SearchTerm + N'%'
      OR StateCode LIKE N'%' + @SearchTerm + N'%'
  );";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(string.Format(sql, Cols), new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm ?? string.Empty,
            CategoryCode = string.IsNullOrWhiteSpace(categoryCode) ? null : categoryCode,
            IsActive = isActive,
            Offset = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize = pageSize
        }, cancellationToken: ct));
        return new()
        {
            Items = (await multi.ReadAsync<CarrierProductRuleDto>()).AsList(),
            TotalCount = await multi.ReadSingleAsync<int>(),
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<Guid> CreateAsync(CreateCarrierProductRuleRequest r, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO Agency.CarrierProductRule
(CarrierProductRuleId,TenantId,CarrierId,CarrierName,CarrierNaic,CarrierProductCode,CarrierProductName,LineOfBusinessId,LineOfBusinessCode,StateCode,RuleCategoryCode,RuleCode,RuleName,RuleDescription,EffectiveDate,ExpirationDate,Priority,BillingType,MinimumDownPaymentPercent,MinimumDownPaymentAmount,MaximumInstallments,RequirePaymentBeforeBinding,AllowPremiumFinance,AllowAgencyBill,AllowDirectBill,AllowZeroDown,RequireSignedApplication,RequirePayment,RequireInspection,RequirePhotos,RequireLossRuns,AllowSameDayBind,MaximumAdvanceBindDays,AllowWeekendBinding,BindingTimeCutoff,RequireUnderwriterApproval,RequireACORD125,RequireACORD126,RequireACORD127,RequireStatementOfValues,RequireFinancialStatement,RequireSupplementalForm,NewBusinessRate,RenewalRate,BrokerFeeAllowed,MaximumBrokerFee,CommissionSchedule,CommissionPaymentMethod,ValidateVIN,ValidateFEIN,ValidateRoofAge,ValidateDriverAge,ValidatePayroll,ValidateSquareFootage,ValidateClaimsHistory,RulePayloadJson,Notes,IsActive,IsDeleted,CreatedDateUtc)
VALUES
(@Id,@TenantId,@CarrierId,@CarrierName,@CarrierNaic,@CarrierProductCode,@CarrierProductName,@LineOfBusinessId,@LineOfBusinessCode,@StateCode,@RuleCategoryCode,@RuleCode,@RuleName,@RuleDescription,@EffectiveDate,@ExpirationDate,@Priority,@BillingType,@MinimumDownPaymentPercent,@MinimumDownPaymentAmount,@MaximumInstallments,@RequirePaymentBeforeBinding,@AllowPremiumFinance,@AllowAgencyBill,@AllowDirectBill,@AllowZeroDown,@RequireSignedApplication,@RequirePayment,@RequireInspection,@RequirePhotos,@RequireLossRuns,@AllowSameDayBind,@MaximumAdvanceBindDays,@AllowWeekendBinding,@BindingTimeCutoff,@RequireUnderwriterApproval,@RequireACORD125,@RequireACORD126,@RequireACORD127,@RequireStatementOfValues,@RequireFinancialStatement,@RequireSupplementalForm,@NewBusinessRate,@RenewalRate,@BrokerFeeAllowed,@MaximumBrokerFee,@CommissionSchedule,@CommissionPaymentMethod,@ValidateVIN,@ValidateFEIN,@ValidateRoofAge,@ValidateDriverAge,@ValidatePayroll,@ValidateSquareFootage,@ValidateClaimsHistory,@RulePayloadJson,@Notes,1,0,SYSUTCDATETIME());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.CarrierId, r.CarrierName, r.CarrierNaic, r.CarrierProductCode, r.CarrierProductName, r.LineOfBusinessId, r.LineOfBusinessCode, StateCode = NormalizeState(r.StateCode), r.RuleCategoryCode, r.RuleCode, r.RuleName, r.RuleDescription, r.EffectiveDate, r.ExpirationDate, r.Priority, r.BillingType, r.MinimumDownPaymentPercent, r.MinimumDownPaymentAmount, r.MaximumInstallments, r.RequirePaymentBeforeBinding, r.AllowPremiumFinance, r.AllowAgencyBill, r.AllowDirectBill, r.AllowZeroDown, r.RequireSignedApplication, r.RequirePayment, r.RequireInspection, r.RequirePhotos, r.RequireLossRuns, r.AllowSameDayBind, r.MaximumAdvanceBindDays, r.AllowWeekendBinding, r.BindingTimeCutoff, r.RequireUnderwriterApproval, r.RequireACORD125, r.RequireACORD126, r.RequireACORD127, r.RequireStatementOfValues, r.RequireFinancialStatement, r.RequireSupplementalForm, r.NewBusinessRate, r.RenewalRate, r.BrokerFeeAllowed, r.MaximumBrokerFee, r.CommissionSchedule, r.CommissionPaymentMethod, r.ValidateVIN, r.ValidateFEIN, r.ValidateRoofAge, r.ValidateDriverAge, r.ValidatePayroll, r.ValidateSquareFootage, r.ValidateClaimsHistory, RulePayloadJson = string.IsNullOrWhiteSpace(r.RulePayloadJson) ? "{}" : r.RulePayloadJson, r.Notes }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid tenantId, Guid id, UpdateCarrierProductRuleRequest r, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Agency.CarrierProductRule
SET CarrierId=@CarrierId,CarrierName=@CarrierName,CarrierNaic=@CarrierNaic,CarrierProductCode=@CarrierProductCode,CarrierProductName=@CarrierProductName,LineOfBusinessId=@LineOfBusinessId,LineOfBusinessCode=@LineOfBusinessCode,StateCode=@StateCode,RuleCategoryCode=@RuleCategoryCode,RuleCode=@RuleCode,RuleName=@RuleName,RuleDescription=@RuleDescription,EffectiveDate=@EffectiveDate,ExpirationDate=@ExpirationDate,Priority=@Priority,BillingType=@BillingType,MinimumDownPaymentPercent=@MinimumDownPaymentPercent,MinimumDownPaymentAmount=@MinimumDownPaymentAmount,MaximumInstallments=@MaximumInstallments,RequirePaymentBeforeBinding=@RequirePaymentBeforeBinding,AllowPremiumFinance=@AllowPremiumFinance,AllowAgencyBill=@AllowAgencyBill,AllowDirectBill=@AllowDirectBill,AllowZeroDown=@AllowZeroDown,RequireSignedApplication=@RequireSignedApplication,RequirePayment=@RequirePayment,RequireInspection=@RequireInspection,RequirePhotos=@RequirePhotos,RequireLossRuns=@RequireLossRuns,AllowSameDayBind=@AllowSameDayBind,MaximumAdvanceBindDays=@MaximumAdvanceBindDays,AllowWeekendBinding=@AllowWeekendBinding,BindingTimeCutoff=@BindingTimeCutoff,RequireUnderwriterApproval=@RequireUnderwriterApproval,RequireACORD125=@RequireACORD125,RequireACORD126=@RequireACORD126,RequireACORD127=@RequireACORD127,RequireStatementOfValues=@RequireStatementOfValues,RequireFinancialStatement=@RequireFinancialStatement,RequireSupplementalForm=@RequireSupplementalForm,NewBusinessRate=@NewBusinessRate,RenewalRate=@RenewalRate,BrokerFeeAllowed=@BrokerFeeAllowed,MaximumBrokerFee=@MaximumBrokerFee,CommissionSchedule=@CommissionSchedule,CommissionPaymentMethod=@CommissionPaymentMethod,ValidateVIN=@ValidateVIN,ValidateFEIN=@ValidateFEIN,ValidateRoofAge=@ValidateRoofAge,ValidateDriverAge=@ValidateDriverAge,ValidatePayroll=@ValidatePayroll,ValidateSquareFootage=@ValidateSquareFootage,ValidateClaimsHistory=@ValidateClaimsHistory,RulePayloadJson=@RulePayloadJson,Notes=@Notes,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND CarrierProductRuleId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, r.CarrierId, r.CarrierName, r.CarrierNaic, r.CarrierProductCode, r.CarrierProductName, r.LineOfBusinessId, r.LineOfBusinessCode, StateCode = NormalizeState(r.StateCode), r.RuleCategoryCode, r.RuleCode, r.RuleName, r.RuleDescription, r.EffectiveDate, r.ExpirationDate, r.Priority, r.BillingType, r.MinimumDownPaymentPercent, r.MinimumDownPaymentAmount, r.MaximumInstallments, r.RequirePaymentBeforeBinding, r.AllowPremiumFinance, r.AllowAgencyBill, r.AllowDirectBill, r.AllowZeroDown, r.RequireSignedApplication, r.RequirePayment, r.RequireInspection, r.RequirePhotos, r.RequireLossRuns, r.AllowSameDayBind, r.MaximumAdvanceBindDays, r.AllowWeekendBinding, r.BindingTimeCutoff, r.RequireUnderwriterApproval, r.RequireACORD125, r.RequireACORD126, r.RequireACORD127, r.RequireStatementOfValues, r.RequireFinancialStatement, r.RequireSupplementalForm, r.NewBusinessRate, r.RenewalRate, r.BrokerFeeAllowed, r.MaximumBrokerFee, r.CommissionSchedule, r.CommissionPaymentMethod, r.ValidateVIN, r.ValidateFEIN, r.ValidateRoofAge, r.ValidateDriverAge, r.ValidatePayroll, r.ValidateSquareFootage, r.ValidateClaimsHistory, RulePayloadJson = string.IsNullOrWhiteSpace(r.RulePayloadJson) ? "{}" : r.RulePayloadJson, r.Notes, r.IsActive }, cancellationToken: ct));
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Carrier product rule '{id}' was not found for tenant '{tenantId}'.");
        }
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var affected = await cn.ExecuteAsync(new CommandDefinition("UPDATE Agency.CarrierProductRule SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND CarrierProductRuleId=@Id AND IsDeleted=0;", new { TenantId = tenantId, Id = id }, cancellationToken: ct));
        if (affected == 0)
        {
            throw new KeyNotFoundException($"Carrier product rule '{id}' was not found for tenant '{tenantId}'.");
        }
    }

    private static string? NormalizeState(string? stateCode) => string.IsNullOrWhiteSpace(stateCode) ? null : stateCode.Trim().ToUpperInvariant();
}
