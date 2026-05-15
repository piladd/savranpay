using SavranPay.Api.Contracts;
using SavranPay.Application.Abstractions;
using SavranPay.Application.Transfers.ConfirmTransfer;
using SavranPay.Application.Transfers.CreateTransfer;
using SavranPay.Infrastructure.Audit;
using SavranPay.Infrastructure.Auth;
using SavranPay.Infrastructure.Compliance;
using SavranPay.Infrastructure.Crypto;
using SavranPay.Infrastructure.Demo;
using SavranPay.Infrastructure.Limits;
using SavranPay.Infrastructure.Notifications;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using SavranPay.Infrastructure.Risk;
using SavranPay.Infrastructure.Transfers;
using SavranPay.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Result", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Cors.Infrastructure.CorsService", LogLevel.Warning);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration.GetValue<string>("DataProtection:KeysPath") ?? "/var/lib/savranpay/dataprotection-keys"))
    .SetApplicationName("SavranPay");
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 5001;
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var frontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray() ?? [];

if (frontendOrigins.Length == 0)
{
    frontendOrigins =
    [
        "https://localhost:5173",
        "http://localhost:5173",
        "https://127.0.0.1:5173",
        "http://127.0.0.1:5173",
        "https://savranpay.netlify.app"
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("SavranPayFrontend", policy =>
    {
        policy.WithOrigins(frontendOrigins)
            .SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                (frontendOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) ||
                 uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)))
            .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", "X-Request-Id")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");
    });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ProductionCryptoOptions>(builder.Configuration.GetSection("ProductionCrypto"));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthentication("SavranPayJwt")
    .AddScheme<AuthenticationSchemeOptions, SavranPayAuthenticationHandler>("SavranPayJwt", options => { });
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IClock, SavranPay.SharedKernel.SystemClock>();
builder.Services.AddSingleton<DemoBankStore>();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var usePostgres = !string.IsNullOrWhiteSpace(postgresConnectionString);
var requireAuthorization = builder.Configuration.GetValue<bool>("Jwt:RequireAuthorization");

if (usePostgres)
{
    builder.Services.AddDbContext<SavranPayDbContext>(options => options.UseNpgsql(postgresConnectionString));
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<IAccountRepository, EfAccountRepository>();
    builder.Services.AddScoped<ITransferOrderRepository, EfTransferOrderRepository>();
    builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
    builder.Services.AddScoped<ITransferExecutionService, EfTransferExecutionService>();
    builder.Services.AddScoped<IAuditService, EfAuditService>();
    builder.Services.AddScoped<IAmlService, EfAmlService>();
    builder.Services.AddScoped<IFraudService, EfFraudService>();
}
else
{
    builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
    builder.Services.AddSingleton<ITransferOrderRepository, InMemoryTransferOrderRepository>();
    builder.Services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
    builder.Services.AddSingleton<ITransferExecutionService, DemoTransferExecutionService>();
    builder.Services.AddSingleton<IAuditService, ConsoleAuditService>();
    builder.Services.AddSingleton<IAmlService, AllowAllAmlService>();
    builder.Services.AddSingleton<IFraudService, SimpleFraudService>();
}

builder.Services.AddSingleton<ILimitService, SimpleLimitService>();
builder.Services.AddSingleton<ICryptoService, DemoCryptoService>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddScoped<CreateTransferHandler>();
builder.Services.AddScoped<ConfirmTransferHandler>();

var app = builder.Build();
long totalRequests = 0;
long serverErrors = 0;

if (usePostgres)
{
    using var scope = app.Services.CreateScope();
    await SavranPayDbInitializer.InitializeAsync(
        scope.ServiceProvider.GetRequiredService<SavranPayDbContext>(),
        CancellationToken.None);
}

var httpsRedirectionDisabled = builder.Configuration.GetValue<bool>("DISABLE_HTTPS_REDIRECTION");

if (!app.Environment.IsDevelopment() && !httpsRedirectionDisabled)
{
    app.UseHsts();
}

if (!httpsRedirectionDisabled)
{
    app.UseHttpsRedirection();
}

app.UseCors("SavranPayFrontend");
app.Use(async (context, next) =>
{
    Interlocked.Increment(ref totalRequests);
    await next();
    if (context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
    {
        Interlocked.Increment(ref serverErrors);
    }
});
app.UseAuthentication();
app.UseMiddleware<JwtAuthenticationMiddleware>();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var connectSources = string.Join(" ", frontendOrigins);
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://unpkg.com 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        $"connect-src 'self' {connectSources}; " +
        "font-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (!usePostgres)
    {
        return Results.Ok(new { status = "ready", storage = "in-memory" });
    }

    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
    return await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready", storage = "postgresql" })
        : Results.Problem("PostgreSQL is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();
app.MapGet("/metrics", () => Results.Text(
    $"""
    # HELP savranpay_http_requests_total Total HTTP requests handled by SavranPay.
    # TYPE savranpay_http_requests_total counter
    savranpay_http_requests_total {Interlocked.Read(ref totalRequests)}
    # HELP savranpay_http_server_errors_total Total HTTP 5xx responses handled by SavranPay.
    # TYPE savranpay_http_server_errors_total counter
    savranpay_http_server_errors_total {Interlocked.Read(ref serverErrors)}
    """,
    "text/plain")).AllowAnonymous();

app.MapPost("/api/v1/auth/login", async (
    LoginRequest request,
    HttpContext httpContext,
    IServiceProvider services,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        var auth = services.GetRequiredService<AuthService>();
        var result = await auth.LoginAsync(
            request.Login,
            request.Password,
            GetIpAddress(httpContext),
            GetUserAgent(httpContext),
            cancellationToken);
        return result is null ? Results.Unauthorized() : Results.Ok(result);
    }

    var tokens = services.GetRequiredService<JwtTokenService>();
    var accessToken = tokens.CreateAccessToken(
        InMemoryAccountRepository.DemoCustomerId,
        InMemoryAccountRepository.DemoCustomerId,
        request.Login,
        [SavranPayRole.Customer]);

    return Results.Ok(new
    {
        accessToken,
        refreshToken = JwtTokenService.CreateRefreshToken(),
        accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes),
        refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays),
        user = new
        {
            Id = InMemoryAccountRepository.DemoCustomerId,
            Login = request.Login,
            FullName = "Demo Customer",
            CustomerId = InMemoryAccountRepository.DemoCustomerId,
            Roles = new[] { SavranPayRole.Customer }
        }
    });
}).AllowAnonymous();

app.MapPost("/api/v1/auth/refresh", async (
    RefreshTokenRequest request,
    HttpContext httpContext,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (!usePostgres)
    {
        return Results.Unauthorized();
    }

    var auth = services.GetRequiredService<AuthService>();
    var result = await auth.RefreshAsync(
        request.RefreshToken,
        GetIpAddress(httpContext),
        GetUserAgent(httpContext),
        cancellationToken);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
}).AllowAnonymous();

var logoutEndpoint = app.MapPost("/api/v1/auth/logout", async (
    LogoutRequest request,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (!usePostgres)
    {
        return Results.NoContent();
    }

    var auth = services.GetRequiredService<AuthService>();
    await auth.LogoutAsync(request.RefreshToken, cancellationToken);
    return Results.NoContent();
});

var meEndpoint = app.MapGet("/api/v1/auth/me", async (
    HttpContext httpContext,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    var userId = TryReadUserIdFromClaims(httpContext);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    if (!usePostgres)
    {
        return Results.Ok(new
        {
            Id = userId,
            Login = httpContext.User.Identity?.Name ?? "demo",
            CustomerId = TryReadCustomerIdFromClaims(httpContext),
            Roles = httpContext.User.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value).ToArray()
        });
    }

    var auth = services.GetRequiredService<AuthService>();
    var user = await auth.GetUserAsync(userId.Value, cancellationToken);
    return user is null ? Results.NotFound() : Results.Ok(user);
});

var changePasswordEndpoint = app.MapPost("/api/v1/auth/change-password", async (
    ChangePasswordRequest request,
    HttpContext httpContext,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    var userId = TryReadUserIdFromClaims(httpContext);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    if (!usePostgres)
    {
        return Results.NoContent();
    }

    var auth = services.GetRequiredService<AuthService>();
    var changed = await auth.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword, cancellationToken);
    return changed ? Results.NoContent() : Results.BadRequest("Current password is invalid.");
});

var sessionsEndpoint = app.MapGet("/api/v1/auth/sessions", async (
    HttpContext httpContext,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    var userId = TryReadUserIdFromClaims(httpContext);
    if (userId is null)
    {
        return Results.Unauthorized();
    }

    if (!usePostgres)
    {
        return Results.Ok(Array.Empty<object>());
    }

    var auth = services.GetRequiredService<AuthService>();
    return Results.Ok(await auth.GetSessionsAsync(userId.Value, cancellationToken));
});

if (requireAuthorization)
{
    logoutEndpoint.RequireAuthorization();
    meEndpoint.RequireAuthorization();
    changePasswordEndpoint.RequireAuthorization();
    sessionsEndpoint.RequireAuthorization();
}

var dashboardEndpoint = app.MapGet("/api/v1/dashboard", async (
    IServiceProvider services,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var customerId = ResolveCustomerId(httpContext, requireAuthorization) ?? DemoBankStore.DemoCustomerId;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);
        var accountRows = await db.Accounts.AsNoTracking().Where(item => item.CustomerId == customerId).ToListAsync(cancellationToken);
        var transferRows = await db.Transfers.AsNoTracking().Where(item => item.CustomerId == customerId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var transferIds = transferRows.Select(item => item.Id).ToArray();
        var accountNumbers = accountRows.Select(item => item.Number).ToArray();
        var privilegedDashboard = CanUseCustomerHeader(httpContext);

        return Results.Ok(new
        {
            customer = new
            {
                Id = customerId,
                FullName = user?.FullName ?? "SavranPay Customer",
                Phone = user?.Phone ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                IdentificationStatus = "Полная идентификация",
                AmlRiskLevel = "Низкий",
                IsBlocked = false
            },
            accounts = accountRows.Select(item => new
            {
                item.Id,
                item.CustomerId,
                item.Number,
                MaskedNumber = MaskAccount(item.Number),
                item.Status,
                AvailableBalance = new { MinorUnits = item.AvailableMinorUnits, item.Currency },
                ReservedBalance = new { MinorUnits = item.ReservedMinorUnits, item.Currency }
            }),
            transfers = transferRows.Select(item => new
            {
                item.Id,
                item.CustomerId,
                item.FromAccountId,
                Recipient = new
                {
                    Type = item.RecipientType,
                    AccountNumber = item.RecipientAccountNumber,
                    BankBic = item.RecipientBankBic,
                    Name = item.RecipientName
                },
                Amount = new { MinorUnits = item.AmountMinorUnits, item.Currency },
                item.Purpose,
                item.Status,
                item.CreatedAt,
                item.UpdatedAt
            }),
            ledger = await db.Ledger.AsNoTracking()
                .Where(entry => privilegedDashboard || accountNumbers.Contains(entry.AccountNumber))
                .OrderByDescending(entry => entry.CreatedAt)
                .ToListAsync(cancellationToken),
            riskChecks = await db.RiskChecks.AsNoTracking()
                .Where(check => privilegedDashboard || transferIds.Contains(check.TransferId))
                .OrderByDescending(check => check.CreatedAt)
                .ToListAsync(cancellationToken),
            supportClaims = (await db.SupportClaims.AsNoTracking()
                .Include(claim => claim.Comments)
                .Where(claim => privilegedDashboard || claim.CustomerId == customerId)
                .OrderByDescending(claim => claim.UpdatedAt)
                .ToListAsync(cancellationToken)).Select(ToSupportClaimView),
            auditEvents = await db.AuditEvents.AsNoTracking()
                .Where(audit => privilegedDashboard || transferIds.Contains(audit.OperationId))
                .OrderByDescending(audit => audit.CreatedAt)
                .ToListAsync(cancellationToken),
            notifications = await db.Notifications.AsNoTracking()
                .Where(notification => privilegedDashboard || transferIds.Contains(notification.TransferId))
                .OrderByDescending(notification => notification.CreatedAt)
                .ToListAsync(cancellationToken),
            limits = DashboardLimits(),
            compliance = DashboardCompliance()
        });
    }

    var store = services.GetRequiredService<DemoBankStore>();
    lock (store.SyncRoot)
    {
        return Results.Ok(new
        {
            customer = store.Customer,
            accounts = store.Accounts.Values.Select(ToAccountView).ToArray(),
            transfers = store.Transfers.Values.OrderByDescending(transfer => transfer.CreatedAt).Select(ToTransferView).ToArray(),
            ledger = store.LedgerEntries.OrderByDescending(entry => entry.CreatedAt).ToArray(),
            riskChecks = store.RiskChecks.OrderByDescending(check => check.CreatedAt).ToArray(),
            supportClaims = store.SupportClaims.OrderByDescending(claim => claim.UpdatedAt).Select(ToDemoSupportClaimView).ToArray(),
            auditEvents = store.AuditEvents.OrderByDescending(audit => audit.CreatedAt).ToArray(),
            notifications = store.Notifications.OrderByDescending(notification => notification.CreatedAt).ToArray(),
            limits = DashboardLimits(),
            compliance = DashboardCompliance()
        });
    }
});
if (requireAuthorization)
{
    dashboardEndpoint.RequireAuthorization(policy => policy.RequireRole(
        SavranPayRole.Customer,
        SavranPayRole.SupportOperator,
        SavranPayRole.AmlOfficer,
        SavranPayRole.FraudOfficer,
        SavranPayRole.Admin,
        SavranPayRole.Auditor));
}

var accountsEndpoint = app.MapGet("/api/v1/accounts", async (IServiceProvider services, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var customerId = ResolveCustomerId(httpContext, requireAuthorization) ?? DemoBankStore.DemoCustomerId;
        var accountRows = await db.Accounts.AsNoTracking().Where(item => item.CustomerId == customerId).ToListAsync(cancellationToken);
        return Results.Ok(accountRows.Select(item => new
        {
            item.Id,
            item.CustomerId,
            item.Number,
            MaskedNumber = MaskAccount(item.Number),
            item.Status,
            AvailableBalance = new { MinorUnits = item.AvailableMinorUnits, item.Currency },
            ReservedBalance = new { MinorUnits = item.ReservedMinorUnits, item.Currency }
        }));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().Accounts.Values.Select(ToAccountView));
});
if (requireAuthorization)
{
    accountsEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.Admin, SavranPayRole.Auditor));
}

var transfersEndpoint = app.MapGet("/api/v1/transfers", async (IServiceProvider services, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var customerId = ResolveCustomerId(httpContext, requireAuthorization) ?? DemoBankStore.DemoCustomerId;
        return Results.Ok(await db.Transfers.AsNoTracking().Where(item => item.CustomerId == customerId).OrderByDescending(item => item.CreatedAt).Select(item => new
        {
            item.Id,
            item.CustomerId,
            item.FromAccountId,
            Recipient = new
            {
                Type = item.RecipientType,
                AccountNumber = item.RecipientAccountNumber,
                BankBic = item.RecipientBankBic,
                Name = item.RecipientName
            },
            Amount = new { MinorUnits = item.AmountMinorUnits, item.Currency },
            item.Purpose,
            item.Status,
            item.CreatedAt,
            item.UpdatedAt
        }).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().Transfers.Values.OrderByDescending(transfer => transfer.CreatedAt).Select(ToTransferView));
});
if (requireAuthorization)
{
    transfersEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.Admin, SavranPayRole.Auditor));
}

var auditEventsEndpoint = app.MapGet("/api/v1/audit-events", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        return Results.Ok(await scope.ServiceProvider.GetRequiredService<SavranPayDbContext>().AuditEvents.AsNoTracking().OrderByDescending(audit => audit.CreatedAt).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().AuditEvents.OrderByDescending(audit => audit.CreatedAt));
});
if (requireAuthorization)
{
    auditEventsEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Auditor, SavranPayRole.Admin));
}

var riskChecksEndpoint = app.MapGet("/api/v1/risk-checks", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        return Results.Ok(await scope.ServiceProvider.GetRequiredService<SavranPayDbContext>().RiskChecks.AsNoTracking().OrderByDescending(check => check.CreatedAt).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().RiskChecks.OrderByDescending(check => check.CreatedAt));
});
if (requireAuthorization)
{
    riskChecksEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.AmlOfficer, SavranPayRole.FraudOfficer, SavranPayRole.Admin));
}

var ledgerEndpoint = app.MapGet("/api/v1/ledger", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        return Results.Ok(await scope.ServiceProvider.GetRequiredService<SavranPayDbContext>().Ledger.AsNoTracking().OrderByDescending(entry => entry.CreatedAt).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().LedgerEntries.OrderByDescending(entry => entry.CreatedAt));
});
if (requireAuthorization)
{
    ledgerEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Auditor, SavranPayRole.Admin));
}

var createTransferEndpoint = app.MapPost("/api/v1/transfers", async (
    CreateTransferRequest request,
    HttpRequest httpRequest,
    CreateTransferHandler handler,
    CancellationToken cancellationToken) =>
{
    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
    if (string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return Results.BadRequest("Idempotency-Key header is required.");
    }

    var customerId = ResolveCustomerId(httpRequest.HttpContext, requireAuthorization)
        ?? InMemoryAccountRepository.DemoCustomerId;

    var command = new CreateTransferCommand(
        customerId,
        request.FromAccountId,
        request.Recipient,
        request.Amount,
        request.Purpose,
        idempotencyKey);

    var result = await handler.Handle(command, cancellationToken);
    return Results.Accepted($"/api/v1/transfers/{result.TransferId}", result);
});
if (requireAuthorization)
{
    createTransferEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.Admin));
}

var confirmTransferEndpoint = app.MapPost("/api/v1/transfers/{transferId:guid}/confirm", async (
    Guid transferId,
    ConfirmTransferRequest request,
    HttpContext httpContext,
    ITransferOrderRepository transfers,
    ConfirmTransferHandler handler,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
    }

    if (!CanAccessTransfer(httpContext, transfer.CustomerId, requireAuthorization, SavranPayRole.Admin))
    {
        return Results.Forbid();
    }

    var command = new ConfirmTransferCommand(
        transferId,
        request.ConfirmationType,
        request.Signature,
        request.Nonce,
        request.Timestamp);

    var result = await handler.Handle(command, cancellationToken);
    var updated = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (updated is not null)
    {
        await transfers.AddAsync(updated, cancellationToken);
    }

    return Results.Ok(result);
});
if (requireAuthorization)
{
    confirmTransferEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.Admin));
}

var confirmationChallengeEndpoint = app.MapGet("/api/v1/transfers/{transferId:guid}/confirmation-challenge", async (
    Guid transferId,
    HttpContext httpContext,
    ITransferOrderRepository transfers,
    ICryptoService crypto,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
    }

    if (!CanAccessTransfer(httpContext, transfer.CustomerId, requireAuthorization, SavranPayRole.Admin))
    {
        return Results.Forbid();
    }

    var nonce = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
    var timestamp = DateTimeOffset.UtcNow;
    var payload = crypto.CreateTransferPayload(transfer, nonce, timestamp);

    return Results.Ok(new
    {
        transfer.Id,
        nonce,
        timestamp,
        payload,
        payloadHash = crypto.ComputePayloadHash(payload),
        demoAlgorithm = "HMAC-SHA-256",
        productionCrypto = "HSM/KMS or certified GOST/SKZI provider through ICryptoService"
    });
});
if (requireAuthorization)
{
    confirmationChallengeEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.Admin));
}

var cancelTransferEndpoint = app.MapPost("/api/v1/transfers/{transferId:guid}/cancel", async (
    Guid transferId,
    HttpContext httpContext,
    ITransferOrderRepository transfers,
    IAuditService audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
    }

    if (!CanAccessTransfer(httpContext, transfer.CustomerId, requireAuthorization, SavranPayRole.Admin))
    {
        return Results.Forbid();
    }

    try
    {
        transfer.Cancel(clock.UtcNow);
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(exception.Message);
    }

    await transfers.AddAsync(transfer, cancellationToken);
    await audit.WriteAsync(transfer.Id, "TransferCancelled", "Client cancelled transfer before confirmation.", cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Accepted($"/api/v1/transfers/{transfer.Id}", new { transfer.Id, transfer.Status });
});
if (requireAuthorization)
{
    cancelTransferEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.Admin));
}

var transferDetailsEndpoint = app.MapGet("/api/v1/transfers/{transferId:guid}", async (
    Guid transferId,
    HttpContext httpContext,
    ITransferOrderRepository transfers,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
    }

    if (!CanAccessTransfer(
            httpContext,
            transfer.CustomerId,
            requireAuthorization,
            SavranPayRole.SupportOperator,
            SavranPayRole.AmlOfficer,
            SavranPayRole.FraudOfficer,
            SavranPayRole.Admin,
            SavranPayRole.Auditor))
    {
        return Results.Forbid();
    }

    return Results.Ok(new
    {
        transfer = ToTransferView(transfer)
    });
});
if (requireAuthorization)
{
    transferDetailsEndpoint.RequireAuthorization(policy => policy.RequireRole(
        SavranPayRole.Customer,
        SavranPayRole.SupportOperator,
        SavranPayRole.AmlOfficer,
        SavranPayRole.FraudOfficer,
        SavranPayRole.Admin,
        SavranPayRole.Auditor));
}

var unauthorizedClaimEndpoint = app.MapPost("/api/v1/transfers/{transferId:guid}/unauthorized-claim", async (
    Guid transferId,
    HttpContext httpContext,
    IServiceProvider services,
    ITransferOrderRepository transfers,
    IAuditService audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
    }

    if (!CanAccessTransfer(httpContext, transfer.CustomerId, requireAuthorization, SavranPayRole.SupportOperator, SavranPayRole.Admin))
    {
        return Results.Forbid();
    }

    transfer.MarkDisputed(clock.UtcNow);
    await transfers.AddAsync(transfer, cancellationToken);
    await audit.WriteAsync(transfer.Id, "UnauthorizedClaimCreated", "Client reported an unauthorized transfer.", cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var now = DateTimeOffset.UtcNow;
        var claim = await db.SupportClaims.FirstOrDefaultAsync(item => item.TransferId == transfer.Id, cancellationToken);
        if (claim is null)
        {
            claim = new SupportClaimEntity
            {
                Id = Guid.NewGuid(),
                TransferId = transfer.Id,
                CustomerId = transfer.CustomerId,
                CreatedByUserId = TryReadUserIdFromClaims(httpContext),
                Category = "Unauthorized transfer",
                Status = "Open",
                AssignedTo = "Support",
                Comment = "Client reported an unauthorized transfer.",
                ContactComment = string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.SupportClaims.Add(claim);
        }
        else
        {
            claim.Status = "Open";
            claim.AssignedTo = "Support";
            claim.UpdatedAt = now;
        }

        db.SupportClaimComments.Add(new SupportClaimCommentEntity
        {
            Id = Guid.NewGuid(),
            SupportClaimId = claim.Id,
            AuthorUserId = TryReadUserIdFromClaims(httpContext),
            AuthorRole = CurrentRole(httpContext),
            Message = "Unauthorized transfer dispute was opened.",
            CreatedAt = now
        });
        AddAuditEvent(db, claim.Id, "SupportClaimCreated", $"Support claim opened for transfer {transfer.Id}.");
        await db.SaveChangesAsync(cancellationToken);
    }
    else
    {
        var store = services.GetRequiredService<DemoBankStore>();
        lock (store.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var claim = store.SupportClaims.FirstOrDefault(item => item.TransferId == transfer.Id);
            if (claim is null)
            {
                claim = new DemoSupportClaim
                {
                    Id = Guid.NewGuid(),
                    TransferId = transfer.Id,
                    CustomerId = transfer.CustomerId,
                    CreatedByUserId = TryReadUserIdFromClaims(httpContext),
                    Category = "Unauthorized transfer",
                    Status = "Open",
                    AssignedTo = "Support",
                    Comment = "Client reported an unauthorized transfer.",
                    ContactComment = string.Empty,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                store.SupportClaims.Add(claim);
            }

            claim.Comments.Add(new DemoSupportClaimComment(
                Guid.NewGuid(),
                claim.Id,
                TryReadUserIdFromClaims(httpContext),
                CurrentRole(httpContext),
                "Unauthorized transfer dispute was opened.",
                now));
            store.AuditEvents.Add(new DemoAuditEvent(Guid.NewGuid(), claim.Id, "SupportClaimCreated", $"Support claim opened for transfer {transfer.Id}.", now));
        }
    }

    return Results.Accepted($"/api/v1/transfers/{transfer.Id}", new { transfer.Id, transfer.Status });
});
if (requireAuthorization)
{
    unauthorizedClaimEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.Admin));
}

app.MapSupportEndpoints(usePostgres, requireAuthorization);

app.MapAdminEndpoints(usePostgres, requireAuthorization);
app.MapRiskEndpoints(usePostgres, requireAuthorization);

app.MapGet("/api/v1/cabinets", () => Results.Ok(new[]
{
    new { Path = "/cabinet/client", Role = SavranPayRole.Customer },
    new { Path = "/cabinet/support", Role = SavranPayRole.SupportOperator },
    new { Path = "/cabinet/aml", Role = SavranPayRole.AmlOfficer },
    new { Path = "/cabinet/fraud", Role = SavranPayRole.FraudOfficer },
    new { Path = "/cabinet/admin", Role = SavranPayRole.Admin },
    new { Path = "/cabinet/audit", Role = SavranPayRole.Auditor }
})).AllowAnonymous();

app.MapGet("/api/v1/demo", () => new
{
    InMemoryAccountRepository.DemoCustomerId,
    InMemoryAccountRepository.DemoAccountId,
    DemoBankStore.SavingsAccountId,
    DemoLogins = new[]
    {
        "client@savranpay.local / Client123!",
        "support@savranpay.local / Support123!",
        "aml@savranpay.local / Aml123!",
        "fraud@savranpay.local / Fraud123!",
        "admin@savranpay.local / Admin123!",
        "audit@savranpay.local / Audit123!"
    }
}).AllowAnonymous();

app.MapFallbackToFile("index.html");

app.Run();

static Guid? TryReadCustomerId(HttpRequest request)
{
    var value = request.Headers["X-Customer-Id"].ToString();
    return Guid.TryParse(value, out var customerId) ? customerId : null;
}

static Guid? TryReadCustomerIdFromClaims(HttpContext context)
{
    var value = context.User.FindFirstValue("customer_id");
    return Guid.TryParse(value, out var customerId) ? customerId : null;
}

static Guid? TryReadUserIdFromClaims(HttpContext context)
{
    var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(value, out var userId) ? userId : null;
}

static Guid? ResolveCustomerId(HttpContext context, bool requireAuthorization)
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

static bool CanAccessTransfer(HttpContext context, Guid transferCustomerId, bool requireAuthorization, params string[] privilegedRoles)
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

static bool CanUseCustomerHeader(HttpContext context)
{
    return context.User.IsInRole(SavranPayRole.SupportOperator)
        || context.User.IsInRole(SavranPayRole.Admin)
        || context.User.IsInRole(SavranPayRole.Auditor)
        || context.User.IsInRole(SavranPayRole.AmlOfficer)
        || context.User.IsInRole(SavranPayRole.FraudOfficer);
}

static string GetIpAddress(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string GetUserAgent(HttpContext context)
{
    var userAgent = context.Request.Headers.UserAgent.ToString();
    return string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent;
}

static object ToSupportClaimView(SupportClaimEntity claim) => new
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

static object ToDemoSupportClaimView(DemoSupportClaim claim) => new
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

static string CurrentRole(HttpContext context) =>
    context.User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Role)?.Value ?? "Anonymous";

static void AddAuditEvent(SavranPayDbContext db, Guid operationId, string eventType, string message)
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

static object ToAccountView(SavranPay.Domain.Accounts.Account account)
{
    return new
    {
        account.Id,
        account.CustomerId,
        account.Number,
        MaskedNumber = MaskAccount(account.Number),
        account.Status,
        account.AvailableBalance,
        account.ReservedBalance
    };
}

static object ToTransferView(SavranPay.Domain.Transfers.TransferOrder transfer)
{
    return new
    {
        transfer.Id,
        transfer.CustomerId,
        transfer.FromAccountId,
        transfer.Recipient,
        transfer.Amount,
        transfer.Purpose,
        transfer.Status,
        transfer.CreatedAt,
        transfer.UpdatedAt
    };
}

static string MaskAccount(string accountNumber)
{
    if (accountNumber.Length <= 8)
    {
        return accountNumber;
    }

    return $"{accountNumber[..4]} **** **** {accountNumber[^4..]}";
}

static object[] DashboardLimits() =>
[
    new { Name = "Одна операция", Value = "600 000 RUB" },
    new { Name = "Новое устройство", Value = "Step-up confirmation" },
    new { Name = "Новый получатель", Value = "Дополнительная антифрод-проверка" }
];

static string[] DashboardCompliance() =>
[
    "JWT + refresh token + роли Customer/SupportOperator/AmlOfficer/FraudOfficer/Admin/Auditor",
    "PostgreSQL/EF Core: users, roles, refresh_tokens, accounts, transfers, ledger, audit_events",
    "AML/KYC и антифрод вынесены в сервисы доменного сценария",
    "Outbox/inbox таблицы готовы для гарантированной событийной доставки",
    "Health checks и structured logging включены для production-контура"
];

public partial class Program;
