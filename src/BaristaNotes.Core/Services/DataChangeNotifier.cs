using BaristaNotes.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Implementation of data change notification service.
/// Broadcasts events to subscribed pages when data is modified.
/// </summary>
public class DataChangeNotifier : IDataChangeNotifier
{
    private readonly ILogger<DataChangeNotifier> _logger;

    public event EventHandler<DataChangedEventArgs>? DataChanged;

    public DataChangeNotifier(ILogger<DataChangeNotifier> logger)
    {
        _logger = logger;
    }

    public void NotifyDataChanged(DataChangeType changeType, object? entity = null)
    {
        _logger.LogDebug("Notifying data change: {ChangeType}, Entity: {EntityType}",
            changeType, entity?.GetType().Name ?? "null");

        try
        {
            DataChanged?.Invoke(this, new DataChangedEventArgs(changeType, entity));
            _logger.LogInformation("Data change notification sent: {ChangeType}", changeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying data change: {ChangeType}", changeType);
        }
    }
}
