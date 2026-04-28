using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services;

public class RecipeService : IRecipeService
{
    private readonly IRecipeRepository _repository;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(IRecipeRepository repository)
        : this(repository, NullLogger<RecipeService>.Instance)
    {
    }

    public RecipeService(IRecipeRepository repository, ILogger<RecipeService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RecipeDto>> GetRecipesForBeanAsync(int beanId)
    {
        var recipes = await _repository.GetByBeanAsync(beanId);
        return recipes.Select(MapToDto).ToList();
    }

    public async Task<RecipeDto?> GetRecipeForBeanAndMethodAsync(int beanId, BrewMethod method)
    {
        var recipe = await _repository.GetByBeanAndMethodAsync(beanId, method);
        return recipe == null ? null : MapToDto(recipe);
    }

    public async Task<RecipeDto?> GetByIdAsync(int id)
    {
        var recipe = await _repository.GetByIdAsync(id);
        return recipe == null ? null : MapToDto(recipe);
    }

    public async Task<RecipeDto> CreateAsync(CreateRecipeDto dto)
    {
        var entity = new Recipe
        {
            BeanId = dto.BeanId,
            BrewMethod = dto.BrewMethod,
            Source = dto.Source,
            SourceUrl = dto.SourceUrl,
            Title = dto.Title,
            DoseIn = dto.DoseIn,
            OutputAmount = dto.OutputAmount,
            GrindHint = dto.GrindHint,
            BrewTempC = dto.BrewTempC,
            TotalTimeSeconds = dto.TotalTimeSeconds,
            ParametersJson = dto.ParametersJson,
            Notes = dto.Notes,
            FetchedAt = DateTime.Now,
            IsEditedByUser = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };

        var created = await _repository.AddAsync(entity);
        _logger.LogInformation(
            "Created recipe RecipeId={RecipeId} BeanId={BeanId} Method={Method} Source={Source}",
            created.Id, created.BeanId, created.BrewMethod, created.Source);
        return MapToDto(created);
    }

    public async Task<RecipeDto> UpsertFromSourceAsync(CreateRecipeDto dto)
    {
        var existing = await _repository.GetByBeanAndMethodAsync(dto.BeanId, dto.BrewMethod);

        if (existing == null)
            return await CreateAsync(dto);

        if (existing.IsEditedByUser)
        {
            _logger.LogDebug(
                "Skipping upsert for RecipeId={RecipeId}: user edits preserved",
                existing.Id);
            return MapToDto(existing);
        }

        existing.Source = dto.Source;
        existing.SourceUrl = dto.SourceUrl;
        existing.Title = dto.Title;
        existing.DoseIn = dto.DoseIn;
        existing.OutputAmount = dto.OutputAmount;
        existing.GrindHint = dto.GrindHint;
        existing.BrewTempC = dto.BrewTempC;
        existing.TotalTimeSeconds = dto.TotalTimeSeconds;
        existing.ParametersJson = dto.ParametersJson;
        existing.Notes = dto.Notes;
        existing.FetchedAt = DateTime.Now;
        existing.LastModifiedAt = DateTime.Now;

        var updated = await _repository.UpdateAsync(existing);
        _logger.LogInformation(
            "Upserted recipe RecipeId={RecipeId} BeanId={BeanId} Method={Method}",
            updated.Id, updated.BeanId, updated.BrewMethod);
        return MapToDto(updated);
    }

    public async Task<RecipeDto> UpdateAsync(int id, UpdateRecipeDto dto)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
            throw new EntityNotFoundException(nameof(Recipe), id);

        if (dto.Title != null) entity.Title = dto.Title;
        if (dto.DoseIn.HasValue) entity.DoseIn = dto.DoseIn;
        if (dto.OutputAmount.HasValue) entity.OutputAmount = dto.OutputAmount;
        if (dto.GrindHint != null) entity.GrindHint = dto.GrindHint;
        if (dto.BrewTempC.HasValue) entity.BrewTempC = dto.BrewTempC;
        if (dto.TotalTimeSeconds.HasValue) entity.TotalTimeSeconds = dto.TotalTimeSeconds;
        if (dto.ParametersJson != null) entity.ParametersJson = dto.ParametersJson;
        if (dto.Notes != null) entity.Notes = dto.Notes;

        entity.IsEditedByUser = true;
        entity.LastModifiedAt = DateTime.Now;

        var updated = await _repository.UpdateAsync(entity);
        return MapToDto(updated);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null)
            throw new EntityNotFoundException(nameof(Recipe), id);

        entity.IsDeleted = true;
        entity.LastModifiedAt = DateTime.Now;
        await _repository.UpdateAsync(entity);
    }

    private static RecipeDto MapToDto(Recipe r) => new()
    {
        Id = r.Id,
        BeanId = r.BeanId,
        BrewMethod = r.BrewMethod,
        Source = r.Source,
        SourceUrl = r.SourceUrl,
        Title = r.Title,
        DoseIn = r.DoseIn,
        OutputAmount = r.OutputAmount,
        GrindHint = r.GrindHint,
        BrewTempC = r.BrewTempC,
        TotalTimeSeconds = r.TotalTimeSeconds,
        ParametersJson = r.ParametersJson,
        Notes = r.Notes,
        FetchedAt = r.FetchedAt,
        IsEditedByUser = r.IsEditedByUser
    };
}
