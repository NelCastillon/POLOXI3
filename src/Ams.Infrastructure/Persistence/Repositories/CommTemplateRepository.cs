using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommTemplateRepository : ICommTemplateRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CommTemplateRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        TemplateId, TenantId, Name, Channel, Category, Language, Status,
        Subject, Body, IncludeOptOutFooter, TcpaNotice, UsageCount,
        CreatedDateUtc, ModifiedDateUtc AS UpdatedAt";

    public async Task<IReadOnlyList<CommTemplateDto>> GetByTenantAsync(Guid tenantId, string? channel = null, string? category = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM Comms.Template
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@Channel  IS NULL OR Channel  = @Channel)
  AND (@Category IS NULL OR Category = @Category)
  AND (@Status   IS NULL OR Status   = @Status)
ORDER BY Category, Name;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<CommTemplateDto>(new CommandDefinition(sql,
            new { TenantId = tenantId, Channel = channel, Category = category, Status = status },
            cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<CommTemplateDto?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM Comms.Template WHERE TemplateId = @TemplateId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CommTemplateDto>(new CommandDefinition(sql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateCommTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var id  = Guid.NewGuid();
        var sql = @"
INSERT INTO Comms.Template
    (TemplateId, TenantId, Name, Channel, Category, Language, Status,
     Subject, Body, IncludeOptOutFooter, TcpaNotice, UsageCount, IsDeleted, CreatedDateUtc, ModifiedDateUtc)
VALUES
    (@TemplateId, @TenantId, @Name, @Channel, @Category, @Language, @Status,
     @Subject, @Body, @IncludeOptOutFooter, @TcpaNotice, 0, 0, GETUTCDATE(), GETUTCDATE());";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TemplateId = id,
            request.TenantId,
            request.Name,
            request.Channel,
            request.Category,
            request.Language,
            request.Status,
            Subject = request.Subject,
            request.Body,
            request.IncludeOptOutFooter,
            request.TcpaNotice
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(UpdateCommTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.Template
SET Name = @Name, Channel = @Channel, Category = @Category, Language = @Language,
    Status = @Status, Subject = @Subject, Body = @Body,
    IncludeOptOutFooter = @IncludeOptOutFooter, TcpaNotice = @TcpaNotice,
    ModifiedDateUtc = GETUTCDATE()
WHERE TemplateId = @TemplateId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.TemplateId,
            request.Name,
            request.Channel,
            request.Category,
            request.Language,
            request.Status,
            Subject = request.Subject,
            request.Body,
            request.IncludeOptOutFooter,
            request.TcpaNotice
        }, cancellationToken: cancellationToken));
    }

    public async Task IncrementUsageAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.Template
SET UsageCount = UsageCount + 1, ModifiedDateUtc = GETUTCDATE()
WHERE TemplateId = @TemplateId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.Template SET IsDeleted = 1, ModifiedDateUtc = GETUTCDATE()
WHERE TemplateId = @TemplateId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }
}
