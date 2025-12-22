namespace SMTMS.Core.Infrastructure;

/// <summary>
/// 文件系统抽象接口
/// 用于解耦文件 IO 操作,便于单元测试和 Mock
/// </summary>
public interface IFileSystem
{
    // 文件操作
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default);
    bool FileExists(string path);
    void DeleteFile(string path);
    void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);
    void MoveFile(string sourceFileName, string destFileName);

    // 目录操作
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive = false);
    string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

    // 路径操作
    string GetFileName(string path);
    string GetDirectoryName(string path);
    string GetRelativePath(string relativeTo, string path);
    string Combine(params string[] paths);
}

