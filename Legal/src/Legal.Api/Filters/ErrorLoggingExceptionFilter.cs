using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Legal.Api.Filters;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Global API exception filter that persists every unhandled controller/action exception into the
// enterprise error-log store. Registered for all controllers, so it covers every module reachable
// through the API without per-controller wiring. It is fail-soft (logging never masks or replaces
// the framework's normal ProblemDetails response) and never marks the exception handled.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ErrorLoggingExceptionFilter(IErrorLogService errorLog) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var httpContext = context.HttpContext;
        var request = httpContext.Request;

        var descriptor = context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
        var module = descriptor?.ControllerName is { Length: > 0 } controller
            ? $"Api.{controller}"
            : "Api";
        var operation = descriptor?.ActionName ?? $"{request.Method} {request.Path}";

        var contextJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            method = request.Method,
            path = request.Path.Value,
            query = request.QueryString.Value,
            traceIdentifier = httpContext.TraceIdentifier
        });

        var entry = new ErrorLogEntry
        {
            Module = module,
            Operation = operation,
            SeverityCode = "Error",
            Message = context.Exception.Message,
            ExceptionType = context.Exception.GetType().FullName,
            StackTrace = context.Exception.ToString(),
            Source = context.Exception.Source,
            CorrelationId = httpContext.TraceIdentifier,
            ContextJson = contextJson,
            TenantId = AuthenticatedRequestContext.GetTenantId(httpContext.User),
            UserId = AuthenticatedRequestContext.GetUserId(httpContext.User)
        };

        await errorLog.LogAsync(entry, httpContext.RequestAborted);

        // Do not mark handled: let the framework produce its normal ProblemDetails/error response.
    }
}
