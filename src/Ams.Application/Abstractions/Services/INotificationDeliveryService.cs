using Ams.Application.Features.Communications;

namespace Ams.Application.Abstractions.Services;

public interface INotificationDeliveryService
{
    Task<Guid> QueueEmailAsync(QueueEmailNotificationRequest request, CancellationToken cancellationToken = default);
    Task<NotificationDeliveryResult> DeliverAsync(Guid tenantId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<int> ProcessQueuedAsync(string leaseOwner, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
}
