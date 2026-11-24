// Pendant un glisser-déposer, la carte tenue suit le curseur en temps réel — pas une étiquette de texte, la carte
// elle-même (comme dans la plupart des applications). Une copie visuelle suit le curseur (position: fixed, pilotée
// directement en JS, jamais par un rendu Blazor : refaire tout le rendu à chaque pixel de mouvement de souris
// coûtait cher et donnait une impression de ralentissement). L'original reste à sa place, juste atténué, pour que
// la grille ne bouge pas sous les pieds de l'utilisateur pendant le glissement.
window.sdbDragCard = {
    _clone: null,
    _sourceId: null,
    _offsetX: 0,
    _offsetY: 0,
    _handler: null,

    start(elementId, clientX, clientY) {
        this.stop(this._sourceId);

        const el = document.getElementById(elementId);
        if (!el) return;

        const rect = el.getBoundingClientRect();
        this._offsetX = clientX - rect.left;
        this._offsetY = clientY - rect.top;
        this._sourceId = elementId;

        const clone = el.cloneNode(true);
        clone.removeAttribute('id');
        clone.classList.add('lib-card-flying');
        clone.style.position = 'fixed';
        clone.style.left = rect.left + 'px';
        clone.style.top = rect.top + 'px';
        clone.style.width = rect.width + 'px';
        clone.style.height = rect.height + 'px';
        clone.style.margin = '0';
        clone.style.pointerEvents = 'none';
        document.body.appendChild(clone);
        this._clone = clone;

        el.classList.add('lib-card-source-hidden');

        this._handler = (e) => {
            if (this._clone) {
                this._clone.style.left = (e.clientX - this._offsetX) + 'px';
                this._clone.style.top = (e.clientY - this._offsetY) + 'px';
            }
        };
        document.addEventListener('mousemove', this._handler);
    },

    stop(elementId) {
        if (this._handler) document.removeEventListener('mousemove', this._handler);
        this._handler = null;
        if (this._clone) { this._clone.remove(); this._clone = null; }

        const id = elementId || this._sourceId;
        if (id) {
            const el = document.getElementById(id);
            if (el) el.classList.remove('lib-card-source-hidden');
        }
        // Filet de sécurité si l'élément d'origine a changé entre-temps (réordonné, etc).
        document.querySelectorAll('.lib-card-source-hidden').forEach(e => e.classList.remove('lib-card-source-hidden'));
        this._sourceId = null;
    }
};
