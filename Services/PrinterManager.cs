using CrealitySparkXWidget.Models;

namespace CrealitySparkXWidget.Services;

public class PrinterManager : IDisposable
{
    private IPrinterService? _activeService;
    private readonly DemoPrinterService _demoService = new();
    private readonly MoonrakerService _moonrakerService = new();

    public event Action<PrinterTelemetry>? TelemetryUpdated;
    public event Action<string>? StatusChanged;

    public bool IsConnected => _activeService?.IsConnected ?? false;
    public string ActiveServiceName => _activeService?.ServiceName ?? "Nessuno";

    public PrinterManager()
    {
        _demoService.TelemetryUpdated += (t) => TelemetryUpdated?.Invoke(t);
        _demoService.StatusChanged += (s) => StatusChanged?.Invoke(s);

        _moonrakerService.TelemetryUpdated += (t) => TelemetryUpdated?.Invoke(t);
        _moonrakerService.StatusChanged += (s) => StatusChanged?.Invoke(s);
    }

    public async Task StartAsync(AppSettings settings)
    {
        await StopAsync();

        if (settings.EnableDemoMode)
        {
            _activeService = _demoService;
            await _demoService.ConnectAsync(settings);
            return;
        }

        // Standard connection: Moonraker
        _activeService = _moonrakerService;
        await _moonrakerService.ConnectAsync(settings);
    }

    public async Task StopAsync()
    {
        if (_activeService != null)
        {
            await _activeService.DisconnectAsync();
            _activeService = null;
        }
    }

    public void Dispose()
    {
        _demoService.Dispose();
        _moonrakerService.Dispose();
    }
}
