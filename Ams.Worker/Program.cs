using Ams.Infrastructure.DependencyInjection;
using Ams.Worker.Automation;
using Ams.Worker.Automation.Executors;
using Ams.Worker.Payments;
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
builder.Services.AddHostedService<AutomationWorkerService>();
builder.Services.AddHostedService<PaymentPlatformWorkerService>();
builder.Services.AddHostedService<SubmitToMarketDispatchWorkerService>();
builder.Services.AddHostedService<QuoteRequestFollowUpWorkerService>();

var host = builder.Build();
host.Run();
