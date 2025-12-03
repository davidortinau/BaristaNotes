using MauiReactor;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Components;

partial class ShotRecordCard : Component
{
    [Prop]
    ShotRecordDto? _shot;

    public override VisualNode Render()
    {
        if (_shot == null)
            return null!;

        return Border(
            VStack(spacing: 8,
                // Header: Drink type and rating
                HStack(spacing: 12,
                    Label($"☕ {_shot.DrinkType}")
                        .FontSize(18)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                    
                    HStack(spacing: 4,
                        Label($"⭐ {_shot.Rating ?? 0}/5")
                            .FontSize(14)
                    )
                    .HEnd()
                ),
                
                // User profiles (if available)
                RenderUserProfiles(),
                
                // Bean info
                Label(_shot.Bean?.Name ?? "Unknown Bean")
                    .FontSize(16),
                
                // Recipe details
                Label($"{_shot.DoseIn}g in → {_shot.ActualOutput ?? _shot.ExpectedOutput}g out ({_shot.ActualTime ?? _shot.ExpectedTime}s)")
                    .FontSize(14)
                    .TextColor(Colors.Gray),
                
                // Equipment (if available)
                RenderEquipment(),
                
                // Timestamp
                Label(FormatTimestamp(_shot.Timestamp))
                    .FontSize(12)
                    .TextColor(Colors.Gray)
            )
            .Padding(12)
        )
        .StrokeThickness(1)
        .Stroke(Colors.LightGray)
        .Margin(8);
    }

    VisualNode? RenderUserProfiles()
    {
        var parts = new List<string>();
        if (_shot?.MadeBy != null)
            parts.Add($"Made by: {_shot.MadeBy.Name}");
        if (_shot?.MadeFor != null)
            parts.Add($"For: {_shot.MadeFor.Name}");

        if (parts.Count == 0)
            return null;

        return Label(string.Join(" • ", parts))
            .FontSize(12)
            .TextColor(Colors.Gray);
    }

    VisualNode? RenderEquipment()
    {
        var equipmentNames = new List<string>();
        if (_shot?.Machine != null)
            equipmentNames.Add(_shot.Machine.Name);
        if (_shot?.Grinder != null)
            equipmentNames.Add(_shot.Grinder.Name);
        equipmentNames.AddRange(_shot?.Accessories?.Select(a => a.Name) ?? Enumerable.Empty<string>());

        if (equipmentNames.Count == 0)
            return null;

        return Label($"Equipment: {string.Join(", ", equipmentNames)}")
            .FontSize(12)
            .TextColor(Colors.Gray);
    }

    string FormatTimestamp(DateTimeOffset timestamp)
    {
        var now = DateTimeOffset.Now;
        var diff = now - timestamp;

        if (diff.TotalMinutes < 1)
            return "Just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return timestamp.ToString("h:mm tt");
        if (diff.TotalDays < 7)
            return timestamp.ToString("ddd h:mm tt");
        
        return timestamp.ToString("MMM d, h:mm tt");
    }
}
