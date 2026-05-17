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

    public async Task<List<Recipe>> GetByBeanAsync(int beanId)
    {
        return await _dbSet
            .Where(r => r.BeanId == beanId && !r.IsDeleted)
            .OrderBy(r => r.BrewMethod)
            .ToListAsync();
    }

    public async Task<Recipe?> GetByBeanAndMethodAsync(int beanId, BrewMethod method)
    {
        return await _dbSet
            .Where(r => r.BeanId == beanId && r.BrewMethod == method && !r.IsDeleted)
            .OrderByDescending(r => r.IsEditedByUser)
            .ThenByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync();
    }

    public async Task DeleteByBeanAsync(int beanId)
    {
        var recipes = await _dbSet
            .Where(r => r.BeanId == beanId && !r.IsDeleted)
            .ToListAsync();
        foreach (var r in recipes)
        {
            r.IsDeleted = true;
            r.LastModifiedAt = DateTime.Now;
        }
        await _context.SaveChangesAsync();
    }
}
