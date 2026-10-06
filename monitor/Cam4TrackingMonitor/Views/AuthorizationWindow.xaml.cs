using System.Windows;
using System.Windows.Input;

namespace Cam4TrackingMonitor.Views;

public partial class AuthorizationWindow : Window
{
    private readonly Func<string, bool> _verify;

    public AuthorizationWindow(Func<string, bool> verify)
    {
        InitializeComponent();
        _verify = verify;
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void Authorize_Click(object sender, RoutedEventArgs e) => TryAuthorize();

    private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TryAuthorize();
        e.Handled = true;
    }

    private void TryAuthorize()
    {
        var password = PasswordInput.Password;
        PasswordInput.Clear();
        if (!_verify(password))
        {
            ErrorText.Text = "Senha incorreta.";
            PasswordInput.Focus();
            return;
        }
        DialogResult = true;
    }
}
