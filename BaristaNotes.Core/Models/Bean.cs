namespace BaristaNotes.Core.Models;

public class Bean
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Roaster { get; set; }
    public string? Origin { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    
    // CoreSync metadata
    public Guid SyncId { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    
    // Navigation properties
    public virtual ICollection<Bag> Bags { get; set; } = new List<Bag>();
}
