using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Payments;
using Dapper;
using Microsoft.AspNetCore.DataProtection;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PaymentPlatformRepository : IPaymentPlatformRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IDataProtector _protector;

    public PaymentPlatformRepository(ISqlConnectionFactory connectionFactory, IDataProtectionProvider dataProtectionProvider)
    {
        _connectionFactory = connectionFactory;
        _protector = dataProtectionProvider.CreateProtector("Ams.PaymentPlatform.Credentials.v1");
    }

    public async Task<PaymentGatewayCredentialDto?> GetCredentialByIdAsync(Guid credentialId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentGatewayCredentialId, TenantId, IntegrationConfigItemId, ProviderCode, EnvironmentCode AS Environment, DisplayName, ProcessorEndpointUrl, IsActive,
       CASE WHEN NULLIF(EncryptedApiKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasApiKey,
       CASE WHEN NULLIF(EncryptedApiSecret, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasApiSecret,
       CASE WHEN NULLIF(EncryptedWebhookSecret, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasWebhookSecret,
       CASE WHEN NULLIF(EncryptedLoginId, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasLoginId,
       CASE WHEN NULLIF(EncryptedTransactionKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasTransactionKey,
       CASE WHEN NULLIF(EncryptedClientKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasClientKey,
       CASE WHEN NULLIF(EncryptedMerchantId, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasMerchantId,
       CreatedDateUtc, ModifiedDateUtc
FROM Billing.PaymentGatewayCredential
WHERE PaymentGatewayCredentialId = @CredentialId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PaymentGatewayCredentialDto>(new CommandDefinition(sql, new { CredentialId = credentialId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<PaymentCredentialSecretKind, string>> GetCredentialSecretsAsync(Guid credentialId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT EncryptedApiKey, EncryptedApiSecret, EncryptedPublishableKey, EncryptedWebhookSecret, EncryptedLoginId, EncryptedTransactionKey, EncryptedClientKey, EncryptedMerchantId
FROM Billing.PaymentGatewayCredential
WHERE PaymentGatewayCredentialId = @CredentialId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await cn.QuerySingleOrDefaultAsync<CredentialSecretRow>(new CommandDefinition(sql, new { CredentialId = credentialId }, cancellationToken: cancellationToken));
        if (row is null) return new Dictionary<PaymentCredentialSecretKind, string>();

        var secrets = new Dictionary<PaymentCredentialSecretKind, string>();
        AddSecret(secrets, PaymentCredentialSecretKind.ApiKey, row.EncryptedApiKey);
        AddSecret(secrets, PaymentCredentialSecretKind.ApiSecret, row.EncryptedApiSecret);
        AddSecret(secrets, PaymentCredentialSecretKind.PublishableKey, row.EncryptedPublishableKey);
        AddSecret(secrets, PaymentCredentialSecretKind.WebhookSecret, row.EncryptedWebhookSecret);
        AddSecret(secrets, PaymentCredentialSecretKind.LoginId, row.EncryptedLoginId);
        AddSecret(secrets, PaymentCredentialSecretKind.TransactionKey, row.EncryptedTransactionKey);
        AddSecret(secrets, PaymentCredentialSecretKind.ClientKey, row.EncryptedClientKey);
        AddSecret(secrets, PaymentCredentialSecretKind.MerchantId, row.EncryptedMerchantId);
        return secrets;
    }

    public async Task<IReadOnlyList<PaymentGatewayCredentialDto>> GetActiveCredentialsAsync(string? providerCode, int maxCount, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT TOP (@MaxCount) PaymentGatewayCredentialId, TenantId, IntegrationConfigItemId, ProviderCode, EnvironmentCode AS Environment, DisplayName, ProcessorEndpointUrl, IsActive,
       CASE WHEN NULLIF(EncryptedApiKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasApiKey,
       CASE WHEN NULLIF(EncryptedApiSecret, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasApiSecret,
       CASE WHEN NULLIF(EncryptedWebhookSecret, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasWebhookSecret,
       CASE WHEN NULLIF(EncryptedLoginId, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasLoginId,
       CASE WHEN NULLIF(EncryptedTransactionKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasTransactionKey,
       CASE WHEN NULLIF(EncryptedClientKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasClientKey,
       CASE WHEN NULLIF(EncryptedMerchantId, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasMerchantId,
       CreatedDateUtc, ModifiedDateUtc
FROM Billing.PaymentGatewayCredential
WHERE IsActive = 1 AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode)
ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PaymentGatewayCredentialDto>(new CommandDefinition(sql, new { ProviderCode = EmptyToNull(providerCode), MaxCount = Math.Max(1, maxCount) }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<PagedResult<PaymentGatewayCredentialDto>> SearchCredentialsAsync(Guid tenantId, string? providerCode, string? environment, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentGatewayCredentialId, TenantId, IntegrationConfigItemId, ProviderCode, EnvironmentCode AS Environment, DisplayName, ProcessorEndpointUrl, IsActive,
       CASE WHEN NULLIF(EncryptedApiKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasApiKey,
       CASE WHEN NULLIF(EncryptedApiSecret, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasApiSecret,
       CASE WHEN NULLIF(EncryptedWebhookSecret, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasWebhookSecret,
       CASE WHEN NULLIF(EncryptedLoginId, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasLoginId,
       CASE WHEN NULLIF(EncryptedTransactionKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasTransactionKey,
       CASE WHEN NULLIF(EncryptedClientKey, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasClientKey,
       CASE WHEN NULLIF(EncryptedMerchantId, '') IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END AS HasMerchantId,
       CreatedDateUtc, ModifiedDateUtc
FROM Billing.PaymentGatewayCredential
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode)
  AND (@Environment IS NULL OR EnvironmentCode = @Environment)
ORDER BY ProviderCode, EnvironmentCode, DisplayName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Billing.PaymentGatewayCredential
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode)
  AND (@Environment IS NULL OR EnvironmentCode = @Environment);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ProviderCode = EmptyToNull(providerCode), Environment = EmptyToNull(environment), Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        return new() { Items = (await multi.ReadAsync<PaymentGatewayCredentialDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> UpsertCredentialAsync(UpsertPaymentGatewayCredentialRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var encryptedSecrets = ProtectSecrets(request);
        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER = (
    SELECT TOP 1 PaymentGatewayCredentialId
    FROM Billing.PaymentGatewayCredential
    WHERE TenantId = @TenantId AND IntegrationConfigItemId = @IntegrationConfigItemId AND ProviderCode = @ProviderCode AND EnvironmentCode = @EnvironmentCode AND IsDeleted = 0
);

IF @ExistingId IS NULL
BEGIN
    SET @ExistingId = NEWID();
    INSERT INTO Billing.PaymentGatewayCredential (PaymentGatewayCredentialId, TenantId, IntegrationConfigItemId, ProviderCode, EnvironmentCode, DisplayName, ProcessorEndpointUrl, EncryptedApiKey, EncryptedApiSecret, EncryptedPublishableKey, EncryptedWebhookSecret, EncryptedLoginId, EncryptedTransactionKey, EncryptedClientKey, EncryptedMerchantId, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@ExistingId, @TenantId, @IntegrationConfigItemId, @ProviderCode, @EnvironmentCode, @DisplayName, @ProcessorEndpointUrl, @EncryptedApiKey, @EncryptedApiSecret, @EncryptedPublishableKey, @EncryptedWebhookSecret, @EncryptedLoginId, @EncryptedTransactionKey, @EncryptedClientKey, @EncryptedMerchantId, @IsActive, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE
BEGIN
    UPDATE Billing.PaymentGatewayCredential
    SET DisplayName = @DisplayName,
        ProcessorEndpointUrl = @ProcessorEndpointUrl,
        EncryptedApiKey = COALESCE(@EncryptedApiKey, EncryptedApiKey),
        EncryptedApiSecret = COALESCE(@EncryptedApiSecret, EncryptedApiSecret),
        EncryptedPublishableKey = COALESCE(@EncryptedPublishableKey, EncryptedPublishableKey),
        EncryptedWebhookSecret = COALESCE(@EncryptedWebhookSecret, EncryptedWebhookSecret),
        EncryptedLoginId = COALESCE(@EncryptedLoginId, EncryptedLoginId),
        EncryptedTransactionKey = COALESCE(@EncryptedTransactionKey, EncryptedTransactionKey),
        EncryptedClientKey = COALESCE(@EncryptedClientKey, EncryptedClientKey),
        EncryptedMerchantId = COALESCE(@EncryptedMerchantId, EncryptedMerchantId),
        IsActive = @IsActive,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ModifiedByUserId
    WHERE PaymentGatewayCredentialId = @ExistingId;
END

SELECT @ExistingId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.IntegrationConfigItemId,
            request.ProviderCode,
            EnvironmentCode = request.Environment,
            request.DisplayName,
            ProcessorEndpointUrl = EmptyToNull(request.ProcessorEndpointUrl),
            EncryptedApiKey = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.ApiKey),
            EncryptedApiSecret = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.ApiSecret),
            EncryptedPublishableKey = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.PublishableKey),
            EncryptedWebhookSecret = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.WebhookSecret),
            EncryptedLoginId = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.LoginId),
            EncryptedTransactionKey = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.TransactionKey),
            EncryptedClientKey = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.ClientKey),
            EncryptedMerchantId = encryptedSecrets.GetValueOrDefault(PaymentCredentialSecretKind.MerchantId),
            request.IsActive,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreatePaymentMethodTokenAsync(TokenizePaymentMethodRequest request, string tokenReference, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
IF @IsDefault = 1
BEGIN
    UPDATE Billing.PaymentMethodToken SET IsDefault = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId WHERE TenantId = @TenantId AND AccountId = @AccountId AND IsDeleted = 0;
END

INSERT INTO Billing.PaymentMethodToken (PaymentMethodTokenId, TenantId, AccountId, PaymentGatewayCredentialId, ProviderCode, TokenReference, PaymentMethodType, Last4, Brand, ExpirationMonth, ExpirationYear, IsDefault, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @AccountId, @PaymentGatewayCredentialId, @ProviderCode, @TokenReference, @PaymentMethodType, @Last4, @Brand, @ExpirationMonth, @ExpirationYear, @IsDefault, 1, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var credential = await GetCredentialByIdAsync(request.PaymentGatewayCredentialId, cancellationToken) ?? throw new InvalidOperationException("Payment gateway credential was not found.");
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.PaymentGatewayCredentialId, credential.ProviderCode, TokenReference = tokenReference, request.PaymentMethodType, request.Last4, request.Brand, request.ExpirationMonth, request.ExpirationYear, request.IsDefault, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<PagedResult<PaymentMethodTokenDto>> SearchPaymentMethodTokensAsync(Guid tenantId, Guid? accountId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentMethodTokenId, TenantId, AccountId, PaymentGatewayCredentialId, ProviderCode, CAST('' AS NVARCHAR(300)) AS TokenReference, PaymentMethodType, Last4, Brand, ExpirationMonth, ExpirationYear, IsDefault, IsActive, CreatedDateUtc
FROM Billing.PaymentMethodToken
WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AccountId IS NULL OR AccountId = @AccountId)
ORDER BY IsDefault DESC, CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Billing.PaymentMethodToken WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AccountId IS NULL OR AccountId = @AccountId);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AccountId = accountId, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        return new() { Items = (await multi.ReadAsync<PaymentMethodTokenDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PaymentMethodTokenDto?> GetPaymentMethodTokenByIdAsync(Guid paymentMethodTokenId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentMethodTokenId, TenantId, AccountId, PaymentGatewayCredentialId, ProviderCode, TokenReference, PaymentMethodType, Last4, Brand, ExpirationMonth, ExpirationYear, IsDefault, IsActive, CreatedDateUtc
FROM Billing.PaymentMethodToken
WHERE PaymentMethodTokenId = @PaymentMethodTokenId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PaymentMethodTokenDto>(new CommandDefinition(sql, new { PaymentMethodTokenId = paymentMethodTokenId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateOperationAsync(PaymentProcessorOperationDto operation, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var id = operation.PaymentProcessorOperationId == Guid.Empty ? Guid.NewGuid() : operation.PaymentProcessorOperationId;
        const string sql = @"
INSERT INTO Billing.PaymentProcessorOperation (PaymentProcessorOperationId, TenantId, PaymentId, PaymentGatewayCredentialId, ProviderCode, OperationType, StatusCode, Amount, CurrencyCode, ProviderOperationId, ProviderStatus, CheckoutUrl, FailureCode, FailureMessage, RequestPayloadJson, RetryCount, NextRetryDateUtc, CreatedDateUtc, CompletedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @PaymentId, @PaymentGatewayCredentialId, @ProviderCode, @OperationType, @StatusCode, @Amount, @CurrencyCode, @ProviderOperationId, @ProviderStatus, @CheckoutUrl, @FailureCode, @FailureMessage, @RequestPayloadJson, @RetryCount, @NextRetryDateUtc, SYSUTCDATETIME(), @CompletedDateUtc, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, operation.TenantId, operation.PaymentId, operation.PaymentGatewayCredentialId, operation.ProviderCode, operation.OperationType, operation.StatusCode, operation.Amount, operation.CurrencyCode, operation.ProviderOperationId, operation.ProviderStatus, operation.CheckoutUrl, operation.FailureCode, operation.FailureMessage, operation.RequestPayloadJson, operation.RetryCount, operation.NextRetryDateUtc, operation.CompletedDateUtc }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateOperationAsync(PaymentProcessorOperationDto operation, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
UPDATE Billing.PaymentProcessorOperation
SET PaymentId = @PaymentId,
    StatusCode = @StatusCode,
    ProviderOperationId = @ProviderOperationId,
    ProviderStatus = @ProviderStatus,
    CheckoutUrl = @CheckoutUrl,
    FailureCode = @FailureCode,
    FailureMessage = @FailureMessage,
    RequestPayloadJson = COALESCE(@RequestPayloadJson, RequestPayloadJson),
    RetryCount = @RetryCount,
    NextRetryDateUtc = @NextRetryDateUtc,
    CompletedDateUtc = @CompletedDateUtc,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE PaymentProcessorOperationId = @PaymentProcessorOperationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, operation, cancellationToken: cancellationToken));
    }

    public async Task<PaymentProcessorOperationDto?> GetOperationByIdAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentProcessorOperationId, TenantId, PaymentId, PaymentGatewayCredentialId, ProviderCode, OperationType, StatusCode, Amount, CurrencyCode, ProviderOperationId, ProviderStatus, CheckoutUrl, FailureCode, FailureMessage, RequestPayloadJson, RetryCount, NextRetryDateUtc, CreatedDateUtc, CompletedDateUtc
FROM Billing.PaymentProcessorOperation
WHERE PaymentProcessorOperationId = @OperationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PaymentProcessorOperationDto>(new CommandDefinition(sql, new { OperationId = operationId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PaymentProcessorOperationDto>> SearchOperationsAsync(Guid tenantId, string? providerCode, string? statusCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentProcessorOperationId, TenantId, PaymentId, PaymentGatewayCredentialId, ProviderCode, OperationType, StatusCode, Amount, CurrencyCode, ProviderOperationId, ProviderStatus, CheckoutUrl, FailureCode, FailureMessage, RequestPayloadJson, RetryCount, NextRetryDateUtc, CreatedDateUtc, CompletedDateUtc
FROM Billing.PaymentProcessorOperation
WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode) AND (@StatusCode IS NULL OR StatusCode = @StatusCode)
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Billing.PaymentProcessorOperation WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode) AND (@StatusCode IS NULL OR StatusCode = @StatusCode);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ProviderCode = EmptyToNull(providerCode), StatusCode = EmptyToNull(statusCode), Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        return new() { Items = (await multi.ReadAsync<PaymentProcessorOperationDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<PaymentProcessorOperationDto>> GetDueRetryOperationsAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT TOP (@MaxCount) PaymentProcessorOperationId, TenantId, PaymentId, PaymentGatewayCredentialId, ProviderCode, OperationType, StatusCode, Amount, CurrencyCode, ProviderOperationId, ProviderStatus, CheckoutUrl, FailureCode, FailureMessage, RequestPayloadJson, RetryCount, NextRetryDateUtc, CreatedDateUtc, CompletedDateUtc
FROM Billing.PaymentProcessorOperation
WHERE IsDeleted = 0 AND StatusCode = @StatusCode AND NextRetryDateUtc IS NOT NULL AND NextRetryDateUtc <= SYSUTCDATETIME()
ORDER BY NextRetryDateUtc ASC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PaymentProcessorOperationDto>(new CommandDefinition(sql, new { MaxCount = Math.Max(1, maxCount), StatusCode = PaymentProcessorOperationStatus.Failed.ToString() }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateWebhookEventAsync(PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER = (SELECT TOP 1 PaymentWebhookEventId FROM Billing.PaymentWebhookEvent WHERE TenantId = @TenantId AND ProviderCode = @ProviderCode AND ProviderEventId = @ProviderEventId AND IsDeleted = 0);
IF @ExistingId IS NULL
BEGIN
    SET @ExistingId = NEWID();
    INSERT INTO Billing.PaymentWebhookEvent (PaymentWebhookEventId, TenantId, ProviderCode, EnvironmentCode, EventType, ProviderEventId, PayloadJson, SignatureHeader, IsProcessed, ReceivedDateUtc, IsDeleted)
    VALUES (@ExistingId, @TenantId, @ProviderCode, @EnvironmentCode, @EventType, @ProviderEventId, @PayloadJson, @SignatureHeader, 0, SYSUTCDATETIME(), 0);
END
SELECT @ExistingId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.TenantId, request.ProviderCode, EnvironmentCode = request.Environment, request.EventType, request.ProviderEventId, request.PayloadJson, request.SignatureHeader }, cancellationToken: cancellationToken));
    }

    public async Task MarkWebhookEventProcessedAsync(Guid webhookEventId, string? processingError, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = "UPDATE Billing.PaymentWebhookEvent SET IsProcessed = @IsProcessed, ProcessedDateUtc = SYSUTCDATETIME(), ProcessingError = @ProcessingError WHERE PaymentWebhookEventId = @WebhookEventId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { WebhookEventId = webhookEventId, IsProcessed = string.IsNullOrWhiteSpace(processingError), ProcessingError = EmptyToNull(processingError) }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PaymentWebhookEventDto>> SearchWebhookEventsAsync(Guid tenantId, string? providerCode, bool? isProcessed, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentWebhookEventId, TenantId, ProviderCode, EnvironmentCode AS Environment, EventType, ProviderEventId, IsProcessed, ReceivedDateUtc, ProcessedDateUtc, ProcessingError
FROM Billing.PaymentWebhookEvent
WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode) AND (@IsProcessed IS NULL OR IsProcessed = @IsProcessed)
ORDER BY ReceivedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Billing.PaymentWebhookEvent WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode) AND (@IsProcessed IS NULL OR IsProcessed = @IsProcessed);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ProviderCode = EmptyToNull(providerCode), IsProcessed = isProcessed, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        return new() { Items = (await multi.ReadAsync<PaymentWebhookEventDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateSettlementBatchAsync(PaymentSettlementBatchDto settlementBatch, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER = (SELECT TOP 1 PaymentSettlementBatchId FROM Billing.PaymentSettlementBatch WHERE TenantId = @TenantId AND ProviderCode = @ProviderCode AND SettlementBatchReference = @SettlementBatchReference AND IsDeleted = 0);
IF @ExistingId IS NULL
BEGIN
    SET @ExistingId = NEWID();
    INSERT INTO Billing.PaymentSettlementBatch (PaymentSettlementBatchId, TenantId, PaymentGatewayCredentialId, ProviderCode, SettlementBatchReference, SettlementDateUtc, GrossAmount, FeeAmount, NetAmount, StatusCode, CreatedDateUtc, IsDeleted)
    VALUES (@ExistingId, @TenantId, @PaymentGatewayCredentialId, @ProviderCode, @SettlementBatchReference, @SettlementDateUtc, @GrossAmount, @FeeAmount, @NetAmount, @StatusCode, SYSUTCDATETIME(), 0);
END
SELECT @ExistingId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, settlementBatch, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PaymentSettlementBatchDto>> SearchSettlementBatchesAsync(Guid tenantId, string? providerCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        const string sql = @"
SELECT PaymentSettlementBatchId, TenantId, PaymentGatewayCredentialId, ProviderCode, SettlementBatchReference, SettlementDateUtc, GrossAmount, FeeAmount, NetAmount, StatusCode, CreatedDateUtc
FROM Billing.PaymentSettlementBatch
WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode)
ORDER BY SettlementDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Billing.PaymentSettlementBatch WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@ProviderCode IS NULL OR ProviderCode = @ProviderCode);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ProviderCode = EmptyToNull(providerCode), Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        return new() { Items = (await multi.ReadAsync<PaymentSettlementBatchDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    private void AddSecret(Dictionary<PaymentCredentialSecretKind, string> secrets, PaymentCredentialSecretKind kind, string? encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue)) return;
        secrets[kind] = _protector.Unprotect(encryptedValue);
    }

    public IReadOnlyDictionary<PaymentCredentialSecretKind, string> ProtectSecrets(UpsertPaymentGatewayCredentialRequest request)
    {
        var result = new Dictionary<PaymentCredentialSecretKind, string>();
        ProtectIfPresent(result, PaymentCredentialSecretKind.ApiKey, request.ApiKey);
        ProtectIfPresent(result, PaymentCredentialSecretKind.ApiSecret, request.ApiSecret);
        ProtectIfPresent(result, PaymentCredentialSecretKind.PublishableKey, request.PublishableKey);
        ProtectIfPresent(result, PaymentCredentialSecretKind.WebhookSecret, request.WebhookSecret);
        ProtectIfPresent(result, PaymentCredentialSecretKind.LoginId, request.LoginId);
        ProtectIfPresent(result, PaymentCredentialSecretKind.TransactionKey, request.TransactionKey);
        ProtectIfPresent(result, PaymentCredentialSecretKind.ClientKey, request.ClientKey);
        ProtectIfPresent(result, PaymentCredentialSecretKind.MerchantId, request.MerchantId);
        return result;
    }

    private void ProtectIfPresent(Dictionary<PaymentCredentialSecretKind, string> secrets, PaymentCredentialSecretKind kind, string? plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return;
        secrets[kind] = _protector.Protect(plainText.Trim());
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing') EXEC(N'CREATE SCHEMA Billing');

IF OBJECT_ID(N'Billing.PaymentGatewayCredential', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.PaymentGatewayCredential
    (
        PaymentGatewayCredentialId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentGatewayCredential PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        IntegrationConfigItemId UNIQUEIDENTIFIER NOT NULL,
        ProviderCode NVARCHAR(80) NOT NULL,
        EnvironmentCode NVARCHAR(20) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        ProcessorEndpointUrl NVARCHAR(500) NULL,
        EncryptedApiKey NVARCHAR(MAX) NULL,
        EncryptedApiSecret NVARCHAR(MAX) NULL,
        EncryptedPublishableKey NVARCHAR(MAX) NULL,
        EncryptedWebhookSecret NVARCHAR(MAX) NULL,
        EncryptedLoginId NVARCHAR(MAX) NULL,
        EncryptedTransactionKey NVARCHAR(MAX) NULL,
        EncryptedClientKey NVARCHAR(MAX) NULL,
        EncryptedMerchantId NVARCHAR(MAX) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_PaymentGatewayCredential_IsActive DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PaymentGatewayCredential_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PaymentGatewayCredential_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Billing.PaymentGatewayCredential', N'ProcessorEndpointUrl') IS NULL
BEGIN
    ALTER TABLE Billing.PaymentGatewayCredential ADD ProcessorEndpointUrl NVARCHAR(500) NULL;
END;

IF OBJECT_ID(N'Billing.PaymentMethodToken', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.PaymentMethodToken
    (
        PaymentMethodTokenId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentMethodToken PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        PaymentGatewayCredentialId UNIQUEIDENTIFIER NOT NULL,
        ProviderCode NVARCHAR(80) NOT NULL,
        TokenReference NVARCHAR(300) NOT NULL,
        PaymentMethodType NVARCHAR(40) NOT NULL,
        Last4 NVARCHAR(4) NULL,
        Brand NVARCHAR(40) NULL,
        ExpirationMonth INT NULL,
        ExpirationYear INT NULL,
        IsDefault BIT NOT NULL CONSTRAINT DF_PaymentMethodToken_IsDefault DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_PaymentMethodToken_IsActive DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PaymentMethodToken_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PaymentMethodToken_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Billing.PaymentProcessorOperation', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.PaymentProcessorOperation
    (
        PaymentProcessorOperationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentProcessorOperation PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        PaymentId UNIQUEIDENTIFIER NULL,
        PaymentGatewayCredentialId UNIQUEIDENTIFIER NOT NULL,
        ProviderCode NVARCHAR(80) NOT NULL,
        OperationType NVARCHAR(40) NOT NULL,
        StatusCode NVARCHAR(40) NOT NULL,
        Amount DECIMAL(18,2) NULL,
        CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_PaymentProcessorOperation_CurrencyCode DEFAULT N'USD',
        ProviderOperationId NVARCHAR(200) NULL,
        ProviderStatus NVARCHAR(100) NULL,
        CheckoutUrl NVARCHAR(1000) NULL,
        FailureCode NVARCHAR(100) NULL,
        FailureMessage NVARCHAR(1000) NULL,
        RequestPayloadJson NVARCHAR(MAX) NULL,
        RetryCount INT NOT NULL CONSTRAINT DF_PaymentProcessorOperation_RetryCount DEFAULT 0,
        NextRetryDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PaymentProcessorOperation_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        CompletedDateUtc DATETIME2 NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PaymentProcessorOperation_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Billing.PaymentProcessorOperation', N'RequestPayloadJson') IS NULL
BEGIN
    ALTER TABLE Billing.PaymentProcessorOperation ADD RequestPayloadJson NVARCHAR(MAX) NULL;
END;

IF OBJECT_ID(N'Billing.PaymentWebhookEvent', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.PaymentWebhookEvent
    (
        PaymentWebhookEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentWebhookEvent PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ProviderCode NVARCHAR(80) NOT NULL,
        EnvironmentCode NVARCHAR(20) NOT NULL,
        EventType NVARCHAR(120) NOT NULL,
        ProviderEventId NVARCHAR(200) NOT NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL,
        SignatureHeader NVARCHAR(1000) NULL,
        IsProcessed BIT NOT NULL CONSTRAINT DF_PaymentWebhookEvent_IsProcessed DEFAULT 0,
        ReceivedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PaymentWebhookEvent_ReceivedDateUtc DEFAULT SYSUTCDATETIME(),
        ProcessedDateUtc DATETIME2 NULL,
        ProcessingError NVARCHAR(1000) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PaymentWebhookEvent_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Billing.PaymentSettlementBatch', N'U') IS NULL
BEGIN
    CREATE TABLE Billing.PaymentSettlementBatch
    (
        PaymentSettlementBatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PaymentSettlementBatch PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        PaymentGatewayCredentialId UNIQUEIDENTIFIER NOT NULL,
        ProviderCode NVARCHAR(80) NOT NULL,
        SettlementBatchReference NVARCHAR(200) NOT NULL,
        SettlementDateUtc DATETIME2 NOT NULL,
        GrossAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PaymentSettlementBatch_GrossAmount DEFAULT 0,
        FeeAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PaymentSettlementBatch_FeeAmount DEFAULT 0,
        NetAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_PaymentSettlementBatch_NetAmount DEFAULT 0,
        StatusCode NVARCHAR(40) NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PaymentSettlementBatch_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PaymentSettlementBatch_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Billing.PaymentSettlementBatch', N'CreatedByUserId') IS NULL
BEGIN
    ALTER TABLE Billing.PaymentSettlementBatch ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH(N'Billing.PaymentSettlementBatch', N'ModifiedDateUtc') IS NULL
BEGIN
    ALTER TABLE Billing.PaymentSettlementBatch ADD ModifiedDateUtc DATETIME2 NULL;
END;

IF COL_LENGTH(N'Billing.PaymentSettlementBatch', N'ModifiedByUserId') IS NULL
BEGIN
    ALTER TABLE Billing.PaymentSettlementBatch ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CredentialSecretRow
    {
        public string? EncryptedApiKey { get; set; }
        public string? EncryptedApiSecret { get; set; }
        public string? EncryptedPublishableKey { get; set; }
        public string? EncryptedWebhookSecret { get; set; }
        public string? EncryptedLoginId { get; set; }
        public string? EncryptedTransactionKey { get; set; }
        public string? EncryptedClientKey { get; set; }
        public string? EncryptedMerchantId { get; set; }
    }
}
