using ObservabilityDemo.Endpoints;
using ObservabilityDemo.Extensions;
using ObservabilityDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// THIS IS ALL YOU NEED FOR OBSERVABILITY!
// Similar to Aspire's: builder.AddServiceDefaults();
// ============================================================
builder.AddObservability();

// Register application services
builder.Services.AddHttpClient<IExternalApiService, ExternalApiService>(client =>
{
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Map endpoints
app.MapDemoEndpoints();
app.MapHealthChecks("/health");

// Root endpoint with API info
app.MapGet("/", () => Results.Ok(new
{
    service = "observability-demo",
    version = "1.0.0",
    endpoints = new[]
    {
        "GET /api/demo/hello - Simple hello endpoint",
        "GET /api/demo/users/{id} - Get user from external API",
        "GET /api/demo/posts?limit={n} - Get posts from external API",
        "GET /api/demo/slow?delayMs={n} - Configurable delay endpoint",
        "GET /api/demo/error - Intentional error endpoint",
        "GET /health - Health check"
    }
}));

app.Run();
