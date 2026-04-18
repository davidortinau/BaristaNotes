using System.Text.Json;

namespace BaristaNotes.Core.Services;

public class PreferencesService : IPreferencesService
{
    private readonly IPreferencesStore _store;
    
    private const string KeyLastDrinkType = "last_drink_type";
    private const string KeyLastBeanId = "last_bean_id";
    private const string KeyLastBagId = "last_bag_id";  // Added for Phase 4 (T039)
    private const string KeyLastMachineId = "last_machine_id";
    private const string KeyLastGrinderId = "last_grinder_id";
    private const string KeyLastAccessoryIds = "last_accessory_ids";
    private const string KeyLastMadeById = "last_made_by_id";
    private const string KeyLastMadeForId = "last_made_for_id";
    private const string KeyLastDoseIn = "last_dose_in";
    private const string KeyLastGrindSetting = "last_grind_setting";
    private const string KeyLastExpectedTime = "last_expected_time";
    private const string KeyLastExpectedOutput = "last_expected_output";
    private const string KeyLastPreinfusionTime = "last_preinfusion_time";
    
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
    
    public int? GetLastBagId()
    {
        var value = _store.Get(KeyLastBagId, -1);
        return value == -1 ? null : value;
    }
    
    public void SetLastBagId(int? bagId)
        => _store.Set(KeyLastBagId, bagId ?? -1);
    
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
    
    public decimal? GetLastDoseIn()
    {
        var value = _store.Get(KeyLastDoseIn, -1.0);
        return value == -1.0 ? null : (decimal)value;
    }
    
    public void SetLastDoseIn(decimal? doseIn)
        => _store.Set(KeyLastDoseIn, doseIn.HasValue ? (double)doseIn.Value : -1.0);
    
    public string? GetLastGrindSetting()
        => _store.Get(KeyLastGrindSetting, null);
    
    public void SetLastGrindSetting(string? grindSetting)
    {
        if (grindSetting != null)
            _store.Set(KeyLastGrindSetting, grindSetting);
        else
            _store.Remove(KeyLastGrindSetting);
    }
    
    public decimal? GetLastExpectedTime()
    {
        var value = _store.Get(KeyLastExpectedTime, -1.0);
        return value == -1.0 ? null : (decimal)value;
    }
    
    public void SetLastExpectedTime(decimal? expectedTime)
        => _store.Set(KeyLastExpectedTime, expectedTime.HasValue ? (double)expectedTime.Value : -1.0);
    
    public decimal? GetLastExpectedOutput()
    {
        var value = _store.Get(KeyLastExpectedOutput, -1.0);
        return value == -1.0 ? null : (decimal)value;
    }
    
    public void SetLastExpectedOutput(decimal? expectedOutput)
        => _store.Set(KeyLastExpectedOutput, expectedOutput.HasValue ? (double)expectedOutput.Value : -1.0);
    
    public decimal? GetLastPreinfusionTime()
    {
        var value = _store.Get(KeyLastPreinfusionTime, -1.0);
        return value == -1.0 ? null : (decimal)value;
    }
    
    public void SetLastPreinfusionTime(decimal? preinfusionTime)
        => _store.Set(KeyLastPreinfusionTime, preinfusionTime.HasValue ? (double)preinfusionTime.Value : -1.0);
    
    public void ClearAll()
    {
        _store.Remove(KeyLastDrinkType);
        _store.Remove(KeyLastBeanId);
        _store.Remove(KeyLastMachineId);
        _store.Remove(KeyLastGrinderId);
        _store.Remove(KeyLastAccessoryIds);
        _store.Remove(KeyLastMadeById);
        _store.Remove(KeyLastMadeForId);
        _store.Remove(KeyLastDoseIn);
        _store.Remove(KeyLastGrindSetting);
        _store.Remove(KeyLastExpectedTime);
        _store.Remove(KeyLastExpectedOutput);
        _store.Remove(KeyLastPreinfusionTime);
    }
}
