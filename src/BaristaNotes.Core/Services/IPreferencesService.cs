namespace BaristaNotes.Core.Services;

public interface IPreferencesService
{
    string? GetLastDrinkType();
    void SetLastDrinkType(string drinkType);
    
    int? GetLastBeanId();
    void SetLastBeanId(int? beanId);
    
    int? GetLastBagId();  // Added for Phase 4 (T039)
    void SetLastBagId(int? bagId);
    
    int? GetLastMachineId();
    void SetLastMachineId(int? machineId);
    
    int? GetLastGrinderId();
    void SetLastGrinderId(int? grinderId);
    
    List<int> GetLastAccessoryIds();
    void SetLastAccessoryIds(List<int> accessoryIds);
    
    int? GetLastMadeById();
    void SetLastMadeById(int? madeById);
    
    int? GetLastMadeForId();
    void SetLastMadeForId(int? madeForId);
    
    decimal? GetLastDoseIn();
    void SetLastDoseIn(decimal? doseIn);
    
    string? GetLastGrindSetting();
    void SetLastGrindSetting(string? grindSetting);
    
    decimal? GetLastExpectedTime();
    void SetLastExpectedTime(decimal? expectedTime);
    
    decimal? GetLastExpectedOutput();
    void SetLastExpectedOutput(decimal? expectedOutput);
    
    decimal? GetLastPreinfusionTime();
    void SetLastPreinfusionTime(decimal? preinfusionTime);
    
    void ClearAll();
}

public interface IPreferencesStore
{
    string? Get(string key, string? defaultValue);
    void Set(string key, string value);
    int Get(string key, int defaultValue);
    void Set(string key, int value);
    double Get(string key, double defaultValue);
    void Set(string key, double value);
    void Remove(string key);
}
