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
    public string? LatestQuoteRequestStatusCode { get; set; }
    public int LatestQuoteRequestVersion { get; set; }
    public DateTime? LatestQuoteRequestDateUtc { get; set; }
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
    public IReadOnlyList<CarrierTransmissionDto> Transmissions { get; set; } = [];
    public IReadOnlyList<CarrierInboundResponseDto> InboundResponses { get; set; } = [];
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
