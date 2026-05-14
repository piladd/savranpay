using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SavranPay.Workers;

public sealed class OutboxWorker : BackgroundService
{
    private readonly ILogger<OutboxWorker> _logger;
    private readonly IServiceProvider _services;

    public OutboxWorker(ILogger<OutboxWorker> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchOutboxAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task DispatchOutboxAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<SavranPayDbContext>();
        if (db is null)
        {
            _logger.LogInformation("Outbox worker heartbeat at {Timestamp:o}. PostgreSQL is not configured.", DateTimeOffset.UtcNow);
            return;
        }

        var messages = await db.OutboxMessages
            .Where(message => message.DispatchedAt == null)
            .OrderBy(message => message.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var notifications = scope.ServiceProvider.GetRequiredService<INotificationSender>();

        foreach (var message in messages)
        {
            try
            {
                if (message.Type.StartsWith("Notification.", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = JsonSerializer.Deserialize<NotificationPayload>(message.Payload);
                    await notifications.SendAsync(
                        payload?.Channel ?? "Push",
                        payload?.Recipient ?? "customer",
                        "SavranPay transfer update",
                        message.Payload,
                        cancellationToken);
                }

                _logger.LogInformation(
                    "Dispatching outbox message {MessageId} of type {MessageType}: {Payload}",
                    message.Id,
                    message.Type,
                    message.Payload);
                message.DispatchedAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception exception)
            {
                message.Error = exception.Message;
                _logger.LogError(exception, "Outbox message {MessageId} dispatch failed.", message.Id);
            }
        }

        if (messages.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record NotificationPayload(Guid TransferId, string Channel, string Status, string? Recipient);
}
