using MessageQueueNET.Razor.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
// Aspire
builder.AddServiceDefaults();
#endif

CreateDefaultConfig(builder);
builder.Configuration.AddJsonFile(Path.Combine("_config", "dashboard.json"), optional: true);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;

    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 10000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddDashboardService(config =>
{
    builder.Configuration.GetSection("dashboard").Bind(config);
});

var app = builder.Build();

#if DEBUG
// Aspire
app.MapDefaultEndpoints();
#endif

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// First start (e.g. from the ZIP download): without _config/dashboard.json and without servers from other sources
// (appsettings, environment variables DASHBOARD__QUEUES__...) the dashboard has nothing to show.
// Create _config/dashboard.json from _config/proto/_dashboard.json (the REST API on http://localhost:9091).
static void CreateDefaultConfig(WebApplicationBuilder builder)
{
    var configFile = Path.Combine(builder.Environment.ContentRootPath, "_config", "dashboard.json");
    var protoFile = Path.Combine(builder.Environment.ContentRootPath, "_config", "proto", "_dashboard.json");

    if (File.Exists(configFile)
        || !File.Exists(protoFile)
        || builder.Configuration.GetSection("dashboard:Queues").GetChildren().Any())
    {
        return;
    }

    try
    {
        File.Copy(protoFile, configFile);

        // same output as the first start setup of the REST API
        Console.WriteLine(new string('#', 113));
        Console.WriteLine("Setup:");
        Console.WriteLine("First start: creating simple _config/dashboard.json. You can modify this file for your servers...");
        Console.WriteLine(new string('#', 113));
        Console.WriteLine(File.ReadAllText(configFile));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Setup: can't create {configFile}: {ex.Message}");
    }
}
