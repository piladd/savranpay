using SavranPay.Api.Contracts;
using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Auth;
using SavranPay.Infrastructure.Demo;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

internal static class AdminEndpoints
{
    internal static void MapAdminEndpoints(this IEndpointRouteBuilder app, bool usePostgres, bool requireAuthorization)
    {
        var adminUsersEndpoint = app.MapGet("/api/v1/admin/users", async (IServiceProvider services, CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.Ok(Array.Empty<object>());
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var users = await db.Users
                .AsNoTracking()
                .Include(user => user.Roles)
                .ThenInclude(userRole => userRole.Role)
                .OrderBy(user => user.Login)
                .ToListAsync(cancellationToken);

            return Results.Ok(users.Select(ApiEndpointHelpers.ToAdminUserView));
        });

        var createAdminUserEndpoint = app.MapPost("/api/v1/admin/users", async (
            CreateUserRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.BadRequest("PostgreSQL mode is required for user management.");
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            if (await db.Users.AnyAsync(user => user.Login == request.Login, cancellationToken))
            {
                return Results.Conflict("User login already exists.");
            }

            var roles = await db.Roles.Where(role => request.Roles.Contains(role.Name)).ToListAsync(cancellationToken);
            if (roles.Count != request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                return Results.BadRequest("One or more roles do not exist.");
            }

            var user = new UserEntity
            {
                Id = Guid.NewGuid(),
                Login = request.Login,
                PasswordHash = PasswordHasher.Hash(request.Password),
                FullName = request.FullName,
                Email = request.Email,
                Phone = request.Phone,
                CustomerId = request.CustomerId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            foreach (var role in roles)
            {
                user.Roles.Add(new UserRoleEntity { UserId = user.Id, RoleId = role.Id });
            }

            db.Users.Add(user);
            ApiEndpointHelpers.AddAuditEvent(db, user.Id, "AdminUserCreated", $"Admin created user {user.Login}.");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/admin/users/{user.Id}", ApiEndpointHelpers.ToAdminUserView(user));
        });

        var updateAdminUserEndpoint = app.MapPatch("/api/v1/admin/users/{userId:guid}", async (
            Guid userId,
            UpdateUserRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.BadRequest("PostgreSQL mode is required for user management.");
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user is null)
            {
                return Results.NotFound();
            }

            user.FullName = request.FullName ?? user.FullName;
            user.Email = request.Email ?? user.Email;
            user.Phone = request.Phone ?? user.Phone;
            user.CustomerId = request.CustomerId ?? user.CustomerId;
            user.IsActive = request.IsActive ?? user.IsActive;

            ApiEndpointHelpers.AddAuditEvent(db, user.Id, "AdminUserUpdated", $"Admin updated user {user.Login}.");
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        var addRoleEndpoint = app.MapPost("/api/v1/admin/users/{userId:guid}/roles", async (
            Guid userId,
            RoleRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.BadRequest("PostgreSQL mode is required for role management.");
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var userExists = await db.Users.AnyAsync(user => user.Id == userId, cancellationToken);
            var role = await db.Roles.SingleOrDefaultAsync(item => item.Name == request.Role, cancellationToken);
            if (!userExists || role is null)
            {
                return Results.NotFound();
            }

            var exists = await db.UserRoles.AnyAsync(item => item.UserId == userId && item.RoleId == role.Id, cancellationToken);
            if (!exists)
            {
                db.UserRoles.Add(new UserRoleEntity { UserId = userId, RoleId = role.Id });
                ApiEndpointHelpers.AddAuditEvent(db, userId, "AdminRoleAdded", $"Role {request.Role} was added.");
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.NoContent();
        });

        var removeRoleEndpoint = app.MapDelete("/api/v1/admin/users/{userId:guid}/roles/{role}", async (
            Guid userId,
            string role,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.BadRequest("PostgreSQL mode is required for role management.");
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var userRole = await db.UserRoles
                .Include(item => item.Role)
                .SingleOrDefaultAsync(item => item.UserId == userId && item.Role.Name == role, cancellationToken);

            if (userRole is null)
            {
                return Results.NotFound();
            }

            db.UserRoles.Remove(userRole);
            ApiEndpointHelpers.AddAuditEvent(db, userId, "AdminRoleRemoved", $"Role {role} was removed.");
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        var blockUserEndpoint = app.MapPost("/api/v1/admin/users/{userId:guid}/block", async (
            Guid userId,
            IServiceProvider services,
            CancellationToken cancellationToken) => await ApiEndpointHelpers.SetUserActiveAsync(userId, false, services, usePostgres, cancellationToken));

        var unblockUserEndpoint = app.MapPost("/api/v1/admin/users/{userId:guid}/unblock", async (
            Guid userId,
            IServiceProvider services,
            CancellationToken cancellationToken) => await ApiEndpointHelpers.SetUserActiveAsync(userId, true, services, usePostgres, cancellationToken));

        var resetUserPasswordEndpoint = app.MapPost("/api/v1/admin/users/{userId:guid}/reset-password", async (
            Guid userId,
            AdminSystemActionRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.BadRequest("PostgreSQL mode is required for user management.");
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var user = await db.Users.Include(item => item.RefreshTokens).Include(item => item.Sessions).SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user is null)
            {
                return Results.NotFound();
            }

            var temporaryPassword = ApiEndpointHelpers.GenerateTemporaryPassword();
            user.PasswordHash = PasswordHasher.Hash(temporaryPassword);
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

            ApiEndpointHelpers.AddAuditEvent(db, user.Id, "AdminPasswordReset", $"Password reset requested. Reason: {request.Reason ?? "not specified"}.");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Accepted($"/api/v1/admin/users/{user.Id}", new
            {
                user.Id,
                Delivery = "Temporary password generated and must be delivered through an approved secure channel."
            });
        });

        var updateUserLimitEndpoint = app.MapPost("/api/v1/admin/users/{userId:guid}/limits", async (
            Guid userId,
            AdminSystemActionRequest request,
            IServiceProvider services,
            CancellationToken cancellationToken) =>
        {
            if (!usePostgres)
            {
                return Results.BadRequest("PostgreSQL mode is required for user management.");
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            var exists = await db.Users.AnyAsync(item => item.Id == userId, cancellationToken);
            if (!exists)
            {
                return Results.NotFound();
            }

            ApiEndpointHelpers.AddAuditEvent(db, userId, "AdminLimitChanged", $"Limit set to {request.LimitMinorUnits ?? 0} minor units. Reason: {request.Reason ?? "not specified"}.");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Accepted($"/api/v1/admin/users/{userId}", new { userId, request.LimitMinorUnits });
        });

        var retryTransferEndpoint = app.MapPost("/api/v1/admin/transfers/{transferId:guid}/retry", async (
            Guid transferId,
            AdminSystemActionRequest request,
            IServiceProvider services,
            ITransferOrderRepository transfers,
            CancellationToken cancellationToken) =>
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
                ApiEndpointHelpers.AddAuditEvent(db, transferId, "AdminTransferRetryRequested", $"Retry requested. Reason: {request.Reason ?? "not specified"}.");
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Accepted($"/api/v1/transfers/{transferId}", new { transferId, action = "RetryRequested" });
        });

        var transferTechnicalDetailsEndpoint = app.MapGet("/api/v1/admin/transfers/{transferId:guid}/technical-details", async (
            Guid transferId,
            IServiceProvider services,
            ITransferOrderRepository transfers,
            CancellationToken cancellationToken) =>
        {
            var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
            if (transfer is null)
            {
                return Results.NotFound();
            }

            if (!usePostgres)
            {
                var store = services.GetRequiredService<DemoBankStore>();
                lock (store.SyncRoot)
                {
                    return Results.Ok(new
                    {
                        transferId,
                        riskChecks = store.RiskChecks.Where(item => item.TransferId == transferId).OrderByDescending(item => item.CreatedAt).ToArray(),
                        auditEvents = store.AuditEvents.Where(item => item.OperationId == transferId).OrderByDescending(item => item.CreatedAt).ToArray(),
                        ledger = store.LedgerEntries.Where(item => item.TransferId == transferId).OrderByDescending(item => item.CreatedAt).ToArray(),
                        supportClaims = store.SupportClaims.Where(item => item.TransferId == transferId).OrderByDescending(item => item.UpdatedAt).Select(ApiEndpointHelpers.ToDemoSupportClaimView).ToArray()
                    });
                }
            }

            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
            return Results.Ok(new
            {
                transferId,
                riskChecks = await db.RiskChecks.AsNoTracking().Where(item => item.TransferId == transferId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken),
                auditEvents = await db.AuditEvents.AsNoTracking().Where(item => item.OperationId == transferId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken),
                ledger = await db.Ledger.AsNoTracking().Where(item => item.TransferId == transferId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken),
                supportClaims = await db.SupportClaims.AsNoTracking().Where(item => item.TransferId == transferId).OrderByDescending(item => item.UpdatedAt).ToListAsync(cancellationToken)
            });
        });

        if (requireAuthorization)
        {
            adminUsersEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            createAdminUserEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            updateAdminUserEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            addRoleEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            removeRoleEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            blockUserEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            unblockUserEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            resetUserPasswordEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            updateUserLimitEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            retryTransferEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
            transferTechnicalDetailsEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Admin));
        }
    }
}
