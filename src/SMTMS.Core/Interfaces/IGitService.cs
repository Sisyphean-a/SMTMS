namespace SMTMS.Core.Interfaces;

public interface IGitService
{
    /// <summary>
    /// Deletes the repository at the specified path (recursively deletes .git folder).
    /// </summary>
    void DeleteRepository(string path); 

    bool IsRepository(string path);
    void Init(string path);
    void CommitAll(string path, string message);
    void Checkout(string path, string branchName);
    // Returns a list of changed files
    IEnumerable<string> GetStatus(string path);
    IEnumerable<SMTMS.Core.Models.GitCommitModel> GetHistory(string path);
    void Reset(string path, string commitHash);
    void RollbackFile(string path, string commitHash, string relativeFilePath);
}
