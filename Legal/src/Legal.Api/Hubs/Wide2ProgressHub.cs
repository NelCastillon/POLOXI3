using Legal.Application.Features.Intelligence;
using Microsoft.AspNetCore.SignalR;

namespace Legal.Api.Hubs;

// Real-time transport for the Wide2 Decision console KPI feed. Clients (the Blazor page) join the
// group named by their search CorrelationId, then receive "progress" messages carrying a running
// snapshot of the four cockpit KPIs while the background pipeline runs. Read-only fan-out hub: the
// pipeline pushes through IWide2ProgressPublisher; clients only subscribe to their own correlation.
public sealed class Wide2ProgressHub:Hub
{
    public const string HubPath="/hubs/wide2-progress";
    public const string ProgressMethod="progress";

    // The client calls this after connecting to receive updates for a specific search operation.
    public Task Subscribe(string correlationId)
        =>string.IsNullOrWhiteSpace(correlationId)
            ?Task.CompletedTask
            :Groups.AddToGroupAsync(Context.ConnectionId,GroupName(correlationId));

    public Task Unsubscribe(string correlationId)
        =>string.IsNullOrWhiteSpace(correlationId)
            ?Task.CompletedTask
            :Groups.RemoveFromGroupAsync(Context.ConnectionId,GroupName(correlationId));

    internal static string GroupName(string correlationId)=>$"wide2:{correlationId.Trim()}";
}

// SignalR-backed publisher registered in the API host. Fan-out is scoped to the run's correlation
// group so only the requesting client is updated. Fully fail-soft by contract (the pipeline wraps
// every call in a try/catch), so a transport hiccup never affects the decision result.
public sealed class SignalRWide2ProgressPublisher(IHubContext<Wide2ProgressHub> hubContext):IWide2ProgressPublisher
{
    public Task PublishAsync(Wide2ProgressUpdate update,CancellationToken cancellationToken=default)
        =>hubContext.Clients.Group(Wide2ProgressHub.GroupName(update.CorrelationId))
            .SendAsync(Wide2ProgressHub.ProgressMethod,update,cancellationToken);
}
