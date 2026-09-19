using System.Text.Json;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Transactional outbox dispatcher (Phase B).
//
// Drains SaaS_Outbox rows atomically claimed by DequeueOutboxBatchAsync and
// dispatches them via IJudzEmailSender. Retry-safe: a claimed message increments
// its attempt count; failures reschedule with backoff up to MaxAttempts, after
// which the row is marked Failed. Email is thus sent AFTER the inviting
// transaction commits, so DB success never depends on SMTP availability.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class OutboxDispatcher(
    ISaasRepository repository,
    IJudzEmailSender emailSender,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var messages = await repository.DequeueOutboxBatchAsync(batchSize, ct);
        var processed = 0;

        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message, ct);
                await repository.MarkOutboxSentAsync(message.OutboxId, ct);
                processed++;
            }
            catch (Exception ex)
            {
                // Exponential-ish backoff capped at ~1 hour.
                var delayMinutes = Math.Min(60, (int)Math.Pow(2, Math.Min(message.AttemptCount, 6)));
                var nextAttemptUtc = DateTime.UtcNow.AddMinutes(delayMinutes);
                logger.LogWarning(ex, "Outbox message {OutboxId} ({MessageType}) failed on attempt {Attempt}; next attempt {Next}.",
                    message.OutboxId, message.MessageType, message.AttemptCount, nextAttemptUtc);
                await repository.MarkOutboxFailedAsync(message.OutboxId, ex.Message, nextAttemptUtc, ct);
            }
        }

        return processed;
    }

    private async Task DispatchAsync(OutboxMessageDto message, CancellationToken ct)
    {
        switch (message.MessageType)
        {
            case InvitationService.InvitationEmailMessageType:
                var payload = JsonSerializer.Deserialize<InvitationEmailPayload>(message.PayloadJson)
                    ?? throw new InvalidOperationException("Invitation email payload could not be deserialized.");
                await emailSender.SendInvitationAsync(payload.Email, payload.TenantName, payload.RoleName, payload.AcceptUrl, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown outbox message type '{message.MessageType}'.");
        }
    }
}
