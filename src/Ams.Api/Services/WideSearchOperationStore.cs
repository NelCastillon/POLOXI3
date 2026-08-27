using System.Collections.Concurrent;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Intelligence;

namespace Ams.Api.Services;

// Async start+poll transport for long-running POLOXI Wide searches. The pipeline runs unchanged on
// a background task with its own DI scope; this store only tracks per-operation status and the
// final response so no HTTP request has to outlive the pipeline. In-memory and single-instance by
// design; expired operations are evicted after the retention window.
public sealed class WideSearchOperationStore(IServiceScopeFactory scopeFactory,ILogger<WideSearchOperationStore> logger)
{
    private static readonly TimeSpan Retention=TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<Guid,Operation> _operations=new();

    public Guid Start(WideSearchRequest request)
    {
        EvictExpired();
        var operationId=Guid.NewGuid();
        var operation=new Operation(request.TenantId);
        _operations[operationId]=operation;
        // Background execution: own scope (the wide service is scoped) and no request-linked token —
        // the caller's HTTP request completes immediately and must not cancel the pipeline. The
        // operation's own token supports explicit user-initiated cancellation via Cancel().
        _=Task.Run(async()=>
        {
            try
            {
                using var scope=scopeFactory.CreateScope();
                var service=scope.ServiceProvider.GetRequiredService<IIntelligenceWideService>();
                var response=await service.SearchDynamicAsync(request,operation.Token);
                operation.Complete(response);
            }
            catch(OperationCanceledException)when(operation.Token.IsCancellationRequested)
            {
                operation.MarkCancelled();
            }
            catch(Exception ex)
            {
                logger.LogError(ex,"POLOXI Wide background operation {OperationId} failed.",operationId);
                operation.Fail(ex.Message);
            }
        });
        return operationId;
    }

    // Explicit user-initiated cancellation (tenant-scoped). Idempotent; returns false when unknown.
    public bool Cancel(Guid tenantId,Guid operationId)
    {
        if(!_operations.TryGetValue(operationId,out var operation)||operation.TenantId!=tenantId)return false;
        operation.RequestCancel();
        return true;
    }

    public WideSearchOperationStatusResponse? GetStatus(Guid tenantId,Guid operationId)
    {
        EvictExpired();
        if(!_operations.TryGetValue(operationId,out var operation)||operation.TenantId!=tenantId)return null;
        var status=operation.Snapshot();
        // Completed/failed operations are removed once observed so results are not retained longer than needed.
        if(status.StatusCode is not "RUNNING")_operations.TryRemove(operationId,out _);
        return status with{OperationId=operationId};
    }

    private void EvictExpired()
    {
        var cutoff=DateTime.UtcNow-Retention;
        foreach(var(key,operation)in _operations)
            if(operation.CreatedUtc<cutoff)_operations.TryRemove(key,out _);
    }

    private sealed class Operation(Guid tenantId)
    {
        private readonly object _gate=new();
        private readonly CancellationTokenSource _cancellation=new();
        private WideSearchResponse? _response;
        private string? _error;
        private string _statusCode="RUNNING";
        public Guid TenantId{get;}=tenantId;
        public DateTime CreatedUtc{get;}=DateTime.UtcNow;
        public CancellationToken Token=>_cancellation.Token;

        public void Complete(WideSearchResponse response){lock(_gate){_response=response;_statusCode="COMPLETED";}}
        public void Fail(string error){lock(_gate){_error=error;_statusCode="FAILED";}}
        public void MarkCancelled(){lock(_gate){_statusCode="CANCELLED";}}
        public void RequestCancel(){try{_cancellation.Cancel();}catch(ObjectDisposedException){/* already finished */}}
        public WideSearchOperationStatusResponse Snapshot(){lock(_gate){return new(Guid.Empty,_statusCode,_response,_error);}}
    }
}
