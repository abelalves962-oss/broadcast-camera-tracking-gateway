using System.Windows;
using System.Windows.Input;
using Cam4TrackingMonitor.Services;

namespace Cam4TrackingMonitor.Views;

public partial class PasswordSetupWindow : Window
{
    private readonly Func<string, bool> _create;

    public PasswordSetupWindow(Func<string, bool> create)
    {
        InitializeComponent();
        _create = create;
        Loaded += (_, _) => NewPasswordInput.Focus();
    }

    private void Create_Click(object sender, RoutedEventArgs e) => TryCreate();
    private void ConfirmPasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TryCreate();
        e.Handled = true;
    }

    private void TryCreate()
    {
        var password = NewPasswordInput.Password;
        var confirmation = ConfirmPasswordInput.Password;
        NewPasswordInput.Clear();
        ConfirmPasswordInput.Clear();
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ErrorText.Text = "As senhas não coincidem.";
            NewPasswordInput.Focus();
            return;
        }
        if (password.Length < MaintenanceAuthorizationService.MinimumPasswordLength)
        {
            ErrorText.Text = "A senha deve possuir pelo menos 6 caracteres.";
            NewPasswordInput.Focus();
            return;
        }
        if (!_create(password))
        {
            ErrorText.Text = "Não foi possível criar a senha de manutenção.";
            NewPasswordInput.Focus();
            return;
        }
        DialogResult = true;
    }
}
