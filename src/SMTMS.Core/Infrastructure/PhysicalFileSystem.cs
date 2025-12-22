namespace SMTMS.Core.Infrastructure;

/// <summary>
/// 物理文件系统实现 - 直接操作真实硬盘
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    // 文件操作
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllBytesAsync(path, cancellationToken);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, contents, cancellationToken);

    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        => File.WriteAllBytesAsync(path, bytes, cancellationToken);

    public bool FileExists(string path)
        => File.Exists(path);

    public void DeleteFile(string path)
        => File.Delete(path);

    public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
        => File.Copy(sourceFileName, destFileName, overwrite);

    public void MoveFile(string sourceFileName, string destFileName)
        => File.Move(sourceFileName, destFileName);

    // 目录操作
    public bool DirectoryExists(string path)
        => Directory.Exists(path);

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(path);

    public void DeleteDirectory(string path, bool recursive = false)
        => Directory.Delete(path, recursive);

    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetFiles(path, searchPattern, searchOption);

    public string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.GetDirectories(path, searchPattern, searchOption);

    // 路径操作
    public string GetFileName(string path)
        => Path.GetFileName(path);

    public string GetDirectoryName(string path)
        => Path.GetDirectoryName(path) ?? string.Empty;

    public string GetRelativePath(string relativeTo, string path)
        => Path.GetRelativePath(relativeTo, path);

    public string Combine(params string[] paths)
        => Path.Combine(paths);
}

