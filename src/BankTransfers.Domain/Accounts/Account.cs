using BankTransfers.Domain.ValueObjects;

namespace BankTransfers.Domain.Accounts;

public sealed class Account
{
    public Account(Guid id, Guid customerId, string number, Money availableBalance, AccountStatus status)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Account id is required.", nameof(id)) : id;
        CustomerId = customerId == Guid.Empty ? throw new ArgumentException("Customer id is required.", nameof(customerId)) : customerId;
        Number = string.IsNullOrWhiteSpace(number) ? throw new ArgumentException("Account number is required.", nameof(number)) : number;
        AvailableBalance = availableBalance;
        ReservedBalance = Money.Zero(availableBalance.Currency);
        Status = status;
    }

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public string Number { get; }
    public Money AvailableBalance { get; private set; }
    public Money ReservedBalance { get; private set; }
    public AccountStatus Status { get; private set; }

    public bool CanDebit(Money amount)
    {
        return Status == AccountStatus.Active
            && AvailableBalance.Currency == amount.Currency
            && AvailableBalance.MinorUnits >= amount.MinorUnits;
    }

    public void Debit(Money amount)
    {
        if (!CanDebit(amount))
        {
            throw new InvalidOperationException("Account cannot be debited.");
        }

        AvailableBalance = new Money(AvailableBalance.MinorUnits - amount.MinorUnits, AvailableBalance.Currency);
    }

    public void Credit(Money amount)
    {
        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Account cannot be credited.");
        }

        if (AvailableBalance.Currency != amount.Currency)
        {
            throw new InvalidOperationException("Currency mismatch.");
        }

        AvailableBalance = new Money(AvailableBalance.MinorUnits + amount.MinorUnits, AvailableBalance.Currency);
    }
}
