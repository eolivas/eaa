using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Infrastructure.Configuration;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Registers MassTransit with conditional transport selection.
/// Uses RabbitMQ when configured, otherwise falls back to InMemory transport.
/// </summary>
public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rabbitMqSection = configuration.GetSection("RabbitMq");
        services.Configure<RabbitMqOptions>(rabbitMqSection);

        var options = rabbitMqSection.Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(busConfig =>
        {
            busConfig.AddConsumers(typeof(MessagingServiceCollectionExtensions).Assembly);

            if (!string.IsNullOrWhiteSpace(options.Host))
            {
                busConfig.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(options.Host, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    // Consumer retry: exponential backoff from 1s to 8s
                    cfg.UseMessageRetry(retryConfig =>
                    {
                        retryConfig.Exponential(
                            options.ConsumerRetryCount,
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(8),
                            TimeSpan.FromSeconds(1));
                    });

                    // MassTransit automatically creates error queues with _error suffix
                    // for messages that fail after all retries are exhausted.
                    // ConfigureEndpoints auto-creates exchanges, queues, and subscriptions.
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                var loggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("Orders.Infrastructure.Messaging");
                logger.LogWarning("RabbitMQ host not configured; using InMemory transport (degraded mode)");

                busConfig.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        // Configure startup retry behavior via MassTransitHostOptions.
        // WaitUntilStarted: blocks app startup until connected (or timeout).
        // StartTimeout: total allowed time for exponential backoff retries.
        // MassTransit internally retries connection with exponential backoff.
        // Timeout calculation: 5 attempts with 1s initial doubling = 1+2+4+8+16 = 31s + buffer.
        if (!string.IsNullOrWhiteSpace(options.Host))
        {
            services.AddOptions<MassTransitHostOptions>()
                .Configure(hostOptions =>
                {
                    hostOptions.WaitUntilStarted = true;
                    hostOptions.StartTimeout = CalculateStartupTimeout(
                        TimeSpan.FromSeconds(1), options.StartupRetryAttempts);
                });
        }

        // --- Outbox Processor (background service) ---
        services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));
        services.AddHostedService<OutboxProcessor>();

        // --- Outbox Retention (background service) ---
        services.Configure<OutboxRetentionOptions>(configuration.GetSection("Outbox:Retention"));
        services.AddHostedService<OutboxRetentionService>();

        return services;
    }

    /// <summary>
    /// Calculates a reasonable startup timeout based on exponential backoff parameters.
    /// Sum of delays: 1 + 2 + 4 + 8 + 16 = 31s for 5 attempts with 1s initial doubling.
    /// Adds buffer for connection attempt durations.
    /// </summary>
    private static TimeSpan CalculateStartupTimeout(TimeSpan initialInterval, int attempts)
    {
        var totalSeconds = 0.0;
        for (var i = 0; i < attempts; i++)
        {
            totalSeconds += initialInterval.TotalSeconds * Math.Pow(2, i);
        }

        // Add buffer for connection attempt time itself
        return TimeSpan.FromSeconds(totalSeconds + 30);
    }
}
