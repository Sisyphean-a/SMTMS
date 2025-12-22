using SMTMS.Core.Models;

namespace SMTMS.UI.Messages;

/// <summary>
/// Git 历史加载完成消息
/// </summary>
public class HistoryLoadedMessage(IReadOnlyList<GitCommitModel> commits)
{
    public IReadOnlyList<GitCommitModel> Commits { get; } = commits;
}

