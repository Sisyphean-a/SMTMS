using System.Windows;
using SMTMS.UI.ViewModels;

namespace SMTMS.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += OnWindowClosing;

        // 订阅保存命令执行前的事件，更新绑定
        viewModel.ModListViewModel.SaveRequested += OnSaveRequested;
    }

    private void OnSaveRequested(object? sender, EventArgs e)
    {
        // 手动更新所有 Explicit 绑定
        NameTextBox?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        AuthorTextBox?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        VersionTextBox?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        DescriptionTextBox?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var viewModel = DataContext as MainViewModel;
        if (viewModel == null) return;

        // 检查是否有未保存的更改
        var unsavedMods = viewModel.ModListViewModel.Mods.Where(m => m.IsDirty).ToList();
        if (unsavedMods.Count != 0)
        {
            var modNames = string.Join("\n", unsavedMods.Select(m => $"• {m.Name}"));
            var result = System.Windows.MessageBox.Show(
                $"以下模组有未保存的更改:\n\n{modNames}\n\n确定要关闭程序吗？未保存的更改将丢失。",
                "未保存的更改",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }
    }
}