using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;

namespace Ams.Application.Abstractions.Persistence;

public interface IMessageRepository
{
    Task<IReadOnlyList<MessageThreadDto>> GetThreadsAsync(GetThreadsRequest request, CancellationToken cancellationToken = default);
    Task<MessageThreadDto?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ReplyAsync(ReplyMessageRequest request, CancellationToken cancellationToken = default);
    Task AssignAsync(AssignThreadRequest request, CancellationToken cancellationToken = default);
    Task EscalateAsync(EscalateThreadRequest request, CancellationToken cancellationToken = default);
    Task ResolveAsync(ResolveThreadRequest request, CancellationToken cancellationToken = default);
    Task MarkReadAsync(MarkReadRequest request, CancellationToken cancellationToken = default);
}
