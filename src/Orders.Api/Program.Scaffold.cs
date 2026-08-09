using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Api.Middleware;
using Orders.Application.Behaviours;
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
// TODO: Register your Application layer assembly here
// builder.Services.AddMediatR(cfg =>
// {
//     cfg.RegisterServicesFromAssembly(typeof(YourCommand).Assembly);
//     cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
//     cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
// });

// --- FluentValidation ---
// TODO: Register validators from your Application assembly
// builder.Services.AddValidatorsFromAssembly(typeof(YourCommand).Assembly);

// --- EF Core ---
// TODO: Register your DbContext
// builder.Services.AddDbContext<YourDbContext>(opt =>
//     opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultDb")));

// --- HttpContextAccessor ---
builder.Services.AddHttpContextAccessor();

// --- JWT Bearer Authentication ---
builder.Services.AddAuthentication()
    .AddJwtBearer(opt =>
    {
        opt.Authority = builder.Configuration["Jwt:Authority"];
        opt.Audience = builder.Configuration["Jwt:Audience"];
    });

builder.Services.AddAuthorization();

// --- Health Checks ---
builder.Services.AddHealthChecks();

// --- OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Kestrel request body size limit ---
builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = 1_048_576);

var app = builder.Build();

// --- Middleware pipeline ---
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestBodySizeLimitMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// --- OpenAPI endpoint ---
app.UseSwagger(c => c.RouteTemplate = "openapi/{documentName}.json");

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "API v1");
    });
}

// --- Health Check Endpoints ---
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
}).AllowAnonymous();

app.MapGet("/", () => "API is running");

// TODO: Map your endpoints here
// app.MapYourEndpoints();

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
