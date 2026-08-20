namespace Ams.Knowledge.Application.Common.Models;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int PageNumber, int PageSize);
