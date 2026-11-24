namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// v1.34.13 : l'éditeur d'une liste de tâches (TodoEditor.razor) est maintenant une page à part entière (route
/// "/todo-editor"), plein écran comme l'éditeur de script, plutôt qu'une modale — demandé explicitement ("fait
/// plutôt en sorte d'avoir une page comme les pages de script"). Une page perd son état local à chaque navigation ;
/// ce service (singleton, comme <see cref="EditorSession"/> pour les scripts) porte donc juste l'identifiant de la
/// liste en cours d'édition d'une page à l'autre — le contenu lui-même reste dans <c>BuilderConfigService.Current.TodoLists</c>,
/// pas dupliqué ici.
/// </summary>
public class TodoEditorSession
{
    public string? ListId { get; set; }
}
