using System.Windows;
using Wpf.Ui.Controls;

namespace NewFPSConverter;

public partial class ConfirmDialog : FluentWindow
{
    public System.Windows.MessageBoxResult Result { get; private set; } = System.Windows.MessageBoxResult.Cancel;

    public ConfirmDialog(string message, string title = "Confirmation", string yesText = "Yes", string noText = "No")
    {
        InitializeComponent();
        MessageText.Text = message;
        Title = title;
        YesButton.Content = yesText;
        NoButton.Content = noText;
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Result = System.Windows.MessageBoxResult.Yes;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Result = System.Windows.MessageBoxResult.No;
        Close();
    }
}
