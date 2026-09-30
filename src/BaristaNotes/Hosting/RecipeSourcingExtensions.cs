using BaristaNotes.Core.Hosting;

namespace BaristaNotes.Hosting;

internal static class RecipeSourcingExtensions
{
    public static MauiAppBuilder AddRecipeSourcing(this MauiAppBuilder builder)
    {
        builder.Services.AddBaristaNotesRecipeSourcing();
        return builder;
    }
}
