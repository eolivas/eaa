namespace Orders.Infrastructure.Configuration;

public class OutboxRetentionOptions
{
    public int IntervalMinutes { get; set; } = 60;
    public int RetentionDays { get; set; } = 7;
    public int BatchSize { get; set; } = 500;
}
