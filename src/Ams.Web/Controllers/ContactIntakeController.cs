using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.ContactIntake;
using Ams.Web.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Ams.Web.Controllers;

[ApiController]
[Route("api/contact")]
[EnableCors("ContactIntake")]
[IgnoreAntiforgeryToken]
[RequestSizeLimit(32_768)]
public sealed class ContactIntakeController : ControllerBase
{
    private readonly IContactIntakeService _service;
    private readonly IContactIntakeNotificationService _notificationService;
    private readonly ContactIntakeSecurityOptions _securityOptions;
    private readonly ILogger<ContactIntakeController> _logger;

    public ContactIntakeController(
        IContactIntakeService service,
        IContactIntakeNotificationService notificationService,
        IOptions<ContactIntakeSecurityOptions> securityOptions,
        ILogger<ContactIntakeController> logger)
    {
        _service = service;
        _notificationService = notificationService;
        _securityOptions = securityOptions.Value;
        _logger = logger;
    }

    [HttpPost("demo-requests")]
    [Consumes("application/json")]
    [EnableRateLimiting("ContactIntake")]
    [ProducesResponseType(typeof(ContactDemoSubmissionResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitDemoRequest([FromBody] CreateContactDemoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var abuseValidationResult = ValidateAbuseSignals();
            if (abuseValidationResult is not null)
                return abuseValidationResult;

            var context = CreateContext();
            var result = await _service.SubmitDemoRequestAsync(request, context, cancellationToken);
            try
            {
                await _notificationService.SendSubmissionNotificationAsync(request, result, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Contact intake notification failed for request {RequestNumber}.", result.RequestNumber);
            }

            return Created($"/api/contact/demo-requests/{result.RequestId}", result);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError("request", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contact demo request submission failed.");
            return Problem(
                title: "Contact request could not be submitted.",
                detail: "Please try again later or contact sales@agencybinder.com.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("options")]
    [EnableRateLimiting("ContactIntake")]
    [ProducesResponseType(typeof(IReadOnlyList<ContactIntakeOptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        var options = await _service.GetOptionsAsync(cancellationToken);
        return Ok(options);
    }

    private IActionResult? ValidateAbuseSignals()
    {
        if (!_securityOptions.Enabled)
            return null;

        if (!Request.HasJsonContentType())
        {
            _logger.LogWarning("Contact intake rejected unsupported content type {ContentType} from {RemoteIp}.", Request.ContentType, HttpContext.Connection.RemoteIpAddress);
            return Problem(
                title: "Unsupported request content.",
                detail: "Submit contact requests as JSON.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        var honeypot = Request.Headers["X-Contact-Honeypot"].ToString();
        if (!string.IsNullOrWhiteSpace(honeypot) && honeypot != ":")
        {
            _logger.LogWarning("Contact intake honeypot triggered from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            return BadRequestProblem("Contact request could not be submitted.");
        }

        var userAgent = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent) || userAgent.Length > _securityOptions.MaxUserAgentLength)
        {
            _logger.LogWarning("Contact intake rejected suspicious user agent from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            return BadRequestProblem("Contact request could not be submitted.");
        }

        var origin = Request.Headers.Origin.ToString();
        if (origin.Length > _securityOptions.MaxOriginLength)
        {
            _logger.LogWarning("Contact intake rejected oversized origin header from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            return BadRequestProblem("Contact request could not be submitted.");
        }

        var elapsedHeader = Request.Headers["X-Contact-Elapsed-Ms"].ToString();
        if (string.IsNullOrWhiteSpace(elapsedHeader))
        {
            _logger.LogInformation("Contact intake elapsed time header was not provided by {RemoteIp}; continuing with rate-limit and validation controls.", HttpContext.Connection.RemoteIpAddress);
            return null;
        }

        if (!int.TryParse(elapsedHeader, out var elapsedMs))
        {
            _logger.LogWarning("Contact intake rejected invalid elapsed time from {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);
            return BadRequestProblem("Contact request could not be submitted.");
        }

        if (elapsedMs < _securityOptions.MinFormCompletionMilliseconds || elapsedMs > _securityOptions.MaxFormCompletionMilliseconds)
        {
            _logger.LogWarning("Contact intake rejected suspicious completion time {ElapsedMs} from {RemoteIp}.", elapsedMs, HttpContext.Connection.RemoteIpAddress);
            return BadRequestProblem("Contact request could not be submitted.");
        }

        return null;
    }

    private IActionResult BadRequestProblem(string detail) => Problem(
        title: "Invalid contact request.",
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest);

    private ContactDemoRequestContext CreateContext() => new()
    {
        RemoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent = Request.Headers.UserAgent.ToString(),
        Referrer = Request.Headers.Referer.ToString(),
        Origin = Request.Headers.Origin.ToString()
    };
}
