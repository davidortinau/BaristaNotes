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
using The49.Maui.BottomSheet;
using Fonts;
using BaristaNotes.Styles;

namespace BaristaNotes;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiReactorApp<App>(app =>
			{
				app.UseTheme<ApplicationTheme>();
				app.SetWindowsSpecificAssetsDirectory("Assets");
				app.Resources.MergedDictionaries.Add(new UXDivers.Popups.Maui.Controls.DarkTheme());
				app.Resources.MergedDictionaries.Add(new UXDivers.Popups.Maui.Controls.PopupStyles());
			})
			.UseUXDiversPopups()
			.UseBottomSheet()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Manrope-Regular.ttf", "Manrope");
				fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemibold");
				fonts.AddFont("MaterialSymbols.ttf", MaterialSymbolsFont.FontFamily);
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
		builder.Services.AddSingleton<IThemeService, ThemeService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();

		// Initialize theme service to load saved theme preference
		var themeService = app.Services.GetRequiredService<IThemeService>();
		Task.Run(async () =>
		{
			var savedMode = await themeService.GetThemeModeAsync();
			await themeService.SetThemeModeAsync(savedMode);
		}).Wait();

		// Apply database migrations
		using (var scope = app.Services.CreateScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
			context.Database.Migrate();
		}

		return app;
	}

	private static void RegisterRoutes()
	{
		MauiReactor.Routing.RegisterRoute<Pages.SettingsPage>("settings");
		MauiReactor.Routing.RegisterRoute<Pages.EquipmentManagementPage>("equipment");
		MauiReactor.Routing.RegisterRoute<Pages.BeanManagementPage>("beans");
		MauiReactor.Routing.RegisterRoute<Pages.UserProfileManagementPage>("profiles");
		MauiReactor.Routing.RegisterRoute<Pages.ShotLoggingPage>("shot-logging");
	}
}
