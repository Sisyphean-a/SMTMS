using SMTMS.Core.Interfaces;
using SMTMS.Core.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace SMTMS.Core.Services;

/// <summary>
/// Google 翻译服务实现
/// </summary>
public class GoogleTranslationService : ITranslationApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleTranslationService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const string GoogleApiUrlTemplate = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&ie=UTF-8&oe=UTF-8&q={2}";
    private const string FallbackApiUrlTemplate = "https://fanyi.sisyphean.top/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";

    public event EventHandler<string>? OnStatusChanged;

    public GoogleTranslationService(ILogger<GoogleTranslationService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        // 初始化重试策略：由于网络问题导致的 HttpRequestException 进行重试
        // 重试3次，采用指数退避算法 (2^n 秒)
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "[翻译] Google API 请求失败 (第 {RetryCount} 次重试)，将在 {Delay} 秒后重试...", retryCount, timeSpan.TotalSeconds);
                });
    }

    /// <summary>
    /// 使用 Google 翻译 API 翻译文本，如果 Google 不可用则自动切换到备用源
    /// </summary>
    public async Task<string> TranslateAsync(string text, string targetLang, string sourceLang = Constants.Translation.DefaultSourceLang)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // 定义降级策略：如果 Google 翻译失败（重试耗尽或其他异常），则切换到备用源
        // 注意：FallbackPolicy 需要捕获当前的 text, targetLang, sourceLang 参数，因此在方法内部定义
        var fallbackPolicy = Policy<string>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackAction: async (ct) => await TranslateWithFallbackAsync(text, targetLang, sourceLang),
                onFallbackAsync: async (outcome) =>
                {
                    if (outcome.Exception != null)
                    {
                        _logger.LogWarning(outcome.Exception, "[翻译] Google 翻译服务不可用，正在切换至备用线路...");
                    }
                    else
                    {
                         _logger.LogWarning("[翻译] Google 翻译服务不可用，正在切换至备用线路...");
                    }
                    OnStatusChanged?.Invoke(this, "Google 翻译无响应，正如西西弗斯推石上山... 正在切换至备用线路");
                    await Task.CompletedTask;
                });

        // 组合策略：Fallback 包裹 Retry (如果 Retry 全部失败，则触发 Fallback)
        var resilienceStrategy = fallbackPolicy.WrapAsync(_retryPolicy);

        // 增加 1 秒延迟提示逻辑 (保留原有 UX 设计)
        var delayTask = Task.Delay(1000);
        var translationTask = resilienceStrategy.ExecuteAsync(async () => await TranslateWithGoogleAsync(text, targetLang, sourceLang));
        
        var completedTask = await Task.WhenAny(translationTask, delayTask);

        if (completedTask == delayTask && !translationTask.IsCompleted)
        {
            // 如果 1 秒后任务还未完成，发送提示
             OnStatusChanged?.Invoke(this, "翻译请求处理中，请稍候...");
        }

        return await translationTask;
    }

    private async Task<string> TranslateWithGoogleAsync(string text, string targetLang, string sourceLang)
    {
        // 使用 Uri.EscapeDataString 进行 URL 编码
        var url = string.Format(GoogleApiUrlTemplate, Uri.EscapeDataString(sourceLang), Uri.EscapeDataString(targetLang), Uri.EscapeDataString(text));

        _logger.LogDebug("[翻译-Google] 请求 URL: {Url}", url.Substring(0, Math.Min(150, url.Length)));

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(json);

        // 解析 Google 翻译 API 返回的 JSON (数组格式或对象格式)
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            var firstElement = doc.RootElement[0];
            if (firstElement.ValueKind == JsonValueKind.Array)
            {
                var translatedText = string.Empty;
                foreach (var item in firstElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                    {
                        translatedText += item[0].GetString();
                    }
                }
                if (!string.IsNullOrEmpty(translatedText))
                {
                    return translatedText;
                }
            }
        }

        if (doc.RootElement.TryGetProperty("sentences", out var sentences))
        {
            var translatedText = string.Empty;
            foreach (var sentence in sentences.EnumerateArray())
            {
                if (sentence.TryGetProperty("trans", out var trans))
                {
                    translatedText += trans.GetString();
                }
            }
            if (!string.IsNullOrEmpty(translatedText))
            {
                return translatedText;
            }
        }

        _logger.LogWarning("[翻译-Google] 警告: 无法解析响应，抛出异常以触发重试或降级");
        throw new Exception("Google API 响应解析失败 (无法找到翻译文本)");
    }

    private async Task<string> TranslateWithFallbackAsync(string text, string targetLang, string sourceLang)
    {
        try
        {
            var url = string.Format(FallbackApiUrlTemplate, Uri.EscapeDataString(sourceLang), Uri.EscapeDataString(targetLang), Uri.EscapeDataString(text));
            _logger.LogDebug("[翻译-Fallback] 请求 URL: {Url}", url.Substring(0, Math.Min(150, url.Length)));

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("translation", out var translationElement))
            {
                var translatedText = translationElement.GetString();
                if (!string.IsNullOrEmpty(translatedText))
                {
                    _logger.LogInformation("[翻译-Fallback] 成功: {Text} -> {TranslatedText}", text, translatedText);
                    return translatedText;
                }
            }
            
            _logger.LogWarning("[翻译-Fallback] 警告: 无法解析响应，返回原文");
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[翻译-Fallback] 错误");
            // 如果备用源也挂了，只能返回原文了
            return text; 
        }
    }
}
