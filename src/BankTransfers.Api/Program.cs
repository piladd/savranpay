using BankTransfers.Api.Contracts;
using BankTransfers.Application.Abstractions;
using BankTransfers.Application.Transfers.ConfirmTransfer;
using BankTransfers.Application.Transfers.CreateTransfer;
using BankTransfers.Infrastructure.Audit;
using BankTransfers.Infrastructure.Auth;
using BankTransfers.Infrastructure.Compliance;
using BankTransfers.Infrastructure.Crypto;
using BankTransfers.Infrastructure.Demo;
using BankTransfers.Infrastructure.Limits;
using BankTransfers.Infrastructure.Notifications;
using BankTransfers.Infrastructure.Persistence;
using BankTransfers.Infrastructure.Risk;
using BankTransfers.Infrastructure.Transfers;
using BankTransfers.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 5001;
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("SavranPayFrontend", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5173",
                "http://localhost:5173",
                "https://127.0.0.1:5173",
                "http://127.0.0.1:5173",
                "https://savranpay.netlify.app")
            .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", "X-Request-Id")
            .WithMethods("GET", "POST", "OPTIONS");
    });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ProductionCryptoOptions>(builder.Configuration.GetSection("ProductionCrypto"));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IClock, SystemClock>();
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
}
else
{
    builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
    builder.Services.AddSingleton<ITransferOrderRepository, InMemoryTransferOrderRepository>();
    builder.Services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
    builder.Services.AddSingleton<ITransferExecutionService, DemoTransferExecutionService>();
    builder.Services.AddSingleton<IAuditService, ConsoleAuditService>();
}

builder.Services.AddSingleton<ILimitService, SimpleLimitService>();
builder.Services.AddSingleton<IAmlService, AllowAllAmlService>();
builder.Services.AddSingleton<IFraudService, SimpleFraudService>();
builder.Services.AddSingleton<ICryptoService, DemoCryptoService>();
builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
builder.Services.AddScoped<CreateTransferHandler>();
builder.Services.AddScoped<ConfirmTransferHandler>();

var app = builder.Build();

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
app.UseMiddleware<JwtAuthenticationMiddleware>();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://unpkg.com 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "connect-src 'self' https://savranpay.netlify.app; " +
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

app.MapPost("/api/v1/auth/login", async (
    LoginRequest request,
    IServiceProvider services,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        var auth = services.GetRequiredService<AuthService>();
        var result = await auth.LoginAsync(request.Login, request.Password, cancellationToken);
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
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (!usePostgres)
    {
        return Results.Unauthorized();
    }

    var auth = services.GetRequiredService<AuthService>();
    var result = await auth.RefreshAsync(request.RefreshToken, cancellationToken);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
}).AllowAnonymous();

var dashboardEndpoint = app.MapGet("/api/v1/dashboard", async (
    IServiceProvider services,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var customerId = TryReadCustomerId(httpContext.Request) ?? TryReadCustomerIdFromClaims(httpContext) ?? DemoBankStore.DemoCustomerId;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);
        var accountRows = await db.Accounts.AsNoTracking().Where(item => item.CustomerId == customerId).ToListAsync(cancellationToken);

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
            transfers = await db.Transfers.AsNoTracking().Where(item => item.CustomerId == customerId).OrderByDescending(item => item.CreatedAt).Select(item => new
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
            }).ToListAsync(cancellationToken),
            ledger = await db.Ledger.AsNoTracking().OrderByDescending(entry => entry.CreatedAt).ToListAsync(cancellationToken),
            riskChecks = Array.Empty<object>(),
            auditEvents = await db.AuditEvents.AsNoTracking().OrderByDescending(audit => audit.CreatedAt).ToListAsync(cancellationToken),
            notifications = await db.Notifications.AsNoTracking().OrderByDescending(notification => notification.CreatedAt).ToListAsync(cancellationToken),
            limits = DashboardLimits(),
            compliance = DashboardCompliance()
        });
    }

    var store = services.GetRequiredService<DemoBankStore>();
    return Results.Ok(new
    {
        customer = store.Customer,
        accounts = store.Accounts.Values.Select(ToAccountView),
        transfers = store.Transfers.Values.OrderByDescending(transfer => transfer.CreatedAt).Select(ToTransferView),
        ledger = store.LedgerEntries.OrderByDescending(entry => entry.CreatedAt),
        riskChecks = store.RiskChecks.OrderByDescending(check => check.CreatedAt),
        auditEvents = store.AuditEvents.OrderByDescending(audit => audit.CreatedAt),
        notifications = store.Notifications.OrderByDescending(notification => notification.CreatedAt),
        limits = DashboardLimits(),
        compliance = DashboardCompliance()
    });
});
if (requireAuthorization)
{
    dashboardEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.Customer, SavranPayRole.SupportOperator, SavranPayRole.Admin, SavranPayRole.Auditor));
}

app.MapGet("/api/v1/accounts", async (IServiceProvider services, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var customerId = TryReadCustomerId(httpContext.Request) ?? TryReadCustomerIdFromClaims(httpContext) ?? DemoBankStore.DemoCustomerId;
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

app.MapGet("/api/v1/transfers", async (IServiceProvider services, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SavranPayDbContext>();
        var customerId = TryReadCustomerId(httpContext.Request) ?? TryReadCustomerIdFromClaims(httpContext) ?? DemoBankStore.DemoCustomerId;
        return Results.Ok(await db.Transfers.AsNoTracking().Where(item => item.CustomerId == customerId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().Transfers.Values.OrderByDescending(transfer => transfer.CreatedAt).Select(ToTransferView));
});

app.MapGet("/api/v1/audit-events", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        return Results.Ok(await scope.ServiceProvider.GetRequiredService<SavranPayDbContext>().AuditEvents.AsNoTracking().OrderByDescending(audit => audit.CreatedAt).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().AuditEvents.OrderByDescending(audit => audit.CreatedAt));
});

app.MapGet("/api/v1/risk-checks", (DemoBankStore store) =>
{
    return Results.Ok(store.RiskChecks.OrderByDescending(check => check.CreatedAt));
});

app.MapGet("/api/v1/ledger", async (IServiceProvider services, CancellationToken cancellationToken) =>
{
    if (usePostgres)
    {
        await using var scope = services.CreateAsyncScope();
        return Results.Ok(await scope.ServiceProvider.GetRequiredService<SavranPayDbContext>().Ledger.AsNoTracking().OrderByDescending(entry => entry.CreatedAt).ToListAsync(cancellationToken));
    }

    return Results.Ok(services.GetRequiredService<DemoBankStore>().LedgerEntries.OrderByDescending(entry => entry.CreatedAt));
});

app.MapPost("/api/v1/transfers", async (
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

    var customerId = TryReadCustomerId(httpRequest)
        ?? TryReadCustomerIdFromClaims(httpRequest.HttpContext)
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

app.MapPost("/api/v1/transfers/{transferId:guid}/confirm", async (
    Guid transferId,
    ConfirmTransferRequest request,
    ITransferOrderRepository transfers,
    ConfirmTransferHandler handler,
    CancellationToken cancellationToken) =>
{
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

app.MapGet("/api/v1/transfers/{transferId:guid}/confirmation-challenge", async (
    Guid transferId,
    ITransferOrderRepository transfers,
    ICryptoService crypto,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
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

app.MapGet("/api/v1/transfers/{transferId:guid}", async (
    Guid transferId,
    ITransferOrderRepository transfers,
    CancellationToken cancellationToken) =>
{
    var transfer = await transfers.GetByIdAsync(transferId, cancellationToken);
    if (transfer is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new
    {
        transfer = ToTransferView(transfer)
    });
});

app.MapPost("/api/v1/transfers/{transferId:guid}/unauthorized-claim", async (
    Guid transferId,
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

    transfer.MarkDisputed(clock.UtcNow);
    await transfers.AddAsync(transfer, cancellationToken);
    await audit.WriteAsync(transfer.Id, "UnauthorizedClaimCreated", "Client reported an unauthorized transfer.", cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Accepted($"/api/v1/transfers/{transfer.Id}", new { transfer.Id, transfer.Status });
});

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

static object ToAccountView(BankTransfers.Domain.Accounts.Account account)
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

static object ToTransferView(BankTransfers.Domain.Transfers.TransferOrder transfer)
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
