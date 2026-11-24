// Le widget de bureau n'a plus de barre de titre Windows (voir WindowsWidgetHost) : ce fichier déclenche, au premier
// appui sur la poignée, le déplacement NATIF de la fenêtre côté .NET (WindowsWidgetHost.BeginDrag, via
// ReleaseCapture + WM_NCLBUTTONDOWN/HTCAPTION) — Windows conduit ensuite tout le glissement lui-même, exactement
// comme il l'aurait fait avec une vraie barre de titre.
//
// Une version plus poussée (sous-classement natif de la fenêtre, réponse directe à WM_NCHITTEST côté C#, sans passer
// par ce fichier du tout) a été essayée pour régler le défaut ci-dessous, mais a cassé TOUT glissement sur un vrai
// Windows — sous-classer la WndProc d'une fenêtre WinUI 3 qui héberge un BlazorWebView semble casser le routage des
// messages souris vers le contenu. Cette approche a été entièrement retirée ; ce fichier redevient donc le mécanisme
// actif, avec son défaut connu :
//
// Défaut connu, non résolu (plusieurs tentatives : releasePointerCapture, pointerdown -> mousedown avec
// stopPropagation) : il faut relâcher le clic pour que le glissement démarre (le curseur reste en attente pendant
// ce temps) — probablement un délai dans l'aller-retour asynchrone JS -> .NET (invokeMethodAsync), qui fait arriver
// le message natif après que le clic physique soit déjà terminé pour un geste bref.
window.sdbWidgetDrag = {
    _el: null,
    _onDown: null,

    start(dotnetRef, elementId) {
        this.stop();

        const el = document.getElementById(elementId);
        if (!el) return;

        this._onDown = (e) => {
            if (e.button !== 0) return; // seul le clic gauche fait glisser
            if (e.target.closest('button')) return;

            // stopPropagation (pas preventDefault) : évite qu'un ancêtre ou un gestionnaire par défaut du WebView
            // ne prenne, lui aussi, une capture de souris qui retarderait la prise en main native ci-dessous.
            e.stopPropagation();
            try { el.releasePointerCapture(e.pointerId); } catch { /* pas de capture à relâcher : rien à faire */ }

            dotnetRef.invokeMethodAsync('OnWidgetDragStart');
        };

        el.addEventListener('mousedown', this._onDown);
        this._el = el;
    },

    stop() {
        if (this._el && this._onDown) {
            this._el.removeEventListener('mousedown', this._onDown);
        }
        this._el = null;
        this._onDown = null;
    }
};
