# Stream Deck : Linux (Raspberry Pi), Windows et Android

Boutons carrés qui lancent des scripts via `http://SERVEUR:8765/NOMDUSCRIPT?token=TOKEN`.
Une seule interface (Blazor), trois plateformes.

```
StreamDeck.Shared/    Interface, modèles, services, CSS, Font Awesome (code commun)
StreamDeck.Desktop/   Hôte Linux / Windows (PhotinoX)  ->  exécutable + install.sh (Raspberry Pi)
StreamDeck.Android/   Hôte Android (.NET MAUI Blazor Hybrid)  ->  APK
PcServer/             Serveur de scripts du PC Windows (server.ps1 v2) + fonctions Windows
```

Chaque appareil garde **sa propre config** (PC, tokens, boutons). Pour copier une config d'un appareil à l'autre :
**Réglages > Copier la configuration** (copier-coller, voir plus bas).

**Première chose à faire sur le PC : installer le nouveau serveur** (section « Serveur du PC » ci-dessous).
Sans lui, les scripts marchent comme avant, mais les actions Windows (volume, média, éteindre…) ne répondent pas.

Compiler chaque projet séparément (`dotnet build StreamDeck.Desktop`, `dotnet build StreamDeck.Android`) :
le projet Android demande le workload MAUI, pas le projet Desktop.

---

# A. Raspberry Pi / Ubuntu

Voir la section « Desktop » plus bas. En bref, depuis le dossier de la solution :

```powershell
dotnet publish StreamDeck.Desktop -c Release -r linux-arm64 --self-contained -o publish-pi
scp -r publish-pi USER@IP_DU_PI:~/streamdeck-publish
```

Sur le Pi : `cd ~/streamdeck-publish && bash install.sh`

(`linux-x64` à la place de `linux-arm64` si `uname -m` affiche `x86_64`.)

---

# B. Android (tablette / téléphone)

## 1. Installer les outils (une seule fois, sur le PC Windows)

Le plus simple : **Visual Studio** (2022 17.12+ ou 2026), Installer > Modifier > cocher
**« Développement d'applications multiplateformes .NET (MAUI) »**. Cela installe aussi le JDK et le SDK Android.

Puis, dans un PowerShell **administrateur** :

```powershell
dotnet workload install maui-android
```

## 2. Construire l'APK

```powershell
cd StreamDeck.Android
dotnet publish -f net10.0-android -c Release
```

L'APK est créé dans `bin\Release\net10.0-android\` (cherche un fichier `*-Signed.apk`, par exemple
`bin\Release\net10.0-android\android-arm64\publish\com.streamdeck.tablet-Signed.apk`).
Première compilation : plusieurs minutes.

Si le SDK Android manque (erreur `XA5300`), installe-le en ligne de commande :

```powershell
dotnet build -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=C:\android-sdk" "-p:AcceptAndroidSDKLicenses=True"
```

## 3. Installer sur l'appareil

- **Par câble** : active « Débogage USB » sur l'appareil, puis `adb install chemin\vers\l-apk.apk`.
- **Sans câble** : copie l'APK sur l'appareil (USB, Drive, mail), ouvre-le, et autorise
  « Installer des applications inconnues » pour l'application utilisée (Fichiers, Chrome…).

## 4. Configurer

L'appareil doit être sur le **même Wi-Fi** que le serveur de scripts.
Ouvre Stream Deck, engrenage en haut à droite : adresse `http://192.168.1.100:8765`, token, « Tester la connexion », boutons, Enregistrer.

## 5. Lancement automatique au démarrage de l'appareil

Réglages Android > Applications > Stream Deck :

1. **Afficher par-dessus d'autres applications** : autoriser (obligatoire sur Android 10 et +).
2. **Batterie** : « Sans restriction » (ou « Ne pas optimiser »).
3. Sur Xiaomi / Huawei / Samsung / OPPO : activer aussi « Démarrage automatique » s'il existe.
4. Redémarre l'appareil pour tester.

Limites : Android restreint fortement les démarrages automatiques, surtout sur les téléphones.
Si ça ne se lance pas, la solution la plus fiable est le mode « appareil dédié » :
Réglages > Sécurité > **Épinglage d'écran** (ou « Épingler l'application ») : l'app reste verrouillée à l'écran.

## Comportement Android

- Plein écran (barres système masquées, elles réapparaissent en glissant depuis le bord).
- L'écran reste allumé tant que l'app est au premier plan.
- La connexion http:// vers le réseau local est autorisée (`usesCleartextTraffic`).
- Pas de bouton « Quitter » : utilise le geste ou le bouton système.
- Sur téléphone en portrait, choisis 2 ou 3 boutons par ligne (et 3 ou 4 lignes) dans les réglages de l'espace de travail.

## Commande vocale (écoute continue)

⚠️ **Écrite sans pouvoir être testée** : aucun appareil ni émulateur Android n'était disponible pendant le
développement. La logique de correspondance (mot de réveil, phrases, fenêtre d'écoute) est testée à fond côté
serveur et confirmée fiable ; le service Android qui écoute vraiment le micro en continu, lui, n'a jamais tourné
une seule fois avant d'arriver entre tes mains. Attends-toi à devoir ajuster quelque chose au premier essai.

Réglages → Commande vocale : active l'écoute (demande la permission micro), choisis un mot de réveil (ex.
« Cortana »), règle la durée d'écoute d'une commande après ce mot. Sur chaque bouton, dans son éditeur, ajoute une
ou plusieurs phrases qui le déclenchent ; « nécessite le mot de réveil » (activé par défaut) décide si la phrase
doit suivre le mot de réveil ou si elle marche à tout moment, dite seule.

Deux façons de le dire : « Cortana, lance Spotify » en une seule phrase, ou « Cortana » puis, juste après,
« lance Spotify ».

Une fois activée, une notification permanente reste affichée tant que l'écoute tourne — Android l'exige pour
qu'une app puisse garder le micro ouvert hors du premier plan, impossible à enlever sans arrêter l'écoute.
Pour que ça tienne dans la durée, désactive l'optimisation de batterie pour l'app dans les réglages Android
(Batterie → Stream Deck → Sans restriction, ou équivalent selon le fabricant) : sans ça, le système peut arrêter
le service au bout d'un moment.

## Commande vocale sur un choix précis (bouton « choix multiple »)

Un bouton « choix multiple » lui-même ne peut qu'ouvrir son popup à la voix (il faut ensuite toucher un choix) —
mais chaque **choix** à l'intérieur peut désormais avoir ses propres phrases, pour le déclencher directement, sans
passer par le popup. Dans l'éditeur du bouton, ouvre un choix : la même section « Commande vocale » que sur un
bouton normal y est, avec son propre réglage « nécessite le mot de réveil » indépendant.

## Variables et commande vocale sur un choix (« choix multiple »)

Corrigé : la section Variables et le bouton « ajouter un trigger vocal » existaient sur un bouton normal, mais pas
encore sur un choix précis d'un bouton « choix multiple » (dans son propre éditeur). Ils y sont maintenant, avec
exactement les mêmes réglages (nombre borné, texte, réglée ici / au lancement, avance automatique).

Dans l'éditeur d'un choix, si son nom n'a jamais été personnalisé (encore vide, ou resté à « Option »), il reprend
automatiquement le nom du script tapé — plus besoin de le retaper à chaque fois.

## Enregistrer une phrase déclencheuse à la voix

En plus de taper une phrase déclencheuse, un bouton <i class="fa-solid fa-microphone-lines"></i> « Ajouter un
trigger vocal » (sur un bouton normal comme sur un choix) écoute une phrase courte et l'ajoute telle que la
reconnaissance vocale l'a vraiment entendue — plus fidèle que de la deviner en la tapant, puisque c'est exactement
ce texte-là qui devra correspondre plus tard pendant l'écoute continue.

⚠️ Comme le reste de la commande vocale, cette fonctionnalité n'a jamais pu être testée sur un appareil réel de mon
côté (aucun Android disponible pendant le développement) — à vérifier au premier essai.

## Deux correctifs sur l'enregistrement de trigger vocal

**L'enregistrement s'arrêtait aussitôt démarré si l'écoute continue tournait en fond** — les deux se disputaient le
micro (Android ne permet qu'une reconnaissance vocale active à la fois). Corrigé : démarrer un enregistrement met
maintenant l'écoute continue en pause le temps de la phrase, puis la relance ensuite automatiquement — seulement si
elle était vraiment active avant.

**Mise en page téléphone** des sections Variables et Commande vocale, dans l'éditeur d'un bouton comme d'un choix :
le bouton d'enregistrement a un texte plus court (l'explication complète reste en info-bulle), et les cartes de
variable s'empilent proprement en colonne sur un écran étroit au lieu de se tasser sur une seule ligne.

## Mise en page téléphone (portrait) revue

Rien ne change dans le fonctionnement (identique en mode paysage/tablette) — seulement l'affichage sur un
téléphone étroit en portrait :

- **Page d'accueil** : le texte du statut serveur ("Serveur joignable"...) et le nom de chaque espace de travail
  se réduisent à leur icône seule (le nom complet reste accessible par un appui long) — ça laissait trop peu de
  place aux onglets d'espaces de travail, qui se retrouvaient coupés.
- **Éditeur d'un bouton ou d'un choix** : l'icône passe au-dessus du contenu plutôt qu'à côté, pour ne plus laisser
  une grande zone vide une fois la commande vocale ou les variables dépliées (leur contenu peut devenir plus haut
  que l'icône). La poignée de glisser-déposer (réordonner les boutons) suit la même logique.
- La carte « Nécessite le mot de réveil » est un peu plus compacte.

## Affichage plein écran (image, heure, date, jour)

Nouveau type de bouton : **Affichage plein écran**. Il n'exécute rien sur un script — il ouvre une fenêtre qui prend
tout l'écran, avec une croix en haut à droite pour la fermer (et Échap, ou un clic en dehors du contenu).

- **Ce qu'il affiche** (réglage « Affiche ») :
  - **Heure** : horloge, mise à jour chaque seconde.
  - **Date** : par exemple « Vendredi 25 septembre 2026 ».
  - **Jour de la semaine** : par exemple « Vendredi ».
  - **Image** : un lien (`https://…`), une image importée depuis l'appareil (700 Ko max), ou — pour l'affichage sur
    le PC — un chemin de fichier déjà présent sur le PC (ex : `C:\Photos\vacances.jpg`).
- **Où l'afficher** (réglage « Où ») :
  - **Sur l'appli** : sur l'appareil qui a appuyé (ou qui a entendu la commande vocale) — téléphone, tablette, ou PC
    via StreamDeck.Desktop.
  - **Sur le PC** : ouvre la fenêtre plein écran directement sur l'écran du PC piloté (nouvelle fonction du serveur,
    voir plus bas) — pratique pour déclencher ça depuis le téléphone mais l'afficher sur un écran de salon relié au PC.
  - **Les deux** : les deux en même temps.
- **Durée d'affichage** : en secondes, ou **0 = infini** (fermeture manuelle seulement, avec la croix).
- **Commande vocale** : exactement comme n'importe quel autre bouton — une phrase déclencheuse dans la section
  « Commande vocale » de son éditeur (avec ou sans mot de réveil, au choix). Utile même si le téléphone est dans la
  poche : « Cortana, montre l'heure » peut afficher l'horloge en plein écran sans toucher l'appareil.

**Limite à connaître** : une image *importée* (celle avec le bouton « Importer », convertie en base64) ne peut être
affichée que « Sur l'appli » — elle est bien trop volumineuse pour tenir dans l'adresse envoyée au PC. Pour afficher
une image sur le PC, utilise un lien ou un chemin de fichier déjà présent dessus.

### Sur le PC : nouvelle fonction du serveur (`_/info`)

`server.ps1` sait maintenant ouvrir lui-même une fenêtre plein écran (fond noir, sans bordure, toujours au premier
plan) via une petite fenêtre WPF lancée dans son propre PowerShell caché — comme un script normal, elle tourne à
côté du serveur sans le bloquer. Installe la dernière version de `server.ps1` pour que le réglage « Sur le PC » /
« Les deux » fonctionne (sinon : « Affichage plein écran inconnu du serveur »).

**Idées d'autres affichages, si l'envie t'en vient** : la météo du jour, le prochain épisode/évènement d'un calendrier,
un compte à rebours, le morceau en cours de lecture (titre/artiste), une citation ou un mème du jour, ou une simple
page web affichée plein écran (utile pour un tableau de bord déjà existant, sans rien recoder ici).

## Historique de la commande vocale







Le panneau d'historique (l'icône <i class="fa-solid fa-clock-rotate-left"></i>) a maintenant deux onglets sur
Android : **Actions** (comme avant, chaque appui de bouton) et **Voix** — chaque phrase entendue, qu'elle ait
déclenché un bouton ou non. Une phrase reconnue montre le bouton qu'elle a lancé ; une phrase sans rapport ne
montre qu'un repère neutre (rien n'est arrivé, ce n'est pas une erreur). Un bouton déclenché à la voix apparaît
aussi dans l'onglet Actions, comme un appui normal — c'était un oubli avant cette mise à jour, corrigé au passage :
les commandes vocales ne remontaient nulle part dans l'historique des actions.

## Dépannage Android

| Symptôme | Piste |
|---|---|
| Boutons : « Serveur injoignable » | Même Wi-Fi ? IP du serveur correcte ? Pare-feu du serveur ouvert sur le port 8765 ? |
| Pastille rouge alors que le serveur tourne | Vérifie l'adresse dans Réglages ; teste `http://IP:8765` depuis le navigateur du téléphone |
| Écran vide au lancement | Installe l'APK Debug depuis Visual Studio (F5 sur l'appareil) : la console Blazor affiche l'erreur |
| Message orange « envoyé, réponse interrompue » | Normal : la requête est partie mais le serveur a fermé la connexion sans réponse HTTP propre (souvent à la fin du script) |
| Ne démarre pas au boot | Étapes 1 à 3 de la section 5, sinon épinglage d'écran |
| `NU1605` (downgrade de paquet) à la compilation | Aligne la version de `Microsoft.AspNetCore.Components.Web` dans StreamDeck.Shared.csproj sur celle de MAUI |
| Commande vocale : l'écoute s'arrête toute seule | Désactive l'optimisation de batterie pour l'app (voir ci-dessus) ; certains fabricants (Xiaomi, Huawei...) sont plus agressifs que d'autres |
| Commande vocale : rien ne se déclenche | Vérifie que le micro est bien accordé (réglages Android de l'app) ; certains appareils ont besoin d'une connexion réseau pour la reconnaissance vocale |
| Affichage plein écran « Sur le PC » : rien ne s'ouvre | Installe la dernière version de `server.ps1` (voir section dédiée) ; vérifie les logs du serveur dans une fenêtre PowerShell ouverte au premier plan pour voir l'erreur exacte |

---

# Desktop (Linux / Windows)

## Prérequis Ubuntu

```bash
sudo apt install libwebkit2gtk-4.1-0 libnotify4
```

## Tester (fenêtre normale)

```bash
dotnet run --project StreamDeck.Desktop -- --windowed
```

Diagnostic : `dotnet run --project StreamDeck.Desktop -- --windowed --debug` (F12 = outils de développement, logs détaillés).
Sous Windows, WebView2 doit être installé (présent par défaut sur Windows 10/11 à jour).

## Publier pour Ubuntu

`uname -m` : `x86_64` → `linux-x64`, `aarch64` → `linux-arm64` (Raspberry Pi).

```bash
dotnet publish StreamDeck.Desktop -c Release -r linux-arm64 --self-contained -o publish
scp -r publish/ user@IP:~/streamdeck-publish
ssh user@IP
cd ~/streamdeck-publish && bash install.sh
```

`install.sh` copie l'app dans `~/.local/share/streamdeck`, l'ajoute au menu et crée l'entrée d'autostart.
Pour un appareil dédié, active la **connexion automatique** dans les paramètres Ubuntu.

## Utilisation

- Lancer : menu « Stream Deck » ou `~/.local/share/streamdeck/run.sh`
- Quitter : Réglages → Quitter l'application, ou `Alt+F4`

## Désinstaller

```bash
rm -rf ~/.local/share/streamdeck ~/.local/share/applications/streamdeck.desktop \
       ~/.config/autostart/streamdeck.desktop
```

## Dépannage

| Symptôme | Piste |
|---|---|
| Écran vide, ou « Chargement » qui reste affiché | Blazor n'a pas démarré : `--windowed --debug`, F12 → Console |
| Erreur de bibliothèque au lancement (Linux) | `ldd ~/.local/share/streamdeck/PhotinoX.Native.so \| grep "not found"` |
| Fenêtre blanche ou noire (Raspberry Pi, certains GPU) | Décommenter une ligne `WEBKIT_DISABLE_…` dans `run.sh` |
| `bad interpreter` avec install.sh | `sed -i 's/\r$//' install.sh` |
| Pastille « Serveur injoignable » | Mauvaise adresse/port, ou serveur de scripts éteint (bouton « Tester la connexion » dans Réglages) |
| Token refusé / Script introuvable | Message affiché en bas de l'écran après l'appui |
| Icône « ? » sur un bouton | Icône non Font Awesome dans l'ancienne config : rechoisir l'icône dans Réglages |

---

# Espaces de travail et pages

Les boutons sont organisés en **espaces de travail** (Jeux, Travail, Dev…), chacun ayant plusieurs **pages**.

- **Écran principal** : les espaces s'affichent en onglets dans la barre du haut (visibles dès qu'il y en a deux ou plus).
  Les pages se changent avec les flèches et les points en bas, ou **en glissant le doigt** à gauche / à droite.
  Le dernier espace utilisé est retrouvé au prochain lancement.
- **Réglages > Espaces de travail** : ajouter, renommer, changer l'icône, réordonner, supprimer un espace,
  et choisir **le PC visé** par cet espace (s'il y a plusieurs PC).
- **Réglages > Boutons** : onglets de pages (avec le remplissage, ex. 5/8), ajout / réordonnancement / suppression de pages,
  nom de page facultatif. Chaque bouton peut être déplacé vers la page précédente ou suivante.
- **Chaque page a sa propre grille** : boutons par ligne, lignes, **espacement** (serré / normal / large) et
  **taille des icônes** (petite / normale / grande). Une page peut donc avoir 6 x 3 petits boutons et la suivante 2 x 1 gros boutons.
- **Boutons larges ou hauts** : chaque bouton a une largeur et une hauteur en cases (ex. 2 x 1). Il n'est agrandi que
  s'il reste assez de place sur la page.
- Une page pleine n'accepte plus de bouton. Si tu réduis la grille, les boutons en trop passent automatiquement
  sur la page suivante (créée au besoin) : rien n'est perdu.
- La suppression d'un espace ou d'une page non vide demande confirmation.
- **Ancienne config** : elle est migrée automatiquement (un espace « Principal », un PC « Mon PC » avec ton adresse et ton token,
  la grille de chaque espace recopiée sur ses pages), sans rien perdre.

---

# Réveiller un PC (Wake-on-LAN)

Un bouton de type **« Réveiller un PC »** envoie un « magic packet » (UDP, broadcast) à la carte réseau du PC.
Il fonctionne depuis n'importe quel appareil (Raspberry Pi, PC, Android) **connecté au même réseau local** que le PC
(pas en 4G/5G, pas depuis l'extérieur sans VPN).

## Dans l'application

Renseigne l'**adresse MAC** du PC dans **Réglages > PC pilotés** (`6C-02-E0-3E-35-7E` pour la carte Ethernet Realtek du
PC en 192.168.1.100) > Enregistrer. Un **petit bouton d'alimentation** apparaît alors dans la barre du haut, à côté de la
pastille « Serveur joignable » : il réveille le PC de l'espace affiché sans occuper de case.

Tu peux aussi créer un bouton de type **Réveiller un PC** dans une page (il utilise l'adresse du PC, ou la sienne si tu la saisis).

Après l'appui, la pastille en haut passe en orange (« Démarrage du PC ») et vérifie le serveur toutes les 3 secondes,
jusqu'à 3 minutes, puis passe au vert quand le PC répond.

Formats de MAC acceptés : `6C-02-E0-3E-35-7E`, `6C:02:E0:3E:35:7E`, `6c02.e03e.357e`, `6C02E03E357E`.

## Sur le PC (à faire une fois)

C'est la partie qui décide si ça marche. Le PC doit être **en Ethernet** (le Wi-Fi réveille rarement).

1. **BIOS / UEFI** (touche Suppr ou F2 au démarrage) : active l'option de réveil par le réseau. Selon la carte mère :
   *Wake on LAN*, *Power On By PCI-E*, *Resume by PCI-E/PCI*, *Wake Up Event Setup*.
   Désactive *ErP / EuP Ready* s'il existe (sinon le PC ne se réveille pas quand il est éteint).
2. **Windows** : Gestionnaire de périphériques > Cartes réseau > **Realtek Gaming GbE Family Controller** > Propriétés.
   - Onglet **Avancé** : *Wake on Magic Packet* = **Activé** ; *Shutdown Wake-On-Lan* = **Activé** (si présent) ;
     *Green Ethernet* et *Energy Efficient Ethernet* = **Désactivé**.
   - Onglet **Gestion de l'alimentation** : coche « Autoriser ce périphérique à sortir l'ordinateur du mode veille »
     et « Autoriser uniquement un paquet Magic Packet à sortir l'ordinateur du mode veille ».
   - Ce sont bien les réglages de la carte **Realtek**, pas des cartes VirtualBox / VMware de la liste.
3. **Démarrage rapide** : Panneau de configuration > Options d'alimentation > « Choisir l'action des boutons d'alimentation »
   > « Modifier des paramètres actuellement non disponibles » > **décoche « Activer le démarrage rapide »**.
   (Sinon le réveil depuis l'arrêt complet échoue souvent, alors que depuis la veille il marche.)
4. Sur ta box : réserve l'adresse **192.168.1.100** à ce PC (réservation DHCP), pour que l'adresse du serveur ne change pas.

## Test

Éteins le PC (ou mets-le en veille), attends 30 secondes, puis appuie sur le bouton depuis un autre appareil du même réseau.
Le PC doit démarrer en quelques secondes. Si rien ne se passe, teste d'abord la **veille** : c'est plus tolérant que l'arrêt complet.

## Dépannage Wake-on-LAN

| Symptôme | Piste |
|---|---|
| « Adresse MAC invalide » | 12 caractères hexadécimaux exactement (0-9, A-F) |
| « Paquet envoyé » mais le PC ne démarre pas | Étapes 1 à 3 ci-dessus (BIOS, carte Realtek, démarrage rapide) |
| Marche depuis la veille, pas depuis l'arrêt | Démarrage rapide encore actif, ou ErP activé dans le BIOS |
| Ne marche que depuis certains appareils | L'appareil est sur un autre réseau (Wi-Fi invité, 4G) ou le routeur filtre le broadcast |
| Le PC est éteint au courant / multiprise coupée | Rien ne peut le réveiller : la carte réseau doit rester alimentée |

---

# Serveur du PC (server.ps1 v2)

Le dossier `PcServer/` contient les fichiers à copier **dans le dossier de ton serveur NFC** (celui qui contient déjà
`server.ps1`, `WindowUtils.ps1` et le dossier `Script`).

| Fichier | Action |
|---|---|
| `server.ps1` | remplace l'ancien |
| `NfcNative.ps1` | **nouveau** (volume, touches média, veille, extinction) |
| `WindowUtils.ps1` | remplace l'ancien (ajoute `Open-BrowserWindow` et les fonctions par poignée de fenêtre) |
| `Script/ceres.ps1`, `Script/SerieAtm.ps1`, `Script/Nexa.ps1` | remplacent les anciens |

`SpotifyHelper.ps1`, `7days.ps1`, `timetoshine.ps1` et `config.txt` ne changent pas.

**Mise à jour** : ferme l'ancien serveur, copie les fichiers, relance `server.ps1`. Le port et le `SECRET` de `config.txt`
sont conservés. Le serveur **refuse désormais de démarrer si `SECRET` est vide**.

## Ce qui change

- **Le serveur ne se bloque plus.** Avant, il exécutait le script avant de répondre : pendant ce temps, plus aucun bouton ne passait
  et l'application affichait une erreur au bout de quelques secondes. Maintenant chaque script est lancé dans son propre PowerShell
  caché et le serveur répond immédiatement (`{"ok":true,"job":"…"}`). La sortie de chaque script est enregistrée dans `logs\`
  (un fichier par lancement) : c'est là qu'il faut regarder si un script ne fait pas ce qu'on attend.
- **Un script en erreur n'est plus signalé par l'application** (elle sait seulement qu'il a été lancé). Consulte `logs\`.
  Dans un enchaînement, l'étape « Attendre la fin du script » suit le script jusqu'au bout.
- **Le token est comparé en temps constant**, et les noms de script sont limités aux lettres, chiffres, espaces, `_` et `-`
  (sous-dossiers avec `/`) : impossible de sortir du dossier `Script`.
- Option `HOST=` dans `config.txt` pour n'écouter que sur une adresse (par défaut toutes : `+`).
- Si le démarrage échoue (port réservé), le message indique la commande `netsh http add urlacl …` à lancer.
- **Planification automatique** : le serveur peut désormais déclencher un script tout seul (toutes les X minutes, ou
  à des heures fixes, avec répétition possible), sans passer par le Stream Deck — réglé depuis Stream Deck Script
  Builder, voir « Planification » dans `StreamDeck.ScriptBuilder/README.md`. Les règles vivent dans `Schedules.json`
  (nouveau, créé automatiquement à côté de `server.ps1`) ; le serveur les revérifie environ toutes les 15 secondes,
  entre deux requêtes HTTP, sans jamais bloquer les appels normaux.
- **Le serveur se relance désormais toujours caché** (`-WindowStyle Hidden`), et un fichier `StartServerHidden.vbs`
  (créé automatiquement à côté de `server.ps1`) permet de le lancer, invisible, dès l'ouverture de session Windows —
  voir « Le serveur ne s'affiche plus comme une application » dans `StreamDeck.ScriptBuilder/README.md`.
- **Raccourcis clavier globaux et widget de bureau** (Stream Deck Script Builder, Réglages) : déclencher un script,
  arrêter toutes les macros en cours, ou redémarrer le serveur avec une combinaison de touches ; et une petite
  fenêtre séparée (widget) avec l'heure et un accès rapide au serveur. Voir `StreamDeck.ScriptBuilder/README.md`.
- **Widget sans bordure, personnalisable** : plus de barre de titre Windows — glisser/réduire/fermer se font depuis
  le widget lui-même. Styles (sombre/clair/verre/accent/personnalisé), police, coins arrondis, touches de scripts
  épinglées (mêmes tuiles que l'app Stream Deck), et un bouton pour rouvrir le constructeur depuis le widget. Sans
  bordure, glisser, réduire, toujours au premier plan et coins arrondis utilisent du vrai code natif Windows
  (WinUI/DWM). Voir « Widget de bureau » dans `StreamDeck.ScriptBuilder/README.md`.

## Chrome et les fenêtres

`Wait-ForProcessWindow` cherche un **nouveau processus** ; si Chrome tournait déjà, il ne trouvait rien et la fenêtre n'était
pas déplacée (la page s'ouvrait là où Chrome était). `Open-BrowserWindow -Url … -ScreenIndex 0 -Maximize` ouvre une **nouvelle
fenêtre** même si Chrome est déjà ouvert, repère cette fenêtre-là et la place sur l'écran demandé (`-1` = écran de droite).
`ceres.ps1` et `SerieAtm.ps1` l'utilisent. `Nexa.ps1` et `SerieAtm.ps1` appelaient des fonctions de `WindowUtils.ps1` sans le charger :
la ligne manquante est ajoutée.

## Adresses du serveur

Toutes demandent `?token=…`.

| Adresse | Rôle |
|---|---|
| `/NOMDUSCRIPT` | lance `Script\NOMDUSCRIPT.ps1` (comme avant) |
| `/_/ping` | test de connexion et de token |
| `/_/audio` (`level=0..100`, `delta=±N`, `mute=on\|off\|toggle`) | volume ; renvoie `{"level":…,"muted":…}` |
| `/_/media?cmd=playpause\|next\|prev\|stop` | touches multimédia (lecteur actif : Spotify, navigateur, VLC…) |
| `/_/power?cmd=shutdown\|restart\|sleep\|hibernate\|lock\|monitoroff\|abort` (`delay=` secondes) | alimentation |
| `/_/desktop?cmd=show\|taskview` | afficher le bureau, vue des tâches |
| `/_/job?id=…` | état d'un script lancé |

Extinction et redémarrage passent par `shutdown.exe` avec un délai de 5 secondes (le temps de répondre) : l'action
« Annuler l'arrêt » les annule. Le serveur doit tourner dans **ta session Windows ouverte** (pas comme service) pour que les
touches média et le volume agissent.

## Démarrage en arrière-plan (sans fenêtre visible)

Si le raccourci dans le dossier Démarrage pointe directement sur `server.ps1`, Windows ouvre une console PowerShell
visible sur le bureau à chaque connexion. Deux façons de le lancer sans fenêtre :

**Option simple — `RunHidden.vbs`** (fourni dans `PcServer/`) :
1. Copie `RunHidden.vbs` à côté de `server.ps1` (même dossier).
2. Touche Windows + R, tape `shell:startup`, Entrée : ça ouvre le dossier Démarrage.
3. Fais un raccourci vers `RunHidden.vbs` dans ce dossier (clic droit sur `RunHidden.vbs` → Envoyer vers → Bureau
   (créer un raccourci), puis déplace ce raccourci dans le dossier Démarrage — ou glisse-dépose directement le
   `.vbs` dans le dossier Démarrage en maintenant Alt, ce qui crée un raccourci).
4. Ferme la fenêtre PowerShell existante si un ancien raccourci la relançait visiblement, puis reconnecte-toi (ou
   double-clique le raccourci) pour tester : aucune fenêtre ne doit apparaître, mais « Tester la connexion »
   (Réglages → PC) doit fonctionner.

**Option plus robuste — tâche planifiée** (survit mieux aux mises à jour Windows, redémarre le serveur tout seul
s'il plante) : exécute une fois `PcServer/Install-StartupTask.ps1` (clic droit → Exécuter avec PowerShell). Elle
enregistre une tâche « StreamDeck-Server » lancée à l'ouverture de session, sans fenêtre. Supprime alors l'ancien
raccourci du dossier Démarrage pour éviter de lancer le serveur deux fois.

Dans les deux cas, si tu avais un ancien raccourci visible dans le dossier Démarrage, supprime-le (ou remplace-le)
pour ne pas avoir deux serveurs qui tournent en même temps sur le même port.

---

# Actions Windows, volume et média

Types de bouton dans **Réglages > Boutons** :

- **Action Windows** : lecture / pause, piste suivante / précédente, stop, volume + / -, muet, afficher le bureau,
  vue des tâches, éteindre l'écran, verrouiller, veille, hibernation, redémarrer, éteindre le PC, annuler l'arrêt.
  Le nom, l'icône et la couleur sont préremplis (tu peux les changer).
- **Curseur de volume** : une barre (à faire large, 2 cases ou plus) avec bouton muet et pourcentage. Elle relit le volume du PC
  au démarrage et toutes les 15 secondes.
- **Contrôles média** : précédent / lecture-pause / suivant sur une seule tuile (3 cases de large conseillées).

Ces deux dernières tuiles ne se « pressent » pas : elles agissent directement et ne signalent que les problèmes.

## Confirmation et code PIN

Chaque bouton (sauf les deux tuiles ci-dessus) peut demander **aucune confirmation**, une **confirmation** (Annuler / Confirmer)
ou le **code PIN**. « Éteindre le PC » et « Redémarrer » sont réglés sur le PIN par défaut ; veille et hibernation sur la confirmation.

**Réglages > Sécurité** : définir, modifier ou supprimer le code (4 à 8 chiffres ; l'ancien code est demandé pour le changer),
et **verrouiller les réglages** par ce code. Après 5 erreurs de suite, la saisie est bloquée 30 secondes.
Si un bouton demande le PIN alors qu'aucun code n'est défini, une simple confirmation est demandée.

Le code est stocké haché (PBKDF2), mais c'est un **verrou d'usage**, pas une protection contre quelqu'un qui aurait accès au
fichier de configuration de l'appareil (un code à 4 chiffres se retrouve vite à partir du fichier).

# Enchaînements

Un bouton **Enchaînement** exécute des étapes dans l'ordre : **Réveil**, **Attendre que le PC réponde** (délai maximum réglable),
**Script** (avec l'option « attendre la fin »), **Action Windows**, **Pause**. Exemple : Réveil > Attendre le PC > script `ceres` > lecture.

Le message affiché suit l'étape en cours (« étape 2/4 »). L'enchaînement **s'arrête à la première étape en échec** et le dit.
Un appui sur la tuile pendant qu'il tourne l'**interrompt** (elle affiche « Annuler »).

# Plusieurs PC

**Réglages > PC pilotés** : un profil par PC (nom, adresse du serveur, token, adresse MAC), avec un bouton **Tester la connexion**
(port ouvert, puis token accepté) et **Envoyer le réveil**. Un PC est « par défaut ».
Chaque **espace** peut viser un PC, et chaque **bouton** peut viser un autre PC que celui de son espace.
La pastille de la barre indique l'état du PC de l'espace affiché (avec son nom quand il y en a plusieurs).

# Confort, historique, copie de configuration

- **Historique** (icône horloge dans la barre) : les 100 derniers appuis avec leur résultat. Effacé à la fermeture de l'application.
- **Réglages > Confort** : assombrissement après 1 min à 2 h d'inactivité (écran noir avec l'heure ; un toucher le réveille
  sans rien déclencher), et vibration au toucher (Android).
- **Réglages > Copier la configuration** : **Générer le texte** puis **Copier**, à coller sur l'autre appareil dans **Importer**.
  Les tokens ne sont inclus que si tu le demandes ; le code PIN n'est jamais exporté. Deux modes : **Ajouter** (les espaces
  importés s'ajoutent, les PC déjà connus ne sont pas dupliqués) ou **Tout remplacer** (tes tokens et ton PIN locaux sont conservés).
  Rien n'est appliqué tant que tu n'as pas cliqué sur **Enregistrer**.

---

# Organisation du code

Un composant = un fichier CSS, classes préfixées par le nom du composant. Tout est dans `StreamDeck.Shared` :

| Composant (`Components/…`) | CSS (`wwwroot/css/…`) |
|---|---|
| `Layout/MainLayout.razor` | `MainLayout.css` |
| `Pages/Home.razor` | `Home.css` |
| `Pages/Settings.razor` | `Settings.css` |
| `Shared/KeyTile.razor` | `KeyTile.css` |
| `Shared/Modal.razor` | `Modal.css` |
| `Shared/IconPicker.razor` | `IconPicker.css` |
| `Shared/NumberStepper.razor` | `NumberStepper.css` |
| `Shared/ChoiceField.razor` | `ChoiceField.css` |
| `Shared/ChoicePicker.razor` | `ChoicePicker.css` |
| `Shared/ToggleSwitch.razor` | `ToggleSwitch.css` |
| `Shared/PinPad.razor` | `PinPad.css` |
| `Shared/ConfirmDialog.razor` | `ConfirmDialog.css` |
| `Shared/HistoryPanel.razor` | `HistoryPanel.css` |
| `Shared/VolumeTile.razor` | `VolumeTile.css` |
| `Shared/MediaTile.razor` | `MediaTile.css` |
| `Shared/IdleGuard.razor` | `IdleGuard.css` (l'écran noir est créé par `wwwroot/deck.js`) |

Les sections de l'écran Réglages sont dans `Components/Editor/` (`PcSection`, `WorkspaceSection`, `PagesSection`, `ButtonEditor`,
`StepEditor`, `SecuritySection`, `ComfortSection`, `BackupSection`) : elles partagent un `EditorState` et réutilisent les
classes `settings-*` de `Settings.css`.

`wwwroot/app.css` : variables (couleurs, rayons) et base. Chaque hôte a son `wwwroot/index.html` qui référence ces CSS
(Desktop : `css/…`, copiés à côté de l'exécutable au build ; Android : `_content/StreamDeck.Shared/css/…`).

Ce qui diffère par plateforme passe par `IAppPlatform` (dossier de config, bouton Quitter, vibration). Les hôtes enregistrent
leurs services avec `services.AddStreamDeckCore()`.

## Script Builder (nouveau)

`StreamDeck.ScriptBuilder` + `StreamDeck.ScriptBuilder.Windows` : application Windows qui génère les scripts `.ps1`
du serveur depuis une interface visuelle (choisir un .exe, enchaîner « ouvrir » puis « plein écran », etc.).
Voir `StreamDeck.ScriptBuilder/README.md`.

## Volume : moteurs et diagnostic

`NfcNative.ps1` essaie trois moteurs, dans l'ordre : API audio de Windows (COM), variante « classique » de la même API,
puis les touches volume du clavier. Si les deux premiers échouent, le volume reste piloté (+5 / -5 / muet / niveau)
mais le niveau affiché est une **estimation** (« ~40 % »). « Tester la connexion » dans les réglages indique quel
moteur est utilisé et, en cas de repli, la raison exacte de l'échec COM.

## Script désactivé (Stream Deck Script Builder)

`server.ps1` consulte `Script\.sdb-disabled.txt` (un nom de script par ligne) avant de lancer quoi que ce soit : un
script y figurant est refusé (code 423), même appelé directement par son nom. Ce fichier est géré depuis Stream Deck
Script Builder (bouton activer/désactiver sur chaque script ou dossier) — inutile d'y toucher à la main.

## Routage des scripts : filet de sécurité

`server.ps1` cherche d'abord `Script\<nom>.ps1` directement. S'il n'existe pas et que le nom ne contient pas de « / »,
il cherche aussi dans les sous-dossiers de `Script` (premier trouvé). Ça permet d'appeler un script par son seul nom
même s'il est rangé dans un sous-dossier — c'est ce que fait Stream Deck Script Builder, dont les dossiers ne sont
qu'un rangement dans l'application (voir son propre README).

## Popup « Choix multiple » : rester ouvert après une sélection

Le popup a maintenant un réglage <i class="fa-solid fa-thumbtack"></i> « Rester ouvert après une sélection »,
**activé par défaut** (dans la plupart des cas c'est ce qu'on veut, surtout pour plusieurs liens). Un choix se
lance, le popup reste ouvert pour en relancer un autre ; désactive le réglage si tu préfères qu'il se ferme après
chaque choix (l'ancien comportement). Le réglage est mémorisé le temps que l'application reste ouverte.

## Déplacer un bouton vers un autre espace de travail

Dans l'éditeur d'un bouton, si plusieurs espaces existent, un bouton <i class="fa-solid fa-right-left"></i> apparaît
à côté de « page précédente / suivante » : il ouvre la liste des autres espaces et déplace le bouton (avec son
contenu) dans celui choisi, sur sa première page qui a de la place (une nouvelle page est créée sinon). Si la grille
de destination est plus petite, la taille du bouton (ColSpan/RowSpan) est réduite au besoin.
Note : si le bouton n'a pas de PC propre (il utilise celui de son espace), le déplacer vers un espace lié à un autre
PC change quel PC il vise — souvent voulu, mais à garder en tête.

## Stream Deck : nom du bouton -> nom du script

Dans l'éditeur de bouton, si tu tapes le **nom affiché** et laisses **nom du script** vide, il se remplit tout seul avec
le nom affiché (pas besoin de taper « Photoshop » deux fois). Si tu modifies le nom du script à la main, il n'est plus
écrasé.