namespace BaristaNotes.Core.Models;

public class ShotEquipment
{
    public int ShotRecordId { get; set; }
    public int EquipmentId { get; set; }
    
    // Navigation properties
    public virtual ShotRecord ShotRecord { get; set; } = null!;
    public virtual Equipment Equipment { get; set; } = null!;
}
