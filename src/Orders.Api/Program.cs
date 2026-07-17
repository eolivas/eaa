using System.Diagnostics.Metrics;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using OpenTelemetry.Trace;
using Orders.Api.Endpoints;
using Orders.Api.Mcp;
using Orders.Api.Middleware;
using Orders.Application.Behaviours;
using Orders.Application.Commands;
using Orders.Application.Interfaces;
using Orders.Domain;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using Serilog;

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

// --- Repository and event publisher ---
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IApplicationEventPublisher, MassTransitEventPublisher>();

// --- MassTransit (InMemory for development) ---
builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

// --- JWT Bearer Authentication ---
builder.Services.AddAuthentication()
    .AddJwtBearer(opt =>
    {
        opt.Authority = builder.Configuration["Jwt:Authority"];
        opt.Audience = builder.Configuration["Jwt:Audience"];
    });

builder.Services.AddAuthorization();

// --- OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .WithTracing(tp => tp
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter());

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

var app = builder.Build();

// --- Middleware pipeline ---
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Orders API");
app.MapOrdersEndpoints();
app.UseMiddleware<McpRateLimiterMiddleware>();
app.MapMcp();

app.Run();
