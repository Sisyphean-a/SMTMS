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

    public void CommitAll(string path, string message)
    {
        using var repo = new Repository(path);
        
        // Auto-stage everything
        Commands.Stage(repo, "*");

        // Use a default signature for auto-commits
        var signature = new Signature("SMTMS Auto", "auto@smtms.local", DateTimeOffset.Now);
        repo.Commit(message, signature, signature);
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

    public IEnumerable<string> GetStatus(string path)
    {
        using var repo = new Repository(path);
        var status = repo.RetrieveStatus();
        return status.Where(s => s.State != FileStatus.Ignored).Select(s => s.FilePath).ToList();
    }

    public IEnumerable<SMTMS.Core.Models.GitCommitModel> GetHistory(string path)
    {
        using var repo = new Repository(path);
        return repo.Commits.Take(50).Select(c => new SMTMS.Core.Models.GitCommitModel
        {
            ShortHash = c.Sha.Substring(0, 7),
            FullHash = c.Sha,
            Message = c.MessageShort,
            Author = c.Author.Name,
            Date = c.Author.When
        }).ToList();
    }

    public void Reset(string path, string commitHash)
    {
        using var repo = new Repository(path);
        var commit = repo.Lookup<Commit>(commitHash);
        if (commit != null)
        {
             repo.Reset(ResetMode.Hard, commit);
        }
        else
        {
             throw new Exception($"Commit '{commitHash}' not found.");
        }
    }

    public void RollbackFile(string path, string commitHash, string relativeFilePath)
    {
        using var repo = new Repository(path);
        var commit = repo.Lookup<Commit>(commitHash);
        if (commit == null)
        {
            throw new Exception($"Commit '{commitHash}' not found.");
        }

        // Checkout the specific file from the given commit
        // Force checkout to overwrite local changes
        var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
        repo.CheckoutPaths(commit.Sha, new[] { relativeFilePath }, options);
    }
    public void DeleteRepository(string path)
    {
        var gitDir = System.IO.Path.Combine(path, ".git");
        if (System.IO.Directory.Exists(gitDir))
        {
            DeleteDirectory(gitDir);
        }
    }

    private void DeleteDirectory(string path)
    {
        foreach (var file in System.IO.Directory.GetFiles(path))
        {
            System.IO.File.SetAttributes(file, System.IO.FileAttributes.Normal);
            System.IO.File.Delete(file);
        }

        foreach (var dir in System.IO.Directory.GetDirectories(path))
        {
            DeleteDirectory(dir);
        }

        System.IO.Directory.Delete(path, false);
    }
}
