namespace Ams.Web.Services;

public sealed class LeadDialogService
{
    public event Action<LeadDialogRequest>? OnOpenCreate;

    public Task OpenCreateAsync(Guid? tenantId = null, Guid? createdByUserId = null, Func<Task>? onLeadCreated = null)
    {
        OnOpenCreate?.Invoke(new LeadDialogRequest(tenantId, createdByUserId, onLeadCreated));
        return Task.CompletedTask;
    }
}

public sealed record LeadDialogRequest(Guid? TenantId, Guid? CreatedByUserId, Func<Task>? OnLeadCreated);
