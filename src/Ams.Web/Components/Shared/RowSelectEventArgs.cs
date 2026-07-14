namespace Ams.Web.Components.Shared;

public sealed class RowSelectEventArgs<TValue>
{
    public TValue Data { get; init; } = default!;
}
