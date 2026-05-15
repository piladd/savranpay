namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class SupportClaimEntity
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
    public List<SupportClaimCommentEntity> Comments { get; } = [];
}
