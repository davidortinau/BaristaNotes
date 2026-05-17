using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// CRUD + query API for <see cref="Models.Recipe"/>. Raises
/// <see cref="IDataChangeNotifier"/> events on mutation.
/// </summary>
public interface IRecipeService
{
    Task<IReadOnlyList<RecipeDto>> GetRecipesForBeanAsync(int beanId);

    Task<RecipeDto?> GetRecipeForBeanAndMethodAsync(int beanId, BrewMethod method);

    Task<RecipeDto?> GetByIdAsync(int id);

    /// <summary>Persists a new recipe (from scraping, AI, or manual entry).</summary>
    Task<RecipeDto> CreateAsync(CreateRecipeDto dto);

    /// <summary>
    /// Upserts a recipe for (BeanId, BrewMethod, Source). Used by the recipe sourcing
    /// pipeline so re-running sourcing replaces the previous result rather than
    /// duplicating it — unless the user has edited the recipe, in which case we
    /// keep their edits and skip the overwrite.
    /// </summary>
    Task<RecipeDto> UpsertFromSourceAsync(CreateRecipeDto dto);

    /// <summary>Updates a recipe and flags <c>IsEditedByUser = true</c>.</summary>
    Task<RecipeDto> UpdateAsync(int id, UpdateRecipeDto dto);

    Task DeleteAsync(int id);
}
