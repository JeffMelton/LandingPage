using Azure.Storage.Blobs;
using LandingPage.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register HttpClient for scraping
builder.Services.AddHttpClient<IApodArchiveScraper, ApodArchiveScraper>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register Azure Blob Storage (connection string from app settings)
var storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
if (!string.IsNullOrEmpty(storageConnectionString))
{
    builder.Services.AddSingleton(new BlobServiceClient(storageConnectionString));
}

// Register services as singletons for in-memory caching
builder.Services.AddSingleton<IDayOfWeekRouter, DayOfWeekRouter>();
builder.Services.AddSingleton<IApodSelector, ApodSelector>();

// Note: ApodArchiveScraper is already registered as singleton via AddHttpClient above

builder.Build().Run();
