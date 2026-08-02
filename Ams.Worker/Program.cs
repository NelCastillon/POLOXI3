using Ams.Infrastructure.DependencyInjection;
using Ams.Infrastructure.Persistence;
using Ams.Worker.Automation;
using Ams.Worker.Automation.Executors;
using Ams.Worker.Accounting;
using Ams.Worker.Certificates;
using Ams.Worker.Compliance;
using Ams.Worker.Payments;
using Ams.Worker.Renewals;
using Ams.Worker.Submissions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: false);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<AutomationJobOrchestrator>();
builder.Services.AddScoped<IJobStepExecutorRegistry, JobStepExecutorRegistry>();
builder.Services.AddScoped<IJobStepExecutor, FileIngestionStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, CarrierDownloadStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, CarrierDownloadMatchStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, CarrierDownloadApplyUpdatesStepExecutor>();
builder.Services.AddScoped<IJobStepExecutor, NotificationStepExecutor>();
builder.Services.AddHttpClient(nameof(ApiRatingConnectorWorkerService));
builder.Services.AddHttpClient(nameof(ProposalDeliveryWorkerService));
builder.Services.AddHostedService<AutomationWorkerService>();
builder.Services.AddHostedService<PaymentPlatformWorkerService>();
builder.Services.AddHostedService<SubmitToMarketDispatchWorkerService>();
builder.Services.AddHostedService<ProposalDeliveryWorkerService>();
builder.Services.AddHostedService<QuoteRequestFollowUpWorkerService>();
builder.Services.AddHostedService<ApiRatingConnectorWorkerService>();
builder.Services.AddHostedService<CertificateRenewalWorkerService>();
builder.Services.AddHostedService<LeadDncScreeningWorker>();
builder.Services.AddHostedService<PolicyRenewalInitiationWorkerService>();
builder.Services.AddHostedService<PolicyGenerationWorkerService>();
builder.Services.AddHostedService<PolicyCreatedAccountingWorkerService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();
}

await host.RunAsync();
