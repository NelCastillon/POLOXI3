using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ams.Application.Features.DocumentIntake;

public static class DocumentIntakeTelemetry
{
    public const string SourceName="AgencyBinder.DocumentIntake";
    public static readonly ActivitySource ActivitySource=new(SourceName,"1.0.0");
    public static readonly Meter Meter=new(SourceName,"1.0.0");
    public static readonly Counter<long> WorkCompleted=Meter.CreateCounter<long>("document_intake.work.completed",unit:"{item}");
    public static readonly Counter<long> WorkRetried=Meter.CreateCounter<long>("document_intake.work.retried",unit:"{item}");
    public static readonly Counter<long> WorkFailed=Meter.CreateCounter<long>("document_intake.work.failed",unit:"{item}");
    public static readonly Counter<long> ProviderRequests=Meter.CreateCounter<long>("document_intake.provider.requests",unit:"{request}");
    public static readonly Counter<long> ProviderErrors=Meter.CreateCounter<long>("document_intake.provider.errors",unit:"{error}");
    public static readonly Histogram<double> WorkDuration=Meter.CreateHistogram<double>("document_intake.work.duration",unit:"ms");
    public static readonly Histogram<double> ProviderDuration=Meter.CreateHistogram<double>("document_intake.provider.duration",unit:"ms");
    public static readonly UpDownCounter<long> ActiveWork=Meter.CreateUpDownCounter<long>("document_intake.work.active",unit:"{item}");
    public static readonly Histogram<long> QueueDepth=Meter.CreateHistogram<long>("document_intake.queue.depth",unit:"{item}");
    public static readonly Histogram<long> OldestQueuedAge=Meter.CreateHistogram<long>("document_intake.queue.oldest_age",unit:"s");
    public static readonly Histogram<long> DeadLetterDepth=Meter.CreateHistogram<long>("document_intake.dead_letter.depth",unit:"{item}");

    public static TagList Tags(string? module,string? workType,string? provider=null)
    {
        var tags=new TagList{{"document_intake.module",module},{"document_intake.work_type",workType}};
        if(!string.IsNullOrWhiteSpace(provider))tags.Add("document_intake.provider",provider);
        return tags;
    }
}
