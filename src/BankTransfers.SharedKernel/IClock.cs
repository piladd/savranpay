namespace BankTransfers.SharedKernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
