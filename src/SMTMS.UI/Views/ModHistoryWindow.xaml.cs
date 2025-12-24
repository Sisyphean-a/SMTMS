using System.Windows;
using SMTMS.UI.ViewModels;

namespace SMTMS.UI.Views;

public partial class ModHistoryWindow : Window
{
    public ModHistoryWindow(ModHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // 处理 ViewModel 发出的关闭请求
        viewModel.OnCloseRequest += Close;
    }
}
