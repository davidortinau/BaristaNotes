using BaristaNotes.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Registry that picks the first matching <see cref="IRoasterRecipeAdapter"/>
/// for a given <see cref="Bean"/>.
/// </summary>
public interface IRoasterRecipeAdapterRegistry
{
    IRoasterRecipeAdapter? FindAdapter(Bean bean);
    IReadOnlyList<IRoasterRecipeAdapter> All { get; }
}

public class RoasterRecipeAdapterRegistry : IRoasterRecipeAdapterRegistry
{
    private readonly IReadOnlyList<IRoasterRecipeAdapter> _adapters;
    private readonly ILogger<RoasterRecipeAdapterRegistry> _logger;

    public RoasterRecipeAdapterRegistry(IEnumerable<IRoasterRecipeAdapter> adapters)
        : this(adapters, NullLogger<RoasterRecipeAdapterRegistry>.Instance)
    {
    }

    public RoasterRecipeAdapterRegistry(
        IEnumerable<IRoasterRecipeAdapter> adapters,
        ILogger<RoasterRecipeAdapterRegistry> logger)
    {
        _adapters = adapters.ToList();
        _logger = logger;
    }

    public IReadOnlyList<IRoasterRecipeAdapter> All => _adapters;

    public IRoasterRecipeAdapter? FindAdapter(Bean bean)
    {
        foreach (var adapter in _adapters)
        {
            if (adapter.CanHandle(bean))
            {
                _logger.LogDebug(
                    "Adapter {AdapterId} matched BeanId={BeanId} Roaster={Roaster}",
                    adapter.Id, bean.Id, bean.Roaster);
                return adapter;
            }
        }

        _logger.LogDebug(
            "No adapter matched BeanId={BeanId} Roaster={Roaster}",
            bean.Id, bean.Roaster);
        return null;
    }
}
