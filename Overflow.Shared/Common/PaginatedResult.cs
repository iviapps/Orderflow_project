using Orderflow.Shared.Common;

namespace Orderflow.Shared.Common;

/// <summary>
/// Result type for paginated service operations
/// </summary>
/// <typeparam name="T">Type of items in the collection</typeparam>
public class PaginatedResult<T>
{
    /// <summary>
    /// Collection of items for the current page
    /// </summary>
    public IEnumerable<T> Data { get; set; } = new List<T>();

    /// <summary>
    /// Pagination metadata
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new()
    {
        Page = 1,
        PageSize = 10,
        TotalCount = 0,
        TotalPages = 0
    };

    /// <summary>
    /// Creates a paginated result
    /// </summary>
    public static PaginatedResult<T> Create(
        IEnumerable<T> data,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<T>
        {
            Data = data,
            Pagination = new PaginationMetadata
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            }
        };
    }
}
