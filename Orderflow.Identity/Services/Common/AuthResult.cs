namespace Orderflow.Identity.Services.Common;

/// <summary>
/// Result type for authentication operations
/// </summary>
/// <typeparam name="T">Type of authentication response data</typeparam>
public class AuthResult<T>
{
    /// <summary>
    /// Indicates whether authentication succeeded
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Authentication response data (null if failed)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Error messages (empty if succeeded)
    /// </summary>
    public IEnumerable<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Creates a successful authentication result
    /// </summary>
    public static AuthResult<T> Success(T data)
    {
        return new AuthResult<T>
        {
            Succeeded = true,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed authentication result
    /// </summary>
    public static AuthResult<T> Failure(IEnumerable<string> errors)
    {
        return new AuthResult<T>
        {
            Succeeded = false,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a failed authentication result with a single error
    /// </summary>
    public static AuthResult<T> Failure(string error)
    {
        return new AuthResult<T>
        {
            Succeeded = false,
            Errors = new[] { error }
        };
    }
}
