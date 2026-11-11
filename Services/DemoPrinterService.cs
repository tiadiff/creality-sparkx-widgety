using CrealitySparkXWidget.Models;

namespace CrealitySparkXWidget.Services;

public class DemoPrinterService : IPrinterService
{
    private CancellationTokenSource? _cts;
    private readonly PrinterTelemetry _telemetry = new();
    private readonly Random _random = new();

    public event Action<PrinterTelemetry>? TelemetryUpdated;
    public event Action<string>? StatusChanged;

    public bool IsConnected { get; private set; }
    public string ServiceName => "Modalità Simulazione (Demo)";

    public Task ConnectAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsConnected = true;
        StatusChanged?.Invoke("Simulatore avviato");

        _telemetry.IsConnected = true;
        _telemetry.ConnectionDetails = "Simulazione Creality SparkX i7";
        _telemetry.CurrentFile = "SparkX_i7_Benchy_Speed.gcode";
        _telemetry.Status = PrinterStatusEnum.Printing;
        _telemetry.StatusText = "In Stampa";
        _telemetry.ProgressPercentage = 54.0;
        _telemetry.CurrentLayer = 168;
        _telemetry.TotalLayers = 310;
        _telemetry.CurrentZ = 33.6;
        _telemetry.PrintSpeedMmPerSec = 350;
        _telemetry.TimeElapsedSeconds = 1120; // ~18 min
        _telemetry.TimeRemainingSeconds = 950;  // ~15 min
        _telemetry.NozzleTemperature = 220.0;
        _telemetry.TargetNozzleTemperature = 220.0;
        _telemetry.BedTemperature = 60.0;
        _telemetry.TargetBedTemperature = 60.0;
        _telemetry.ChamberTemperature = 38.2;
        _telemetry.FanSpeedPercent = 100;
        _telemetry.AuxFanSpeedPercent = 70;
        _telemetry.LastUpdated = DateTime.Now;

        _telemetry.CfsSlots = new List<FilamentSlotInfo>
        {
            new() { SlotIndex = 1, ColorHex = "#00D26A", Material = "Hyper PLA", IsActive = true, RemainingPercent = 82 },
            new() { SlotIndex = 2, ColorHex = "#1E293B", Material = "PETG", IsActive = false, RemainingPercent = 65 },
            new() { SlotIndex = 3, ColorHex = "#E2E8F0", Material = "PLA-CF", IsActive = false, RemainingPercent = 40 },
            new() { SlotIndex = 4, ColorHex = "#EF4444", Material = "TPU 95A", IsActive = false, RemainingPercent = 95 }
        };

        _telemetry.DetailedActivityTitle = "In Estrusione (Pareti)";
        _telemetry.DetailedActivityIcon = "🧵";
        _telemetry.DetailedActivityColor = "#00D26A";
        _telemetry.DetailedActivityDescription = "Stampa perimetri esterni ad alta precisione (350 mm/s)";
        _telemetry.SlicerMessage = "Printing Outer Wall";

        _ = RunSimulationLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    private int _demoPhaseIndex = 0;
    private static readonly (string Title, string Icon, string Color, string Desc, string SlicerMsg)[] DemoPhases = new[]
    {
        ("In Estrusione (Pareti)", "🧵", "#00D26A", "Stampa perimetri esterni ad alta precisione (350 mm/s)", "Printing Outer Wall"),
        ("Riempimento (Infill)", "🧵", "#10B981", "Estrusione reticolo gyroide interno (340 mm/s)", "Printing Infill"),
        ("Stampa Supporti", "🧱", "#0284C7", "Costruzione strutture ad albero per parti a sbalzo", "Printing Tree Supports"),
        ("Torre di Spurgo (Prime Tower)", "🗼", "#8B5CF6", "Stampa torre di compensazione multi-colore CFS", "Printing Wipe Tower"),
        ("Spurgo & Pulizia Ugello", "🔄", "#06B6D4", "Flusso di spurgo e pulizia nello scivolo di scarico", "Purging filament"),
        ("Cambio Filamento (CFS)", "🎨", "#A855F7", "Alimentazione slot 1 Hyper PLA nel condotto", "CFS Load Material")
    };

    private async Task RunSimulationLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, token);

                if (_telemetry.Status == PrinterStatusEnum.Printing)
                {
                    _telemetry.ProgressPercentage = Math.Min(100.0, _telemetry.ProgressPercentage + 0.3);
                    _telemetry.TimeElapsedSeconds += 2;
                    _telemetry.TimeRemainingSeconds = Math.Max(0, _telemetry.TimeRemainingSeconds - 2);

                    if (_random.NextDouble() > 0.4 && _telemetry.CurrentLayer < _telemetry.TotalLayers)
                    {
                        _telemetry.CurrentLayer++;
                        _telemetry.CurrentZ = Math.Round(_telemetry.CurrentLayer * 0.2, 1);
                    }

                    // Cycle demo activities every few steps
                    _demoPhaseIndex = (_demoPhaseIndex + 1) % DemoPhases.Length;
                    var phase = DemoPhases[_demoPhaseIndex];
                    _telemetry.DetailedActivityTitle = phase.Title;
                    _telemetry.DetailedActivityIcon = phase.Icon;
                    _telemetry.DetailedActivityColor = phase.Color;
                    _telemetry.DetailedActivityDescription = phase.Desc;
                    _telemetry.SlicerMessage = phase.SlicerMsg;

                    // Realistic small fluctuations
                    _telemetry.NozzleTemperature = Math.Round(220.0 + (_random.NextDouble() * 1.2 - 0.6), 1);
                    _telemetry.BedTemperature = Math.Round(60.0 + (_random.NextDouble() * 0.4 - 0.2), 1);
                    _telemetry.ChamberTemperature = Math.Round(38.0 + (_random.NextDouble() * 0.6 - 0.3), 1);
                    _telemetry.PrintSpeedMmPerSec = _random.Next(320, 360);

                    if (_telemetry.ProgressPercentage >= 100.0)
                    {
                        _telemetry.Status = PrinterStatusEnum.Completed;
                        _telemetry.StatusText = "Stampa Completata!";
                        _telemetry.TargetNozzleTemperature = 0;
                        _telemetry.TargetBedTemperature = 0;
                        _telemetry.TimeRemainingSeconds = 0;
                    }
                }
                else if (_telemetry.Status == PrinterStatusEnum.Completed)
                {
                    // Cool down
                    _telemetry.NozzleTemperature = Math.Max(30, _telemetry.NozzleTemperature - 2.5);
                    _telemetry.BedTemperature = Math.Max(25, _telemetry.BedTemperature - 0.8);
                }

                _telemetry.LastUpdated = DateTime.Now;
                TelemetryUpdated?.Invoke(_telemetry.Clone());
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore simulation loop errors
            }
        }
    }

    public Task DisconnectAsync()
    {
        try
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch { }

        IsConnected = false;
        _telemetry.IsConnected = false;
        _telemetry.Status = PrinterStatusEnum.Offline;
        _telemetry.StatusText = "Disconnesso";
        TelemetryUpdated?.Invoke(_telemetry);
        StatusChanged?.Invoke("Simulatore arrestato");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            if (_cts != null && !_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch { }
        try
        {
            _cts?.Dispose();
        }
        catch { }
        _cts = null;
    }
}

