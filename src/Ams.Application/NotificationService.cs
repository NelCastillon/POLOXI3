using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

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
}
