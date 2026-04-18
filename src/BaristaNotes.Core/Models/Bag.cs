namespace BaristaNotes.Core.Models;

public class Bag
{
    public int Id { get; set; }
    public int BeanId { get; set; }
    public DateTime RoastDate { get; set; }
    public string? Notes { get; set; }
    public bool IsComplete { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual Bean Bean { get; set; } = null!;
    public virtual ICollection<ShotRecord> ShotRecords { get; set; } = new List<ShotRecord>();
}
