using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Models;

/// <summary>
/// Mutable filter state for UI binding.
/// Converts to immutable ShotFilterCriteriaDto for service calls.
/// </summary>
public class ShotFilterCriteria
{
    public List<int> BeanIds { get; set; } = new();
    public List<int> MadeForIds { get; set; } = new();
    public List<int> Ratings { get; set; } = new();
    
    public bool HasFilters => 
        BeanIds.Count > 0 || 
        MadeForIds.Count > 0 || 
        Ratings.Count > 0;
    
    public int FilterCount => 
        BeanIds.Count + MadeForIds.Count + Ratings.Count;
    
    public ShotFilterCriteriaDto ToDto() => new()
    {
        BeanIds = BeanIds.Count > 0 ? BeanIds.ToList() : null,
        MadeForIds = MadeForIds.Count > 0 ? MadeForIds.ToList() : null,
        Ratings = Ratings.Count > 0 ? Ratings.ToList() : null
    };
    
    public void Clear()
    {
        BeanIds.Clear();
        MadeForIds.Clear();
        Ratings.Clear();
    }
    
    public ShotFilterCriteria Clone() => new()
    {
        BeanIds = new List<int>(BeanIds),
        MadeForIds = new List<int>(MadeForIds),
        Ratings = new List<int>(Ratings)
    };
}
