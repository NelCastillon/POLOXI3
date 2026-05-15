using Microsoft.AspNetCore.SignalR.Client;

namespace Ams.Web.Services;

public sealed class LeadScoringRealtimeClient : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private HubConnection? _connection;
    private Guid _tenantId;

    public LeadScoringRealtimeClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task StartAsync(Guid tenantId, Func<Guid, Task> onLeadScoresChanged, CancellationToken cancellationToken = default)
    {
        if (_connection is not null && _tenantId == tenantId)
        {
            return;
        }

        await DisposeAsync();
        _tenantId = tenantId;

        var baseUrl = _configuration["Api:BaseUrl"] ?? "https://localhost:7051/";
        var hubUrl = new Uri(new Uri(baseUrl), "hubs/lead-scoring");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<Guid>("LeadScoresChanged", changedTenantId =>
        {
            return changedTenantId == _tenantId
                ? onLeadScoresChanged(changedTenantId)
                : Task.CompletedTask;
        });

        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("JoinTenant", tenantId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            if (_connection.State == HubConnectionState.Connected && _tenantId != Guid.Empty)
            {
                await _connection.InvokeAsync("LeaveTenant", _tenantId);
            }
        }
        catch
        {
        }

        await _connection.DisposeAsync();
        _connection = null;
        _tenantId = Guid.Empty;
    }
}
