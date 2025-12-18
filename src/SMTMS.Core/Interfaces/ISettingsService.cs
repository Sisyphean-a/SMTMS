using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

/// <summary>
/// 应用程序配置服务接口
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// 获取应用程序配置
    /// </summary>
    Task<AppSettings> GetSettingsAsync();

    /// <summary>
    /// 保存应用程序配置
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// 更新最后使用的Mods目录
    /// </summary>
    Task UpdateLastModsDirectoryAsync(string directory);
}
