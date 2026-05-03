using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public MessageRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string ThreadColumns = @"
        t.ThreadId, t.TenantId, t.AccountName, t.AccountId,
        t.ContactName, t.ContactEmail, t.ContactPhone,
        t.Channel, t.Subject, t.BodyPreview, t.Status, t.Priority,
        t.AssignedTo, t.Producer, t.Branch,
        t.IsRead, t.IsEscalated, t.OptedOut, t.MessageCount,
        t.LastActivityAt, t.Sentiment, t.CsrOwner, t.AiSummary";

    public async Task<IReadOnlyList<MessageThreadDto>> GetThreadsAsync(GetThreadsRequest request, CancellationToken cancellationToken = default)
    {
        var channel = NormalizeFilter(request.Channel);
        var status = NormalizeFilter(request.Status);
        var assignedTo = NormalizeFilter(request.AssignedTo);
        var searchTerm = NormalizeFilter(request.SearchTerm);

        var sql = $@"
SELECT {ThreadColumns}
FROM Comms.MessageThread t
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
  AND (@Channel IS NULL OR t.Channel = @Channel)
  AND (@Status IS NULL OR t.Status = @Status)
  AND (@AssignedTo IS NULL OR t.AssignedTo = @AssignedTo)
  AND (@SearchTerm IS NULL OR t.AccountName LIKE '%' + @SearchTerm + '%'
       OR t.Subject LIKE '%' + @SearchTerm + '%'
       OR t.ContactName LIKE '%' + @SearchTerm + '%')
ORDER BY t.LastActivityAt DESC;

SELECT m.MessageId, m.ThreadId, m.SenderName, m.Channel, m.Direction,
       m.Body, m.SentAt, m.DeliveryStatus, m.IsAutomated
FROM Comms.ThreadMessage m
INNER JOIN Comms.MessageThread t ON t.ThreadId = m.ThreadId
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
ORDER BY m.SentAt ASC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { request.TenantId, Channel = channel, Status = status, AssignedTo = assignedTo, SearchTerm = searchTerm },
            cancellationToken: cancellationToken));

        var threads = (await multi.ReadAsync<MessageThreadDto>()).AsList();
        var messages = (await multi.ReadAsync<ThreadMessageDto>()).AsList();

        var lookup = messages.GroupBy(m => m.ThreadId)
                             .ToDictionary(g => g.Key, g => (IReadOnlyList<ThreadMessageDto>)g.ToList());

        return threads.Select(t => new MessageThreadDto
        {
            ThreadId       = t.ThreadId,
            TenantId       = t.TenantId,
            AccountName    = t.AccountName,
            AccountId      = t.AccountId,
            ContactName    = t.ContactName,
            ContactEmail   = t.ContactEmail,
            ContactPhone   = t.ContactPhone,
            Channel        = t.Channel,
            Subject        = t.Subject,
            BodyPreview    = t.BodyPreview,
            Status         = t.Status,
            Priority       = t.Priority,
            AssignedTo     = t.AssignedTo,
            Producer       = t.Producer,
            Branch         = t.Branch,
            IsRead         = t.IsRead,
            IsEscalated    = t.IsEscalated,
            OptedOut       = t.OptedOut,
            MessageCount   = t.MessageCount,
            LastActivityAt = t.LastActivityAt,
            Sentiment      = t.Sentiment,
            CsrOwner       = t.CsrOwner,
            AiSummary      = t.AiSummary,
            Messages       = lookup.GetValueOrDefault(t.ThreadId, [])
        }).ToList();
    }

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public async Task<MessageThreadDto?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {ThreadColumns} FROM Comms.MessageThread t WHERE t.ThreadId = @ThreadId AND t.IsDeleted = 0;

SELECT m.MessageId, m.ThreadId, m.SenderName, m.Channel, m.Direction,
       m.Body, m.SentAt, m.DeliveryStatus, m.IsAutomated
FROM Comms.ThreadMessage m WHERE m.ThreadId = @ThreadId ORDER BY m.SentAt ASC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ThreadId = threadId }, cancellationToken: cancellationToken));

        var thread = await multi.ReadSingleOrDefaultAsync<MessageThreadDto>();
        if (thread is null) return null;
        var messages = (await multi.ReadAsync<ThreadMessageDto>()).AsList();
        return new MessageThreadDto
        {
            ThreadId       = thread.ThreadId,
            TenantId       = thread.TenantId,
            AccountName    = thread.AccountName,
            AccountId      = thread.AccountId,
            ContactName    = thread.ContactName,
            ContactEmail   = thread.ContactEmail,
            ContactPhone   = thread.ContactPhone,
            Channel        = thread.Channel,
            Subject        = thread.Subject,
            BodyPreview    = thread.BodyPreview,
            Status         = thread.Status,
            Priority       = thread.Priority,
            AssignedTo     = thread.AssignedTo,
            Producer       = thread.Producer,
            Branch         = thread.Branch,
            IsRead         = thread.IsRead,
            IsEscalated    = thread.IsEscalated,
            OptedOut       = thread.OptedOut,
            MessageCount   = thread.MessageCount,
            LastActivityAt = thread.LastActivityAt,
            Sentiment      = thread.Sentiment,
            CsrOwner       = thread.CsrOwner,
            AiSummary      = thread.AiSummary,
            Messages       = messages
        };
    }

    public async Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var threadId  = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var preview   = request.Body.Length > 120 ? request.Body[..120] : request.Body;
        var sql = @"
INSERT INTO Comms.MessageThread
    (ThreadId, TenantId, AccountName, AccountId, Channel, Subject, BodyPreview,
     Status, Priority, AssignedTo, IsRead, IsEscalated, OptedOut, MessageCount,
     LastActivityAt, Sentiment, IsDeleted, CreatedDateUtc)
VALUES
    (@ThreadId, @TenantId, @AccountName, @AccountId, @Channel, @Subject, @Preview,
     'Open', @Priority, @AssignedTo, 0, 0, 0, 1, GETUTCDATE(), 'Neutral', 0, GETUTCDATE());

INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated)
VALUES
    (@MessageId, @ThreadId, @SenderName, @Channel, 'Outbound', @Body, GETUTCDATE(), 'Delivered', 0);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ThreadId  = threadId,
            MessageId = messageId,
            request.TenantId,
            request.AccountName,
            AccountId = request.AccountId,
            request.Channel,
            Subject = string.IsNullOrEmpty(request.Subject) ? "(No subject)" : request.Subject,
            Preview = preview,
            request.Priority,
            request.AssignedTo,
            SenderName = request.AssignedTo ?? "Agent",
            Body = request.Body
        }, cancellationToken: cancellationToken));
        return threadId;
    }

    public async Task<Guid> ReplyAsync(ReplyMessageRequest request, CancellationToken cancellationToken = default)
    {
        var messageId = Guid.NewGuid();
        var preview   = request.Body.Length > 120 ? request.Body[..120] : request.Body;
        var sql = @"
INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated)
VALUES
    (@MessageId, @ThreadId, @SenderName, @Channel, 'Outbound', @Body, GETUTCDATE(), 'Delivered', 0);

UPDATE Comms.MessageThread
SET MessageCount    = MessageCount + 1,
    BodyPreview     = @Preview,
    LastActivityAt  = GETUTCDATE(),
    Status          = 'Pending',
    ModifiedDateUtc = GETUTCDATE()
WHERE ThreadId = @ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            MessageId  = messageId,
            request.ThreadId,
            request.SenderName,
            request.Channel,
            Body = request.Body,
            Preview = preview
        }, cancellationToken: cancellationToken));
        return messageId;
    }

    public async Task AssignAsync(AssignThreadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET AssignedTo = @AssignedTo, ModifiedDateUtc = GETUTCDATE()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;

INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated)
VALUES
    (NEWID(), @ThreadId, 'System', 'Internal Note', 'Outbound',
     CONCAT('Assigned to ', @AssignedTo, CASE WHEN @Note IS NULL OR @Note = '' THEN '' ELSE '. Note: ' + @Note END),
     GETUTCDATE(), 'Delivered', 1);

UPDATE Comms.MessageThread
SET MessageCount = MessageCount + 1, LastActivityAt = GETUTCDATE()
WHERE ThreadId = @ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId, request.AssignedTo, Note = request.Note }, cancellationToken: cancellationToken));
    }

    public async Task EscalateAsync(EscalateThreadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET IsEscalated = 1, Priority = 'Urgent', AssignedTo = @EscalateTo, ModifiedDateUtc = GETUTCDATE()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;

INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated)
VALUES
    (NEWID(), @ThreadId, 'System', 'Internal Note', 'Outbound',
     CONCAT('Escalated to ', @EscalateTo, '. Reason: ', @Reason,
            CASE WHEN @Note IS NULL OR @Note = '' THEN '' ELSE '. ' + @Note END),
     GETUTCDATE(), 'Delivered', 1);

UPDATE Comms.MessageThread
SET MessageCount = MessageCount + 1, LastActivityAt = GETUTCDATE()
WHERE ThreadId = @ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId, request.EscalateTo, request.Reason, Note = request.Note }, cancellationToken: cancellationToken));
    }

    public async Task ResolveAsync(ResolveThreadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET Status = 'Resolved', ModifiedDateUtc = GETUTCDATE()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId }, cancellationToken: cancellationToken));
    }

    public async Task MarkReadAsync(MarkReadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET IsRead = 1, ModifiedDateUtc = GETUTCDATE()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId }, cancellationToken: cancellationToken));
    }
}
