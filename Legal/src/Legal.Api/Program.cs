using Legal.Api.Middlewares;
using Legal.Api.Security;
using Legal.Infrastructure.DependencyInjection;
using Legal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in IntelligencePolicies.All)
        options.AddPolicy(permission, policy => policy.AddRequirements(new IntelligencePermissionRequirement(permission)));
});
builder.Services.AddSingleton<IAuthorizationHandler, IntelligencePermissionAuthorizationHandler>();
builder.Services.AddLegalInfrastructure(builder.Configuration);
// Ambient tenant for runtime-effective DB-backed EpistemicAuthoritySettings resolution.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Legal.Application.Abstractions.Services.IEpistemicTenantAccessor, Legal.Api.Security.HttpEpistemicTenantAccessor>();
// Async start+poll transport for long-running POLOXI Wide searches (transport only; pipeline unchanged).
builder.Services.AddSingleton<Legal.Api.Services.WideSearchOperationStore>();

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
