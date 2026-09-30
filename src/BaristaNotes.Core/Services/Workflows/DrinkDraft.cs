using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services.Workflows;

public class DrinkDraft
{
    public BrewMethod BrewMethod { get; set; } = BrewMethod.Espresso;
    public string DrinkType { get; set; } = "Espresso";
    public decimal DoseIn { get; set; } = 18m;
    public decimal ExpectedOutput { get; set; } = 36m;
    public decimal ExpectedTime { get; set; } = 28m;
    public decimal? ActualOutput { get; set; }
    public decimal? ActualTime { get; set; }
    public int? GrindMicrons { get; set; }
    public decimal? WaterTempC { get; set; }
    public TemperatureUnit TempUnit { get; set; } = TemperatureUnit.Fahrenheit;
    public int Rating { get; set; } = 2;
    public string? TastingNotes { get; set; }
    public int? SelectedBagId { get; set; }
    public UserProfileDto? SelectedMaker { get; set; }
    public UserProfileDto? SelectedRecipient { get; set; }
    public int? SelectedMachineId { get; set; }
    public int? SelectedGrinderId { get; set; }
    public List<int> SelectedAccessoryIds { get; set; } = [];
    public List<BagSummaryDto> AvailableBags { get; set; } = [];
    public List<UserProfileDto> AvailableUsers { get; set; } = [];
    public List<EquipmentDto> AvailableEquipment { get; set; } = [];
    public string? BeanName { get; set; }
}

public sealed record DrinkLoadResult(
    int? EditingShotId,
    ShotRecordDto? Shot,
    List<BagSummaryDto> Bags,
    List<UserProfileDto> Users,
    List<EquipmentDto> Equipment);
