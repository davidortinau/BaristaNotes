namespace BaristaNotes.Core.Services.Workflows;

public enum VoiceRouteKind
{
    Drink,
    History,
    Settings,
    Profiles,
    Beans,
    Equipment,
    Shot,
    Profile,
    Bean,
    Bag,
    EquipmentDetail,
    Ranges
}

public sealed record VoiceRoutePlan(
    VoiceRouteKind Kind,
    bool IsRoot,
    int? EntityId,
    int? BeanId,
    string? BeanName,
    string? IgnoredQuery)
{
    public static VoiceRoutePlan From(VoiceNavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var split = request.Route.Split('?', 2);
        var route = split[0];
        var kind = route switch
        {
            "//shots" or "shots" => VoiceRouteKind.Drink,
            "//history" or "history" => VoiceRouteKind.History,
            "//settings" or "settings" => VoiceRouteKind.Settings,
            "profiles" => VoiceRouteKind.Profiles,
            "beans" => VoiceRouteKind.Beans,
            "equipment" => VoiceRouteKind.Equipment,
            "shot-logging" => VoiceRouteKind.Shot,
            "profile-form" => VoiceRouteKind.Profile,
            "bean-detail" => VoiceRouteKind.Bean,
            "bag-detail" => VoiceRouteKind.Bag,
            "equipment-detail" => VoiceRouteKind.EquipmentDetail,
            "value-ranges" => VoiceRouteKind.Ranges,
            _ => throw new ArgumentException("Unsupported native voice route.", nameof(request))
        };
        if (request.EntityId.HasValue && kind is not (VoiceRouteKind.Shot or VoiceRouteKind.Profile
            or VoiceRouteKind.Bean or VoiceRouteKind.Bag or VoiceRouteKind.EquipmentDetail))
            throw new ArgumentException("This route does not accept an entity ID.", nameof(request));
        return new(kind, route.StartsWith("//", StringComparison.Ordinal), request.EntityId,
            request.BeanId, request.BeanName, split.Length == 2 ? split[1] : null);
    }
}
