using CrealitySparkXWidget.Models;

namespace CrealitySparkXWidget.Services;

public interface IPrinterService : IDisposable
{
    event Action<PrinterTelemetry>? TelemetryUpdated;
    event Action<string>? StatusChanged;
    
    bool IsConnected { get; }
    string ServiceName { get; }
    
    Task ConnectAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

