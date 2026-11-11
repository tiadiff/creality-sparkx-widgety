using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using CrealitySparkXWidget.Helpers;
using CrealitySparkXWidget.Services;
using CrealitySparkXWidget.ViewModels;
using CrealitySparkXWidget.Views;

namespace CrealitySparkXWidget;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private NotifyIcon? _notifyIcon;
    private WidgetWindow? _widgetWindow;
    private WidgetViewModel? _viewModel;
    private PrinterManager? _printerManager;
    private SettingsService? _settingsService;
    private ToolStripMenuItem? _demoMenuItem;

    public static void Log(string msg)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrealitySparkXWidget");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "app.log"), $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        Log("OnStartup starting");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log($"Unhandled AppDomain exception: {args.ExceptionObject}");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log($"DispatcherUnhandledException: {args.Exception}");
            args.Handled = true;
        };

        const string mutexName = "CrealitySparkXWidget_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            Log("Another instance is already running via Mutex. Exiting.");
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            IconHelper.EnsureIconFile();

            _settingsService = new SettingsService();
            _printerManager = new PrinterManager();
            _viewModel = new WidgetViewModel(_printerManager, _settingsService);
            _widgetWindow = new WidgetWindow(_viewModel);

            SetupNotifyIcon();

            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(WidgetViewModel.TrayTooltipText))
                {
                    UpdateTrayTooltip(_viewModel.TrayTooltipText);
                }
                if (args.PropertyName == nameof(WidgetViewModel.SettingsDemoMode))
                {
                    if (_demoMenuItem != null)
                    {
                        _demoMenuItem.Checked = _viewModel.SettingsDemoMode;
                    }
                }
            };

            Log("Initializing ViewModel & connecting...");
            await _viewModel.InitializeAsync();
            Log("Startup completed. Running in tray.");

            if (e.Args.Contains("--show"))
            {
                _widgetWindow?.ShowAboveTaskbar();
            }
        }
        catch (Exception ex)
        {
            Log($"Exception during OnStartup: {ex}");
        }
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = IconHelper.CreatePrinterIcon(),
            Text = "Creality SparkX i7",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Mostra Widget", null, (s, e) =>
        {
            _widgetWindow?.ShowAboveTaskbar();
        })
        {
            Font = new System.Drawing.Font(contextMenu.Font, System.Drawing.FontStyle.Bold)
        };

        var webItem = new ToolStripMenuItem("Apri Dashboard Web", null, (s, e) =>
        {
            _viewModel?.OpenWebDashboardCommand.Execute(null);
        });

        _demoMenuItem = new ToolStripMenuItem("Modalità Demo", null, (s, e) =>
        {
            _viewModel?.ToggleDemoModeCommand.Execute(null);
        })
        {
            CheckOnClick = true,
            Checked = _viewModel?.SettingsDemoMode ?? false
        };

        var reconnectItem = new ToolStripMenuItem("Riconnetti", null, (s, e) =>
        {
            _viewModel?.ReconnectCommand.Execute(null);
        });

        var settingsItem = new ToolStripMenuItem("Impostazioni...", null, (s, e) =>
        {
            if (_viewModel != null && _widgetWindow != null)
            {
                _viewModel.IsSettingsOpen = true;
                _widgetWindow.ShowAboveTaskbar();
            }
        });

        var exitItem = new ToolStripMenuItem("Esci", null, (s, e) =>
        {
            ExitApplication();
        });

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(webItem);
        contextMenu.Items.Add(_demoMenuItem);
        contextMenu.Items.Add(reconnectItem);
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _widgetWindow?.ShowAboveTaskbar();
            }
        };

        _notifyIcon.DoubleClick += (s, e) =>
        {
            _widgetWindow?.ShowAboveTaskbar();
        };

        UpdateTrayTooltip(_viewModel?.TrayTooltipText ?? "Creality SparkX i7");
    }

    private void UpdateTrayTooltip(string text)
    {
        if (_notifyIcon == null) return;

        var firstLine = text.Split('\n')[0].Trim();
        if (firstLine.Length > 63)
        {
            firstLine = firstLine[..60] + "...";
        }

        try
        {
            _notifyIcon.Text = firstLine;
        }
        catch { }
    }

    private void ExitApplication()
    {
        Log("ExitApplication called");
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _widgetWindow?.ForceClose();
        _printerManager?.Dispose();
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch { }

        try
        {
            _mutex?.Dispose();
        }
        catch { }
        _mutex = null;

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("OnExit called");
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _printerManager?.Dispose();
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch { }

        try
        {
            _mutex?.Dispose();
        }
        catch { }
        _mutex = null;

        base.OnExit(e);
    }
}
