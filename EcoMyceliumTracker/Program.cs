using System.Threading.RateLimiting;
using DotNetEnv;
using EcoMyceliumTracker.Configuration;
using EcoMyceliumTracker.Endpoints;
using EcoMyceliumTracker.Infrastructure.Errors;
using EcoMyceliumTracker.Infrastructure.Health;
using EcoMyceliumTracker.Infrastructure.Persistence;
using EcoMyceliumTracker.Repositories;
using EcoMyceliumTracker.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "HH:mm:ss ");
}
else
{
    builder.Logging.AddJsonConsole();
}

builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("PostgresConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "A connection string 'PostgresConnection' não foi configurada.");
    }

    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DatabaseMigrationRunner>();
builder.Services.AddScoped<IMyceliumRepository, MyceliumRepository>();
builder.Services.AddScoped<ISensorRepository, SensorRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EcoMyceliumTracker API",
        Version = "v1",
        Description = "API for mycelium networks, sensors and nutrient transfers."
    });
    options.AddSecurityDefinition(ApiKeyAuthenticationDefaults.Scheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = ApiKeyAuthenticationDefaults.HeaderName,
        In = ParameterLocation.Header,
        Description = "API key used to access /api endpoints."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(ApiKeyAuthenticationDefaults.Scheme, document, null)] = []
    });
});

builder.Services
    .AddOptions<TransferOptions>()
    .Bind(builder.Configuration.GetSection(TransferOptions.SectionName))
    .Validate(options => options.HighEnergyThresholdMg > 0,
        "Transfers:HighEnergyThresholdMg must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.Scheme,
        _ => { });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    var permitLimit = Math.Max(1, builder.Configuration.GetValue("RateLimit:PermitLimit", 100));
    var windowSeconds = Math.Max(1, builder.Configuration.GetValue("RateLimit:WindowSeconds", 60));
    var queueLimit = Math.Max(0, builder.Configuration.GetValue("RateLimit:QueueLimit", 0));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "The request rate limit was exceeded.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "rate_limit_exceeded";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        await context.HttpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };
    options.AddPolicy("api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});

var telemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("EcoMyceliumTracker"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddNpgsql())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    telemetry.UseOtlpExporter();
    builder.Logging.AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.AddOtlpExporter();
    });
}

var app = builder.Build();

if (app.Configuration.GetValue("Database:RunMigrations", true))
{
    await app.Services.GetRequiredService<DatabaseMigrationRunner>()
        .MigrateAsync(app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler();

// The API surface is only published outside production, where every endpoint
// already sits behind an API key.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "EcoMyceliumTracker API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithTags("Health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
})
    .AllowAnonymous();

app.MapApi();

app.Run();

public partial class Program
{
}
