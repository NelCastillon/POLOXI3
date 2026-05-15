using Ams.Web;
using Ams.Web.Components;
using Ams.Web.Security;
using Ams.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syncfusion.Blazor;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var syncfusionKey = builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionKey))
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
});
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddSyncfusionBlazor();

// Scoped: each Blazor Server circuit gets its own shell state
builder.Services.AddScoped<ShellStateService>();
builder.Services.AddScoped<BreadcrumbService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NavigationContextService>();
builder.Services.AddScoped<ConfirmationDialogService>();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddTransient<LeadScoringRealtimeClient>();

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7051/");
});

builder.Services.AddHttpClient<ProducerWorkbenchApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7051/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
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

    var tenantResult = await apiClient.SearchTenantsAsync(tenantText, 1, 25, cancellationToken);
    var tenant = tenantResult?.Items.FirstOrDefault(t =>
        string.Equals(t.TenantCode, tenantText, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(t.TenantName, tenantText, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(t.PrimaryDomain, tenantText, StringComparison.OrdinalIgnoreCase))
        ?? tenantResult?.Items.FirstOrDefault();

    if (tenant is null || !tenant.IsActive || !string.Equals(tenant.StatusCode, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Redirect(loginRedirect("The organization is not active or could not be found.", form));
    }

    var user = await apiClient.ValidateLoginAsync(tenant.TenantId, email, password, cancellationToken);
    if (user is null)
    {
        return Results.Redirect(loginRedirect("The credentials provided could not be verified.", form));
    }

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
        IsPersistent = form.RememberMe,
        ExpiresUtc = DateTimeOffset.UtcNow.Add(form.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8)),
        AllowRefresh = true
    };

    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

    var returnUrl = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl;
    if (!returnUrl.StartsWith("/", StringComparison.Ordinal) || returnUrl.StartsWith("//", StringComparison.Ordinal))
    {
        returnUrl = "/";
    }

    return Results.Redirect(returnUrl);
});

app.MapGet("/auth/sign-out", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login?signedOut=true");
});

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
