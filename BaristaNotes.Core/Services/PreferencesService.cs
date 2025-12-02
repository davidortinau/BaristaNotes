using System.Text.Json;

namespace BaristaNotes.Core.Services;

public class PreferencesService : IPreferencesService
{
    private readonly IPreferencesStore _store;
    
    private const string KeyLastDrinkType = "last_drink_type";
    private const string KeyLastBeanId = "last_bean_id";
    private const string KeyLastMachineId = "last_machine_id";
    private const string KeyLastGrinderId = "last_grinder_id";
    private const string KeyLastAccessoryIds = "last_accessory_ids";
    private const string KeyLastMadeById = "last_made_by_id";
    private const string KeyLastMadeForId = "last_made_for_id";
    
    public PreferencesService(IPreferencesStore store)
    {
        _store = store;
    }
    
    public string? GetLastDrinkType()
        => _store.Get(KeyLastDrinkType, null);
    
    public void SetLastDrinkType(string drinkType)
        => _store.Set(KeyLastDrinkType, drinkType);
    
    public int? GetLastBeanId()
    {
        var value = _store.Get(KeyLastBeanId, -1);
        return value == -1 ? null : value;
    }
    
    public void SetLastBeanId(int? beanId)
        => _store.Set(KeyLastBeanId, beanId ?? -1);
    
    public int? GetLastMachineId()
    {
        var value = _store.Get(KeyLastMachineId, -1);
        return value == -1 ? null : value;
    }
    
    public void SetLastMachineId(int? machineId)
        => _store.Set(KeyLastMachineId, machineId ?? -1);
    
    public int? GetLastGrinderId()
    {
        var value = _store.Get(KeyLastGrinderId, -1);
        return value == -1 ? null : value;
    }
    
    public void SetLastGrinderId(int? grinderId)
        => _store.Set(KeyLastGrinderId, grinderId ?? -1);
    
    public List<int> GetLastAccessoryIds()
    {
        var json = _store.Get(KeyLastAccessoryIds, "[]");
        return json == null ? new List<int>() : JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
    }
    
    public void SetLastAccessoryIds(List<int> accessoryIds)
    {
        var json = JsonSerializer.Serialize(accessoryIds);
        _store.Set(KeyLastAccessoryIds, json);
    }
    
    public int? GetLastMadeById()
    {
        var value = _store.Get(KeyLastMadeById, -1);
        return value == -1 ? null : value;
    }
    
    public void SetLastMadeById(int? madeById)
        => _store.Set(KeyLastMadeById, madeById ?? -1);
    
    public int? GetLastMadeForId()
    {
        var value = _store.Get(KeyLastMadeForId, -1);
        return value == -1 ? null : value;
    }
    
    public void SetLastMadeForId(int? madeForId)
        => _store.Set(KeyLastMadeForId, madeForId ?? -1);
    
    public void ClearAll()
    {
        _store.Remove(KeyLastDrinkType);
        _store.Remove(KeyLastBeanId);
        _store.Remove(KeyLastMachineId);
        _store.Remove(KeyLastGrinderId);
        _store.Remove(KeyLastAccessoryIds);
        _store.Remove(KeyLastMadeById);
        _store.Remove(KeyLastMadeForId);
    }
}
