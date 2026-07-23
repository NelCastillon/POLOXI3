using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Submissions;

public sealed class ApiRatingConnectorWorkerService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WorkerOptions _options;
    private readonly ILogger<ApiRatingConnectorWorkerService> _logger;

    public ApiRatingConnectorWorkerService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, IOptions<WorkerOptions> options, ILogger<ApiRatingConnectorWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS API rating connector worker started with {PollIntervalSeconds}s polling interval.", _options.ApiRatingPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = new ApiRatingWorkerSettings(_options.ApiRatingPollIntervalSeconds, _options.MaxApiRatingTransmissionsPerPoll, true);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                settings = await GetWorkerSettingsAsync(connectionFactory, stoppingToken);

                if (settings.Enabled)
                {
                    var processed = await ProcessPendingTransmissionsAsync(connectionFactory, settings.MaxTransmissionsPerPoll, stoppingToken);
                    if (processed > 0)
                    {
                        _logger.LogInformation("API rating connector worker processed {TransmissionCount} rating transmissions.", processed);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API rating connector worker polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, settings.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task<ApiRatingWorkerSettings> GetWorkerSettingsAsync(ISqlConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
IF OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NULL
BEGIN
    SELECT CAST(1 AS bit) AS Enabled, @PollIntervalSeconds AS PollIntervalSeconds, @MaxTransmissionsPerPoll AS MaxTransmissionsPerPoll;
    RETURN;
END;

SELECT
    CAST(CASE WHEN LOWER(COALESCE(MAX(CASE WHEN SettingCode = N'API_RATING_WORKER_ENABLED' THEN COALESCE(SettingValue, DefaultValue) END), N'true')) IN (N'true', N'1', N'yes', N'on') THEN 1 ELSE 0 END AS bit) AS Enabled,
    COALESCE(TRY_CONVERT(INT, MAX(CASE WHEN SettingCode = N'API_RATING_WORKER_POLL_SECONDS' THEN COALESCE(SettingValue, DefaultValue) END)), @PollIntervalSeconds) AS PollIntervalSeconds,
    COALESCE(TRY_CONVERT(INT, MAX(CASE WHEN SettingCode = N'API_RATING_WORKER_BATCH_SIZE' THEN COALESCE(SettingValue, DefaultValue) END)), @MaxTransmissionsPerPoll) AS MaxTransmissionsPerPoll
FROM Agency.CarrierSetting
WHERE CarrierId IS NULL
  AND IsActive = 1
  AND IsDeleted = 0
  AND SettingCode IN (N'API_RATING_WORKER_ENABLED', N'API_RATING_WORKER_POLL_SECONDS', N'API_RATING_WORKER_BATCH_SIZE');";

        var settings = await cn.QuerySingleAsync<ApiRatingWorkerSettings>(new CommandDefinition(sql, new { PollIntervalSeconds = _options.ApiRatingPollIntervalSeconds, MaxTransmissionsPerPoll = _options.MaxApiRatingTransmissionsPerPoll }, cancellationToken: cancellationToken));
        return new ApiRatingWorkerSettings(Math.Clamp(settings.PollIntervalSeconds, 10, 3600), Math.Clamp(settings.MaxTransmissionsPerPoll, 1, 100), settings.Enabled);
    }

    private async Task<int> ProcessPendingTransmissionsAsync(ISqlConnectionFactory connectionFactory, int maxTransmissions, CancellationToken cancellationToken)
    {
        var claimed = await ClaimPendingTransmissionsAsync(connectionFactory, maxTransmissions, cancellationToken);
        var processed = 0;

        foreach (var transmission in claimed)
        {
            try
            {
                var response = await ExecuteRatingAsync(transmission, cancellationToken);
                if (ShouldCreateQuote(response))
                {
                    await PersistSuccessfulRatingAsync(connectionFactory, transmission, response, cancellationToken);
                }
                else
                {
                    await PersistMarketOutcomeAsync(connectionFactory, transmission, response, cancellationToken);
                }
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "API rating transmission {CarrierTransmissionId} failed: {Message}", transmission.CarrierTransmissionId, ex.Message);
                await PersistFailedRatingAsync(connectionFactory, transmission, ex.Message, cancellationToken);
                processed++;
            }
        }

        return processed;
    }

    private static async Task<IReadOnlyList<ApiRatingTransmission>> ClaimPendingTransmissionsAsync(ISqlConnectionFactory connectionFactory, int maxTransmissions, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
IF OBJECT_ID(N'Submissions.CarrierTransmission', N'U') IS NULL
BEGIN
    SELECT TOP 0 CAST(NULL AS UNIQUEIDENTIFIER) AS CarrierTransmissionId;
    RETURN;
END;

DECLARE @WorkerId NVARCHAR(120) = CONCAT(HOST_NAME(), N':ApiRatingConnector');
DECLARE @Batch TABLE (CarrierTransmissionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

;WITH NextBatch AS
(
    SELECT TOP (@MaxTransmissions) t.CarrierTransmissionId
    FROM Submissions.CarrierTransmission t WITH (READPAST, UPDLOCK, ROWLOCK)
    OUTER APPLY
    (
        SELECT TOP 1 IsDisabled = CAST(1 AS bit)
        FROM Agency.CarrierSetting setting
        WHERE setting.TenantId = t.TenantId
          AND setting.CarrierId IS NULL
          AND setting.SettingCode = N'API_RATING_WORKER_ENABLED'
          AND setting.IsActive = 1
          AND setting.IsDeleted = 0
          AND LOWER(COALESCE(setting.SettingValue, setting.DefaultValue, N'true')) IN (N'false', N'0', N'no', N'off')
    ) disabled
    WHERE t.IsDeleted = 0
      AND t.TransmissionTypeCode = N'QuoteRequest'
      AND t.ChannelCode = N'API'
      AND t.StatusCode IN (N'AwaitingExternalConnector', N'Queued', N'Failed')
      AND t.AttemptCount < 5
      AND disabled.IsDisabled IS NULL
    ORDER BY t.CreatedDateUtc
)
UPDATE t
SET StatusCode = N'Processing',
    AttemptCount = AttemptCount + 1,
    LastAttemptDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    LastError = NULL
OUTPUT inserted.CarrierTransmissionId INTO @Batch
FROM Submissions.CarrierTransmission t
INNER JOIN NextBatch b ON b.CarrierTransmissionId = t.CarrierTransmissionId;

SELECT t.CarrierTransmissionId, t.TenantId, t.SubmissionId, t.SubmissionMarketId, t.CarrierId, t.CarrierExternalConnectorId,
       t.PayloadJson, t.EndpointUri, c.EndpointUri AS ConnectorEndpointUri, c.ConfigurationJson,
       COALESCE(NULLIF(ratingEndpoint.SettingValue, N''), NULLIF(ratingEndpoint.DefaultValue, N''), NULLIF(t.EndpointUri, N''), NULLIF(c.EndpointUri, N'')) AS RatingEndpointUri,
       COALESCE(NULLIF(authMode.SettingValue, N''), NULLIF(authMode.DefaultValue, N''), N'None') AS AuthMode,
       COALESCE(NULLIF(apiKey.SettingValue, N''), NULLIF(apiKey.DefaultValue, N'')) AS ApiKey,
       COALESCE(NULLIF(apiKeyHeader.SettingValue, N''), NULLIF(apiKeyHeader.DefaultValue, N''), N'x-api-key') AS ApiKeyHeader,
       COALESCE(NULLIF(bearerToken.SettingValue, N''), NULLIF(bearerToken.DefaultValue, N'')) AS BearerToken,
       COALESCE(TRY_CONVERT(INT, COALESCE(NULLIF(timeoutSeconds.SettingValue, N''), NULLIF(timeoutSeconds.DefaultValue, N''))), 30) AS TimeoutSeconds
FROM Submissions.CarrierTransmission t
INNER JOIN @Batch b ON b.CarrierTransmissionId = t.CarrierTransmissionId
LEFT JOIN Agency.CarrierExternalConnector c ON c.CarrierExternalConnectorId = t.CarrierExternalConnectorId AND c.IsDeleted = 0
OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = t.TenantId AND CarrierId = t.CarrierId AND SettingCode = N'CARRIER_RATING_API_ENDPOINT' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) ratingEndpoint
OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = t.TenantId AND CarrierId = t.CarrierId AND SettingCode = N'CARRIER_RATING_API_AUTH_MODE' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) authMode
OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = t.TenantId AND CarrierId = t.CarrierId AND SettingCode = N'CARRIER_RATING_API_KEY' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) apiKey
OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = t.TenantId AND CarrierId = t.CarrierId AND SettingCode = N'CARRIER_RATING_API_KEY_HEADER' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) apiKeyHeader
OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = t.TenantId AND CarrierId = t.CarrierId AND SettingCode = N'CARRIER_RATING_BEARER_TOKEN' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) bearerToken
OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = t.TenantId AND CarrierId = t.CarrierId AND SettingCode = N'CARRIER_RATING_REQUEST_TIMEOUT_SECONDS' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) timeoutSeconds;";

        return (await cn.QueryAsync<ApiRatingTransmission>(new CommandDefinition(sql, new { MaxTransmissions = Math.Max(1, maxTransmissions) }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task<ApiRatingResponse> ExecuteRatingAsync(ApiRatingTransmission transmission, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transmission.RatingEndpointUri))
        {
            throw new InvalidOperationException("No active DB-backed rating API endpoint is configured for this carrier.");
        }

        var client = _httpClientFactory.CreateClient(nameof(ApiRatingConnectorWorkerService));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(transmission.TimeoutSeconds, 5, 300));

        using var request = new HttpRequestMessage(HttpMethod.Post, transmission.RatingEndpointUri)
        {
            Content = JsonContent.Create(ParsePayload(transmission.PayloadJson), options: JsonOptions)
        };

        if (string.Equals(transmission.AuthMode, "ApiKey", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(transmission.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(string.IsNullOrWhiteSpace(transmission.ApiKeyHeader) ? "x-api-key" : transmission.ApiKeyHeader, transmission.ApiKey);
        }
        else if (string.Equals(transmission.AuthMode, "BearerToken", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(transmission.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", transmission.BearerToken);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Rating API returned {(int)response.StatusCode}: {TrimForStorage(responseText, 500)}");
        }

        return NormalizeResponse(responseText, transmission);
    }

    private static object ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new { };
        }

        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.Clone();
    }

    private static ApiRatingResponse NormalizeResponse(string responseText, ApiRatingTransmission transmission)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseText) ? "{}" : responseText);
        var root = document.RootElement;

        return new ApiRatingResponse(
            Status: ReadString(root, "status") ?? ReadString(root, "quoteStatus") ?? "Received",
            QuoteNumber: ReadString(root, "quoteNumber") ?? ReadString(root, "referenceNumber"),
            AnnualPremium: ReadDecimal(root, "annualPremium") ?? ReadDecimal(root, "premium") ?? ReadDecimal(root, "totalPremium") ?? 0m,
            Deductible: ReadDecimal(root, "deductible"),
            Limit: ReadDecimal(root, "limit"),
            CommissionPercent: ReadDecimal(root, "commissionPercent"),
            Subjectivities: ReadString(root, "subjectivities"),
            Exclusions: ReadString(root, "exclusions"),
            CarrierRating: ReadString(root, "carrierRating"),
            PaymentTerms: ReadString(root, "paymentTerms"),
            MinimumEarnedPremium: ReadDecimal(root, "minimumEarnedPremium"),
            TaxesAndFees: ReadDecimal(root, "taxesAndFees"),
            BrokerFee: ReadDecimal(root, "brokerFee"),
            TriaIncluded: ReadBool(root, "triaIncluded"),
            CoverageNotes: ReadString(root, "coverageNotes") ?? ReadString(root, "notes"),
            ExpiresDateUtc: ReadDateTime(root, "expiresDateUtc") ?? ReadDateTime(root, "expiresDate") ?? DateTime.UtcNow.AddDays(30),
            EffectiveDate: ReadDateTime(root, "effectiveDate"),
            CoverageForms: ReadString(root, "coverageForms"),
            IsBindable: ReadBool(root, "isBindable") ?? false,
            RawPayloadJson: responseText,
            CarrierReferenceNumber: ReadString(root, "carrierReferenceNumber") ?? ReadString(root, "referenceNumber") ?? $"API-{transmission.CarrierTransmissionId:N}"[..20]);
    }

    private static async Task PersistSuccessfulRatingAsync(ISqlConnectionFactory connectionFactory, ApiRatingTransmission transmission, ApiRatingResponse rating, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
DECLARE @QuoteId UNIQUEIDENTIFIER = NEWID();
DECLARE @QuoteRequestId UNIQUEIDENTIFIER = (
    SELECT TOP 1 QuoteRequestId
    FROM Submissions.QuoteRequest
    WHERE SubmissionMarketId = @SubmissionMarketId
      AND SubmissionId = @SubmissionId
      AND TenantId = @TenantId
      AND IsDeleted = 0
    ORDER BY RequestVersion DESC, RequestedDateUtc DESC);

IF @QuoteRequestId IS NULL
BEGIN
    SET @QuoteRequestId = NEWID();
    INSERT INTO Submissions.QuoteRequest
        (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestMethodCode, DeliveryMethodCode, QuoteRequestScopeCode, RequestVersion, StatusCode, RequestedDateUtc, DueDateUtc, CorrelationId, CreatedDateUtc, IsDeleted)
    VALUES
        (@QuoteRequestId, @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, N'InitialRequest', N'ApiRating', N'API', N'Package', 1, N'Submitted', SYSUTCDATETIME(), DATEADD(day, 1, SYSUTCDATETIME()), CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId)), SYSUTCDATETIME(), 0);
END;

INSERT INTO Submissions.Quote
    (QuoteId, SubmissionId, SubmissionMarketId, QuoteRequestId, CarrierId, QuoteNumber, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
     Subjectivities, Exclusions, CarrierRating, PaymentTerms, MinimumEarnedPremium, TaxesAndFees, BrokerFee, TriaIncluded,
     EffectiveDate, CoverageForms, IsBindable, CoverageNotes, QuotedDateUtc, ExpiresDateUtc, QuoteRequestDateUtc, QuoteReceivedDateUtc, ResponseVersion,
     ResponseSourceCode, CarrierReferenceNumber, CreatedDateUtc, ModifiedDateUtc, IsDeleted)
VALUES
    (@QuoteId, @SubmissionId, @SubmissionMarketId, @QuoteRequestId, @CarrierId,
     COALESCE(NULLIF(@QuoteNumber, N''), CONCAT(N'API-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @QuoteId), N'-', N''), 8))),
     @Status, @AnnualPremium, @Deductible, @Limit, @CommissionPercent, @Subjectivities, @Exclusions, @CarrierRating, @PaymentTerms,
     @MinimumEarnedPremium, @TaxesAndFees, @BrokerFee, @TriaIncluded, @EffectiveDate, @CoverageForms, @IsBindable, @CoverageNotes,
     SYSUTCDATETIME(), @ExpiresDateUtc, (SELECT RequestedDateUtc FROM Submissions.QuoteRequest WHERE QuoteRequestId = @QuoteRequestId), SYSUTCDATETIME(), 1,
     N'Api', @CarrierReferenceNumber, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);

INSERT INTO Submissions.CarrierInboundResponse
    (CarrierInboundResponseId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, CarrierTransmissionId, SourceChannelCode, ResponseTypeCode, StatusCode,
     CarrierReferenceNumber, PayloadJson, ReceivedDateUtc, ProcessedDateUtc, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, @CarrierTransmissionId, N'API', N'QuoteResponse', N'Processed',
     @CarrierReferenceNumber, COALESCE(NULLIF(@RawPayloadJson, N''), N'{}'), SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME(), 0);

UPDATE Submissions.QuoteRequest
SET StatusCode = N'Quoted',
    CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
    DeliveryMethodCode = COALESCE(DeliveryMethodCode, N'API'),
    RetryCount = COALESCE(RetryCount, 0) + 1,
    LastAttemptDateUtc = SYSUTCDATETIME(),
    DispatchedDateUtc = COALESCE(DispatchedDateUtc, (SELECT LastAttemptDateUtc FROM Submissions.CarrierTransmission WHERE CarrierTransmissionId = @CarrierTransmissionId), SYSUTCDATETIME()),
    AcknowledgedDateUtc = COALESCE(AcknowledgedDateUtc, SYSUTCDATETIME()),
    ResponseDateUtc = COALESCE(ResponseDateUtc, SYSUTCDATETIME()),
    CorrelationId = COALESCE(NULLIF(CorrelationId, N''), CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId))),
    LastError = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE QuoteRequestId = @QuoteRequestId;

UPDATE Submissions.SubmissionMarket
SET Status = N'Quoted',
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;

DECLARE @DerivedSubmissionStatus NVARCHAR(50) = CASE
    WHEN EXISTS (SELECT 1 FROM Submissions.BoundPolicy bp WHERE bp.SubmissionId = @SubmissionId AND bp.TenantId = @TenantId AND bp.IsDeleted = 0) THEN N'Bound'
    WHEN EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction pbt WHERE pbt.SubmissionId = @SubmissionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0 AND pbt.BindStatusCode IN (N'Draft', N'PendingApproval', N'ReadyToBind', N'Submitted', N'Acknowledged', N'CarrierReviewing', N'MoreInformationRequired', N'Confirmed')) THEN N'Binding'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status = N'Accepted') THEN N'Customer Accepted'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Sent', N'Pending Decision')) THEN N'Presented'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0) THEN N'Proposal Prepared'
    WHEN EXISTS (SELECT 1 FROM Submissions.Quote q WHERE q.SubmissionId = @SubmissionId AND q.IsDeleted = 0) THEN N'Quotes Received'
    WHEN EXISTS (SELECT 1 FROM Submissions.QuoteRequest qr WHERE qr.SubmissionId = @SubmissionId AND qr.TenantId = @TenantId AND qr.IsDeleted = 0) THEN N'Marketing'
    ELSE N'Ready for Marketing'
END;

UPDATE Submissions.Submission
SET Status = CASE WHEN @DerivedSubmissionStatus = N'Bound' THEN N'Bound' WHEN Status IN (N'Lost', N'Cancelled', N'Closed') THEN Status ELSE @DerivedSubmissionStatus END,
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE Submissions.CarrierTransmission
SET StatusCode = N'ResponseReceived',
    SentDateUtc = COALESCE(SentDateUtc, LastAttemptDateUtc, SYSUTCDATETIME()),
    ConfirmedDateUtc = COALESCE(ConfirmedDateUtc, SYSUTCDATETIME()),
    ExternalReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), ExternalReferenceNumber),
    LastError = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CarrierTransmissionId = @CarrierTransmissionId AND IsDeleted = 0;

INSERT INTO Submissions.CarrierTransmissionEvent
    (CarrierTransmissionEventId, TenantId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, EventPayloadJson, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @CarrierTransmissionId, @SubmissionId, @SubmissionMarketId, N'ApiRatingQuoteReceived', N'API rating connector returned normalized quote terms.', COALESCE(NULLIF(@RawPayloadJson, N''), N'{}'), SYSUTCDATETIME(), 0);

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES
    (NEWID(), @SubmissionId, @TenantId, N'ApiRatingQuoteReceived', N'API rating connector returned and persisted quote terms.', SYSUTCDATETIME(), N'Quote', @QuoteId, N'ApiRatingConnectorWorker', 0);";

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            transmission.TenantId,
            transmission.SubmissionId,
            transmission.SubmissionMarketId,
            transmission.CarrierId,
            transmission.CarrierTransmissionId,
            Status = MapQuoteStatus(rating.Status),
            rating.QuoteNumber,
            rating.AnnualPremium,
            rating.Deductible,
            rating.Limit,
            rating.CommissionPercent,
            rating.Subjectivities,
            rating.Exclusions,
            rating.CarrierRating,
            rating.PaymentTerms,
            rating.MinimumEarnedPremium,
            rating.TaxesAndFees,
            rating.BrokerFee,
            rating.TriaIncluded,
            rating.EffectiveDate,
            rating.CoverageForms,
            rating.IsBindable,
            rating.CoverageNotes,
            rating.ExpiresDateUtc,
            rating.CarrierReferenceNumber,
            rating.RawPayloadJson
        }, cancellationToken: cancellationToken));
    }

    private static async Task PersistMarketOutcomeAsync(ISqlConnectionFactory connectionFactory, ApiRatingTransmission transmission, ApiRatingResponse rating, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
DECLARE @QuoteRequestId UNIQUEIDENTIFIER = (
    SELECT TOP 1 QuoteRequestId
    FROM Submissions.QuoteRequest
    WHERE SubmissionMarketId = @SubmissionMarketId
      AND SubmissionId = @SubmissionId
      AND TenantId = @TenantId
      AND IsDeleted = 0
    ORDER BY RequestVersion DESC, RequestedDateUtc DESC);
DECLARE @MappedQuoteRequestStatus NVARCHAR(50) = @MappedStatus;

IF @QuoteRequestId IS NULL
BEGIN
    SET @QuoteRequestId = NEWID();
    INSERT INTO Submissions.QuoteRequest
        (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestMethodCode, DeliveryMethodCode, QuoteRequestScopeCode, RequestVersion, StatusCode, RequestedDateUtc, DueDateUtc, CorrelationId, CreatedDateUtc, IsDeleted)
    VALUES
        (@QuoteRequestId, @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, N'InitialRequest', N'ApiRating', N'API', N'Package', 1, N'Submitted', SYSUTCDATETIME(), DATEADD(day, 1, SYSUTCDATETIME()), CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId)), SYSUTCDATETIME(), 0);
END;

INSERT INTO Submissions.CarrierInboundResponse
    (CarrierInboundResponseId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, CarrierTransmissionId, SourceChannelCode, ResponseTypeCode, StatusCode,
     CarrierReferenceNumber, PayloadJson, ReceivedDateUtc, ProcessedDateUtc, ProcessingError, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, @CarrierTransmissionId, N'API', N'QuoteResponse', @MappedQuoteRequestStatus,
     @CarrierReferenceNumber, COALESCE(NULLIF(@RawPayloadJson, N''), N'{}'), SYSUTCDATETIME(), SYSUTCDATETIME(), NULLIF(@CoverageNotes, N''), SYSUTCDATETIME(), 0);

UPDATE Submissions.QuoteRequest
SET StatusCode = @MappedQuoteRequestStatus,
    CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
    CoverageNotes = COALESCE(NULLIF(@CoverageNotes, N''), CoverageNotes),
    DeliveryMethodCode = COALESCE(DeliveryMethodCode, N'API'),
    RetryCount = COALESCE(RetryCount, 0) + 1,
    LastAttemptDateUtc = SYSUTCDATETIME(),
    DispatchedDateUtc = COALESCE(DispatchedDateUtc, (SELECT LastAttemptDateUtc FROM Submissions.CarrierTransmission WHERE CarrierTransmissionId = @CarrierTransmissionId), SYSUTCDATETIME()),
    AcknowledgedDateUtc = CASE WHEN @MappedQuoteRequestStatus IN (N'Acknowledged', N'UnderReview', N'MoreInformationRequired', N'Quoted') THEN COALESCE(AcknowledgedDateUtc, SYSUTCDATETIME()) ELSE AcknowledgedDateUtc END,
    ResponseDateUtc = CASE WHEN @MappedQuoteRequestStatus NOT IN (N'Submitted', N'Acknowledged', N'UnderReview') THEN COALESCE(ResponseDateUtc, SYSUTCDATETIME()) ELSE ResponseDateUtc END,
    CorrelationId = COALESCE(NULLIF(CorrelationId, N''), CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId))),
    LastError = CASE WHEN @MappedQuoteRequestStatus IN (N'Failed', N'Declined') THEN COALESCE(NULLIF(@CoverageNotes, N''), LastError) ELSE LastError END,
    ClosedDateUtc = CASE WHEN @MappedQuoteRequestStatus IN (N'Declined', N'Failed', N'Expired', N'Cancelled') THEN COALESCE(ClosedDateUtc, SYSUTCDATETIME()) ELSE ClosedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE QuoteRequestId = @QuoteRequestId;

UPDATE Submissions.QuoteRequestHistory
SET StatusCode = @MappedQuoteRequestStatus,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @SubmissionMarketId
  AND IsDeleted = 0
  AND StatusCode IN (N'PendingDispatch', N'Submitted', N'Acknowledged', N'UnderReview', N'MoreInformationRequired');

UPDATE Submissions.SubmissionMarket
SET Status = CASE
        WHEN @MappedQuoteRequestStatus = N'Declined' THEN N'Declined'
        WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN N'Need Info'
        WHEN @MappedQuoteRequestStatus = N'UnderReview' THEN N'Under Review'
        WHEN @MappedQuoteRequestStatus IN (N'Acknowledged', N'Submitted') THEN N'Awaiting Response'
        WHEN @MappedQuoteRequestStatus IN (N'Failed', N'Expired', N'Cancelled') THEN @MappedQuoteRequestStatus
        ELSE Status END,
    RespondedDateUtc = CASE WHEN @MappedQuoteRequestStatus IN (N'Declined', N'Failed', N'Expired', N'Cancelled') THEN COALESCE(RespondedDateUtc, SYSUTCDATETIME()) ELSE RespondedDateUtc END,
    DeclineReason = CASE WHEN @MappedQuoteRequestStatus = N'Declined' THEN COALESCE(NULLIF(@CoverageNotes, N''), DeclineReason) ELSE DeclineReason END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;

IF @MappedQuoteRequestStatus IN (N'MoreInformationRequired', N'UnderReview')
BEGIN
    DECLARE @ResponsibleUserId UNIQUEIDENTIFIER = (SELECT TOP 1 AssignedToUserId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);
    DECLARE @AccountId UNIQUEIDENTIFIER = (SELECT TOP 1 AccountId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);
    DECLARE @FollowUpTaskId UNIQUEIDENTIFIER = NEWID();
    DECLARE @FollowUpTitle NVARCHAR(200) = CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN N'Provide carrier requested information' ELSE N'Follow up on quote request under review' END;
    DECLARE @FollowUpDescription NVARCHAR(2000) = COALESCE(NULLIF(@CoverageNotes, N''), CONCAT(N'API rating response recorded as ', @MappedQuoteRequestStatus, N'.'));

    IF @MappedQuoteRequestStatus = N'MoreInformationRequired'
       AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeQuestion WHERE SubmissionId = @SubmissionId AND QuestionCode = CONCAT(N'MarketInfo-', LEFT(CONVERT(NVARCHAR(36), @QuoteRequestId), 8)) AND IsDeleted = 0)
    BEGIN
        INSERT INTO Submissions.SubmissionIntakeQuestion
            (IntakeQuestionId, SubmissionId, TenantId, QuestionCode, QuestionText, HelpText, IsRequired, AnswerText, IsAnswered, StatusCode, StatusReason, ReviewDueDateUtc, SubmissionMarketId, CarrierId, ScopeCode, BlocksSubmit, CreatedDateUtc, IsDeleted)
        VALUES
            (NEWID(), @SubmissionId, @TenantId, CONCAT(N'MarketInfo-', LEFT(CONVERT(NVARCHAR(36), @QuoteRequestId), 8)), @FollowUpTitle, @FollowUpDescription, 1, NULL, 0, N'NeedsReview', @FollowUpDescription, DATEADD(day, 3, SYSUTCDATETIME()), @SubmissionMarketId, @CarrierId, N'MarketResponse', 1, SYSUTCDATETIME(), 0);
    END;

    IF OBJECT_ID(N'OPS.TaskItem', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM OPS.TaskItem WHERE TenantId = @TenantId AND RelatedEntityName = N'QuoteRequest' AND RelatedEntityId = @QuoteRequestId AND TaskTypeCode IN (N'MarketInfoRequest', N'MarketFollowUp') AND IsDeleted = 0)
    BEGIN
        INSERT INTO OPS.TaskItem
            (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CreatedDateUtc, IsDeleted)
        VALUES
            (@FollowUpTaskId, @TenantId, CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @FollowUpTaskId), N'-', N''), 6)), @FollowUpTitle, @FollowUpDescription,
             CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN N'MarketInfoRequest' ELSE N'MarketFollowUp' END, N'Marketing', N'High', N'Open', N'QuoteRequest', @QuoteRequestId, @AccountId, @ResponsibleUserId, DATEADD(day, CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN 3 ELSE 5 END, CONVERT(date, SYSUTCDATETIME())), SYSUTCDATETIME(), 0);

        UPDATE Submissions.SubmissionMarket
        SET FollowUpTaskId = COALESCE(FollowUpTaskId, @FollowUpTaskId),
            NextActionDateUtc = COALESCE(NextActionDateUtc, DATEADD(day, CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN 3 ELSE 5 END, SYSUTCDATETIME()))
        WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;
    END;

    IF OBJECT_ID(N'Core.Notification', N'U') IS NOT NULL AND @ResponsibleUserId IS NOT NULL
    BEGIN
        INSERT INTO Core.Notification
            (NotificationId, TenantId, RecipientUserId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, Priority, Category, DeliveryProvider, DeliveryStatus, PolicyStatus, SyncStatus, CreatedDateUtc, IsDeleted)
        VALUES
            (NEWID(), @TenantId, @ResponsibleUserId, N'InApp', @FollowUpTitle, @FollowUpDescription, N'QuoteRequest', @QuoteRequestId, N'Delivered', 0, N'High', N'Quote Request', N'AMS', N'Delivered', N'Compliant', N'Synced', SYSUTCDATETIME(), 0);
    END;
END;

DECLARE @DerivedSubmissionStatus NVARCHAR(50) = CASE
    WHEN EXISTS (SELECT 1 FROM Submissions.BoundPolicy bp WHERE bp.SubmissionId = @SubmissionId AND bp.TenantId = @TenantId AND bp.IsDeleted = 0) THEN N'Bound'
    WHEN EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction pbt WHERE pbt.SubmissionId = @SubmissionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0 AND pbt.BindStatusCode IN (N'Draft', N'PendingApproval', N'ReadyToBind', N'Submitted', N'Acknowledged', N'CarrierReviewing', N'MoreInformationRequired', N'Confirmed')) THEN N'Binding'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status = N'Accepted') THEN N'Customer Accepted'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Sent', N'Pending Decision')) THEN N'Presented'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0) THEN N'Proposal Prepared'
    WHEN EXISTS (SELECT 1 FROM Submissions.Quote q WHERE q.SubmissionId = @SubmissionId AND q.IsDeleted = 0) THEN N'Quotes Received'
    WHEN EXISTS (SELECT 1 FROM Submissions.QuoteRequest qr WHERE qr.SubmissionId = @SubmissionId AND qr.TenantId = @TenantId AND qr.IsDeleted = 0) THEN N'Marketing'
    ELSE N'Ready for Marketing'
END;

UPDATE Submissions.Submission
SET Status = CASE WHEN @DerivedSubmissionStatus = N'Bound' THEN N'Bound' WHEN Status IN (N'Lost', N'Cancelled', N'Closed') THEN Status ELSE @DerivedSubmissionStatus END,
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE Submissions.CarrierTransmission
SET StatusCode = N'ResponseReceived',
    SentDateUtc = COALESCE(SentDateUtc, LastAttemptDateUtc, SYSUTCDATETIME()),
    ConfirmedDateUtc = CASE WHEN @MappedQuoteRequestStatus IN (N'Acknowledged', N'Submitted', N'UnderReview') THEN ConfirmedDateUtc ELSE COALESCE(ConfirmedDateUtc, SYSUTCDATETIME()) END,
    ExternalReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), ExternalReferenceNumber),
    LastError = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CarrierTransmissionId = @CarrierTransmissionId AND IsDeleted = 0;

INSERT INTO Submissions.CarrierTransmissionEvent
    (CarrierTransmissionEventId, TenantId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, EventPayloadJson, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @CarrierTransmissionId, @SubmissionId, @SubmissionMarketId, N'ApiRatingMarketOutcome', CONCAT(N'API rating connector returned ', @MappedQuoteRequestStatus, N' without creating a quote.'), COALESCE(NULLIF(@RawPayloadJson, N''), N'{}'), SYSUTCDATETIME(), 0);

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES
    (NEWID(), @SubmissionId, @TenantId, N'ApiRatingMarketOutcome', CONCAT(N'API rating connector returned ', @MappedQuoteRequestStatus, N' without creating a quote.'), SYSUTCDATETIME(), N'QuoteRequest', @QuoteRequestId, N'ApiRatingConnectorWorker', 0);";

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            transmission.TenantId,
            transmission.SubmissionId,
            transmission.SubmissionMarketId,
            transmission.CarrierId,
            transmission.CarrierTransmissionId,
            MappedStatus = MapQuoteRequestStatus(rating.Status),
            rating.CarrierReferenceNumber,
            rating.CoverageNotes,
            rating.RawPayloadJson
        }, cancellationToken: cancellationToken));
    }

    private static async Task PersistFailedRatingAsync(ISqlConnectionFactory connectionFactory, ApiRatingTransmission transmission, string errorMessage, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
UPDATE Submissions.CarrierTransmission
SET StatusCode = CASE WHEN AttemptCount >= 5 THEN N'Failed' ELSE N'AwaitingExternalConnector' END,
    FailedDateUtc = CASE WHEN AttemptCount >= 5 THEN COALESCE(FailedDateUtc, SYSUTCDATETIME()) ELSE FailedDateUtc END,
    LastError = @ErrorMessage,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE CarrierTransmissionId = @CarrierTransmissionId AND IsDeleted = 0;

INSERT INTO Submissions.CarrierTransmissionEvent
    (CarrierTransmissionEventId, TenantId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, EventPayloadJson, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @CarrierTransmissionId, @SubmissionId, @SubmissionMarketId, N'ApiRatingFailed', @ErrorMessage, N'{}', SYSUTCDATETIME(), 0);

INSERT INTO Submissions.CarrierInboundResponse
    (CarrierInboundResponseId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, CarrierTransmissionId, SourceChannelCode, ResponseTypeCode, StatusCode,
     CarrierReferenceNumber, PayloadJson, ReceivedDateUtc, ProcessingError, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, @CarrierTransmissionId, N'API', N'QuoteResponse', N'Failed', NULL,
     CONCAT(N'{""error"":""', STRING_ESCAPE(@ErrorMessage, 'json'), N'""}'), SYSUTCDATETIME(), @ErrorMessage, SYSUTCDATETIME(), 0);

UPDATE qr
SET RetryCount = COALESCE(qr.RetryCount, 0) + 1,
    LastAttemptDateUtc = SYSUTCDATETIME(),
    LastError = @ErrorMessage,
    StatusCode = CASE WHEN t.AttemptCount >= 5 THEN N'Failed' ELSE qr.StatusCode END,
    ResponseDateUtc = CASE WHEN t.AttemptCount >= 5 THEN COALESCE(qr.ResponseDateUtc, SYSUTCDATETIME()) ELSE qr.ResponseDateUtc END,
    ClosedDateUtc = CASE WHEN t.AttemptCount >= 5 THEN COALESCE(qr.ClosedDateUtc, SYSUTCDATETIME()) ELSE qr.ClosedDateUtc END,
    CorrelationId = COALESCE(NULLIF(qr.CorrelationId, N''), CONCAT(N'QR-', CONVERT(NVARCHAR(36), qr.QuoteRequestId))),
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.QuoteRequest qr
INNER JOIN Submissions.CarrierTransmission t ON t.SubmissionMarketId = qr.SubmissionMarketId AND t.SubmissionId = qr.SubmissionId AND t.CarrierId = qr.CarrierId AND t.CarrierTransmissionId = @CarrierTransmissionId
WHERE qr.IsDeleted = 0
  AND qr.QuoteRequestId = (
      SELECT TOP 1 latest.QuoteRequestId
      FROM Submissions.QuoteRequest latest
      WHERE latest.SubmissionMarketId = @SubmissionMarketId
        AND latest.SubmissionId = @SubmissionId
        AND latest.TenantId = @TenantId
        AND latest.IsDeleted = 0
      ORDER BY latest.RequestVersion DESC, latest.RequestedDateUtc DESC
  );

DECLARE @DerivedSubmissionStatus NVARCHAR(50) = CASE
    WHEN EXISTS (SELECT 1 FROM Submissions.BoundPolicy bp WHERE bp.SubmissionId = @SubmissionId AND bp.TenantId = @TenantId AND bp.IsDeleted = 0) THEN N'Bound'
    WHEN EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction pbt WHERE pbt.SubmissionId = @SubmissionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0 AND pbt.BindStatusCode IN (N'Draft', N'PendingApproval', N'ReadyToBind', N'Submitted', N'Acknowledged', N'CarrierReviewing', N'MoreInformationRequired', N'Confirmed')) THEN N'Binding'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status = N'Accepted') THEN N'Customer Accepted'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Sent', N'Pending Decision')) THEN N'Presented'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0) THEN N'Proposal Prepared'
    WHEN EXISTS (SELECT 1 FROM Submissions.Quote q WHERE q.SubmissionId = @SubmissionId AND q.IsDeleted = 0) THEN N'Quotes Received'
    WHEN EXISTS (SELECT 1 FROM Submissions.QuoteRequest qr WHERE qr.SubmissionId = @SubmissionId AND qr.TenantId = @TenantId AND qr.IsDeleted = 0) THEN N'Marketing'
    ELSE N'Ready for Marketing'
END;

UPDATE Submissions.Submission
SET Status = CASE WHEN @DerivedSubmissionStatus = N'Bound' THEN N'Bound' WHEN Status IN (N'Lost', N'Cancelled', N'Closed') THEN Status ELSE @DerivedSubmissionStatus END,
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            transmission.TenantId,
            transmission.SubmissionId,
            transmission.SubmissionMarketId,
            transmission.CarrierId,
            transmission.CarrierTransmissionId,
            ErrorMessage = TrimForStorage(errorMessage, 1900)
        }, cancellationToken: cancellationToken));
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return bool.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static DateTime? ReadDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null;
    }

    private static string TrimForStorage(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static bool ShouldCreateQuote(ApiRatingResponse response)
    {
        var normalized = NormalizeStatus(response.Status);
        if (normalized is "Declined" or "Rejected" or "MoreInformationRequired" or "Referred" or "Failed" or "Expired" or "Withdrawn" or "NoResponse" or "Cancelled" or "Acknowledged" or "Submitted" or "UnderReview" or "CarrierProcessing" or "AwaitingUnderwriter")
        {
            return false;
        }

        return normalized is "Quoted" or "QuoteReturned" or "Returned" or "Received" or "Approved" or "Bindable"
            || response.AnnualPremium > 0
            || !string.IsNullOrWhiteSpace(response.QuoteNumber);
    }

    private static string MapQuoteStatus(string? status)
    {
        return NormalizeStatus(status) switch
        {
            "Approved" or "ApprovedForPresentation" => "Approved for Presentation",
            "Presented" => "Presented",
            "Selected" => "Selected",
            "Bound" => "Bound",
            "RevisionRequested" => "Revision Requested",
            "Expired" => "Expired",
            "Superseded" => "Superseded",
            _ => "Received"
        };
    }

    private static string MapQuoteRequestStatus(string? status)
    {
        return NormalizeStatus(status) switch
        {
            "Declined" or "Rejected" => "Declined",
            "MoreInformationRequired" or "NeedInfo" or "NeedsInfo" => "MoreInformationRequired",
            "Referred" or "AwaitingUnderwriter" => "UnderReview",
            "Acknowledged" => "Acknowledged",
            "Submitted" => "Submitted",
            "UnderReview" or "CarrierProcessing" or "Processing" => "UnderReview",
            "Expired" => "Expired",
            "Withdrawn" or "NoResponse" => "Cancelled",
            "Cancelled" => "Cancelled",
            "Failed" => "Failed",
            _ => "UnderReview"
        };
    }

    private static string NormalizeStatus(string? status)
        => string.IsNullOrWhiteSpace(status) ? string.Empty : status.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase);

    private sealed record ApiRatingWorkerSettings(int PollIntervalSeconds, int MaxTransmissionsPerPoll, bool Enabled);

    private sealed record ApiRatingTransmission(
        Guid CarrierTransmissionId,
        Guid TenantId,
        Guid SubmissionId,
        Guid SubmissionMarketId,
        Guid CarrierId,
        Guid? CarrierExternalConnectorId,
        string PayloadJson,
        string? EndpointUri,
        string? ConnectorEndpointUri,
        string ConfigurationJson,
        string? RatingEndpointUri,
        string AuthMode,
        string? ApiKey,
        string ApiKeyHeader,
        string? BearerToken,
        int TimeoutSeconds);

    private sealed record ApiRatingResponse(
        string Status,
        string? QuoteNumber,
        decimal AnnualPremium,
        decimal? Deductible,
        decimal? Limit,
        decimal? CommissionPercent,
        string? Subjectivities,
        string? Exclusions,
        string? CarrierRating,
        string? PaymentTerms,
        decimal? MinimumEarnedPremium,
        decimal? TaxesAndFees,
        decimal? BrokerFee,
        bool? TriaIncluded,
        string? CoverageNotes,
        DateTime ExpiresDateUtc,
        DateTime? EffectiveDate,
        string? CoverageForms,
        bool IsBindable,
        string RawPayloadJson,
        string CarrierReferenceNumber);
}
