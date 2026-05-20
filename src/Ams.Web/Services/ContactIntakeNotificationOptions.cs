namespace Ams.Web.Services;

public sealed class ContactIntakeNotificationOptions
{
    public bool Enabled { get; set; } = true;
    public string ToEmail { get; set; } = "ams_admin@agencybinder.com";
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "AgencyBinder Contact Intake";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
