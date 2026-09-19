using Legal.Web.Services;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<BreadcrumbService>();

// Bridges the active Blazor circuit's services to the ApiClient message handler
// pipeline so AuthRedirectHandler can reach the initialized NavigationManager.
builder.Services.AddScoped<CircuitServicesAccessor>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, CircuitServicesAccessorHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "judz.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddTransient<ActingUserHandler>();
builder.Services.AddTransient<AuthRedirectHandler>();
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:7251/");
    // 100-second HttpClient default; the pipeline governs its own budgets via AI.Legal_FeaturePolicy.
    client.Timeout = TimeSpan.FromMinutes(30);
})
    .AddHttpMessageHandler<ActingUserHandler>()
    // Any 401/403 from the API redirects the user to /login from every page.
    .AddHttpMessageHandler<AuthRedirectHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// ── Server-side auth endpoints ───────────────────────────────────────────────
// Login runs as a plain request so a cookie can be written before the response
// starts (interactive components cannot call HttpContext.SignInAsync safely).
app.MapPost("/auth/login", async (HttpContext http, ApiClient apiClient) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();

    var login = await apiClient.LoginAsync(new Legal.Application.Features.Saas.LoginRequest(email, password));

    if (!login.Succeeded)
    {
        if (login.RequiresVerification)
            return Results.Redirect($"/verify-email?email={Uri.EscapeDataString(email)}");
        var message = Uri.EscapeDataString(login.Message ?? "Invalid credentials.");
        return Results.Redirect($"/login?error={message}&email={Uri.EscapeDataString(email)}");
    }

    if (login.UserId is not { } userId || userId == Guid.Empty)
        return Results.Redirect("/login?error=Sign%20in%20failed.");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId.ToString()),
        new("sub", userId.ToString()),
        new(ClaimTypes.Name, string.IsNullOrWhiteSpace(login.DisplayName) ? email : login.DisplayName!)
    };
    if (!string.IsNullOrWhiteSpace(login.Email))
        claims.Add(new Claim(ClaimTypes.Email, login.Email!));
    if (login.TenantId is { } tenantId && tenantId != Guid.Empty)
        claims.Add(new Claim("tenant_id", tenantId.ToString()));
    // Persist the user's real, role-derived permissions so the API can enforce
    // per-capability access on forwarded claims instead of an open dev bypass.
    if (login.Permissions is { Count: > 0 } permissions)
        foreach (var permission in permissions)
            claims.Add(new Claim("permission", permission));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/legal/search");
});

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapRazorComponents<Legal.Web.App>()
    .AddInteractiveServerRenderMode();
app.Run();
