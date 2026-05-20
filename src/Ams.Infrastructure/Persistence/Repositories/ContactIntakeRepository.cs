using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.ContactIntake;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ContactIntakeRepository : IContactIntakeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ContactIntakeRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ContactDemoSubmissionResult> CreateDemoRequestAsync(CreateContactDemoRequest request, ContactDemoRequestContext context, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid();
        var requestNumber = $"DEMO-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";

        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT INTO Marketing.ContactDemoRequest
(
    RequestId, RequestNumber, FirstName, LastName, WorkEmail, Phone, Title, AgencyName,
    AgencySize, Branches, BusinessLines, CurrentSystem, Timeline, Budget, Message,
    ConsentToContact, StatusCode, SourceCode, RemoteIpAddress, UserAgent, Referrer, Origin,
    CreatedDateUtc, IsDeleted
)
VALUES
(
    @RequestId, @RequestNumber, @FirstName, @LastName, @WorkEmail, @Phone, @Title, @AgencyName,
    @AgencySize, @Branches, @BusinessLines, @CurrentSystem, @Timeline, @Budget, @Message,
    @ConsentToContact, 'New', 'Website', @RemoteIpAddress, @UserAgent, @Referrer, @Origin,
    SYSUTCDATETIME(), 0
);

INSERT INTO Marketing.ContactDemoRequestPriority (RequestId, PriorityCode, CreatedDateUtc)
SELECT @RequestId, [value], SYSUTCDATETIME()
FROM STRING_SPLIT(@PrioritiesCsv, ',')
WHERE NULLIF(LTRIM(RTRIM([value])), '') IS NOT NULL;

COMMIT TRANSACTION;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RequestId = requestId,
            RequestNumber = requestNumber,
            request.FirstName,
            request.LastName,
            request.WorkEmail,
            request.Phone,
            request.Title,
            request.AgencyName,
            request.AgencySize,
            request.Branches,
            request.BusinessLines,
            request.CurrentSystem,
            request.Timeline,
            request.Budget,
            request.Message,
            request.ConsentToContact,
            context.RemoteIpAddress,
            context.UserAgent,
            context.Referrer,
            context.Origin,
            PrioritiesCsv = string.Join(',', request.Priorities)
        }, cancellationToken: cancellationToken));

        return new ContactDemoSubmissionResult
        {
            RequestId = requestId,
            RequestNumber = requestNumber,
            Message = "Your enterprise consultation request was received."
        };
    }

    public async Task<IReadOnlyList<ContactIntakeOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT OptionType, Code, Label, SortOrder
FROM Marketing.ContactIntakeOption
WHERE IsActive = 1
ORDER BY OptionType, SortOrder, Label;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<ContactIntakeOptionDto>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
