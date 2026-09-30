using BaristaNotes.Core.Data;
using BaristaNotes.Core.Hosting;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Recipes;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using BaristaNotes.Tests.Unit;
using Moq;

namespace BaristaNotes.Tests.Integration;

public sealed class SharedWorkflowTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), $"BaristaNotes-workflow-tests-{Guid.NewGuid():N}");
    private ServiceProvider _provider = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _provider = CreateProvider(Path.Combine(_directory, "workflow.db"));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task SharedComposition_ResolvesAndInitializesOnce()
    {
        var initializer = _provider.GetRequiredService<DatabaseInitializationService>();
        var first = initializer.InitializeAsync();
        var second = initializer.InitializeAsync();

        Assert.Same(first, second);
        await Task.WhenAll(first, second);
        Assert.Same(first, initializer.InitializeAsync());

        using var scope = _provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DrinkWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GrindPickerWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EquipmentWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProfileWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BeanWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BagWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BeanCreationWorkflow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AddCoffeeWorkflow>());
        Assert.False(scope.ServiceProvider.GetRequiredService<IAIRecipeGenerator>().IsAvailable);
        var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
        Assert.Empty(await context.Beans.ToListAsync());
    }

    [Fact]
    public async Task Initialization_RetriesAfterStorageBecomesAvailable()
    {
        var missingDirectory = Path.Combine(_directory, "initially-missing");
        await using var provider = CreateProvider(Path.Combine(missingDirectory, "retry.db"));
        var initializer = provider.GetRequiredService<DatabaseInitializationService>();

        var failed = initializer.InitializeAsync();
        await Assert.ThrowsAsync<SqliteException>(() => failed);

        Directory.CreateDirectory(missingDirectory);
        var retried = initializer.InitializeAsync();
        Assert.NotSame(failed, retried);
        await retried;

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
        Assert.Empty(await context.ShotRecords.ToListAsync());
    }

    [Fact]
    public async Task ManualCoffeeAndDrink_PersistAcrossNewScopes()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        var notifications = new List<DataChangeType>();
        _provider.GetRequiredService<IDataChangeNotifier>().DataChanged +=
            (_, args) => notifications.Add(args.ChangeType);

        int shotId;
        int bagId;
        using (var scope = _provider.CreateScope())
        {
            var creation = await scope.ServiceProvider.GetRequiredService<BeanCreationWorkflow>()
                .CreateWithInitialBagAsync(new CreateBeanDto { Name = "Workflow coffee" });

            Assert.True(creation.Bean.Success);
            Assert.NotNull(creation.InitialBag);
            Assert.True(creation.InitialBag.Success);
            Assert.NotNull(creation.InitialBag.Data);
            bagId = creation.InitialBag.Data.Id;
            Assert.Equal(
                new[] { DataChangeType.BeanCreated, DataChangeType.BagCreated },
                notifications);

            var workflow = scope.ServiceProvider.GetRequiredService<DrinkWorkflow>();
            var draft = new DrinkDraft();
            workflow.ApplyLoadedData(draft, await workflow.LoadAsync());
            Assert.Single(draft.AvailableBags);
            Assert.Null(draft.SelectedBagId);

            draft.SelectedBagId = bagId;
            draft.DoseIn = 18.3m;
            draft.ExpectedOutput = 36.5m;
            draft.Rating = 0;
            var saved = await workflow.SaveAsync(draft);
            shotId = saved.Id;
        }

        using var verification = _provider.CreateScope();
        var reopened = verification.ServiceProvider.GetRequiredService<DrinkWorkflow>();
        var edited = new DrinkDraft();
        reopened.ApplyLoadedData(edited, await reopened.LoadAsync(shotId));

        Assert.Equal(bagId, edited.SelectedBagId);
        Assert.Equal(18.3m, edited.DoseIn);
        Assert.Equal(36.5m, edited.ExpectedOutput);
        Assert.Equal(0, edited.Rating);
        var history = await verification.ServiceProvider.GetRequiredService<IShotService>()
            .GetShotHistoryAsync(0, 50);
        Assert.Single(history.Items);
    }

    [Fact]
    public async Task GrindHistoryAndSelection_PersistAcrossDatabaseScopes()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int bagId;
        using (var scope = _provider.CreateScope())
        {
            var creation = await scope.ServiceProvider.GetRequiredService<BeanCreationWorkflow>()
                .CreateWithInitialBagAsync(new CreateBeanDto { Name = "Grind history coffee" });
            Assert.True(creation.InitialBag?.Success);
            bagId = creation.InitialBag!.Data!.Id;
            var shots = scope.ServiceProvider.GetRequiredService<IShotService>();
            await shots.CreateShotAsync(new CreateShotDto
            {
                BagId = bagId, BrewMethod = BrewMethod.Espresso, DrinkType = "Espresso",
                GrindMicrons = 315, DoseIn = 18, ExpectedOutput = 36, ExpectedTime = 28,
                Timestamp = DateTime.Now.AddMinutes(-2)
            });
            await shots.CreateShotAsync(new CreateShotDto
            {
                BagId = bagId, BrewMethod = BrewMethod.V60, DrinkType = "Pour Over",
                GrindMicrons = 550, DoseIn = 18, ExpectedOutput = 300, ExpectedTime = 180,
                Timestamp = DateTime.Now.AddMinutes(-1)
            });
        }

        int savedId;
        using (var scope = _provider.CreateScope())
        {
            var draft = new DrinkDraft { SelectedBagId = bagId };
            var loaded = await scope.ServiceProvider.GetRequiredService<GrindPickerWorkflow>().LoadAsync(draft);
            Assert.Equal(315, loaded.Microns);
            Assert.Null(draft.GrindMicrons);

            var range = scope.ServiceProvider.GetRequiredService<IDrinkValueRangeService>()
                .Resolve(DrinkValueMetric.GrindMicrons, draft.BrewMethod);
            var picker = new GrindPickerState(range, draft.GrindMicrons, loaded.Microns);
            picker.Select(320);
            draft.GrindMicrons = picker.DoneValue;
            savedId = (await scope.ServiceProvider.GetRequiredService<DrinkWorkflow>().SaveAsync(draft)).Id;
        }

        using var verification = _provider.CreateScope();
        var saved = await verification.ServiceProvider.GetRequiredService<IShotService>().GetShotByIdAsync(savedId);
        Assert.Equal(320, saved?.GrindMicrons);
        var reopened = await verification.ServiceProvider.GetRequiredService<GrindPickerWorkflow>()
            .LoadAsync(new DrinkDraft { SelectedBagId = bagId });
        Assert.Equal(320, reopened.Microns);
    }

    [Fact]
    public async Task EquipmentSaveClearAndArchive_PreserveRowAcrossScopes()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int equipmentId;
        using (var scope = _provider.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<EquipmentWorkflow>()
                .SaveAsync(new EquipmentDraft
                {
                    Name = "Workflow grinder", SelectedType = EquipmentType.Grinder, Notes = "Initial notes"
                });
            equipmentId = saved.Id;
        }

        using (var scope = _provider.CreateScope())
        {
            var existing = await scope.ServiceProvider.GetRequiredService<IEquipmentService>()
                .GetEquipmentByIdAsync(equipmentId);
            Assert.NotNull(existing);
            var draft = new EquipmentDraft();
            draft.ApplyLoadedData(existing);
            draft.Notes = "";
            await scope.ServiceProvider.GetRequiredService<EquipmentWorkflow>().SaveAsync(draft);
        }

        using (var scope = _provider.CreateScope())
        {
            var existing = await scope.ServiceProvider.GetRequiredService<IEquipmentService>()
                .GetEquipmentByIdAsync(equipmentId);
            Assert.Null(existing?.Notes);
            await scope.ServiceProvider.GetRequiredService<EquipmentWorkflow>().ArchiveAsync(equipmentId);
        }

        using var verification = _provider.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<BaristaNotesContext>();
        var row = await context.Equipment.SingleAsync(item => item.Id == equipmentId);
        Assert.False(row.IsActive);
        Assert.False(row.IsDeleted);
        Assert.Null(row.Notes);
        Assert.Empty(await verification.ServiceProvider.GetRequiredService<IEquipmentService>()
            .GetAllActiveEquipmentAsync());
    }

    [Fact]
    public async Task ProfileDetails_PersistAndContextClearRemainsExplicit()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int profileId;
        using (var scope = _provider.CreateScope())
        {
            var draft = new ProfileDraft { Name = "Workflow guest", Context = new string('x', 2000) };
            var saved = await scope.ServiceProvider.GetRequiredService<ProfileWorkflow>().SaveDetailsAsync(draft);
            profileId = saved.Id;
            Assert.Equal(profileId, draft.ProfileId);
        }

        using (var scope = _provider.CreateScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IUserProfileService>()
                .GetProfileByIdAsync(profileId);
            Assert.Equal(2000, loaded?.Context?.Length);
            var draft = new ProfileDraft();
            draft.ApplyLoadedData(loaded!);
            draft.Context = new string('x', 2001);
            await Assert.ThrowsAsync<BaristaNotes.Core.Services.Exceptions.ValidationException>(
                () => scope.ServiceProvider.GetRequiredService<ProfileWorkflow>().SaveDetailsAsync(draft));
        }

        using (var scope = _provider.CreateScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IUserProfileService>()
                .GetProfileByIdAsync(profileId);
            Assert.Equal(2000, loaded?.Context?.Length);
            var draft = new ProfileDraft();
            draft.ApplyLoadedData(loaded!);
            draft.Context = "";
            await scope.ServiceProvider.GetRequiredService<ProfileWorkflow>().SaveDetailsAsync(draft);
        }

        using var verification = _provider.CreateScope();
        var profiles = await verification.ServiceProvider.GetRequiredService<IUserProfileService>().GetAllProfilesAsync();
        var profile = Assert.Single(profiles);
        Assert.Equal(profileId, profile.Id);
        Assert.Null(profile.Context);
        Assert.Equal("Workflow guest", profile.Name);
    }

    [Fact]
    public async Task BeanAndBagEdits_PersistAndCompletionIsIndependentOfFormSave()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int beanId;
        int bagId;
        using (var scope = _provider.CreateScope())
        {
            var bean = await scope.ServiceProvider.GetRequiredService<IBeanService>().CreateBeanAsync(new CreateBeanDto
            {
                Name = "Bag workflow coffee", Roaster = "Roaster", Origin = "Origin", Notes = "Bean notes"
            });
            Assert.True(bean.Success);
            beanId = bean.Data!.Id;
            var bag = await scope.ServiceProvider.GetRequiredService<BagWorkflow>().SaveAsync(new BagDraft
            {
                BeanId = beanId, RoastDate = DateTime.Today.AddDays(-4), Notes = "Bag notes"
            });
            Assert.True(bag.Success);
            bagId = bag.Data!.Id;
        }

        using (var scope = _provider.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<IBeanService>().GetBeanByIdAsync(beanId);
            var bean = new BeanDraft();
            bean.ApplyLoadedData(saved!);
            bean.Roaster = bean.Origin = bean.Notes = "";
            await scope.ServiceProvider.GetRequiredService<BeanWorkflow>().UpdateAsync(bean);

            var bags = scope.ServiceProvider.GetRequiredService<IBagService>();
            var bag = new BagDraft();
            bag.ApplyLoadedData((await bags.GetBagByIdAsync(bagId))!, bean.Name);
            bag.RoastDate = DateTime.Today.AddDays(-3);
            await scope.ServiceProvider.GetRequiredService<BagWorkflow>().SaveAsync(bag);
            await bags.MarkBagCompleteAsync(bagId);
        }

        using (var scope = _provider.CreateScope())
        {
            var bean = await scope.ServiceProvider.GetRequiredService<IBeanService>().GetBeanByIdAsync(beanId);
            Assert.Null(bean!.Roaster);
            Assert.Null(bean.Origin);
            Assert.Null(bean.Notes);
            var bags = scope.ServiceProvider.GetRequiredService<IBagService>();
            var bag = await bags.GetBagByIdAsync(bagId);
            Assert.Equal(DateTime.Today.AddDays(-3), bag!.RoastDate);
            Assert.Equal("Bag notes", bag.Notes);
            Assert.True(bag.IsComplete);
            Assert.Empty(await bags.GetActiveBagsForShotLoggingAsync());
            await bags.ReactivateBagAsync(bagId);
        }

        using var verification = _provider.CreateScope();
        var active = await verification.ServiceProvider.GetRequiredService<IBagService>().GetActiveBagsForShotLoggingAsync();
        Assert.Equal(bagId, Assert.Single(active).Id);
    }

    [Fact]
    public async Task SharedAdvice_ReadsRealSavedContextWithNoExternalProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPreferencesStore, TestPreferencesStore>();
        services.AddSingleton(Mock.Of<IImageProcessingService>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var client = new SharedAIServiceTests.TestChatClient(
            """{"adjustments":[],"reasoning":"Keep the recipe steady."}""");
        services.AddSingleton<IChatClient>(client);
        services.AddBaristaNotesCore(Path.Combine(_directory, "advice.db")).AddBaristaNotesAI();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int shotId;
        using (var scope = provider.CreateScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<BeanCreationWorkflow>()
                .CreateWithInitialBagAsync(new CreateBeanDto { Name = "AI context test bean" });
            var saved = await scope.ServiceProvider.GetRequiredService<DrinkWorkflow>()
                .SaveAsync(new DrinkDraft { SelectedBagId = created.InitialBag!.Data!.Id, DoseIn = 18.5m });
            shotId = saved.Id;
        }

        var advice = await provider.GetRequiredService<IAIAdviceService>().GetAdviceForShotAsync(shotId);

        Assert.True(advice.Success);
        Assert.Equal("Keep the recipe steady.", advice.Reasoning);
        Assert.Contains("AI context test bean", advice.PromptSent);
        Assert.Contains("18.5g", advice.PromptSent);
        Assert.Equal(1, client.Calls);
        using var verification = provider.CreateScope();
        var savedShot = await verification.ServiceProvider.GetRequiredService<IShotService>().GetShotByIdAsync(shotId);
        Assert.Equal(18.5m, savedShot?.DoseIn);
    }

    [Fact]
    public async Task SharedRecommendations_ReadRatedHistoryAndEquipmentFromFreshScopes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPreferencesStore, TestPreferencesStore>();
        services.AddSingleton(Mock.Of<IImageProcessingService>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var client = new SharedAIServiceTests.TestChatClient(
            """{"dose":30,"grind":"coarse","output":500,"duration":240}""");
        services.AddSingleton<IChatClient>(client);
        services.AddBaristaNotesCore(Path.Combine(_directory, "recommendations.db")).AddBaristaNotesAI();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int beanId;
        int recentShotId = 0;
        using (var scope = provider.CreateScope())
        {
            var creation = await scope.ServiceProvider.GetRequiredService<BeanCreationWorkflow>()
                .CreateWithInitialBagAsync(new CreateBeanDto { Name = "French Press history" });
            beanId = creation.Bean.Data!.Id;
            var bagId = creation.InitialBag!.Data!.Id;
            var equipment = scope.ServiceProvider.GetRequiredService<IEquipmentService>();
            var machine = await equipment.CreateEquipmentAsync(new CreateEquipmentDto
            {
                Name = "Recent machine", Type = EquipmentType.Machine
            });
            var grinder = await equipment.CreateEquipmentAsync(new CreateEquipmentDto
            {
                Name = "Recent grinder", Type = EquipmentType.Grinder
            });
            var shots = scope.ServiceProvider.GetRequiredService<IShotService>();
            for (var index = 0; index < 12; index++)
            {
                var best = index < 10;
                var saved = await shots.CreateShotAsync(new CreateShotDto
                {
                    BagId = bagId,
                    BrewMethod = best ? BrewMethod.FrenchPress : BrewMethod.Espresso,
                    DrinkType = best ? "French Press" : "Espresso",
                    DoseIn = best ? 30 : 18,
                    ExpectedOutput = best ? 500 : 36,
                    ActualOutput = best ? 500 : 36,
                    ExpectedTime = best ? 240 : 28,
                    ActualTime = best ? 240 : 28,
                    Rating = best ? 4 : 0,
                    MachineId = best ? null : machine.Id,
                    GrinderId = best ? null : grinder.Id,
                    Timestamp = DateTime.Now.AddDays(index - 15)
                });
                recentShotId = saved.Id;
            }
        }

        var singleton = provider.GetRequiredService<IAIAdviceService>();
        var result = await singleton.GetRecommendationsForBeanAsync(beanId);
        Assert.True(result.Success);
        Assert.Equal(RecommendationType.ReturningBean, result.RecommendationType);
        Assert.Contains("French Press", client.LastMessages![0].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"30(?:\.0+)?g in", client.LastMessages[1].Text);
        Assert.Contains("Recent machine", client.LastMessages[1].Text);
        Assert.Contains("Recent grinder", client.LastMessages[1].Text);

        using (var verification = provider.CreateScope())
        {
            var context = await verification.ServiceProvider.GetRequiredService<IShotService>()
                .GetBeanRecommendationContextAsync(beanId);
            Assert.NotNull(context);
            Assert.True(context.HasHistory);
            Assert.Equal(10, context.HistoricalShots!.Count);
            Assert.All(context.HistoricalShots, shot =>
            {
                Assert.Equal(4, shot.Rating);
                Assert.Equal(BrewMethod.FrenchPress, shot.BrewMethod);
                Assert.Equal(30m, shot.DoseIn);
            });
            Assert.Equal(context.HistoricalShots.OrderByDescending(shot => shot.Timestamp), context.HistoricalShots);
            Assert.Equal("Recent grinder", context.Equipment?.GrinderName);
        }

        await singleton.GetAdviceForShotAsync(recentShotId);
        var afterAdvice = await singleton.GetRecommendationsForBeanAsync(beanId);
        Assert.True(afterAdvice.Success);
        Assert.Equal(RecommendationType.ReturningBean, afterAdvice.RecommendationType);
        Assert.Contains("French Press", client.LastMessages![0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PhotoCoffeeCreation_StoresNotesOnBagAndBrowseAddsOnlyBag()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        var notifications = new List<DataChangedEventArgs>();
        _provider.GetRequiredService<IDataChangeNotifier>().DataChanged += (_, args) => notifications.Add(args);
        int beanId;
        int firstBagId;
        using (var scope = _provider.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AddCoffeeWorkflow>().CreateAsync(new AddCoffeeDraft
            {
                Name = " Photo coffee ", Roaster = " Roaster ", Origin = " Ethiopia ",
                Notes = " Printed notes ", RoastDate = DateTime.Today.AddDays(-3)
            });
            Assert.True(result.Bean.Success);
            Assert.True(result.InitialBag?.Success);
            beanId = result.Bean.Data!.Id;
            firstBagId = result.InitialBag!.Data!.Id;
        }
        using (var scope = _provider.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AddCoffeeWorkflow>().AddBagForExistingBeanAsync(beanId);
            Assert.True(result.Success);
            Assert.NotEqual(firstBagId, result.Data!.Id);
        }
        using var verification = _provider.CreateScope();
        var bean = Assert.Single(await verification.ServiceProvider.GetRequiredService<IBeanService>().GetAllActiveBeansAsync());
        Assert.Equal("Photo coffee", bean.Name);
        Assert.Equal("Roaster", bean.Roaster);
        Assert.Null(bean.Notes);
        var bags = await verification.ServiceProvider.GetRequiredService<IBagService>().GetBagsForBeanAsync(beanId);
        Assert.Equal(2, bags.Count);
        Assert.Equal("Printed notes", bags.Single(bag => bag.Id == firstBagId).Notes);
        Assert.Equal(DateTime.Today, bags.Single(bag => bag.Id != firstBagId).RoastDate);
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task BagDetailCount_DoesNotDependOnTrackedShotsOrRatings()
    {
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        int bagId;
        int emptyBagId;
        using (var scope = _provider.CreateScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<BeanCreationWorkflow>()
                .CreateWithInitialBagAsync(new CreateBeanDto { Name = "Bag count fixture" });
            bagId = created.InitialBag!.Data!.Id;
            var emptyBag = await scope.ServiceProvider.GetRequiredService<IBagService>()
                .CreateNewBagForBeanAsync(created.Bean.Data!.Id, DateTime.Today);
            emptyBagId = emptyBag.Data!.Id;
            var shots = scope.ServiceProvider.GetRequiredService<IShotService>();
            for (var index = 0; index < 3; index++)
            {
                var shot = await shots.CreateShotAsync(new CreateShotDto
                {
                    BagId = bagId, DoseIn = 18, ExpectedOutput = 36, ExpectedTime = 28,
                    DrinkType = "Espresso", Rating = index == 0 ? null : 3
                });
                if (index == 2)
                    await shots.DeleteShotAsync(shot.Id);
            }
        }

        using var verification = _provider.CreateScope();
        var bags = verification.ServiceProvider.GetRequiredService<IBagService>();
        var bag = await bags.GetBagByIdAsync(bagId);
        Assert.NotNull(bag);
        Assert.Empty(bag.ShotRecords);
        Assert.Equal(3, await bags.GetShotCountAsync(bagId));
        Assert.Equal(0, await bags.GetShotCountAsync(emptyBagId));
        Assert.Equal(3, await bags.GetShotCountAsync(bagId));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bags.GetShotCountAsync(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bags.GetShotCountAsync(-1));
        var rating = await verification.ServiceProvider.GetRequiredService<IRatingService>().GetBagRatingAsync(bagId);
        Assert.Equal(2, rating.TotalShots);
        Assert.Equal(1, rating.RatedShots);
        Assert.Empty(bag.ShotRecords);
        Assert.Empty(verification.ServiceProvider.GetRequiredService<BaristaNotesContext>()
            .ChangeTracker.Entries<ShotRecord>());
    }

    [Fact]
    public void MethodChange_UsesEffectiveCustomDefaultOnlyInNewMode()
    {
        using var scope = _provider.CreateScope();
        var ranges = scope.ServiceProvider.GetRequiredService<IDrinkValueRangeService>();
        ranges.SaveOverride(DrinkValueMetric.DoseIn, BrewMethod.Espresso, 20m, 25m);
        ranges.SetMode(DrinkValueMetric.DoseIn, ValueRangeMode.Custom);
        var workflow = scope.ServiceProvider.GetRequiredService<DrinkWorkflow>();
        var draft = new DrinkDraft
        {
            BrewMethod = BrewMethod.PourOver,
            DrinkType = "Pour Over",
            DoseIn = 18m,
            ActualTime = 180,
            ActualOutput = 250
        };

        workflow.ChangeBrewMethod(draft, BrewMethod.Espresso, isEditing: false);

        Assert.Equal(20m, draft.DoseIn);
        Assert.Equal("Espresso", draft.DrinkType);
        Assert.Null(draft.ActualTime);
        Assert.Null(draft.ActualOutput);

        ranges.SetMode(DrinkValueMetric.DoseIn, ValueRangeMode.Auto);
        workflow.ChangeBrewMethod(draft, BrewMethod.PourOver, isEditing: false);
        workflow.ChangeBrewMethod(draft, BrewMethod.Espresso, isEditing: false);
        Assert.Equal(18m, draft.DoseIn);

        draft.ActualTime = 29;
        draft.ActualOutput = 37;
        workflow.ChangeBrewMethod(draft, BrewMethod.PourOver, isEditing: true);
        Assert.Equal(18m, draft.DoseIn);
        Assert.Equal(29, draft.ActualTime);
        Assert.Equal(37, draft.ActualOutput);
    }

    [Fact]
    public void ApplyingNewDrinkDefaults_PreservesFieldsTheSourceDoesNotReload()
    {
        using var scope = _provider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<DrinkWorkflow>();
        var draft = new DrinkDraft
        {
            ActualTime = 31,
            ActualOutput = 39,
            TastingNotes = "Unsaved notes",
            BeanName = "Existing display fallback"
        };
        var loaded = new DrinkLoadResult(
            null,
            new ShotRecordDto
            {
                BrewMethod = BrewMethod.Espresso,
                DrinkType = "Latte",
                DoseIn = 19,
                ExpectedTime = 28,
                ExpectedOutput = 38,
                Rating = 4
            },
            [], [], []);

        workflow.ApplyLoadedData(draft, loaded);

        Assert.Equal(19, draft.DoseIn);
        Assert.Equal("Latte", draft.DrinkType);
        Assert.Equal(31, draft.ActualTime);
        Assert.Equal(39, draft.ActualOutput);
        Assert.Equal("Unsaved notes", draft.TastingNotes);
        Assert.Equal("Existing display fallback", draft.BeanName);
    }

    private static ServiceProvider CreateProvider(string databasePath)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPreferencesStore, TestPreferencesStore>();
        services.AddSingleton(Mock.Of<IImageProcessingService>());
        services.AddBaristaNotesCore(databasePath);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private sealed class TestPreferencesStore : IPreferencesStore
    {
        private readonly Dictionary<string, object> _values = [];

        public string? Get(string key, string? defaultValue) =>
            _values.TryGetValue(key, out var value) ? (string)value : defaultValue;
        public int Get(string key, int defaultValue) =>
            _values.TryGetValue(key, out var value) ? (int)value : defaultValue;
        public double Get(string key, double defaultValue) =>
            _values.TryGetValue(key, out var value) ? (double)value : defaultValue;
        public void Set(string key, string value) => _values[key] = value;
        public void Set(string key, int value) => _values[key] = value;
        public void Set(string key, double value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
    }
}
