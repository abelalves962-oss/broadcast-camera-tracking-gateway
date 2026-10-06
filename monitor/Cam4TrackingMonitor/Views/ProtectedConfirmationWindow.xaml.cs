using System.Windows;

namespace Cam4TrackingMonitor.Views;

public partial class ProtectedConfirmationWindow : Window
{
    public ProtectedConfirmationWindow(string title, string message, string confirmText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        Loaded += (_, _) => Focus();
    }

    public static bool Confirm(string title, string message, string confirmText)
    {
        var dialog = new ProtectedConfirmationWindow(title, message, confirmText)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
