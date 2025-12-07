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
using Microsoft.Maui.Handlers;
using Syncfusion.Maui.Core.Hosting;

#if IOS || MACCATALYST
using BaristaNotes.Platforms.iOS;
#endif

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
					{ "BackgroundTertiaryColor", Colors.Purple },
					{ "PrimaryColor", AppColors.Dark.Primary },
					{ "TextColor", AppColors.Dark.TextPrimary },
					{ "PopupBorderColor", AppColors.Dark.Outline }
				};
				app.Resources.MergedDictionaries.Add(customResources);
			})
			.ConfigureMauiHandlers(handlers =>
			{
				ModifyEntrys();

				// this sets the stage for Large Titles support in iOS
				// #if IOS || MACCATALYST
				// 				handlers.AddHandler<Microsoft.Maui.Controls.Shell, CustomShellRenderer>();
				// #endif
			})
			.UseUXDiversPopups()
			.UseBottomSheet()
			.UseMauiCommunityToolkit()
			.ConfigureSyncfusionCore()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Manrope-Regular.ttf", "Manrope");
				fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemibold");
				fonts.AddFont("MaterialSymbols.ttf", MaterialSymbolsFont.FontFamily);
				fonts.AddFont("coffee-icons.ttf", "coffee-icons");
			});

		// #if IOS || MACCATALYST
		// 		// Custom shell renderer to enable large titles
		// 		CustomShellRenderer.PrefersLargeTitles = true;
		// #endif

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

		// Image services
		builder.Services.AddSingleton<Microsoft.Maui.Media.IMediaPicker>(Microsoft.Maui.Media.MediaPicker.Default);
		builder.Services.AddSingleton<IImagePickerService, ImagePickerService>();
		builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();

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
		MauiReactor.Routing.RegisterRoute<Pages.EquipmentDetailPage>("equipment-detail");
		MauiReactor.Routing.RegisterRoute<Pages.UserProfileManagementPage>("profiles");
		MauiReactor.Routing.RegisterRoute<Pages.ProfileFormPage>("profile-form");
		MauiReactor.Routing.RegisterRoute<Pages.ShotLoggingPage>("shot-logging");
	}
}
