using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Workbench;

public sealed class MyWorkbenchRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string? SearchTerm { get; set; }

    public string? ViewCode { get; set; }

    public string? PriorityCode { get; set; }

    public string? StatusCode { get; set; }

    public DateOnly? WorkDate { get; set; }
}

public sealed class MyWorkbenchTaskStatusRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(40)]
    public string StatusCode { get; set; } = string.Empty;

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class MyWorkbenchNotificationStatusRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public bool IsRead { get; set; } = true;

    public Guid? ModifiedByUserId { get; set; }
}
