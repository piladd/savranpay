using BankTransfers.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BankTransfers.Infrastructure.Notifications;

public sealed class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;

    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string channel, string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Notification adapter sent {Channel} notification to {Recipient}. Subject={Subject}; Body={Body}",
            channel,
            recipient,
            subject,
            body);

        return Task.CompletedTask;
    }
}
