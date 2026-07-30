using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Submissions;

public sealed class ProposalDeliveryWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WorkerOptions _options;
    private readonly ILogger<ProposalDeliveryWorkerService> _logger;

    public ProposalDeliveryWorkerService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IOptions<WorkerOptions> options,
        ILogger<ProposalDeliveryWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS proposal delivery worker started with {PollIntervalSeconds}s polling interval.", _options.ProposalDeliveryPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                var processed = 0;

                while (processed < Math.Clamp(_options.MaxProposalDeliveriesPerPoll, 1, 100))
                {
                    var dispatch = await ClaimNextAsync(connectionFactory, _options.ProposalDeliveryClaimLeaseMinutes, stoppingToken);
                    if (dispatch is null) break;

                    await ProcessAsync(connectionFactory, dispatch, stoppingToken);
                    processed++;
                }

                if (processed > 0)
                {
                    _logger.LogInformation("Proposal delivery worker processed {DispatchCount} dispatch records.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Proposal delivery worker polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.ProposalDeliveryPollIntervalSeconds, 10, 3600)), stoppingToken);
        }
    }

    private static async Task<ProposalDeliveryWorkItem?> ClaimNextAsync(ISqlConnectionFactory connectionFactory, int claimLeaseMinutes, CancellationToken cancellationToken)
    {
        const string sql = """
DECLARE @DispatchId UNIQUEIDENTIFIER;
DECLARE @WorkerId NVARCHAR(200) = CONCAT(HOST_NAME(), N':', APP_NAME(), N':ProposalDelivery');

BEGIN TRANSACTION;
UPDATE Submissions.ProposalDeliveryDispatch WITH (ROWLOCK)
SET StatusCode = CASE WHEN AttemptCount < MaxAttempts THEN N'Queued' ELSE N'Failed' END,
    NextAttemptDateUtc = CASE WHEN AttemptCount < MaxAttempts THEN SYSUTCDATETIME() ELSE NULL END,
    ErrorCode = N'WORKER_CLAIM_EXPIRED',
    ErrorMessage = N'The prior worker claim expired before completion. Delivery was recovered; external delivery may require duplicate-send review.',
    ClaimedDateUtc = NULL,
    ClaimedBy = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE StatusCode = N'Processing'
  AND IsDeleted = 0
  AND ClaimedDateUtc < DATEADD(minute, -@ClaimLeaseMinutes, SYSUTCDATETIME());

SELECT TOP 1 @DispatchId = dispatch.ProposalDeliveryDispatchId
FROM Submissions.ProposalDeliveryDispatch dispatch WITH (UPDLOCK, READPAST, ROWLOCK)
INNER JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = dispatch.ProposalDeliveryProviderId
 AND provider.TenantId = dispatch.TenantId
 AND provider.IsActive = 1
 AND provider.IsDeleted = 0
WHERE dispatch.StatusCode = N'Queued'
  AND dispatch.IsDeleted = 0
  AND dispatch.AttemptCount < dispatch.MaxAttempts
  AND COALESCE(dispatch.NextAttemptDateUtc, dispatch.CreatedDateUtc) <= SYSUTCDATETIME()
ORDER BY COALESCE(dispatch.NextAttemptDateUtc, dispatch.CreatedDateUtc), dispatch.CreatedDateUtc;

IF @DispatchId IS NOT NULL
BEGIN
    UPDATE Submissions.ProposalDeliveryDispatch
    SET StatusCode = N'Processing',
        AttemptCount = AttemptCount + 1,
        ClaimedDateUtc = SYSUTCDATETIME(),
        ClaimedBy = @WorkerId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE ProposalDeliveryDispatchId = @DispatchId;
END;
COMMIT TRANSACTION;

SELECT dispatch.ProposalDeliveryDispatchId, dispatch.TenantId, dispatch.SubmissionId, dispatch.ProposalId,
       dispatch.ProposalVersionNumber, dispatch.DeliveryMethodCode, dispatch.Recipient, dispatch.AttemptCount, dispatch.MaxAttempts,
       provider.ProposalDeliveryProviderId, provider.ProviderCode, provider.HandlerCode, provider.DisplayName AS ProviderName,
       provider.EndpointUri, provider.SenderAddress, provider.SecretReference, provider.ConfigurationJson,
       provider.IsConfigured, provider.RetryDelaySeconds,
       proposal.Title, proposal.HtmlContent, proposal.PdfUrl, proposal.DocumentId,
       submission.AccountId
FROM Submissions.ProposalDeliveryDispatch dispatch
INNER JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = dispatch.ProposalDeliveryProviderId
INNER JOIN Submissions.Proposal proposal
  ON proposal.ProposalId = dispatch.ProposalId AND proposal.TenantId = dispatch.TenantId AND proposal.IsDeleted = 0
INNER JOIN Submissions.Submission submission
  ON submission.SubmissionId = dispatch.SubmissionId AND submission.TenantId = dispatch.TenantId AND submission.IsDeleted = 0
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ProposalDeliveryWorkItem>(new CommandDefinition(sql, new { ClaimLeaseMinutes = Math.Clamp(claimLeaseMinutes, 5, 1440) }, cancellationToken: cancellationToken));
    }

    private async Task ProcessAsync(ISqlConnectionFactory connectionFactory, ProposalDeliveryWorkItem dispatch, CancellationToken cancellationToken)
    {
        try
        {
            if (!dispatch.IsConfigured)
            {
                await MarkConfigurationRequiredAsync(connectionFactory, dispatch, "PROVIDER_NOT_CONFIGURED", $"{dispatch.ProviderName} requires tenant configuration.", cancellationToken);
                return;
            }

            var result = dispatch.HandlerCode switch
            {
                "Smtp" => await SendSmtpAsync(dispatch, cancellationToken),
                "Portal" => await PublishPortalAsync(connectionFactory, dispatch, cancellationToken),
                "ESignature" => await SendESignatureAsync(dispatch, cancellationToken),
                "Manual" => DeliveryResult.Sent($"manual:{dispatch.ProposalDeliveryDispatchId}"),
                _ => DeliveryResult.ConfigurationRequired("HANDLER_NOT_SUPPORTED", $"Delivery handler '{dispatch.HandlerCode}' is not supported.")
            };

            if (result.IsConfigurationRequired)
            {
                await MarkConfigurationRequiredAsync(connectionFactory, dispatch, result.ErrorCode!, result.ErrorMessage!, cancellationToken);
            }
            else
            {
                await MarkDeliveredAsync(connectionFactory, dispatch, result.ExternalDeliveryId!, result.ResponseJson, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await MarkFailedOrRetryAsync(connectionFactory, dispatch, ex, cancellationToken);
        }
    }

    private static async Task<DeliveryResult> SendSmtpAsync(ProposalDeliveryWorkItem dispatch, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(dispatch.EndpointUri, UriKind.Absolute, out var endpoint) || !string.Equals(endpoint.Scheme, "smtp", StringComparison.OrdinalIgnoreCase))
            return DeliveryResult.ConfigurationRequired("SMTP_ENDPOINT_REQUIRED", "SMTP delivery requires an smtp:// endpoint URI.");
        if (string.IsNullOrWhiteSpace(dispatch.SenderAddress))
            return DeliveryResult.ConfigurationRequired("SMTP_SENDER_REQUIRED", "SMTP delivery requires a sender address.");

        var settings = ParseConfiguration(dispatch.ConfigurationJson);
        settings.TryGetValue("username", out var username);
        var password = ResolveSecret(dispatch.SecretReference);
        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
            return DeliveryResult.ConfigurationRequired("SMTP_SECRET_REQUIRED", "SMTP authentication requires the configured secret reference.");

        using var message = new MailMessage
        {
            From = new MailAddress(dispatch.SenderAddress),
            Subject = dispatch.Title,
            Body = dispatch.HtmlContent ?? dispatch.Title,
            IsBodyHtml = !string.IsNullOrWhiteSpace(dispatch.HtmlContent)
        };
        message.To.Add(new MailAddress(dispatch.Recipient));
        message.Headers.Add("X-AMS-Proposal-Id", dispatch.ProposalId.ToString());

        using var client = new SmtpClient(endpoint.Host, endpoint.Port > 0 ? endpoint.Port : 25)
        {
            EnableSsl = !settings.TryGetValue("enableSsl", out var enableSsl) || !bool.TryParse(enableSsl, out var parsedSsl) || parsedSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message, cancellationToken);
        return DeliveryResult.Sent($"smtp:{dispatch.ProposalDeliveryDispatchId}");
    }

    private static async Task<DeliveryResult> PublishPortalAsync(ISqlConnectionFactory connectionFactory, ProposalDeliveryWorkItem dispatch, CancellationToken cancellationToken)
    {
        const string sql = """
DECLARE @ContactId UNIQUEIDENTIFIER;
SELECT TOP 1 @ContactId = contact.ContactId
FROM Client.Contact contact
WHERE contact.TenantId = @TenantId
  AND contact.AccountId = @AccountId
  AND contact.IsPortalUser = 1
  AND contact.IsDeleted = 0
  AND LOWER(contact.Email) = LOWER(@Recipient);

IF @ContactId IS NULL
BEGIN
    SELECT CAST(NULL AS UNIQUEIDENTIFIER);
    RETURN;
END;

DECLARE @PortalProposalDeliveryId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Portal.ProposalDelivery
    (PortalProposalDeliveryId, TenantId, AccountId, ContactId, SubmissionId, ProposalId,
     ProposalDeliveryDispatchId, Title, HtmlContent, DocumentId, PublishedDateUtc, StatusCode,
     CreatedDateUtc, IsDeleted)
VALUES
    (@PortalProposalDeliveryId, @TenantId, @AccountId, @ContactId, @SubmissionId, @ProposalId,
     @DispatchId, @Title, @Body, @DocumentId, SYSUTCDATETIME(), N'Published', SYSUTCDATETIME(), 0);
SELECT @PortalProposalDeliveryId;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var notificationId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new
        {
            dispatch.TenantId,
            dispatch.AccountId,
            dispatch.SubmissionId,
            dispatch.Recipient,
            dispatch.Title,
            Body = dispatch.HtmlContent ?? dispatch.Title,
            dispatch.ProposalId,
            DispatchId = dispatch.ProposalDeliveryDispatchId,
            dispatch.DocumentId
        }, cancellationToken: cancellationToken));

        return notificationId.HasValue
            ? DeliveryResult.Sent($"portal:{notificationId.Value}")
            : DeliveryResult.ConfigurationRequired("PORTAL_RECIPIENT_REQUIRED", "The recipient must be an active portal contact on the submission account.");
    }

    private async Task<DeliveryResult> SendESignatureAsync(ProposalDeliveryWorkItem dispatch, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(dispatch.EndpointUri, UriKind.Absolute, out var endpoint) || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return DeliveryResult.ConfigurationRequired("ESIGN_ENDPOINT_REQUIRED", "E-signature delivery requires an HTTPS provider endpoint.");
        var secret = ResolveSecret(dispatch.SecretReference);
        if (string.IsNullOrWhiteSpace(secret))
            return DeliveryResult.ConfigurationRequired("ESIGN_SECRET_REQUIRED", "E-signature delivery requires the configured secret reference.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        request.Headers.Add("Idempotency-Key", dispatch.ProposalDeliveryDispatchId.ToString());
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            dispatch.ProposalDeliveryDispatchId,
            dispatch.ProposalId,
            dispatch.SubmissionId,
            dispatch.Recipient,
            dispatch.Title,
            dispatch.HtmlContent,
            dispatch.PdfUrl,
            dispatch.DocumentId
        }), Encoding.UTF8, MediaTypeNames.Application.Json);

        var client = _httpClientFactory.CreateClient(nameof(ProposalDeliveryWorkerService));
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"E-signature provider returned {(int)response.StatusCode}: {responseBody}", null, response.StatusCode);

        var externalId = response.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(externalId) && response.Headers.TryGetValues("X-Envelope-Id", out var envelopeIds)) externalId = envelopeIds.FirstOrDefault();
        externalId ??= $"esign:{dispatch.ProposalDeliveryDispatchId}";
        return DeliveryResult.Sent(externalId, JsonSerializer.Serialize(new { statusCode = (int)response.StatusCode, body = responseBody }));
    }

    private static async Task MarkDeliveredAsync(ISqlConnectionFactory connectionFactory, ProposalDeliveryWorkItem dispatch, string externalDeliveryId, string? responseJson, CancellationToken cancellationToken)
    {
        const string sql = """
BEGIN TRANSACTION;
UPDATE Submissions.ProposalDeliveryDispatch
SET StatusCode = N'Delivered', ExternalDeliveryId = @ExternalDeliveryId,
    CompletedDateUtc = COALESCE(CompletedDateUtc, SYSUTCDATETIME()),
    ResponseJson = @ResponseJson, ErrorCode = NULL, ErrorMessage = NULL, NextAttemptDateUtc = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND StatusCode = N'Processing' AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    ROLLBACK TRANSACTION;
    RETURN;
END;

UPDATE Submissions.Proposal
SET Status = CASE WHEN PresentedDateUtc IS NULL THEN N'Delivered' ELSE Status END,
    GovernanceStatusCode = CASE WHEN PresentedDateUtc IS NULL THEN N'Delivered' ELSE GovernanceStatusCode END,
    DeliveryStatus = N'Delivered', DeliveryMethod = @DeliveryMethodCode, Recipient = @Recipient,
    SentDateUtc = COALESCE(SentDateUtc, SYSUTCDATETIME()),
    DeliveryConfirmedDateUtc = COALESCE(DeliveryConfirmedDateUtc, SYSUTCDATETIME()),
    LastDeliveryDispatchId = @DispatchId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'Delivered', CONCAT(N'Proposal delivery completed through ', @ProviderName, N' to ', @Recipient, N'.'), SYSUTCDATETIME(), SYSUTCDATETIME(), 0);

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES
    (NEWID(), @SubmissionId, @TenantId, N'ProposalDelivered', CONCAT(@ProviderName, N' completed delivery for ', @Recipient, N'.'), SYSUTCDATETIME(), N'ProposalDeliveryDispatch', @DispatchId, N'Worker', 0);

IF @DeliveryMethodCode IN (N'ESignature', N'ESign')
BEGIN
    DECLARE @EnvelopeId UNIQUEIDENTIFIER = NEWID(), @ESignRequestId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Submissions.ProposalESignEnvelope (ProposalESignEnvelopeId,TenantId,SubmissionId,ProposalId,ProposalVersionNumber,ProposalDeliveryDispatchId,ESignRequestId,ProposalDeliveryProviderId,ProviderCode,ExternalEnvelopeId,StatusCode,SentDateUtc,CreatedDateUtc,IsDeleted)
    VALUES (@EnvelopeId,@TenantId,@SubmissionId,@ProposalId,@ProposalVersionNumber,@DispatchId,@ESignRequestId,@ProviderId,@ProviderCode,@ExternalDeliveryId,N'Sent',SYSUTCDATETIME(),SYSUTCDATETIME(),0);
    INSERT INTO DMS.ESignRequest (ESignRequestId,TenantId,DocumentId,SignerName,SignerEmail,Priority,Status,SentDate,DueDate,Message,ProposalId,ProposalVersionNumber,ProposalDeliveryDispatchId,ProposalESignEnvelopeId,ProviderCode,ExternalEnvelopeId,CreatedDateUtc,IsDeleted)
    SELECT @ESignRequestId,@TenantId,proposal.DocumentId,recipient.RecipientName,recipient.RecipientEmail,N'High',N'Sent',SYSUTCDATETIME(),DATEADD(day,7,SYSUTCDATETIME()),N'Please review and sign the approved proposal.',@ProposalId,@ProposalVersionNumber,@DispatchId,@EnvelopeId,@ProviderCode,@ExternalDeliveryId,SYSUTCDATETIME(),0
    FROM Submissions.Proposal proposal
    OUTER APPLY (SELECT TOP 1 RecipientName,RecipientEmail FROM Submissions.ProposalRecipient WHERE ProposalId=@ProposalId AND TenantId=@TenantId AND IsSigner=1 AND IsDeleted=0 ORDER BY SigningOrder) recipient
    WHERE proposal.ProposalId=@ProposalId AND proposal.DocumentId IS NOT NULL AND recipient.RecipientEmail IS NOT NULL;
END;
COMMIT TRANSACTION;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DispatchId = dispatch.ProposalDeliveryDispatchId,
            dispatch.TenantId,
            dispatch.SubmissionId,
            dispatch.ProposalId,
            dispatch.DeliveryMethodCode,
            dispatch.Recipient,
            dispatch.ProviderName,
            dispatch.ProposalVersionNumber,
            ProviderId = dispatch.ProposalDeliveryProviderId,
            dispatch.ProviderCode,
            ExternalDeliveryId = externalDeliveryId,
            ResponseJson = responseJson
        }, cancellationToken: cancellationToken));
    }

    private static async Task MarkConfigurationRequiredAsync(ISqlConnectionFactory connectionFactory, ProposalDeliveryWorkItem dispatch, string errorCode, string errorMessage, CancellationToken cancellationToken)
    {
        const string sql = """
UPDATE Submissions.ProposalDeliveryDispatch
SET StatusCode = N'Configuration Required', ErrorCode = @ErrorCode, ErrorMessage = @ErrorMessage,
    NextAttemptDateUtc = NULL, ClaimedDateUtc = NULL, ClaimedBy = NULL, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND IsDeleted = 0;
UPDATE Submissions.Proposal
SET DeliveryStatus = N'Configuration Required', LastDeliveryDispatchId = @DispatchId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DispatchId = dispatch.ProposalDeliveryDispatchId, dispatch.TenantId, dispatch.ProposalId, ErrorCode = errorCode, ErrorMessage = errorMessage }, cancellationToken: cancellationToken));
    }

    private static async Task MarkFailedOrRetryAsync(ISqlConnectionFactory connectionFactory, ProposalDeliveryWorkItem dispatch, Exception exception, CancellationToken cancellationToken)
    {
        var retry = dispatch.AttemptCount < dispatch.MaxAttempts;
        const string sql = """
UPDATE Submissions.ProposalDeliveryDispatch
SET StatusCode = @StatusCode,
    NextAttemptDateUtc = @NextAttemptDateUtc,
    ClaimedDateUtc = NULL,
    ClaimedBy = NULL,
    ErrorCode = @ErrorCode,
    ErrorMessage = @ErrorMessage,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND IsDeleted = 0;
UPDATE Submissions.Proposal
SET DeliveryStatus = @StatusCode, LastDeliveryDispatchId = @DispatchId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DispatchId = dispatch.ProposalDeliveryDispatchId,
            dispatch.TenantId,
            dispatch.ProposalId,
            StatusCode = retry ? "Queued" : "Failed",
            NextAttemptDateUtc = retry ? DateTime.UtcNow.AddSeconds(dispatch.RetryDelaySeconds) : (DateTime?)null,
            ErrorCode = exception is HttpRequestException http && http.StatusCode.HasValue ? $"HTTP_{(int)http.StatusCode.Value}" : exception.GetType().Name,
            ErrorMessage = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message
        }, cancellationToken: cancellationToken));
    }

    private static Dictionary<string, string> ParseConfiguration(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson)) return new(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(configurationJson);
        return document.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveSecret(string? secretReference)
        => string.IsNullOrWhiteSpace(secretReference) ? null : Environment.GetEnvironmentVariable(secretReference);

    private sealed class ProposalDeliveryWorkItem
    {
        public Guid ProposalDeliveryDispatchId { get; set; }
        public Guid TenantId { get; set; }
        public Guid SubmissionId { get; set; }
        public Guid ProposalId { get; set; }
        public Guid AccountId { get; set; }
        public Guid ProposalDeliveryProviderId { get; set; }
        public int ProposalVersionNumber { get; set; }
        public string DeliveryMethodCode { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public string ProviderCode { get; set; } = string.Empty;
        public string HandlerCode { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string? EndpointUri { get; set; }
        public string? SenderAddress { get; set; }
        public string? SecretReference { get; set; }
        public string? ConfigurationJson { get; set; }
        public bool IsConfigured { get; set; }
        public int RetryDelaySeconds { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? HtmlContent { get; set; }
        public string? PdfUrl { get; set; }
        public Guid? DocumentId { get; set; }
    }

    private sealed record DeliveryResult(bool IsConfigurationRequired, string? ExternalDeliveryId, string? ResponseJson, string? ErrorCode, string? ErrorMessage)
    {
        public static DeliveryResult Sent(string externalDeliveryId, string? responseJson = null) => new(false, externalDeliveryId, responseJson, null, null);
        public static DeliveryResult ConfigurationRequired(string errorCode, string errorMessage) => new(true, null, null, errorCode, errorMessage);
    }
}
