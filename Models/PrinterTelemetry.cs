using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrealitySparkXWidget.Models;

public class PrinterTelemetry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    private PrinterStatusEnum _status = PrinterStatusEnum.Offline;
    public PrinterStatusEnum Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(EstimatedFinishTime));
            }
        }
    }

    private string _statusText = "Disconnesso";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _connectionDetails = string.Empty;
    public string ConnectionDetails
    {
        get => _connectionDetails;
        set => SetProperty(ref _connectionDetails, value);
    }

    // Detailed Activity / Phase Tracking
    private string _detailedActivityTitle = "Inattiva";
    public string DetailedActivityTitle
    {
        get => _detailedActivityTitle;
        set => SetProperty(ref _detailedActivityTitle, value);
    }

    private string _detailedActivityIcon = "💤";
    public string DetailedActivityIcon
    {
        get => _detailedActivityIcon;
        set => SetProperty(ref _detailedActivityIcon, value);
    }

    private string _detailedActivityDescription = "Stampante pronta per un nuovo lavoro";
    public string DetailedActivityDescription
    {
        get => _detailedActivityDescription;
        set => SetProperty(ref _detailedActivityDescription, value);
    }

    private string _detailedActivityColor = "#64748B";
    public string DetailedActivityColor
    {
        get => _detailedActivityColor;
        set => SetProperty(ref _detailedActivityColor, value);
    }

    private string _slicerMessage = string.Empty;
    public string SlicerMessage
    {
        get => _slicerMessage;
        set
        {
            if (SetProperty(ref _slicerMessage, value))
            {
                OnPropertyChanged(nameof(HasSlicerMessage));
            }
        }
    }

    public bool HasSlicerMessage => !string.IsNullOrWhiteSpace(SlicerMessage);

    // Job Stats
    private string _currentFile = string.Empty;
    public string CurrentFile
    {
        get => _currentFile;
        set => SetProperty(ref _currentFile, value);
    }

    private double _progressPercentage;
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    private long _timeElapsedSeconds;
    public long TimeElapsedSeconds
    {
        get => _timeElapsedSeconds;
        set
        {
            if (SetProperty(ref _timeElapsedSeconds, value))
            {
                OnPropertyChanged(nameof(TimeElapsed));
            }
        }
    }

    private long _timeRemainingSeconds;
    public long TimeRemainingSeconds
    {
        get => _timeRemainingSeconds;
        set
        {
            if (SetProperty(ref _timeRemainingSeconds, value))
            {
                OnPropertyChanged(nameof(TimeRemaining));
                OnPropertyChanged(nameof(EstimatedFinishTime));
            }
        }
    }

    private int _currentLayer;
    public int CurrentLayer
    {
        get => _currentLayer;
        set => SetProperty(ref _currentLayer, value);
    }

    private int _totalLayers;
    public int TotalLayers
    {
        get => _totalLayers;
        set => SetProperty(ref _totalLayers, value);
    }

    private double _currentZ;
    public double CurrentZ
    {
        get => _currentZ;
        set => SetProperty(ref _currentZ, value);
    }

    private int _printSpeedMmPerSec;
    public int PrintSpeedMmPerSec
    {
        get => _printSpeedMmPerSec;
        set => SetProperty(ref _printSpeedMmPerSec, value);
    }

    // Temperatures (°C)
    private double _nozzleTemperature;
    public double NozzleTemperature
    {
        get => _nozzleTemperature;
        set => SetProperty(ref _nozzleTemperature, value);
    }

    private double _targetNozzleTemperature;
    public double TargetNozzleTemperature
    {
        get => _targetNozzleTemperature;
        set => SetProperty(ref _targetNozzleTemperature, value);
    }

    private double _bedTemperature;
    public double BedTemperature
    {
        get => _bedTemperature;
        set => SetProperty(ref _bedTemperature, value);
    }

    private double _targetBedTemperature;
    public double TargetBedTemperature
    {
        get => _targetBedTemperature;
        set => SetProperty(ref _targetBedTemperature, value);
    }

    private double? _chamberTemperature;
    public double? ChamberTemperature
    {
        get => _chamberTemperature;
        set
        {
            if (SetProperty(ref _chamberTemperature, value))
            {
                OnPropertyChanged(nameof(SecondarySensorTitle));
                OnPropertyChanged(nameof(SecondarySensorValue));
                OnPropertyChanged(nameof(SecondarySensorSubtext));
            }
        }
    }

    private double? _mcuTemperature;
    public double? McuTemperature
    {
        get => _mcuTemperature;
        set
        {
            if (SetProperty(ref _mcuTemperature, value))
            {
                OnPropertyChanged(nameof(SecondarySensorTitle));
                OnPropertyChanged(nameof(SecondarySensorValue));
                OnPropertyChanged(nameof(SecondarySensorSubtext));
            }
        }
    }

    // Fans & Accessories
    private int _fanSpeedPercent;
    public int FanSpeedPercent
    {
        get => _fanSpeedPercent;
        set => SetProperty(ref _fanSpeedPercent, value);
    }

    private int? _fanRpm;
    public int? FanRpm
    {
        get => _fanRpm;
        set => SetProperty(ref _fanRpm, value);
    }

    private int? _auxFanSpeedPercent;
    public int? AuxFanSpeedPercent
    {
        get => _auxFanSpeedPercent;
        set => SetProperty(ref _auxFanSpeedPercent, value);
    }

    private bool _lightOn;
    public bool LightOn
    {
        get => _lightOn;
        set => SetProperty(ref _lightOn, value);
    }

    // Multi-color CFS (Color Filament System)
    private List<FilamentSlotInfo> _cfsSlots = new();
    public List<FilamentSlotInfo> CfsSlots
    {
        get => _cfsSlots;
        set
        {
            if (SetProperty(ref _cfsSlots, value))
            {
                OnPropertyChanged(nameof(HasCfs));
            }
        }
    }

    public bool HasCfs => CfsSlots != null && CfsSlots.Count > 0;

    private DateTime _lastUpdated = DateTime.MinValue;
    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    public TimeSpan TimeElapsed => TimeSpan.FromSeconds(Math.Max(0, TimeElapsedSeconds));
    public TimeSpan TimeRemaining => TimeSpan.FromSeconds(Math.Max(0, TimeRemainingSeconds));

    public string EstimatedFinishTime
    {
        get
        {
            if (Status != PrinterStatusEnum.Printing && Status != PrinterStatusEnum.Paused)
                return "--:--";

            if (TimeRemainingSeconds <= 0)
                return "--:--";

            var finish = DateTime.Now.AddSeconds(TimeRemainingSeconds);
            return finish.ToString("HH:mm");
        }
    }

    public string SecondarySensorTitle =>
        (ChamberTemperature.HasValue && ChamberTemperature.Value > 0) ? "🏠 Camera" :
        (McuTemperature.HasValue && McuTemperature.Value > 0) ? "💻 MCU" : "🏠 Camera";

    public string SecondarySensorValue =>
        (ChamberTemperature.HasValue && ChamberTemperature.Value > 0) ? $"{ChamberTemperature.Value:F1} °C" :
        (McuTemperature.HasValue && McuTemperature.Value > 0) ? $"{McuTemperature.Value:F1} °C" : "-- °C";

    public string SecondarySensorSubtext =>
        (ChamberTemperature.HasValue && ChamberTemperature.Value > 0) ? "Temperatura box" :
        (McuTemperature.HasValue && McuTemperature.Value > 0) ? "Scheda madre" : "Non rilevato";

    public PrinterTelemetry Clone()
    {
        return new PrinterTelemetry
        {
            IsConnected = this.IsConnected,
            Status = this.Status,
            StatusText = this.StatusText,
            ConnectionDetails = this.ConnectionDetails,
            DetailedActivityTitle = this.DetailedActivityTitle,
            DetailedActivityIcon = this.DetailedActivityIcon,
            DetailedActivityDescription = this.DetailedActivityDescription,
            DetailedActivityColor = this.DetailedActivityColor,
            SlicerMessage = this.SlicerMessage,
            CurrentFile = this.CurrentFile,
            ProgressPercentage = this.ProgressPercentage,
            TimeElapsedSeconds = this.TimeElapsedSeconds,
            TimeRemainingSeconds = this.TimeRemainingSeconds,
            CurrentLayer = this.CurrentLayer,
            TotalLayers = this.TotalLayers,
            CurrentZ = this.CurrentZ,
            PrintSpeedMmPerSec = this.PrintSpeedMmPerSec,
            NozzleTemperature = this.NozzleTemperature,
            TargetNozzleTemperature = this.TargetNozzleTemperature,
            BedTemperature = this.BedTemperature,
            TargetBedTemperature = this.TargetBedTemperature,
            ChamberTemperature = this.ChamberTemperature,
            McuTemperature = this.McuTemperature,
            FanSpeedPercent = this.FanSpeedPercent,
            FanRpm = this.FanRpm,
            AuxFanSpeedPercent = this.AuxFanSpeedPercent,
            LightOn = this.LightOn,
            CfsSlots = new List<FilamentSlotInfo>(this.CfsSlots),
            LastUpdated = this.LastUpdated
        };
    }
}
