namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Indicates the type of AI bean recommendation.
/// </summary>
public enum RecommendationType
{
    /// <summary>
    /// Bean has no shot history; recommendation based on bean characteristics.
    /// </summary>
    NewBean,

    /// <summary>
    /// Bean has shot history; recommendation based on user's past shots.
    /// </summary>
    ReturningBean
}
