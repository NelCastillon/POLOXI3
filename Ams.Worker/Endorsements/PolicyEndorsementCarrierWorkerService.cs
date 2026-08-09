using System.Net.Http.Headers;
using System.Text;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Ams.Worker.Automation;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Endorsements;

public sealed class PolicyEndorsementCarrierWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WorkerOptions _options;
    private readonly ILogger<PolicyEndorsementCarrierWorkerService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public PolicyEndorsementCarrierWorkerService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, IOptions<WorkerOptions> options, ILogger<PolicyEndorsementCarrierWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPolicyEndorsementRepository>();
                var items = await repository.ClaimCarrierDispatchesAsync(_workerId, Math.Clamp(_options.MaxEndorsementWorkItemsPerPoll, 1, 100), Lease, stoppingToken);
                foreach (var item in items)
                    await ProcessAsync(repository, item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Endorsement carrier dispatch polling cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessAsync(IPolicyEndorsementRepository repository, PolicyEndorsementCarrierDispatchWorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(item.ChannelCode, "Api", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.ChannelCode, "CarrierApi", StringComparison.OrdinalIgnoreCase))
                throw new CarrierDispatchException("UNSUPPORTED_AUTOMATION_CHANNEL", "The configured carrier channel requires tracked manual, portal, email, or download handling and cannot be marked as automatically submitted.", false);

            if (!Uri.TryCreate(item.EndpointUri, UriKind.Absolute, out var endpoint))
                throw new CarrierDispatchException("CONFIGURATION_ERROR", "A valid carrier endpoint is required.", false);

            using var request = new HttpRequestMessage(new HttpMethod(string.IsNullOrWhiteSpace(item.HttpMethod) ? "POST" : item.HttpMethod), endpoint)
            {
                Content = new StringContent(item.RequestPayload, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("Idempotency-Key", item.IdempotencyKey);
            ApplyAuthentication(request, item);

            var client = _httpClientFactory.CreateClient(nameof(PolicyEndorsementCarrierWorkerService));
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(item.TimeoutSeconds, 5, 300));
            using var response = await client.SendAsync(request, cancellationToken);
            var responsePayload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new CarrierDispatchException("CARRIER_HTTP_ERROR", $"Carrier returned HTTP {(int)response.StatusCode}.", (int)response.StatusCode is 408 or 429 or >= 500, responsePayload, (int)response.StatusCode);

            var reference = response.Headers.TryGetValues("x-reference-id", out var values) ? values.FirstOrDefault() : null;
            await repository.CompleteCarrierDispatchAsync(item.CarrierDispatchId, _workerId, new("Submitted", reference, responsePayload, (int)response.StatusCode), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CarrierDispatchException exception)
        {
            await repository.FailCarrierDispatchAsync(item.CarrierDispatchId, _workerId, new(exception.ErrorCode, exception.Message, exception.IsRetryable, ResponsePayload: exception.ResponsePayload, HttpStatusCode: exception.HttpStatusCode), cancellationToken);
            _logger.LogWarning(exception, "Endorsement carrier dispatch {DispatchId} failed.", item.CarrierDispatchId);
        }
        catch (Exception exception)
        {
            await repository.FailCarrierDispatchAsync(item.CarrierDispatchId, _workerId, new("DISPATCH_ERROR", exception.Message, true), cancellationToken);
            _logger.LogError(exception, "Endorsement carrier dispatch {DispatchId} failed unexpectedly.", item.CarrierDispatchId);
        }
    }

    private static void ApplyAuthentication(HttpRequestMessage request, PolicyEndorsementCarrierDispatchWorkItem item)
    {
        if (string.Equals(item.AuthenticationTypeCode, "Bearer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.SecretReference))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", item.SecretReference);
        else if (string.Equals(item.AuthenticationTypeCode, "ApiKey", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.SecretReference))
            request.Headers.TryAddWithoutValidation("x-api-key", item.SecretReference);
    }

    private TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(10, _options.EndorsementPollIntervalSeconds));
    private TimeSpan Lease => TimeSpan.FromMinutes(Math.Clamp(_options.EndorsementClaimLeaseMinutes, 1, 120));

    private sealed class CarrierDispatchException(string errorCode, string message, bool isRetryable, string? responsePayload = null, int? httpStatusCode = null) : Exception(message)
    {
        public string ErrorCode { get; } = errorCode;
        public bool IsRetryable { get; } = isRetryable;
        public string? ResponsePayload { get; } = responsePayload;
        public int? HttpStatusCode { get; } = httpStatusCode;
    }
}
