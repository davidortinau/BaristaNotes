namespace BaristaNotes.Core.Models;

public class ShotRecord
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    
    // Foreign keys
    public int BagId { get; set; }
    public int? MachineId { get; set; }
    public int? GrinderId { get; set; }
    public int? MadeById { get; set; }
    public int? MadeForId { get; set; }
    
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
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual Bag Bag { get; set; } = null!;
    public virtual Equipment? Machine { get; set; }
    public virtual Equipment? Grinder { get; set; }
    public virtual UserProfile? MadeBy { get; set; }
    public virtual UserProfile? MadeFor { get; set; }
    public virtual ICollection<ShotEquipment> ShotEquipments { get; set; } = new List<ShotEquipment>();
}
