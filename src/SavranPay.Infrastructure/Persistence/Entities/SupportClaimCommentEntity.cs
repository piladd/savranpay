namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class SupportClaimCommentEntity
{
    public Guid Id { get; set; }
    public Guid SupportClaimId { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string AuthorRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public SupportClaimEntity Claim { get; set; } = null!;
}
