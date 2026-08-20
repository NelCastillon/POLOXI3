namespace Ams.Web.Components.Shared;

public sealed class RowDeselectEventArgs<TValue>
{
    public TValue Data { get; init; } = default!;
}
