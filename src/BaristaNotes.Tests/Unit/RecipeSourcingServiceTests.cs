using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Recipes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaristaNotes.Tests.Unit;

public class RecipeSourcingServiceTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    private readonly BeanRepository _beanRepo;
    private readonly RecipeRepository _recipeRepo;
    private readonly RecipeService _recipeService;
    private readonly Bean _bean;

    public RecipeSourcingServiceTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase($"SourcingTest_{Guid.NewGuid()}")
            .Options;
        _context = new BaristaNotesContext(options);
        _beanRepo = new BeanRepository(_context);
        _recipeRepo = new RecipeRepository(_context);
        _recipeService = new RecipeService(_recipeRepo);

        _bean = new Bean
        {
            Name = "Monarch",
            Roaster = "Onyx Coffee Lab",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(_bean);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SourceRecipesAsync_ReturnsEmpty_WhenNoAdapterAndNoAI()
    {
        _bean.Roaster = "Unknown Roaster";
        _context.SaveChanges();

        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(Array.Empty<IRoasterRecipeAdapter>()),
            aiGenerator: null);

        var result = await service.SourceRecipesAsync(_bean.Id);

        Assert.Empty(result);
        Assert.Equal(0, await _context.Recipes.CountAsync());
    }

    [Fact]
    public async Task SourceRecipesAsync_FallsBackToAI_WhenAdapterReturnsEmpty()
    {
        var emptyAdapter = new FakeAdapter("onyx", "Onyx Coffee Lab", Array.Empty<ScrapedRecipe>());
        var aiGen = new FakeAIGenerator(
            isAvailable: true,
            recipes: new[]
            {
                new ScrapedRecipe { BrewMethod = BrewMethod.Espresso, DoseIn = 18m, OutputAmount = 36m },
                new ScrapedRecipe { BrewMethod = BrewMethod.PourOver, DoseIn = 22m, OutputAmount = 360m }
            });
        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(new IRoasterRecipeAdapter[] { emptyAdapter }),
            aiGen);

        var result = await service.SourceRecipesAsync(_bean.Id);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(RecipeSource.AIGenerated, r.Source));
    }

    [Fact]
    public async Task SourceRecipesAsync_UsesAdapter_WhenItReturnsRecipes()
    {
        var adapter = new FakeAdapter("onyx", "Onyx Coffee Lab",
            new[]
            {
                new ScrapedRecipe { BrewMethod = BrewMethod.Espresso, DoseIn = 18m, OutputAmount = 36m }
            });
        var aiGen = new FakeAIGenerator(isAvailable: true, recipes: Array.Empty<ScrapedRecipe>());

        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(new IRoasterRecipeAdapter[] { adapter }),
            aiGen);

        var result = await service.SourceRecipesAsync(_bean.Id);

        var recipe = Assert.Single(result);
        Assert.Equal(RecipeSource.RoasterSite, recipe.Source);
        Assert.False(aiGen.WasCalled, "AI fallback should not be invoked when adapter succeeds");
    }

    [Fact]
    public async Task SourceRecipesAsync_IsIdempotent_OnSecondRun()
    {
        var adapter = new FakeAdapter("onyx", "Onyx Coffee Lab",
            new[]
            {
                new ScrapedRecipe { BrewMethod = BrewMethod.Espresso, DoseIn = 18m, OutputAmount = 36m }
            });
        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(new IRoasterRecipeAdapter[] { adapter }));

        await service.SourceRecipesAsync(_bean.Id);
        await service.SourceRecipesAsync(_bean.Id);

        Assert.Equal(1, await _context.Recipes.CountAsync(r => !r.IsDeleted));
    }

    [Fact]
    public async Task SourceRecipesAsync_PreservesUserEditsOnReSource()
    {
        var adapter = new FakeAdapter("onyx", "Onyx Coffee Lab",
            new[]
            {
                new ScrapedRecipe { BrewMethod = BrewMethod.Espresso, DoseIn = 18m, OutputAmount = 36m }
            });
        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(new IRoasterRecipeAdapter[] { adapter }));

        var first = await service.SourceRecipesAsync(_bean.Id);
        await _recipeService.UpdateAsync(first[0].Id,
            new Core.Services.DTOs.UpdateRecipeDto { DoseIn = 19m });

        adapter.Recipes = new[]
        {
            new ScrapedRecipe { BrewMethod = BrewMethod.Espresso, DoseIn = 21m, OutputAmount = 42m }
        };
        var second = await service.SourceRecipesAsync(_bean.Id);

        Assert.Equal(19m, second[0].DoseIn);
        Assert.True(second[0].IsEditedByUser);
    }

    [Fact]
    public async Task SourceRecipesAsync_DoesNotThrow_WhenAdapterThrows()
    {
        var throwingAdapter = new ThrowingAdapter();
        var aiGen = new FakeAIGenerator(isAvailable: true, recipes: Array.Empty<ScrapedRecipe>());
        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(new IRoasterRecipeAdapter[] { throwingAdapter }),
            aiGen);

        var result = await service.SourceRecipesAsync(_bean.Id);

        // Throwing adapter propagates to sourcing service, which swallows it.
        Assert.Empty(result);
    }

    [Fact]
    public async Task SourceRecipesAsync_ReturnsEmpty_WhenBeanMissing()
    {
        var service = new RecipeSourcingService(
            _beanRepo, _recipeService,
            new RoasterRecipeAdapterRegistry(Array.Empty<IRoasterRecipeAdapter>()));

        var result = await service.SourceRecipesAsync(99999);

        Assert.Empty(result);
    }

    private sealed class FakeAdapter : IRoasterRecipeAdapter
    {
        public string Id { get; }
        public string RoasterName { get; }
        public IReadOnlyList<ScrapedRecipe> Recipes { get; set; }

        public FakeAdapter(string id, string roasterName, IReadOnlyList<ScrapedRecipe> recipes)
        {
            Id = id; RoasterName = roasterName; Recipes = recipes;
        }

        public bool CanHandle(Bean bean) =>
            string.Equals(bean.Roaster, RoasterName, StringComparison.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ScrapedRecipe>> FetchAsync(Bean bean, CancellationToken ct)
            => Task.FromResult(Recipes);
    }

    private sealed class ThrowingAdapter : IRoasterRecipeAdapter
    {
        public string Id => "throwing";
        public string RoasterName => "Onyx Coffee Lab";
        public bool CanHandle(Bean bean) => true;
        public Task<IReadOnlyList<ScrapedRecipe>> FetchAsync(Bean bean, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    private sealed class FakeAIGenerator : IAIRecipeGenerator
    {
        private readonly IReadOnlyList<ScrapedRecipe> _recipes;
        public bool WasCalled { get; private set; }
        public bool IsAvailable { get; }

        public FakeAIGenerator(bool isAvailable, IReadOnlyList<ScrapedRecipe> recipes)
        {
            IsAvailable = isAvailable;
            _recipes = recipes;
        }

        public Task<IReadOnlyList<ScrapedRecipe>> GenerateAsync(Bean bean, CancellationToken ct)
        {
            WasCalled = true;
            return Task.FromResult(_recipes);
        }
    }
}
