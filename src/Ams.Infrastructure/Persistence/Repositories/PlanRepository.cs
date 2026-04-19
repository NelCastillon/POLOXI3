using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Plans;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PlanRepository : IPlanRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PlanRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = """
        PlanId, PlanCode, PlanName, BillingFrequency,
        BasePrice, IncludedUsers, IncludedStorageGb, IncludedApiCallsPerDay,
        IsEnterprise, IsActive, CreatedDateUtc, ModifiedDateUtc
        """;

    public async Task<PlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM Commercial.[Plan] WHERE PlanId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PlanDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PlanDto>> SearchAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            ;WITH Cte AS (
                SELECT {SelectColumns}
                FROM Commercial.[Plan]
                WHERE IsDeleted = 0
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR PlanCode LIKE '%' + @SearchTerm + '%'
                       OR PlanName LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY PlanCode ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Commercial.[Plan]
            WHERE IsDeleted = 0
              AND (@SearchTerm IS NULL OR @SearchTerm = ''
                   OR PlanCode LIKE '%' + @SearchTerm + '%'
                   OR PlanName LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PlanDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PlanDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Commercial.[Plan]
                (PlanId, PlanCode, PlanName, BillingFrequency,
                 BasePrice, IncludedUsers, IncludedStorageGb, IncludedApiCallsPerDay,
                 IsEnterprise, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@PlanId, @PlanCode, @PlanName, @BillingFrequency,
                 @BasePrice, @IncludedUsers, @IncludedStorageGb, @IncludedApiCallsPerDay,
                 @IsEnterprise, 1, SYSUTCDATETIME(), @CreatedByUserId, 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PlanId                 = id,
            request.PlanCode,
            request.PlanName,
            request.BillingFrequency,
            request.BasePrice,
            request.IncludedUsers,
            request.IncludedStorageGb,
            request.IncludedApiCallsPerDay,
            request.IsEnterprise,
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Commercial.[Plan] SET
                PlanName               = @PlanName,
                BillingFrequency       = @BillingFrequency,
                BasePrice              = @BasePrice,
                IncludedUsers          = @IncludedUsers,
                IncludedStorageGb      = @IncludedStorageGb,
                IncludedApiCallsPerDay = @IncludedApiCallsPerDay,
                IsEnterprise           = @IsEnterprise,
                ModifiedDateUtc        = SYSUTCDATETIME()
            WHERE PlanId = @PlanId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PlanId = id,
            request.PlanName,
            request.BillingFrequency,
            request.BasePrice,
            request.IncludedUsers,
            request.IncludedStorageGb,
            request.IncludedApiCallsPerDay,
            request.IsEnterprise,
        }, cancellationToken: cancellationToken));
    }

    public async Task CloneAsync(Guid id, string newPlanCode, string newPlanName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Commercial.[Plan]
                (PlanId, PlanCode, PlanName, BillingFrequency,
                 BasePrice, IncludedUsers, IncludedStorageGb, IncludedApiCallsPerDay,
                 IsEnterprise, IsActive, CreatedDateUtc, IsDeleted)
            SELECT NEWID(), @NewPlanCode, @NewPlanName, BillingFrequency,
                   BasePrice, IncludedUsers, IncludedStorageGb, IncludedApiCallsPerDay,
                   IsEnterprise, 0, SYSUTCDATETIME(), 0
            FROM Commercial.[Plan]
            WHERE PlanId = @PlanId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql,
            new { PlanId = id, NewPlanCode = newPlanCode, NewPlanName = newPlanName },
            cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Commercial.[Plan] SET IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE PlanId = @PlanId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PlanId = id, IsActive = isActive }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Commercial.[Plan] SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE PlanId = @PlanId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PlanId = id }, cancellationToken: cancellationToken));
    }
}
