using LibGit2Sharp;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Aspects;
using SMTMS.Core.Models;
using Newtonsoft.Json;

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

    public IEnumerable<ModDiffModel> GetStructuredDiff(string repoPath, string commitHash)
    {
        using var repo = new Repository(repoPath);
        var commit = repo.Lookup<Commit>(commitHash);
        if (commit == null) return Enumerable.Empty<ModDiffModel>();

        var parent = commit.Parents.FirstOrDefault();
        var tree = commit.Tree;
        var parentTree = parent?.Tree;

        var changes = repo.Diff.Compare<TreeChanges>(parentTree, tree);
        var diffModels = new List<ModDiffModel>();

        foreach (var change in changes)
        {
            // 只处理 manifest.json 文件
            if (!change.Path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var diffModel = new ModDiffModel
            {
                RelativePath = change.Path,
                ChangeType = change.Status.ToString(),
                FolderName = ExtractFolderName(change.Path)
            };

            try
            {
                ModManifest? oldManifest = null;
                ModManifest? newManifest = null;

                // 获取旧版本内容
                if (change.Status != ChangeKind.Added && parent != null)
                {
                    var oldContent = GetFileContentAtCommit(repoPath, parent.Sha, change.Path);
                    if (!string.IsNullOrEmpty(oldContent))
                    {
                        oldManifest = JsonConvert.DeserializeObject<ModManifest>(oldContent);
                    }
                }

                // 获取新版本内容
                if (change.Status != ChangeKind.Deleted)
                {
                    var newContent = GetFileContentAtCommit(repoPath, commit.Sha, change.Path);
                    if (!string.IsNullOrEmpty(newContent))
                    {
                        newManifest = JsonConvert.DeserializeObject<ModManifest>(newContent);
                    }
                }

                // 设置 UniqueID 和 ModName
                diffModel.UniqueID = newManifest?.UniqueID ?? oldManifest?.UniqueID ?? "Unknown";
                diffModel.ModName = newManifest?.Name ?? oldManifest?.Name ?? "Unknown Mod";

                // 比较字段变更
                int changeCount = 0;

                // 名称变更
                if (oldManifest?.Name != newManifest?.Name)
                {
                    diffModel.NameChange = new FieldChange
                    {
                        FieldName = "名称",
                        OldValue = oldManifest?.Name,
                        NewValue = newManifest?.Name
                    };
                    changeCount++;
                }

                // 描述变更
                if (oldManifest?.Description != newManifest?.Description)
                {
                    diffModel.DescriptionChange = new FieldChange
                    {
                        FieldName = "描述",
                        OldValue = oldManifest?.Description,
                        NewValue = newManifest?.Description
                    };
                    changeCount++;
                }

                // 作者变更
                if (oldManifest?.Author != newManifest?.Author)
                {
                    diffModel.AuthorChange = new FieldChange
                    {
                        FieldName = "作者",
                        OldValue = oldManifest?.Author,
                        NewValue = newManifest?.Author
                    };
                    changeCount++;
                }

                // 版本变更
                if (oldManifest?.Version != newManifest?.Version)
                {
                    diffModel.VersionChange = new FieldChange
                    {
                        FieldName = "版本",
                        OldValue = oldManifest?.Version,
                        NewValue = newManifest?.Version
                    };
                    changeCount++;
                }

                diffModel.ChangeCount = changeCount;
                diffModels.Add(diffModel);
            }
            catch (Exception)
            {
                // 如果解析失败，仍然添加基本信息
                diffModel.ModName = "解析失败";
                diffModel.ChangeCount = 0;
                diffModels.Add(diffModel);
            }
        }

        return diffModels;
    }

    private string ExtractFolderName(string path)
    {
        // 从路径中提取文件夹名称
        // 例如: "Mods/MyMod/manifest.json" -> "MyMod"
        var parts = path.Replace("\\", "/").Split('/');
        if (parts.Length >= 2)
        {
            return parts[parts.Length - 2];
        }
        return path;
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
