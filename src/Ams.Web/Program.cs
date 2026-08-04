using Ams.Web;
using Ams.Web.Components;
using Ams.Web.Security;
using Ams.Web.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
});

builder.Services.Configure<ContactIntakeSecurityOptions>(builder.Configuration.GetSection("ContactIntake:Security"));

var contactAllowedOrigins = builder.Configuration.GetSection("ContactIntake:AllowedOrigins").Get<string[]>() ?? [];
var contactSecurityOptions = builder.Configuration.GetSection("ContactIntake:Security").Get<ContactIntakeSecurityOptions>() ?? new();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ContactIntake", policy =>
    {
        if (contactAllowedOrigins.Length > 0)
        {
            policy.WithOrigins(contactAllowedOrigins)
                .WithMethods("POST", "OPTIONS")
                .WithHeaders(
                    "content-type",
                    "x-requested-with",
                    "x-contact-rendered-at",
                    "x-contact-elapsed-ms",
                    "x-contact-honeypot")
                .SetPreflightMaxAge(TimeSpan.FromHours(1));
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ContactIntake", httpContext =>
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ipAddress = forwardedFor?.Split(',').FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(ipAddress))
            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var partitionKey = $"{ipAddress}|{userAgent.GetHashCode(StringComparison.Ordinal)}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, contactSecurityOptions.PermitLimit),
            Window = TimeSpan.FromMinutes(Math.Max(1, contactSecurityOptions.WindowMinutes)),
            QueueLimit = Math.Max(0, contactSecurityOptions.QueueLimit),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-Ams.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/sign-out";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantAdmin", policy => policy.RequireRole("SYSTEM_ADMIN", "TENANT_ADMIN"));
    options.AddPolicy("UsersManage", policy => policy.AddRequirements(new PermissionRequirement("USER_MANAGE")));
    options.AddPolicy("RolesManage", policy => policy.AddRequirements(new PermissionRequirement("ROLE_MANAGE")));
    options.AddPolicy("ReportsView", policy => policy.AddRequirements(new PermissionRequirement("REPORT_VIEW")));
    options.AddPolicy("CrmView", policy => policy.AddRequirements(new PermissionRequirement("CRM_VIEW")));
    options.AddPolicy("BillingView", policy => policy.AddRequirements(new PermissionRequirement("BILLING_VIEW")));
    foreach (var permission in new[]
    {
        KnowledgePolicies.ConceptsRead,
        KnowledgePolicies.ConceptsManage,
        KnowledgePolicies.MappingsRead,
        KnowledgePolicies.MappingsManage,
        KnowledgePolicies.MappingsApprove,
        KnowledgePolicies.RulesManage,
        KnowledgePolicies.Publish,
        KnowledgePolicies.Import,
        KnowledgePolicies.AuditRead,
        DocumentIntakePolicies.Read,
        DocumentIntakePolicies.Upload,
        DocumentIntakePolicies.Review,
        DocumentIntakePolicies.Reprocess,
        DocumentIntakePolicies.Promote,
        DocumentIntakePolicies.Admin
    }.Concat(IntelligencePolicies.All))
    {
        options.AddPolicy(permission, policy => policy.AddRequirements(new PermissionRequirement(permission)));
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<ContactIntakeNotificationOptions>(builder.Configuration.GetSection("ContactIntake:Notification"));
builder.Services.AddScoped<IContactIntakeNotificationService, SmtpContactIntakeNotificationService>();

// Scoped: each Blazor Server circuit gets its own shell state
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ShellStateService>();
builder.Services.AddScoped<BreadcrumbService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NavigationContextService>();
builder.Services.AddScoped<ConfirmationDialogService>();
builder.Services.AddScoped<LeadDialogService>();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddTransient<LeadScoringRealtimeClient>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ActingUserHeaderHandler>();

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7051/");
}).AddHttpMessageHandler<ActingUserHeaderHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapPost("/auth/sign-in", async (
    [FromForm] LoginForm form,
    ApiClient apiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    static string loginRedirect(string message, LoginForm form) =>
        $"/login?error={Uri.EscapeDataString(message)}&email={Uri.EscapeDataString(form.Email ?? string.Empty)}&tenant={Uri.EscapeDataString(form.Tenant ?? string.Empty)}&returnUrl={Uri.EscapeDataString(form.ReturnUrl ?? string.Empty)}";

    var email = (form.Email ?? string.Empty).Trim();
    var tenantText = (form.Tenant ?? string.Empty).Trim();
    var password = form.Password ?? string.Empty;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(tenantText) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect(loginRedirect("Enter your organization, email, and password.", form));
    }

    PagedResult<TenantDto>? tenantResult;
    try
    {
        tenantResult = await apiClient.SearchTenantsAsync(tenantText, 1, 25, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
        return Results.Redirect(loginRedirect($"The AMS API could not be reached or returned an error: {ex.Message}", form));
    }

    var tenant = tenantResult?.Items.FirstOrDefault(t =>
        string.Equals(t.TenantCode, tenantText, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(t.TenantName, tenantText, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(t.PrimaryDomain, tenantText, StringComparison.OrdinalIgnoreCase))
        ?? tenantResult?.Items.FirstOrDefault();

    if (tenant is null || !tenant.IsActive || !string.Equals(tenant.StatusCode, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Redirect(loginRedirect("The organization is not active or could not be found.", form));
    }

    LoginValidationResultDto? loginResult;
    try
    {
        loginResult = await apiClient.ValidateLoginAsync(tenant.TenantId, email, password, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
        return Results.Redirect(loginRedirect($"The AMS API could not validate the credentials: {ex.Message}", form));
    }

    if (loginResult is null)
    {
        return Results.Redirect(loginRedirect("The credentials provided could not be verified.", form));
    }

    if (loginResult.RequiresTwoFactor)
    {
        if (loginResult.Challenge is null)
        {
            return Results.Redirect(loginRedirect("Two-factor authentication could not be started for this account.", form));
        }

        var twoFactorUrl = $"/login/2fa?tenantId={loginResult.Challenge.TenantId}&challengeId={loginResult.Challenge.ChallengeId}&destination={Uri.EscapeDataString(loginResult.Challenge.DestinationMasked)}&returnUrl={Uri.EscapeDataString(form.ReturnUrl ?? string.Empty)}&rememberMe={form.RememberMe}";
        return Results.Redirect(twoFactorUrl);
    }

    if (loginResult.User is null)
    {
        return Results.Redirect(loginRedirect("The credentials provided could not be verified.", form));
    }

    await SignInAuthenticatedUserAsync(httpContext, loginResult.User, tenant, form.RememberMe);

    var returnUrl = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl;
    if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
    {
        returnUrl = "/";
    }

    return Results.Redirect(returnUrl);
});

app.MapPost("/auth/2fa/verify", async (
    [FromForm] TwoFactorLoginForm form,
    ApiClient apiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    static string verifyRedirect(string message, TwoFactorLoginForm form) =>
        $"/login/2fa?tenantId={form.TenantId}&challengeId={form.ChallengeId}&destination={Uri.EscapeDataString(form.Destination ?? string.Empty)}&returnUrl={Uri.EscapeDataString(form.ReturnUrl ?? string.Empty)}&rememberMe={form.RememberMe}&error={Uri.EscapeDataString(message)}";

    if (form.TenantId == Guid.Empty || form.ChallengeId == Guid.Empty || string.IsNullOrWhiteSpace(form.Code))
    {
        return Results.Redirect(verifyRedirect("Enter the SMS verification code.", form));
    }

    AuthenticatedUserDto? user;
    try
    {
        user = await apiClient.VerifyTwoFactorAsync(new VerifyTwoFactorRequest
        {
            TenantId = form.TenantId,
            ChallengeId = form.ChallengeId,
            Code = form.Code.Trim()
        }, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
        return Results.Redirect(verifyRedirect($"The AMS API could not verify the SMS code: {ex.Message}", form));
    }

    if (user is null)
    {
        return Results.Redirect(verifyRedirect("The SMS verification code could not be verified.", form));
    }

    PagedResult<TenantDto>? tenantResult;
    try
    {
        tenantResult = await apiClient.SearchTenantsAsync(user.TenantId.ToString(), 1, 1, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
        return Results.Redirect(verifyRedirect($"The AMS API could not load the tenant context: {ex.Message}", form));
    }

    var tenant = tenantResult?.Items.FirstOrDefault(t => t.TenantId == user.TenantId);
    if (tenant is null)
    {
        return Results.Redirect(verifyRedirect("The organization context could not be loaded.", form));
    }

    await SignInAuthenticatedUserAsync(httpContext, user, tenant, form.RememberMe);

    var returnUrl = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl;
    if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
    {
        returnUrl = "/";
    }

    return Results.Redirect(returnUrl);
});

static async Task SignInAuthenticatedUserAsync(HttpContext httpContext, AuthenticatedUserDto user, TenantDto tenant, bool rememberMe)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.FullName : user.DisplayName),
        new(ClaimTypes.Email, user.Email),
        new("sub", user.UserId.ToString()),
        new("tenant_id", user.TenantId.ToString()),
        new("tenantId", user.TenantId.ToString()),
        new("tenant_code", tenant.TenantCode),
        new("tenant_name", tenant.TenantName),
        new("user_name", user.UserName),
        new("mfa_enabled", user.MfaEnabled.ToString())
    };

    foreach (var role in user.RoleCodes)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    foreach (var permission in user.PermissionCodes)
    {
        claims.Add(new Claim("permission", permission));
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    var properties = new AuthenticationProperties
    {
        IsPersistent = rememberMe,
        ExpiresUtc = DateTimeOffset.UtcNow.Add(rememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8)),
        AllowRefresh = true
    };

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
}

app.MapGet("/auth/sign-out", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login?signedOut=true");
});

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public sealed class LoginForm
{
    public string? Tenant { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public sealed class TwoFactorLoginForm
{
    public Guid TenantId { get; set; }
    public Guid ChallengeId { get; set; }
    public string? Code { get; set; }
    public string? Destination { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
