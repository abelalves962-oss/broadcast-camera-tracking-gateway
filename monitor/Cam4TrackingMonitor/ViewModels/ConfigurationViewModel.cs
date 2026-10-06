using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Services;
using Cam4TrackingMonitor.Views;

namespace Cam4TrackingMonitor.ViewModels;

public sealed class DestinationEditRow : ObservableObject
{
    private string _newIp = "0.0.0.0";
    private int _newPort;
    public int Number { get; init; }
    public bool CurrentLoaded { get; init; }
    public string CurrentIp { get; set; } = "N/A";
    public string NewIp { get => _newIp; set { if (Set(ref _newIp, value)) { OnPropertyChanged(nameof(IsChanged)); OnPropertyChanged(nameof(ChangeStatus)); } } }
    public int CurrentPort { get; set; }
    public int NewPort { get => _newPort; set { if (Set(ref _newPort, value)) { OnPropertyChanged(nameof(IsChanged)); OnPropertyChanged(nameof(ChangeStatus)); } } }
    public bool IsChanged => CurrentLoaded && (!string.Equals(CurrentIp, NewIp, StringComparison.OrdinalIgnoreCase) || CurrentPort != NewPort);
    public string ChangeStatus => IsChanged ? "ALTERADO" : string.Empty;
}

public sealed class ConfigurationViewModel : ObservableObject
{
    private readonly ConfigurationService _service;
    private readonly MaintenanceAuthorizationService _authorization;
    private readonly DispatcherTimer _maintenanceTimer;
    private TrackingBoxConfiguration? _current;
    private TrackingBoxConfiguration _edited = new();
    private string _status = "Clique em Ler da Tracking Box.";
    public TrackingBoxConfiguration? CurrentConfiguration { get => _current; private set => Set(ref _current, value); }
    public TrackingBoxConfiguration EditedConfiguration { get => _edited; private set => Set(ref _edited, value); }
    public ObservableCollection<DestinationEditRow> Destinations { get; } = new();
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string MaintenanceStatus => _authorization.IsAuthorized
        ? $"Manutenção autorizada · Expira em: {_authorization.Remaining:mm\\:ss}"
        : "Alterações protegidas";
    public AsyncRelayCommand ReadCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand RebootCommand { get; }
    public AsyncRelayCommand DefaultCommand { get; }
    public RelayCommand ChangePasswordCommand { get; }
    public event EventHandler<string>? HostChanged;
    public event EventHandler<TrackingBoxTelemetry>? TelemetryValidated;

    public ConfigurationViewModel(ConfigurationService service, MaintenanceAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
        ReadCommand = new AsyncRelayCommand(Read);
        ApplyCommand = new AsyncRelayCommand(Apply);
        SaveCommand = new AsyncRelayCommand(Save);
        RebootCommand = new AsyncRelayCommand(Reboot);
        DefaultCommand = new AsyncRelayCommand(RestoreDefault);
        ChangePasswordCommand = new RelayCommand(_authorization.ChangePassword);
        _maintenanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _maintenanceTimer.Tick += (_, _) => OnPropertyChanged(nameof(MaintenanceStatus));
        _maintenanceTimer.Start();
        LoadRows(_edited, null);
    }

    private async Task Read()
    {
        Status = "Lendo configuração...";
        var result = await _service.ReadAsync();
        Status = result.Message;
        if (result.Success && result.Configuration is not null) SetConfiguration(result.Configuration);
    }

    private async Task Apply()
    {
        if (!_authorization.EnsureAuthorized()) return;
        OnPropertyChanged(nameof(MaintenanceStatus));
        if (CurrentConfiguration is null) { Status = "Leia a configuração antes de aplicar alterações."; return; }
        SyncRowsToEdited();
        if (ConfigurationService.HasNetworkChanges(CurrentConfiguration, EditedConfiguration))
        {
            var text = $"Alterações de rede serão aplicadas inicialmente em RAM.\n\nIP atual: {CurrentConfiguration.Ip}\nNovo IP: {EditedConfiguration.Ip}\n\nÉ necessário Salvar e Reiniciar para a nova rede entrar em vigor. A conexão poderá ser perdida.\n\nDeseja aplicar?";
            if (MessageBox.Show(text, "Confirmar alteração de rede", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        }
        Status = "Aplicando alterações em RAM...";
        var result = await _service.ApplyAsync(CurrentConfiguration, EditedConfiguration);
        Status = result.Message;
        if (result.Success) CurrentConfiguration = EditedConfiguration.Clone();
    }

    private async Task Save()
    {
        if (!_authorization.EnsureAuthorized()) return;
        OnPropertyChanged(nameof(MaintenanceStatus));
        if (MessageBox.Show("Salvar a configuração atual na EEPROM/Flash? O ACK não é garantia absoluta de persistência.", "Salvar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var result = await _service.SaveAsync();
        Status = result.Success ? $"ACK de SAVE recebido: {result.Response.Trim()}" : $"Falha no SAVE: {result.Error}";
    }

    private async Task RestoreDefault()
    {
        if (!_authorization.EnsureAuthorized()) return;
        OnPropertyChanged(nameof(MaintenanceStatus));
        const string message = "Restaurar configuração padrão?\n\nOs valores padrão serão carregados na Tracking Box.\n\nA configuração atualmente salva na EEPROM não será substituída até que seja executado o comando Salvar.\n\nDeseja continuar?";
        if (!ProtectedConfirmationWindow.Confirm("Restaurar configuração padrão", message, "Restaurar")) return;
        var result = await _service.RestoreDefaultAsync();
        Status = result.Success ? result.Response.Trim() : $"Falha: {result.Error}";
        if (result.Success) await Read();
    }

    private async Task Reboot()
    {
        if (!_authorization.EnsureAuthorized()) return;
        OnPropertyChanged(nameof(MaintenanceStatus));
        SyncRowsToEdited();
        var reconnectHost = CurrentConfiguration?.Ip ?? EditedConfiguration.Ip;
        if (!ProtectedConfirmationWindow.Confirm("Reiniciar", "Reiniciar a Tracking Box?", "Reiniciar")) return;
        Status = "REBOOTING — aguardando reconexão...";
        var result = await _service.RebootAndReconnectAsync(reconnectHost);
        Status = result.Message;
        if (result.Success && result.Configuration is not null)
        {
            SetConfiguration(result.Configuration);
            // O endpoint validado pode diferir do IP reportado na configuração. Nunca redirecionar
            // o polling para um endereço que não respondeu a ?? e ?D.
            HostChanged?.Invoke(this, _service.ActiveHost);
            if (result.Telemetry is not null) TelemetryValidated?.Invoke(this, result.Telemetry);
        }
    }

    private void SetConfiguration(TrackingBoxConfiguration configuration)
    {
        CurrentConfiguration = configuration;
        EditedConfiguration = configuration.Clone();
        LoadRows(EditedConfiguration, CurrentConfiguration);
        OnPropertyChanged(nameof(CurrentConfiguration)); OnPropertyChanged(nameof(EditedConfiguration));
    }

    private void LoadRows(TrackingBoxConfiguration edited, TrackingBoxConfiguration? current)
    {
        Destinations.Clear();
        for (var index = 0; index < 7; index++) Destinations.Add(new DestinationEditRow { Number = index + 1, CurrentLoaded = current is not null, CurrentIp = current?.Destinations[index].Ip ?? "N/A", NewIp = edited.Destinations[index].Ip, CurrentPort = current?.Destinations[index].Port ?? 0, NewPort = edited.Destinations[index].Port });
    }

    private void SyncRowsToEdited()
    {
        foreach (var row in Destinations) { EditedConfiguration.Destinations[row.Number - 1].Ip = row.NewIp; EditedConfiguration.Destinations[row.Number - 1].Port = row.NewPort; }
        EditedConfiguration.AxisOrder = EditedConfiguration.AxisOrder.ToUpperInvariant();
        EditedConfiguration.AngleOrder = EditedConfiguration.AngleOrder.ToUpperInvariant();
    }
}
