using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BankTransfers.IntegrationTests;

public sealed class TransferApiTests
{
    [Fact]
    public async Task CreateTransfer_ReturnsAccepted()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Content = JsonContent.Create(new
        {
            fromAccountId = "22222222-2222-2222-2222-222222222222",
            recipient = new
            {
                type = "Account",
                accountNumber = "40817810000000000002",
                bankBic = "044525225",
                name = "Иван Петров"
            },
            amount = new
            {
                minorUnits = 150000,
                currency = "RUB"
            },
            purpose = "Перевод собственных средств"
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateTransferResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.TransferId);
        Assert.Equal("PendingClientConfirmation", result.Status);
    }

    [Fact]
    public async Task Dashboard_ReturnsDemoCustomerAndAccounts()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard");

        Assert.NotNull(dashboard);
        Assert.NotNull(dashboard.Customer);
        Assert.NotEmpty(dashboard.Accounts);
    }

    [Fact]
    public async Task Cors_AllowsViteFrontendOrigin()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Add("Origin", "http://127.0.0.1:5173");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains("http://127.0.0.1:5173", values);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("DISABLE_HTTPS_REDIRECTION", "true");
                builder.UseEnvironment("Development");
            });
    }

    private sealed record CreateTransferResponse(Guid TransferId, string Status);

    private sealed record DashboardResponse(CustomerResponse Customer, AccountResponse[] Accounts);

    private sealed record CustomerResponse(Guid Id, string FullName);

    private sealed record AccountResponse(Guid Id, string MaskedNumber);
}
