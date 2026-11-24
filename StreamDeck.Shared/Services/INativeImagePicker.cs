namespace StreamDeck.Services;

/// <summary>
/// v1.34.19, correctif demandé ("l'ajout d'image... ça donne un écran noir / des fois ça crash durant
/// l'exploration de l'explorateur") : certains hôtes ont leur propre sélecteur de fichier natif (voir
/// NativePickerImageAdapter côté StreamDeck.ScriptBuilder, qui adapte son INativePicker déjà existant) PARCE QUE le
/// composant Blazor standard <c>&lt;InputFile&gt;</c> (utilisé par ButtonEditor/OptionEditor pour importer une
/// image) provoque un écran noir, voire un plantage, dans la WebView de Script Builder sur Windows — exactement
/// pour ça que Script Builder avait déjà construit ce sélecteur natif ailleurs (image épinglée du widget, logo de
/// signature mail, police personnalisée) avant même l'onglet « Téléphone ». Optionnel (<see cref="NoOpNativeImagePicker"/>
/// par défaut, comme sur Android où &lt;InputFile&gt; marche très bien) : ButtonEditor/OptionEditor n'utilisent le
/// sélecteur natif que quand <see cref="CanPick"/> est vrai, et gardent &lt;InputFile&gt; sinon.
/// </summary>
public interface INativeImagePicker
{
    bool CanPick { get; }

    /// <summary>Choisit un fichier image et renvoie son contenu déjà encodé en data URI (<c>data:&lt;mime&gt;;base64,...</c>), ou null si annulé/échec.</summary>
    Task<string?> PickImageAsync();
}

/// <summary>Repli par défaut : pas de sélecteur natif pour cet hôte, ButtonEditor/OptionEditor utilisent &lt;InputFile&gt;.</summary>
public class NoOpNativeImagePicker : INativeImagePicker
{
    public bool CanPick => false;
    public Task<string?> PickImageAsync() => Task.FromResult<string?>(null);
}
