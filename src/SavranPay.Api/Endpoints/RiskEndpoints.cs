using SavranPay.Api.Contracts;
using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Auth;
using SavranPay.SharedKernel;

internal static class RiskEndpoints
{
    internal static void MapRiskEndpoints(this IEndpointRouteBuilder app, bool usePostgres, bool requireAuthorization)
    {
        var amlDecisionEndpoint = app.MapPost("/api/v1/aml/transfers/{transferId:guid}/decision", async (
            Guid transferId,
            RiskDecisionRequest request,
            IServiceProvider services,
            ITransferOrderRepository transfers,
            IAuditService audit,
            IUnitOfWork unitOfWork,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            return await ApiEndpointHelpers.RecordManualRiskDecisionAsync(
                transferId,
                "AML",
                request,
                services,
                usePostgres,
                transfers,
                audit,
                unitOfWork,
                clock,
                cancellationToken);
        });

        var fraudDecisionEndpoint = app.MapPost("/api/v1/fraud/transfers/{transferId:guid}/decision", async (
            Guid transferId,
            RiskDecisionRequest request,
            IServiceProvider services,
            ITransferOrderRepository transfers,
            IAuditService audit,
            IUnitOfWork unitOfWork,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            return await ApiEndpointHelpers.RecordManualRiskDecisionAsync(
                transferId,
                "Fraud",
                request,
                services,
                usePostgres,
                transfers,
                audit,
                unitOfWork,
                clock,
                cancellationToken);
        });

        if (requireAuthorization)
        {
            amlDecisionEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.AmlOfficer, SavranPayRole.Admin));
            fraudDecisionEndpoint.RequireAuthorization(policy => policy.RequireRole(SavranPayRole.FraudOfficer, SavranPayRole.Admin));
        }
    }
}
