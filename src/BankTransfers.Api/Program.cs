using BankTransfers.Api.Contracts;
using BankTransfers.Application.Abstractions;
using BankTransfers.Application.Transfers.ConfirmTransfer;
using BankTransfers.Application.Transfers.CreateTransfer;
using BankTransfers.Infrastructure.Audit;
using BankTransfers.Infrastructure.Compliance;
using BankTransfers.Infrastructure.Crypto;
using BankTransfers.Infrastructure.Demo;
using BankTransfers.Infrastructure.Limits;
using BankTransfers.Infrastructure.Persistence;
using BankTransfers.Infrastructure.Risk;
using BankTransfers.Infrastructure.Transfers;
using BankTransfers.SharedKernel;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

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
                "http://127.0.0.1:5173")
            .WithHeaders("Content-Type", "Idempotency-Key", "X-Request-Id")
            .WithMethods("GET", "POST");
    });
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<DemoBankStore>();
builder.Services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
builder.Services.AddSingleton<ITransferOrderRepository, InMemoryTransferOrderRepository>();
builder.Services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();
builder.Services.AddSingleton<ILimitService, SimpleLimitService>();
builder.Services.AddSingleton<IAmlService, AllowAllAmlService>();
builder.Services.AddSingleton<IFraudService, SimpleFraudService>();
builder.Services.AddSingleton<ICryptoService, DemoCryptoService>();
builder.Services.AddSingleton<ITransferExecutionService, DemoTransferExecutionService>();
builder.Services.AddSingleton<IAuditService, ConsoleAuditService>();
builder.Services.AddScoped<CreateTransferHandler>();
builder.Services.AddScoped<ConfirmTransferHandler>();

var app = builder.Build();

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
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://unpkg.com 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
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

app.MapGet("/api/v1/dashboard", (DemoBankStore store) => Results.Ok(new
{
    customer = store.Customer,
    accounts = store.Accounts.Values.Select(ToAccountView),
    transfers = store.Transfers.Values
        .OrderByDescending(transfer => transfer.CreatedAt)
        .Select(ToTransferView),
    ledger = store.LedgerEntries.OrderByDescending(entry => entry.CreatedAt),
    riskChecks = store.RiskChecks.OrderByDescending(check => check.CreatedAt),
    auditEvents = store.AuditEvents.OrderByDescending(audit => audit.CreatedAt),
    notifications = store.Notifications.OrderByDescending(notification => notification.CreatedAt),
    limits = new[]
    {
        new { Name = "Одна операция", Value = "600 000 RUB" },
        new { Name = "Новое устройство", Value = "Step-up confirmation" },
        new { Name = "Новый получатель", Value = "Дополнительная антифрод-проверка" }
    },
    compliance = new[]
    {
        "AML/KYC: базовая проверка клиента и назначения платежа",
        "Антифрод: сумма, устройство, новый получатель, частота операций",
        "Идемпотентность: заголовок Idempotency-Key",
        "Аудит: каждое действие попадает в журнал",
        "Ledger: перевод создает дебетовую и кредитовую запись"
    }
}));

app.MapGet("/api/v1/accounts", (DemoBankStore store) =>
{
    return Results.Ok(store.Accounts.Values.Select(ToAccountView));
});

app.MapGet("/api/v1/transfers", (DemoBankStore store) =>
{
    return Results.Ok(store.Transfers.Values.OrderByDescending(transfer => transfer.CreatedAt).Select(ToTransferView));
});

app.MapGet("/api/v1/audit-events", (DemoBankStore store) =>
{
    return Results.Ok(store.AuditEvents.OrderByDescending(audit => audit.CreatedAt));
});

app.MapGet("/api/v1/risk-checks", (DemoBankStore store) =>
{
    return Results.Ok(store.RiskChecks.OrderByDescending(check => check.CreatedAt));
});

app.MapGet("/api/v1/ledger", (DemoBankStore store) =>
{
    return Results.Ok(store.LedgerEntries.OrderByDescending(entry => entry.CreatedAt));
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
        demoSharedSecretWarning = "Учебный стенд: секрет находится в браузере только для демонстрации WebCrypto. В банке ключ хранится в HSM/СКЗИ."
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
    await audit.WriteAsync(transfer.Id, "UnauthorizedClaimCreated", "Client reported an unauthorized transfer.", cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Accepted($"/api/v1/transfers/{transfer.Id}", new { transfer.Id, transfer.Status });
});

app.MapGet("/api/v1/demo", () => new
{
    InMemoryAccountRepository.DemoCustomerId,
    InMemoryAccountRepository.DemoAccountId,
    DemoBankStore.SavingsAccountId
});

app.MapFallbackToFile("index.html");

app.Run();

static Guid? TryReadCustomerId(HttpRequest request)
{
    var value = request.Headers["X-Customer-Id"].ToString();
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

public partial class Program;
