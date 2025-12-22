namespace SMTMS.Core.Common;

/// <summary>
/// 表示带详细信息的操作结果（用于翻译、同步等复杂操作）
/// </summary>
public class OperationResult
{
    public bool IsSuccess { get; }
    public int SuccessCount { get; }
    public int ErrorCount { get; }
    public string Message { get; }
    public List<string> Details { get; }

    private OperationResult(bool isSuccess, int successCount, int errorCount, string message, List<string>? details = null)
    {
        IsSuccess = isSuccess;
        SuccessCount = successCount;
        ErrorCount = errorCount;
        Message = message;
        Details = details ?? new List<string>();
    }

    public static OperationResult Success(int count, string message)
        => new(true, count, 0, message);

    public static OperationResult PartialSuccess(int successCount, int errorCount, string message, List<string>? details = null)
        => new(successCount > 0, successCount, errorCount, message, details);

    public static OperationResult Failure(string message, List<string>? details = null)
        => new(false, 0, 0, message, details);

    public static OperationResult Failure(int errorCount, string message, List<string>? details = null)
        => new(false, 0, errorCount, message, details);
}

