namespace CrealitySparkXWidget.Models;

public class FilamentSlotInfo
{
    public int SlotIndex { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public string Material { get; set; } = "PLA";
    public bool IsActive { get; set; }
    public bool IsLoaded { get; set; } = true;
    public int? RemainingPercent { get; set; }

    public string DisplayName => $"Slot {SlotIndex}: {Material}";
}

