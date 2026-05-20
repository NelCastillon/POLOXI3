namespace Ams.Application.Features.ContactIntake;

public sealed class ContactDemoRequestContext
{
    public string? RemoteIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    public string? Origin { get; set; }
}
