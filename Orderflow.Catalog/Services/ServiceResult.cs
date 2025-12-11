namespace Orderflow.Catalog.Services;

public class ServiceResult
{
    public bool Succeeded { get; protected set; }
    public List<string> Errors { get; protected set; } = [];

    public static ServiceResult Success() => new() { Succeeded = true };
    public static ServiceResult Failure(params string[] errors) => new() { Succeeded = false, Errors = [.. errors] };
    public static ServiceResult Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors.ToList() };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }
    public int TotalCount { get; private set; }

    public static ServiceResult<T> Success(T data, int totalCount = 0) => new()
    {
        Succeeded = true,
        Data = data,
        TotalCount = totalCount
    };

    public new static ServiceResult<T> Failure(params string[] errors) => new()
    {
        Succeeded = false,
        Errors = [.. errors]
    };

    public new static ServiceResult<T> Failure(IEnumerable<string> errors) => new()
    {
        Succeeded = false,
        Errors = errors.ToList()
    };
}