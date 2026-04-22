using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;

namespace Ams.Application;

public sealed class MessageService : IMessageService
{
    private readonly IMessageRepository _repository;
    public MessageService(IMessageRepository repository) => _repository = repository;

    public Task<IReadOnlyList<MessageThreadDto>> GetThreadsAsync(GetThreadsRequest request, CancellationToken cancellationToken = default)
        => _repository.GetThreadsAsync(request, cancellationToken);

    public Task<MessageThreadDto?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default)
        => _repository.GetThreadByIdAsync(threadId, cancellationToken);

    public Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
        => _repository.SendMessageAsync(request, cancellationToken);

    public Task<Guid> ReplyAsync(ReplyMessageRequest request, CancellationToken cancellationToken = default)
        => _repository.ReplyAsync(request, cancellationToken);

    public Task AssignAsync(AssignThreadRequest request, CancellationToken cancellationToken = default)
        => _repository.AssignAsync(request, cancellationToken);

    public Task EscalateAsync(EscalateThreadRequest request, CancellationToken cancellationToken = default)
        => _repository.EscalateAsync(request, cancellationToken);

    public Task ResolveAsync(ResolveThreadRequest request, CancellationToken cancellationToken = default)
        => _repository.ResolveAsync(request, cancellationToken);

    public Task MarkReadAsync(MarkReadRequest request, CancellationToken cancellationToken = default)
        => _repository.MarkReadAsync(request, cancellationToken);
}
