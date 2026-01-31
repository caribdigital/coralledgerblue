namespace CoralLedger.Blue.Application.Common.Models;

/// <summary>
/// Represents the result of a service operation, distinguishing between success, failure, and empty results.
/// This pattern helps avoid silent failures where callers cannot distinguish between "no data" and "error occurred".
/// </summary>
/// <typeparam name="T">The type of data returned on success</typeparam>
public record ServiceResult<T>
{
    /// <summary>
    /// Indicates whether the operation completed successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The result value (may be null even on success if no data was found)
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception details if available (optional, for logging/debugging)
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates a successful result with a value
    /// </summary>
    public static ServiceResult<T> Ok(T value) => new()
    {
        Success = true,
        Value = value
    };

    /// <summary>
    /// Creates a successful result with no value (e.g., no data found, but not an error)
    /// </summary>
    public static ServiceResult<T> OkEmpty() => new()
    {
        Success = true,
        Value = default
    };

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static ServiceResult<T> Fail(string errorMessage, Exception? exception = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };

    /// <summary>
    /// Helper to check if the result has a value
    /// </summary>
    public bool HasValue => Success && Value is not null;
}
