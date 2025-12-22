using SMTMS.Core.Models;

namespace SMTMS.UI.Messages;

/// <summary>
/// Git 历史加载完成消息
/// </summary>
public class HistoryLoadedMessage
{
    public IReadOnlyList<GitCommitModel> Commits { get; }

    public HistoryLoadedMessage(IReadOnlyList<GitCommitModel> commits)
    {
        Commits = commits;
    }
}

