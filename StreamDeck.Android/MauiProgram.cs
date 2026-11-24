using Microsoft.Extensions.DependencyInjection;
using StreamDeck.Services;

namespace StreamDeck.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<DeckApp>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        builder.Services.AddSingleton<IAppPlatform, AndroidPlatform>();
        // Journal du dernier plantage (v1.34.20, voir CrashLog) — doit être enregistré AVANT AddStreamDeckCore()
        // (son repli TryAddSingleton inactif sinon).
        builder.Services.AddSingleton<ICrashLogProvider, AndroidCrashLogProvider>();
        // Alarme des minuteurs (v1.36, voir TimerService) — même raison d'ordre que ci-dessus : repli inactif sinon.
        builder.Services.AddSingleton<IAlarmPlayer, AndroidAlarmPlayer>();
        builder.Services.AddStreamDeckCore();

        return builder.Build();
    }
}
