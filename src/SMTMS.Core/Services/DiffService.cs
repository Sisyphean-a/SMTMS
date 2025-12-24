using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using SMTMS.Core.Models;

namespace SMTMS.Core.Services;

public class DiffService : IDiffService
{
    private readonly ISideBySideDiffBuilder _diffBuilder;

    public DiffService()
    {
        _diffBuilder = new SideBySideDiffBuilder(new Differ());
    }

    public ModDiffModel Compare(string? oldText, string? newText)
    {
        var model = new ModDiffModel();
        
        // 简单的变更统计，如果需要详细的 SideBySide Model，可以在这里构建
        // 但 ModDiffModel 目前是为 Git 设计的摘要模型。
        // 我们这里复用 ModDiffModel 来携带 "ChangeSummary" 等信息
        // 实际上前端 UI 可能直接需要 SideBySideDiffModel。
        
        // 这里我们先做一个适配：
        // 如果文本不一样，计算基本的变更类型
        
        oldText ??= string.Empty;
        newText ??= string.Empty;
        
        if (oldText == newText)
        {
            model.ChangeCount = 0;
            return model;
        }

        var diff = _diffBuilder.BuildDiffModel(oldText, newText);
        
        // 统计变更行数
        int changes = 0;
        foreach (var line in diff.NewText.Lines)
        {
            if (line.Type == ChangeType.Inserted || line.Type == ChangeType.Modified)
                changes++;
        }
         foreach (var line in diff.OldText.Lines)
        {
             if (line.Type == ChangeType.Deleted) // 只是粗略统计
                changes++;
        }

        model.ChangeCount = changes;
        model.ChangeType = changes > 0 ? "Modified" : "Unchanged";
        
        // 注意：这里没有填充 FieldChange (Name/Description)，因为 DiffPlex 是纯文本对比
        // 如果需要语义对比（JSON 字段），需要解析 JSON 并对比对象。
        // 暂时只提供文本级 Diff 统计。
        
        return model;
    }
}
