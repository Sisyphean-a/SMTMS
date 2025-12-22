using SMTMS.Core.Infrastructure;

namespace SMTMS.Tests.Mocks;

/// <summary>
/// 内存文件系统 Mock - 用于单元测试
/// </summary>
public class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _textFiles = new();
    private readonly Dictionary<string, byte[]> _binaryFiles = new();
    private readonly HashSet<string> _directories = [];

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_textFiles.ContainsKey(path))
            throw new FileNotFoundException($"File not found: {path}");
        return Task.FromResult(_textFiles[path]);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        // 优先从 binary files 读取,如果不存在则尝试从 text files 转换
        if (_binaryFiles.ContainsKey(path))
            return Task.FromResult(_binaryFiles[path]);

        if (_textFiles.ContainsKey(path))
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(_textFiles[path]));

        throw new FileNotFoundException($"File not found: {path}");
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default)
    {
        _textFiles[path] = contents;
        return Task.CompletedTask;
    }

    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
    {
        _binaryFiles[path] = bytes;
        return Task.CompletedTask;
    }

    public bool FileExists(string path)
        => _textFiles.ContainsKey(path) || _binaryFiles.ContainsKey(path);

    public void DeleteFile(string path)
    {
        _textFiles.Remove(path);
        _binaryFiles.Remove(path);
    }

    public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
    {
        if (!overwrite && FileExists(destFileName))
            throw new IOException($"File already exists: {destFileName}");

        if (_textFiles.ContainsKey(sourceFileName))
            _textFiles[destFileName] = _textFiles[sourceFileName];
        else if (_binaryFiles.ContainsKey(sourceFileName))
            _binaryFiles[destFileName] = _binaryFiles[sourceFileName];
        else
            throw new FileNotFoundException($"File not found: {sourceFileName}");
    }

    public void MoveFile(string sourceFileName, string destFileName)
    {
        CopyFile(sourceFileName, destFileName, true);
        DeleteFile(sourceFileName);
    }

    public bool DirectoryExists(string path)
        => _directories.Contains(path);

    public void CreateDirectory(string path)
        => _directories.Add(path);

    public void DeleteDirectory(string path, bool recursive = false)
    {
        if (recursive)
        {
            var toRemove = _textFiles.Keys.Where(k => k.StartsWith(path)).ToList();
            foreach (var key in toRemove)
                _textFiles.Remove(key);

            var binToRemove = _binaryFiles.Keys.Where(k => k.StartsWith(path)).ToList();
            foreach (var key in binToRemove)
                _binaryFiles.Remove(key);
        }
        _directories.Remove(path);
    }

    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var allFiles = _textFiles.Keys.Concat(_binaryFiles.Keys).Distinct();
        var normalizedPath = NormalizePath(path);

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            return allFiles
                .Where(f => NormalizePath(Path.GetDirectoryName(f) ?? "") == normalizedPath)
                .Where(f => MatchesPattern(Path.GetFileName(f), searchPattern))
                .ToArray();
        }
        else
        {
            // AllDirectories: 包含所有子目录中的文件
            return allFiles
                .Where(f =>
                {
                    var normalizedFile = NormalizePath(f);
                    // 文件必须在指定路径下（包括子目录）
                    return normalizedFile.StartsWith(normalizedPath + "/");
                })
                .Where(f => MatchesPattern(Path.GetFileName(f), searchPattern))
                .ToArray();
        }
    }

    public string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var normalizedPath = NormalizePath(path);

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            // 查找直接子目录
            return _directories
                .Where(d =>
                {
                    var normalizedDir = NormalizePath(d);
                    // 检查是否是直接子目录
                    if (!normalizedDir.StartsWith(normalizedPath + "/"))
                        return false;

                    // 确保没有更深层的子目录
                    var relativePart = normalizedDir[(normalizedPath.Length + 1)..];
                    return !relativePart.Contains("/");
                })
                .Where(d => MatchesPattern(Path.GetFileName(d), searchPattern))
                .ToArray();
        }
        else
        {
            return _directories
                .Where(d => NormalizePath(d).StartsWith(normalizedPath + "/"))
                .Where(d => MatchesPattern(Path.GetFileName(d), searchPattern))
                .ToArray();
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace("\\", "/").TrimEnd('/');
    }

    public string GetFileName(string path)
        => Path.GetFileName(path);

    public string GetDirectoryName(string path)
        => Path.GetDirectoryName(path) ?? string.Empty;

    public string GetRelativePath(string relativeTo, string path)
        => Path.GetRelativePath(relativeTo, path);

    public string Combine(params string[] paths)
        => NormalizePath(Path.Combine(paths));

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern == "*") return true;
        // 简单的通配符匹配
        return fileName.EndsWith(pattern.TrimStart('*'));
    }
}

