using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Plans;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PlanSubEntityRepository : IPlanSubEntityRepository
{
    private readonly ISqlConnectionFactory _cf;
    public PlanSubEntityRepository(ISqlConnectionFactory cf) => _cf = cf;

    // ── Features ─────────────────────────────────────────────
    public async Task<IReadOnlyList<PlanFeatureDto>> GetFeaturesAsync(Guid planId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT PlanFeatureId, PlanId, FeatureCode, FeatureName, IsIncluded, Notes, CreatedDateUtc
            FROM Commercial.PlanFeature
            WHERE PlanId = @PlanId AND IsDeleted = 0
            ORDER BY FeatureCode;
            """;
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var rows = await cn.QueryAsync<PlanFeatureDto>(new CommandDefinition(sql, new { PlanId = planId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> AddFeatureAsync(AddPlanFeatureRequest r, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Commercial.PlanFeature (PlanFeatureId, PlanId, FeatureCode, FeatureName, IsIncluded, Notes, CreatedDateUtc, IsDeleted)
            VALUES (@Id, @PlanId, @FeatureCode, @FeatureName, @IsIncluded, @Notes, SYSUTCDATETIME(), 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.PlanId, r.FeatureCode, r.FeatureName, r.IsIncluded, r.Notes }, cancellationToken: ct));
        return id;
    }

    public async Task RemoveFeatureAsync(Guid planFeatureId, CancellationToken ct = default)
    {
        const string sql = "UPDATE Commercial.PlanFeature SET IsDeleted = 1 WHERE PlanFeatureId = @Id;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = planFeatureId }, cancellationToken: ct));
    }

    // ── Limits ───────────────────────────────────────────────
    public async Task<IReadOnlyList<PlanLimitDto>> GetLimitsAsync(Guid planId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT PlanLimitId, PlanId, MetricTypeCode, LimitValue, LimitUnit, PeriodCode, IsEnforced, Notes, CreatedDateUtc
            FROM Commercial.PlanLimit
            WHERE PlanId = @PlanId AND IsDeleted = 0
            ORDER BY MetricTypeCode;
            """;
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var rows = await cn.QueryAsync<PlanLimitDto>(new CommandDefinition(sql, new { PlanId = planId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> AddLimitAsync(AddPlanLimitRequest r, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Commercial.PlanLimit (PlanLimitId, PlanId, MetricTypeCode, LimitValue, LimitUnit, PeriodCode, IsEnforced, Notes, CreatedDateUtc, IsDeleted)
            VALUES (@Id, @PlanId, @MetricTypeCode, @LimitValue, @LimitUnit, @PeriodCode, @IsEnforced, @Notes, SYSUTCDATETIME(), 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.PlanId, r.MetricTypeCode, r.LimitValue, r.LimitUnit, r.PeriodCode, r.IsEnforced, r.Notes }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateLimitAsync(UpdatePlanLimitRequest r, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Commercial.PlanLimit SET LimitValue = @LimitValue, IsEnforced = @IsEnforced, Notes = @Notes
            WHERE PlanLimitId = @PlanLimitId AND IsDeleted = 0;
            """;
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { r.PlanLimitId, r.LimitValue, r.IsEnforced, r.Notes }, cancellationToken: ct));
    }

    public async Task RemoveLimitAsync(Guid planLimitId, CancellationToken ct = default)
    {
        const string sql = "UPDATE Commercial.PlanLimit SET IsDeleted = 1 WHERE PlanLimitId = @Id;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = planLimitId }, cancellationToken: ct));
    }

    // ── Add-Ons ──────────────────────────────────────────────
    public async Task<IReadOnlyList<PlanAddOnDto>> GetAddOnsAsync(Guid planId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT PlanAddOnId, PlanId, AddOnCode, AddOnName, Price, BillingFrequency, Description, IsActive, CreatedDateUtc
            FROM Commercial.PlanAddOn
            WHERE PlanId = @PlanId AND IsDeleted = 0
            ORDER BY AddOnCode;
            """;
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var rows = await cn.QueryAsync<PlanAddOnDto>(new CommandDefinition(sql, new { PlanId = planId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> AddAddOnAsync(AddPlanAddOnRequest r, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Commercial.PlanAddOn (PlanAddOnId, PlanId, AddOnCode, AddOnName, Price, BillingFrequency, Description, IsActive, CreatedDateUtc, IsDeleted)
            VALUES (@Id, @PlanId, @AddOnCode, @AddOnName, @Price, @BillingFrequency, @Description, 1, SYSUTCDATETIME(), 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.PlanId, r.AddOnCode, r.AddOnName, r.Price, r.BillingFrequency, r.Description }, cancellationToken: ct));
        return id;
    }

    public async Task RemoveAddOnAsync(Guid planAddOnId, CancellationToken ct = default)
    {
        const string sql = "UPDATE Commercial.PlanAddOn SET IsDeleted = 1 WHERE PlanAddOnId = @Id;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = planAddOnId }, cancellationToken: ct));
    }
}
