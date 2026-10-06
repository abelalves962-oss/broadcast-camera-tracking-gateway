using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Cam4TrackingMonitor.Views;

namespace Cam4TrackingMonitor.Services;

internal enum PasswordChangeResult { Success, IncorrectCurrentPassword, Failed }

public sealed class MaintenanceAuthorizationService
{
    internal const int MinimumPasswordLength = 6;
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly TimeSpan SessionDuration = TimeSpan.FromMinutes(10);
    private readonly string _settingsPath;
    private DateTimeOffset? _authorizedUntil;

    public MaintenanceAuthorizationService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cam4TrackingMonitor", "maintenance-auth.json"))
    {
    }

    internal MaintenanceAuthorizationService(string settingsPath) => _settingsPath = settingsPath;

    public bool IsAuthorized
    {
        get
        {
            if (!File.Exists(_settingsPath) || _authorizedUntil is null || DateTimeOffset.Now >= _authorizedUntil.Value)
            {
                _authorizedUntil = null;
                return false;
            }
            return true;
        }
    }

    public TimeSpan Remaining => IsAuthorized ? _authorizedUntil!.Value - DateTimeOffset.Now : TimeSpan.Zero;

    public bool EnsureAuthorized()
    {
        if (IsAuthorized) return true;
        if (!File.Exists(_settingsPath))
        {
            var setupDialog = new PasswordSetupWindow(CreatePassword) { Owner = Application.Current.MainWindow };
            if (setupDialog.ShowDialog() != true) return false;
            _authorizedUntil = DateTimeOffset.Now.Add(SessionDuration);
            return true;
        }

        var dialog = new AuthorizationWindow(VerifyPassword) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return false;
        _authorizedUntil = DateTimeOffset.Now.Add(SessionDuration);
        return true;
    }

    public void ChangePassword()
    {
        if (!File.Exists(_settingsPath))
        {
            _authorizedUntil = null;
            MessageBox.Show("Senha de manutenção ainda não configurada. Use uma ação protegida para criar a senha.",
                "Alterar senha de manutenção", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new ChangePasswordWindow(ChangePasswordCredentials) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() == true) _authorizedUntil = DateTimeOffset.Now.Add(SessionDuration);
    }

    internal bool VerifyPassword(string password)
    {
        byte[]? actual = null;
        try
        {
            if (!File.Exists(_settingsPath)) return false;
            var settings = JsonSerializer.Deserialize<AuthSettings>(File.ReadAllText(_settingsPath));
            if (!IsValid(settings)) return false;
            var salt = Convert.FromBase64String(settings!.Salt);
            var expected = Convert.FromBase64String(settings.Hash);
            try
            {
                actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, settings.Iterations,
                    HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
                CryptographicOperations.ZeroMemory(expected);
            }
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
        finally
        {
            if (actual is not null) CryptographicOperations.ZeroMemory(actual);
        }
    }

    internal bool CreatePassword(string password) =>
        password.Length >= MinimumPasswordLength && !File.Exists(_settingsPath) && WritePassword(password, false);

    internal PasswordChangeResult ChangePasswordCredentials(string currentPassword, string newPassword)
    {
        if (!VerifyPassword(currentPassword)) return PasswordChangeResult.IncorrectCurrentPassword;
        if (newPassword.Length < MinimumPasswordLength || !File.Exists(_settingsPath)) return PasswordChangeResult.Failed;
        return WritePassword(newPassword, true) ? PasswordChangeResult.Success : PasswordChangeResult.Failed;
    }

    private bool WritePassword(string password, bool overwrite)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(directory)) return false;
        byte[]? salt = null;
        byte[]? hash = null;
        var temporaryPath = _settingsPath + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            salt = RandomNumberGenerator.GetBytes(SaltSize);
            hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            var settings = new AuthSettings(1, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            if (!overwrite)
            {
                using var stream = new FileStream(_settingsPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream);
                writer.Write(json);
                return true;
            }
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, true);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (CryptographicException) { return false; }
        finally
        {
            if (salt is not null) CryptographicOperations.ZeroMemory(salt);
            if (hash is not null) CryptographicOperations.ZeroMemory(hash);
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch (IOException) { }
        }
    }

    private static bool IsValid(AuthSettings? settings)
    {
        if (settings is null || settings.Version != 1 || settings.Iterations < 1) return false;
        try
        {
            return Convert.FromBase64String(settings.Salt).Length == SaltSize &&
                   Convert.FromBase64String(settings.Hash).Length == HashSize;
        }
        catch (FormatException) { return false; }
    }

    private sealed record AuthSettings(int Version, int Iterations, string Salt, string Hash);
}
