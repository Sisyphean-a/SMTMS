namespace SMTMS.Core.Interfaces;

/// <summary>
/// 翻译 API 服务接口
/// </summary>
public interface ITranslationApiService
{
    /// <summary>
    /// 翻译文本
    /// </summary>
    /// <param name="text">要翻译的文本</param>
    /// <param name="targetLang">目标语言</param>
    /// <param name="sourceLang">源语言（默认为 auto 自动检测）</param>
    /// <returns>翻译后的文本</returns>
    Task<string> TranslateAsync(string text, string targetLang, string sourceLang = "auto");
}
