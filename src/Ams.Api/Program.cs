using Ams.Api.Extensions;
using Ams.Api.Filters;
using Ams.Api.Hubs;
using Ams.Api.Middlewares;
using Ams.Api.Security;
using Ams.Infrastructure.DependencyInjection;
using Ams.Infrastructure.Persistence;
using Ams.Knowledge.Infrastructure.DependencyInjection;
using Ams.Knowledge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<EntityAuditActionFilter>());
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
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
        KnowledgePolicies.AuditRead
    })
    {
        options.AddPolicy(permission, policy => policy.AddRequirements(new KnowledgePermissionRequirement(permission)));
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, KnowledgePermissionAuthorizationHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKnowledgeInfrastructure(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();

// ── Run database migrations on startup ───────────────────────
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();

    var knowledgeMigrator = scope.ServiceProvider.GetRequiredService<KnowledgeDatabaseMigrator>();
    await knowledgeMigrator.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<LeadScoringHub>("/hubs/lead-scoring");
app.MapHealthChecks("/health");
app.Run();
