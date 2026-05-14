using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankTransfers.Workers;

public sealed class OutboxWorker : BackgroundService
{
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(ILogger<OutboxWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Outbox worker heartbeat at {Timestamp:o}.", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
