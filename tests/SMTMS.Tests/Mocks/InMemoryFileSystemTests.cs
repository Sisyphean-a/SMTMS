namespace SMTMS.Tests.Mocks;

public class InMemoryFileSystemTests
{
    [Fact]
    public void GetDirectories_ReturnsDirectSubdirectories()
    {
        // Arrange
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory("/mods");
        fs.CreateDirectory("/mods/Mod1");
        fs.CreateDirectory("/mods/Mod2");

        // Act
        var dirs = fs.GetDirectories("/mods");

        // Assert
        Assert.Equal(2, dirs.Length);
        Assert.Contains("/mods/Mod1", dirs);
        Assert.Contains("/mods/Mod2", dirs);
    }

    [Fact]
    public void DirectoryExists_ReturnsTrueForExistingDirectory()
    {
        // Arrange
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory("/mods");

        // Act & Assert
        Assert.True(fs.DirectoryExists("/mods"));
    }

    [Fact]
    public void DirectoryExists_ReturnsFalseForNonExistingDirectory()
    {
        // Arrange
        var fs = new InMemoryFileSystem();

        // Act & Assert
        Assert.False(fs.DirectoryExists("/mods"));
    }

    [Fact]
    public async Task FullWorkflow_CreateDirectoriesAndFiles()
    {
        // Arrange
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory("/mods");
        fs.CreateDirectory("/mods/TestMod");
        await fs.WriteAllTextAsync("/mods/TestMod/manifest.json", "test content");

        // Act
        var exists = fs.DirectoryExists("/mods");
        var dirs = fs.GetDirectories("/mods");
        var fileExists = fs.FileExists("/mods/TestMod/manifest.json");
        var content = await fs.ReadAllTextAsync("/mods/TestMod/manifest.json");

        // Assert
        Assert.True(exists);
        Assert.Single(dirs);
        Assert.Equal("/mods/TestMod", dirs[0]);
        Assert.True(fileExists);
        Assert.Equal("test content", content);
    }

    [Fact]
    public async Task GetFiles_AllDirectories_FindsNestedFiles()
    {
        // Arrange
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory("/mods");
        fs.CreateDirectory("/mods/TestMod");
        await fs.WriteAllTextAsync("/mods/TestMod/manifest.json", "test");

        // Act
        var files = fs.GetFiles("/mods", "manifest.json", SearchOption.AllDirectories);

        // Assert
        Assert.Single(files);
        Assert.Equal("/mods/TestMod/manifest.json", files[0]);
    }
}

