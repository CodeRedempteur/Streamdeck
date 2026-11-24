using Microsoft.Extensions.DependencyInjection;
using StreamDeck.ScriptBuilder.Services;

namespace StreamDeck.ScriptBuilderApp;

public partial class BuilderApp : Application
{
    public BuilderApp()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "Stream Deck Script Builder",
            Width = 1360,
            Height = 900,
            MinimumWidth = 760,
            MinimumHeight = 560
        };

        // Ouvre aussi le widget de bureau automatiquement au lancement (demandé, v1.34.2) — juste après la création
        // de la fenêtre principale plutôt que dans son constructeur, pour ne pas retarder son tout premier affichage.
        // Protégé par try/catch : un échec ici (service introuvable, widget déjà géré autrement…) ne doit jamais
        // empêcher le lancement de l'application elle-même.
        window.Created += (_, _) =>
        {
            try { IPlatformApplication.Current?.Services.GetService<IWidgetHost>()?.Open(); }
            catch (Exception) { /* l'appli principale reste utilisable ; le widget peut toujours s'ouvrir à la main */ }
        };

        return window;
    }
}
