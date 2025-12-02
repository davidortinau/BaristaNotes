namespace BaristaNotes.Core.Services;

public interface IPreferencesService
{
    string? GetLastDrinkType();
    void SetLastDrinkType(string drinkType);
    
    int? GetLastBeanId();
    void SetLastBeanId(int? beanId);
    
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
    
    void ClearAll();
}

public interface IPreferencesStore
{
    string? Get(string key, string? defaultValue);
    void Set(string key, string value);
    int Get(string key, int defaultValue);
    void Set(string key, int value);
    void Remove(string key);
}
