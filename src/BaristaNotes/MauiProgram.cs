using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Shiny;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Media;
using MauiReactor;
using UXDivers.Popups.Maui;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Services;
using BaristaNotes.Infrastructure;
using BaristaNotes.Services;
using Fonts;
using BaristaNotes.Styles;
using Microsoft.Maui.Handlers;
using Syncfusion.Maui.Core.Hosting;
using Microsoft.Maui.Essentials.AI;

#if IOS
using BaristaNotes.Platforms.iOS;
#endif

namespace BaristaNotes;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var startupTimer = System.Diagnostics.Stopwatch.StartNew();
		void LogTiming(string phase) => Console.WriteLine($"[STARTUP] {phase}: {startupTimer.ElapsedMilliseconds}ms");
		
		LogTiming("CreateMauiApp entered");
		
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiReactorApp<BaristaApp>(app =>
			{
				app.UseTheme<ApplicationTheme>();
				app.SetWindowsSpecificAssetsDirectory("Assets");
				app.Resources.MergedDictionaries.Add(new UXDivers.Popups.Maui.Controls.DarkTheme());
				app.Resources.MergedDictionaries.Add(new UXDivers.Popups.Maui.Controls.PopupStyles());

				// Add custom resources
				var customResources = new ResourceDictionary
				{
					// Font Families
					{ "IconsFontFamily", MaterialSymbolsFont.FontFamily },
					{ "AppFontFamily", "Manrope" },
					{ "AppSemiBoldFamily", "ManropeSemibold" },
					
					// UXDivers Popups Icon Overrides
					{ "UXDPopupsCloseIconButton", MaterialSymbolsFont.Close },
					{ "UXDPopupsCheckCircleIconButton", MaterialSymbolsFont.Check_circle },
					
					// Icon Colors
					// { "IconOrange", Color.FromArgb("#FF7134") },
					// { "IconMagenta", Color.FromArgb("#FF1AD9") },
					// { "IconCyan", Color.FromArgb("#05D9FF") },
					// { "IconGreen", Color.FromArgb("#2FFF74") },
					// { "IconPurple", Color.FromArgb("#BD3BFF") },
					// { "IconBlue", Color.FromArgb("#1C7BFF") },
					// { "IconLime", Color.FromArgb("#C8FF01") },
					// { "IconRed", Color.FromArgb("#FF0000") },
					// { "IconDarkBlue", Color.FromArgb("#6422FF") },
					{ "BackgroundColor", AppColors.Dark.Surface },
					{ "BackgroundSecondaryColor", AppColors.Dark.Surface },
					{ "BackgroundTertiaryColor", Colors.Red },
					{ "PrimaryColor", AppColors.Dark.Primary },
					{ "PrimaryVariantColor", AppColors.Dark.SurfaceElevated },
					{ "TextColor", AppColors.Dark.TextPrimary },
					{ "TextTertiaryColor", AppColors.Dark.TextSecondary },
					{ "PopupBackgroundColor", AppColors.Dark.SurfaceElevated },
					{ "PopupBorderColor", AppColors.Dark.Outline }
				};
				app.Resources.MergedDictionaries.Add(customResources);
			})
			.ConfigureMauiHandlers(handlers =>
			{
				ModifyEntrys();
			})
			.UseUXDiversPopups()
			.UseMauiCommunityToolkit()
			.ConfigureSyncfusionCore()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Manrope-Regular.ttf", "Manrope");
				fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemibold");
				fonts.AddFont("MaterialSymbols.ttf", MaterialSymbolsFont.FontFamily);
				fonts.AddFont("coffee-icons.ttf", "coffee-icons");
			});

		LogTiming("Builder configured");

		// Register MauiReactor routes
		RegisterRoutes();

		// Load configuration using Shiny's platform bundle support
		// This loads appsettings.json from platform-specific locations:
		// - Android: Assets folder
		// - iOS/Mac: Bundle Resources
		// - Windows: Embedded resources
		// In DEBUG mode, also loads appsettings.Development.json for local API keys
#if DEBUG
		builder.Configuration.AddJsonPlatformBundle("Development");
#else
		builder.Configuration.AddJsonPlatformBundle();
#endif

		// Register Syncfusion license
		var sfKey = builder.Configuration["Syncfusion:Key"];
		if (!string.IsNullOrEmpty(sfKey))
		{
			Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(sfKey);
		}

		// Database configuration
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "barista_notes.db");
		builder.Services.AddDbContext<BaristaNotesContext>(options =>
			options.UseSqlite($"Data Source={dbPath}"));

		LogTiming("DbContext registered");

		// Bootstrap logging: Console.WriteLine used here because ILogger<T> is not available
		// during DI container build phase (circular dependency). This is the only acceptable
		// use of Console.WriteLine in the application.
		Console.WriteLine($"Database path: {dbPath}");

		// Register Apple Intelligence chat client (iOS only)
		// Note: AppleIntelligenceChatClient requires iOS 26.0+, but VoiceCommandService
		// gracefully falls back to OpenAI when local AI isn't available or fails
#if IOS
#pragma warning disable CA1416 // Validate platform compatibility
		try
		{
			// Only register if we can successfully create the client
			// This will fail on iOS < 26.0 or if Apple Intelligence is not available
			var appleIntelligenceClient = new AppleIntelligenceChatClient();
			builder.Services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(appleIntelligenceClient);
			Console.WriteLine("Apple Intelligence chat client registered successfully");
		}
		catch (Exception ex)
		{
			// Apple Intelligence not available - VoiceCommandService will use OpenAI fallback
			Console.WriteLine($"Apple Intelligence not available, will use OpenAI: {ex.Message}");
		}
#pragma warning restore CA1416
#endif

		// Register Repositories (scoped)
		builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
		builder.Services.AddScoped<IBeanRepository, BeanRepository>();
		builder.Services.AddScoped<IBagRepository, BagRepository>();  // T040: Phase 4
		builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
		builder.Services.AddScoped<IShotRecordRepository, ShotRecordRepository>();

		// Register MAUI preferences store
		builder.Services.AddSingleton<IPreferencesStore, MauiPreferencesStore>();

		// Register Services (scoped/singleton)
		builder.Services.AddScoped<IShotService, ShotService>();
		builder.Services.AddScoped<IEquipmentService, EquipmentService>();
		builder.Services.AddScoped<IBeanService, BeanService>();
		builder.Services.AddScoped<IBagService, BagService>();  // T040: Phase 4
		builder.Services.AddScoped<IUserProfileService, UserProfileService>();
		builder.Services.AddScoped<IRatingService, RatingService>();
		builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
		builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
		builder.Services.AddSingleton<IThemeService, ThemeService>();
		builder.Services.AddSingleton<IAIAdviceService, AIAdviceService>();

		// Image services
		builder.Services.AddSingleton<Microsoft.Maui.Media.IMediaPicker>(Microsoft.Maui.Media.MediaPicker.Default);
		builder.Services.AddSingleton<IImagePickerService, ImagePickerService>();
		builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();

		// Vision service for image analysis
		builder.Services.AddSingleton<IVisionService, VisionService>();

		// Voice command services
		// Use online SpeechToText for better accuracy (uses Apple's cloud services like Notes app)
		builder.Services.AddSingleton<ISpeechToText>(SpeechToText.Default);
		builder.Services.AddSingleton<ISpeechRecognitionService, SpeechRecognitionService>();
		builder.Services.AddSingleton<IDataChangeNotifier, DataChangeNotifier>();
		builder.Services.AddSingleton<INavigationRegistry, NavigationRegistry>();
		builder.Services.AddScoped<IVoiceCommandService, VoiceCommandService>();

		// Voice overlay using WindowOverlay pattern (cross-platform)
		builder.UseVoiceOverlay();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		LogTiming("Services registered, calling Build()");
		var app = builder.Build();
		LogTiming("Build() completed");

		// Theme initialization moved to BaristaApp.OnMounted() to avoid blocking main thread
		// Database migration moved to async startup to avoid iOS watchdog timeout
		
		LogTiming("CreateMauiApp returning (deferred: theme init, db migration)");
		return app;
	}

	private static void ModifyEntrys()
	{
#if IOS || MACCATALYST
		EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
		{
			// Remove border
			handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;

			// Optional: transparent background
			handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;

			// Optional: add a tiny left padding so text isn't flush
			handler.PlatformView.LeftView = new UIKit.UIView(new CoreGraphics.CGRect(0, 0, 4, 0));
			handler.PlatformView.LeftViewMode = UIKit.UITextFieldViewMode.Always;
		});

		PickerHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
		{
			// Remove border + make background transparent
			handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
			handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;

			// Optional: add a tiny left padding so text isn't flush to the edge
			handler.PlatformView.LeftView = new UIKit.UIView(new CoreGraphics.CGRect(0, 0, 4, 0));
			handler.PlatformView.LeftViewMode = UIKit.UITextFieldViewMode.Always;
		});
#endif

#if ANDROID
		EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
		{
			// Remove background/underline + any focus tint
			handler.PlatformView.Background = null;
			handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
			handler.PlatformView.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

			// Optional: tweak padding so text isn't cramped
			handler.PlatformView.SetPadding(0, handler.PlatformView.PaddingTop, 0, handler.PlatformView.PaddingBottom);
		});

		PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
		{
			var pv = handler.PlatformView;

			// Remove default underline / background & tints
			pv.Background = null;
			pv.SetBackgroundColor(Android.Graphics.Color.Transparent);
			pv.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

			// Optional: tighten side padding so text aligns with other controls
			pv.SetPadding(0, 0, 0, 0);
		});
#endif
	}

	private static void RegisterRoutes()
	{
		// MauiReactor.Routing.RegisterRoute<Pages.SettingsPage>("settings");
		MauiReactor.Routing.RegisterRoute<Pages.EquipmentManagementPage>("equipment");
		MauiReactor.Routing.RegisterRoute<Pages.BeanManagementPage>("beans");
		MauiReactor.Routing.RegisterRoute<Pages.BeanDetailPage>("bean-detail");
		MauiReactor.Routing.RegisterRoute<Pages.BagDetailPage>("bag-detail");
		MauiReactor.Routing.RegisterRoute<Pages.EquipmentDetailPage>("equipment-detail");
		MauiReactor.Routing.RegisterRoute<Pages.UserProfileManagementPage>("profiles");
		MauiReactor.Routing.RegisterRoute<Pages.ProfileFormPage>("profile-form");
		MauiReactor.Routing.RegisterRoute<Pages.ShotLoggingPage>("shot-logging");
	}
}
