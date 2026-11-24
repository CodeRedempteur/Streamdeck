namespace StreamDeck.Mobile;

public partial class DeckApp : Application
{
    public DeckApp()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new Window(new MainPage()) { Title = "Stream Deck" };
}
