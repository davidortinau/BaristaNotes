using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Models;

public class ShotRecord
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    
    // Foreign keys
    public int BagId { get; set; }
    public int? MachineId { get; set; }
    public int? GrinderId { get; set; }
    public int? MadeById { get; set; }
    public int? MadeForId { get; set; }

    /// <summary>
    /// Brew method for this record. Defaults to Espresso for backwards
    /// compatibility with pre-v2 rows. Combined with <see cref="ParametersJson"/>,
    /// this lets a single record represent any drink (espresso, pour over, moka, etc.)
    /// while keeping the shared columns (Dose, Time, Output) as first-class.
    /// </summary>
    public BrewMethod BrewMethod { get; set; } = BrewMethod.Espresso;

    /// <summary>
    /// Optional JSON blob of brew-method-specific parameters that don't
    /// warrant their own column (e.g. pour-over pour schedule, moka burner
    /// level, aeropress plunge style). Null when not used.
    /// </summary>
    public string? ParametersJson { get; set; }

    // Recipe parameters
    public decimal DoseIn { get; set; }
    public string GrindSetting { get; set; } = string.Empty;
    public decimal ExpectedTime { get; set; }
    public decimal ExpectedOutput { get; set; }
    public string DrinkType { get; set; } = string.Empty;
    
    // Actual results
    public decimal? ActualTime { get; set; }
    public decimal? ActualOutput { get; set; }
    public decimal? PreinfusionTime { get; set; }
    
    // Rating
    public int? Rating { get; set; }
    
    // Tasting notes (optional free text)
    public string? TastingNotes { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual Bag Bag { get; set; } = null!;
    public virtual Equipment? Machine { get; set; }
    public virtual Equipment? Grinder { get; set; }
    public virtual UserProfile? MadeBy { get; set; }
    public virtual UserProfile? MadeFor { get; set; }
    public virtual ICollection<ShotEquipment> ShotEquipments { get; set; } = new List<ShotEquipment>();
}
