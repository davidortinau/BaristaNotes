using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Event args for data change notifications.
/// </summary>
public class DataChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of data change that occurred.
    /// </summary>
    public DataChangeType ChangeType { get; }

    /// <summary>
    /// The entity that was created/updated/deleted, if available.
    /// </summary>
    public object? Entity { get; }

    public DataChangedEventArgs(DataChangeType changeType, object? entity = null)
    {
        ChangeType = changeType;
        Entity = entity;
    }
}

/// <summary>
/// Service for broadcasting data change notifications across pages.
/// Used by voice commands to notify pages when data is modified.
/// </summary>
public interface IDataChangeNotifier
{
    /// <summary>
    /// Event raised when data changes occur (from voice commands or other sources).
    /// </summary>
    event EventHandler<DataChangedEventArgs>? DataChanged;

    /// <summary>
    /// Notifies subscribers that data has changed.
    /// </summary>
    /// <param name="changeType">The type of data change.</param>
    /// <param name="entity">The affected entity, if available.</param>
    void NotifyDataChanged(DataChangeType changeType, object? entity = null);
}
