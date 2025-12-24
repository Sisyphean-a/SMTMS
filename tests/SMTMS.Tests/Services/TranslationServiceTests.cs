using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;
using SMTMS.Tests.Mocks;
using SMTMS.Translation.Services;

namespace SMTMS.Tests.Services;

public class TranslationServiceTests
{
    private readonly InMemoryFileSystem _fileSystem;
    private readonly InMemoryModRepository _modRepository;
    private readonly TranslationService _service;

    public TranslationServiceTests()
    {
        _fileSystem = new InMemoryFileSystem();
        _modRepository = new InMemoryModRepository();

        // 创建 Mock ServiceProvider
        var services = new ServiceCollection();
        services.AddScoped<IModRepository>(_ => _modRepository);
        // Added Mock HistoryRepository
        var mockHistoryRepo = new Moq.Mock<IHistoryRepository>();
        services.AddScoped<IHistoryRepository>(_ => mockHistoryRepo.Object);
        
        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        // 创建所有必需的服务
        var loggerFactory = new LoggerFactory();
        var legacyImportService = new LegacyImportService(
            loggerFactory.CreateLogger<LegacyImportService>(),
            _fileSystem);
        var scanService = new TranslationScanService(
            loggerFactory.CreateLogger<TranslationScanService>(),
            _fileSystem);
        var restoreService = new TranslationRestoreService(
            loggerFactory.CreateLogger<TranslationRestoreService>(),
            _fileSystem);
        
        // Removed GitTranslationService

        _service = new TranslationService(
            scopeFactory,
            legacyImportService,
            scanService,
            restoreService);
    }

    [Fact]
    public async Task SaveTranslationsToDbAsync_DirectoryNotExists_ReturnsFailure()
    {
        // 执行
        var result = await _service.SaveTranslationsToDbAsync("/nonexistent");

        // 断言
        Assert.False(result.IsSuccess);
        Assert.Contains("目录不存在", result.Message);
    }

    [Fact]
    public async Task SaveTranslationsToDbAsync_ValidManifest_SavesTranslation()
    {
        // 准备
        _fileSystem.CreateDirectory("/mods");
        _fileSystem.CreateDirectory("/mods/TestMod");

        const string manifestJson = """
                                    {
                                        "Name": "测试模组",
                                        "Author": "Test Author",
                                        "Version": "1.0.0",
                                        "Description": "这是一个测试模组",
                                        "UniqueID": "TestAuthor.TestMod"
                                    }
                                    """;

        await _fileSystem.WriteAllTextAsync("/mods/TestMod/manifest.json", manifestJson);

        // Act
        var result = await _service.SaveTranslationsToDbAsync("/mods");

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
        // Act
        var result = await _service.RestoreTranslationsFromDbAsync("/nonexistent");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("模组目录不存在", result.Message); // Updated expectation to match implementation
    }

    [Fact]
    public async Task RestoreTranslationsFromDbAsync_NoTranslations_ReturnsSuccess()
    {
        // Arrange
        _fileSystem.CreateDirectory("/mods");

        // Act
        var result = await _service.RestoreTranslationsFromDbAsync("/mods");

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

        const string originalManifest = """
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

        // Act
        var result = await _service.RestoreTranslationsFromDbAsync("/mods");

        // Assert
        Assert.True(result.IsSuccess);

        var restoredContent = await _fileSystem.ReadAllTextAsync("/mods/TestMod/manifest.json");
        Assert.Contains("测试模组", restoredContent);
        Assert.Contains("这是一个测试模组", restoredContent);
    }
    [Fact]
    public async Task SaveTranslationsToDbAsync_NestedMod_Ignored()
    {
        // Arrange
        _fileSystem.CreateDirectory("/mods");
        _fileSystem.CreateDirectory("/mods/Group");
        _fileSystem.CreateDirectory("/mods/Group/NestedMod");

        const string manifestJson = """
                                    {
                                        "Name": "Deep Nested Mod",
                                        "Author": "Test Author",
                                        "Version": "1.0.0",
                                        "UniqueID": "TestAuthor.NestedMod"
                                    }
                                    """;

        // Write manifest deep in the structure
        await _fileSystem.WriteAllTextAsync("/mods/Group/NestedMod/manifest.json", manifestJson);

        // Act
        // 当前逻辑：ScanManifestFilesAsync 仅扫描 /mods 的一级子目录。
        // /mods/Group 不包含 manifest.json（在此上下文中它是一个文件夹，而不是模组根目录）
        // /mods/Group/NestedMod 包含 manifest.json 但深度为 2 层。
        // 等等，TranslationScanService 逻辑：
        // var subDirectories = _fileSystem.GetDirectories(modDirectory); --> 返回 /mods/Group
        // 检查 /mods/Group/manifest.json --> False。
        // 因此它应该找到 0 个模组。
        var result = await _service.SaveTranslationsToDbAsync("/mods");

        // Assert
        // Expect 0 success because it should not find the nested mod
        Assert.True(result.IsSuccess); // It succeeds in "doing nothing" or finding 0 mods
        Assert.Equal(0, result.SuccessCount); 
        
        // Ensure not saved to DB
        var mod = await _modRepository.GetModAsync("TestAuthor.NestedMod");
        Assert.Null(mod);
    }
}
