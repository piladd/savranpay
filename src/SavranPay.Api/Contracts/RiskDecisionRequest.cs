namespace SavranPay.Api.Contracts;

public sealed record RiskDecisionRequest(
    string Decision,
    string Details,
    string[]? RiskFactors,
    string? BlockReason,
    bool? DocumentsRequested,
    bool? StepUpRequired);
