namespace BaristaNotes.Core.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }

    /// <summary>
    /// Free-form learned context about this person (preferences, history insights, notes)
    /// that the AI can read and append to over time. Soft cap 2000 chars enforced in service.
    /// </summary>
    public string? Context { get; set; }

    public DateTime CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<ShotRecord> ShotsMadeBy { get; set; } = new List<ShotRecord>();
    public virtual ICollection<ShotRecord> ShotsMadeFor { get; set; } = new List<ShotRecord>();
}
