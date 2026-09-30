using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private async Task LoadBeanRecipesAsync(BeanEditor editor, bool refresh = false)
    {
        if (!IsCurrentBean(editor) || editor.State.Draft.BeanId is not int id
            || (refresh ? editor.RefreshingRecipes : editor.LoadingRecipes)) return;
        if (refresh) editor.RefreshingRecipes = true; else editor.LoadingRecipes = true;
        editor.State.RecipeError = null;
        RenderBeanRecipes(editor);
        try
        {
            var recipes = await InScopeAsync(services => refresh
                ? services.GetRequiredService<IBeanService>().RefreshRecipesAsync(id, editor.Cancellation)
                : services.GetRequiredService<IRecipeService>().GetRecipesForBeanAsync(id));
            if (!IsCurrentBean(editor)) return;
            editor.State.Recipes = recipes.ToList();
            if (refresh) editor.RefreshingRecipes = false; else editor.LoadingRecipes = false;
            RenderBeanRecipes(editor);
            ObserveBeanTask(() => TranslateBeanRecipesAsync(editor));
            if (refresh)
            {
                if (recipes.Count == 0)
                    await _feedback.ShowInfoAsync("No recipes found for this bean yet.");
                else
                    await _feedback.ShowSuccessAsync($"Refreshed {recipes.Count} recipe(s).");
            }
        }
        catch (OperationCanceledException) when (editor.Cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Recipe {Operation} for bean {BeanId} failed", refresh ? "refresh" : "read", id);
            if (IsCurrentBean(editor))
            {
                editor.State.RecipeError = $"Failed to {(refresh ? "refresh" : "load")} recipes: {ErrorMessage(exception)}";
                if (refresh) ShowFeedback("Recipe refresh failed.", isError: true);
            }
        }
        finally
        {
            if (refresh) editor.RefreshingRecipes = false; else editor.LoadingRecipes = false;
            if (IsCurrentBean(editor)) RenderBeanRecipes(editor);
        }
    }

    private async Task TranslateBeanRecipesAsync(BeanEditor editor)
    {
        if (!IsCurrentBean(editor)) return;
        editor.Translation?.Cancel();
        editor.Translation?.Dispose();
        var source = CancellationTokenSource.CreateLinkedTokenSource(editor.Cancellation);
        editor.Translation = source;
        var cancellation = source.Token;
        var recipes = editor.State.Recipes.ToArray();
        var beanId = editor.State.Draft.BeanId;
        editor.Translating = true;
        RenderBeanRecipes(editor);
        try
        {
            var result = await InScopeAsync(async services =>
            {
                var equipment = await services.GetRequiredService<IEquipmentRepository>().GetByTypeAsync(EquipmentType.Grinder);
                cancellation.ThrowIfCancellationRequested();
                var grinder = equipment.FirstOrDefault(item => item.IsActive && !item.IsDeleted);
                var translations = new Dictionary<int, GrindTranslationResult>();
                if (grinder is not null)
                {
                    await services.GetRequiredService<IGrinderProfileRepository>().GetOrCreateForEquipmentAsync(grinder);
                    var translator = services.GetRequiredService<IGrindTranslationService>();
                    foreach (var recipe in recipes)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(recipe.GrindHint)) continue;
                        try
                        {
                            translations[recipe.Id] = await translator.TranslateAsync(
                                new GrindTranslationRequest(grinder.Id, grinder.Name, recipe.GrindHint,
                                    recipe.BrewMethod, beanId), cancellation);
                        }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                        catch (Exception exception)
                        {
                            _logger.LogWarning(exception, "Recipe {RecipeId} grind translation unavailable", recipe.Id);
                        }
                    }
                }
                return (Id: grinder?.Id, Name: grinder?.Name, Translations: translations);
            });
            if (!IsCurrentBean(editor) || cancellation.IsCancellationRequested || !ReferenceEquals(editor.Translation, source))
                return;
            editor.State.RecipeGrinderId = result.Id;
            editor.State.RecipeGrinderName = result.Name;
            editor.State.Translations = result.Translations;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Recipe grind translation batch unavailable");
        }
        finally
        {
            if (IsCurrentBean(editor) && ReferenceEquals(editor.Translation, source))
            {
                editor.Translating = false;
                RenderBeanRecipes(editor);
            }
        }
    }

    private void RenderBeanRecipes(BeanEditor editor)
    {
        if (!IsCurrentBean(editor)) return;
        var section = BeanSection("RECIPES");
        var heading = _style.Row();
        heading.SetGravity(GravityFlags.CenterVertical);
        var caption = section.GetChildAt(0)!;
        section.RemoveView(caption);
        heading.AddView(caption, new LinearLayout.LayoutParams(0, -2, 1));
        section.AddView(heading);
        var screen = ReplaceBeanSection(editor, "recipes", editor.RecipesHost, section);
        if (editor.RefreshingRecipes)
            heading.AddView(new ProgressBar(this), new LinearLayout.LayoutParams(_style.Dp(20), _style.Dp(20)));
        else
        {
            var refresh = BeanMiniAction(editor.State.Recipes.Count == 0 ? "FIND" : "REFRESH", "BeanRefreshRecipes");
            Bind(screen, refresh, () => ObserveBeanTask(() => LoadBeanRecipesAsync(editor, refresh: true)));
            heading.AddView(refresh);
        }
        if (editor.State.RecipeError is { } error)
            section.AddView(_style.Label(error, 13, color: _style.Error),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        if (editor.LoadingRecipes && editor.State.Recipes.Count == 0)
            section.AddView(new ProgressBar(this), new LinearLayout.LayoutParams(-2, -2)
            {
                Gravity = GravityFlags.CenterHorizontal, TopMargin = _style.Dp(10)
            });
        if (!editor.LoadingRecipes && !editor.RefreshingRecipes && editor.State.Recipes.Count == 0 && editor.State.RecipeError is null)
        {
            section.AddView(_style.Label("No recipes yet.", 14),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
            // Preserve the source FIND/REFRESH wording mismatch.
            section.AddView(_style.Label("Tap REFRESH to look up brew guides.", 12, color: _style.Secondary),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(4) });
        }
        foreach (var recipe in editor.State.Recipes)
        {
            var card = _style.Column();
            card.SetBackgroundColor(_style.SurfaceVariant);
            card.SetPadding(_style.Dp(12), _style.Dp(12), _style.Dp(12), _style.Dp(12));
            NativeStyle.Identify(card, $"BeanRecipe_{recipe.Id}");
            var row = _style.Row();
            row.SetGravity(GravityFlags.CenterVertical);
            row.AddView(_style.Label(recipe.BrewMethod.DisplayName(), 16, true), new LinearLayout.LayoutParams(0, -2, 1));
            void Badge(string text)
            {
                var badge = _style.Label(text, 9, true, _style.Secondary);
                badge.LetterSpacing = 1.5f * .0624f;
                badge.SetPadding(_style.Dp(6), _style.Dp(2), _style.Dp(6), _style.Dp(2));
                row.AddView(badge, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(6) });
            }
            Badge(BeanDisplay.RecipeSourceText(recipe.Source));
            if (recipe.IsEditedByUser) Badge("EDITED");
            card.AddView(row);
            void Line(string text, int size = 12, bool bold = false, Color? color = null)
                => card.AddView(_style.Label(text, size, bold, color ?? _style.Secondary),
                    new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(6) });
            if (!string.IsNullOrWhiteSpace(recipe.Title)) Line(recipe.Title);
            var parameters = BeanDisplay.RecipeParameters(recipe);
            var hasParameters = BeanDisplay.HasRecipeParameters(recipe);
            Line(parameters, hasParameters ? 13 : 12, hasParameters, hasParameters ? _style.Text : _style.Secondary);
            if (!string.IsNullOrWhiteSpace(recipe.GrindHint))
            {
                Line($"Grind: {recipe.GrindHint}");
                if (editor.State.RecipeGrinderId.HasValue)
                {
                    editor.State.Translations.TryGetValue(recipe.Id, out var translation);
                    var chip = RenderRecipeTranslation(translation, editor.Translating && translation is null);
                    if (chip is not null)
                        card.AddView(chip, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(6) });
                }
            }
            if (!string.IsNullOrWhiteSpace(recipe.Notes)) Line(recipe.Notes);
            if (!string.IsNullOrWhiteSpace(recipe.SourceUrl))
            {
                var link = _style.Label("View source →", 12, true, _style.Primary);
                link.Focusable = true;
                NativeStyle.Identify(link, $"RecipeSource_{recipe.Id}");
                Bind(screen, link, () => OpenRecipeSource(recipe.SourceUrl));
                card.AddView(link, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(6) });
            }
            section.AddView(card, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        }
    }

    private View? RenderRecipeTranslation(GrindTranslationResult? result, bool loading)
    {
        if (loading) return _style.Label("Translating grind…", 11, color: _style.Secondary);
        if (result is null) return null;
        var presentation = BeanDisplay.Translation(result);
        if (presentation.Headline is null)
            return string.IsNullOrWhiteSpace(presentation.Explanation)
                ? null
                : _style.Label(presentation.Explanation, 11, color: _style.Secondary);
        var column = _style.Column();
        var row = _style.Row();
        row.SetGravity(GravityFlags.CenterVertical);
        row.AddView(_style.Label(presentation.Headline, 12, true), new LinearLayout.LayoutParams(0, -2, 1));
        var color = presentation.BadgeKind switch
        {
            GrindTranslationBadgeKind.UserHistory => Color.SeaGreen,
            GrindTranslationBadgeKind.Calculated or GrindTranslationBadgeKind.KnownMatch => Color.SteelBlue,
            GrindTranslationBadgeKind.AI => Color.MediumPurple,
            _ => Color.Gray
        };
        var badge = _style.Label(presentation.Badge, 10, color: Color.White);
        badge.SetBackgroundColor(color);
        badge.SetPadding(_style.Dp(6), _style.Dp(2), _style.Dp(6), _style.Dp(2));
        row.AddView(badge, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(6) });
        column.AddView(row);
        if (!string.IsNullOrWhiteSpace(presentation.Explanation))
            column.AddView(_style.Label(presentation.Explanation, 11, color: _style.Secondary),
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(2) });
        return column;
    }

    private void OpenRecipeSource(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new InvalidOperationException("Recipe source is not a supported web URL.");
            using var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(uri.AbsoluteUri));
            intent.AddCategory(Intent.CategoryBrowsable!);
            StartActivity(intent);
        }
        catch (Exception exception)
        {
            // The source leaves the recipe page in place on browser failure.
            _logger.LogWarning(exception, "Opening recipe source failed");
        }
    }
}
