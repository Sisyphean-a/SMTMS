using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMTMS.Core.Common;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Tests.Mocks;
using SMTMS.Translation.Services;

namespace SMTMS.Tests.Services;

public class TranslationServiceTests
{
    private readonly InMemoryFileSystem _fileSystem;
    private readonly InMemoryModRepository _modRepository;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TranslationService> _logger;

    public TranslationServiceTests()
    {
        _fileSystem = new InMemoryFileSystem();
        _modRepository = new InMemoryModRepository();
        
        // 创建 Mock ServiceProvider
        var services = new ServiceCollection();
        services.AddScoped<IModRepository>(_ => _modRepository);
        var serviceProvider = services.BuildServiceProvider();
        
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _logger = new LoggerFactory().CreateLogger<TranslationService>();
    }

    [Fact]
    public async Task SaveTranslationsToDbAsync_DirectoryNotExists_ReturnsFailure()
    {
        // Arrange
        var service = new TranslationService(_scopeFactory, _logger, _fileSystem);

        // Act
        var result = await service.SaveTranslationsToDbAsync("/nonexistent");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("目录不存在", result.Message);
    }

    [Fact]
    public async Task SaveTranslationsToDbAsync_ValidManifest_SavesTranslation()
    {
        // Arrange
        _fileSystem.CreateDirectory("/mods");
        _fileSystem.CreateDirectory("/mods/TestMod");

        var manifestJson = """
        {
            "Name": "测试模组",
            "Author": "Test Author",
            "Version": "1.0.0",
            "Description": "这是一个测试模组",
            "UniqueID": "TestAuthor.TestMod"
        }
        """;

        await _fileSystem.WriteAllTextAsync("/mods/TestMod/manifest.json", manifestJson);
        var service = new TranslationService(_scopeFactory, _logger, _fileSystem);

        // Act
        var result = await service.SaveTranslationsToDbAsync("/mods");

        // Assert
        Assert.True(result.IsSuccess, $"Expected success but got: {result.Message}. Details: {string.Join(", ", result.Details)}");
        Assert.Equal(1, result.SuccessCount);

        var mod = await _modRepository.GetModAsync("TestAuthor.TestMod");
        Assert.NotNull(mod);
        Assert.Equal("测试模组", mod.TranslatedName);
        Assert.Equal("这是一个测试模组", mod.TranslatedDescription);
    }

    [Fact]
    public async Task RestoreTranslationsFromDbAsync_DirectoryNotExists_ReturnsFailure()
    {
        // Arrange
        var service = new TranslationService(_scopeFactory, _logger, _fileSystem);

        // Act
        var result = await service.RestoreTranslationsFromDbAsync("/nonexistent");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("目录不存在", result.Message);
    }

    [Fact]
    public async Task RestoreTranslationsFromDbAsync_NoTranslations_ReturnsSuccess()
    {
        // Arrange
        _fileSystem.CreateDirectory("/mods");
        var service = new TranslationService(_scopeFactory, _logger, _fileSystem);

        // Act
        var result = await service.RestoreTranslationsFromDbAsync("/mods");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("没有需要恢复的翻译", result.Message);
    }

    [Fact]
    public async Task RestoreTranslationsFromDbAsync_WithTranslations_RestoresFiles()
    {
        // Arrange
        _fileSystem.CreateDirectory("/mods");
        _fileSystem.CreateDirectory("/mods/TestMod");

        var originalManifest = """
        {
            "Name": "Test Mod",
            "Author": "Test Author",
            "Version": "1.0.0",
            "Description": "A test mod",
            "UniqueID": "TestAuthor.TestMod"
        }
        """;

        await _fileSystem.WriteAllTextAsync("/mods/TestMod/manifest.json", originalManifest);

        // 添加翻译到数据库
        _modRepository.AddMod(new ModMetadata
        {
            UniqueID = "TestAuthor.TestMod",
            TranslatedName = "测试模组",
            TranslatedDescription = "这是一个测试模组",
            RelativePath = "TestMod/manifest.json"
        });

        var service = new TranslationService(_scopeFactory, _logger, _fileSystem);

        // Act
        var result = await service.RestoreTranslationsFromDbAsync("/mods");

        // Assert
        Assert.True(result.IsSuccess);

        var restoredContent = await _fileSystem.ReadAllTextAsync("/mods/TestMod/manifest.json");
        Assert.Contains("测试模组", restoredContent);
        Assert.Contains("这是一个测试模组", restoredContent);
    }
}

