using Microsoft.Extensions.DependencyInjection;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<BuilderApp>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        builder.Services.AddSingleton<IBuilderPlatform, WindowsPlatform>();
        builder.Services.AddSingleton<INativePicker, WindowsPicker>();
        builder.Services.AddSingleton<IMacroRecorder, WindowsMacroRecorder>();
        builder.Services.AddSingleton<IGlobalHotkeyService, WindowsGlobalHotkeyService>();
        builder.Services.AddSingleton<IWidgetHost, WindowsWidgetHost>();
        // Enregistrement vocal de trigger depuis le PC (v1.34.18) — doit être enregistré AVANT AddScriptBuilder()
        // (qui n'ajoute qu'un repli inactif via TryAddSingleton si rien n'est déjà enregistré, voir ServiceRegistration).
        builder.Services.AddSingleton<IPcSpeechRecognizer, WindowsSpeechRecognizer>();
        // Écoute vocale continue du widget (v1.35) — même principe/ordre d'enregistrement que ci-dessus, mais une
        // interface distincte (IPcVoiceListener) : celle-ci écoute en continu et déclenche directement une touche,
        // plutôt que de remplir un champ de texte après une seule phrase.
        builder.Services.AddSingleton<IPcVoiceListener, WindowsVoiceListener>();
        // Sélecteur d'image natif pour ButtonEditor/OptionEditor (v1.34.19, voir NativePickerImageAdapter) — doit
        // être enregistré AVANT AddScriptBuilder() (son repli TryAddSingleton inactif sinon).
        builder.Services.AddSingleton<StreamDeck.Services.INativeImagePicker, NativePickerImageAdapter>();
        // Alarme des minuteurs (v1.36.1, "sur pc ça sonne pas", voir WindowsAlarmPlayer) — même raison d'ordre que
        // ci-dessus : AddScriptBuilder() n'ajoute qu'un repli inactif (TryAddSingleton) si rien n'est déjà enregistré.
        builder.Services.AddSingleton<StreamDeck.Services.IAlarmPlayer, WindowsAlarmPlayer>();
        builder.Services.AddScriptBuilder();

        var app = builder.Build();

        // v1.37.4, demandé explicitement ("rappel sur toute l'app") : CalendarService est un singleton avec son
        // propre minuteur de rappels (voir ce service), mais un service .NET "Singleton" n'est créé qu'à la première
        // fois que quelque chose le demande — sans cette ligne, il ne se créerait (et donc ne commencerait à vérifier
        // les rappels) qu'à la première ouverture du widget flottant, exactement le bug signalé. Le forcer ici fait
        // tourner les rappels dès le lancement de l'app, widget ouvert ou non.
        _ = app.Services.GetRequiredService<StreamDeck.ScriptBuilder.Services.CalendarService>();

        // v1.38 : roues WheelConvert — même raison que ci-dessus : le pont doit tourner dès le lancement pour republier
        // les roues à chaque modification de la configuration, même si l'onglet Réglages n'est jamais ouvert.
        _ = app.Services.GetRequiredService<StreamDeck.ScriptBuilder.Services.WheelBridgeService>();

        return app;
    }
}
