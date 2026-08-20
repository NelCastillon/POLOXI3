using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Communications;

public sealed record QueueEmailNotificationRequest(
    Guid TenantId,
    [property: Required, EmailAddress, StringLength(320)] string RecipientAddress,
    [property: EmailAddress, StringLength(320)] string? ReplyToAddress,
    [property: Required, StringLength(200)] string Subject,
    [property: Required] string Body,
    bool IsBodyHtml,
    [property: Required, StringLength(100)] string TemplateCode,
    [property: Required, StringLength(100)] string EntityName,
    Guid? EntityId,
    [property: Required, StringLength(200)] string ExternalCorrelationId,
    [property: StringLength(40)] string Priority,
    [property: StringLength(80)] string Category,
    Guid? CreatedByUserId,
    IReadOnlyCollection<NotificationAttachmentRequest> Attachments);

public sealed record NotificationAttachmentRequest(
    Guid? DocumentId,
    [property: StringLength(2000)] string? StorageReference,
    [property: Required, StringLength(500)] string FileName,
    [property: Required, StringLength(200)] string ContentType);

public sealed record NotificationDeliveryResult(
    Guid NotificationId,
    string StatusCode,
    string? ExternalDeliveryId,
    string? ErrorMessage,
    bool RetryScheduled);
