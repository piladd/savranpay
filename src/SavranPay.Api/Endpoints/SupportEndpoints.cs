using SavranPay.Api.Contracts;
using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Auth;
using SavranPay.Infrastructure.Demo;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SupportEndpoints
{
    internal static void MapSupportEndpoints(this IEndpointRouteBuilder app, bool usePostgres, bool requireAuthorization)
    {
        var supportClaimsEndpoint = app.MapGet("/api/v1/support/claims", async (
            IServiceProvider services,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                var store = services.GetRequiredService<DemoBankStore>();
                lock (store.SyncRoot)
                {
                    return Results.Ok(store.SupportClaims.OrderByDescending(claim => claim.UpdatedAt).Select(ApiEndpointHelpers.ToDemoSupportClaimView).ToArray());
                }
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var customerId = ApiEndpointHelpers.ResolveCustomerId(httpContext, requireAuthorization);
            var privileged = ApiEndpointHelpers.CanUseCustomerHeader(httpContext);
            var claims = await db.SupportClaims
                .AsNoTracking()
                .Include(claim => claim.Comments)
                .Where(claim => privileged || claim.CustomerId == customerId)
                .OrderByDescending(claim => claim.UpdatedAt)
                .ToListAsync(cancellationToken);

            return Results.Ok(claims.Select(ApiEndpointHelpers.ToSupportClaimView));
        });

        var upsertSupportClaimEndpoint = app.MapPut("/api/v1/support/transfers/{transferId:guid}/claim", async (
            Guid transferId,
            SupportClaimRequest request,
            IServiceProvider services,
            HttpContext httpContext,
            ITransferOrderRepository transfers,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                var demoTransfer = await transfers.GetByIdAsync(transferId, cancellationToken);
                if (demoTransfer is null)
                {
                    return Results.NotFound();
                }

                if (!ApiEndpointHelpers.CanAccessTransfer(httpContext, demoTransfer.CustomerId, requireAuthorization, SavranPayRole.SupportOperator, SavranPayRole.Admin))
                {
                    return Results.Forbid();
                }

                var store = services.GetRequiredService<DemoBankStore>();
                object demoClaimView;
                lock (store.SyncRoot)
                {
                    var demoNow = DateTimeOffset.UtcNow;
                    var demoClaim = store.SupportClaims.FirstOrDefault(item => item.TransferId == transferId);
                    var demoClaimCreated = demoClaim is null;
                    if (demoClaim is null)
                    {
                        demoClaim = new DemoSupportClaim
                        {
                            Id = Guid.NewGuid(),
                            TransferId = transferId,
                            CustomerId = demoTransfer.CustomerId,
                            CreatedByUserId = ApiEndpointHelpers.TryReadUserIdFromClaims(httpContext),
                            CreatedAt = demoNow
                        };
                        store.SupportClaims.Add(demoClaim);
                    }

                    demoClaim.Category = ApiEndpointHelpers.NormalizeSupportValue(request.Category, "General");
                    demoClaim.Status = ApiEndpointHelpers.NormalizeSupportValue(request.Status, "Open");
                    demoClaim.AssignedTo = ApiEndpointHelpers.NormalizeSupportValue(request.AssignedTo, "Support");
                    demoClaim.Comment = request.Comment ?? string.Empty;
                    demoClaim.ContactComment = request.ContactComment ?? string.Empty;
                    demoClaim.UpdatedAt = demoNow;
                    demoClaim.Comments.Add(new DemoSupportClaimComment(
                        Guid.NewGuid(),
                        demoClaim.Id,
                        ApiEndpointHelpers.TryReadUserIdFromClaims(httpContext),
                        ApiEndpointHelpers.CurrentRole(httpContext),
                        $"{demoClaim.Status}; assigned to {demoClaim.AssignedTo}. {demoClaim.Comment}",
                        demoNow));
                    store.AuditEvents.Add(new DemoAuditEvent(
                        Guid.NewGuid(),
                        demoClaim.Id,
                        demoClaimCreated ? "SupportClaimCreated" : "SupportClaimUpdated",
                        $"Support claim for transfer {transferId} is {demoClaim.Status}, assigned to {demoClaim.AssignedTo}.",
                        demoNow));
                    demoClaimView = ApiEndpointHelpers.ToDemoSupportClaimView(demoClaim);
                }

                return Results.Ok(demoClaimView);
            }

            var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
            if (transfer is null)
            {
                return Results.NotFound();
            }

            if (!ApiEndpointHelpers.CanAccessTransfer(httpContext, transfer.CustomerId, requireAuthorization, SavranPayRole.SupportOperator, SavranPayRole.Admin))
            {
                return Results.Forbid();
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var now = DateTimeOffset.UtcNow;
            var claim = await db.SupportClaims
                .Include(item => item.Comments)
                .FirstOrDefaultAsync(item => item.TransferId == transferId, cancellationToken);
            var created = claim is null;

            if (claim is null)
            {
                claim = new SupportClaimEntity
                {
                    Id = Guid.NewGuid(),
                    TransferId = transferId,
                    CustomerId = transfer.CustomerId,
                    CreatedByUserId = ApiEndpointHelpers.TryReadUserIdFromClaims(httpContext),
                    CreatedAt = now
                };
                db.SupportClaims.Add(claim);
            }

            claim.Category = ApiEndpointHelpers.NormalizeSupportValue(request.Category, "General");
            claim.Status = ApiEndpointHelpers.NormalizeSupportValue(request.Status, "Open");
            claim.AssignedTo = ApiEndpointHelpers.NormalizeSupportValue(request.AssignedTo, "Support");
            claim.Comment = request.Comment ?? string.Empty;
            claim.ContactComment = request.ContactComment ?? string.Empty;
            claim.UpdatedAt = now;

            db.SupportClaimComments.Add(new SupportClaimCommentEntity
            {
                Id = Guid.NewGuid(),
                SupportClaimId = claim.Id,
                AuthorUserId = ApiEndpointHelpers.TryReadUserIdFromClaims(httpContext),
                AuthorRole = ApiEndpointHelpers.CurrentRole(httpContext),
                Message = $"{claim.Status}; assigned to {claim.AssignedTo}. {claim.Comment}",
                CreatedAt = now
            });
            ApiEndpointHelpers.AddAuditEvent(db, claim.Id, created ? "SupportClaimCreated" : "SupportClaimUpdated", $"Support claim for transfer {transferId} is {claim.Status}, assigned to {claim.AssignedTo}.");
            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(ApiEndpointHelpers.ToSupportClaimView(claim));
        });

        var addSupportClaimCommentEndpoint = app.MapPost("/api/v1/support/claims/{claimId:guid}/comments", async (
            Guid claimId,
            SupportClaimCommentRequest request,
            IServiceProvider services,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                var store = services.GetRequiredService<DemoBankStore>();
                object demoClaimView;
                lock (store.SyncRoot)
                {
                    var demoClaim = store.SupportClaims.FirstOrDefault(item => item.Id == claimId);
                    if (demoClaim is null)
                    {
                        return Results.NotFound();
                    }

                    if (!ApiEndpointHelpers.CanUseCustomerHeader(httpContext) && ApiEndpointHelpers.TryReadCustomerIdFromClaims(httpContext) != demoClaim.CustomerId)
                    {
                        return Results.Forbid();
                    }

                    var demoNow = DateTimeOffset.UtcNow;
                    demoClaim.Comments.Add(new DemoSupportClaimComment(
                        Guid.NewGuid(),
                        demoClaim.Id,
                        ApiEndpointHelpers.TryReadUserIdFromClaims(httpContext),
                        ApiEndpointHelpers.CurrentRole(httpContext),
                        request.Message,
                        demoNow));
                    demoClaim.UpdatedAt = demoNow;
                    store.AuditEvents.Add(new DemoAuditEvent(Guid.NewGuid(), demoClaim.Id, "SupportClaimCommentAdded", $"Comment added to support claim {demoClaim.Id}.", demoNow));
                    demoClaimView = ApiEndpointHelpers.ToDemoSupportClaimView(demoClaim);
                }

                return Results.Ok(demoClaimView);
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var claim = await db.SupportClaims.Include(item => item.Comments).SingleOrDefaultAsync(item => item.Id == claimId, cancellationToken);
            if (claim is null)
            {
                return Results.NotFound();
            }

            if (!ApiEndpointHelpers.CanUseCustomerHeader(httpContext) && ApiEndpointHelpers.TryReadCustomerIdFromClaims(httpContext) != claim.CustomerId)
            {
                return Results.Forbid();
            }

            var now = DateTimeOffset.UtcNow;
            db.SupportClaimComments.Add(new SupportClaimCommentEntity
            {
                Id = Guid.NewGuid(),
                SupportClaimId = claim.Id,
                AuthorUserId = ApiEndpointHelpers.TryReadUserIdFromClaims(httpContext),
                AuthorRole = ApiEndpointHelpers.CurrentRole(httpContext),
                Message = request.Message,
                CreatedAt = now
            });
            claim.UpdatedAt = now;
            ApiEndpointHelpers.AddAuditEvent(db, claim.Id, "SupportClaimCommentAdded", $"Comment added to support claim {claim.Id}.");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ApiEndpointHelpers.ToSupportClaimView(claim));
        });

        if (requireAuthorization)
        {
            supportClaimsEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.AmlOfficer, SavranPayRole.FraudOfficer, SavranPayRole.Admin, SavranPayRole.Auditor));
            upsertSupportClaimEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.Admin));
            addSupportClaimCommentEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.AmlOfficer, SavranPayRole.FraudOfficer, SavranPayRole.Admin));
        }
    }
}
