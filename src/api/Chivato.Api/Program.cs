using Chivato.Api.Services;
using Chivato.Application;
using Chivato.Application.Common;
using Chivato.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Chivato API",
        Version = "v1",
        Description = "Azure Infrastructure Drift Monitor API"
    });
    c.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "Chivato API",
        Version = "v2",
        Description = "Azure Infrastructure Drift Monitor API - CQRS"
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5280",
            builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5280"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Configuration
var storageConnectionString = builder.Configuration["StorageConnectionString"]
    ?? Environment.GetEnvironmentVariable("StorageConnectionString")
    ?? "UseDevelopmentStorage=true";

var serviceBusConnectionString = builder.Configuration["ServiceBusConnectionString"]
    ?? Environment.GetEnvironmentVariable("ServiceBusConnectionString")
    ?? string.Empty;

// ========================================
// Clean Architecture: Application Layer
// ========================================
builder.Services.AddApplication();

// ========================================
// Clean Architecture: Infrastructure Layer
// ========================================
builder.Services.AddInfrastructure(storageConnectionString, serviceBusConnectionString);

// ========================================
// Current User (from HttpContext)
// ========================================
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Health checks
builder.Services.AddHealthChecks();

// Application Insights
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chivato API v1");
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "Chivato API v2 (CQRS)");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapGet("/health/ready", () => Results.Ok(new { status = "ready" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

// Root redirect to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
