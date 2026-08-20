using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data.Repositories;

public interface IRecipeRepository : IRepository<Recipe>
{
    Task<List<Recipe>> GetByBeanAsync(int beanId);
    Task<Recipe?> GetByBeanAndMethodAsync(int beanId, BrewMethod method);
    Task DeleteByBeanAsync(int beanId);
}

public class RecipeRepository : Repository<Recipe>, IRecipeRepository
{
    public RecipeRepository(BaristaNotesContext context) : base(context) { }

    public override Task<Recipe?> GetByIdAsync(int id)
    {
        var recipeId = id;
        return _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId);
    }

    public override Task<List<Recipe>> GetAllAsync()
        => _context.Recipes.ToListAsync();

    public async Task<List<Recipe>> GetByBeanAsync(int beanId)
    {
        var targetBeanId = beanId;
        return await _context.Recipes
            .Where(r => r.BeanId == targetBeanId && !r.IsDeleted)
            .OrderBy(r => r.BrewMethod)
            .ToListAsync();
    }

    public async Task<Recipe?> GetByBeanAndMethodAsync(int beanId, BrewMethod method)
    {
        var targetBeanId = beanId;
        var targetMethod = method;
        return await _context.Recipes
            .Where(r => r.BeanId == targetBeanId
                && r.BrewMethod == targetMethod
                && !r.IsDeleted)
            .OrderByDescending(r => r.IsEditedByUser)
            .ThenByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteByBeanAsync(int beanId)
    {
        var targetBeanId = beanId;
        var recipes = await _context.Recipes
            .Where(r => r.BeanId == targetBeanId && !r.IsDeleted)
            .ToListAsync();
        foreach (var r in recipes)
        {
            r.IsDeleted = true;
            r.LastModifiedAt = DateTime.Now;
        }
        await _context.SaveChangesAsync();
    }
}
