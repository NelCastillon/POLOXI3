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
using Ams.Application.Features.DocumentIntake;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Ams.Infrastructure.Services;
using Ams.Api.Services;
using Ams.Application.Abstractions.Intelligence;
using Azure.Core;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<EntityAuditActionFilter>());
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddCheck<DocumentIntakeReadinessHealthCheck>("document-intake-readiness",tags:["ready","document-intake"]);
builder.Services.AddProblemDetails();
var intakeOtlpEndpoint=builder.Configuration["DocumentIntake:Telemetry:OtlpEndpoint"]??Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource=>resource.AddService("Ams.Api"))
    .WithTracing(tracing=>
    {
        tracing.AddSource(DocumentIntakeTelemetry.SourceName).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if(Uri.TryCreate(intakeOtlpEndpoint,UriKind.Absolute,out var endpoint))tracing.AddOtlpExporter(options=>options.Endpoint=endpoint);
    })
    .WithMetrics(metrics=>
    {
        metrics.AddMeter(DocumentIntakeTelemetry.SourceName).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if(Uri.TryCreate(intakeOtlpEndpoint,UriKind.Absolute,out var endpoint))metrics.AddOtlpExporter(options=>options.Endpoint=endpoint);
    });
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
        KnowledgePolicies.AuditRead,
        DocumentIntakePolicies.Read,
        DocumentIntakePolicies.Upload,
        DocumentIntakePolicies.Review,
        DocumentIntakePolicies.Reprocess,
        DocumentIntakePolicies.Promote,
        DocumentIntakePolicies.Admin
    }.Concat(IntelligencePolicies.All))
    {
        options.AddPolicy(permission, policy => policy.AddRequirements(
            permission.StartsWith("DMS.INTAKE.", StringComparison.Ordinal)
                ? new DocumentIntakePermissionRequirement(permission)
                : permission.StartsWith("Intelligence.",StringComparison.Ordinal)
                    ? new IntelligencePermissionRequirement(permission)
                    : new KnowledgePermissionRequirement(permission)));
    }
});
builder.Services.AddSingleton<IAuthorizationHandler, KnowledgePermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, DocumentIntakePermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, IntelligencePermissionAuthorizationHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<TokenCredential>(_ =>
    builder.Environment.IsDevelopment()
        ? new AzureCliCredential()
        : new ManagedIdentityCredential());
builder.Services.AddKnowledgeInfrastructure(builder.Configuration);
builder.Services.AddScoped<ISemanticQueryExpander,KnowledgeSemanticQueryExpander>();
// Async start+poll transport for long-running POLOXI Wide searches (transport only; pipeline unchanged).
builder.Services.AddSingleton<Ams.Api.Services.WideSearchOperationStore>();
builder.Services.AddApiServices();

var app = builder.Build();

// ── Run database migrations on startup ───────────────────────
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();

    var knowledgeMigrator = scope.ServiceProvider.GetRequiredService<KnowledgeDatabaseMigrator>();
    await knowledgeMigrator.MigrateAsync();

    var promptSeeder = scope.ServiceProvider.GetRequiredService<IntelligencePromptSeeder>();
    await promptSeeder.SeedAsync();
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
app.MapHealthChecks("/health/ready",new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate=registration=>registration.Tags.Contains("ready"),
    ResponseWriter=async(context,report)=>
    {
        context.Response.ContentType="application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status=report.Status.ToString(),
            totalDurationMilliseconds=report.TotalDuration.TotalMilliseconds,
            checks=report.Entries.Select(entry=>new{component=entry.Key,status=entry.Value.Status.ToString(),message=entry.Value.Description,durationMilliseconds=entry.Value.Duration.TotalMilliseconds})
        });
    }
});
app.Run();
