using Ams.Application.Abstractions.Services;
using Ams.Infrastructure.DependencyInjection;
using Ams.Infrastructure.Persistence;
using Ams.Knowledge.Infrastructure.DependencyInjection;
using Ams.Knowledge.Infrastructure.Persistence;
using Ams.Worker.Knowledge;
using Ams.Worker.Automation;
using Ams.Worker.Automation.Executors;
using Ams.Worker.Accounting;
using Ams.Worker.Certificates;
using Ams.Worker.Compliance;
using Ams.Worker.Communications;
using Ams.Worker.Documents;
using Ams.Worker.Endorsements;
using Ams.Worker.Intelligence;
using Ams.Worker.Payments;
using Ams.Worker.Renewals;
using Ams.Worker.Search;
using Ams.Worker.Submissions;
using Ams.Application.Features.DocumentIntake;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: false);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKnowledgeInfrastructure(builder.Configuration);
builder.Services.AddScoped<IDocumentKnowledgeNormalizer, KnowledgeDocumentNormalizer>();
builder.Services.AddScoped<IDocumentIntakeProcessor, DocumentIntakeProcessor>();
builder.Services.AddScoped<IntelligenceWorkerProcessor>();
var intakeOtlpEndpoint=builder.Configuration["DocumentIntake:Telemetry:OtlpEndpoint"]??Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource=>resource.AddService("Ams.Worker"))
    .WithTracing(tracing=>
    {
        tracing.AddSource(DocumentIntakeTelemetry.SourceName).AddHttpClientInstrumentation();
        if(Uri.TryCreate(intakeOtlpEndpoint,UriKind.Absolute,out var endpoint))tracing.AddOtlpExporter(options=>options.Endpoint=endpoint);
    })
    .WithMetrics(metrics=>
    {
        metrics.AddMeter(DocumentIntakeTelemetry.SourceName).AddHttpClientInstrumentation();
        if(Uri.TryCreate(intakeOtlpEndpoint,UriKind.Absolute,out var endpoint))metrics.AddOtlpExporter(options=>options.Endpoint=endpoint);
    });

builder.Services.AddScoped<AutomationJobOrchestrator>();
builder.Services.AddScoped<IJobStepExecutorRegistry, JobStepExecutorRegistry>();
builder.Services.AddScoped<IJobStepExecutor, FileIngestionStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, CarrierDownloadStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, CarrierDownloadMatchStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, CarrierDownloadApplyUpdatesStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, NotificationStepExecutor>();
builder.Services.AddHttpClient(nameof(ApiRatingConnectorWorkerService));
builder.Services.AddHttpClient(nameof(ProposalDeliveryWorkerService));
builder.Services.AddHttpClient(nameof(PolicyEndorsementCarrierWorkerService));
builder.Services.AddHostedService<AutomationWorkerService>();
builder.Services.AddHostedService<PaymentPlatformWorkerService>();
builder.Services.AddHostedService<SubmitToMarketDispatchWorkerService>();
builder.Services.AddHostedService<ProposalDeliveryWorkerService>();
builder.Services.AddHostedService<NotificationDeliveryWorkerService>();
builder.Services.AddHostedService<QuoteRequestFollowUpWorkerService>();
builder.Services.AddHostedService<ApiRatingConnectorWorkerService>();
builder.Services.AddHostedService<CertificateRenewalWorkerService>();
builder.Services.AddHostedService<LeadDncScreeningWorker>();
builder.Services.AddHostedService<PolicyRenewalInitiationWorkerService>();
builder.Services.AddHostedService<PolicyGenerationWorkerService>();
builder.Services.AddHostedService<PolicyCreatedAccountingWorkerService>();
builder.Services.AddHostedService<ESignDispatchWorkerService>();
builder.Services.AddHostedService<PolicyEndorsementCarrierWorkerService>();
builder.Services.AddHostedService<PolicyEndorsementAccountingWorkerService>();
builder.Services.AddHostedService<PolicyEndorsementDocumentWorkerService>();
builder.Services.AddHostedService<KnowledgeWorkerService>();
builder.Services.AddHostedService<DocumentIntakeWorkerService>();
builder.Services.AddHostedService<DocumentIntakeMalwareWorkerService>();
builder.Services.AddHostedService<DocumentIntakeRetentionWorkerService>();
builder.Services.AddHostedService<DocumentIntakePromptEvaluationWorkerService>();
builder.Services.AddHostedService<DocumentIntakeTelemetryWorkerService>();
builder.Services.AddHostedService<IntelligenceWorkerService>();
builder.Services.AddHostedService<SearchProjectionWorkerService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();

    var knowledgeMigrator = scope.ServiceProvider.GetRequiredService<KnowledgeDatabaseMigrator>();
    await knowledgeMigrator.MigrateAsync();
}

await host.RunAsync();
