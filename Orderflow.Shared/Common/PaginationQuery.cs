namespace Orderflow.Shared.Common;

/// <summary>
/// Base pagination query parameters
/// </summary>
public record PaginationQuery
{
    /// <summary>
    /// Page number (starts at 1)
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size (items per page, max 100)
    /// </summary>
    public int PageSize { get; init; } = 10;
}
