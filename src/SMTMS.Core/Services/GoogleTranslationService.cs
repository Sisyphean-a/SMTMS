using SMTMS.Core.Interfaces;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace SMTMS.Core.Services;

/// <summary>
/// Google 翻译服务实现
/// </summary>
public class GoogleTranslationService : ITranslationApiService
{
    private readonly HttpClient _httpClient;

    public GoogleTranslationService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// 使用 Google 翻译 API 翻译文本
    /// </summary>
    public async Task<string> TranslateAsync(string text, string targetLang, string sourceLang = "auto")
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        try
        {
            // 使用 Uri.EscapeDataString 进行 URL 编码
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={Uri.EscapeDataString(sourceLang)}&tl={Uri.EscapeDataString(targetLang)}&dt=t&ie=UTF-8&oe=UTF-8&q={Uri.EscapeDataString(text)}";

            Console.WriteLine($"[翻译] 请求 URL: {url.Substring(0, Math.Min(150, url.Length))}...");

            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode}): {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[翻译] 响应: {json.Substring(0, Math.Min(200, json.Length))}...");

            var doc = JsonDocument.Parse(json);

            // 解析 Google 翻译 API 返回的 JSON
            // 格式: [[["翻译结果","原文",null,null,3]],null,"en",null,null,null,null,[]]
            // 或: {"sentences":[{"trans":"翻译结果","orig":"原文",...},...]}
            
            // 尝试新格式（数组格式）
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
                        Console.WriteLine($"[翻译] 成功: {text} -> {translatedText}");
                        return translatedText;
                    }
                }
            }

            // 尝试旧格式（对象格式）
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
                    Console.WriteLine($"[翻译] 成功: {text} -> {translatedText}");
                    return translatedText;
                }
            }

            Console.WriteLine($"[翻译] 警告: 无法解析响应，返回原文");
            return text;
        }
        catch (Exception ex)
        {
            // 翻译失败时返回原文并记录详细错误
            Console.WriteLine($"[翻译] 错误: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[翻译] 内部错误: {ex.InnerException.Message}");
            }
            throw; // 重新抛出异常，让调用者处理
        }
    }
}
