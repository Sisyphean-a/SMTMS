namespace SMTMS.Core.Interfaces;

public interface IGitService
{
    bool IsRepository(string path);
    void Init(string path);
    string Clone(string sourceUrl, string destinationPath);
    void StageAll(string path);
    void Commit(string path, string message, string authorName, string authorEmail);
    void Push(string path, string username, string password);
    void Pull(string path, string authorName, string authorEmail);
    void Checkout(string path, string branchName);
    IEnumerable<string> GetRemotes(string path);
    void AddRemote(string path, string name, string url);
    // Returns a list of changed files
    IEnumerable<string> GetStatus(string path);
}
