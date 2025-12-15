using Orderflow.Shared.Common;

namespace Orderflow.Identity.Dtos.Common;

/// <summary>
/// Generic paginated response wrapper for collections
/// </summary>
/// <typeparam name="T">Type of items in the collection</typeparam>
public record PaginatedResponse<T>
{
    /// <summary>
    /// Page data (collection of items)
    /// </summary>
    public required IEnumerable<T> Data { get; init; }

    /// <summary>
    /// Pagination metadata (page info, counts)
    /// </summary>
    public required PaginationMetadata Pagination { get; init; }
}
