using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Communications;

namespace Ams.Application;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;

    public NotificationService(INotificationRepository repository)
        => _repository = repository;

    public Task<NotificationDto?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(notificationId, cancellationToken);

    public Task<PagedResult<NotificationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<NotificationTemplateDto>> SearchTemplatesAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchTemplatesAsync(searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task SetReadAsync(Guid notificationId, bool isRead, CancellationToken cancellationToken = default)
        => _repository.SetReadAsync(notificationId, isRead, cancellationToken);

    public Task SetStatusAsync(Guid notificationId, string statusCode, CancellationToken cancellationToken = default)
        => _repository.SetStatusAsync(notificationId, statusCode, cancellationToken);

    public Task RetryAsync(Guid notificationId, string? providerName = null, CancellationToken cancellationToken = default)
        => _repository.RetryAsync(notificationId, providerName, cancellationToken);

    public Task MarkAllReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
        => _repository.MarkAllReadAsync(tenantId, recipientUserId, cancellationToken);

    public Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(notificationId, cancellationToken);

    public Task DeleteReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteReadAsync(tenantId, recipientUserId, cancellationToken);
}
