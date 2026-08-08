using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orders.Infrastructure.Persistence;
using Orders.Integration.Tests.Auth;
using Testcontainers.PostgreSql;
using Xunit;

namespace Orders.Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory that replaces production dependencies with test doubles:
/// - PostgreSQL via Testcontainers for database isolation
/// - MassTransit InMemory test harness for message assertions
/// - Test authentication handler that always succeeds
/// </summary>
public class OrdersWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("integration_tests")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    public string ConnectionString => _postgresContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:OrdersDb", _postgresContainer.GetConnectionString());
        builder.UseSetting("RabbitMq:Host", "localhost");
        builder.UseSetting("Outbox:PollingIntervalSeconds", "1");

        builder.ConfigureServices(services =>
        {
            // Replace EF Core DbContext with Testcontainers PostgreSQL
            services.RemoveAll<DbContextOptions<OrdersDbContext>>();
            services.RemoveAll<OrdersDbContext>();

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<OrdersDbContext>));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            services.AddDbContext<OrdersDbContext>(opt =>
                opt.UseNpgsql(_postgresContainer.GetConnectionString()));

            // Replace MassTransit with InMemory test harness
            RemoveMassTransitServices(services);

            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumers(typeof(Orders.Infrastructure.Messaging.MessagingServiceCollectionExtensions).Assembly);
            });

            // Replace Authentication with test handler
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.RemoveAll<IAuthenticationHandlerProvider>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            RemoveHealthChecks(services);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Resets the database by dropping and recreating the schema.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var massTransitDescriptors = services
            .Where(d =>
                (d.ServiceType.FullName?.Contains("MassTransit") == true ||
                d.ImplementationType?.FullName?.Contains("MassTransit") == true ||
                d.ServiceType == typeof(IBus) ||
                d.ServiceType == typeof(IBusControl) ||
                d.ServiceType == typeof(IPublishEndpoint) ||
                d.ServiceType == typeof(ISendEndpointProvider)) &&
                d.ServiceType != typeof(Orders.Application.Interfaces.IApplicationEventPublisher))
            .ToList();

        foreach (var descriptor in massTransitDescriptors)
        {
            services.Remove(descriptor);
        }

        var hostedServiceDescriptors = services
            .Where(d =>
                d.ServiceType == typeof(IHostedService) &&
                d.ImplementationType?.FullName?.Contains("MassTransit") == true)
            .ToList();

        foreach (var descriptor in hostedServiceDescriptors)
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveHealthChecks(IServiceCollection services)
    {
        var healthCheckDescriptors = services
            .Where(d =>
                d.ServiceType.FullName?.Contains("HealthCheck") == true &&
                d.ImplementationType?.FullName?.Contains("Rabbit") == true)
            .ToList();

        foreach (var descriptor in healthCheckDescriptors)
        {
            services.Remove(descriptor);
        }
    }
}
