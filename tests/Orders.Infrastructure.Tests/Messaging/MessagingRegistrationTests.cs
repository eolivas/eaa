using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Infrastructure.Messaging;
using Xunit;

namespace Orders.Infrastructure.Tests.Messaging;

public class MessagingRegistrationTests
{
    [Fact]
    public void AddMessaging_WhenRabbitMqHostPresent_RegistersRabbitMqTransport()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMessaging(configuration);

        // Assert — MassTransit is registered and the bus can be resolved
        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IBus>();
        Assert.NotNull(bus);

        // Verify that the bus address uses the rabbitmq scheme
        Assert.StartsWith("rabbitmq://", bus.Address.ToString());
    }

    [Fact]
    public void AddMessaging_WhenRabbitMqHostAbsent_UsesInMemoryTransportAndLogsWarning()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // No RabbitMq:Host configured
            })
            .Build();

        var services = new ServiceCollection();
        var logMessages = new List<string>();
        services.AddLogging(builder =>
        {
            builder.AddProvider(new InMemoryLoggerProvider(logMessages));
        });

        // Act
        services.AddMessaging(configuration);

        // Assert — MassTransit is registered and the bus can be resolved
        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IBus>();
        Assert.NotNull(bus);

        // Verify that the bus address uses the loopback (in-memory) scheme
        Assert.StartsWith("loopback://", bus.Address.ToString());

        // Verify warning was logged
        Assert.Contains(logMessages,
            msg => msg.Contains("RabbitMQ host not configured; using InMemory transport (degraded mode)"));
    }

    [Fact]
    public void AddMessaging_WhenRabbitMqHostEmpty_UsesInMemoryTransport()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMessaging(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var bus = provider.GetService<IBus>();
        Assert.NotNull(bus);
        Assert.StartsWith("loopback://", bus.Address.ToString());
    }

    /// <summary>
    /// Simple in-memory logger provider for capturing log messages in tests.
    /// </summary>
    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages;

        public InMemoryLoggerProvider(List<string> messages) => _messages = messages;

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_messages);

        public void Dispose() { }

        private sealed class InMemoryLogger : ILogger
        {
            private readonly List<string> _messages;

            public InMemoryLogger(List<string> messages) => _messages = messages;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
