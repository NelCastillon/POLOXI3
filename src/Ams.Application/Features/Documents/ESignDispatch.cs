namespace Ams.Application.Features.Documents;

public sealed record ESignDispatchWorkItem(
    Guid ESignDispatchId,
    Guid TenantId,
    Guid ESignRequestId,
    Guid DocumentId,
    string FileName,
    string StoragePath,
    string? ContentType,
    string SignerName,
    string SignerEmail,
    string? Message,
    string IdempotencyKey,
    int AttemptCount,
    int MaxAttempts,
    string AccountId,
    string IntegrationKey,
    string UserId,
    string OAuthBaseUri,
    string ApiBaseUri,
    string SecretReference);

public sealed record ESignEnvelopeDispatchResult(
    string ProviderEnvelopeId,
    string ProviderStatus,
    string? ProviderRecipientId);

public sealed record ESignDispatchFailure(
    string ErrorCode,
    string ErrorMessage,
    bool IsRetryable,
    DateTime? RetryAtUtc = null);
