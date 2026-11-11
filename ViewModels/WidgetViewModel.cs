using System.Diagnostics;
using System.Windows.Input;
using CrealitySparkXWidget.Models;
using CrealitySparkXWidget.Services;

namespace CrealitySparkXWidget.ViewModels;

public class WidgetViewModel : BaseViewModel
{
    private readonly PrinterManager _printerManager;
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    private PrinterTelemetry _telemetry = new();
    private bool _isSettingsOpen;

    // Editable settings fields
    private string _settingsHost = string.Empty;
    private int _settingsPort = 7125;
    private int _settingsRefreshInterval = 2;
    private bool _settingsAutoConnect = true;
    private bool _settingsStartWithWindows = false;
    private bool _settingsDemoMode = false;
    private string _settingsWebUrl = string.Empty;

    public PrinterTelemetry Telemetry
    {
        get => _telemetry;
        private set
        {
            _telemetry = value;
            OnPropertyChanged(nameof(Telemetry));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(StatusBadgeText));
            OnPropertyChanged(nameof(TrayTooltipText));
            OnPropertyChanged(nameof(IsPrinting));
            OnPropertyChanged(nameof(IsOffline));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(TimeFormatted));
            OnPropertyChanged(nameof(ETAFormatted));
            OnPropertyChanged(nameof(ProgressInt));
            OnPropertyChanged(nameof(HasCfs));
            OnPropertyChanged(nameof(CfsSlots));
            OnPropertyChanged(nameof(LayerDisplay));
            OnPropertyChanged(nameof(ZDisplay));
            OnPropertyChanged(nameof(SpeedDisplay));
            OnPropertyChanged(nameof(FanDisplay));
            OnPropertyChanged(nameof(SecondarySensorTitle));
            OnPropertyChanged(nameof(SecondarySensorValue));
            OnPropertyChanged(nameof(SecondarySensorSubtext));
            OnPropertyChanged(nameof(DetailedActivityTitle));
            OnPropertyChanged(nameof(DetailedActivityIcon));
            OnPropertyChanged(nameof(DetailedActivityDescription));
            OnPropertyChanged(nameof(DetailedActivityColor));
            OnPropertyChanged(nameof(SlicerMessage));
            OnPropertyChanged(nameof(HasSlicerMessage));
            OnPropertyChanged(nameof(LastUpdatedShort));
            OnPropertyChanged(nameof(LastUpdatedTooltip));
            OnPropertyChanged(nameof(FooterStatusDisplay));
        }
    }

    private System.Windows.Threading.DispatcherTimer? _pollPulseTimer;
    private bool _isPollPulse;
    public bool IsPollPulse
    {
        get => _isPollPulse;
        set => SetProperty(ref _isPollPulse, value);
    }

    public string LastUpdatedShort => Telemetry.LastUpdated > DateTime.MinValue
        ? Telemetry.LastUpdated.ToString("HH:mm:ss")
        : "--:--:--";

    public string LastUpdatedTooltip => Telemetry.LastUpdated > DateTime.MinValue
        ? $"Ultimo aggiornamento telemetria: {Telemetry.LastUpdated:HH:mm:ss}"
        : "In attesa del primo aggiornamento...";

    public string FooterStatusDisplay
    {
        get
        {
            var conn = !string.IsNullOrEmpty(Telemetry.ConnectionDetails) ? Telemetry.ConnectionDetails : "Pronto";
            if (Telemetry.LastUpdated > DateTime.MinValue)
            {
                return $"{conn} • {Telemetry.LastUpdated:HH:mm:ss}";
            }
            return conn;
        }
    }

    private void TriggerPollPulse()
    {
        IsPollPulse = true;
        OnPropertyChanged(nameof(LastUpdatedShort));
        OnPropertyChanged(nameof(LastUpdatedTooltip));
        OnPropertyChanged(nameof(FooterStatusDisplay));

        _pollPulseTimer?.Stop();
        _pollPulseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _pollPulseTimer.Tick += (s, e) =>
        {
            _pollPulseTimer?.Stop();
            IsPollPulse = false;
        };
        _pollPulseTimer.Start();
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public string SettingsHost
    {
        get => _settingsHost;
        set => SetProperty(ref _settingsHost, value);
    }

    public int SettingsPort
    {
        get => _settingsPort;
        set => SetProperty(ref _settingsPort, value);
    }

    public int SettingsRefreshInterval
    {
        get => _settingsRefreshInterval;
        set => SetProperty(ref _settingsRefreshInterval, value);
    }

    public bool SettingsAutoConnect
    {
        get => _settingsAutoConnect;
        set => SetProperty(ref _settingsAutoConnect, value);
    }

    public bool SettingsStartWithWindows
    {
        get => _settingsStartWithWindows;
        set => SetProperty(ref _settingsStartWithWindows, value);
    }

    public bool SettingsDemoMode
    {
        get => _settingsDemoMode;
        set => SetProperty(ref _settingsDemoMode, value);
    }

    public string SettingsWebUrl
    {
        get => _settingsWebUrl;
        set => SetProperty(ref _settingsWebUrl, value);
    }

    public bool IsPrinting => Telemetry.Status == PrinterStatusEnum.Printing || Telemetry.Status == PrinterStatusEnum.Paused;
    public bool IsOffline => !Telemetry.IsConnected || Telemetry.Status == PrinterStatusEnum.Offline;
    public bool IsIdle => Telemetry.IsConnected && (Telemetry.Status == PrinterStatusEnum.Idle || Telemetry.Status == PrinterStatusEnum.Completed);
    public bool HasCfs => Telemetry.HasCfs;
    public List<FilamentSlotInfo> CfsSlots => Telemetry.CfsSlots;

    public int ProgressInt => (int)Math.Round(Telemetry.ProgressPercentage);

    public string LayerDisplay
    {
        get
        {
            if (Telemetry.TotalLayers > 0)
                return $"{Telemetry.CurrentLayer} / {Telemetry.TotalLayers}";
            if (Telemetry.CurrentLayer > 0)
                return $"{Telemetry.CurrentLayer}";
            return "-- / --";
        }
    }

    public string ZDisplay => $"{Telemetry.CurrentZ:F2} mm";

    public string SpeedDisplay => $"{Telemetry.PrintSpeedMmPerSec} mm/s";

    public string FanDisplay => $"{Telemetry.FanSpeedPercent}%";

    public string SecondarySensorTitle => Telemetry.SecondarySensorTitle;
    public string SecondarySensorValue => Telemetry.SecondarySensorValue;
    public string SecondarySensorSubtext => Telemetry.SecondarySensorSubtext;

    public string TimeFormatted
    {
        get
        {
            if (Telemetry.TimeElapsedSeconds <= 0 && Telemetry.TimeRemainingSeconds <= 0)
                return "--";

            var elapsed = Telemetry.TimeElapsed;
            var remaining = Telemetry.TimeRemaining;
            return $"{elapsed.Hours:D2}h {elapsed.Minutes:D2}m / ~{remaining.Hours:D2}h {remaining.Minutes:D2}m";
        }
    }

    public string ETAFormatted => Telemetry.EstimatedFinishTime;

    public string DetailedActivityTitle => Telemetry.DetailedActivityTitle;
    public string DetailedActivityIcon => Telemetry.DetailedActivityIcon;
    public string DetailedActivityDescription => Telemetry.DetailedActivityDescription;
    public string DetailedActivityColor => Telemetry.DetailedActivityColor;
    public string SlicerMessage => Telemetry.SlicerMessage;
    public bool HasSlicerMessage => Telemetry.HasSlicerMessage;

    public string StatusBadgeText
    {
        get
        {
            if (!Telemetry.IsConnected) return "Offline";
            if (IsPrinting)
                return $"{DetailedActivityIcon} {DetailedActivityTitle} ({ProgressInt}%)";
            return $"{DetailedActivityIcon} {DetailedActivityTitle}";
        }
    }

    public string StatusColor => !Telemetry.IsConnected ? "#EF4444" : DetailedActivityColor;

    public string TrayTooltipText
    {
        get
        {
            if (!Telemetry.IsConnected)
                return "Creality SparkX i7 - Disconnesso";

            if (IsPrinting)
            {
                return $"Creality SparkX i7 - {DetailedActivityIcon} {DetailedActivityTitle} ({ProgressInt}%)\n" +
                       $"Ugello: {Telemetry.NozzleTemperature:F0}°C / {Telemetry.TargetNozzleTemperature:F0}°C | Piatto: {Telemetry.BedTemperature:F0}°C\n" +
                       $"Rimanente: {Telemetry.TimeRemaining.Hours:D2}h {Telemetry.TimeRemaining.Minutes:D2}m";
            }

            return $"Creality SparkX i7 - {DetailedActivityIcon} {DetailedActivityTitle}\n" +
                   $"Ugello: {Telemetry.NozzleTemperature:F0}°C | Piatto: {Telemetry.BedTemperature:F0}°C";
        }
    }

    public ICommand ToggleSettingsCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CancelSettingsCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand ToggleDemoModeCommand { get; }
    public ICommand OpenWebDashboardCommand { get; }

    public WidgetViewModel(PrinterManager printerManager, SettingsService settingsService)
    {
        _printerManager = printerManager;
        _settingsService = settingsService;
        _settings = _settingsService.Load();

        // If port was saved as 9999 from previous version, auto-correct to 7125 (Moonraker)
        if (_settings.Port == 9999 || _settings.Port <= 0)
        {
            _settings.Port = 7125;
            _settingsService.Save(_settings);
        }

        LoadSettingsToFields();

        _printerManager.TelemetryUpdated += (t) =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                Telemetry = t;
                TriggerPollPulse();
                App.Log($"UI Telemetry: Activity={DetailedActivityIcon} {DetailedActivityTitle}, Layer={LayerDisplay}, Z={ZDisplay}, Speed={SpeedDisplay}, Fan={FanDisplay}, Sensor={SecondarySensorTitle}:{SecondarySensorValue}, Nozzle={t.NozzleTemperature}°C, Bed={t.BedTemperature}°C, Prog={t.ProgressPercentage}%");
            });
        };

        ToggleSettingsCommand = new RelayCommand(() =>
        {
            if (!IsSettingsOpen)
            {
                LoadSettingsToFields();
            }
            IsSettingsOpen = !IsSettingsOpen;
        });

        SaveSettingsCommand = new RelayCommand(async () =>
        {
            ApplyFieldsToSettings();
            _settingsService.Save(_settings);
            StartupManager.SetRunAtStartup(_settings.StartWithWindows);

            IsSettingsOpen = false;
            await _printerManager.StartAsync(_settings);
        });

        CancelSettingsCommand = new RelayCommand(() =>
        {
            IsSettingsOpen = false;
        });

        ReconnectCommand = new RelayCommand(async () =>
        {
            await _printerManager.StartAsync(_settings);
        });

        ToggleDemoModeCommand = new RelayCommand(async () =>
        {
            _settings.EnableDemoMode = !_settings.EnableDemoMode;
            _settingsService.Save(_settings);
            LoadSettingsToFields();
            await _printerManager.StartAsync(_settings);
        });

        OpenWebDashboardCommand = new RelayCommand(() =>
        {
            var url = _settings.GetWebDashboardUrl();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        });
    }

    public async Task InitializeAsync()
    {
        if (_settings.AutoConnectOnStartup || _settings.EnableDemoMode)
        {
            await _printerManager.StartAsync(_settings);
        }
    }

    private void LoadSettingsToFields()
    {
        SettingsHost = _settings.PrinterHost;
        SettingsPort = _settings.Port == 9999 ? 7125 : _settings.Port;
        SettingsRefreshInterval = _settings.RefreshIntervalSeconds;
        SettingsAutoConnect = _settings.AutoConnectOnStartup;
        SettingsStartWithWindows = _settings.StartWithWindows;
        SettingsDemoMode = _settings.EnableDemoMode;
        SettingsWebUrl = _settings.CustomWebDashboardUrl;
    }

    private void ApplyFieldsToSettings()
    {
        _settings.PrinterHost = SettingsHost.Trim();
        _settings.Port = SettingsPort <= 0 ? 7125 : SettingsPort;
        _settings.RefreshIntervalSeconds = Math.Max(1, SettingsRefreshInterval);
        _settings.AutoConnectOnStartup = SettingsAutoConnect;
        _settings.StartWithWindows = SettingsStartWithWindows;
        _settings.EnableDemoMode = SettingsDemoMode;
        _settings.CustomWebDashboardUrl = SettingsWebUrl.Trim();
    }
}
