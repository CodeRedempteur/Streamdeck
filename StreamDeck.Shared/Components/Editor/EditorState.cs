using StreamDeck.Models;

namespace StreamDeck.Components.Editor;

/// <summary>
/// État partagé de l'écran de réglages : la configuration en cours d'édition (copie de travail)
/// et ce qui est sélectionné (PC, espace, page). Les sections le modifient et préviennent via <see cref="Notify"/>.
/// </summary>
public class EditorState
{
    public EditorState(DeckConfig config, int pageIndex)
    {
        Cfg = config;
        Cfg.EnsureValid();

        PcId = Cfg.DefaultPc.Id;
        WorkspaceId = Cfg.ActiveWorkspace.Id;
        PageIndex = pageIndex;
        ClampPage();
    }

    public DeckConfig Cfg { get; }

    public Guid PcId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int PageIndex { get; set; }

    /// <summary>Suppression en attente de confirmation.</summary>
    public Guid? ConfirmWorkspace { get; set; }
    public Guid? ConfirmPage { get; set; }
    public Guid? ConfirmPc { get; set; }

    public event Action? Changed;

    public void Notify() => Changed?.Invoke();

    // ----- Sélection courante -----

    public PcProfile Pc => Cfg.Pcs.FirstOrDefault(p => p.Id == PcId) ?? Cfg.Pcs[0];
    public Workspace Ws => Cfg.Workspaces.FirstOrDefault(w => w.Id == WorkspaceId) ?? Cfg.Workspaces[0];
    public DeckPage Page => Ws.Pages[Math.Clamp(PageIndex, 0, Ws.Pages.Count - 1)];
    public int PageCount => Ws.Pages.Count;

    public void ClampPage() => PageIndex = Math.Clamp(PageIndex, 0, Ws.Pages.Count - 1);

    public void CancelConfirm()
    {
        ConfirmWorkspace = null;
        ConfirmPage = null;
        ConfirmPc = null;
    }

    /// <summary>Après un import : revient sur des sélections valides.</summary>
    public void ResetSelection()
    {
        Cfg.EnsureValid();
        PcId = Cfg.DefaultPc.Id;
        WorkspaceId = Cfg.ActiveWorkspace.Id;
        PageIndex = 0;
        CancelConfirm();
    }

    // ----- Sélecteur d'icônes partagé (boutons et espaces) -----

    public bool IconPickerOpen { get; private set; }
    public string? IconCurrent { get; private set; }
    private Action<string>? _iconApply;

    public void RequestIcon(string? current, Action<string> apply)
    {
        IconCurrent = current;
        _iconApply = apply;
        IconPickerOpen = true;
        Notify();
    }

    public void PickIcon(string cls)
    {
        _iconApply?.Invoke(cls);
        CloseIconPicker();
    }

    public void CloseIconPicker()
    {
        IconPickerOpen = false;
        _iconApply = null;
        Notify();
    }
}
