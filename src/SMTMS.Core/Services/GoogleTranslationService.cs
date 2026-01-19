using SMTMS.Core.Interfaces;
using SMTMS.Core.Common;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SMTMS.Core.Services;

/// <summary>
/// Google 翻译服务实现
/// </summary>
public class GoogleTranslationService : ITranslationApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleTranslationService> _logger;
    private bool? _canConnectToGoogle;

    private const string GoogleApiUrlTemplate = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&ie=UTF-8&oe=UTF-8&q={2}";
    private const string FallbackApiUrlTemplate = "https://fanyi.sisyphean.top/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}";

    public event EventHandler<string>? OnStatusChanged;

    public GoogleTranslationService(ILogger<GoogleTranslationService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// 使用 Google 翻译 API 翻译文本，如果 Google 不可用则自动切换到备用源
    /// </summary>
    public async Task<string> TranslateAsync(string text, string targetLang, string sourceLang = Constants.Translation.DefaultSourceLang)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // 确定是否可以使用 Google
        bool useGoogle = await EnsureGoogleConnectionStatusAsync();

        Task<string> translationTask;

        if (useGoogle)
        {
            // 包装 Google 翻译任务，以便处理内部的异常并进行故障转移
            translationTask = TranslateWithGoogleAndFallbackIfNeededAsync(text, targetLang, sourceLang);
        }
        else
        {
            translationTask = TranslateWithFallbackAsync(text, targetLang, sourceLang);
        }

        // 增加 1 秒延迟提示逻辑
        var delayTask = Task.Delay(1000);
        var completedTask = await Task.WhenAny(translationTask, delayTask);

        if (completedTask == delayTask && !translationTask.IsCompleted)
        {
            // 如果 1 秒后任务还未完成，发送提示
             OnStatusChanged?.Invoke(this, "翻译请求处理中，请稍候...");
        }

        return await translationTask;
    }

    private async Task<string> TranslateWithGoogleAndFallbackIfNeededAsync(string text, string targetLang, string sourceLang)
    {
        try
        {
            return await TranslateWithGoogleAsync(text, targetLang, sourceLang);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[翻译] Google 翻译失败，尝试重置状态并使用备用源");
            OnStatusChanged?.Invoke(this, "Google 翻译无响应，正如西西弗斯推石上山... 正在切换至备用线路");
            
            // 如果在使用 Google 时报错，重置状态，并尝试使用备用源
            _canConnectToGoogle = false;
            return await TranslateWithFallbackAsync(text, targetLang, sourceLang);
        }
    }

    private async Task<bool> EnsureGoogleConnectionStatusAsync()
    {
        // 如果已有缓存结果，直接返回
        if (_canConnectToGoogle.HasValue)
        {
            return _canConnectToGoogle.Value;
        }

        // 进行连通性测试
        try
        {
            _logger.LogInformation("[翻译] 正在检测 Google 连通性...");
            // 只有在首次检测时，如果时间较长才提示，不过通常 TCP 连接超时是 1s，这里可以不提示，或者提示正在探测
            // OnStatusChanged?.Invoke(this, "正在探测 Google 连通性..."); 

            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync("translate.googleapis.com", 443);
            var timeoutTask = Task.Delay(1000); // 1秒超时

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            if (completedTask == connectTask && client.Connected)
            {
                _logger.LogInformation("[翻译] Google 连接成功");
                _canConnectToGoogle = true;
            }
            else
            {
                _logger.LogWarning("[翻译] Google 连接超时或失败");
                OnStatusChanged?.Invoke(this, "无法触达 Google，正在切换至备用翻译网络...");
                _canConnectToGoogle = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[翻译] Google 连接检测异常");
            OnStatusChanged?.Invoke(this, "网络连接异常，将使用备用翻译服务...");
            _canConnectToGoogle = false;
        }

        return _canConnectToGoogle.Value;
    }

    private async Task<string> TranslateWithGoogleAsync(string text, string targetLang, string sourceLang)
    {
        // 使用 Uri.EscapeDataString 进行 URL 编码
        var url = string.Format(GoogleApiUrlTemplate, Uri.EscapeDataString(sourceLang), Uri.EscapeDataString(targetLang), Uri.EscapeDataString(text));

        _logger.LogDebug("[翻译-Google] 请求 URL: {Url}", url.Substring(0, Math.Min(150, url.Length)));

        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode}): {errorContent}");
        }

        var json = await response.Content.ReadAsStringAsync();
        // Console.WriteLine($"[翻译-Google] 响应: {json.Substring(0, Math.Min(200, json.Length))}...");

        var doc = JsonDocument.Parse(json);

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
                    // Console.WriteLine($"[翻译-Google] 成功: {text} -> {translatedText}");
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
                // Console.WriteLine($"[翻译-Google] 成功: {text} -> {translatedText}");
                return translatedText;
            }
        }

        _logger.LogWarning("[翻译-Google] 警告: 无法解析响应，返回原文");
        return text;
    }

    private async Task<string> TranslateWithFallbackAsync(string text, string targetLang, string sourceLang)
    {
        try
        {
            var url = string.Format(FallbackApiUrlTemplate, Uri.EscapeDataString(sourceLang), Uri.EscapeDataString(targetLang), Uri.EscapeDataString(text));
            _logger.LogDebug("[翻译-Fallback] 请求 URL: {Url}", url.Substring(0, Math.Min(150, url.Length)));

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode}): {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            // Console.WriteLine($"[翻译-Fallback] 响应: {json.Substring(0, Math.Min(200, json.Length))}...");

            var doc = JsonDocument.Parse(json);
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
            throw; 
        }
    }
}
