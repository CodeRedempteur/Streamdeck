using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;
using StreamDeck.Components;
using StreamDeck.Services;

namespace StreamDeck.Desktop;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // "--windowed" : fenêtre normale (pratique pour développer sur son PC)
        // "--debug"    : outils de développement du navigateur (F12) + logs détaillés
        var windowed = args.Contains("--windowed");
        var debug = args.Contains("--debug");

        var builder = PhotinoBlazorApp.CreateBuilder(args);

        if (debug)
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Services.AddSingleton<IAppPlatform, DesktopPlatform>();
        builder.Services.AddStreamDeckCore();

        builder.RootComponents.Add<App>("#app");

        builder.ConfigureMainWindow(window =>
        {
            window.SetTitle("Stream Deck");
            window.SetDevToolsEnabled(debug);

            if (windowed)
                window.SetSize(1100, 700);
            else
                window.SetFullScreen(true);
        });

        await using var app = builder.Build();
        return app.Run();
    }
}
