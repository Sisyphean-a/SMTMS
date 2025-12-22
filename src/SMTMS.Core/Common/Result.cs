namespace SMTMS.Core.Common;

/// <summary>
/// 表示操作结果的基类
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    protected Result(bool isSuccess, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static Result Success() => new(true);
    
    public static Result Failure(string errorMessage) => new(false, errorMessage);
    
    public static Result Failure(Exception exception) => new(false, exception.Message, exception);
    
    public static Result Failure(string errorMessage, Exception exception) => new(false, errorMessage, exception);
}

/// <summary>
/// 表示带返回值的操作结果
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value = default, string? errorMessage = null, Exception? exception = null)
        : base(isSuccess, errorMessage, exception)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value);
    
    public new static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
    
    public new static Result<T> Failure(Exception exception) => new(false, default, exception.Message, exception);
    
    public new static Result<T> Failure(string errorMessage, Exception exception) => new(false, default, errorMessage, exception);
}

/// <summary>
/// 表示批量操作的结果
/// </summary>
public class BatchResult : Result
{
    public int SuccessCount { get; }
    public int FailureCount { get; }
    public List<string> Errors { get; }

    private BatchResult(bool isSuccess, int successCount, int failureCount, List<string> errors, string? errorMessage = null)
        : base(isSuccess, errorMessage)
    {
        SuccessCount = successCount;
        FailureCount = failureCount;
        Errors = errors;
    }

    public static BatchResult Success(int successCount) 
        => new(true, successCount, 0, new List<string>());
    
    public static BatchResult PartialSuccess(int successCount, int failureCount, List<string> errors)
        => new(successCount > 0, successCount, failureCount, errors, 
            $"部分成功: {successCount} 成功, {failureCount} 失败");
    
    public static BatchResult Failure(int failureCount, List<string> errors)
        => new(false, 0, failureCount, errors, $"全部失败: {failureCount} 个错误");
}

