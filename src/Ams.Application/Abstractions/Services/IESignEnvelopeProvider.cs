using Ams.Application.Features.Documents;

namespace Ams.Application.Abstractions.Services;

public interface IESignEnvelopeProvider
{
    Task<ESignEnvelopeDispatchResult> SendAsync(
        ESignDispatchWorkItem workItem,
        Stream documentContent,
        CancellationToken cancellationToken = default);
}

public sealed class ESignProviderException : Exception
{
    public ESignProviderException(string errorCode, string message, bool isRetryable, DateTime? retryAtUtc = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsRetryable = isRetryable;
        RetryAtUtc = retryAtUtc;
    }

    public string ErrorCode { get; }
    public bool IsRetryable { get; }
    public DateTime? RetryAtUtc { get; }
}
