using Microsoft.UI.Xaml;

namespace StreamDeck.ScriptBuilderApp.WinUI;

/// <summary>Point d'entrée WinUI 3 : délègue tout à MauiProgram.</summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
