// Petites fonctions JavaScript utilisées par l'application (appelées depuis Blazor).
window.deck = window.deck || {};

// ----- Presse-papiers -----
window.deck.copyText = async function (text) {
    try {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch (e) { /* on essaie la méthode de secours */ }

    try {
        const area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(area);
        return ok;
    } catch (e) {
        return false;
    }
};

// Sélectionne le contenu d'une zone de texte (facilite le copier à la main).
window.deck.selectText = function (selector) {
    const el = document.querySelector(selector);
    if (el) el.select();
};

// ----- Assombrissement après inactivité -----
window.deck.idle = {
    minutes: 0,
    timer: null,
    clockTimer: null,
    overlay: null,
    dimmed: false,
    lastMove: 0,

    configure: function (minutes) {
        this.minutes = Math.max(0, parseInt(minutes, 10) || 0);
        this._ensure();
        this._wake();
    },

    _ensure: function () {
        if (this.overlay) return;

        const self = this;
        const overlay = document.createElement('div');
        overlay.className = 'idle-overlay';
        overlay.setAttribute('aria-hidden', 'true');
        overlay.innerHTML = '<span class="idle-clock"></span>';
        document.body.appendChild(overlay);
        this.overlay = overlay;

        // Un toucher sur l'écran assombri le réveille sans rien déclencher dessous.
        const wake = function (event) {
            if (self.dimmed) {
                event.preventDefault();
                event.stopPropagation();
                self._wake();
            } else {
                self._arm();
            }
        };

        overlay.addEventListener('pointerdown', wake);
        overlay.addEventListener('click', wake);
        window.addEventListener('pointerdown', function () { if (!self.dimmed) self._arm(); }, true);
        window.addEventListener('keydown', function (e) { if (self.dimmed) { e.preventDefault(); self._wake(); } else { self._arm(); } }, true);
        window.addEventListener('mousemove', function () {
            const now = Date.now();
            if (now - self.lastMove < 1000) return;
            self.lastMove = now;
            if (self.dimmed) self._wake(); else self._arm();
        }, true);
    },

    _arm: function () {
        clearTimeout(this.timer);
        this.timer = null;
        if (this.minutes <= 0) return;

        const self = this;
        this.timer = setTimeout(function () { self._dim(); }, this.minutes * 60 * 1000);
    },

    _dim: function () {
        this.dimmed = true;
        this._updateClock();
        this.overlay.classList.add('is-dimmed');

        const self = this;
        clearInterval(this.clockTimer);
        this.clockTimer = setInterval(function () { self._updateClock(); }, 20000);
    },

    _wake: function () {
        this.dimmed = false;
        clearInterval(this.clockTimer);
        this.clockTimer = null;
        if (this.overlay) this.overlay.classList.remove('is-dimmed');
        this._arm();
    },

    _updateClock: function () {
        const clock = this.overlay && this.overlay.querySelector('.idle-clock');
        if (clock) clock.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }
};
