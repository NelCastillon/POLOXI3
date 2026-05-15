using Microsoft.AspNetCore.SignalR;

namespace Ams.Api.Hubs;

public sealed class LeadScoringHub : Hub
{
    public Task JoinTenant(Guid tenantId)
        => Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId));

    public Task LeaveTenant(Guid tenantId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantGroup(tenantId));

    public static string TenantGroup(Guid tenantId) => $"tenant:{tenantId:N}";
}
