using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services;

public interface IEquipmentService
{
    Task<List<EquipmentDto>> GetAllActiveEquipmentAsync();
    Task<List<EquipmentDto>> GetEquipmentByTypeAsync(EquipmentType type);
    Task<EquipmentDto?> GetEquipmentByIdAsync(int id);
    Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentDto dto);
    Task<EquipmentDto> UpdateEquipmentAsync(int id, UpdateEquipmentDto dto);
    Task ArchiveEquipmentAsync(int id);
    Task DeleteEquipmentAsync(int id);
}
