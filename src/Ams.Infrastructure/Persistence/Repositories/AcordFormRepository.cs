using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AcordFormRepository : IAcordFormRepository
{
    private const string SelectColumns = @"
        AcordFormId, TenantId, FormNumber, FormName, LineOfBusiness, Edition, Status,
        PolicyNumber, AiPrefilled, PrefillFieldCount, PrefillConfidence, OwnerName,
        Description, LastModifiedDateUtc, CreatedDateUtc";

    private readonly ISqlConnectionFactory _connectionFactory;

    public AcordFormRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<AcordFormDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {SelectColumns}
FROM DMS.AcordForm
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY FormNumber, FormName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<AcordFormDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<AcordFormDto?> GetByIdAsync(Guid acordFormId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {SelectColumns}
FROM DMS.AcordForm
WHERE AcordFormId = @AcordFormId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AcordFormDto>(new CommandDefinition(sql, new { AcordFormId = acordFormId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateAcordFormRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO DMS.AcordForm
    (AcordFormId, TenantId, FormNumber, FormName, LineOfBusiness, Edition, Status, PolicyNumber, AiPrefilled, PrefillFieldCount, PrefillConfidence, OwnerName, Description, LastModifiedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@AcordFormId, @TenantId, @FormNumber, @FormName, @LineOfBusiness, @Edition, @Status, @PolicyNumber, @AiPrefilled, @PrefillFieldCount, @PrefillConfidence, @OwnerName, @Description, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AcordFormId = id,
            request.TenantId,
            request.FormNumber,
            request.FormName,
            request.LineOfBusiness,
            request.Edition,
            request.Status,
            request.PolicyNumber,
            request.AiPrefilled,
            request.PrefillFieldCount,
            request.PrefillConfidence,
            request.OwnerName,
            request.Description,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateStatusAsync(UpdateAcordFormStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.AcordForm
SET Status = @Status,
    LastModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AcordFormId = @AcordFormId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task PrefillAsync(PrefillAcordFormRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.AcordForm
SET PolicyNumber = COALESCE(@PolicyNumber, PolicyNumber),
    AiPrefilled = 1,
    PrefillFieldCount = @PrefillFieldCount,
    PrefillConfidence = @PrefillConfidence,
    Status = CASE WHEN Status = N'Blank' THEN N'In Progress' ELSE Status END,
    LastModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AcordFormId = @AcordFormId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }
}
