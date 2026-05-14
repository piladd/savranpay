namespace SavranPay.Application.Abstractions;

public interface INotificationSender
{
    Task SendAsync(string channel, string recipient, string subject, string body, CancellationToken cancellationToken);
}
