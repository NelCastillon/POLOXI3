using Ams.Application.Common.Dtos;

namespace Ams.Web.Components.Shared;

public sealed record RequestBindDialogContext(
    Guid TenantId,
    Guid UserId,
    Guid SubmissionId,
    Guid QuoteId,
    Guid AccountId,
    Guid CarrierId,
    Guid ProposalId,
    Guid CustomerAuthorizationId,
    Guid ClientAcceptanceId,
    string AccountName,
    string SubmissionNumber,
    string QuoteNumber,
    string CarrierName,
    string LineOfBusiness,
    decimal AnnualPremium,
    DateTime EffectiveDate,
    DateTime ExpirationDate,
    string? PolicyNumber,
    string? Subjectivities,
    ClientAcceptanceDto Acceptance,
    IReadOnlyList<PolicyCreationSourceDto> PolicySources,
    IReadOnlyList<PolicyBindStatusDto> BindStatuses,
    IReadOnlyList<SubmissionReferenceOptionDto> AuthorizationMethods,
    IReadOnlyList<SubmissionReferenceOptionDto> ConfirmationSources,
    IReadOnlyList<SubmissionReferenceOptionDto> BindingMethods,
    IReadOnlyList<SubmissionReferenceOptionDto> BindingAuthorities);

public sealed record RequestBindDialogResult(
    Guid TransactionId,
    Guid SubmissionId,
    Guid QuoteId,
    string BindStatusCode,
    bool CreatedPolicy);
