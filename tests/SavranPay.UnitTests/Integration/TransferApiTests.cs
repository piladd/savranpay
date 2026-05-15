using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SavranPay.UnitTests.Integration;

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
                name = "РРІР°РЅ РџРµС‚СЂРѕРІ"
            },
            amount = new
            {
                minorUnits = 150000,
                currency = "RUB"
            },
            purpose = "РџРµСЂРµРІРѕРґ СЃРѕР±СЃС‚РІРµРЅРЅС‹С… СЃСЂРµРґСЃС‚РІ"
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

    [Fact]
    public async Task Auth_LoginAndMe_ReturnsCurrentUser()
    {
        await using var factory = CreateFactory(requireAuthorization: true);
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            login = "client@savranpay.local",
            password = "Client123!"
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var me = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task CustomerHeaderSpoofing_IsIgnoredForCustomerRole()
    {
        await using var factory = CreateFactory(requireAuthorization: true);
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            login = "client@savranpay.local",
            password = "Client123!"
        });

        var token = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(token);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("X-Customer-Id", "99999999-9999-9999-9999-999999999999");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.NotNull(dashboard);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), dashboard.Customer.Id);
    }

    [Fact]
    public async Task UnauthorizedClaim_CreatesSupportClaimInDashboard()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var transferId = await CreateDemoTransferAsync(client);

        using var response = await client.PostAsJsonAsync($"/api/v1/transfers/{transferId}/unauthorized-claim", new
        {
            reason = "Unauthorized transfer",
            description = "Client disputes the operation.",
            contactPhone = "+79990000000"
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var dashboardResponse = await client.GetAsync("/api/v1/dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        await using var stream = await dashboardResponse.Content.ReadAsStreamAsync();
        using var dashboard = await JsonDocument.ParseAsync(stream);
        var supportClaims = dashboard.RootElement.GetProperty("supportClaims");

        Assert.Equal(JsonValueKind.Array, supportClaims.ValueKind);
        Assert.Contains(supportClaims.EnumerateArray(), claim =>
            claim.GetProperty("transferId").GetGuid() == transferId &&
            claim.GetProperty("assignedTo").GetString() == "Support");
    }

    [Fact]
    public async Task AdminTransferActions_RecordRetryAndReturnTechnicalDetails()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var transferId = await CreateDemoTransferAsync(client);

        using var retryResponse = await client.PostAsJsonAsync($"/api/v1/admin/transfers/{transferId}/retry", new
        {
            reason = "Integration test retry"
        });

        Assert.Equal(HttpStatusCode.Accepted, retryResponse.StatusCode);

        using var detailsResponse = await client.GetAsync($"/api/v1/admin/transfers/{transferId}/technical-details");

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        await using var stream = await detailsResponse.Content.ReadAsStreamAsync();
        using var details = await JsonDocument.ParseAsync(stream);

        Assert.Equal(transferId, details.RootElement.GetProperty("transferId").GetGuid());
    }

    private static WebApplicationFactory<Program> CreateFactory(bool requireAuthorization = false)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("DISABLE_HTTPS_REDIRECTION", "true");
                builder.UseSetting("Jwt:RequireAuthorization", requireAuthorization ? "true" : "false");
                builder.UseEnvironment("Development");
            });
    }

    private static async Task<Guid> CreateDemoTransferAsync(HttpClient client)
    {
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
                name = "Demo Recipient"
            },
            amount = new
            {
                minorUnits = 150000,
                currency = "RUB"
            },
            purpose = "Integration test transfer"
        });

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateTransferResponse>();
        Assert.NotNull(result);
        return result.TransferId;
    }

    private sealed record CreateTransferResponse(Guid TransferId, string Status);
    private sealed record LoginResponse(string AccessToken, string RefreshToken);

    private sealed record DashboardResponse(CustomerResponse Customer, AccountResponse[] Accounts);

    private sealed record CustomerResponse(Guid Id, string FullName);

    private sealed record AccountResponse(Guid Id, string MaskedNumber);
}
