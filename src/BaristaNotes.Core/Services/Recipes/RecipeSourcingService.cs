using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Orchestrates recipe sourcing for a bean: try the matching roaster adapter,
/// fall back to AI generation when the adapter is missing or returns nothing,
/// then persist each result via <see cref="IRecipeService.UpsertFromSourceAsync"/>
/// so user-edited recipes are preserved.
/// </summary>
public interface IRecipeSourcingService
{
    /// <summary>
    /// Fetches recipes for the given bean and persists them. Returns the
    /// resulting (persisted) recipes. Never throws; failures are logged and
    /// produce an empty list.
    /// </summary>
    Task<IReadOnlyList<RecipeDto>> SourceRecipesAsync(int beanId, CancellationToken ct = default);
}

public sealed class RecipeSourcingService : IRecipeSourcingService
{
    private readonly IBeanRepository _beanRepository;
    private readonly IRecipeService _recipeService;
    private readonly IRoasterRecipeAdapterRegistry _registry;
    private readonly IAIRecipeGenerator? _aiGenerator;
    private readonly ILogger<RecipeSourcingService> _logger;

    public RecipeSourcingService(
        IBeanRepository beanRepository,
        IRecipeService recipeService,
        IRoasterRecipeAdapterRegistry registry,
        IAIRecipeGenerator? aiGenerator = null,
        ILogger<RecipeSourcingService>? logger = null)
    {
        _beanRepository = beanRepository;
        _recipeService = recipeService;
        _registry = registry;
        _aiGenerator = aiGenerator;
        _logger = logger ?? NullLogger<RecipeSourcingService>.Instance;
    }

    public async Task<IReadOnlyList<RecipeDto>> SourceRecipesAsync(
        int beanId, CancellationToken ct = default)
    {
        try
        {
            var bean = await _beanRepository.GetByIdAsync(beanId);
            if (bean == null || bean.IsDeleted)
            {
                _logger.LogDebug("SourceRecipes: BeanId={BeanId} not found", beanId);
                return Array.Empty<RecipeDto>();
            }

            RecipeSource source = RecipeSource.RoasterSite;
            IReadOnlyList<ScrapedRecipe> scraped = Array.Empty<ScrapedRecipe>();

            var adapter = _registry.FindAdapter(bean);
            if (adapter != null)
            {
                _logger.LogInformation(
                    "SourceRecipes: using adapter {AdapterId} for BeanId={BeanId} Roaster={Roaster}",
                    adapter.Id, bean.Id, bean.Roaster);
                scraped = await adapter.FetchAsync(bean, ct);
            }

            if (scraped.Count == 0)
            {
                if (_aiGenerator is { IsAvailable: true })
                {
                    _logger.LogInformation(
                        "SourceRecipes: falling back to AI for BeanId={BeanId}", bean.Id);
                    scraped = await _aiGenerator.GenerateAsync(bean, ct);
                    source = RecipeSource.AIGenerated;
                }
                else
                {
                    _logger.LogInformation(
                        "SourceRecipes: no adapter match and AI unavailable for BeanId={BeanId}",
                        bean.Id);
                    return Array.Empty<RecipeDto>();
                }
            }

            if (scraped.Count == 0)
                return Array.Empty<RecipeDto>();

            var persisted = new List<RecipeDto>(scraped.Count);
            foreach (var s in scraped)
            {
                ct.ThrowIfCancellationRequested();
                var dto = new CreateRecipeDto
                {
                    BeanId = bean.Id,
                    BrewMethod = s.BrewMethod,
                    Source = source,
                    SourceUrl = s.SourceUrl,
                    Title = s.Title,
                    DoseIn = s.DoseIn,
                    OutputAmount = s.OutputAmount,
                    GrindHint = s.GrindHint,
                    BrewTempC = s.BrewTempC,
                    TotalTimeSeconds = s.TotalTimeSeconds,
                    ParametersJson = s.ParametersJson,
                    Notes = s.Notes
                };
                var result = await _recipeService.UpsertFromSourceAsync(dto);
                persisted.Add(result);
            }

            _logger.LogInformation(
                "SourceRecipes: persisted {Count} recipes for BeanId={BeanId} Source={Source}",
                persisted.Count, bean.Id, source);
            return persisted;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug("SourceRecipes: cancelled BeanId={BeanId}", beanId);
            return Array.Empty<RecipeDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SourceRecipes: unexpected error BeanId={BeanId}", beanId);
            return Array.Empty<RecipeDto>();
        }
    }
}
