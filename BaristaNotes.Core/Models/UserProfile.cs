namespace BaristaNotes.Core.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ShotRecord> ShotsMadeBy { get; set; } = new List<ShotRecord>();
    public virtual ICollection<ShotRecord> ShotsMadeFor { get; set; } = new List<ShotRecord>();
}
