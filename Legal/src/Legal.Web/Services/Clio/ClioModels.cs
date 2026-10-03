namespace Legal.Web.Services.Clio;

/// <summary>Minimal matter metadata returned from Clio (read-only).</summary>
public sealed record ClioMatter(
    long Id,
    string DisplayNumber,
    string? Description,
    string? Status,
    string? PracticeArea,
    string? ClientName,
    long? ClientId = null)
{
    /// <summary>Custom-field values read from the matter (read-only).</summary>
    public IReadOnlyList<ClioCustomFieldValue> CustomFields { get; init; } = [];

    /// <summary>The Clio matter stage name (e.g. "Litigation"), read-only context only.</summary>
    public string? StageName { get; init; }

    /// <summary>The Clio matter stage id, when available.</summary>
    public long? StageId { get; init; }
}

/// <summary>A single Clio matter custom-field value (read-only).</summary>
public sealed record ClioCustomFieldValue(
    string Name,
    string? FieldType,
    string? Value);

/// <summary>Minimal contact metadata related to a matter (read-only).</summary>
public sealed record ClioContact(
    long Id,
    string? Name,
    string? Type,
    string? Relationship);

/// <summary>Minimal document metadata (no content) for a matter (read-only).</summary>
public sealed record ClioDocumentInfo(
    long Id,
    string? Name,
    string? Category,
    string? Folder,
    string? LatestVersion);

/// <summary>Aggregated Sapini discovery result for the integration page.</summary>
public sealed record ClioSapiniDiscovery(
    ClioMatter Matter,
    IReadOnlyList<ClioContact> Contacts,
    IReadOnlyList<ClioDocumentInfo> Documents,
    IReadOnlyList<ClioCustomFieldValue> CustomFields)
{
    /// <summary>
    /// Dated activity events (tasks, calendar entries, communications) read from
    /// Clio for the matter. Read-only context used to enrich the unified timeline.
    /// </summary>
    public IReadOnlyList<ClioActivityEvent> Activities { get; init; } = [];
}

/// <summary>
/// A normalized, dated Clio activity (read-only) used to build the case timeline.
/// Kind is one of Task, Calendar, Communication.
/// </summary>
public sealed record ClioActivityEvent(
    string Kind,
    long Id,
    string? Title,
    DateTimeOffset? OccurredAt,
    string? Detail,
    bool? Completed);

/// <summary>Raw document bytes retrieved from Clio (read-only download).</summary>
public sealed record ClioDocumentContent(byte[] Content, string? ContentType);
