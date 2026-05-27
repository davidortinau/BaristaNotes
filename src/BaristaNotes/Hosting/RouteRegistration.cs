namespace BaristaNotes.Hosting;

internal static class RouteRegistration
{
    public static void RegisterAll()
    {
        MauiReactor.Routing.RegisterRoute<Pages.EquipmentManagementPage>("equipment");
        MauiReactor.Routing.RegisterRoute<Pages.BeanManagementPage>("beans");
        MauiReactor.Routing.RegisterRoute<Pages.BeanDetailPage>("bean-detail");
        MauiReactor.Routing.RegisterRoute<Pages.BagDetailPage>("bag-detail");
        MauiReactor.Routing.RegisterRoute<Pages.EquipmentDetailPage>("equipment-detail");
        MauiReactor.Routing.RegisterRoute<Pages.UserProfileManagementPage>("profiles");
        MauiReactor.Routing.RegisterRoute<Pages.ProfileFormPage>("profile-form");
        MauiReactor.Routing.RegisterRoute<Pages.ShotLoggingGridPage>("shot-logging");
    }
}
