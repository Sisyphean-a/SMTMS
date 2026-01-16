using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using SMTMS.Core.Infrastructure;
using SMTMS.Core.Interfaces;
using SMTMS.Translation.Services;
using SMTMS.Tests.Mocks;

namespace SMTMS.Tests.Performance;

public class FileReadPerformanceTests
{
    private readonly Mock<IFileSystem> _fileSystemMock;
    private readonly TranslationScanService _service;
    private readonly InMemoryModRepository _modRepository;
    private readonly Mock<IHistoryRepository> _historyRepoMock;

    public FileReadPerformanceTests()
    {
        _fileSystemMock = new Mock<IFileSystem>();
        _modRepository = new InMemoryModRepository();
        _historyRepoMock = new Mock<IHistoryRepository>();

        var logger = new Mock<ILogger<TranslationScanService>>();

        _service = new TranslationScanService(logger.Object, _fileSystemMock.Object);
    }

    [Fact]
    public async Task ScanService_ShouldReadFiles_Efficiently()
    {
        // Arrange
        string modDir = "/mods";
        string modName = "TestMod";
        string manifestPath = $"/mods/{modName}/manifest.json";
        string content = """
                         {
                             "Name": "Test Mod",
                             "Author": "Test Author",
                             "Version": "1.0.0",
                             "UniqueID": "TestAuthor.TestMod"
                         }
                         """;

        _fileSystemMock.Setup(fs => fs.DirectoryExists(modDir)).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetDirectories(modDir, It.IsAny<string>(), It.IsAny<SearchOption>())).Returns(new[] { $"/mods/{modName}" });
        _fileSystemMock.Setup(fs => fs.Combine($"/mods/{modName}", "manifest.json")).Returns(manifestPath);
        _fileSystemMock.Setup(fs => fs.FileExists(manifestPath)).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetRelativePath(modDir, manifestPath)).Returns($"{modName}/manifest.json");
        _fileSystemMock.Setup(fs => fs.GetFileName(manifestPath)).Returns("manifest.json");

        // Mock ReadAllBytesAsync
        _fileSystemMock.Setup(fs => fs.ReadAllBytesAsync(manifestPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(content));

        // Mock ReadAllTextAsync
        _fileSystemMock.Setup(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        // Act
        await _service.SaveTranslationsToDbAsync(modDir, _modRepository, _historyRepoMock.Object);

        // Assert
        // After optimization, it should only call ReadAllBytesAsync

        _fileSystemMock.Verify(fs => fs.ReadAllBytesAsync(manifestPath, It.IsAny<CancellationToken>()), Times.Once, "Should call ReadAllBytesAsync once");

        // This MUST be 0 now
        _fileSystemMock.Verify(fs => fs.ReadAllTextAsync(manifestPath, It.IsAny<CancellationToken>()), Times.Never, "Should NOT call ReadAllTextAsync");
    }
}
