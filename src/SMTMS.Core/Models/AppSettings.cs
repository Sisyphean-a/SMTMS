using System.ComponentModel.DataAnnotations;
using SMTMS.Core.Common;

namespace SMTMS.Core.Models;

/// <summary>
/// 应用程序配置设置模型
/// </summary>
public class AppSettings
{
    [Key]
    public int Id { get; set; } = 1;

    /// <summary>
    /// 用户最后选择的Mods目录路径
    /// </summary>
    public string? LastModsDirectory { get; set; }

    /// <summary>
    /// 窗口宽度
    /// </summary>
    public int WindowWidth { get; set; } = Constants.UI.DefaultWindowWidth;

    /// <summary>
    /// 窗口高度
    /// </summary>
    public int WindowHeight { get; set; } = Constants.UI.DefaultWindowHeight;

    /// <summary>
    /// 启动时自动扫描模组
    /// </summary>
    public bool AutoScanOnStartup { get; set; } = true;

    /// <summary>
    /// 是否启用黑夜模式
    /// </summary>
    public bool IsDarkMode { get; set; } = false;

    /// <summary>
    /// 翻译 API 类型（Google, DeepL 等）
    /// </summary>
    public string TranslationApiType { get; set; } = Constants.Translation.ProviderGoogle;

    /// <summary>
    /// 翻译源语言（auto 为自动检测）
    /// </summary>
    public string TranslationSourceLang { get; set; } = Constants.Translation.DefaultSourceLang;

    /// <summary>
    /// 翻译目标语言
    /// </summary>
    public string TranslationTargetLang { get; set; } = Constants.Translation.DefaultTargetLang;
}
