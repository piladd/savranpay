using SavranPay.Api.Contracts;
using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Auth;
using SavranPay.Infrastructure.Demo;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using SavranPay.SharedKernel;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;

internal static class ApiEndpointHelpers
{
    internal static Guid? TryReadCustomerId(HttpRequest request)
    {
        var value = request.Headers["X-Customer-Id"].ToString();
        return Guid.TryParse(value, out var customerId) ? customerId : null;
    }

    internal static Guid? TryReadCustomerIdFromClaims(HttpContext context)
    {
        var value = context.User.FindFirstValue("customer_id");
        return Guid.TryParse(value, out var customerId) ? customerId : null;
    }

    internal static Guid? TryReadUserIdFromClaims(HttpContext context)
    {
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    internal static Guid? ResolveCustomerId(HttpContext context, bool requireAuthorization)
    {
        var claimCustomerId = TryReadCustomerIdFromClaims(context);

        if (CanUseCustomerHeader(context))
        {
            return TryReadCustomerId(context.Request) ?? claimCustomerId;
        }

        if (requireAuthorization)
        {
            return claimCustomerId;
        }

        return claimCustomerId ?? TryReadCustomerId(context.Request);
    }

    internal static bool CanAccessTransfer(HttpContext context, Guid transferCustomerId, bool requireAuthorization, params string[] privilegedRoles)
    {
        if (!requireAuthorization)
        {
            return true;
        }

        if (privilegedRoles.Any(context.User.IsInRole))
        {
            return true;
        }

        return TryReadCustomerIdFromClaims(context) == transferCustomerId;
    }

    internal static bool CanUseCustomerHeader(HttpContext context)
    {
        return context.User.IsInRole(SavranPayRole.SupportOperator)
            || context.User.IsInRole(SavranPayRole.Admin)
            || context.User.IsInRole(SavranPayRole.Auditor)
            || context.User.IsInRole(SavranPayRole.AmlOfficer)
            || context.User.IsInRole(SavranPayRole.FraudOfficer);
    }

    internal static object ToAdminUserView(UserEntity user) => new
    {
        user.Id,
        user.Login,
        user.FullName,
        user.Email,
        user.Phone,
        user.CustomerId,
        user.IsActive,
        user.CreatedAt,
        Roles = user.Roles.Select(item => item.Role.Name).Order().ToArray()
    };

    internal static object ToSupportClaimView(SupportClaimEntity claim) => new
    {
        claim.Id,
        claim.TransferId,
        claim.CustomerId,
        claim.CreatedByUserId,
        claim.Category,
        claim.Status,
        claim.AssignedTo,
        claim.Comment,
        claim.ContactComment,
        claim.CreatedAt,
        claim.UpdatedAt,
        Comments = claim.Comments
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id,
                item.SupportClaimId,
                item.AuthorUserId,
                item.AuthorRole,
                item.Message,
                item.CreatedAt
            })
            .ToArray()
    };

    internal static object ToDemoSupportClaimView(DemoSupportClaim claim) => new
    {
        claim.Id,
        claim.TransferId,
        claim.CustomerId,
        claim.CreatedByUserId,
        claim.Category,
        claim.Status,
        claim.AssignedTo,
        claim.Comment,
        claim.ContactComment,
        claim.CreatedAt,
        claim.UpdatedAt,
        Comments = claim.Comments
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id,
                item.SupportClaimId,
                item.AuthorUserId,
                item.AuthorRole,
                item.Message,
                item.CreatedAt
            })
            .ToArray()
    };

    internal static string NormalizeSupportValue(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    internal static string CurrentRole(HttpContext context) =>
        context.User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Role)?.Value ?? "Anonymous";

    internal static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        Span<char> password = stackalloc char[20];
        Span<byte> bytes = stackalloc byte[20];
        RandomNumberGenerator.Fill(bytes);

        for (var index = 0; index < password.Length; index++)
        {
            password[index] = alphabet[bytes[index] % alphabet.Length];
        }

        return new string(password);
    }

    internal static void AddAuditEvent(SavranPayDbContext db, Guid operationId, string eventType, string message)
    {
        var now = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            EventType = eventType,
            Message = message,
            CreatedAt = now
        });
        db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = $"Audit.{eventType}",
            Payload = JsonSerializer.Serialize(new { operationId, eventType, message }),
            CreatedAt = now
        });
    }

    internal static async Task<IResult> SetUserActiveAsync(
        Guid userId,
        bool isActive,
        IServiceProvider services,
        bool usePostgres,
        CancellationToken cancellationToken)
    {
        if (!usePostgres)
        {
            return Results.BadRequest("PostgreSQL mode is required for user management.");
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var user = await db.Users
            .Include(item => item.RefreshTokens)
            .Include(item => item.Sessions)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
        {
            return Results.NotFound();
        }

        user.IsActive = isActive;
        if (!isActive)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var token in user.RefreshTokens.Where(item => item.RevokedAt is null))
            {
                token.RevokedAt = now;
            }

            foreach (var session in user.Sessions.Where(item => item.RevokedAt is null))
            {
                session.RevokedAt = now;
                session.LastSeenAt = now;
            }
        }

        AddAuditEvent(db, user.Id, isActive ? "AdminUserUnblocked" : "AdminUserBlocked", $"User {user.Login} active state set to {isActive}.");
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    internal static async Task<IResult> RecordManualRiskDecisionAsync(
        Guid transferId,
        string checkType,
        RiskDecisionRequest request,
        IServiceProvider services,
        bool usePostgres,
        ITransferOrderRepository transfers,
        IAuditService audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
        if (transfer is null)
        {
            return Results.NotFound();
        }

        if (usePostgres)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            db.RiskChecks.Add(new RiskCheckEntity
            {
                Id = Guid.NewGuid(),
                TransferId = transferId,
                CheckType = checkType,
                Decision = request.Decision,
                Details = request.Details,
                DeviceFingerprint = $"manual-{transfer.CustomerId:N}"[..39],
                IpAddress = "manual-review",
                RiskFactors = JsonSerializer.Serialize(request.RiskFactors ?? []),
                BlockReason = request.BlockReason ?? string.Empty,
                DocumentsRequested = request.DocumentsRequested ?? false,
                StepUpRequired = request.StepUpRequired ?? request.Decision.Equals("ManualReview", StringComparison.OrdinalIgnoreCase),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (request.Decision.Equals("Block", StringComparison.OrdinalIgnoreCase) ||
            request.Decision.Equals("Blocked", StringComparison.OrdinalIgnoreCase) ||
            request.Decision.Equals("CriticalRisk", StringComparison.OrdinalIgnoreCase))
        {
            transfer.Fail(clock.UtcNow);
            await transfers.AddAsync(transfer, cancellationToken);
        }

        await audit.WriteAsync(transferId, $"{checkType}ManualDecision", $"{request.Decision}: {request.Details}", cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Results.Accepted($"/api/v1/transfers/{transferId}", new { transferId, request.Decision });
    }
}
