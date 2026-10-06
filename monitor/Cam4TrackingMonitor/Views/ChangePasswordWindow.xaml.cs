using System.Windows;
using System.Windows.Input;
using Cam4TrackingMonitor.Services;

namespace Cam4TrackingMonitor.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly Func<string, string, PasswordChangeResult> _change;

    internal ChangePasswordWindow(Func<string, string, PasswordChangeResult> change)
    {
        InitializeComponent();
        _change = change;
        Loaded += (_, _) => CurrentPasswordInput.Focus();
    }

    private void Change_Click(object sender, RoutedEventArgs e) => TryChange();
    private void ConfirmPasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TryChange();
        e.Handled = true;
    }

    private void TryChange()
    {
        var currentPassword = CurrentPasswordInput.Password;
        var newPassword = NewPasswordInput.Password;
        var confirmation = ConfirmPasswordInput.Password;
        CurrentPasswordInput.Clear();
        NewPasswordInput.Clear();
        ConfirmPasswordInput.Clear();
        if (newPassword.Length < MaintenanceAuthorizationService.MinimumPasswordLength)
        {
            ErrorText.Text = "A senha deve possuir pelo menos 6 caracteres.";
            NewPasswordInput.Focus();
            return;
        }
        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
        {
            ErrorText.Text = "As senhas não coincidem.";
            NewPasswordInput.Focus();
            return;
        }
        var result = _change(currentPassword, newPassword);
        if (result == PasswordChangeResult.IncorrectCurrentPassword)
        {
            ErrorText.Text = "Senha atual incorreta.";
            CurrentPasswordInput.Focus();
            return;
        }
        if (result != PasswordChangeResult.Success)
        {
            ErrorText.Text = "Não foi possível alterar a senha de manutenção.";
            CurrentPasswordInput.Focus();
            return;
        }
        DialogResult = true;
    }
}
