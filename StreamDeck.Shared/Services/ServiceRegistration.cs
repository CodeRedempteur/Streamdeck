using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace StreamDeck.Services;

public static class ServiceRegistration
{
    /// <summary>Enregistre les services communs. Chaque hôte doit aussi enregistrer son <see cref="IAppPlatform"/>.</summary>
    public static IServiceCollection AddStreamDeckCore(this IServiceCollection services)
    {
        // v1.34.19 : TryAddSingleton — repli inactif ici (<InputFile> reste utilisé, comme avant), un hôte avec un
        // sélecteur natif (Script Builder sur Windows) l'enregistre lui-même avant d'appeler cette méthode.
        services.TryAddSingleton<INativeImagePicker, NoOpNativeImagePicker>();
        // v1.34.20 : repli inactif ici — seul Android enregistre un vrai fournisseur avant d'appeler cette méthode.
        services.TryAddSingleton<ICrashLogProvider, NoOpCrashLogProvider>();
        // v1.36 : repli inactif ici (pas de son) — Android enregistre un vrai lecteur d'alarme avant d'appeler
        // cette méthode (voir TimerService/TimerOverlay, bouton "Minuteur").
        services.TryAddSingleton<IAlarmPlayer, NoOpAlarmPlayer>();
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ScriptRunner>();
        services.AddSingleton<WakeService>();
        services.AddSingleton<ActionService>();
        services.AddSingleton<HistoryService>();
        services.AddSingleton<VoiceHistoryService>();
        services.AddSingleton<SecurityService>();
        services.AddSingleton<VoiceMatcher>();
        services.AddSingleton<InfoDisplayService>();
        services.AddSingleton<TimerService>();
        // Météo pour le bouton "Affichage plein écran" (InfoKind.Weather, v1.34.6) — et pour le widget de bureau
        // (StreamDeck.ScriptBuilder), qui référence ce projet et l'enregistre à nouveau dans son propre conteneur.
        services.AddSingleton<WeatherService>();
        // Pause de l'envoi au serveur (bouton de la barre du haut, v1.35) et bouton "Retrouver le téléphone".
        services.AddSingleton<RequestPauseService>();
        services.AddSingleton<FindPhoneService>();
        return services;
    }
}
