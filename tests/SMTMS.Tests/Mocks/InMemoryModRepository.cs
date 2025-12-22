using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Tests.Mocks;

/// <summary>
/// 内存模组仓储 Mock - 用于单元测试
/// </summary>
public class InMemoryModRepository : IModRepository
{
    private readonly Dictionary<string, ModMetadata> _mods = new();

    public Task<ModMetadata?> GetModAsync(string uniqueId, CancellationToken cancellationToken = default)
    {
        _mods.TryGetValue(uniqueId, out var mod);
        return Task.FromResult(mod);
    }

    public Task<IEnumerable<ModMetadata>> GetAllModsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<ModMetadata>>(_mods.Values.ToList());
    }

    public Task UpsertModAsync(ModMetadata mod, CancellationToken cancellationToken = default)
    {
        _mods[mod.UniqueID] = mod;
        return Task.CompletedTask;
    }

    public Task UpsertModsAsync(IEnumerable<ModMetadata> mods, CancellationToken cancellationToken = default)
    {
        foreach (var mod in mods)
        {
            _mods[mod.UniqueID] = mod;
        }
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 内存实现不需要保存
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, ModMetadata>> GetModsByIdsAsync(IEnumerable<string> uniqueIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, ModMetadata>();
        foreach (var id in uniqueIds)
        {
            if (_mods.TryGetValue(id, out var mod))
            {
                result[id] = mod;
            }
        }
        return Task.FromResult(result);
    }

    // 测试辅助方法
    public void Clear()
    {
        _mods.Clear();
    }

    public void AddMod(ModMetadata mod)
    {
        _mods[mod.UniqueID] = mod;
    }
}

