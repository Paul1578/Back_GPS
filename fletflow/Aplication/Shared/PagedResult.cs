namespace fletflow.Application.Shared;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required int TotalPages { get; init; }
}

public static class PaginationLimits
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public static bool IsValid(int page, int pageSize) =>
        page >= 1 && pageSize >= 1 && pageSize <= MaximumPageSize;

    public static int GetOffset(int page, int pageSize) =>
        (int)Math.Min(((long)page - 1) * pageSize, int.MaxValue);
}
