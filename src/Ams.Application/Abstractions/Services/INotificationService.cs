using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface INotificationService
{
    Task<NotificationDto?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationTemplateDto>> SearchTemplatesAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
