using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Maui;
using MauiReactor;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Services;
using BaristaNotes.Infrastructure;

namespace BaristaNotes;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiReactorApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

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
}
