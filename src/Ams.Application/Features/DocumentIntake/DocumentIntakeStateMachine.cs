namespace Ams.Application.Features.DocumentIntake;

public static class DocumentIntakeStateMachine
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> SessionTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DocumentIntakeStatuses.Draft] = Set(DocumentIntakeStatuses.Queued, DocumentIntakeStatuses.Cancelled),
            [DocumentIntakeStatuses.Queued] = Set(DocumentIntakeStatuses.Processing, DocumentIntakeStatuses.Cancelled, DocumentIntakeStatuses.Failed),
            [DocumentIntakeStatuses.Processing] = Set(DocumentIntakeStatuses.ReviewRequired, DocumentIntakeStatuses.Ready, DocumentIntakeStatuses.Failed, DocumentIntakeStatuses.Cancelled),
            [DocumentIntakeStatuses.ReviewRequired] = Set(DocumentIntakeStatuses.Ready, DocumentIntakeStatuses.Queued, DocumentIntakeStatuses.Cancelled),
            [DocumentIntakeStatuses.Ready] = Set(DocumentIntakeStatuses.Completed, DocumentIntakeStatuses.Queued, DocumentIntakeStatuses.Cancelled),
            [DocumentIntakeStatuses.Failed] = Set(DocumentIntakeStatuses.Queued, DocumentIntakeStatuses.Cancelled),
            [DocumentIntakeStatuses.Completed] = Set(),
            [DocumentIntakeStatuses.Cancelled] = Set()
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> WorkTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [DocumentIntakeWorkStatuses.Pending] = Set(DocumentIntakeWorkStatuses.Processing, DocumentIntakeWorkStatuses.Cancelled),
            [DocumentIntakeWorkStatuses.Processing] = Set(DocumentIntakeWorkStatuses.Completed, DocumentIntakeWorkStatuses.RetryScheduled, DocumentIntakeWorkStatuses.Failed, DocumentIntakeWorkStatuses.DeadLettered),
            [DocumentIntakeWorkStatuses.RetryScheduled] = Set(DocumentIntakeWorkStatuses.Processing, DocumentIntakeWorkStatuses.Cancelled, DocumentIntakeWorkStatuses.DeadLettered),
            [DocumentIntakeWorkStatuses.Failed] = Set(DocumentIntakeWorkStatuses.RetryScheduled, DocumentIntakeWorkStatuses.DeadLettered, DocumentIntakeWorkStatuses.Cancelled),
            [DocumentIntakeWorkStatuses.Completed] = Set(),
            [DocumentIntakeWorkStatuses.DeadLettered] = Set(DocumentIntakeWorkStatuses.RetryScheduled, DocumentIntakeWorkStatuses.Cancelled),
            [DocumentIntakeWorkStatuses.Cancelled] = Set()
        };

    public static bool CanTransitionSession(string currentStatus, string nextStatus)
        => SessionTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(nextStatus);

    public static bool CanTransitionWorkItem(string currentStatus, string nextStatus)
        => WorkTransitions.TryGetValue(currentStatus, out var allowed) && allowed.Contains(nextStatus);

    public static void EnsureSessionTransition(string currentStatus, string nextStatus)
    {
        if (!CanTransitionSession(currentStatus, nextStatus))
            throw new InvalidOperationException($"Document intake session cannot transition from '{currentStatus}' to '{nextStatus}'.");
    }

    public static void EnsureWorkItemTransition(string currentStatus, string nextStatus)
    {
        if (!CanTransitionWorkItem(currentStatus, nextStatus))
            throw new InvalidOperationException($"Document intake work item cannot transition from '{currentStatus}' to '{nextStatus}'.");
    }

    public static TimeSpan GetRetryDelay(int attemptNumber) => attemptNumber switch
    {
        <= 1 => TimeSpan.FromSeconds(30),
        2 => TimeSpan.FromMinutes(2),
        3 => TimeSpan.FromMinutes(10),
        4 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(2)
    };

    private static IReadOnlySet<string> Set(params string[] values)
        => new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}
