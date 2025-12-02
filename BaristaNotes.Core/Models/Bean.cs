namespace BaristaNotes.Core.Models;

public class Bean
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Roaster { get; set; }
    public DateTimeOffset? RoastDate { get; set; }
    public string? Origin { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ShotRecord> ShotRecords { get; set; } = new List<ShotRecord>();
}
