using SMTMS.Core.Services;
using SMTMS.Tests.Mocks;

namespace SMTMS.Tests.Services;

public class ModServiceTests
{
    [Fact]
    public async Task ScanModsAsync_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        var service = new ModService(fileSystem);

        // Act
        var result = await service.ScanModsAsync("/mods");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanModsAsync_ValidManifest_ReturnsModList()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        fileSystem.CreateDirectory("/mods");
        fileSystem.CreateDirectory("/mods/TestMod");
        
        var manifestJson = """
        {
            "Name": "Test Mod",
            "Author": "Test Author",
            "Version": "1.0.0",
            "Description": "A test mod",
            "UniqueID": "TestAuthor.TestMod"
        }
        """;
        
        await fileSystem.WriteAllTextAsync("/mods/TestMod/manifest.json", manifestJson);
        var service = new ModService(fileSystem);

        // Act
        var result = await service.ScanModsAsync("/mods");

        // Assert
        var mods = result.ToList();
        Assert.Single(mods);
        Assert.Equal("Test Mod", mods[0].Name);
        Assert.Equal("TestAuthor.TestMod", mods[0].UniqueID);
    }

    [Fact]
    public async Task ScanModsAsync_MultipleMods_ReturnsAllMods()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        fileSystem.CreateDirectory("/mods");
        fileSystem.CreateDirectory("/mods/Mod1");
        fileSystem.CreateDirectory("/mods/Mod2");
        
        await fileSystem.WriteAllTextAsync("/mods/Mod1/manifest.json", 
            """{"Name": "Mod 1", "UniqueID": "Author.Mod1", "Version": "1.0.0"}""");
        await fileSystem.WriteAllTextAsync("/mods/Mod2/manifest.json", 
            """{"Name": "Mod 2", "UniqueID": "Author.Mod2", "Version": "1.0.0"}""");
        
        var service = new ModService(fileSystem);

        // Act
        var result = await service.ScanModsAsync("/mods");

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task ReadManifestAsync_ValidFile_ReturnsManifest()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        var manifestJson = """
        {
            "Name": "Test Mod",
            "Author": "Test Author",
            "Version": "1.0.0",
            "UniqueID": "TestAuthor.TestMod"
        }
        """;
        
        await fileSystem.WriteAllTextAsync("/manifest.json", manifestJson);
        var service = new ModService(fileSystem);

        // Act
        var manifest = await service.ReadManifestAsync("/manifest.json");

        // Assert
        Assert.NotNull(manifest);
        Assert.Equal("Test Mod", manifest.Name);
        Assert.Equal("/manifest.json", manifest.ManifestPath);
    }

    [Fact]
    public async Task ReadManifestAsync_FileNotFound_ReturnsNull()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        var service = new ModService(fileSystem);

        // Act
        var manifest = await service.ReadManifestAsync("/nonexistent.json");

        // Assert
        Assert.Null(manifest);
    }

    [Fact]
    public async Task WriteManifestAsync_ValidManifest_WritesFile()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        var service = new ModService(fileSystem);
        var manifest = new SMTMS.Core.Models.ModManifest
        {
            Name = "Test Mod",
            Author = "Test Author",
            Version = "1.0.0",
            UniqueID = "TestAuthor.TestMod"
        };

        // Act
        await service.WriteManifestAsync("/manifest.json", manifest);

        // Assert
        var content = await fileSystem.ReadAllTextAsync("/manifest.json");
        Assert.Contains("Test Mod", content);
        Assert.Contains("TestAuthor.TestMod", content);
    }

    [Fact]
    public async Task ReadManifestAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var fileSystem = new InMemoryFileSystem();
        await fileSystem.WriteAllTextAsync("/invalid.json", "{ invalid json }");
        var service = new ModService(fileSystem);

        // Act
        var manifest = await service.ReadManifestAsync("/invalid.json");

        // Assert
        Assert.Null(manifest);
    }
}

