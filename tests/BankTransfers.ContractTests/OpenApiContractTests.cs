using Xunit;

namespace BankTransfers.ContractTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public void TransferContract_MatchesOpenApiSpecification()
    {
        var root = FindRepositoryRoot();
        var openApiPath = Path.Combine(root, "docs", "api", "openapi.yaml");

        Assert.True(File.Exists(openApiPath), "OpenAPI specification file must exist.");

        var yaml = File.ReadAllText(openApiPath);
        Assert.Contains("/api/v1/transfers", yaml);
        Assert.Contains("/api/v1/transfers/{transferId}/confirm", yaml);
        Assert.Contains("Idempotency-Key", yaml);
        Assert.Contains("ConfirmTransferRequest", yaml);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BankTransfers.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
