namespace SMTMS.Core.Models;

/// <summary>
/// 表示单个模组在某次提交中的变更详情
/// </summary>
public class ModDiffModel
{
    /// <summary>
    /// 模组的唯一标识符
    /// </summary>
    public string UniqueID { get; set; } = string.Empty;

    /// <summary>
    /// 模组名称（使用新值或旧值，优先新值）
    /// </summary>
    public string ModName { get; set; } = string.Empty;

    /// <summary>
    /// 相对路径（如 "Mods/ModFolder/manifest.json"）
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// 文件夹名称（从路径中提取）
    /// </summary>
    public string FolderName { get; set; } = string.Empty;

    /// <summary>
    /// 变更类型（Added, Modified, Deleted）
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// 变更的字段数量
    /// </summary>
    public int ChangeCount { get; set; }

    /// <summary>
    /// 名称变更
    /// </summary>
    public FieldChange? NameChange { get; set; }

    /// <summary>
    /// 描述变更
    /// </summary>
    public FieldChange? DescriptionChange { get; set; }

    /// <summary>
    /// 作者变更
    /// </summary>
    public FieldChange? AuthorChange { get; set; }

    /// <summary>
    /// 版本变更
    /// </summary>
    public FieldChange? VersionChange { get; set; }

    /// <summary>
    /// 是否有任何变更
    /// </summary>
    public bool HasChanges => ChangeCount > 0;

    /// <summary>
    /// 变更摘要（用于列表显示）
    /// </summary>
    public string ChangeSummary
    {
        get
        {
            if (ChangeCount == 0) return "无变更";
            var changes = new List<string>();
            if (NameChange?.HasChange == true) changes.Add("名称");
            if (DescriptionChange?.HasChange == true) changes.Add("描述");
            if (AuthorChange?.HasChange == true) changes.Add("作者");
            if (VersionChange?.HasChange == true) changes.Add("版本");
            return $"{ChangeCount} 项变更: {string.Join(", ", changes)}";
        }
    }

    /// <summary>
    /// 图标（根据变更类型）
    /// </summary>
    public string Icon
    {
        get
        {
            return ChangeType switch
            {
                "Added" => "➕",
                "Deleted" => "❌",
                "Modified" => "📝",
                _ => "📄"
            };
        }
    }
}

/// <summary>
/// 表示单个字段的变更
/// </summary>
public class FieldChange
{
    /// <summary>
    /// 字段名称
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 旧值
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// 新值
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// 是否有变更
    /// </summary>
    public bool HasChange => OldValue != NewValue;

    /// <summary>
    /// 变更摘要（用于简短显示）
    /// </summary>
    public string ChangeSummary
    {
        get
        {
            if (!HasChange) return "无变更";
            var oldPreview = OldValue?.Length > 30 ? OldValue.Substring(0, 30) + "..." : OldValue ?? "(空)";
            var newPreview = NewValue?.Length > 30 ? NewValue.Substring(0, 30) + "..." : NewValue ?? "(空)";
            return $"{oldPreview} → {newPreview}";
        }
    }
}

