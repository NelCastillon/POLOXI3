using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Communications;

namespace Ams.Application.Abstractions.Services;

public interface INotificationService
{
    Task<NotificationDto?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationTemplateDto>> SearchTemplatesAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default);
    Task SetReadAsync(Guid notificationId, bool isRead, CancellationToken cancellationToken = default);
    Task SetStatusAsync(Guid notificationId, string statusCode, CancellationToken cancellationToken = default);
    Task RetryAsync(Guid notificationId, string? providerName = null, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task DeleteReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default);
}
