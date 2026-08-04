using ObjectStorage.Blazor.Components;
using ObjectStorage.Blazor.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddSingleton<TimingLogStore>();

builder.Services.AddHttpClient<BackupApiClient>(client =>
{
    string baseUrl =
        builder.Configuration["BackupApi:BaseUrl"]
        ?? "http://localhost:5213";

    client.BaseAddress =
        new Uri(baseUrl);

    client.Timeout =
        TimeSpan.FromMinutes(120);
});

var app = builder.Build();

string? pathBase =
    builder.Configuration["PathBase"];

if (!string.IsNullOrWhiteSpace(pathBase) &&
    pathBase != "/")
{
    app.UsePathBase(pathBase.TrimEnd('/'));
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
