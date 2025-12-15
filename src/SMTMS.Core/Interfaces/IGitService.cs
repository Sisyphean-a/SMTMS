namespace SMTMS.Core.Interfaces;

public interface IGitService
{
    bool IsRepository(string path);
    void Init(string path);
    void StageAll(string path);
    void Commit(string path, string message, string authorName, string authorEmail);
}
