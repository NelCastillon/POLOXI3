using Ams.Application.Common.Dtos;
using Ams.Application.Features.Workbench;

namespace Ams.Application.Abstractions.Persistence;

public interface IMyWorkbenchRepository
{
    Task<MyWorkbenchDto> GetAsync(MyWorkbenchRequest request, CancellationToken cancellationToken = default);

    Task SetTaskStatusAsync(Guid taskItemId, MyWorkbenchTaskStatusRequest request, CancellationToken cancellationToken = default);

    Task SetNotificationReadAsync(Guid notificationId, MyWorkbenchNotificationStatusRequest request, CancellationToken cancellationToken = default);
}
