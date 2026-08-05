using System.Net;
using System.Net.Mail;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Communications;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Ams.Infrastructure.Services;

public sealed class NotificationDeliveryService(
    ISqlConnectionFactory connectionFactory,
    IDocumentStorageService storage,
    ILogger<NotificationDeliveryService> logger) : INotificationDeliveryService
{
    public async Task<Guid> QueueEmailAsync(QueueEmailNotificationRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        const string sql = """
DECLARE @NotificationId UNIQUEIDENTIFIER;
SELECT @NotificationId=NotificationId FROM Core.Notification WITH(UPDLOCK,HOLDLOCK)
WHERE TenantId=@TenantId AND ExternalCorrelationId=@ExternalCorrelationId AND IsDeleted=0;
IF @NotificationId IS NULL
BEGIN
    SET @NotificationId=NEWID();
    DECLARE @TemplateId UNIQUEIDENTIFIER=(SELECT TOP(1) TemplateId FROM Core.NotificationTemplate WHERE TemplateCode=@TemplateCode AND IsActive=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END);
    INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,TemplateId,ChannelCode,RecipientAddress,ReplyToAddress,Subject,Body,IsBodyHtml,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,NextAttemptDateUtc,MaxAttempts,ExternalCorrelationId,LastSyncedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES(@NotificationId,@TenantId,'00000000-0000-0000-0000-000000000000',@TemplateId,N'Email',@RecipientAddress,@ReplyToAddress,@Subject,@Body,@IsBodyHtml,@EntityName,@EntityId,N'Queued',0,@Priority,@Category,N'PLATFORM_SMTP',N'Queued',N'Compliant',N'Synced',0,SYSUTCDATETIME(),5,@ExternalCorrelationId,SYSUTCDATETIME(),SYSUTCDATETIME(),@CreatedByUserId,0);
    INSERT Core.NotificationAuditLog(TenantId,NotificationId,ActionName,Details,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@TenantId,@NotificationId,N'Queued',N'Email queued through Notification Platform.',SYSUTCDATETIME(),@CreatedByUserId,0);
END;
SELECT @NotificationId;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var notificationId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, request, transaction, cancellationToken: cancellationToken));
        foreach (var attachment in request.Attachments)
        {
            const string attachmentSql = """
IF NOT EXISTS(SELECT 1 FROM Core.NotificationAttachment WHERE TenantId=@TenantId AND NotificationId=@NotificationId AND ((DocumentId=@DocumentId AND @DocumentId IS NOT NULL) OR (StorageReference=@StorageReference AND @StorageReference IS NOT NULL)) AND IsDeleted=0)
INSERT Core.NotificationAttachment(NotificationAttachmentId,TenantId,NotificationId,DocumentId,StorageReference,FileName,ContentType,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(NEWID(),@TenantId,@NotificationId,@DocumentId,@StorageReference,@FileName,@ContentType,SYSUTCDATETIME(),@CreatedByUserId,0);
""";
            await connection.ExecuteAsync(new CommandDefinition(attachmentSql, new { request.TenantId, NotificationId = notificationId, attachment.DocumentId, attachment.StorageReference, attachment.FileName, attachment.ContentType, request.CreatedByUserId }, transaction, cancellationToken: cancellationToken));
        }
        transaction.Commit();
        return notificationId;
    }

    public async Task<NotificationDeliveryResult> DeliverAsync(Guid tenantId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string claimSql = """
UPDATE Core.Notification WITH(ROWLOCK) SET StatusCode=N'Processing',DeliveryStatus=N'Processing',AttemptCount=AttemptCount+1,LastAttemptDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND NotificationId=@NotificationId AND IsDeleted=0 AND StatusCode IN(N'Queued',N'Failed',N'Processing') AND AttemptCount<MaxAttempts;
SELECT notification.NotificationId,notification.TenantId,notification.RecipientAddress,notification.ReplyToAddress,notification.Subject,notification.Body,notification.IsBodyHtml,notification.AttemptCount,notification.MaxAttempts,
       provider.ProviderCode,provider.EndpointReference,provider.SenderAddress,provider.SenderDisplayName,provider.CredentialReference,provider.ConfigurationJson,provider.RetryDelaySeconds
FROM Core.Notification notification
OUTER APPLY(SELECT TOP(1) candidate.* FROM Core.NotificationDeliveryProvider candidate WHERE candidate.ChannelCode=N'Email' AND candidate.IsActive=1 AND candidate.IsDeleted=0 AND (candidate.TenantId=notification.TenantId OR candidate.TenantId IS NULL) ORDER BY CASE WHEN candidate.TenantId=notification.TenantId THEN 0 ELSE 1 END) provider
WHERE notification.TenantId=@TenantId AND notification.NotificationId=@NotificationId AND notification.IsDeleted=0;
SELECT attachment.DocumentId,attachment.StorageReference,attachment.FileName,attachment.ContentType,document.StoragePath
FROM Core.NotificationAttachment attachment LEFT JOIN DMS.Document document ON document.TenantId=attachment.TenantId AND document.DocumentId=attachment.DocumentId AND document.IsDeleted=0
WHERE attachment.TenantId=@TenantId AND attachment.NotificationId=@NotificationId AND attachment.IsDeleted=0;
""";
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(claimSql, new { TenantId = tenantId, NotificationId = notificationId }, cancellationToken: cancellationToken));
        var item = await multi.ReadSingleOrDefaultAsync<DeliveryRow>();
        var attachments = (await multi.ReadAsync<AttachmentRow>()).AsList();
        if (item is null) return new(notificationId, "NotFound", null, "Notification was not found.", false);
        if (string.IsNullOrWhiteSpace(item.EndpointReference) || string.IsNullOrWhiteSpace(item.SenderAddress))
            return await FailAsync(connection, item, "SMTP provider configuration is incomplete.", cancellationToken);

        try
        {
            var endpoint = new Uri(item.EndpointReference);
            if (!string.Equals(endpoint.Scheme, "smtp", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("SMTP endpoint must use smtp://.");
            var configuration = System.Text.Json.JsonSerializer.Deserialize<SmtpConfiguration>(item.ConfigurationJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            using var message = new MailMessage { From = new(item.SenderAddress, item.SenderDisplayName), Subject = item.Subject, Body = item.Body, IsBodyHtml = item.IsBodyHtml };
            message.To.Add(item.RecipientAddress!);
            if (!string.IsNullOrWhiteSpace(item.ReplyToAddress)) message.ReplyToList.Add(item.ReplyToAddress);
            var downloads = new List<DocumentStorageDownloadResult>();
            try
            {
                foreach (var attachment in attachments)
                {
                    var reference = attachment.StorageReference ?? attachment.StoragePath;
                    if (string.IsNullOrWhiteSpace(reference)) continue;
                    var download = await storage.DownloadAsync(reference, cancellationToken) ?? throw new InvalidOperationException($"Notification attachment '{attachment.FileName}' was not found.");
                    downloads.Add(download);
                    message.Attachments.Add(new Attachment(download.Content, attachment.FileName, attachment.ContentType ?? download.ContentType ?? "application/octet-stream"));
                }
                using var client = new SmtpClient(endpoint.Host, endpoint.Port > 0 ? endpoint.Port : 25) { EnableSsl = configuration.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network };
                if (!string.IsNullOrWhiteSpace(configuration.Username)) client.Credentials = new NetworkCredential(configuration.Username, ResolveSecret(item.CredentialReference));
                await client.SendMailAsync(message, cancellationToken);
            }
            finally
            {
                foreach (var download in downloads) await download.Content.DisposeAsync();
            }
            const string sentSql = """
UPDATE Core.Notification SET StatusCode=N'Sent',DeliveryStatus=N'Sent',SentDateUtc=SYSUTCDATETIME(),DeliveredDateUtc=SYSUTCDATETIME(),NextAttemptDateUtc=NULL,ErrorMessage=NULL,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND NotificationId=@NotificationId;
INSERT Core.NotificationDeliveryAttempt(TenantId,NotificationId,ProviderName,ChannelCode,StatusCode,AttemptDateUtc,CreatedDateUtc,IsDeleted) VALUES(@TenantId,@NotificationId,@ProviderCode,N'Email',N'Sent',SYSUTCDATETIME(),SYSUTCDATETIME(),0);
INSERT Core.NotificationAuditLog(TenantId,NotificationId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES(@TenantId,@NotificationId,N'Sent',N'Email sent through shared Notification Platform.',SYSUTCDATETIME(),0);
""";
            await connection.ExecuteAsync(new CommandDefinition(sentSql, new { TenantId = tenantId, NotificationId = notificationId, item.ProviderCode }, cancellationToken: cancellationToken));
            return new(notificationId, "Sent", $"smtp:{notificationId}", null, false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification delivery failed for {NotificationId}: {Message}", notificationId, ex.Message);
            return await FailAsync(connection, item, ex.Message, cancellationToken);
        }
    }

    public async Task<int> ProcessQueuedAsync(string leaseOwner, int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP(@BatchSize) TenantId,NotificationId FROM Core.Notification WITH(UPDLOCK,READPAST,ROWLOCK)
WHERE ChannelCode=N'Email' AND IsDeleted=0 AND StatusCode IN(N'Queued',N'Failed') AND AttemptCount<MaxAttempts AND COALESCE(NextAttemptDateUtc,CreatedDateUtc)<=SYSUTCDATETIME()
ORDER BY COALESCE(NextAttemptDateUtc,CreatedDateUtc),CreatedDateUtc;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await connection.QueryAsync<QueueRow>(new CommandDefinition(sql, new { BatchSize = Math.Clamp(batchSize, 1, 100), LeaseOwner = leaseOwner, LeaseSeconds = (int)leaseDuration.TotalSeconds }, cancellationToken: cancellationToken))).AsList();
        foreach (var item in items) await DeliverAsync(item.TenantId, item.NotificationId, cancellationToken);
        return items.Count;
    }

    private static void Validate(QueueEmailNotificationRequest request)
    {
        if (request.TenantId == Guid.Empty || string.IsNullOrWhiteSpace(request.RecipientAddress) || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body) || string.IsNullOrWhiteSpace(request.ExternalCorrelationId))
            throw new ArgumentException("Tenant, recipient, subject, body, and correlation id are required.");
    }

    private static string? ResolveSecret(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        if (!reference.StartsWith("env://", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("SMTP credentials must use env://VARIABLE_NAME or anonymous delivery.");
        var variable = reference["env://".Length..].Trim();
        var secret = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException($"SMTP credential environment variable '{variable}' is not configured.");
        return secret;
    }

    private static async Task<NotificationDeliveryResult> FailAsync(System.Data.IDbConnection connection, DeliveryRow item, string error, CancellationToken cancellationToken)
    {
        var retry = item.AttemptCount < item.MaxAttempts;
        const string sql = """
UPDATE Core.Notification SET StatusCode=N'Failed',DeliveryStatus=N'Failed',ErrorMessage=@ErrorMessage,NextAttemptDateUtc=CASE WHEN @Retry=1 THEN DATEADD(second,@RetryDelaySeconds,SYSUTCDATETIME()) ELSE NULL END,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND NotificationId=@NotificationId;
INSERT Core.NotificationDeliveryAttempt(TenantId,NotificationId,ProviderName,ChannelCode,StatusCode,AttemptDateUtc,ErrorMessage,CreatedDateUtc,IsDeleted) VALUES(@TenantId,@NotificationId,COALESCE(@ProviderCode,N'UNCONFIGURED'),N'Email',N'Failed',SYSUTCDATETIME(),@ErrorMessage,SYSUTCDATETIME(),0);
""";
        await connection.ExecuteAsync(new CommandDefinition(sql, new { item.TenantId, item.NotificationId, ErrorMessage = error[..Math.Min(error.Length, 1000)], Retry = retry, item.RetryDelaySeconds, item.ProviderCode }, cancellationToken: cancellationToken));
        return new(item.NotificationId, "Failed", null, error, retry);
    }

    private sealed record QueueRow(Guid TenantId, Guid NotificationId);
    private sealed record AttachmentRow(Guid? DocumentId, string? StorageReference, string FileName, string? ContentType, string? StoragePath);
    private sealed record DeliveryRow(Guid NotificationId, Guid TenantId, string? RecipientAddress, string? ReplyToAddress, string Subject, string Body, bool IsBodyHtml, int AttemptCount, int MaxAttempts, string? ProviderCode, string? EndpointReference, string? SenderAddress, string? SenderDisplayName, string? CredentialReference, string ConfigurationJson, int RetryDelaySeconds);
    private sealed class SmtpConfiguration { public string? Username { get; set; } public bool EnableSsl { get; set; } = true; }
}
