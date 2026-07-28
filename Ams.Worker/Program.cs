using Ams.Infrastructure.DependencyInjection;
using Ams.Worker.Automation;
using Ams.Worker.Automation.Executors;
using Ams.Worker.Certificates;
using Ams.Worker.Compliance;
using Ams.Worker.Payments;
using Ams.Worker.Renewals;
using Ams.Worker.Submissions;

var builder = Host.CreateApplicationBuilder(args);

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
builder.Services.AddHostedService<AutomationWorkerService>();
builder.Services.AddHostedService<PaymentPlatformWorkerService>();
builder.Services.AddHostedService<SubmitToMarketDispatchWorkerService>();
builder.Services.AddHostedService<QuoteRequestFollowUpWorkerService>();
builder.Services.AddHostedService<ApiRatingConnectorWorkerService>();
builder.Services.AddHostedService<CertificateRenewalWorkerService>();
builder.Services.AddHostedService<LeadDncScreeningWorker>();
builder.Services.AddHostedService<PolicyRenewalInitiationWorkerService>();

var host = builder.Build();
host.Run();
