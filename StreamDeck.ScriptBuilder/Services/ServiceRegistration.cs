using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StreamDeck.Services;

namespace StreamDeck.ScriptBuilder.Services;

public static class ServiceRegistration
{
    /// <summary>
    /// Enregistre les services du constructeur. L'hôte doit aussi enregistrer son <see cref="IBuilderPlatform"/> et,
    /// pour l'enregistrement vocal de trigger (v1.34.18), son <see cref="IPcSpeechRecognizer"/> AVANT d'appeler
    /// cette méthode — <see cref="NoOpSpeechRecognizer"/> n'est ajouté ici qu'en secours (<c>TryAddSingleton</c>,
    /// ne remplace pas un enregistrement déjà fait par l'hôte). Même chose depuis v1.35 pour
    /// <see cref="IPcVoiceListener"/> (écoute continue, pas à confondre avec <see cref="IPcSpeechRecognizer"/>
    /// ci-dessus) : <see cref="NoOpVoiceListener"/> n'est qu'un repli.
    /// </summary>
    public static IServiceCollection AddScriptBuilder(this IServiceCollection services)
    {
        services.TryAddSingleton<IPcSpeechRecognizer, NoOpSpeechRecognizer>();
        services.TryAddSingleton<IPcVoiceListener, NoOpVoiceListener>();
        // Sélecteur d'image natif (v1.34.19, voir INativeImagePicker côté StreamDeck.Shared) — repli inactif ici ;
        // l'hôte Windows enregistre NativePickerImageAdapter par-dessus avant d'appeler AddScriptBuilder().
        services.TryAddSingleton<StreamDeck.Services.INativeImagePicker, StreamDeck.Services.NoOpNativeImagePicker>();
        // Alarme des minuteurs (v1.36, voir TimerService/WidgetTimerService) — repli inactif ici ; l'hôte Windows
        // enregistre une vraie implémentation avant d'appeler AddScriptBuilder().
        services.TryAddSingleton<StreamDeck.Services.IAlarmPlayer, StreamDeck.Services.NoOpAlarmPlayer>();
        services.AddSingleton<BuilderConfigService>();
        services.AddSingleton<ScriptStore>();
        services.AddSingleton<ScheduleStore>();
        services.AddSingleton<ScriptTester>();
        services.AddSingleton<FileSystemBrowser>();
        services.AddSingleton<ShellService>();
        services.AddSingleton<ProcessService>();
        services.AddSingleton<ServerRestarter>();
        services.AddSingleton<MacroControlService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<EditorSession>();
        services.AddSingleton<TodoEditorSession>();
        services.AddSingleton<MailFetchService>();
        services.AddSingleton<WeatherService>();
        services.AddSingleton<PhoneRunner>();
        // Onglet « Téléphone » (v1.34.16) : réutilise les composants d'édition partagés avec l'app téléphone
        // (WorkspaceSection, PagesSection, ButtonEditor…), qui attendent IAppPlatform/ConfigService par injection.
        services.AddSingleton<IAppPlatform, ScriptBuilderPhonePlatform>();
        services.AddSingleton<ConfigService>();
        // Minuteur/chronomètre (v1.36) : TimerService pour l'onglet "Téléphone" (ButtonEditor réutilisé, bouton de
        // type Timer) — WidgetTimerService, séparé, pour les touches du widget lui-même (exécution locale, pas de
        // PcProfile/ActionService impliqué, voir WidgetModels.cs/Widget.razor).
        services.AddSingleton<TimerService>();
        services.AddSingleton<WidgetTimerService>();
        // PcSection (choisir/tester/réveiller le PC d'un bouton, "plusieurs PC" demandé explicitement dès la
        // v1.34.16) — services sûrs : tout est déclenché par un clic explicite (Tester/Réveiller), rien en tâche de
        // fond à l'enregistrement de ces singletons.
        services.AddSingleton<RequestPauseService>();
        services.AddSingleton<ScriptRunner>();
        services.AddSingleton<WakeService>();
        services.AddSingleton<PhoneConfigTransferService>();
        services.AddSingleton<PcCommandServerService>();
        // Onglet « Calendrier » (v1.37) : tâches fréquentes/routines/réveils de jour — voir CalendarModels.cs,
        // déclenchés par Check() appelé périodiquement depuis Widget.razor (même principe que CheckTodoNotifications).
        services.AddSingleton<CalendarService>();
        // Roues WheelConvert (v1.38) : publie BuilderConfig.WheelConvertWheels dans %AppData%\WheelConvert\providers\
        // à chaque modification — doit être créé au lancement (voir MauiProgram, même principe que CalendarService).
        services.AddSingleton<WheelBridgeService>();
        return services;
    }
}
