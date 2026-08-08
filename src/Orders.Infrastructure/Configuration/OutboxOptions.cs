namespace Orders.Infrastructure.Configuration;

public class OutboxOptions
{
    public int BatchSize { get; set; } = 20;
    public int MaxRetries { get; set; } = 5;
    public int PollingIntervalSeconds { get; set; } = 5;
}
