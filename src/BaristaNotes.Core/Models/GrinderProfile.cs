namespace BaristaNotes.Core.Models;

/// <summary>
/// Per-grinder calibration profile that lives alongside an <see cref="Equipment"/>
/// row of <see cref="Models.Enums.EquipmentType.Grinder"/>. Holds the grinder's
/// scale bounds and a set of anchor points (micron ↔ setting) that the
/// deterministic translator interpolates between. Anchors may be seeded from
/// community defaults (e.g. DF64), filled in by AI fallback, or written by the
/// user when they confirm / adjust a suggested setting.
/// </summary>
public class GrinderProfile
{
    public int Id { get; set; }

    /// <summary>FK to the <see cref="Equipment"/> row (must be Type=Grinder).</summary>
    public int EquipmentId { get; set; }

    /// <summary>Minimum setting on the grinder's native scale (e.g. 0 for DF64).</summary>
    public decimal? MinSetting { get; set; }

    /// <summary>Maximum setting on the grinder's native scale (e.g. 9 for DF64).</summary>
    public decimal? MaxSetting { get; set; }

    /// <summary>
    /// Smallest meaningful setting increment (e.g. 0.1 for a stepless DF64 with
    /// decimal ticks). Null means unknown / continuous.
    /// </summary>
    public decimal? StepSize { get; set; }

    /// <summary>
    /// JSON array of calibration anchor points. Each anchor is
    /// <c>{ "micron": 250, "setting": 1.8, "source": "ai"|"user"|"community", "updatedAt": "..." }</c>.
    /// Consumed by the deterministic interpolator; overwritten by the user whenever
    /// they confirm / adjust a suggested setting for a logged drink.
    /// </summary>
    public string? AnchorsJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }

    // CoreSync metadata (preparation for future sync)
    public Guid SyncId { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public virtual Equipment? Equipment { get; set; }
}
