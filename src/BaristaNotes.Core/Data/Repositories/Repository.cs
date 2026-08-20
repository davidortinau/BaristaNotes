using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Core.Data.Repositories;

public abstract class Repository<T> : IRepository<T> where T : class
{
    protected readonly BaristaNotesContext _context;
    protected readonly DbSet<T> _dbSet;
    
    public Repository(BaristaNotesContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }
    
    public abstract Task<T?> GetByIdAsync(int id);

    public abstract Task<List<T>> GetAllAsync();
    
    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
    
    public virtual async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
    
    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
