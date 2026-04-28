using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaristaNotes.Tests.Unit;

public class RecipeServiceTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    private readonly RecipeService _service;
    private readonly Bean _bean;

    public RecipeServiceTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: $"RecipeServiceTest_{Guid.NewGuid()}")
            .Options;

        _context = new BaristaNotesContext(options);
        var repo = new RecipeRepository(_context);
        _service = new RecipeService(repo);

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
    public async Task CreateAsync_PersistsRecipe()
    {
        var dto = NewRecipeDto(BrewMethod.Espresso);

        var result = await _service.CreateAsync(dto);

        Assert.True(result.Id > 0);
        Assert.Equal(_bean.Id, result.BeanId);
        Assert.Equal(BrewMethod.Espresso, result.BrewMethod);
        Assert.Equal(RecipeSource.RoasterSite, result.Source);
        Assert.False(result.IsEditedByUser);
        Assert.Equal(1, await _context.Recipes.CountAsync());
    }

    [Fact]
    public async Task UpsertFromSourceAsync_InsertsWhenMissing()
    {
        var result = await _service.UpsertFromSourceAsync(NewRecipeDto(BrewMethod.PourOver));

        Assert.Equal(BrewMethod.PourOver, result.BrewMethod);
        Assert.Equal(1, await _context.Recipes.CountAsync());
    }

    [Fact]
    public async Task UpsertFromSourceAsync_UpdatesExistingForSameMethod()
    {
        await _service.UpsertFromSourceAsync(NewRecipeDto(BrewMethod.Espresso, doseIn: 18m));

        var updated = await _service.UpsertFromSourceAsync(
            NewRecipeDto(BrewMethod.Espresso, doseIn: 20m));

        Assert.Equal(20m, updated.DoseIn);
        Assert.Equal(1, await _context.Recipes.CountAsync());
    }

    [Fact]
    public async Task UpsertFromSourceAsync_PreservesUserEdits()
    {
        var created = await _service.CreateAsync(NewRecipeDto(BrewMethod.Espresso, doseIn: 18m));
        await _service.UpdateAsync(created.Id, new UpdateRecipeDto { DoseIn = 19m });

        var result = await _service.UpsertFromSourceAsync(
            NewRecipeDto(BrewMethod.Espresso, doseIn: 21m));

        Assert.Equal(19m, result.DoseIn);
        Assert.True(result.IsEditedByUser);
    }

    [Fact]
    public async Task UpdateAsync_SetsIsEditedByUser()
    {
        var created = await _service.CreateAsync(NewRecipeDto(BrewMethod.Espresso));

        var updated = await _service.UpdateAsync(created.Id, new UpdateRecipeDto
        {
            Notes = "Tweaked"
        });

        Assert.True(updated.IsEditedByUser);
        Assert.Equal("Tweaked", updated.Notes);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenMissing()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _service.UpdateAsync(999, new UpdateRecipeDto { Notes = "x" }));
    }

    [Fact]
    public async Task GetRecipesForBeanAsync_ReturnsOnlyNonDeletedForBean()
    {
        await _service.CreateAsync(NewRecipeDto(BrewMethod.Espresso));
        await _service.CreateAsync(NewRecipeDto(BrewMethod.PourOver));

        var otherBean = new Bean
        {
            Name = "Other",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(otherBean);
        _context.SaveChanges();
        await _service.CreateAsync(NewRecipeDto(BrewMethod.Moka, beanId: otherBean.Id));

        var results = await _service.GetRecipesForBeanAsync(_bean.Id);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(_bean.Id, r.BeanId));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var created = await _service.CreateAsync(NewRecipeDto(BrewMethod.Espresso));

        await _service.DeleteAsync(created.Id);

        var fetched = await _context.Recipes.FindAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsDeleted);
        Assert.Empty(await _service.GetRecipesForBeanAsync(_bean.Id));
    }

    private CreateRecipeDto NewRecipeDto(
        BrewMethod method,
        int? beanId = null,
        decimal? doseIn = null) => new()
    {
        BeanId = beanId ?? _bean.Id,
        BrewMethod = method,
        Source = RecipeSource.RoasterSite,
        SourceUrl = "https://example.com/monarch",
        Title = $"{method} recipe",
        DoseIn = doseIn ?? 18m,
        OutputAmount = 36m,
        GrindHint = "medium-fine",
        BrewTempC = 94m,
        TotalTimeSeconds = 28m,
        Notes = "Example"
    };
}
