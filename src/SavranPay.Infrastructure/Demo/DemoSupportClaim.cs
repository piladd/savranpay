namespace SavranPay.Infrastructure.Demo;

public sealed class DemoSupportClaim
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string ContactComment { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<DemoSupportClaimComment> Comments { get; } = [];
}

public sealed record DemoSupportClaimComment(
    Guid Id,
    Guid SupportClaimId,
    Guid? AuthorUserId,
    string AuthorRole,
    string Message,
    DateTimeOffset CreatedAt);
