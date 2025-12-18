using System.ComponentModel.DataAnnotations;

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
    public int WindowWidth { get; set; } = 1200;

    /// <summary>
    /// 窗口高度
    /// </summary>
    public int WindowHeight { get; set; } = 800;

    /// <summary>
    /// 启动时自动扫描模组
    /// </summary>
    public bool AutoScanOnStartup { get; set; } = true;
}
