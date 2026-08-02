using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Ams.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Request was cancelled by the client.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business rule validation failed.");
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Business rule validation failed", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Request validation failed.");
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Request validation failed", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "The request is not authorized.");
            await WriteProblemAsync(context, HttpStatusCode.Forbidden, "The request is not authorized", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "The requested resource was not found.");
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "Resource not found", ex.Message);
        }
        catch (SqlException ex) when (ex.Number is >= 52400 and <= 52599)
        {
            _logger.LogWarning(ex, "Persistence business or concurrency rule failed with SQL error {ErrorNumber}.", ex.Number);
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "The request conflicts with the current workflow state", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "An unexpected error occurred", "The request could not be completed.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode status, string title, string detail)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = context.Response.StatusCode,
            Instance = context.Request.Path
        });
    }
}
