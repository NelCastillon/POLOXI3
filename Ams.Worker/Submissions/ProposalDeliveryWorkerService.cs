using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Communications;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Submissions;

public sealed class ProposalDeliveryWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly WorkerOptions _options;
    private readonly ILogger<ProposalDeliveryWorkerService> _logger;

    public ProposalDeliveryWorkerService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IOptions<WorkerOptions> options,
        ILogger<ProposalDeliveryWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
                var notificationDelivery = scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>();
                var processed = 0;

                while (processed < Math.Clamp(_options.MaxProposalDeliveriesPerPoll, 1, 100))
                {
                    var dispatch = await ClaimNextAsync(connectionFactory, _options.ProposalDeliveryClaimLeaseMinutes, stoppingToken);
                    if (dispatch is null) break;

                    await ProcessAsync(connectionFactory, notificationDelivery, dispatch, stoppingToken);
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
       dispatch.DeliveryCategoryCode,dispatch.DeliveryTypeCode,dispatch.EntityName,dispatch.EntityId,dispatch.AccountId AS DispatchAccountId,dispatch.Subject,dispatch.HtmlContent AS DispatchHtmlContent,dispatch.DocumentId AS DispatchDocumentId,
       dispatch.ProposalVersionNumber, dispatch.DeliveryMethodCode, dispatch.Recipient, dispatch.AttemptCount, dispatch.MaxAttempts,
       provider.ProposalDeliveryProviderId, provider.ProviderCode, provider.HandlerCode, provider.DisplayName AS ProviderName,
       provider.EndpointUri, provider.SenderAddress, provider.SecretReference, provider.ConfigurationJson,
       provider.IsConfigured, provider.RetryDelaySeconds,
       COALESCE(dispatch.Subject,proposal.Title) Title, COALESCE(dispatch.HtmlContent,proposal.HtmlContent) HtmlContent, proposal.PdfUrl, COALESCE(dispatch.DocumentId,proposal.DocumentId) DocumentId,
       COALESCE(dispatch.AccountId,submission.AccountId) AccountId, submission.SubmissionNumber, account.AccountName,
       tenant.TenantName AS AgencyName, agency.ContactEmail AS AgencyEmail, agency.ContactPhone AS AgencyPhone,
       COALESCE(NULLIF(assignedUser.DisplayName, N''), NULLIF(assignedUser.FullName, N''), assignedUser.UserName) AS AssignedPersonName,
       assignedUser.Email AS AssignedPersonEmail, assignedUser.PhoneNumber AS AssignedPersonPhone,
       (SELECT q.QuoteId, q.QuoteNumber, carrier.CarrierName, q.AnnualPremium, q.TaxesAndFees, q.BrokerFee,
               q.MinimumEarnedPremium, q.PaymentTerms, q.TriaIncluded, q.IsBindable, q.CarrierRating, q.EffectiveDate, q.ExpiresDateUtc,
               JSON_QUERY((SELECT line.LineOfBusiness, line.Status, line.QuotedPremium, line.Deductible, line.[Limit],
                                  line.CoverageForms, line.Subjectivities, line.Exclusions, line.PaymentTerms,
                                  line.MinimumEarnedPremium, line.TaxesAndFees, line.BrokerFee, line.TriaIncluded,
                                  line.IsBindable, line.CoverageNotes, line.SortOrder
                           FROM Submissions.QuoteLine line
                           WHERE line.QuoteId = q.QuoteId AND line.TenantId = dispatch.TenantId AND line.IsDeleted = 0
                           ORDER BY line.SortOrder, line.LineOfBusiness FOR JSON PATH)) AS Lines
        FROM Submissions.ProposalQuote pq
        INNER JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.IsDeleted = 0
        INNER JOIN Core.Carrier carrier ON carrier.CarrierId = q.CarrierId AND carrier.IsDeleted = 0
        WHERE pq.ProposalId = dispatch.ProposalId AND pq.TenantId = dispatch.TenantId AND pq.IsDeleted = 0
        ORDER BY pq.SortOrder FOR JSON PATH) AS PackageJson
FROM Submissions.ProposalDeliveryDispatch dispatch
INNER JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = dispatch.ProposalDeliveryProviderId
LEFT JOIN Submissions.Proposal proposal
  ON proposal.ProposalId = dispatch.ProposalId AND proposal.TenantId = dispatch.TenantId AND proposal.IsDeleted = 0
LEFT JOIN Submissions.Submission submission
  ON submission.SubmissionId = dispatch.SubmissionId AND submission.TenantId = dispatch.TenantId AND submission.IsDeleted = 0
INNER JOIN Core.Tenant tenant
  ON tenant.TenantId = dispatch.TenantId AND tenant.IsDeleted = 0
LEFT JOIN Client.Account account
  ON account.AccountId = COALESCE(dispatch.AccountId,submission.AccountId) AND account.TenantId = dispatch.TenantId AND account.IsDeleted = 0
LEFT JOIN Agency.Profile agency
  ON agency.TenantId = dispatch.TenantId AND agency.IsDeleted = 0
LEFT JOIN IAM.[User] assignedUser
  ON assignedUser.UserId = submission.AssignedToUserId AND assignedUser.TenantId = submission.TenantId AND assignedUser.IsDeleted = 0
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ProposalDeliveryWorkItem>(new CommandDefinition(sql, new { ClaimLeaseMinutes = Math.Clamp(claimLeaseMinutes, 5, 1440) }, cancellationToken: cancellationToken));
    }

    private async Task ProcessAsync(ISqlConnectionFactory connectionFactory, INotificationDeliveryService notificationDelivery, ProposalDeliveryWorkItem dispatch, CancellationToken cancellationToken)
    {
        try
        {
            if (!dispatch.IsConfigured && !string.Equals(dispatch.HandlerCode,"Smtp",StringComparison.OrdinalIgnoreCase))
            {
                await MarkConfigurationRequiredAsync(connectionFactory, dispatch, "PROVIDER_NOT_CONFIGURED", $"{dispatch.ProviderName} requires tenant configuration.", cancellationToken);
                return;
            }

            var result = dispatch.HandlerCode switch
            {
                "Smtp" => await SendEmailAsync(notificationDelivery,dispatch,cancellationToken),
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
            var deliveryError = ClassifyDeliveryException(dispatch, ex);
            _logger.LogError(ex,
                "Proposal delivery failed for dispatch {DispatchId}. Provider={ProviderName}; Handler={HandlerCode}; Recipient={Recipient}; ErrorCode={ErrorCode}; ErrorMessage={ErrorMessage}",
                dispatch.ProposalDeliveryDispatchId,
                dispatch.ProviderName,
                dispatch.HandlerCode,
                dispatch.Recipient,
                deliveryError.ErrorCode,
                deliveryError.ErrorMessage);
            await MarkFailedOrRetryAsync(connectionFactory, dispatch, ex, cancellationToken);
        }
    }

    private static async Task<DeliveryResult> SendEmailAsync(INotificationDeliveryService notificationDelivery,ProposalDeliveryWorkItem dispatch,CancellationToken cancellationToken)
    {
        var attachments=dispatch.DocumentId.HasValue?new[]{new NotificationAttachmentRequest(dispatch.DocumentId,null,$"Proposal-{dispatch.ProposalVersionNumber}.pdf","application/pdf")}:[];
        var notificationId=await notificationDelivery.QueueEmailAsync(new(dispatch.TenantId,dispatch.Recipient,null,BuildEmailSubject(dispatch),BuildEmailHtml(dispatch),true,"PROPOSAL_DELIVERY",dispatch.EntityName??"Proposal",dispatch.EntityId??dispatch.ProposalId,$"proposal-delivery:{dispatch.ProposalDeliveryDispatchId}","High",dispatch.DeliveryCategoryCode,null,attachments),cancellationToken);
        var result=await notificationDelivery.DeliverAsync(dispatch.TenantId,notificationId,cancellationToken);
        if(!string.Equals(result.StatusCode,"Sent",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(result.ErrorMessage??"Notification Platform could not deliver the proposal email.");
        return DeliveryResult.Sent(result.ExternalDeliveryId??$"notification:{notificationId}");
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

IF @DeliveryCategoryCode=N'Proposal'
BEGIN
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
            dispatch.DeliveryCategoryCode,
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
        var deliveryError = ClassifyDeliveryException(dispatch, exception);
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
            deliveryError.ErrorCode,
            ErrorMessage = deliveryError.ErrorMessage.Length > 2000 ? deliveryError.ErrorMessage[..2000] : deliveryError.ErrorMessage
        }, cancellationToken: cancellationToken));
    }

    private static (string ErrorCode, string ErrorMessage) ClassifyDeliveryException(ProposalDeliveryWorkItem dispatch, Exception exception)
    {
        if (exception is HttpRequestException http && http.StatusCode.HasValue)
        {
            return ($"HTTP_{(int)http.StatusCode.Value}", exception.Message);
        }

        if (dispatch.HandlerCode.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            var fullMessage = exception.ToString();
            if (ContainsAny(fullMessage, "authentication", "authenticated", "password", "credential", "5.7", "username", "not accepted", "logon", "login"))
            {
                return ("SMTP_AUTH_FAILED", "Notification Platform SMTP authentication failed. Verify the database provider credential reference and its env:// environment variable, then retry delivery.");
            }

            if (ContainsAny(fullMessage, "RemoteCertificateNameMismatch", "certificate name mismatch", "remote certificate is invalid"))
            {
                return ("SMTP_CERTIFICATE_NAME_MISMATCH", "SMTP TLS certificate name mismatch. The endpoint host does not match the certificate returned by the SMTP server. Do not use mail.agencybinder.com if it is only a DNS alias; set the Proposal Delivery Provider Endpoint URI to smtp://netsol-smtp-oxcs.hostingplatform.com:587, then retry delivery.");
            }

            if (ContainsAny(fullMessage, "secure connection", "ssl", "tls", "starttls", "certificate"))
            {
                return ("SMTP_TLS_FAILED", "SMTP TLS/SSL negotiation failed. Verify the endpoint port and enableSsl provider JSON. For NetworkSolutions use port 587 with {\"enableSsl\":\"true\"}.");
            }

            if (ContainsAny(fullMessage, "no such host", "actively refused", "timed out", "timeout", "unable to connect", "connection"))
            {
                return ("SMTP_CONNECTION_FAILED", "SMTP connection failed. Verify the Endpoint URI host and port are reachable from Ams.Worker. For Network Solutions use smtp://netsol-smtp-oxcs.hostingplatform.com:587.");
            }

            if (ContainsAny(fullMessage, "mailbox unavailable", "recipient", "5.1.1", "user unknown", "invalid mailbox"))
            {
                return ("SMTP_RECIPIENT_REJECTED", "SMTP recipient was rejected by the mail server. Verify the proposal recipient email address, then edit the recipient or resend the proposal.");
            }

            return ("SMTP_SEND_FAILED", $"SMTP send failed. {exception.GetBaseException().Message}");
        }

        return (exception.GetType().Name, exception.Message);
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string BuildEmailSubject(ProposalDeliveryWorkItem dispatch)
    {
        if (!string.IsNullOrWhiteSpace(dispatch.Subject)) return dispatch.Subject;
        var accountName = string.IsNullOrWhiteSpace(dispatch.AccountName) ? null : dispatch.AccountName.Trim();
        return accountName is null
            ? $"Insurance Proposal | {dispatch.Title}"
            : $"Insurance Proposal for {accountName} | {dispatch.Title}";
    }

    private static string BuildEmailHtml(ProposalDeliveryWorkItem dispatch)
    {
        if (!string.Equals(dispatch.DeliveryCategoryCode, "Proposal", StringComparison.OrdinalIgnoreCase))
            return dispatch.HtmlContent ?? $"<p>{WebUtility.HtmlEncode(dispatch.Title)}</p>";
        var title = WebUtility.HtmlEncode(dispatch.Title);
        var accountName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(dispatch.AccountName) ? "Valued Client" : dispatch.AccountName);
        var submissionNumber = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(dispatch.SubmissionNumber) ? (dispatch.SubmissionId?.ToString("N")[..8].ToUpperInvariant() ?? "N/A") : dispatch.SubmissionNumber);
        var agencyName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(dispatch.AgencyName) ? "Your Insurance Agency" : dispatch.AgencyName);
        var assignedPersonName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(dispatch.AssignedPersonName) ? "Your Agency Representative" : dispatch.AssignedPersonName);
        var assignedPersonEmail = BuildEmailContactLink(dispatch.AssignedPersonEmail, dispatch.AgencyEmail);
        var assignedPersonPhone = BuildPhoneContactLink(dispatch.AssignedPersonPhone, dispatch.AgencyPhone);
        var proposalContent = BuildPackageHtml(dispatch);

        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>{{title}}</title>
  <style>
    .proposal-content h1 { display:none!important; }
    .proposal-content p { margin:0 0 16px;color:#475569;font-size:15px;line-height:1.65; }
    .proposal-content table { width:100%;border-collapse:collapse;margin-top:16px;font-size:13px; }
    .proposal-content th { padding:11px 10px;background:#123b67;color:#fff;text-align:left;font-size:11px;letter-spacing:.35px;text-transform:uppercase; }
    .proposal-content td { padding:11px 10px;border-bottom:1px solid #e2e8f0;color:#334155;vertical-align:top; }
    .proposal-content tr:nth-child(even) td { background:#f8fafc; }
    @media only screen and (max-width:620px) {
      .proposal-content table { display:block;overflow-x:auto;white-space:nowrap; }
    }
  </style>
</head>
<body style="margin:0;padding:0;background:#eef3f8;font-family:Arial,'Helvetica Neue',sans-serif;color:#172033;">
  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="width:100%;background:#eef3f8;">
    <tr>
      <td align="center" style="padding:28px 12px;">
        <table role="presentation" width="680" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:680px;background:#ffffff;border:1px solid #dbe5f0;border-radius:18px;overflow:hidden;box-shadow:0 12px 36px rgba(15,23,42,.10);">
          <tr>
            <td style="padding:26px 30px;background:#123b67;background-image:linear-gradient(135deg,#123b67,#176b87);color:#ffffff;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0">
                <tr>
                  <td>
                     <div style="font-size:12px;font-weight:700;letter-spacing:1.4px;text-transform:uppercase;color:#bfe7f5;">{{agencyName}}</div>
                    <div style="margin-top:7px;font-size:27px;line-height:1.2;font-weight:800;">Your Insurance Proposal</div>
                    <div style="margin-top:8px;font-size:14px;line-height:1.5;color:#e4f4fa;">Prepared securely for {{accountName}}</div>
                  </td>
                  <td align="right" valign="top" style="font-size:12px;color:#d9edf6;white-space:nowrap;">Submission {{submissionNumber}}<br>Proposal v{{dispatch.ProposalVersionNumber}}</td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style="padding:28px 30px 18px;">
              <div style="font-size:14px;line-height:1.65;color:#475569;">Hello,</div>
              <div style="margin-top:8px;font-size:16px;line-height:1.65;color:#334155;">Please review the insurance proposal prepared for <strong style="color:#0f172a;">{{accountName}}</strong>. The proposal details and available quote options are summarized below.</div>
              <div style="margin-top:22px;padding:18px 20px;border:1px solid #dbeafe;border-left:5px solid #2563eb;border-radius:12px;background:#f8fbff;">
                <div style="font-size:11px;font-weight:800;letter-spacing:1px;text-transform:uppercase;color:#2563eb;">Proposal</div>
                <div style="margin-top:5px;font-size:21px;line-height:1.35;font-weight:800;color:#0f172a;">{{title}}</div>
              </div>
            </td>
          </tr>
          <tr>
            <td style="padding:0 30px 26px;">
              <div class="proposal-content" style="font-size:15px;line-height:1.6;color:#334155;">
                {{proposalContent}}
              </div>
            </td>
          </tr>
          <tr>
            <td style="padding:20px 30px;border-top:1px solid #e2e8f0;background:#f8fafc;">
              <div style="font-size:11px;font-weight:800;letter-spacing:1px;text-transform:uppercase;color:#2563eb;">Your proposal contact</div>
              <div style="margin-top:5px;font-size:16px;font-weight:800;color:#0f172a;">{{assignedPersonName}}</div>
              <div style="margin-top:7px;font-size:13px;line-height:1.7;color:#475569;">{{assignedPersonEmail}}{{assignedPersonPhone}}</div>
              <div style="margin-top:10px;font-size:13px;line-height:1.6;color:#475569;"><strong style="color:#0f172a;">Questions?</strong> Contact {{assignedPersonName}} to review coverage options and next steps.</div>
              <div style="margin-top:12px;font-size:11px;line-height:1.55;color:#7c8a9d;">This proposal is provided for review and does not bind or alter coverage. Coverage is effective only after documented authorization and carrier confirmation.</div>
              <div style="margin-top:14px;font-size:11px;color:#94a3b8;">AgencyBinder · Professional insurance workflow and client service</div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";
    }

    private static string BuildEmailContactLink(string? assignedEmail, string? agencyEmail)
    {
        var email = string.IsNullOrWhiteSpace(assignedEmail) ? agencyEmail : assignedEmail;
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        var encodedEmail = WebUtility.HtmlEncode(email.Trim());
        return $"<a href=\"mailto:{encodedEmail}\" style=\"color:#2563eb;font-weight:700;text-decoration:none;\">{encodedEmail}</a>";
    }

    private static string BuildPhoneContactLink(string? assignedPhone, string? agencyPhone)
    {
        var phone = string.IsNullOrWhiteSpace(assignedPhone) ? agencyPhone : assignedPhone;
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var encodedPhone = WebUtility.HtmlEncode(phone.Trim());
        var separator = "<span style=\"padding:0 8px;color:#94a3b8;\">&middot;</span>";
        return $"{separator}<a href=\"tel:{encodedPhone}\" style=\"color:#2563eb;font-weight:700;text-decoration:none;\">{encodedPhone}</a>";
    }

    private static string BuildPackageHtml(ProposalDeliveryWorkItem dispatch)
    {
        var packages = string.IsNullOrWhiteSpace(dispatch.PackageJson)
            ? []
            : JsonSerializer.Deserialize<List<ProposalEmailPackage>>(dispatch.PackageJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (packages.Count == 0) return ExtractBodyContent(dispatch.HtmlContent);

        var html = new StringBuilder("<div style=\"font-size:11px;font-weight:800;letter-spacing:1px;text-transform:uppercase;color:#2563eb;\">Coverage packages</div><div style=\"margin:5px 0 16px;font-size:22px;font-weight:800;color:#0f172a;\">Detailed quote options</div>");
        foreach (var package in packages)
        {
            var lines = package.Lines.Count == 0
                ? [new ProposalEmailLine { LineOfBusiness = "Package premium", Status = "Quoted", QuotedPremium = package.AnnualPremium, TaxesAndFees = package.TaxesAndFees, BrokerFee = package.BrokerFee }]
                : package.Lines;
            var premiumTotal = lines.Sum(x => x.QuotedPremium);
            var taxesTotal = lines.Sum(x => x.TaxesAndFees ?? 0);
            var brokerFeeTotal = lines.Sum(x => x.BrokerFee ?? 0);

            html.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin:0 0 18px;border:1px solid #dbe5f0;border-radius:12px;overflow:hidden;\">");
            html.Append($"<tr><td colspan=\"6\" style=\"padding:14px 16px;background:#123b67;color:#fff;\"><strong style=\"font-size:16px;color:#fff;\">{WebUtility.HtmlEncode(package.CarrierName)}</strong><span style=\"float:right;color:#d9edf6;\">Quote {WebUtility.HtmlEncode(package.QuoteNumber)}</span><div style=\"margin-top:5px;font-size:11px;color:#c7eaf4;\">Effective {FormatEmailDate(package.EffectiveDate)} &middot; Expires {FormatEmailDate(package.ExpiresDateUtc)} &middot; Rating {WebUtility.HtmlEncode(DisplayEmailValue(package.CarrierRating))}</div></td></tr>");
            html.Append("<tr><th style=\"padding:9px;background:#eaf2fb;color:#123b67;text-align:left;\">Line</th><th style=\"padding:9px;background:#eaf2fb;color:#123b67;text-align:right;\">Limit</th><th style=\"padding:9px;background:#eaf2fb;color:#123b67;text-align:right;\">Deductible</th><th style=\"padding:9px;background:#eaf2fb;color:#123b67;text-align:right;\">Premium</th><th style=\"padding:9px;background:#eaf2fb;color:#123b67;text-align:right;\">Fees</th><th style=\"padding:9px;background:#eaf2fb;color:#123b67;text-align:right;\">Total</th></tr>");
            foreach (var line in lines.OrderBy(x => x.SortOrder))
            {
                var fees = (line.TaxesAndFees ?? 0) + (line.BrokerFee ?? 0);
                html.Append($"<tr><td style=\"padding:10px;border-top:1px solid #e2e8f0;color:#0f172a;\"><strong>{WebUtility.HtmlEncode(line.LineOfBusiness)}</strong><div style=\"font-size:11px;color:#64748b;\">{WebUtility.HtmlEncode(line.Status)}{BuildEmailLineNotes(line)}</div></td><td style=\"padding:10px;border-top:1px solid #e2e8f0;text-align:right;\">{FormatEmailMoney(line.Limit)}</td><td style=\"padding:10px;border-top:1px solid #e2e8f0;text-align:right;\">{FormatEmailMoney(line.Deductible)}</td><td style=\"padding:10px;border-top:1px solid #e2e8f0;text-align:right;\">{line.QuotedPremium:C2}</td><td style=\"padding:10px;border-top:1px solid #e2e8f0;text-align:right;\">{fees:C2}</td><td style=\"padding:10px;border-top:1px solid #e2e8f0;text-align:right;font-weight:800;\">{line.QuotedPremium + fees:C2}</td></tr>");
            }
            html.Append($"<tr><td colspan=\"3\" style=\"padding:11px;background:#e8f1ff;color:#123b67;font-weight:800;\">Package totals</td><td style=\"padding:11px;background:#e8f1ff;text-align:right;font-weight:800;\">{premiumTotal:C2}</td><td style=\"padding:11px;background:#e8f1ff;text-align:right;font-weight:800;\">{taxesTotal + brokerFeeTotal:C2}</td><td style=\"padding:11px;background:#e8f1ff;text-align:right;font-weight:800;color:#123b67;\">{premiumTotal + taxesTotal + brokerFeeTotal:C2}</td></tr>");
            html.Append($"<tr><td colspan=\"6\" style=\"padding:11px 14px;background:#f8fafc;color:#475569;font-size:11px;\"><strong>Payment:</strong> {WebUtility.HtmlEncode(DisplayEmailValue(lines.Select(x => x.PaymentTerms).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? package.PaymentTerms))} &middot; <strong>MEP:</strong> {FormatEmailMoney(lines.Sum(x => x.MinimumEarnedPremium ?? 0) is var mep && mep > 0 ? mep : package.MinimumEarnedPremium)} &middot; <strong>TRIA:</strong> {FormatEmailBoolean(lines.Select(x => x.TriaIncluded).FirstOrDefault(x => x.HasValue) ?? package.TriaIncluded)} &middot; <strong>Bindable:</strong> {(lines.All(x => x.IsBindable) ? "Yes" : "No")}</td></tr></table>");
        }
        return html.ToString();
    }

    private static string BuildEmailLineNotes(ProposalEmailLine line)
    {
        var notes = new[] { line.CoverageNotes, line.CoverageForms, line.Subjectivities, line.Exclusions }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(WebUtility.HtmlEncode);
        var value = string.Join(" &middot; ", notes);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"<br>{value}";
    }

    private static string DisplayEmailValue(string? value) => string.IsNullOrWhiteSpace(value) ? "Not provided" : value;
    private static string FormatEmailMoney(decimal? value) => value.HasValue ? value.Value.ToString("C2") : "Not provided";
    private static string FormatEmailDate(DateTime? value) => value.HasValue ? value.Value.ToString("MMM d, yyyy") : "Not provided";
    private static string FormatEmailBoolean(bool? value) => value switch { true => "Included", false => "Not included", _ => "Not provided" };

    private static string ExtractBodyContent(string? htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent)) return "<p style=\"margin:0;color:#475569;\">Your proposal package is ready for review.</p>";

        var bodyStart = htmlContent.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyStart < 0) return htmlContent;

        var contentStart = htmlContent.IndexOf('>', bodyStart);
        if (contentStart < 0) return htmlContent;

        var bodyEnd = htmlContent.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyEnd > contentStart ? htmlContent[(contentStart + 1)..bodyEnd] : htmlContent[(contentStart + 1)..];
    }

    private string? ResolveSecret(string? secretReference)
    {
        if (string.IsNullOrWhiteSpace(secretReference)) return null;
        secretReference = secretReference.Trim();

        var environmentValue = Environment.GetEnvironmentVariable(secretReference);
        if (!string.IsNullOrWhiteSpace(environmentValue)) return environmentValue;

        var configurationValue = _configuration[secretReference];
        if (!string.IsNullOrWhiteSpace(configurationValue)) return configurationValue;

        var secretsValue = _configuration[$"Secrets:{secretReference}"];
        if (!string.IsNullOrWhiteSpace(secretsValue)) return secretsValue;

        var proposalDeliverySecretsValue = _configuration[$"ProposalDelivery:Secrets:{secretReference}"];
        if (!string.IsNullOrWhiteSpace(proposalDeliverySecretsValue)) return proposalDeliverySecretsValue;

        return null;
    }

    private sealed class ProposalDeliveryWorkItem
    {
        public Guid ProposalDeliveryDispatchId { get; set; }
        public Guid TenantId { get; set; }
        public Guid? SubmissionId { get; set; }
        public Guid? ProposalId { get; set; }
        public Guid AccountId { get; set; }
        public Guid ProposalDeliveryProviderId { get; set; }
        public int? ProposalVersionNumber { get; set; }
        public string DeliveryCategoryCode { get; set; } = "Proposal";
        public string DeliveryTypeCode { get; set; } = "ProposalPackage";
        public string? EntityName { get; set; }
        public Guid? EntityId { get; set; }
        public string? Subject { get; set; }
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
        public string? AccountName { get; set; }
        public string? SubmissionNumber { get; set; }
        public string? AgencyName { get; set; }
        public string? AgencyEmail { get; set; }
        public string? AgencyPhone { get; set; }
        public string? AssignedPersonName { get; set; }
        public string? AssignedPersonEmail { get; set; }
        public string? AssignedPersonPhone { get; set; }
        public string? PackageJson { get; set; }
        public string? HtmlContent { get; set; }
        public string? PdfUrl { get; set; }
        public Guid? DocumentId { get; set; }
    }

    private sealed class ProposalEmailPackage
    {
        public string QuoteNumber { get; set; } = string.Empty;
        public string CarrierName { get; set; } = string.Empty;
        public decimal AnnualPremium { get; set; }
        public decimal? TaxesAndFees { get; set; }
        public decimal? BrokerFee { get; set; }
        public decimal? MinimumEarnedPremium { get; set; }
        public string? PaymentTerms { get; set; }
        public bool? TriaIncluded { get; set; }
        public bool IsBindable { get; set; }
        public string? CarrierRating { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpiresDateUtc { get; set; }
        public List<ProposalEmailLine> Lines { get; set; } = [];
    }

    private sealed class ProposalEmailLine
    {
        public string LineOfBusiness { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal QuotedPremium { get; set; }
        public decimal? Deductible { get; set; }
        public decimal? Limit { get; set; }
        public string? CoverageForms { get; set; }
        public string? Subjectivities { get; set; }
        public string? Exclusions { get; set; }
        public string? PaymentTerms { get; set; }
        public decimal? MinimumEarnedPremium { get; set; }
        public decimal? TaxesAndFees { get; set; }
        public decimal? BrokerFee { get; set; }
        public bool? TriaIncluded { get; set; }
        public bool IsBindable { get; set; }
        public string? CoverageNotes { get; set; }
        public int SortOrder { get; set; }
    }

    private sealed record DeliveryResult(bool IsConfigurationRequired, string? ExternalDeliveryId, string? ResponseJson, string? ErrorCode, string? ErrorMessage)
    {
        public static DeliveryResult Sent(string externalDeliveryId, string? responseJson = null) => new(false, externalDeliveryId, responseJson, null, null);
        public static DeliveryResult ConfigurationRequired(string errorCode, string errorMessage) => new(true, null, null, errorCode, errorMessage);
    }
}
