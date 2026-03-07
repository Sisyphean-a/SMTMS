# Mod List Open Folder Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add an `Open` button column to the mod list so users can open the corresponding mod folder directly from each row.

**Architecture:** Keep row UI in `ModListView.axaml`, route button clicks to `ModListViewModel`, and isolate shell launching behind a small Avalonia service interface for testability. Resolve the target folder from `ManifestPath` in the view model and surface invalid-path failures via the existing status messaging flow.

**Tech Stack:** Avalonia UI, CommunityToolkit.Mvvm, xUnit, Moq

---

### Task 1: Add failing view-model tests

**Files:**
- Modify: `tests/SMTMS.Tests/SMTMS.Tests.csproj`
- Create: `tests/SMTMS.Tests/ViewModels/ModListViewModelTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void OpenModFolderCommand_WithValidManifestPath_OpensContainingDirectory()
{
    var launcher = new Mock<IPathOpener>();
    var viewModel = CreateViewModel(launcher.Object);
    var mod = CreateModViewModel("D:/Games/Mods/TestMod/manifest.json");

    viewModel.OpenModFolderCommand.Execute(mod);

    launcher.Verify(x => x.Open("D:/Games/Mods/TestMod"), Times.Once);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/SMTMS.Tests/SMTMS.Tests.csproj --filter "FullyQualifiedName~ModListViewModelTests"`
Expected: FAIL because the view model command/service does not exist yet.

### Task 2: Add minimal launcher abstraction

**Files:**
- Create: `src/SMTMS.Avalonia/Services/IPathOpener.cs`
- Create: `src/SMTMS.Avalonia/Services/ShellPathOpener.cs`
- Modify: `src/SMTMS.Avalonia/App.axaml.cs`

**Step 1: Write minimal implementation**

```csharp
public interface IPathOpener
{
    void Open(string path);
}

public sealed class ShellPathOpener : IPathOpener
{
    public void Open(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
```

**Step 2: Register service**

```csharp
services.AddSingleton<IPathOpener, ShellPathOpener>();
```

### Task 3: Add row command and UI column

**Files:**
- Modify: `src/SMTMS.Avalonia/ViewModels/ModListViewModel.cs`
- Modify: `src/SMTMS.Avalonia/Views/ModListView.axaml`

**Step 1: Implement row command**

```csharp
[RelayCommand]
public void OpenModFolder(ModViewModel? mod)
{
    if (mod?.ManifestPath is not { Length: > 0 } manifestPath)
    {
        WeakReferenceMessenger.Default.Send(new StatusMessage("打开目录失败: 文件路径无效", StatusLevel.Error));
        return;
    }

    var folderPath = Path.GetDirectoryName(manifestPath);
    if (string.IsNullOrWhiteSpace(folderPath))
    {
        WeakReferenceMessenger.Default.Send(new StatusMessage("打开目录失败: 无法解析模组目录", StatusLevel.Error));
        return;
    }

    _pathOpener.Open(folderPath);
}
```

**Step 2: Add DataGrid column**

```xml
<DataGridTemplateColumn Header="Open" Width="72">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate x:DataType="vm:ModViewModel">
      <Button
        Command="{Binding #Root.DataContext.ModListViewModel.OpenModFolderCommand}"
        CommandParameter="{Binding}"
        Content="Open" />
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

### Task 4: Verify tests

**Files:**
- Test: `tests/SMTMS.Tests/ViewModels/ModListViewModelTests.cs`

**Step 1: Run focused tests**

Run: `dotnet test tests/SMTMS.Tests/SMTMS.Tests.csproj --filter "FullyQualifiedName~ModListViewModelTests"`
Expected: PASS

**Step 2: Run adjacent regression tests**

Run: `dotnet test tests/SMTMS.Tests/SMTMS.Tests.csproj --filter "FullyQualifiedName~TranslationServiceTests|FullyQualifiedName~FileReadPerformanceTests"`
Expected: PASS

**Step 3: Commit**

```bash
git add docs/plans/2026-03-07-mod-list-open-folder-design.md docs/plans/2026-03-07-mod-list-open-folder.md tests/SMTMS.Tests/SMTMS.Tests.csproj tests/SMTMS.Tests/ViewModels/ModListViewModelTests.cs src/SMTMS.Avalonia/Services/IPathOpener.cs src/SMTMS.Avalonia/Services/ShellPathOpener.cs src/SMTMS.Avalonia/ViewModels/ModListViewModel.cs src/SMTMS.Avalonia/Views/ModListView.axaml src/SMTMS.Avalonia/App.axaml.cs
git commit -m "feat: add open-folder action to mod list"
```

Note: do not commit unless explicitly requested by the user.
