using SMTMS.Core.Models;

namespace SMTMS.Core.Services;

public interface IDiffService
{
    /// <summary>
    /// 对比两段文本，返回差异模型（用于 UI 显示差异）
    /// </summary>
    /// <param name="oldText">旧文本</param>
    /// <param name="newText">新文本</param>
    /// <returns>差异模型</returns>
    ModDiffModel Compare(string? oldText, string? newText);
}
