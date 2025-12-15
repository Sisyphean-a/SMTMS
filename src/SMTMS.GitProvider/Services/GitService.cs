using LibGit2Sharp;
using SMTMS.Core.Interfaces;

namespace SMTMS.GitProvider.Services;

public class GitService : IGitService
{
    public bool IsRepository(string path)
    {
        return Repository.IsValid(path);
    }

    public void Init(string path)
    {
        Repository.Init(path);
    }

    public void StageAll(string path)
    {
        using var repo = new Repository(path);
        Commands.Stage(repo, "*");
    }

    public void Commit(string path, string message, string authorName, string authorEmail)
    {
        using var repo = new Repository(path);
        var signature = new Signature(authorName, authorEmail, DateTimeOffset.Now);
        repo.Commit(message, signature, signature);
    }
}
