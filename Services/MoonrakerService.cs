using System.IO;
using System.Net.Http;
using System.Text.Json;
using CrealitySparkXWidget.Models;

namespace CrealitySparkXWidget.Services;

public class MoonrakerService : IPrinterService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource? _cts;
    private readonly PrinterTelemetry _telemetry = new();
    private AppSettings? _settings;

    private string _cachedFilename = string.Empty;
    private double _cachedObjectHeight = 0.0;
    private double _cachedLayerHeight = 0.2;
    private int _cachedTotalLayers = 0;
    private double _cachedWipeTowerPosY = 0.0;

    public event Action<PrinterTelemetry>? TelemetryUpdated;
    public event Action<string>? StatusChanged;

    public bool IsConnected { get; private set; }
    public string ServiceName => "Moonraker (Klipper)";

    public Task ConnectAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => PollingLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task PollingLoopAsync(CancellationToken token)
    {
        // Auto-fix if port is 9999 to 7125 for Moonraker
        int port = (_settings?.Port == 9999 || _settings?.Port <= 0) ? 7125 : (_settings?.Port ?? 7125);
        string host = _settings?.PrinterHost ?? "127.0.0.1";
        string baseUrl = $"http://{host}:{port}";

        string[] objectQueries = new[]
        {
            "print_stats",
            "virtual_sdcard",
            "display_status",
            "extruder",
            "heater_bed",
            "gcode_move",
            "toolhead",
            "motion_report",
            "output_pin fan0",
            "output_pin fanp0",
            "fan_feedback",
            "fan",
            "temperature_sensor mcu_temp",
            "temperature_sensor chamber",
            "temperature_sensor box",
            "box",
            "smart_filament_rack",
            "heaters",
            "pause_resume",
            "exclude_object",
            "idle_timeout",
            "custom_macro"
        };

        string queryString = string.Join("&", objectQueries.Select(Uri.EscapeDataString));
        string fullQueryUrl = $"{baseUrl}/printer/objects/query?{queryString}";

        while (!token.IsCancellationRequested)
        {
            try
            {
                string requestUrl = $"{fullQueryUrl}&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var response = await _httpClient.GetAsync(requestUrl, token);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(token);
                    await ParseMoonrakerJsonAsync(json, baseUrl, token);

                    if (!IsConnected)
                    {
                        IsConnected = true;
                        StatusChanged?.Invoke($"Connesso a {host}:{port}");
                    }

                    _telemetry.IsConnected = true;
                    _telemetry.ConnectionDetails = $"{host}:{port} (Moonraker)";
                    _telemetry.LastUpdated = DateTime.Now;
                    TelemetryUpdated?.Invoke(_telemetry.Clone());
                }
                else
                {
                    SetDisconnected("Risposta HTTP non valida");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SetDisconnected($"Errore connessione: {ex.Message}");
            }

            var delaySec = _settings?.RefreshIntervalSeconds ?? 2;
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, delaySec)), token);
        }
    }

    private void SetDisconnected(string reason)
    {
        if (IsConnected)
        {
            IsConnected = false;
            StatusChanged?.Invoke(reason);
        }
        _telemetry.IsConnected = false;
        _telemetry.Status = PrinterStatusEnum.Offline;
        _telemetry.StatusText = "Non raggiungibile";
        TelemetryUpdated?.Invoke(_telemetry.Clone());
    }

    private async Task ParseMoonrakerJsonAsync(string json, string baseUrl, CancellationToken token)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("result", out var resultEl) ||
                !resultEl.TryGetProperty("status", out var statusEl))
            {
                return;
            }

            // 1. Extruder / Nozzle
            if (statusEl.TryGetProperty("extruder", out var extruderEl))
            {
                if (extruderEl.TryGetProperty("temperature", out var tempProp))
                    _telemetry.NozzleTemperature = Math.Round(tempProp.GetDouble(), 1);
                if (extruderEl.TryGetProperty("target", out var targetProp))
                    _telemetry.TargetNozzleTemperature = Math.Round(targetProp.GetDouble(), 1);
            }

            // 2. Heated Bed
            if (statusEl.TryGetProperty("heater_bed", out var bedEl))
            {
                if (bedEl.TryGetProperty("temperature", out var tempProp))
                    _telemetry.BedTemperature = Math.Round(tempProp.GetDouble(), 1);
                if (bedEl.TryGetProperty("target", out var targetProp))
                    _telemetry.TargetBedTemperature = Math.Round(targetProp.GetDouble(), 1);
            }

            // 3. Chamber / Enclosure / MCU Temperatures
            if (statusEl.TryGetProperty("temperature_sensor mcu_temp", out var mcuEl) &&
                mcuEl.TryGetProperty("temperature", out var mcuTempProp))
            {
                _telemetry.McuTemperature = Math.Round(mcuTempProp.GetDouble(), 1);
            }

            if (statusEl.TryGetProperty("temperature_sensor chamber", out var chamberEl) &&
                chamberEl.TryGetProperty("temperature", out var chTempProp))
            {
                _telemetry.ChamberTemperature = Math.Round(chTempProp.GetDouble(), 1);
            }
            else if (statusEl.TryGetProperty("temperature_sensor box", out var boxTempEl) &&
                     boxTempEl.TryGetProperty("temperature", out var boxTempProp))
            {
                _telemetry.ChamberTemperature = Math.Round(boxTempProp.GetDouble(), 1);
            }

            // 4. Fans (Cooling fan, Aux fan, RPM)
            // Creality OS uses "output_pin fan0" for part cooling fan and "output_pin fanp0" for auxiliary fan
            if (statusEl.TryGetProperty("output_pin fan0", out var fan0PinEl) &&
                fan0PinEl.TryGetProperty("value", out var fan0Val))
            {
                _telemetry.FanSpeedPercent = (int)Math.Round(Math.Clamp(fan0Val.GetDouble() * 100.0, 0, 100));
            }
            else if (statusEl.TryGetProperty("fan", out var fanEl) &&
                     fanEl.TryGetProperty("speed", out var speedProp))
            {
                _telemetry.FanSpeedPercent = (int)Math.Round(Math.Clamp(speedProp.GetDouble() * 100.0, 0, 100));
            }

            if (statusEl.TryGetProperty("output_pin fanp0", out var fanpPinEl) &&
                fanpPinEl.TryGetProperty("value", out var fanpVal))
            {
                _telemetry.AuxFanSpeedPercent = (int)Math.Round(Math.Clamp(fanpVal.GetDouble() * 100.0, 0, 100));
            }

            if (statusEl.TryGetProperty("fan_feedback", out var fanFbEl) &&
                fanFbEl.TryGetProperty("fan1_speed", out var fan1RpmProp))
            {
                _telemetry.FanRpm = fan1RpmProp.GetInt32();
            }

            // 5. Z Position (Height)
            double currentZ = 0.0;
            if (statusEl.TryGetProperty("gcode_move", out var gcodeMoveEl) &&
                gcodeMoveEl.TryGetProperty("gcode_position", out var gcodePosEl) &&
                gcodePosEl.ValueKind == JsonValueKind.Array &&
                gcodePosEl.GetArrayLength() >= 3)
            {
                currentZ = gcodePosEl[2].GetDouble();
            }
            else if (statusEl.TryGetProperty("print_stats", out var statsZEl) &&
                     statsZEl.TryGetProperty("z_pos", out var zPosProp))
            {
                currentZ = zPosProp.GetDouble();
            }
            else if (statusEl.TryGetProperty("toolhead", out var toolheadEl) &&
                     toolheadEl.TryGetProperty("position", out var thPosEl) &&
                     thPosEl.ValueKind == JsonValueKind.Array &&
                     thPosEl.GetArrayLength() >= 3)
            {
                currentZ = thPosEl[2].GetDouble();
            }
            else if (statusEl.TryGetProperty("motion_report", out var motionEl) &&
                     motionEl.TryGetProperty("live_position", out var livePosEl) &&
                     livePosEl.ValueKind == JsonValueKind.Array &&
                     livePosEl.GetArrayLength() >= 3)
            {
                currentZ = livePosEl[2].GetDouble();
            }
            _telemetry.CurrentZ = Math.Round(currentZ, 2);

            // 6. Print Speed (Velocity mm/s)
            int currentSpeed = 0;
            if (statusEl.TryGetProperty("motion_report", out var motionRepEl) &&
                motionRepEl.TryGetProperty("live_velocity", out var liveVelProp))
            {
                currentSpeed = (int)Math.Round(liveVelProp.GetDouble());
            }

            if (currentSpeed <= 0 && statusEl.TryGetProperty("gcode_move", out var gmEl))
            {
                double speedMmMin = 0;
                double factor = 1.0;
                if (gmEl.TryGetProperty("speed", out var sProp)) speedMmMin = sProp.GetDouble();
                if (gmEl.TryGetProperty("speed_factor", out var sfProp)) factor = sfProp.GetDouble();
                currentSpeed = (int)Math.Round((speedMmMin / 60.0) * factor);
            }
            _telemetry.PrintSpeedMmPerSec = currentSpeed;

            // 7. Display Status & Slicer Message
            string slicerMessage = string.Empty;
            if (statusEl.TryGetProperty("display_status", out var dispEl))
            {
                if (dispEl.TryGetProperty("progress", out var progProp))
                {
                    _telemetry.ProgressPercentage = Math.Round(Math.Clamp(progProp.GetDouble() * 100.0, 0.0, 100.0), 1);
                }
                if (dispEl.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                {
                    slicerMessage = msgProp.GetString() ?? string.Empty;
                }
            }
            _telemetry.SlicerMessage = slicerMessage;

            // 8. Virtual SDCard (Precise layer tracking and metadata from Creality OS)
            if (statusEl.TryGetProperty("virtual_sdcard", out var vsdEl))
            {
                if (vsdEl.TryGetProperty("layer", out var vLayerProp) && vLayerProp.GetInt32() > 0)
                {
                    _telemetry.CurrentLayer = vLayerProp.GetInt32();
                }
                if (vsdEl.TryGetProperty("layer_count", out var vLayerCountProp) && vLayerCountProp.GetInt32() > 0)
                {
                    _telemetry.TotalLayers = vLayerCountProp.GetInt32();
                    _cachedTotalLayers = vLayerCountProp.GetInt32();
                }
                if (vsdEl.TryGetProperty("cur_print_data", out var cpdEl) &&
                    cpdEl.TryGetProperty("metadata", out var metaEl))
                {
                    if (metaEl.TryGetProperty("wipe_tower_posy", out var wtProp))
                        _cachedWipeTowerPosY = wtProp.GetDouble();
                    if (metaEl.TryGetProperty("layer_count", out var metaLcProp) && metaLcProp.GetInt32() > 0)
                        _cachedTotalLayers = metaLcProp.GetInt32();
                    if (metaEl.TryGetProperty("layer_height", out var lhProp))
                        _cachedLayerHeight = lhProp.GetDouble();
                }
            }

            // 9. Toolhead position & Extruder live movement
            double toolheadX = 0.0;
            double toolheadY = 0.0;
            double liveExtruderVelocity = 0.0;
            double liveVel = 0.0;

            if (statusEl.TryGetProperty("motion_report", out var mrMotionEl))
            {
                if (mrMotionEl.TryGetProperty("live_extruder_velocity", out var levProp))
                    liveExtruderVelocity = levProp.GetDouble();
                if (mrMotionEl.TryGetProperty("live_velocity", out var lvProp))
                    liveVel = lvProp.GetDouble();
                if (mrMotionEl.TryGetProperty("live_position", out var lpEl) &&
                    lpEl.ValueKind == JsonValueKind.Array && lpEl.GetArrayLength() >= 2)
                {
                    toolheadX = lpEl[0].GetDouble();
                    toolheadY = lpEl[1].GetDouble();
                }
            }

            if (toolheadX == 0 && statusEl.TryGetProperty("toolhead", out var thMotionEl) &&
                thMotionEl.TryGetProperty("position", out var thMotionPosEl) &&
                thMotionPosEl.ValueKind == JsonValueKind.Array && thMotionPosEl.GetArrayLength() >= 2)
            {
                toolheadX = thMotionPosEl[0].GetDouble();
                toolheadY = thMotionPosEl[1].GetDouble();
            }

            // 10. Pause / Resume state
            bool isPaused = false;
            if (statusEl.TryGetProperty("pause_resume", out var prEl) &&
                prEl.TryGetProperty("is_paused", out var ipProp))
            {
                isPaused = ipProp.GetBoolean();
            }

            // 11. Heaters Waiting state (Heating bed/nozzle)
            bool extruderWait = false;
            bool bedWait = false;
            if (statusEl.TryGetProperty("heaters", out var heatersEl))
            {
                if (heatersEl.TryGetProperty("extruder_temperature_wait", out var ewProp))
                    extruderWait = ewProp.GetBoolean();
                if (heatersEl.TryGetProperty("bed_temperature_wait", out var bwProp))
                    bedWait = bwProp.GetBoolean();
            }

            // 12. Exclude Object (for detection of Prime Tower / Supports)
            string currentObject = string.Empty;
            if (statusEl.TryGetProperty("exclude_object", out var eoEl) &&
                eoEl.TryGetProperty("current_object", out var coProp) &&
                coProp.ValueKind == JsonValueKind.String)
            {
                currentObject = coProp.GetString() ?? string.Empty;
            }

            // 13. Custom macro / calibration flags
            int levelingCalibration = 0;
            if (statusEl.TryGetProperty("custom_macro", out var cmEl) &&
                cmEl.TryGetProperty("leveling_calibration", out var cmCalibProp))
            {
                levelingCalibration = cmCalibProp.GetInt32();
            }

            // 14. Box cut state
            int cutState = 0;
            string boxState = string.Empty;
            if (statusEl.TryGetProperty("box", out var bCutEl))
            {
                if (bCutEl.TryGetProperty("cut_state", out var csProp)) cutState = csProp.GetInt32();
                if (bCutEl.TryGetProperty("state", out var bsProp)) boxState = bsProp.GetString() ?? string.Empty;
            }

            // 15. Print Stats (File, State, Times, Layers)
            string stateStr = "idle";
            if (statusEl.TryGetProperty("print_stats", out var statsEl))
            {
                if (statsEl.TryGetProperty("filename", out var fileProp))
                {
                    var file = fileProp.GetString() ?? string.Empty;
                    _telemetry.CurrentFile = Path.GetFileName(file);

                    // Fetch metadata if filename changed or not cached
                    if (!string.IsNullOrEmpty(file) && file != _cachedFilename)
                    {
                        _cachedFilename = file;
                        _ = FetchFileMetadataAsync(baseUrl, file, token);
                    }
                }

                if (statsEl.TryGetProperty("print_duration", out var durProp))
                    _telemetry.TimeElapsedSeconds = (long)durProp.GetDouble();

                // Layer info fallback
                int currentLayer = _telemetry.CurrentLayer;
                int totalLayers = _telemetry.TotalLayers;

                if (currentLayer <= 0 || totalLayers <= 0)
                {
                    if (statsEl.TryGetProperty("info", out var infoEl))
                    {
                        if (currentLayer <= 0 && infoEl.TryGetProperty("current_layer", out var clProp) && clProp.ValueKind == JsonValueKind.Number)
                            currentLayer = clProp.GetInt32();
                        if (totalLayers <= 0 && infoEl.TryGetProperty("total_layer", out var tlProp) && tlProp.ValueKind == JsonValueKind.Number)
                            totalLayers = tlProp.GetInt32();
                    }

                    if (totalLayers <= 0 && _cachedTotalLayers > 0)
                    {
                        totalLayers = _cachedTotalLayers;
                    }

                    if (currentLayer <= 0 && _telemetry.CurrentZ > 0)
                    {
                        double lHeight = _cachedLayerHeight > 0.05 ? _cachedLayerHeight : 0.2;
                        currentLayer = (int)Math.Max(1, Math.Round(_telemetry.CurrentZ / lHeight));
                        if (totalLayers > 0 && currentLayer > totalLayers)
                            currentLayer = totalLayers;
                    }

                    _telemetry.CurrentLayer = currentLayer;
                    _telemetry.TotalLayers = totalLayers;
                }

                // State string
                if (statsEl.TryGetProperty("state", out var stateProp))
                {
                    stateStr = stateProp.GetString()?.ToLowerInvariant() ?? "idle";
                }

                // Calculate remaining time
                if (_telemetry.ProgressPercentage > 0 && _telemetry.TimeElapsedSeconds > 0)
                {
                    var totalSec = _telemetry.TimeElapsedSeconds / (_telemetry.ProgressPercentage / 100.0);
                    _telemetry.TimeRemainingSeconds = Math.Max(0, (long)(totalSec - _telemetry.TimeElapsedSeconds));
                }
            }

            // 16. Determine Detailed Activity / Phase
            DetermineDetailedActivity(
                stateStr,
                isPaused,
                bedWait,
                extruderWait,
                toolheadX,
                toolheadY,
                liveVel,
                liveExtruderVelocity,
                levelingCalibration,
                cutState,
                boxState,
                currentObject,
                slicerMessage);

            // 17. CFS Multi-Color Filament System ("box" object in Creality SparkX / K2)
            if (statusEl.TryGetProperty("box", out var boxEl))
            {
                ParseCfsBox(boxEl);
            }
        }
        catch { }
    }

    private void ParseCfsBox(JsonElement boxEl)
    {
        try
        {
            var slots = new List<FilamentSlotInfo>();
            string activeFilamentLetter = string.Empty;

            if (boxEl.TryGetProperty("T1", out var t1El) && t1El.TryGetProperty("filament", out var filProp))
            {
                activeFilamentLetter = filProp.GetString() ?? string.Empty; // e.g. "B"
            }

            // Check "same_material" list
            // Each entry: ["000001", "0FF6E1A", ["T1A"], "PLA"]
            if (boxEl.TryGetProperty("same_material", out var sameMatEl) &&
                sameMatEl.ValueKind == JsonValueKind.Array)
            {
                int index = 1;
                foreach (var item in sameMatEl.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() >= 4)
                    {
                        var rawColor = item[1].GetString() ?? "0FFFFFF";
                        var material = item[3].GetString() ?? "PLA";

                        // Clean color hex (Creality formats as "0FF6E1A" -> "#FF6E1A")
                        string hexColor = CleanCrealityColorHex(rawColor);

                        string slotCode = $"T1{(char)('A' + index - 1)}";
                        bool isActive = string.Equals(activeFilamentLetter, ((char)('A' + index - 1)).ToString(), StringComparison.OrdinalIgnoreCase);

                        slots.Add(new FilamentSlotInfo
                        {
                            SlotIndex = index,
                            ColorHex = hexColor,
                            Material = material,
                            IsActive = isActive,
                            IsLoaded = true
                        });
                        index++;
                    }
                }
            }

            if (slots.Count > 0)
            {
                _telemetry.CfsSlots = slots;
            }
        }
        catch { }
    }

    private void DetermineDetailedActivity(
        string printStatsState,
        bool isPaused,
        bool bedWait,
        bool extruderWait,
        double toolheadX,
        double toolheadY,
        double liveVelocity,
        double liveExtruderVelocity,
        int levelingCalibration,
        int cutState,
        string boxState,
        string currentObject,
        string slicerMessage)
    {
        var msgLower = (slicerMessage ?? string.Empty).ToLowerInvariant();
        var objLower = (currentObject ?? string.Empty).ToLowerInvariant();

        // 1. Paused: ONLY if print_stats.state is actually "paused"
        if (printStatsState == "paused")
        {
            _telemetry.Status = PrinterStatusEnum.Paused;
            _telemetry.StatusText = "In Pausa";
            _telemetry.DetailedActivityTitle = "In Pausa";
            _telemetry.DetailedActivityIcon = "⏸️";
            _telemetry.DetailedActivityColor = "#F59E0B";
            _telemetry.DetailedActivityDescription = toolheadX >= 265
                ? "Testina parcheggiata fuori piatto nello scivolo di scarico"
                : "Stampa momentaneamente sospesa";
            return;
        }

        // 2. Completed
        if (printStatsState == "complete")
        {
            _telemetry.Status = PrinterStatusEnum.Completed;
            _telemetry.StatusText = "Completata";
            _telemetry.DetailedActivityTitle = "Stampa Ultimata";
            _telemetry.DetailedActivityIcon = "✅";
            _telemetry.DetailedActivityColor = "#10B981";
            _telemetry.DetailedActivityDescription = "Pezzo stampato con successo. Piatto pronto per la rimozione";
            _telemetry.TimeRemainingSeconds = 0;
            return;
        }

        // 3. Cancelled
        if (printStatsState == "cancelled")
        {
            _telemetry.Status = PrinterStatusEnum.Idle;
            _telemetry.StatusText = "Annullata";
            _telemetry.DetailedActivityTitle = "Stampa Annullata";
            _telemetry.DetailedActivityIcon = "⏹️";
            _telemetry.DetailedActivityColor = "#94A3B8";
            _telemetry.DetailedActivityDescription = "Il lavoro è stato interrotto dall'utente";
            return;
        }

        // 4. Heating Phase (Bed or Nozzle)
        if (bedWait || (_telemetry.TargetBedTemperature > 0 && _telemetry.BedTemperature < _telemetry.TargetBedTemperature - 2.5 && printStatsState == "printing"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Riscaldamento";
            _telemetry.DetailedActivityTitle = "Riscaldamento Piatto";
            _telemetry.DetailedActivityIcon = "♨️";
            _telemetry.DetailedActivityColor = "#F97316";
            _telemetry.DetailedActivityDescription = $"Raggiungimento temperatura: {_telemetry.BedTemperature:F0} / {_telemetry.TargetBedTemperature:F0} °C";
            return;
        }

        if (extruderWait || (_telemetry.TargetNozzleTemperature > 0 && _telemetry.NozzleTemperature < _telemetry.TargetNozzleTemperature - 5.0 && printStatsState == "printing"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Riscaldamento";
            _telemetry.DetailedActivityTitle = "Riscaldamento Ugello";
            _telemetry.DetailedActivityIcon = "🔥";
            _telemetry.DetailedActivityColor = "#EF4444";
            _telemetry.DetailedActivityDescription = $"Raggiungimento temperatura: {_telemetry.NozzleTemperature:F0} / {_telemetry.TargetNozzleTemperature:F0} °C";
            return;
        }

        // 5. Cooling Phase
        if (_telemetry.TargetNozzleTemperature == 0 && _telemetry.NozzleTemperature > 50 && (printStatsState == "idle" || printStatsState == "complete" || printStatsState == "standby"))
        {
            _telemetry.Status = PrinterStatusEnum.Idle;
            _telemetry.StatusText = "Raffreddamento";
            _telemetry.DetailedActivityTitle = "Raffreddamento";
            _telemetry.DetailedActivityIcon = "❄️";
            _telemetry.DetailedActivityColor = "#38BDF8";
            _telemetry.DetailedActivityDescription = $"Raffreddamento componenti in corso (Ugello: {_telemetry.NozzleTemperature:F0} °C)";
            return;
        }

        // 6. Leveling / Calibration / Homing
        if (levelingCalibration == 1 || msgLower.Contains("level") || msgLower.Contains("calibrat") || msgLower.Contains("homing") || msgLower.Contains("g28") || msgLower.Contains("g29") || msgLower.Contains("mesh"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Calibrazione";
            _telemetry.DetailedActivityTitle = "Calibrazione & Livellamento";
            _telemetry.DetailedActivityIcon = "📐";
            _telemetry.DetailedActivityColor = "#8B5CF6";
            _telemetry.DetailedActivityDescription = "Sondaggio piatto e calibrazione assi della stampante";
            return;
        }

        // 7. Filament Cut (CFS)
        if (cutState != 0 || msgLower.Contains("cut") || msgLower.Contains("taglio"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Taglio Filo";
            _telemetry.DetailedActivityTitle = "Taglio Filamento";
            _telemetry.DetailedActivityIcon = "✂️";
            _telemetry.DetailedActivityColor = "#EC4899";
            _telemetry.DetailedActivityDescription = "Lama di taglio attiva per cambio bobina CFS";
            return;
        }

        // 8. Purge / Wipe / Flush (Spurgo)
        if ((toolheadX >= 265 && (liveExtruderVelocity > 0.05 || msgLower.Contains("flush") || msgLower.Contains("purge") || msgLower.Contains("spurgo"))) ||
            msgLower.Contains("wipe") || msgLower.Contains("pulizia ugello"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Spurgo";
            _telemetry.DetailedActivityTitle = "Spurgo & Pulizia Ugello";
            _telemetry.DetailedActivityIcon = "🔄";
            _telemetry.DetailedActivityColor = "#06B6D4";
            _telemetry.DetailedActivityDescription = "Estrusione di spurgo e scarico materiale nello scivolo";
            return;
        }

        // 9. CFS Filament Load / Unload (Cambio colore)
        if (boxState == "loading" || boxState == "unloading" || msgLower.Contains("load") || msgLower.Contains("unload") || msgLower.Contains("cambio filamento"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Cambio CFS";
            _telemetry.DetailedActivityTitle = "Cambio Filamento (CFS)";
            _telemetry.DetailedActivityIcon = "🎨";
            _telemetry.DetailedActivityColor = "#A855F7";
            _telemetry.DetailedActivityDescription = "Alimentazione e inserimento nuovo colore nel condotto";
            return;
        }

        // 10. Prime Tower (Torre di spurgo multi-colore)
        bool isNearWipeTower = _cachedWipeTowerPosY > 0 && Math.Abs(toolheadY - _cachedWipeTowerPosY) < 22 && toolheadX > 180 && liveExtruderVelocity > 0.05;
        if (objLower.Contains("tower") || objLower.Contains("wipe") || objLower.Contains("prime") ||
            msgLower.Contains("prime tower") || msgLower.Contains("wipe tower") || msgLower.Contains("torre") ||
            isNearWipeTower)
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Prime Tower";
            _telemetry.DetailedActivityTitle = "Torre di Spurgo (Prime Tower)";
            _telemetry.DetailedActivityIcon = "🗼";
            _telemetry.DetailedActivityColor = "#8B5CF6";
            _telemetry.DetailedActivityDescription = "Stampa torre di compensazione e transizione colore";
            return;
        }

        // 11. Supports (Supporti)
        if (objLower.Contains("support") || msgLower.Contains("support") || msgLower.Contains("supporti"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Supporti";
            _telemetry.DetailedActivityTitle = "Stampa Supporti";
            _telemetry.DetailedActivityIcon = "🧱";
            _telemetry.DetailedActivityColor = "#0284C7";
            _telemetry.DetailedActivityDescription = "Costruzione strutture di supporto per parti a sbalzo";
            return;
        }

        // 12. First Layer
        if (_telemetry.CurrentLayer == 1 && printStatsState == "printing" && liveExtruderVelocity > 0.05)
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Primo Layer";
            _telemetry.DetailedActivityTitle = "Primo Strato (Layer 1)";
            _telemetry.DetailedActivityIcon = "🏁";
            _telemetry.DetailedActivityColor = "#10B981";
            _telemetry.DetailedActivityDescription = "Stampa primo strato ad alta aderenza sul piatto";
            return;
        }

        // 13. Slicer specific feature messages
        if (msgLower.Contains("infill") || msgLower.Contains("riempimento"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Riempimento";
            _telemetry.DetailedActivityTitle = "Riempimento (Infill)";
            _telemetry.DetailedActivityIcon = "🧵";
            _telemetry.DetailedActivityColor = "#10B981";
            _telemetry.DetailedActivityDescription = $"Estrusione reticolo interno ({_telemetry.PrintSpeedMmPerSec} mm/s)";
            return;
        }

        if (msgLower.Contains("outer wall") || msgLower.Contains("parete esterna"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Parete Esterna";
            _telemetry.DetailedActivityTitle = "Parete Esterna (Outer Wall)";
            _telemetry.DetailedActivityIcon = "🧵";
            _telemetry.DetailedActivityColor = "#10B981";
            _telemetry.DetailedActivityDescription = $"Stampa perimetro esterno ad alta precisione ({_telemetry.PrintSpeedMmPerSec} mm/s)";
            return;
        }

        if (msgLower.Contains("inner wall") || msgLower.Contains("parete interna"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Parete Interna";
            _telemetry.DetailedActivityTitle = "Parete Interna (Inner Wall)";
            _telemetry.DetailedActivityIcon = "🧵";
            _telemetry.DetailedActivityColor = "#10B981";
            _telemetry.DetailedActivityDescription = $"Stampa perimetri interni ({_telemetry.PrintSpeedMmPerSec} mm/s)";
            return;
        }

        if (msgLower.Contains("bridge") || msgLower.Contains("ponte"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Ponte";
            _telemetry.DetailedActivityTitle = "Ponte Sospeso (Bridge)";
            _telemetry.DetailedActivityIcon = "🌉";
            _telemetry.DetailedActivityColor = "#F59E0B";
            _telemetry.DetailedActivityDescription = $"Estrusione filamento sospeso nel vuoto ({_telemetry.PrintSpeedMmPerSec} mm/s)";
            return;
        }

        if (msgLower.Contains("top") || msgLower.Contains("solid"))
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            _telemetry.StatusText = "Superficie Top";
            _telemetry.DetailedActivityTitle = "Superficie Superiore (Top)";
            _telemetry.DetailedActivityIcon = "🧵";
            _telemetry.DetailedActivityColor = "#10B981";
            _telemetry.DetailedActivityDescription = $"Stampa strati superiori pieni di finitura ({_telemetry.PrintSpeedMmPerSec} mm/s)";
            return;
        }

        // 14. Active Printing / Extrusion vs Rapid Travel
        if (printStatsState == "printing")
        {
            _telemetry.Status = PrinterStatusEnum.Printing;
            if (liveExtruderVelocity > 0.05)
            {
                _telemetry.StatusText = "Estrusione";
                _telemetry.DetailedActivityTitle = "In Estrusione (Stampa)";
                _telemetry.DetailedActivityIcon = "🧵";
                _telemetry.DetailedActivityColor = "#00D26A";
                _telemetry.DetailedActivityDescription = $"Deposizione filamento attiva a {_telemetry.PrintSpeedMmPerSec} mm/s";
            }
            else if (liveExtruderVelocity <= 0 && liveVelocity > 15)
            {
                _telemetry.StatusText = "Spostamento";
                _telemetry.DetailedActivityTitle = "Spostamento Rapido (Travel)";
                _telemetry.DetailedActivityIcon = "💨";
                _telemetry.DetailedActivityColor = "#64748B";
                _telemetry.DetailedActivityDescription = $"Traslazione rapida testina a {_telemetry.PrintSpeedMmPerSec} mm/s";
            }
            else
            {
                _telemetry.StatusText = "In Stampa";
                _telemetry.DetailedActivityTitle = "In Stampa";
                _telemetry.DetailedActivityIcon = "⚡";
                _telemetry.DetailedActivityColor = "#00D26A";
                _telemetry.DetailedActivityDescription = $"Lavoro in esecuzione (Layer {_telemetry.CurrentLayer}/{_telemetry.TotalLayers})";
            }
            return;
        }

        // 15. Default Idle
        _telemetry.Status = PrinterStatusEnum.Idle;
        _telemetry.StatusText = "Pronta / Inattiva";
        _telemetry.DetailedActivityTitle = "Inattiva (Idle)";
        _telemetry.DetailedActivityIcon = "💤";
        _telemetry.DetailedActivityColor = "#64748B";
        _telemetry.DetailedActivityDescription = "Stampante pronta per un nuovo lavoro di stampa";
    }

    private static string CleanCrealityColorHex(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "-1" || raw == "None") return "#64748B";
        if (raw.StartsWith("#")) return raw;
        if (raw.StartsWith("0") && raw.Length == 7)
            return "#" + raw.Substring(1);
        if (raw.Length == 6)
            return "#" + raw;
        return "#64748B";
    }

    private async Task FetchFileMetadataAsync(string baseUrl, string filename, CancellationToken token)
    {
        try
        {
            _cachedFilename = filename;
            var url = $"{baseUrl}/server/files/metadata?filename={Uri.EscapeDataString(filename)}";
            var resp = await _httpClient.GetAsync(url, token);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(token);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("result", out var resEl))
                {
                    if (resEl.TryGetProperty("object_height", out var ohProp))
                        _cachedObjectHeight = ohProp.GetDouble();

                    if (resEl.TryGetProperty("layer_height", out var lhProp))
                        _cachedLayerHeight = lhProp.GetDouble();
                    else if (resEl.TryGetProperty("first_layer_height", out var flhProp))
                        _cachedLayerHeight = flhProp.GetDouble();

                    if (resEl.TryGetProperty("layer_count", out var lcProp) && lcProp.GetInt32() > 0)
                        _cachedTotalLayers = lcProp.GetInt32();
                    else if (_cachedObjectHeight > 0 && _cachedLayerHeight > 0.05)
                        _cachedTotalLayers = (int)Math.Ceiling(_cachedObjectHeight / _cachedLayerHeight);

                    if (resEl.TryGetProperty("wipe_tower_posy", out var wtpProp))
                        _cachedWipeTowerPosY = wtpProp.GetDouble();
                }
            }
        }
        catch { }
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
