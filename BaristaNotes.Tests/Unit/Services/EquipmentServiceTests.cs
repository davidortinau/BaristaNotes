using Xunit;
using Moq;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Tests.Unit.Services;

public class EquipmentServiceTests
{
    private readonly Mock<IEquipmentRepository> _mockRepository;
    private readonly EquipmentService _service;

    public EquipmentServiceTests()
    {
        _mockRepository = new Mock<IEquipmentRepository>();
        _service = new EquipmentService(_mockRepository.Object);
    }

    [Fact]
    public async Task CreateEquipmentAsync_WithValidData_CreatesEquipment()
    {
        // Arrange
        var createDto = new CreateEquipmentDto
        {
            Name = "Rocket Appartamento",
            Type = EquipmentType.Machine,
            Notes = "Heat exchange machine"
        };

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Equipment>()))
            .ReturnsAsync((Equipment e) => e);

        // Act
        var result = await _service.CreateEquipmentAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Name, result.Name);
        Assert.Equal(createDto.Type, result.Type);
        Assert.Equal(createDto.Notes, result.Notes);
        Assert.True(result.IsActive);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Equipment>()), Times.Once);
    }

    [Fact]
    public async Task CreateEquipmentAsync_WithEmptyName_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateEquipmentDto
        {
            Name = "",
            Type = EquipmentType.Machine
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _service.CreateEquipmentAsync(createDto));
    }

    [Fact]
    public async Task CreateEquipmentAsync_WithNameTooLong_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateEquipmentDto
        {
            Name = new string('A', 101),
            Type = EquipmentType.Machine
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _service.CreateEquipmentAsync(createDto));
    }

    [Fact]
    public async Task ArchiveEquipmentAsync_ExistingEquipment_SetsIsActiveFalse()
    {
        // Arrange
        var equipment = new Equipment
        {
            Id = 1,
            Name = "Old Grinder",
            Type = EquipmentType.Grinder,
            IsActive = true,
            SyncId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.Now,
            LastModifiedAt = DateTimeOffset.Now
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(equipment);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Equipment>()))
            .ReturnsAsync((Equipment e) => e);

        // Act
        await _service.ArchiveEquipmentAsync(1);

        // Assert
        Assert.False(equipment.IsActive);
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<Equipment>(e => !e.IsActive)), Times.Once);
    }

    [Fact]
    public async Task ArchiveEquipmentAsync_NonExistentEquipment_ThrowsEntityNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Equipment?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => 
            _service.ArchiveEquipmentAsync(999));
    }

    [Fact]
    public async Task GetAllActiveEquipmentAsync_ReturnsOnlyActiveEquipment()
    {
        // Arrange
        var equipment = new List<Equipment>
        {
            new Equipment { Id = 1, Name = "Active Machine", Type = EquipmentType.Machine, IsActive = true, SyncId = Guid.NewGuid(), CreatedAt = DateTimeOffset.Now, LastModifiedAt = DateTimeOffset.Now },
            new Equipment { Id = 2, Name = "Archived Machine", Type = EquipmentType.Machine, IsActive = false, SyncId = Guid.NewGuid(), CreatedAt = DateTimeOffset.Now, LastModifiedAt = DateTimeOffset.Now }
        };

        _mockRepository
            .Setup(r => r.GetActiveEquipmentAsync())
            .ReturnsAsync(equipment.Where(e => e.IsActive).ToList());

        // Act
        var result = await _service.GetAllActiveEquipmentAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Active Machine", result.First().Name);
    }

    [Fact]
    public async Task GetEquipmentByTypeAsync_FiltersCorrectly()
    {
        // Arrange
        var equipment = new List<Equipment>
        {
            new Equipment { Id = 1, Name = "Machine 1", Type = EquipmentType.Machine, IsActive = true, SyncId = Guid.NewGuid(), CreatedAt = DateTimeOffset.Now, LastModifiedAt = DateTimeOffset.Now },
            new Equipment { Id = 2, Name = "Grinder 1", Type = EquipmentType.Grinder, IsActive = true, SyncId = Guid.NewGuid(), CreatedAt = DateTimeOffset.Now, LastModifiedAt = DateTimeOffset.Now }
        };

        _mockRepository
            .Setup(r => r.GetByTypeAsync(EquipmentType.Grinder))
            .ReturnsAsync(equipment.Where(e => e.Type == EquipmentType.Grinder).ToList());

        // Act
        var result = await _service.GetEquipmentByTypeAsync(EquipmentType.Grinder);

        // Assert
        Assert.Single(result);
        Assert.Equal("Grinder 1", result.First().Name);
    }
}
