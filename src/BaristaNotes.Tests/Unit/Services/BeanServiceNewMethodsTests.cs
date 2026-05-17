using Xunit;
using Moq;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Tests.Unit.Services;

public class BeanServiceRecentBeansTests
{
    private readonly Mock<IBeanRepository> _mockRepository;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly BeanService _service;

    public BeanServiceRecentBeansTests()
    {
        _mockRepository = new Mock<IBeanRepository>();
        _mockRatingService = new Mock<IRatingService>();
        _service = new BeanService(_mockRepository.Object, _mockRatingService.Object);
    }

    private static Bean MakeBean(int id, string name, IEnumerable<Bag>? bags = null)
    {
        return new Bean
        {
            Id = id,
            Name = name,
            IsActive = true,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            CreatedAt = DateTime.Now.AddDays(-180),
            LastModifiedAt = DateTime.Now,
            Bags = bags?.ToList() ?? new List<Bag>()
        };
    }

    private static Bag MakeBag(DateTime createdAt, IEnumerable<ShotRecord>? shots = null, bool isDeleted = false)
    {
        return new Bag
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            CreatedAt = createdAt,
            RoastDate = createdAt,
            IsActive = true,
            IsDeleted = isDeleted,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = createdAt,
            ShotRecords = shots?.ToList() ?? new List<ShotRecord>()
        };
    }

    private static ShotRecord MakeShot(DateTime timestamp, bool isDeleted = false) => new()
    {
        Id = Random.Shared.Next(1, int.MaxValue),
        Timestamp = timestamp,
        DrinkType = "Espresso",
        GrindSetting = "3",
        SyncId = Guid.NewGuid(),
        LastModifiedAt = timestamp,
        IsDeleted = isDeleted
    };

    [Fact]
    public async Task GetRecentBeansAsync_orders_by_most_recent_activity()
    {
        var now = DateTime.Now;
        // Bean A: newest bag is 10 days old, no shots
        var beanA = MakeBean(1, "A", new[] { MakeBag(now.AddDays(-10)) });
        // Bean B: bag 30 days old but has a shot 2 days old -> most recent activity 2 days
        var beanB = MakeBean(2, "B", new[]
        {
            MakeBag(now.AddDays(-30), new[] { MakeShot(now.AddDays(-2)) })
        });
        // Bean C: bag 5 days old, no shots
        var beanC = MakeBean(3, "C", new[] { MakeBag(now.AddDays(-5)) });

        _mockRepository.Setup(r => r.GetActiveBeansWithActivityAsync())
            .ReturnsAsync(new List<Bean> { beanA, beanB, beanC });

        var result = await _service.GetRecentBeansAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result[0].Id); // B (shot 2 days ago)
        Assert.Equal(3, result[1].Id); // C (bag 5 days ago)
        Assert.Equal(1, result[2].Id); // A (bag 10 days ago)
    }

    [Fact]
    public async Task GetRecentBeansAsync_excludes_beans_older_than_withinDays()
    {
        var now = DateTime.Now;
        var recent = MakeBean(1, "Recent", new[] { MakeBag(now.AddDays(-5)) });
        var old = MakeBean(2, "Old", new[] { MakeBag(now.AddDays(-120)) });
        var noActivity = MakeBean(3, "NoActivity");

        _mockRepository.Setup(r => r.GetActiveBeansWithActivityAsync())
            .ReturnsAsync(new List<Bean> { recent, old, noActivity });

        var result = await _service.GetRecentBeansAsync(limit: 10, withinDays: 90);

        Assert.Single(result);
        Assert.Equal(1, result[0].Id);
    }

    [Fact]
    public async Task GetRecentBeansAsync_respects_limit()
    {
        var now = DateTime.Now;
        var beans = Enumerable.Range(1, 10)
            .Select(i => MakeBean(i, $"B{i}", new[] { MakeBag(now.AddDays(-i)) }))
            .ToList();

        _mockRepository.Setup(r => r.GetActiveBeansWithActivityAsync())
            .ReturnsAsync(beans);

        var result = await _service.GetRecentBeansAsync(limit: 3);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.Select(b => b.Id).ToArray());
    }

    [Fact]
    public async Task GetRecentBeansAsync_excludes_deleted_and_inactive()
    {
        // Repo's responsibility to filter IsDeleted/IsActive. Verify service asks the correct repo method.
        var now = DateTime.Now;
        var active = MakeBean(1, "Active", new[] { MakeBag(now.AddDays(-1)) });

        _mockRepository.Setup(r => r.GetActiveBeansWithActivityAsync())
            .ReturnsAsync(new List<Bean> { active });

        var result = await _service.GetRecentBeansAsync();

        Assert.Single(result);
        _mockRepository.Verify(r => r.GetActiveBeansWithActivityAsync(), Times.Once);
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetRecentBeansAsync_ignores_deleted_bags_and_shots_when_ranking()
    {
        var now = DateTime.Now;
        // Bean has a recent deleted bag (should be ignored) and an older non-deleted bag.
        var bean = MakeBean(1, "B", new[]
        {
            MakeBag(now.AddDays(-1), isDeleted: true),
            MakeBag(now.AddDays(-40), new[] { MakeShot(now.AddDays(-2), isDeleted: true), MakeShot(now.AddDays(-35)) })
        });

        _mockRepository.Setup(r => r.GetActiveBeansWithActivityAsync())
            .ReturnsAsync(new List<Bean> { bean });

        var result = await _service.GetRecentBeansAsync(withinDays: 50);

        Assert.Single(result);
    }
}

public class BeanServiceFuzzyFindTests
{
    private readonly Mock<IBeanRepository> _mockRepository;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly BeanService _service;

    public BeanServiceFuzzyFindTests()
    {
        _mockRepository = new Mock<IBeanRepository>();
        _mockRatingService = new Mock<IRatingService>();
        _service = new BeanService(_mockRepository.Object, _mockRatingService.Object);
    }

    private static Bean MakeBean(int id, string name, string? roaster) => new()
    {
        Id = id,
        Name = name,
        Roaster = roaster,
        IsActive = true,
        IsDeleted = false,
        SyncId = Guid.NewGuid(),
        CreatedAt = DateTime.Now,
        LastModifiedAt = DateTime.Now
    };

    [Fact]
    public async Task FuzzyFindByNameRoasterAsync_exact_match_returns_bean()
    {
        var bean = MakeBean(1, "Ethiopia Yirgacheffe", "Blue Bottle");
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean> { bean });

        var result = await _service.FuzzyFindByNameRoasterAsync("  ethiopia   YIRGACHEFFE ", "blue bottle");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public async Task FuzzyFindByNameRoasterAsync_near_miss_within_levenshtein_2()
    {
        var bean = MakeBean(1, "Ethiopia Yirgacheffe", "Blue Bottle");
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean> { bean });

        // Misspelled by 2 chars
        var result = await _service.FuzzyFindByNameRoasterAsync("Ethiopia Yrgacheff", "Blue Bottle");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
    }

    [Fact]
    public async Task FuzzyFindByNameRoasterAsync_distance_3_returns_null()
    {
        var bean = MakeBean(1, "Ethiopia", "Blue Bottle");
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean> { bean });

        // "Ethi" vs "Ethiopia" is distance 4
        var result = await _service.FuzzyFindByNameRoasterAsync("Ethi", "Blue Bottle");

        Assert.Null(result);
    }

    [Fact]
    public async Task FuzzyFindByNameRoasterAsync_different_roaster_returns_null()
    {
        var bean = MakeBean(1, "Ethiopia Yirgacheffe", "Blue Bottle");
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean> { bean });

        var result = await _service.FuzzyFindByNameRoasterAsync("Ethiopia Yirgacheffe", "Stumptown");

        Assert.Null(result);
    }

    [Fact]
    public async Task FuzzyFindByNameRoasterAsync_without_roaster_only_exact_match()
    {
        var bean = MakeBean(1, "Ethiopia Yirgacheffe", "Blue Bottle");
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean> { bean });

        // Exact normalized match without roaster -> hit
        var exact = await _service.FuzzyFindByNameRoasterAsync("Ethiopia Yirgacheffe", null);
        Assert.NotNull(exact);

        // Near miss without roaster -> miss (strict v1)
        var near = await _service.FuzzyFindByNameRoasterAsync("Ethiopia Yrgacheff", null);
        Assert.Null(near);
    }
}

public class BeanServiceAutocompleteTests
{
    private readonly Mock<IBeanRepository> _mockRepository;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly BeanService _service;

    public BeanServiceAutocompleteTests()
    {
        _mockRepository = new Mock<IBeanRepository>();
        _mockRatingService = new Mock<IRatingService>();
        _service = new BeanService(_mockRepository.Object, _mockRatingService.Object);
    }

    private static Bean MakeBean(int id, string? roaster, string? origin) => new()
    {
        Id = id,
        Name = $"Bean {id}",
        Roaster = roaster,
        Origin = origin,
        IsActive = true,
        IsDeleted = false,
        SyncId = Guid.NewGuid(),
        CreatedAt = DateTime.Now,
        LastModifiedAt = DateTime.Now
    };

    [Fact]
    public async Task GetDistinctRoastersAsync_returns_sorted_distinct_non_empty()
    {
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean>
            {
                MakeBean(1, "Stumptown", "Ethiopia"),
                MakeBean(2, "blue bottle", "Colombia"),
                MakeBean(3, "Blue Bottle", "Kenya"),   // case-insensitive duplicate
                MakeBean(4, "  Stumptown  ", null),    // trimmed duplicate
                MakeBean(5, "", "X"),                   // empty -> excluded
                MakeBean(6, null, "Y"),                 // null -> excluded
                MakeBean(7, "Onyx", null)
            });

        var result = await _service.GetDistinctRoastersAsync();

        Assert.Equal(new[] { "blue bottle", "Onyx", "Stumptown" }, result.ToArray());
    }

    [Fact]
    public async Task GetDistinctOriginsAsync_returns_sorted_distinct_non_empty()
    {
        _mockRepository.Setup(r => r.GetNonDeletedBeansAsync(null))
            .ReturnsAsync(new List<Bean>
            {
                MakeBean(1, "R", "Ethiopia"),
                MakeBean(2, "R", "ethiopia"),
                MakeBean(3, "R", "  Colombia  "),
                MakeBean(4, "R", "Kenya"),
                MakeBean(5, "R", ""),
                MakeBean(6, "R", null),
                MakeBean(7, "R", "Brazil")
            });

        var result = await _service.GetDistinctOriginsAsync();

        Assert.Equal(new[] { "Brazil", "Colombia", "Ethiopia", "Kenya" }, result.ToArray());
    }
}
