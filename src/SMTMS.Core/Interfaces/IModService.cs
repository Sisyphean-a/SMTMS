using SMTMS.Core.Models;

namespace SMTMS.Core.Interfaces;

public interface IModService
{
    /// <summary>
    /// 扫描目录中的 Mod 并返回有效的清单列表。
    /// </summary>
    Task<IEnumerable<ModManifest>> ScanModsAsync(string modsDirectory);

    /// <summary>
    /// 读取单个清单文件。
    /// </summary>
    Task<ModManifest?> ReadManifestAsync(string manifestPath);

    /// <summary>
    /// 将更改保存到清单文件。
    /// </summary>
    Task WriteManifestAsync(string manifestPath, ModManifest manifest);

    /// <summary>
    /// 更新清单中的特定字段，确保保留格式注释。
    /// </summary>
    Task UpdateModManifestAsync(string manifestPath, string? newName, string? newDescription);
}
