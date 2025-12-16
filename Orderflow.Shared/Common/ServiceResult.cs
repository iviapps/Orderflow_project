namespace Orderflow.Shared.Common;

/// <summary>
/// Base service result for operations without return data
/// </summary>
public class ServiceResult
{
    /// <summary>
    /// Indicates whether the operation succeeded
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Success or informational message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error messages (empty if succeeded)
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static ServiceResult Success(string message = "")
    {
        return new ServiceResult
        {
            Succeeded = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed result with error messages
    /// </summary>
    public static ServiceResult Failure(IEnumerable<string> errors)
    {
        return new ServiceResult
        {
            Succeeded = false,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a failed result with a single error message
    /// </summary>
    public static ServiceResult Failure(string error)
    {
        return new ServiceResult
        {
            Succeeded = false,
            Errors = new[] { error }
        };
    }
}

/// <summary>
/// Service result for operations that return data
/// </summary>
/// <typeparam name="T">Type of data returned</typeparam>
public class ServiceResult<T> : ServiceResult
{
    /// <summary>
    /// Result data (null if failed)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Creates a successful result with data
    /// </summary>
    public static ServiceResult<T> Success(T data, string message = "")
    {
        return new ServiceResult<T>
        {
            Succeeded = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed result with error messages
    /// </summary>
    public new static ServiceResult<T> Failure(IEnumerable<string> errors)
    {
        return new ServiceResult<T>
        {
            Succeeded = false,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a failed result with a single error message
    /// </summary>
    public new static ServiceResult<T> Failure(string error)
    {
        return new ServiceResult<T>
        {
            Succeeded = false,
            Errors = new[] { error }
        };
    }
}
