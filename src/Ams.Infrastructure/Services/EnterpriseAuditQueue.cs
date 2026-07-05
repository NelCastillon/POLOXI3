using System.Threading.Channels;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ams.Infrastructure.Services;

public sealed class EnterpriseAuditQueue : BackgroundService, IEnterpriseAuditQueue
{
    private readonly Channel<LogEnterpriseAuditEventRequest> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnterpriseAuditQueue> _logger;

    public EnterpriseAuditQueue(IServiceScopeFactory scopeFactory, ILogger<EnterpriseAuditQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queue = Channel.CreateBounded<LogEnterpriseAuditEventRequest>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask QueueAsync(LogEnterpriseAuditEventRequest request, CancellationToken cancellationToken = default)
        => _queue.Writer.WriteAsync(request, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IEnterpriseAuditService>();
                await service.LogAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist queued enterprise audit event {ActionType} for tenant {TenantId}.", request.ActionType, request.TenantId);
            }
        }
    }
}
