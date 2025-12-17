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
        
        // Create .gitignore to exclude database files
        var gitIgnorePath = Path.Combine(path, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            File.WriteAllText(gitIgnorePath, "smtms.db\nsmtms.db-shm\nsmtms.db-wal\n");
        }
    }

    public void CommitAll(string path, string message)
    {
        using var repo = new Repository(path);
        
        // Ensure .gitignore is valid
        var gitIgnorePath = Path.Combine(path, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
             File.WriteAllText(gitIgnorePath, "smtms.db\nsmtms.db-shm\nsmtms.db-wal\n");
        }

        // Auto-stage everything (respecting .gitignore)
        Commands.Stage(repo, "*");

        // Check if there are changes to commit
        var status = repo.RetrieveStatus();
        if (!status.IsDirty) return;

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

    public string GetDiff(string repoPath, string commitHash)
    {
         using var repo = new Repository(repoPath);
         var commit = repo.Lookup<Commit>(commitHash);
         if (commit == null) return "Commit not found.";

         var sb = new System.Text.StringBuilder();
         var parent = commit.Parents.FirstOrDefault();
         var tree = commit.Tree;
         var parentTree = parent?.Tree; 

         var changes = repo.Diff.Compare<TreeChanges>(parentTree, tree);

         if (!changes.Any()) return "No changes (Empty Commit)";

         foreach (var change in changes)
         {
             // Debug: Show all paths to diagnose why filter failed
             sb.AppendLine($"{change.Status}: {change.Path}");
         }
         
         return sb.ToString();
    }
    
    public IEnumerable<SMTMS.Core.Models.GitCommitModel> GetFileHistory(string repoPath, string relativeFilePath)
    {
        using var repo = new Repository(repoPath);
        
        // LibGit2Sharp expects forward slashes for paths in the repo
        var normalizedPath = relativeFilePath.Replace("\\", "/");
        
        var entries = repo.Commits.QueryBy(normalizedPath);

        return entries.Select(c => new SMTMS.Core.Models.GitCommitModel
        {
            ShortHash = c.Commit.Sha.Substring(0, 7),
            FullHash = c.Commit.Sha,
            Message = c.Commit.MessageShort,
            Author = c.Commit.Author.Name,
            Date = c.Commit.Author.When
        }).ToList();
    }


    public string GetFileContentAtCommit(string repoPath, string commitHash, string relativeFilePath)
    {
        using var repo = new Repository(repoPath);
        var commit = repo.Lookup<Commit>(commitHash);
        if (commit == null) throw new Exception("Commit not found");

        var treeEntry = commit[relativeFilePath];
        if (treeEntry == null) return string.Empty;

        var blob = treeEntry.Target as Blob;
        if (blob == null) return string.Empty;

        return blob.GetContentText();
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
