using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using SMTMS.Core.Models;
using SMTMS.Core.Interfaces;

namespace SMTMS.UI.Views;

public partial class ModHistoryDialog : Window
{
    private readonly IGitService _gitService;
    private readonly string _repoPath;
    private readonly string _repoRelativePath;
    private readonly List<GitCommitModel> _history;

    public GitCommitModel? SelectedCommit { get; private set; }
    public ModManifest? SelectedManifest { get; private set; }
    public enum DialogAction
    {
        None,
        ApplyToEditor
    }
    public DialogAction Action { get; private set; } = DialogAction.None;

    public ModHistoryDialog(string modName, IEnumerable<GitCommitModel> history, IGitService gitService, string repoPath, string repoRelativePath)
    {
        InitializeComponent();

        _gitService = gitService;
        _repoPath = repoPath;
        _repoRelativePath = repoRelativePath;
        _history = history.ToList();

        ModNameTextBlock.Text = modName;
        HistoryListView.ItemsSource = _history;

        // 默认选中第一项
        if (_history.Count != 0)
        {
            HistoryListView.SelectedIndex = 0;
        }
    }

    private void HistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryListView.SelectedItem is GitCommitModel commit)
        {
            LoadCommitDetails(commit);
        }
    }

    private void LoadCommitDetails(GitCommitModel commit)
    {
        try
        {
            // LibGit2Sharp 需要使用正斜杠路径
            var normalizedPath = _repoRelativePath.Replace("\\", "/");

            // 获取该版本的 manifest.json 内容
            var fileContent = _gitService.GetFileContentAtCommit(_repoPath, commit.FullHash, normalizedPath);

            if (string.IsNullOrEmpty(fileContent))
            {
                NameTextBox.Text = "(文件不存在)";
                DescriptionTextBox.Text = "(文件不存在)";
                VersionTextBox.Text = "";
                AuthorTextBox.Text = "";
                return;
            }

            // 解析 manifest.json
            var manifest = JsonConvert.DeserializeObject<ModManifest>(fileContent);
            if (manifest != null)
            {
                NameTextBox.Text = manifest.Name;
                DescriptionTextBox.Text = manifest.Description;
                VersionTextBox.Text = manifest.Version;
                AuthorTextBox.Text = manifest.Author;
                SelectedManifest = manifest;
            }
            else
            {
                NameTextBox.Text = "(解析失败)";
                DescriptionTextBox.Text = "(解析失败)";
                VersionTextBox.Text = "";
                AuthorTextBox.Text = "";
            }
        }
        catch (Exception ex)
        {
            NameTextBox.Text = $"(错误: {ex.Message})";
            DescriptionTextBox.Text = "";
            VersionTextBox.Text = "";
            AuthorTextBox.Text = "";
        }
    }

    private void ApplyToEditor_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is GitCommitModel commit && SelectedManifest != null)
        {
            SelectedCommit = commit;
            Action = DialogAction.ApplyToEditor;
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("请选择一个版本。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Action = DialogAction.None;
        DialogResult = false;
        Close();
    }
}
