namespace Ams.Web.Components.Shared;

public sealed class RecordDoubleClickEventArgs<TValue>
{
    public TValue RowData { get; init; } = default!;
}
