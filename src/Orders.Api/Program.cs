using System.Diagnostics.Metrics;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Orders.Api.Endpoints;
using Orders.Api.Extensions;
using Orders.Api.Mcp;
using Orders.Api.Middleware;
using Orders.Api.Services;
using Orders.Application.Behaviours;
using Orders.Application.Commands;
using Orders.Application.Interfaces;
using Orders.Domain;
using Orders.Infrastructure.Http;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using Serilog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
    .WriteTo.OpenTelemetry()
    .Enrich.WithProperty("Service", ctx.Configuration["Service"] ?? "Orders.Api"));

// --- MediatR with pipeline behaviours ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(PlaceOrderCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
});

// --- FluentValidation ---
builder.Services.AddValidatorsFromAssembly(typeof(PlaceOrderCommand).Assembly);

// --- EF Core with Npgsql ---
builder.Services.AddDbContext<OrdersDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("OrdersDb")));

// --- HttpContextAccessor and Correlation ID Accessor ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();

// --- Repository and event publisher ---
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IApplicationEventPublisher, MassTransitEventPublisher>();

// --- MassTransit messaging (conditional RabbitMQ/InMemory transport) ---
builder.Services.AddMessaging(builder.Configuration);

// --- JWT Bearer Authentication ---
builder.Services.AddAuthentication()
    .AddJwtBearer(opt =>
    {
        opt.Authority = builder.Configuration["Jwt:Authority"];
        opt.Audience = builder.Configuration["Jwt:Audience"];
    });

builder.Services.AddAuthorization();

// --- Rate Limiting ---
builder.Services.AddOrdersRateLimiter(builder.Configuration);

// --- CORS ---
builder.Services.AddCorsPolicy(builder.Configuration);

// --- Health Checks (Req 5) ---
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("OrdersDb")!,
        timeout: TimeSpan.FromSeconds(5),
        tags: ["ready"])
    .AddRabbitMQ(
        new Uri($"amqp://{builder.Configuration["RabbitMq:Username"] ?? "guest"}:{builder.Configuration["RabbitMq:Password"] ?? "guest"}@{builder.Configuration["RabbitMq:Host"] ?? "localhost"}"),
        timeout: TimeSpan.FromSeconds(5),
        tags: ["ready"]);

// --- OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .WithTracing(tp => tp
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(mp => mp
        .AddAspNetCoreInstrumentation()
        .AddMeter("Orders.Mcp")
        .AddOtlpExporter());

// --- OpenAPI (Req 10) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- HTTP Client Resilience (Req 11) ---
builder.Services.AddInventoryHttpClient(builder.Configuration);

// --- MCP Model Tier & Context Budget ---
builder.Services.Configure<ModelTierOptions>(builder.Configuration.GetSection("Mcp:ModelTiers"));
builder.Services.Configure<ContextBudgetOptions>(builder.Configuration.GetSection("Mcp:ContextBudget"));
builder.Services.AddSingleton<ContextBudgetEnforcer>();

// --- Distributed Cache (in-memory fallback) ---
builder.Services.AddDistributedMemoryCache();

// --- MCP Semantic Cache ---
builder.Services.Configure<McpSemanticCacheOptions>(
    builder.Configuration.GetSection("Mcp:SemanticCache"));

var mcpMeter = new Meter("Orders.Mcp");
var mcpCacheHitsCounter = mcpMeter.CreateCounter<long>("mcp.cache.hits", description: "Number of MCP semantic cache hits");
builder.Services.AddSingleton(mcpCacheHitsCounter);
builder.Services.AddSingleton<McpSemanticCache>();

// --- MCP Rate Limiting ---
builder.Services.Configure<McpRateLimitOptions>(
    builder.Configuration.GetSection("Mcp:RateLimit"));

// --- MCP Server ---
builder.Services.AddMcpServer()
    .WithTools<OrderMcpTools>();

// --- MCP Token Instrumentation ---
builder.Services.AddSingleton<McpTokenInstrumentation>();

// --- Kestrel request body size limit (secondary safeguard) ---
builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = 1_048_576);

var app = builder.Build();

// --- Database migration ---
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

    if (app.Environment.IsDevelopment())
    {
        try
        {
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to apply database migrations. Shutting down.");
            Environment.Exit(1);
        }

        await DatabaseSeeder.SeedAsync(dbContext);
    }
    else
    {
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var count = pendingMigrations.Count();
        if (count > 0)
        {
            Log.Warning("There are {Count} pending migrations. Apply them before deploying to production.", count);
        }
    }
}

// --- Middleware pipeline (correct ordering) ---

// 1. Security Headers (outermost — applies to all responses)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Correlation ID (before all other middleware so all logs have it)
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Exception Handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 4. Request Body Size Limit (reject oversized payloads early)
app.UseMiddleware<RequestBodySizeLimitMiddleware>();

// 5. CORS (must be before rate limiting and auth)
app.UseCors();

// 6. Rate Limiting
app.UseRateLimiter();

// 7. Rate Limit Headers
app.UseMiddleware<RateLimitHeadersMiddleware>();

// 8. Authentication
app.UseAuthentication();

// 9. Authorization
app.UseAuthorization();

// --- OpenAPI endpoint (always served, no auth) ---
app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");

// --- Swagger UI (Development only, Req 10) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Orders API v1");
    });
}

// --- Health Check Endpoints (Req 5) ---
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // No checks — liveness just confirms process is running
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

app.MapGet("/", () => "Orders API");
app.MapOrdersEndpoints();
app.UseMiddleware<McpRateLimiterMiddleware>();
app.MapMcp();

app.Run();

// --- Health check JSON response writer ---
static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var entries = new Dictionary<string, object>();
    foreach (var entry in report.Entries)
    {
        entries[entry.Key] = new
        {
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.ToString()
        };
    }

    var result = new
    {
        status = report.Status.ToString(),
        entries
    };

    return context.Response.WriteAsync(
        JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}
