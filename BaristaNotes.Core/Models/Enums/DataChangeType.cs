namespace BaristaNotes.Core.Models.Enums;

/// <summary>
/// Type of data change for cross-page notifications.
/// </summary>
public enum DataChangeType
{
    BeanCreated = 1,
    BeanUpdated = 2,
    BagCreated = 3,
    BagUpdated = 4,
    ShotCreated = 5,
    ShotUpdated = 6,
    ShotDeleted = 7,
    EquipmentCreated = 8,
    EquipmentUpdated = 9,
    ProfileCreated = 10,
    ProfileUpdated = 11
}
