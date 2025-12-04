using Xunit;
using BaristaNotes.Core.Services;
using BaristaNotes.Tests.Mocks;

namespace BaristaNotes.Tests.Unit.Services;

public class PreferencesServiceTests
{
    private readonly MockPreferencesStore _store;
    private readonly PreferencesService _service;
    
    public PreferencesServiceTests()
    {
        _store = new MockPreferencesStore();
        _service = new PreferencesService(_store);
        // Clear all preferences before each test
        _store.Clear();
    }
    
    [Fact]
    public void GetLastDrinkType_NoValueSet_ReturnsNull()
    {
        // Act
        var result = _service.GetLastDrinkType();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void SetLastDrinkType_ThenGet_ReturnsSetValue()
    {
        // Arrange
        const string expected = "Latte";
        
        // Act
        _service.SetLastDrinkType(expected);
        var result = _service.GetLastDrinkType();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetLastBeanId_NoValueSet_ReturnsNull()
    {
        // Act
        var result = _service.GetLastBeanId();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void SetLastBeanId_ThenGet_ReturnsSetValue()
    {
        // Arrange
        const int expected = 42;
        
        // Act
        _service.SetLastBeanId(expected);
        var result = _service.GetLastBeanId();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void SetLastBeanId_Null_ThenGet_ReturnsNull()
    {
        // Arrange
        _service.SetLastBeanId(42); // Set a value first
        
        // Act
        _service.SetLastBeanId(null); // Clear it
        var result = _service.GetLastBeanId();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void GetLastMachineId_NoValueSet_ReturnsNull()
    {
        // Act
        var result = _service.GetLastMachineId();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void SetLastMachineId_ThenGet_ReturnsSetValue()
    {
        // Arrange
        const int expected = 10;
        
        // Act
        _service.SetLastMachineId(expected);
        var result = _service.GetLastMachineId();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetLastGrinderId_NoValueSet_ReturnsNull()
    {
        // Act
        var result = _service.GetLastGrinderId();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void SetLastGrinderId_ThenGet_ReturnsSetValue()
    {
        // Arrange
        const int expected = 20;
        
        // Act
        _service.SetLastGrinderId(expected);
        var result = _service.GetLastGrinderId();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetLastAccessoryIds_NoValueSet_ReturnsEmptyList()
    {
        // Act
        var result = _service.GetLastAccessoryIds();
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
    
    [Fact]
    public void SetLastAccessoryIds_ThenGet_ReturnsSetValue()
    {
        // Arrange
        var expected = new List<int> { 1, 2, 3 };
        
        // Act
        _service.SetLastAccessoryIds(expected);
        var result = _service.GetLastAccessoryIds();
        
        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void SetLastAccessoryIds_EmptyList_ThenGet_ReturnsEmptyList()
    {
        // Arrange
        _service.SetLastAccessoryIds(new List<int> { 1, 2, 3 }); // Set a value first
        
        // Act
        _service.SetLastAccessoryIds(new List<int>()); // Clear it
        var result = _service.GetLastAccessoryIds();
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
    
    [Fact]
    public void GetLastMadeById_NoValueSet_ReturnsNull()
    {
        // Act
        var result = _service.GetLastMadeById();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void SetLastMadeById_ThenGet_ReturnsSetValue()
    {
        // Arrange
        const int expected = 5;
        
        // Act
        _service.SetLastMadeById(expected);
        var result = _service.GetLastMadeById();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void GetLastMadeForId_NoValueSet_ReturnsNull()
    {
        // Act
        var result = _service.GetLastMadeForId();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void SetLastMadeForId_ThenGet_ReturnsSetValue()
    {
        // Arrange
        const int expected = 7;
        
        // Act
        _service.SetLastMadeForId(expected);
        var result = _service.GetLastMadeForId();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void ClearAll_RemovesAllPreferences()
    {
        // Arrange
        _service.SetLastDrinkType("Espresso");
        _service.SetLastBeanId(1);
        _service.SetLastMachineId(2);
        _service.SetLastGrinderId(3);
        _service.SetLastAccessoryIds(new List<int> { 4, 5 });
        _service.SetLastMadeById(6);
        _service.SetLastMadeForId(7);
        
        // Act
        _service.ClearAll();
        
        // Assert
        Assert.Null(_service.GetLastDrinkType());
        Assert.Null(_service.GetLastBeanId());
        Assert.Null(_service.GetLastMachineId());
        Assert.Null(_service.GetLastGrinderId());
        Assert.Empty(_service.GetLastAccessoryIds());
        Assert.Null(_service.GetLastMadeById());
        Assert.Null(_service.GetLastMadeForId());
    }
}
