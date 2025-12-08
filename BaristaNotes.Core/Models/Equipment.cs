using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Models;

public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EquipmentType Type { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    
    // CoreSync metadata (preparation for future sync)
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ShotEquipment> ShotEquipments { get; set; } = new List<ShotEquipment>();
}
