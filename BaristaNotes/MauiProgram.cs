using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Maui;
using MauiReactor;
using UXDivers.Popups.Maui;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Services;
using BaristaNotes.Infrastructure;
using BaristaNotes.Services;

namespace BaristaNotes;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiReactorApp<App>(app =>
			{
				app.SetWindowsSpecificAssetsDirectory("Assets");
			})
			.UseUXDiversPopups()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Register MauiReactor routes
		RegisterRoutes();

		// Database configuration
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "barista_notes.db");
		builder.Services.AddDbContext<BaristaNotesContext>(options =>
			options.UseSqlite($"Data Source={dbPath}"));

		// Register Repositories (scoped)
		builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
		builder.Services.AddScoped<IBeanRepository, BeanRepository>();
		builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
		builder.Services.AddScoped<IShotRecordRepository, ShotRecordRepository>();

		// Register MAUI preferences store
		builder.Services.AddSingleton<IPreferencesStore, MauiPreferencesStore>();

		// Register Services (scoped/singleton)
		builder.Services.AddScoped<IShotService, ShotService>();
		builder.Services.AddScoped<IEquipmentService, EquipmentService>();
		builder.Services.AddScoped<IBeanService, BeanService>();
		builder.Services.AddScoped<IUserProfileService, UserProfileService>();
		builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
		builder.Services.AddSingleton<IFeedbackService, FeedbackService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Run migrations on startup
		using (var scope = app.Services.CreateScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
			context.Database.EnsureCreated();
		}

		return app;
	}

	private static void RegisterRoutes()
	{
		MauiReactor.Routing.RegisterRoute<Pages.SettingsPage>("settings");
		MauiReactor.Routing.RegisterRoute<Pages.EquipmentManagementPage>("equipment");
		MauiReactor.Routing.RegisterRoute<Pages.BeanManagementPage>("beans");
		MauiReactor.Routing.RegisterRoute<Pages.UserProfileManagementPage>("profiles");
	}
}
