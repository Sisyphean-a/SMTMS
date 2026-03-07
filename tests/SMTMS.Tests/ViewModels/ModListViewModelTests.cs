using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SMTMS.Avalonia.Messages;
using SMTMS.Avalonia.Services;
using SMTMS.Avalonia.ViewModels;
using SMTMS.Core.Interfaces;
using SMTMS.Core.Models;

namespace SMTMS.Tests.ViewModels;

public class ModListViewModelTests
{
    [Fact]
    public void OpenModFolderCommand_WithValidManifestPath_OpensContainingDirectory()
    {
        using var tempDir = new TempDirectory();
        var modDir = Path.Combine(tempDir.Path, "TestMod");
        Directory.CreateDirectory(modDir);

        var manifestPath = Path.Combine(modDir, "manifest.json");
        var launcher = new Mock<IPathOpener>();
        var viewModel = CreateViewModel(launcher.Object);
        var mod = CreateModViewModel(manifestPath);

        viewModel.OpenModFolderCommand.Execute(mod);

        launcher.Verify(x => x.Open(modDir), Times.Once);
    }

    [Fact]
    public void OpenModFolderCommand_WithMissingManifestPath_DoesNotOpenFolder()
    {
        var launcher = new Mock<IPathOpener>();
        var viewModel = CreateViewModel(launcher.Object);
        var collector = new StatusMessageCollector();

        WeakReferenceMessenger.Default.Register<StatusMessageCollector, StatusMessage>(
            collector,
            static (recipient, message) => recipient.Receive(message));

        try
        {
            var mod = CreateModViewModel(null);

            viewModel.OpenModFolderCommand.Execute(mod);

            launcher.Verify(x => x.Open(It.IsAny<string>()), Times.Never);
            Assert.Contains(collector.Messages, x => x.Level == StatusLevel.Error && x.Value.Contains("文件路径无效"));
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(collector);
        }
    }

    private static ModListViewModel CreateViewModel(IPathOpener pathOpener)
    {
        return new ModListViewModel(
            Mock.Of<IModService>(),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<ILogger<ModListViewModel>>(),
            pathOpener);
    }

    private static ModViewModel CreateModViewModel(string? manifestPath)
    {
        return new ModViewModel(new ModManifest
        {
            Name = "Test Mod",
            Author = "Test Author",
            Version = "1.0.0",
            Description = "Test Description",
            UniqueID = "Test.Author.Mod",
            ManifestPath = manifestPath
        });
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"smtms-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private sealed class StatusMessageCollector
    {
        public List<StatusMessage> Messages { get; } = [];

        public void Receive(StatusMessage message)
        {
            Messages.Add(message);
        }
    }
}
