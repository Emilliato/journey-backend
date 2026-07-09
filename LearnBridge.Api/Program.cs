var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    app = "LearnBridge API",
    timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck");

app.Run();
