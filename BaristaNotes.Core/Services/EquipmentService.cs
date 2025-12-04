using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Core.Services;

public class EquipmentService : IEquipmentService
{
    private readonly IEquipmentRepository _equipmentRepository;
    
    public EquipmentService(IEquipmentRepository equipmentRepository)
    {
        _equipmentRepository = equipmentRepository;
    }
    
    public async Task<List<EquipmentDto>> GetAllActiveEquipmentAsync()
    {
        var equipment = await _equipmentRepository.GetActiveEquipmentAsync();
        return equipment.Select(MapToDto).ToList();
    }
    
    public async Task<List<EquipmentDto>> GetEquipmentByTypeAsync(EquipmentType type)
    {
        var equipment = await _equipmentRepository.GetByTypeAsync(type);
        return equipment.Select(MapToDto).ToList();
    }
    
    public async Task<EquipmentDto?> GetEquipmentByIdAsync(int id)
    {
        var equipment = await _equipmentRepository.GetByIdAsync(id);
        return equipment == null ? null : MapToDto(equipment);
    }
    
    public async Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentDto dto)
    {
        ValidateCreateEquipment(dto);
        
        var equipment = new Equipment
        {
            Name = dto.Name,
            Type = dto.Type,
            Notes = dto.Notes,
            IsActive = true,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var created = await _equipmentRepository.AddAsync(equipment);
        return MapToDto(created);
    }
    
    public async Task<EquipmentDto> UpdateEquipmentAsync(int id, UpdateEquipmentDto dto)
    {
        var equipment = await _equipmentRepository.GetByIdAsync(id);
        if (equipment == null)
            throw new EntityNotFoundException(nameof(Equipment), id);
        
        if (dto.Name != null)
            equipment.Name = dto.Name;
        if (dto.Type.HasValue)
            equipment.Type = dto.Type.Value;
        if (dto.Notes != null)
            equipment.Notes = dto.Notes;
        if (dto.IsActive.HasValue)
            equipment.IsActive = dto.IsActive.Value;
        
        equipment.LastModifiedAt = DateTimeOffset.Now;
        
        var updated = await _equipmentRepository.UpdateAsync(equipment);
        return MapToDto(updated);
    }
    
    public async Task ArchiveEquipmentAsync(int id)
    {
        var equipment = await _equipmentRepository.GetByIdAsync(id);
        if (equipment == null)
            throw new EntityNotFoundException(nameof(Equipment), id);
        
        equipment.IsActive = false;
        equipment.LastModifiedAt = DateTimeOffset.Now;
        await _equipmentRepository.UpdateAsync(equipment);
    }
    
    public async Task DeleteEquipmentAsync(int id)
    {
        var equipment = await _equipmentRepository.GetByIdAsync(id);
        if (equipment == null)
            throw new EntityNotFoundException(nameof(Equipment), id);
        
        equipment.IsDeleted = true;
        equipment.LastModifiedAt = DateTimeOffset.Now;
        await _equipmentRepository.UpdateAsync(equipment);
    }
    
    private EquipmentDto MapToDto(Equipment equipment) => new()
    {
        Id = equipment.Id,
        Name = equipment.Name,
        Type = equipment.Type,
        Notes = equipment.Notes,
        IsActive = equipment.IsActive,
        CreatedAt = equipment.CreatedAt
    };
    
    private void ValidateCreateEquipment(CreateEquipmentDto dto)
    {
        var errors = new Dictionary<string, List<string>>();
        
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add(nameof(dto.Name), new List<string> { "Name is required" });
        else if (dto.Name.Length > 100)
            errors.Add(nameof(dto.Name), new List<string> { "Name must be 100 characters or less" });
        
        if (dto.Notes?.Length > 500)
            errors.Add(nameof(dto.Notes), new List<string> { "Notes must be 500 characters or less" });
        
        if (errors.Any())
            throw new ValidationException(errors);
    }
}
