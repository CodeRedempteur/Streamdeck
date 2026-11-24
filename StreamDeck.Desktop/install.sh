#!/usr/bin/env bash
# Installe Stream Deck sur Ubuntu : copie l'app, l'ajoute au menu et la lance au démarrage de session.
# À lancer depuis le dossier publié :  bash install.sh
set -euo pipefail

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_DIR="$HOME/.local/share/streamdeck"

if [ ! -f "$SRC_DIR/StreamDeck" ]; then
  echo "Exécutable 'StreamDeck' introuvable dans $SRC_DIR (lance ce script depuis le dossier publié)." >&2
  exit 1
fi

if ! ldconfig -p 2>/dev/null | grep -q 'libwebkit2gtk-4.1'; then
  echo "⚠ WebKitGTK 4.1 est requis. Installe-le puis relance ce script :"
  echo "    sudo apt install libwebkit2gtk-4.1-0 libnotify4"
  exit 1
fi

echo "→ Copie vers $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"
if [ "$SRC_DIR" != "$INSTALL_DIR" ]; then
  cp -a "$SRC_DIR"/. "$INSTALL_DIR"/
fi
chmod +x "$INSTALL_DIR/StreamDeck"

echo "→ Script de lancement"
cat > "$INSTALL_DIR/run.sh" <<'RUN'
#!/usr/bin/env bash
cd "$(dirname "$(readlink -f "$0")")"

# Si la fenêtre reste blanche ou noire (Raspberry Pi, certains GPU), décommente une de ces lignes :
# export WEBKIT_DISABLE_DMABUF_RENDERER=1
# export WEBKIT_DISABLE_COMPOSITING_MODE=1

exec ./StreamDeck "$@"
RUN
chmod +x "$INSTALL_DIR/run.sh"

ENTRY="[Desktop Entry]
Type=Application
Name=Stream Deck
Comment=Boutons pour lancer des scripts
Exec=$INSTALL_DIR/run.sh
Path=$INSTALL_DIR
Terminal=false
Categories=Utility;"

echo "→ Entrée dans le menu des applications"
mkdir -p "$HOME/.local/share/applications"
printf '%s\n' "$ENTRY" > "$HOME/.local/share/applications/streamdeck.desktop"

echo "→ Démarrage automatique à l'ouverture de session"
mkdir -p "$HOME/.config/autostart"
printf '%s\nX-GNOME-Autostart-enabled=true\nX-GNOME-Autostart-Delay=2\n' "$ENTRY" > "$HOME/.config/autostart/streamdeck.desktop"

echo
echo "Terminé."
echo "  • Lancer maintenant : $INSTALL_DIR/run.sh   (ou chercher « Stream Deck » dans les applications)"
echo "  • Au prochain démarrage de session, l'app s'ouvre toute seule en plein écran."
echo "  • Quitter : Réglages ⚙ → Quitter l'application  (ou Alt+F4)"
echo "  • Fenêtre normale pour tester : $INSTALL_DIR/run.sh --windowed"
