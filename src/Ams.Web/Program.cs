using Ams.Web;
using Ams.Web.Components;
using Ams.Web.Services;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

var syncfusionKey = builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionKey))
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSyncfusionBlazor();

// Scoped: each Blazor Server circuit gets its own shell state
builder.Services.AddScoped<ShellStateService>();
builder.Services.AddScoped<BreadcrumbService>();
builder.Services.AddScoped<NotificationService>();

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
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
