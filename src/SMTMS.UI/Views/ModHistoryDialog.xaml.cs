using System.Collections.Generic;
using System.Windows;
using SMTMS.Core.Models;

namespace SMTMS.UI.Views;

public partial class ModHistoryDialog : Window
{
    public GitCommitModel? SelectedCommit { get; private set; }

    public ModHistoryDialog(string modName, IEnumerable<GitCommitModel> history)
    {
        InitializeComponent();
        ModNameTextBlock.Text = modName;
        HistoryListView.ItemsSource = history;
    }

    private void Rollback_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryListView.SelectedItem is GitCommitModel commit)
        {
            var result = MessageBox.Show($"Are you sure you want to rollback '{ModNameTextBlock.Text}' to version {commit.ShortHash}?\n\nThis will overwrite your local translation file.", 
                                         "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                SelectedCommit = commit;
                DialogResult = true;
                Close();
            }
        }
        else
        {
             MessageBox.Show("Please select a version to rollback to.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
