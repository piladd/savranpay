namespace SavranPay.SharedKernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
