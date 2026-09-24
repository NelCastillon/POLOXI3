using Legal.Api.Middlewares;
using Legal.Api.Security;
using Legal.Infrastructure.DependencyInjection;
using Legal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
// Authentication: the Development scheme (open, demo fallback) is used only in the
// Development environment. In all other environments the hardened ForwardedIdentity
// scheme validates an HMAC signature over the forwarded X-Acting-* headers, so the
// API never trusts unsigned identity headers in Production.
var authScheme = builder.Environment.IsDevelopment()
    ? DevelopmentAuthenticationHandler.SchemeName
    : ForwardedIdentityAuthenticationHandler.SchemeName;
var authentication = builder.Services.AddAuthentication(authScheme);
if (builder.Environment.IsDevelopment())
    authentication.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, _ => { });
else
    authentication.AddScheme<AuthenticationSchemeOptions, ForwardedIdentityAuthenticationHandler>(ForwardedIdentityAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in IntelligencePolicies.All)
        options.AddPolicy(permission, policy => policy.AddRequirements(new IntelligencePermissionRequirement(permission)));
});
builder.Services.AddSingleton<IAuthorizationHandler, IntelligencePermissionAuthorizationHandler>();
builder.Services.AddLegalInfrastructure(builder.Configuration);
// Judz.ai Early Access SaaS — ASP.NET Core Identity (host owns the ASP.NET Core framework reference).
// The Identity EF Core store DbContext is registered by AddLegalInfrastructure; here we configure the
// Identity core services, roles, token providers and sign-in manager used by the auth controllers.
builder.Services.AddIdentityCore<Legal.Infrastructure.Identity.ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .AddEntityFrameworkStores<Legal.Infrastructure.Identity.JudzIdentityDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
// Ambient tenant for runtime-effective DB-backed EpistemicAuthoritySettings resolution.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Legal.Application.Abstractions.Services.IEpistemicTenantAccessor, Legal.Api.Security.HttpEpistemicTenantAccessor>();
// Host-backed execution environment so the decision service can enforce PROD-only rules (DEV Logic
// is rejected server-side in a Production deployment).
builder.Services.AddSingleton<Legal.Application.Abstractions.Services.IExecutionEnvironment, Legal.Api.Security.HostExecutionEnvironment>();
// Async start+poll transport for long-running POLOXI Wide searches (transport only; pipeline unchanged).
builder.Services.AddSingleton<Legal.Api.Services.WideSearchOperationStore>();
// Isolated Wide2 transport backing /legal/personalinjury_decision2 (separate code path).
builder.Services.AddSingleton<Legal.Api.Services.Wide2SearchOperationStore>();
// Transactional outbox drain worker
builder.Services.AddHostedService<Legal.Api.Services.OutboxDrainHostedService>();
builder.Services.AddHostedService<Legal.Api.Services.LegalDocumentSearchProjectionHostedService>();

var app = builder.Build();

// ── Run Legal database migrations on startup ─────────────────────
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<LegalDatabaseMigrator>();
    await migrator.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
