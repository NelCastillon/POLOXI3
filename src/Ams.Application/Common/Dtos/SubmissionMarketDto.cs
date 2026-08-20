namespace Ams.Application.Common.Dtos;

public sealed class SubmissionMarketDto
{
    public Guid SubmissionMarketId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AppetiteScore { get; set; }
    public bool IsRecommended { get; set; }
    public string? DeclineReason { get; set; }
    public string? UnderwriterName { get; set; }
    public string? UnderwriterEmail { get; set; }
    public string? UnderwriterPhone { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public string? RequestedCoverageSummary { get; set; }
    public string? RequestedLimits { get; set; }
    public string? SubmissionMethodCode { get; set; }
    public Guid? FollowUpTaskId { get; set; }
    public Guid? LatestQuoteId { get; set; }
    public string? LatestQuoteNumber { get; set; }
    public string? LatestQuoteStatus { get; set; }
    public DateTime? LatestQuoteReceivedDateUtc { get; set; }
    public DateTime AddedDateUtc { get; set; }
    public DateTime? SubmittedDateUtc { get; set; }
    public DateTime? RespondedDateUtc { get; set; }
    public string? QuoteRequestScopeCode { get; set; }
    public decimal? RequestedPremium { get; set; }
    public string? LatestQuoteRequestActionCode { get; set; }
    public string? LatestQuoteRequestReasonCode { get; set; }
    public string? LatestQuoteRequestMethodCode { get; set; }
    public string? LatestQuoteRequestStatusCode { get; set; }
    public int LatestQuoteRequestVersion { get; set; }
    public DateTime? LatestQuoteRequestDateUtc { get; set; }
    public Guid? LatestQuoteRequestId { get; set; }
    public string? LatestQuoteRequestDeliveryMethodCode { get; set; }
    public string? LatestQuoteRequestAssignedUnderwriterName { get; set; }
    public string? LatestQuoteRequestAssignedUnderwriterEmail { get; set; }
    public string? LatestQuoteRequestAssignedUnderwriterPhone { get; set; }
    public DateTime? LatestQuoteRequestDueDateUtc { get; set; }
    public int LatestQuoteRequestRetryCount { get; set; }
    public string? LatestQuoteRequestCorrelationId { get; set; }
    public string? LatestQuoteRequestCarrierReferenceNumber { get; set; }
    public DateTime? LatestQuoteRequestResponseDateUtc { get; set; }
    public DateTime? LatestQuoteRequestDispatchedDateUtc { get; set; }
    public DateTime? LatestQuoteRequestAcknowledgedDateUtc { get; set; }
    public DateTime? LatestQuoteRequestLastAttemptDateUtc { get; set; }
    public string? LatestQuoteRequestLastError { get; set; }
    public int LatestQuoteRequestAttachmentCount { get; set; }
    public bool IsPastDue { get; set; }
    public int? DaysUntilDue { get; set; }
    public int? DaysPastDue { get; set; }
    public Guid? LatestCarrierTransmissionId { get; set; }
    public string? LatestTransmissionStatusCode { get; set; }
    public string? LatestTransmissionChannelCode { get; set; }
    public string? LatestTransmissionConnectorName { get; set; }
    public string? LatestTransmissionExternalReferenceNumber { get; set; }
    public DateTime? LatestTransmissionSentDateUtc { get; set; }
    public DateTime? LatestTransmissionConfirmedDateUtc { get; set; }
    public DateTime? LatestTransmissionFailedDateUtc { get; set; }
    public DateTime? LatestTransmissionBounceDateUtc { get; set; }
    public string? LatestTransmissionLastError { get; set; }
    public string? LatestInboundResponseStatusCode { get; set; }
    public string? LatestInboundResponseTypeCode { get; set; }
    public DateTime? LatestInboundResponseReceivedDateUtc { get; set; }
    public IReadOnlyList<SubmissionMarketLineDto> RequestedLines { get; set; } = [];
    public IReadOnlyList<QuoteRequestDto> QuoteRequests { get; set; } = [];
    public IReadOnlyList<CarrierTransmissionDto> Transmissions { get; set; } = [];
    public IReadOnlyList<CarrierInboundResponseDto> InboundResponses { get; set; } = [];
}

public sealed class QuoteRequestDto
{
    public Guid QuoteRequestId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SubmissionMarketId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string QuoteRequestActionCode { get; set; } = string.Empty;
    public string? QuoteRequestReasonCode { get; set; }
    public string QuoteRequestMethodCode { get; set; } = string.Empty;
    public string? DeliveryMethodCode { get; set; }
    public string QuoteRequestScopeCode { get; set; } = string.Empty;
    public decimal? RequestedPremium { get; set; }
    public decimal? Premium { get; set; }
    public decimal? CommissionPercent { get; set; }
    public string? QuoteNumber { get; set; }
    public DateTime? ExpirationDateUtc { get; set; }
    public string? CoverageNotes { get; set; }
    public string? CarrierReferenceNumber { get; set; }
    public int RequestVersion { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime RequestedDateUtc { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public string? AssignedUnderwriterName { get; set; }
    public string? AssignedUnderwriterEmail { get; set; }
    public string? AssignedUnderwriterPhone { get; set; }
    public int RetryCount { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? DispatchedDateUtc { get; set; }
    public DateTime? AcknowledgedDateUtc { get; set; }
    public DateTime? ResponseDateUtc { get; set; }
    public DateTime? LastAttemptDateUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime? ClosedDateUtc { get; set; }
    public int AttachmentCount { get; set; }
    public int QuoteCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class SubmissionMarketLineDto
{
    public Guid SubmissionMarketLineId { get; set; }
    public Guid SubmissionMarketId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SubmissionLineId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public decimal TargetPremium { get; set; }
}

public sealed class CarrierTransmissionDto
{
    public Guid CarrierTransmissionId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SubmissionMarketId { get; set; }
    public Guid CarrierId { get; set; }
    public Guid? CarrierExternalConnectorId { get; set; }
    public string? ConnectorName { get; set; }
    public string TransmissionTypeCode { get; set; } = string.Empty;
    public string ChannelCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? Subject { get; set; }
    public string? EndpointUri { get; set; }
    public string? ExternalReferenceNumber { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptDateUtc { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public DateTime? ConfirmedDateUtc { get; set; }
    public DateTime? FailedDateUtc { get; set; }
    public DateTime? BounceDateUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public IReadOnlyList<CarrierTransmissionEventDto> Events { get; set; } = [];
}

public sealed class CarrierTransmissionEventDto
{
    public Guid CarrierTransmissionEventId { get; set; }
    public Guid CarrierTransmissionId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SubmissionMarketId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string? EventMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class CarrierInboundResponseDto
{
    public Guid CarrierInboundResponseId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid? CarrierTransmissionId { get; set; }
    public string SourceChannelCode { get; set; } = string.Empty;
    public string ResponseTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? CarrierReferenceNumber { get; set; }
    public DateTime ReceivedDateUtc { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public string? ProcessingError { get; set; }
}
