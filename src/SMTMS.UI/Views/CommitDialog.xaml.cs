using System.Windows;

namespace SMTMS.UI.Views;

public partial class CommitDialog : Window
{
    public string CommitMessage { get; private set; } = string.Empty;

    public CommitDialog(string defaultMessage = "")
    {
        InitializeComponent();
        MessageTextBox.Text = defaultMessage;
        MessageTextBox.Focus();
        MessageTextBox.SelectAll();
    }

    private void Commit_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
        {
            System.Windows.MessageBox.Show("Please enter a commit message.", "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        CommitMessage = MessageTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
