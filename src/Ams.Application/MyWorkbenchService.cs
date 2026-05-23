using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Workbench;

namespace Ams.Application;

public sealed class MyWorkbenchService : IMyWorkbenchService
{
    private readonly IMyWorkbenchRepository _repository;

    public MyWorkbenchService(IMyWorkbenchRepository repository)
    {
        _repository = repository;
    }

    public Task<MyWorkbenchDto> GetAsync(MyWorkbenchRequest request, CancellationToken cancellationToken = default)
        => _repository.GetAsync(request, cancellationToken);

    public Task SetTaskStatusAsync(Guid taskItemId, MyWorkbenchTaskStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.SetTaskStatusAsync(taskItemId, request, cancellationToken);

    public Task SetNotificationReadAsync(Guid notificationId, MyWorkbenchNotificationStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.SetNotificationReadAsync(notificationId, request, cancellationToken);
}
