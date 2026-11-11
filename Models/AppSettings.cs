namespace CrealitySparkXWidget.Models;

public class AppSettings
{
    public string PrinterHost { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 7125;
    public int RefreshIntervalSeconds { get; set; } = 2;
    public bool AutoConnectOnStartup { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string CustomWebDashboardUrl { get; set; } = string.Empty;
    public bool EnableDemoMode { get; set; } = false;

    public string GetWebDashboardUrl()
    {
        if (!string.IsNullOrWhiteSpace(CustomWebDashboardUrl))
            return CustomWebDashboardUrl;

        if (string.IsNullOrWhiteSpace(PrinterHost) || PrinterHost == "127.0.0.1" || PrinterHost == "localhost")
            return "http://localhost:7125";

        // Often Creality Fluidd/Web UI is on 4408, Moonraker is on 7125
        return $"http://{PrinterHost}:4408";
    }
}
