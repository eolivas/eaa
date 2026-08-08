namespace Orders.Infrastructure.Configuration;

public class RabbitMqOptions
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public int ConsumerRetryCount { get; set; } = 3;
    public int StartupRetryAttempts { get; set; } = 5;
}
