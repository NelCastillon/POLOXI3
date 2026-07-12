using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Payments;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/payment-platform")]
public sealed class PaymentPlatformController : ControllerBase
{
    private readonly IPaymentPlatformService _service;

    public PaymentPlatformController(IPaymentPlatformService service) => _service = service;

    [HttpGet("credentials")]
    public async Task<IActionResult> SearchCredentials([FromQuery] Guid tenantId, [FromQuery] string? providerCode, [FromQuery] string? environment, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return BadRequest("Tenant is required.");
        return Ok(await _service.SearchCredentialsAsync(tenantId, providerCode, environment, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("credentials")]
    public async Task<IActionResult> UpsertCredential([FromBody] UpsertPaymentGatewayCredentialRequest request, CancellationToken cancellationToken)
        => Ok(new { paymentGatewayCredentialId = await _service.UpsertCredentialAsync(request, cancellationToken) });

    [HttpGet("tokens")]
    public async Task<IActionResult> SearchPaymentMethodTokens([FromQuery] Guid tenantId, [FromQuery] Guid? accountId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return BadRequest("Tenant is required.");
        return Ok(await _service.SearchPaymentMethodTokensAsync(tenantId, accountId, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("tokens")]
    public async Task<IActionResult> TokenizePaymentMethod([FromBody] TokenizePaymentMethodRequest request, CancellationToken cancellationToken)
        => Ok(new { paymentMethodTokenId = await _service.TokenizePaymentMethodAsync(request, cancellationToken) });

    [HttpPost("checkout-sessions")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateCheckoutSessionAsync(request, cancellationToken));

    [HttpPost("charges")]
    public async Task<IActionResult> Charge([FromBody] ChargePaymentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ChargeAsync(request, cancellationToken));

    [HttpPost("refunds")]
    public async Task<IActionResult> Refund([FromBody] RefundPaymentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.RefundAsync(request, cancellationToken));

    [HttpPost("voids")]
    public async Task<IActionResult> Void([FromBody] VoidPaymentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.VoidAsync(request, cancellationToken));

    [HttpGet("operations")]
    public async Task<IActionResult> SearchOperations([FromQuery] Guid tenantId, [FromQuery] string? providerCode, [FromQuery] string? statusCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return BadRequest("Tenant is required.");
        return Ok(await _service.SearchOperationsAsync(tenantId, providerCode, statusCode, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("webhook-events")]
    public async Task<IActionResult> SearchWebhookEvents([FromQuery] Guid tenantId, [FromQuery] string? providerCode, [FromQuery] bool? isProcessed, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return BadRequest("Tenant is required.");
        return Ok(await _service.SearchWebhookEventsAsync(tenantId, providerCode, isProcessed, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("webhooks")]
    public async Task<IActionResult> IngestWebhook([FromBody] PaymentWebhookIngestRequest request, CancellationToken cancellationToken)
        => Ok(new { paymentWebhookEventId = await _service.IngestWebhookAsync(request, cancellationToken) });

    [HttpGet("settlements")]
    public async Task<IActionResult> SearchSettlementBatches([FromQuery] Guid tenantId, [FromQuery] string? providerCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return BadRequest("Tenant is required.");
        return Ok(await _service.SearchSettlementBatchesAsync(tenantId, providerCode, pageNumber, pageSize, cancellationToken));
    }

    [HttpPost("retries/process")]
    public async Task<IActionResult> ProcessRetries([FromQuery] int maxCount = 50, CancellationToken cancellationToken = default)
        => Ok(new { processed = await _service.ProcessDueRetriesAsync(maxCount, cancellationToken) });

    [HttpPost("settlements/poll")]
    public async Task<IActionResult> PollSettlements([FromQuery] int maxCredentials = 50, CancellationToken cancellationToken = default)
        => Ok(new { processed = await _service.PollSettlementsAsync(maxCredentials, cancellationToken) });
}
