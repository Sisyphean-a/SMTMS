using LibGit2Sharp;
using SMTMS.Core.Interfaces;

using SMTMS.Core.Aspects;

namespace SMTMS.GitProvider.Services;

[Log]
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

    public string Clone(string sourceUrl, string destinationPath)
    {
        return Repository.Clone(sourceUrl, destinationPath);
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

    public void Push(string path, string username, string password)
    {
        using var repo = new Repository(path);
        var remote = repo.Network.Remotes["origin"];
        if (remote == null)
        {
            throw new Exception("Remote 'origin' not found.");
        }

        var options = new PushOptions
        {
            CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials
            {
                Username = username,
                Password = password
            }
        };
        repo.Network.Push(remote, repo.Head.CanonicalName, options);
    }

    public void Pull(string path, string authorName, string authorEmail)
    {
        using var repo = new Repository(path);
        var signature = new Signature(authorName, authorEmail, DateTimeOffset.Now);
        var options = new PullOptions(); // Default options
        Commands.Pull(repo, signature, options);
    }

    public void Checkout(string path, string branchName)
    {
        using var repo = new Repository(path);
        var branch = repo.Branches[branchName];
        if (branch != null)
        {
            Commands.Checkout(repo, branch);
        }
        else
        {
             throw new Exception($"Branch '{branchName}' not found.");
        }
    }

    public IEnumerable<string> GetRemotes(string path)
    {
        using var repo = new Repository(path);
        return repo.Network.Remotes.Select(r => r.Name).ToList();
    }

    public void AddRemote(string path, string name, string url)
    {
        using var repo = new Repository(path);
        repo.Network.Remotes.Add(name, url);
    }

    public IEnumerable<string> GetStatus(string path)
    {
        using var repo = new Repository(path);
        var status = repo.RetrieveStatus();
        return status.Where(s => s.State != FileStatus.Ignored).Select(s => s.FilePath).ToList();
    }
}
