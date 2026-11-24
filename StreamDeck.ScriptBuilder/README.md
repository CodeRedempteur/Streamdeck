# Stream Deck Script Builder

Application Windows (Blazor Hybrid / MAUI) qui **génère les scripts PowerShell** de ton serveur NFC / Stream Deck
sans écrire de code : tu choisis un programme, tu enchaînes des actions (ouvrir, plein écran, volume…) et le `.ps1`
est créé dans le bon dossier.

## Lancer

Prérequis : SDK .NET 10 et la charge de travail MAUI (`dotnet workload install maui`).

```powershell
dotnet run --project StreamDeck.ScriptBuilder.Windows -f net10.0-windows10.0.19041.0
```

Ou ouvrir `StreamDeck.slnx` dans Visual Studio et lancer **StreamDeck.ScriptBuilder.Windows**.

Pour obtenir un `.exe` autonome :

```powershell
dotnet publish StreamDeck.ScriptBuilder.Windows -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained
```

## La bibliothèque

> **v1.34.0 — deux onglets sur la page principale.** L'en-tête affiche maintenant deux onglets : **Scripts** (la
> bibliothèque décrite ci-dessous, onglet par défaut) et **Widget** (configuration du widget de bureau, ex-Réglages —
> voir la section « Widget de bureau » plus bas). Les Réglages ne gardent plus que les raccourcis clavier et le
> dossier du serveur.
>
> **v1.34.1 — retour partiel dans les Réglages.** L'onglet **Widget** ne garde que la composition des pages (espaces
> de travail, pages, blocs) — l'apparence du widget et les boîtes mail sont **revenues dans les Réglages**, comme
> avant la v1.34.0 (visuel en grille de cartes jugé pas adapté à ces réglages-là). Voir « Widget de bureau » plus bas.
>
> **v1.34.2 — en-tête unifié sur les trois pages, widget ouvert au lancement.** Réglages a maintenant le même en-tête
> à onglets (Scripts / Widget / Réglages) que la page principale, plutôt qu'un en-tête différent avec une flèche
> « Retour ». Un bouton « Widget » apparaît désormais dans cet en-tête, sur les trois pages, pour ouvrir le widget (ou
> le ramener au premier plan) sans avoir à changer d'onglet. Le widget **s'ouvre automatiquement au lancement de
> l'application**, plutôt qu'à la demande seulement. L'onglet Widget lui-même perd son encadré façon « Réglages » au
> profit d'une présentation identique à celle de l'onglet Scripts (zones de travail en puces puis grille de cartes,
> sans bloc englobant).
>
> **v1.34.3 — en-tête commun, filtre/archivage du courrier, pages du widget en glisser-déposer.** Les trois pages
> (Scripts, Widget, Réglages) partagent maintenant exactement le même en-tête (même composant) : plus d'icône à côté
> du titre, une vraie navigation à onglets soulignés plutôt que trois boutons repris du sélecteur d'espace de travail,
> et le bouton pour ouvrir le widget est réduit à une icône seule, toujours au même endroit — comme les boutons
> « Arrêter les macros » / « Redémarrer le serveur », désormais aussi dans cet en-tête plutôt qu'ailleurs sur chaque
> page. Corrigé : sur Widget et Réglages, le nom de l'espace de travail, l'interrupteur « Actif sur le widget » et le
> bouton corbeille pouvaient retomber chacun sur leur propre ligne au lieu de rester alignés (l'interrupteur, large de
> 100 % par défaut, réclamait toute la ligne — voir « Corrections » plus bas). Dans le widget, un 3ᵉ bouton (icône
> filtre) apparaît à côté d'Actualiser/Nouveau message sur les pages de courrier : Aujourd'hui / 7 derniers jours / 30
> derniers jours / Favoris / Archivés — chaque message a maintenant aussi un bouton pour l'archiver (case archive),
> en plus de favori et supprimer. Les pages du widget se réorganisent maintenant par **glisser-déposer** (comme les
> écrans dans un espace de travail), y compris vers un autre espace de travail en le déposant sur son onglet ; les
> flèches restent disponibles en repli mais pointent maintenant **gauche/droite** plutôt que haut/bas (les pages sont
> rangées en grille, pas en liste verticale).
>
> **v1.34.4 — en-tête réellement identique, carte « Ajouter une page », popup de filtre corrigée.** Correctif : « Nouveau
> dossier »/« Nouveau script » vivaient encore dans l'en-tête commun sur la page Scripts seulement — l'en-tête n'était
> donc pas vraiment identique sur les trois pages malgré la v1.34.3. Ces deux boutons sont maintenant dans la barre
> d'outils de la page Scripts elle-même (à droite de « Trier »), et l'en-tête (MainHeader.razor) n'accepte plus aucune
> action propre à une page. Le bouton « Ajouter une page » du Widget, posé au hasard sous la grille, est remplacé par
> une carte en pointillés avec un « + », intégrée dans la même grille que les pages. Corrigé : le menu du filtre de
> courrier (v1.34.3) pouvait apparaître tronqué/caché quand la boîte n'avait rien à afficher (« rien aujourd'hui ») —
> toute la zone courrier (en-tête + liste) défilait comme un seul bloc, ce qui rognait le menu dès que ce bloc était
> court ; seule la liste des messages défile maintenant, l'en-tête et son menu restent toujours entièrement visibles,
> au premier plan par rapport au reste de la page.
>
> **v1.34.5 — bulles centrées, signature mail, pièces jointes, plusieurs destinataires, page de veille, météo, heure
> dans le monde.** Correctif : les bulles du widget (filtre de courrier, notifications, sélecteur d'espace de travail)
> pouvaient déborder du bord droit/bas de la fenêtre et provoquer une barre de défilement horizontale — elles sont
> maintenant **centrées** dans la fenêtre du widget plutôt qu'ancrées à côté du bouton qui les ouvre, avec un fond qui
> les referme au clic en dehors. Chaque boîte mail peut avoir une **signature** (logo + coordonnées, voir Réglages →
> boîte mail → section Signature), ajoutée à l'envoi via une case à cocher (activée par défaut si configurée). La
> composition d'un message peut envoyer à **plusieurs destinataires** (adresses séparées par une virgule) et **joindre
> des fichiers** (un par un, retirables) ; le champ « À » **autocomplète** avec les adresses déjà vues dans le courrier
> reçu. Limite assumée : un message avec pièce jointe ne peut pas être programmé pour l'instant (pièces jointes trop
> volumineuses pour `config.json`) — envoie-le tout de suite ou retire les pièces jointes ; coller une image
> directement dans le corps du message n'est pas encore fait (jointe en pièce jointe, en attendant).
>
> Nouveau : une **page de veille** (Réglages du widget) peut être désignée — le widget y bascule automatiquement (avec
> une transition réglable) s'il n'est pas survolé pendant un délai réglable, et revient à la normale dès que la souris
> revient dessus. Deux nouveaux blocs de page : **Météo** (recherche une région, affiche température/description/icône
> via Open-Meteo, gratuit et sans compte) et **Heure dans le monde** (choisit un fuseau horaire, affiche l'heure
> courante avec une icône de globe — pas encore un repère positionné précisément sur une carte, une évolution possible
> plus tard).
>
> **Non vérifié dans cet environnement** (pas d'accès réseau sortant vers ces services depuis ce bac à sable) : l'envoi
> SMTP avec signature/pièces jointes (MailKit/MimeKit, comme pour tout ce qui touche au courrier depuis le début) et le
> relevé météo (Open-Meteo) — écrits d'après leur documentation publique, à tester sur un vrai Windows connecté à
> Internet.
>
> **v1.34.6 — page de veille par page, recherche de fuseau par pays, météo/heure sur l'appli, corrections d'affichage,
> pause d'envoi, retrouver le téléphone, listes de tâches.** La **page de veille** n'est plus une seule page désignée
> globalement : chaque page choisit maintenant elle-même (dans son éditeur) si elle **participe à la veille** et si
> elle doit **n'apparaître QU'en veille** (masquée du carrousel normal) — plusieurs pages peuvent y participer à la
> fois, elles alternent alors automatiquement (durée réglable, Réglages du widget → « Alternance si plusieurs pages »).
> La recherche de fuseau horaire (widget et appli) a maintenant une **barre de recherche par pays**, en plus du nom de
> ville/région déjà cherchable — best-effort (table de correspondance ville → pays d'une soixantaine d'entrées, pas
> exhaustive : un fuseau sans mot-clé reconnu reste cherchable par ville, juste pas par pays). Cette barre de recherche
> profite en fait à **toutes** les listes à choix de l'application (scripts, boîtes mail…), pas seulement aux fuseaux
> horaires — elle apparaît automatiquement dès qu'une liste dépasse 6 éléments. **Météo** et **Heure dans le monde**
> sont maintenant aussi disponibles sur l'appli (téléphone/PC), via un bouton « Affichage plein écran » — comme
> l'affichage de l'heure/date déjà existant ; limité pour l'instant à l'appli elle-même, pas encore à l'affichage sur
> le PC piloté (server.ps1). Corrigé : dans la modale « Touches » du widget, cliquer sur l'icône d'une touche pour la
> changer ouvrait une popup qui se retrouvait **sous** la modale principale plutôt que par-dessus (les deux fonds
> modaux avaient la même priorité d'affichage) — la popup d'icône passe maintenant au-dessus, et son choix de rôles
> couvre davantage d'icônes qu'avant. Corrigé : ouvrir un message (lecture/réponse/nouveau message) dans le bloc
> courrier du widget laissait apparaître l'en-tête précédent (nom de la boîte, boutons modifier/filtre/actualiser) par
> transparence au-dessus — l'affichage du message passe maintenant correctement au premier plan.
>
> Si le temps le permettait : un bouton dans la barre du haut de l'appli **coupe l'envoi des requêtes au serveur**
> (icône pause/lecture) — pratique pour éviter un appui accidentel sans fermer l'app ; toute tentative pendant la pause
> est clairement signalée plutôt que silencieusement ignorée. Un nouveau type de bouton **« Retrouver le téléphone »**
> (déclenchable par une touche ou une commande vocale, comme n'importe quel autre bouton) passe le téléphone en mode
> sonnerie et joue une alarme au volume maximum pendant 60 secondes (ou jusqu'à l'arrêt manuel, bouton qui apparaît
> dans la barre du haut pendant que ça sonne) — **Android uniquement**, sans effet sur StreamDeck.Desktop. Les pages du
> widget peuvent désormais avoir un bloc **liste de tâches** : cases à cocher directement cliquables dans le widget,
> avec une échéance optionnelle (date + heure) qui fait ressortir la tâche en rouge une fois dépassée — pas encore de
> notification, juste une mise en évidence visuelle. Confirmé au passage : le widget affiche déjà une **notification
> Windows** à l'arrivée d'un nouveau message (depuis la v1.31.0) — rien de nouveau à construire là, la fonctionnalité
> demandée existait déjà.
>
> **Non vérifié dans cet environnement** : en plus de MailKit/Open-Meteo déjà mentionnés ci-dessus, l'affichage météo/
> heure dans le monde sur l'appli (même mécanisme qu'InfoDisplay heure/date, jamais exécuté ici pour de vrai) et,
> surtout, **tout le code spécifique à Android** de cette version (mode sonnerie, alarme, permissions) — ce bac à sable
> n'a pas le kit de développement Android, impossible d'y compiler ni d'y tester StreamDeck.Android, contrairement au
> widget de bureau (StreamDeck.ScriptBuilder + StreamDeck.Shared) qui, lui, a été recompilé ici avec succès après
> chaque changement de cette version.
>
> **v1.34.7 — correctif de compilation Android, sélecteur de date/heure personnalisé, tâches enrichies (description,
> couleur, rappels).** Corrigé : la compilation de StreamDeck.Android échouait (`VolumeNotificationFlags` n'a pas de
> membre « None », une énumération `[Flags]` Android — remplacé par `0`, qui a le même effet : aucun indicateur). Le
> champ de date/heure (utilisé par les tâches, voir plus bas) est maintenant un **sélecteur personnalisé** en modale
> — jour, puis mois, puis année, puis heure/minute par grilles de boutons — plutôt que le sélecteur natif du système
> (rendu trop différent entre Windows/WebView2 et Android, et qui ne s'intégrait pas visuellement) ; réutilisable
> partout dans l'appli (`DateTimeField`/`DateTimePickerModal`, StreamDeck.Shared). Les tâches (bloc « Liste de
> tâches », v1.34.6) ont trois nouveautés : une **description** facultative, consultable dans le widget en cliquant
> sur le texte d'une tâche (une modale de détail s'ouvre, sans cocher la tâche par erreur — cocher se fait maintenant
> uniquement via la case elle-même) ; un **niveau** (Normal/Important/Urgent), affiché comme un liseré de couleur sur
> la tâche dans le widget ; et un **rappel** configurable par tâche (nombre de jours avant l'échéance, répétition —
> aucune/quotidienne/hebdomadaire/mensuelle — et cible du rappel — widget seulement, notification Windows, ou les
> deux). Une tâche récurrente cochée n'est pas gardée « faite » : son échéance avance directement à l'occurrence
> suivante. La notification Windows des tâches réutilise le même mécanisme que celle du nouveau courrier (v1.31.0),
> généralisé en une méthode `Notify(titre, texte)` sur `IWidgetHost`.
>
> **Non vérifié dans cet environnement** : comme pour les versions précédentes, MailKit/Open-Meteo, l'affichage
> météo/heure dans le monde sur l'appli, et tout le code Android (y compris le correctif `VolumeNotificationFlags`
> ci-dessus — corrigé d'après la documentation de l'API Android, mais pas recompilé ici faute de kit de développement
> Android ; à confirmer que `dotnet build` passe maintenant sur ta machine). Le widget de bureau
> (StreamDeck.ScriptBuilder + StreamDeck.Shared, y compris le nouveau sélecteur de date/heure et les tâches
> enrichies) a en revanche été recompilé ici avec succès avant l'envoi.
>
> **v1.34.8 — les listes de tâches deviennent une bibliothèque à part (nouvel onglet « Tâches »), rangeable en
> dossiers et partageable entre plusieurs pages.** Jusqu'ici, les tâches d'un bloc « Liste de tâches » vivaient
> directement dans ce bloc, sur une seule page : impossible de la déplacer ailleurs sans tout retaper, et pas de vue
> d'ensemble de toutes les listes. Un nouvel onglet **Tâches** (à côté de Scripts/Widget/Réglages) liste maintenant
> toutes tes listes, **rangeables en dossiers** comme les scripts (jusqu'à 3 niveaux, ex. `Perso/Courses`) — c'est là
> que tâches, description, niveau et rappels se créent et se modifient. Un bloc « Liste de tâches » du widget se
> contente désormais de **choisir** une de ces listes (comme un bloc « Courrier » choisit une boîte mail), avec un
> raccourci vers l'onglet Tâches pour l'éditer. Conséquence directe : **déplacer une liste d'une page à l'autre** ne
> demande plus que de retirer le bloc d'une page et d'en ajouter un ailleurs en choisissant la même liste — et une
> même liste peut même être choisie sur **plusieurs pages (ou espaces de travail) en même temps**, elle reste
> synchronisée partout (cocher une tâche sur une page la coche partout où elle apparaît). Les listes déjà créées dans
> un bloc d'une version antérieure sont migrées automatiquement à l'ouverture (une nouvelle liste dans la
> bibliothèque, nommée d'après la page, choisie directement dans le bloc qui la contenait) — rien à refaire à la main.
>
> **Non vérifié dans cet environnement** : comme pour les versions précédentes, MailKit/Open-Meteo, l'affichage
> météo/heure dans le monde sur l'appli, et tout le code Android — aucun changement Android dans cette version. Le
> widget de bureau (StreamDeck.ScriptBuilder + StreamDeck.Shared, y compris la nouvelle bibliothèque de tâches et la
> migration automatique) a en revanche été recompilé ici avec succès avant l'envoi.
>
> **v1.34.9 — corrections de lisibilité (éditeur de tâches, popups du widget), minute précise, notifications de
> tâches enfin visibles, en-tête mobile allégé, plusieurs espaces de travail actifs.** Plusieurs correctifs remontés
> avec captures d'écran : l'éditeur d'une tâche (onglet Tâches) détournait une classe CSS pensée pour une toute petite
> paire label+nombre pour y empiler la description et les réglages de rappel — d'où le texte de description sans
> aucun style et les boutons « posés n'importe où » ; refait avec une mise en page dédiée (pas besoin d'onglets, la
> fenêtre est large). Dans le widget, le détail d'une tâche (et plus généralement toute fenêtre ouverte par-dessus le
> widget) utilisait les couleurs de la fenêtre principale, très proches de celles du widget lui-même — quasiment
> illisible, remonté comme "fond transparent" ; le filtre courrier et les notifications avaient le même problème à un
> degré moindre — les trois utilisent maintenant une couleur opaque dédiée, nettement détachée du fond du widget, quel
> que soit le thème choisi (y compris "verre"). Le sélecteur de date/heure ne permettait de choisir la minute que par
> paliers de 5 (8h12 impossible, seulement 8h10 ou 8h15) : un réglage fin ±1 minute a été ajouté à côté de la grille.
>
> Les rappels de tâches réglés sur « Widget seulement » ne changeaient auparavant QUE la couleur du texte dans la
> liste — rien qui attire vraiment l'œil, ce qui explique le rappel remonté comme "jamais reçu" alors que le réglage
> était bien celui-là (pas de notification Windows demandée pour cette tâche-là, donc son absence était normale ;
> c'est l'absence de toute mise en évidence côté widget qui ne l'était pas). Les rappels « Widget » et « Les deux »
> apparaissent maintenant dans la même zone de notifications (cloche) que les envois de courrier en échec, avec le nom
> de la tâche et son échéance — cliquer dessus ouvre son détail ou la marque faite directement. Dans l'appli mobile,
> l'en-tête n'affiche plus « Stream Deck » ni le texte du statut serveur ("en ligne"/"hors ligne"…) en toutes lettres,
> seulement l'icône et la pastille de couleur (les deux restent en info-bulle) — demandé explicitement, ça prenait de
> la place sur un écran étroit.
>
> Enfin, un espace de travail peut maintenant être marqué « aussi actif pour la veille » (Réglages du widget, sur
> n'importe quel espace autre que celui affiché) : ses pages « Afficher en veille » participent alors au carrousel de
> veille en plus de celles de l'espace affiché normalement — le cas cité : un espace « Bureau » affiché au quotidien,
> un autre dédié aux pages de veille. Le carrousel normal (hors veille) reste limité à l'unique espace affiché ; seule
> la veille rassemble plusieurs espaces.
>
> **Non vérifié dans cet environnement** : comme pour les versions précédentes, MailKit/Open-Meteo, l'affichage
> météo/heure dans le monde sur l'appli, et tout le code Android (l'allégement de l'en-tête mobile touche un fichier
> Blazor partagé — StreamDeck.Shared — compilé et vérifié ici, mais son rendu réel sur un téléphone reste à confirmer
> de ton côté, comme pour tout changement visuel Android). Le widget de bureau (StreamDeck.ScriptBuilder +
> StreamDeck.Shared, y compris tous les correctifs ci-dessus) a été recompilé ici avec succès avant l'envoi — mais
> plusieurs de ces correctifs (notamment le contraste des popups en thème "verre", et le comportement avec deux
> espaces de travail actifs) reposent sur un raisonnement CSS/logique que je n'ai pas pu vérifier à l'écran, faute de
> pouvoir lancer le widget lui-même ici : à confirmer sur ta machine.
>
> **v1.34.10 — le correctif de contraste des popups du widget (v1.34.9) était insuffisant, cause racine trouvée et
> corrigée ; plusieurs espaces de travail actifs en même temps (repris tel que demandé) ; option page complète pour
> le courrier en veille ; popups fermées automatiquement en entrant en veille.** Remonté à nouveau avec captures
> d'écran : le détail d'une tâche restait illisible malgré le correctif précédent. En creusant, la vraie cause n'était
> pas la couleur mais le **positionnement** : `.widget-server` (la zone qui défile) utilisait une propriété CSS
> (`container-type`, pour que les touches épinglées se redimensionnent correctement) qui, par un effet de bord de la
> norme CSS, transforme cette zone en « cadre de référence » pour tout ce qui est en `position: fixed` à l'intérieur
> — la fenêtre de détail d'une tâche (et le filtre courrier) se retrouvait donc confinée à cette seule zone au lieu de
> couvrir toute la fenêtre du widget, d'où le mauvais recouvrement/les « barres noires » sous la cloche et le
> comportement cassé si le filtre courrier restait ouvert en passant en veille. Cette propriété a été déplacée sur le
> seul élément qui en a vraiment besoin (la grille de touches épinglées), et — en plus, par sécurité — toutes les
> popups du widget (détail de tâche, filtre courrier, notifications, sélecteur d'espace) utilisent maintenant une
> couleur fixe dédiée au lieu de celle du thème du widget (qui, vérification faite, était identique au fond du widget
> lui-même pour 3 thèmes sur 4 — d'où le "transparent" qui persistait malgré le correctif v1.34.9). Toute popup encore
> ouverte se ferme aussi automatiquement en entrant en veille, pour éviter tout chevauchement bancal.
>
> Sur le courrier en veille : un nouveau bouton sur le bloc « Courrier » (Réglages du widget) permet de choisir, page
> par page, entre le résumé (« X message(s), Y non lu(s) », comme avant) et la **liste complète des messages**
> (lecture seule, comme demandé).
>
> Enfin, le modèle « un seul espace de travail affiché + un second marqué secondairement actif pour la veille »
> introduit en v1.34.9 ne correspondait pas à ce qui était demandé (et provoquait aussi le bug de bascule remonté) —
> remplacé par un seul réglage **« Actif sur le widget »** par espace, plusieurs pouvant être activés à la fois (actif
> par défaut, un espace désactivé peut être réactivé à tout moment, au moins un reste toujours actif). Un espace actif
> participe à la fois au carrousel normal ET à la veille — le cas cité (un espace « Bureau » affiché au quotidien et
> un autre dédié aux pages de veille, tous deux actifs) fonctionne maintenant tel que décrit, sans bascule manquée.
>
> **Non vérifié dans cet environnement** : comme pour les versions précédentes, MailKit/Open-Meteo, l'affichage
> météo/heure dans le monde sur l'appli, et tout le code Android — aucun changement Android dans cette version. Le
> widget de bureau (StreamDeck.ScriptBuilder + StreamDeck.Shared, y compris tous les correctifs ci-dessus) a été
> recompilé ici avec succès avant l'envoi, mais le rendu réel des popups (positionnement et couleur, sur les
> différents thèmes) et le nouveau comportement multi-espaces actifs reposent sur un raisonnement CSS/logique que je
> n'ai pas pu vérifier à l'écran, faute de pouvoir lancer le widget lui-même ici : à confirmer sur ta machine, en
> espérant cette fois que ce soit la bonne — n'hésite pas à renvoyer une capture si ce n'est toujours pas ça.
>
> **v1.34.11 — le modèle "plusieurs espaces actifs" de v1.34.10 est retiré (ça ne correspondait pas à ce qui était
> demandé), le sélecteur d'espace redevient exclusif comme avant ; la modale de détail de tâche et le filtre courrier
> sortis de la zone défilante du widget (cause réelle du "toujours transparent") ; bordure d'accent ajoutée à toutes
> les bulles pour qu'elles se détachent même sur un thème sombre proche en teinte.** Remonté à nouveau avec captures
> d'écran, et à raison : le modèle v1.34.10 (plusieurs espaces "actifs" fusionnés dans le carrousel normal) ne
> correspondait pas à ce qui était demandé — chaque espace doit garder ses propres pages, un seul affiché à la fois
> sur le widget. Le sélecteur redevient donc un choix exclusif (comme avant v1.34.9), avec en plus une icône lune par
> espace non affiché pour le marquer "aussi actif pour la veille" — ses pages "Afficher en veille" s'ajoutent alors à
> celles de l'espace affiché, directement dans le sélecteur du widget plutôt que seulement dans les Réglages.
>
> Pour la modale de détail de tâche et le filtre courrier, toujours transparents malgré DEUX correctifs précédents :
> en creusant plus loin, ces deux bulles précises vivaient encore dans la zone qui défile (.widget-server) au moment
> des dernières captures — seul le déplacement d'une propriété CSS (v1.34.10) avait été tenté, insuffisant. Cette
> fois, les deux ont été sorties complètement de cette zone (comme le sélecteur d'espace et les notifications, qui
> eux fonctionnaient déjà) : leur affichage ne peut plus être confiné ou mal positionné par quoi que ce soit dans le
> carrousel. En plus, toutes les bulles du widget ont maintenant une bordure nette en couleur d'accent (pas juste un
> fond uni) pour qu'elles se détachent visuellement même sur un thème sombre dont la teinte serait proche.
>
> **Non vérifié dans cet environnement** : comme pour les versions précédentes, MailKit/Open-Meteo, l'affichage
> météo/heure dans le monde sur l'appli, et tout le code Android — aucun changement Android dans cette version. Le
> widget de bureau a été recompilé ici avec succès avant l'envoi, mais comme toujours le rendu réel (positionnement
> des bulles maintenant sorties de la zone défilante, contraste des couleurs, comportement du sélecteur d'espace)
> reste à vérifier sur ta machine — désolé pour les allers-retours sur ce point précis, je n'ai pas la possibilité de
> voir le widget tourner ici pour le confirmer moi-même avant de te l'envoyer.
>
> Sur tes demandes pour l'appli téléphone (réglage vocal pour "retrouver mon téléphone" sans bouton dédié, sonneries
> personnalisées, musique déclenchée par bouton) et sur la nouvelle catégorie d'actions "téléphone" dans le
> constructeur de scripts (sonnerie du PC qui déclenche celle du téléphone via le WiFi) : je préfère ne pas les
> bâcler dans ce round déjà chargé de correctifs. La dernière en particulier demande de construire un vrai canal de
> communication PC → téléphone qui n'existe pas encore (aujourd'hui tout part du téléphone vers le PC, jamais
> l'inverse) — une vraie décision d'architecture, pas un petit ajustement. Je m'y mets au prochain envoi.

> **v1.34.22 — cause RÉELLE du plantage « Recevoir depuis le PC » (v1.34.20) trouvée grâce au journal de plantage que
> tu as envoyé : corrigée pour de vrai cette fois, au lieu du simple durcissement défensif de la v1.34.20.**
>
> **La vraie cause.** `MissingForegroundServiceTypeException`, visible dans le journal que tu as copié-collé depuis
> les Réglages (merci — exactement ce pour quoi ce journal a été ajouté). Déclarer le type du service au premier plan
> dans le manifeste de l'appli (`dataSync`, fait depuis la toute première version de cette fonctionnalité) ne suffit
> plus sur les versions d'Android récentes que vise ton téléphone (targetSDK=36 dans le journal) : il faut AUSSI le
> redonner explicitement à l'appel qui démarre le service. C'était l'étape manquante — corrigé.
>
> **Pareil pour l'écoute vocale continue**, par précaution : même mécanique de démarrage (service au premier plan),
> même correctif appliqué, même si tu n'as pas signalé de plantage dessus — pour ne pas attendre un deuxième rapport
> avant de la corriger aussi.
>
> **Sur ta question (port 8766 identique sur tes deux téléphones) : aucun souci.** Chaque téléphone fait tourner son
> propre petit serveur, sur SA PROPRE adresse IP — le port ne compte que par rapport à l'adresse, pas tout seul. Deux
> appareils différents peuvent utiliser le même port sans se gêner, exactement comme deux maisons peuvent toutes les
> deux avoir un numéro de rue "12" tant qu'elles ne sont pas dans la même rue. Ce n'était pas la cause du problème.
>
> **Non vérifié dans cet environnement** : comme toujours pour StreamDeck.Android (aucun SDK Android disponible ici).
> Le correctif s'appuie sur le message d'exception exact que tu as envoyé et sur l'API Android officielle documentée
> pour ce cas précis (bascule vers la version de `StartForeground` qui précise le type) — mais n'a pas pu être
> recompilé pour de vrai avant de te l'envoyer. Réessaie d'activer « Recevoir depuis le PC » sur ton deuxième
> téléphone ; si ça plante encore, le nouveau journal (Réglages) me dira quoi corriger ensuite.
>
> **v1.34.21 — plusieurs téléphones : Réglages → Téléphone devient une liste (comme les PC pilotés) au lieu d'un
> seul, et chaque étape « Téléphone » d'un script choisit maintenant LEQUEL viser (plus seulement l'action).**
>
> **Réglages → Téléphone.** Même présentation à onglets que « PC pilotés » : un bouton + pour ajouter un téléphone,
> chacun avec son nom, son adresse IP, son port, son jeton, son bouton « Tester la connexion », et un « téléphone par
> défaut » (utilisé par une étape qui n'en désigne pas un autre). Ta configuration existante (un seul téléphone) est
> reprise automatiquement comme premier téléphone de la liste, désigné par défaut — rien à refaire.
>
> **L'étape « Téléphone » d'un script.** Dès que plus d'un téléphone est configuré, un nouveau sélecteur apparaît
> au-dessus de « Action » pour choisir lequel des téléphones configurés cette étape vise — avant, seule l'action
> (sonnerie/alarme, ouvrir une application) était réglable, toujours vers l'unique téléphone configuré, exactement le
> trou que tu as signalé. Avec un seul téléphone configuré, le sélecteur reste caché (rien à choisir) et l'étape
> vise ce téléphone, comme avant.
>
> **Onglet Téléphone (modèles composés sur le PC).** Le bouton « Envoyer au téléphone » d'un modèle vise maintenant
> aussi un téléphone choisi (sélecteur « Envoyer à » dans la barre d'outils, dès que plus d'un téléphone est
> configuré) plutôt que toujours le même.
>
> **v1.34.20 — suite au plantage remonté sur l'appli téléphone (« recevoir les commandes du PC » qui fait planter
> l'app sur un Samsung, fonctionne sur un autre téléphone) : durcit le service concerné contre ce genre de plantage
> fabricant-dépendant, et ajoute un journal du dernier plantage consultable dans les Réglages du téléphone pour
> diagnostiquer précisément ce qui s'est passé.**
>
> **Pourquoi je n'ai pas pu corriger la cause exacte tout de suite.** Un plantage qui n'arrive que sur un modèle
> précis et pas sur un autre, sans message d'erreur ni journal (adb) disponible ici, n'est pas diagnosticable avec
> certitude à distance — plusieurs causes plausibles liées à Android/Samsung existent (icône de notification
> introuvable sur cette version d'Android, restrictions plus strictes du fabricant sur les services au premier
> plan...), mais aucune ne peut être confirmée sans plus d'information.
>
> **Durci contre le cas le plus probable.** Le service qui reçoit les commandes du PC (activé par le réglage « Recevoir
> depuis le PC ») protège maintenant tout son démarrage : une erreur qui s'y produirait (par exemple la construction
> de la notification, qui dépend de ressources système pouvant varier d'un fabricant à l'autre) est rattrapée au lieu
> de faire planter toute l'application, et l'icône de la notification a un repli si celle de l'app n'est pas
> disponible sur cet appareil — une cause plausible mais non confirmée pour ce plantage précis.
>
> **Nouveau : le journal du dernier plantage, dans les Réglages.** Si l'application plante quand même (celui-ci ou un
> autre, à l'avenir), une nouvelle carte apparaît en haut des Réglages au prochain lancement avec le détail technique
> complet de ce qui s'est passé, un bouton pour le copier et me l'envoyer directement, et un bouton pour l'effacer.
> Rien ne s'affiche tant qu'aucun plantage n'a eu lieu. Si ton Samsung replante malgré ce correctif, ce journal me
> dira enfin exactement quoi corriger.
>
> **Non vérifié dans cet environnement** : ce correctif touche uniquement l'appli téléphone (StreamDeck.Android), qui
> — contrairement à StreamDeck.Shared/StreamDeck.ScriptBuilder — n'a jamais pu compiler dans ce bac à sable (pas de
> SDK/émulateur Android disponible ici, comme pour tout ce qui touche à cette appli depuis le début). Le reste du
> projet (StreamDeck.Shared, StreamDeck.ScriptBuilder, y compris la nouvelle carte de Réglages) recompile avec succès.
> Merci de réessayer d'activer « Recevoir depuis le PC » sur ton Samsung — s'il plante encore, va dans Réglages et
> envoie-moi ce que montre la nouvelle carte « Dernier plantage ».
>
> **v1.34.19 — corrige deux soucis remontés sur l'onglet Téléphone (v1.34.16) : le style manquant de la fenêtre
> « Choisir une icône », et l'écran noir/plantage parfois rencontré en important une image pour un bouton ou un
> choix.**
>
> **Style manquant pour « Choisir une icône ».** La feuille de style de cette fenêtre (IconPicker.css) n'avait jamais
> été liée dans la page de Script Builder — elle ne servait nulle part côté PC avant que l'onglet Téléphone ne la
> réutilise (v1.34.16), donc l'oubli n'avait jamais été remarqué. Ajoutée dans l'en-tête de la page, comme les autres
> styles partagés.
>
> **Écran noir / plantage en ajoutant une image.** Le composant standard de Blazor pour choisir un fichier
> (`<InputFile>`) ne s'entend pas bien avec la fenêtre web intégrée (WebView2) de l'application Windows — Script
> Builder l'avait déjà contourné partout ailleurs (image du widget, logo de signature mail, police personnalisée) en
> utilisant son propre sélecteur de fichier natif Windows, mais ce contournement n'avait pas suivi quand les éditeurs
> de bouton/choix de l'appli téléphone ont été réutilisés tels quels dans l'onglet Téléphone. Maintenant, importer une
> image pour un bouton (ou un choix) passe par ce même sélecteur natif côté PC — l'appli téléphone, elle, continue
> d'utiliser `<InputFile>` comme avant (rien n'y change).
>
> **Non vérifié dans cet environnement** : ce qui recompile dans ce bac à sable (StreamDeck.Shared,
> StreamDeck.ScriptBuilder) l'a fait avec succès, mais l'hôte Windows (StreamDeck.ScriptBuilder.Windows, qui seul
> héberge le sélecteur de fichier natif et la fenêtre WebView2 concernée) n'entre de toute façon jamais dans ce bac à
> sable — donc le correctif de l'écran noir en particulier n'a pas pu être rejoué pour de vrai ici, seulement
> raisonné par analogie avec les autres imports d'image qui utilisent déjà ce même sélecteur natif avec succès.
>
> **v1.34.18 — les triggers vocaux d'un bouton (déjà transférés tels quels depuis la v1.34.16/17) sont maintenant
> visibles ET modifiables dans l'onglet Téléphone du PC, avec un vrai enregistrement au micro du PC en plus de la
> saisie au clavier ; ajout de PcSection (plusieurs PC, manquait depuis la v1.34.16) dans le même onglet.**
>
> **Triggers vocaux : visibles et modifiables côté PC.** Ils étaient déjà transférés sans problème avec le reste de
> la configuration (export/import, c'est un simple JSON complet) — mais la section qui permet de les VOIR/MODIFIER
> dans l'éditeur de bouton (ButtonEditor) était entièrement masquée côté PC, parce qu'elle dépendait d'un micro
> disponible. Maintenant : la liste des triggers et le champ pour en taper un restent visibles partout ; seul le
> bouton micro reste conditionné à un micro disponible.
>
> **Enregistrement au micro, aussi depuis le PC.** Plutôt que de se limiter à la saisie au clavier, le bouton
> « Enregistrer un trigger » marche maintenant aussi depuis Script Builder (Windows) — utilise le moteur de dictée
> de Windows (hors connexion, pas besoin d'internet).
>
> **PcSection ajoutée dans l'onglet Téléphone.** Oubliée dans la v1.34.16 alors que « plusieurs PC » faisait partie
> de la demande d'origine — un modèle composé sur le PC ne pouvait choisir/adresser qu'un seul « Mon PC » par
> défaut, sans vraie adresse. Maintenant la même carte que sur le téléphone (ajouter/nommer/adresser plusieurs PC,
> tester la connexion, réveil réseau) est disponible pour chaque modèle.
>
> **Non vérifié dans cet environnement** : comme toujours pour ce qui touche à l'appli téléphone (aucun changement
> téléphone dans cette version, à part le JSON déjà transféré tel quel) ; côté PC, ce qui recompile dans ce bac à
> sable (StreamDeck.Shared, StreamDeck.ScriptBuilder) l'a fait avec succès, mais **l'enregistrement vocal au micro
> du PC n'a pas pu être testé ici** (pas de micro ni de moteur de reconnaissance Windows disponibles) — et l'hôte
> Windows (StreamDeck.ScriptBuilder.Windows, qui héberge ce nouveau code) n'entre de toute façon jamais dans ce bac
> à sable (seules les bibliothèques partagées y recompilent, comme indiqué à chaque version).
>
> **v1.34.17 — complète le sens manquant de la v1.34.16 : un bouton « Envoyer au PC » dans l'appli téléphone (Réglages)
> envoie la configuration du téléphone à Script Builder en réseau direct — chaque envoi devient un nouveau modèle
> dans l'onglet Téléphone, rien n'y est jamais écrasé automatiquement.**
>
> **Comment ça marche.** Un petit serveur (symétrique de celui du téléphone, v1.34.13) tourne maintenant DANS Script
> Builder lui-même — à activer dans Réglages → « Recevoir depuis le téléphone », qui affiche l'adresse IP de ce PC,
> un port et un jeton généré automatiquement. Ces trois informations se recopient côté téléphone, dans le nouveau
> bloc Réglages → « Envoyer au PC » (adresse IP de ce PC, port, jeton). Le bouton « Envoyer au PC » y transmet alors
> la configuration actuellement affichée à l'écran (même avant d'avoir cliqué « Enregistrer ») ; côté PC, elle arrive
> comme un **nouveau modèle** nommé « Depuis le téléphone (date heure) » dans l'onglet Téléphone — jamais en
> écrasant un modèle existant, pour pouvoir comparer avant de choisir lequel garder ou renvoyer.
>
> **Non vérifié dans cet environnement** : comme pour la v1.34.16, tout le côté téléphone (le nouveau bloc « Envoyer
> au PC », jamais testé pour de vrai avec un appareil Android) — mais le côté PC (le nouveau petit serveur qui
> écoute, démarré/arrêté au lancement de l'application et selon Réglages → « Recevoir depuis le téléphone ») a pu
> être recompilé avec succès ici, ce qui n'était pas le cas pour le code Android.
>
> **v1.34.16 — nouvel onglet « Téléphone » : compose des configurations de téléphone depuis le PC (plusieurs modèles,
> mêmes espaces/pages/boutons/images que l'app téléphone) et envoie-les en Wi-Fi direct. Correctif au passage : une
> tâche "retirée de la liste" (v1.34.15) revenait silencieusement à chaque enregistrement.**
>
> **Correctif — "Retirer de la liste" (v1.34.15) ne tenait pas.** Le marquage "retiré" d'une tâche n'était pas
> recopié par la fonction interne qui reconstruit la configuration à chaque enregistrement (`Update`, dans
> `BuilderConfigService`) — la tâche réapparaissait donc dans la liste du widget dès le prochain enregistrement
> (ouverture des Réglages, rappel qui se déclenche, etc.), sans que tu aies rien touché. Repéré en relisant ce fichier
> pour la fonctionnalité ci-dessous ; corrigé.
>
> **Nouvel onglet « Téléphone ».** Réponse à la demande : « créer des config tel depuis le pc et l'importer sur le
> tel ». Le nouvel onglet (à côté de Scripts / Widget / Tâches / Réglages) permet de créer **plusieurs modèles** de
> configuration téléphone, chacun avec ses propres espaces de travail, pages et boutons (icône, couleur, image
> importée ou lien, script…) — l'édition réutilise **exactement** les mêmes composants que l'app téléphone (pas une
> réplique approximative), donc le même rendu et les mêmes réglages de bouton. Un bouton « Envoyer au téléphone » sur
> chaque modèle transmet toute la configuration **en réseau direct** (Wi-Fi), en réutilisant l'adresse/le jeton déjà
> configurés dans Réglages → Téléphone pour le bloc de script « Téléphone » — pas de fichier à transférer à la main.
> Côté téléphone, la configuration reçue **remplace** entièrement les espaces/pages/boutons actuels (comme un import
> classique) ; le jeton du PC resté local n'est pas touché.
>
> **Sens PC → téléphone seulement dans cette version.** Le sens inverse — un bouton « Envoyer au PC » depuis l'app
> téléphone — demande un petit serveur équivalent qui écoute côté PC, qui n'existe pas encore : je préfère l'étudier
> correctement (notamment comment il doit cohabiter avec le serveur de scripts existant) plutôt que de l'improviser
> dans ce même envoi. Prévu pour une prochaine version.
>
> **Non vérifié dans cet environnement** : comme pour la v1.34.13, tout ce qui touche à l'appli téléphone
> (`PhoneCommandServerService`, étendu ici pour recevoir une configuration complète en plus des commandes
> sonner/ouvrir une app) — aucun appareil Android disponible ici pour un vrai envoi de bout en bout. Côté PC, le
> nouvel onglet « Téléphone » recompile avec succès avant l'envoi (vérifié ici), mais comme toujours le rendu réel
> (disposition de la grille de modèles, résultat visuel de l'éditeur réutilisé dans ce nouveau contexte) reste à
> confirmer sur ta machine.
>
> **v1.34.15 — bulles d'info "i" repositionnées (plus de texte coupé/superposé) ; tâches retirables de la liste en un
> clic depuis leur détail (réactivables depuis les réglages du bloc « Liste de tâches ») ; onglet « Historique » dans
> les notifications du widget ; bouton rapide marche/arrêt du serveur tout en haut du widget.**
>
> **Bulles "i" : texte coupé/bulles mal placées (capture reçue).** La bulle (introduite en v1.34.13) s'ouvrait centrée
> sur l'icône, ce qui la faisait souvent déborder à GAUCHE de la fenêtre pour une icône proche du bord gauche
> (fréquent : la plupart le sont) — le début du texte, le plus important, devenait invisible. Elle s'ouvre maintenant
> alignée sur le bord gauche de l'icône (déborde à droite si besoin, sans gravité). Également corrigé dans l'étape
> « Téléphone » du constructeur de scripts (celle de ta capture) : les deux bulles qui se suivaient sans lien visuel
> clair sont maintenant chacune à côté de ce qu'elles expliquent précisément (l'action, puis le champ du paquet) au
> lieu d'être empilées en bas de la carte.
>
> **Retirer une tâche de la liste, en un clic.** Depuis le détail d'une tâche (widget), un bouton « Retirer de la
> liste » — elle disparaît de l'affichage mais n'est PAS supprimée (description, échéance, rappels intacts).
> Réactivable depuis les Réglages du widget, dans la configuration du bloc « Liste de tâches » concerné : une petite
> liste des tâches retirées de CETTE liste, avec un bouton « Réactiver » chacune.
>
> **Historique des notifications.** La cloche du widget n'affichait que ce qui restait "actif" (rappel pas encore
> traité, envoi en échec) — une fois traité, plus aucune trace. Un nouvel onglet « Historique » à côté de « Actives »
> garde maintenant un journal (jusqu'à 100 entrées) de ce qui a déjà été notifié : rappels de tâches déclenchés, et
> envois programmés réussis ou échoués — avec un bouton pour le vider si besoin.
>
> **Bouton marche/arrêt du serveur, tout en haut du widget.** Avant, il fallait qu'une page du widget ait un bloc
> « Contrôles serveur » posé dessus pour pouvoir l'éteindre/rallumer depuis le widget — invisible sinon. Un bouton
> compact est maintenant toujours présent dans la barre du haut (à côté de la cloche), peu importe la composition des
> pages — vert si le serveur tourne, rouge sinon, toujours synchronisé avec le bloc "Contrôles serveur" s'il y en a
> un sur une page.
>
> **Non vérifié dans cet environnement** : comme toujours, MailKit/Open-Meteo, l'affichage météo/heure dans le monde,
> et le rendu réel dans le widget de bureau (Windows) — recompilé ici avec succès avant l'envoi, mais je ne peux pas
> le voir tourner d'ici, donc le positionnement effectif des bulles d'info (notamment sur un écran très étroit) et
> l'alignement des nouveaux éléments (onglets Historique, bouton serveur dans la barre du haut) restent à confirmer
> sur ta machine.
>
> **v1.34.14 — correctif de compilation sur l'appli téléphone (nouveau service `PhoneCommandServerService` de la
> v1.34.13) : il manquait `using System.Threading;`, nécessaire pour `CancellationToken`/`CancellationTokenSource`
> (contrairement à `System.Threading.Tasks`, `System.Threading` n'est pas ajouté automatiquement). Si tu obtiens
> encore des erreurs après avoir repris ce fichier, fais un Nettoyer la solution + supprime les dossiers `bin`/`obj`
> de `StreamDeck.Android` (et `StreamDeck.Shared`) avant de recompiler — une compilation incrémentielle bloquée après
> l'ajout d'un nouveau fichier peut afficher une liste d'erreurs bien plus large et confuse que le vrai problème.**
>
> **v1.34.13 — déclenchement vocal de "retrouver mon téléphone" à l'enregistrement (comme pour un bouton) ; nouveau
> canal PC → téléphone (sonnerie/alarme et ouverture d'une application, depuis un script) ; les bulles d'info "i" ne
> s'affichent plus qu'au survol (sur les 3 apps) ; tâches repliables dans l'éditeur de liste ; éditeur de liste de
> tâches transformé en page plein écran (comme les scripts), prêt à recevoir de futures options par tâche.**
>
> **Phrase vocale "retrouver mon téléphone" enregistrable à la voix.** Dans Réglages (appli téléphone), à côté du
> champ texte de la phrase déclencheur, un bouton "Enregistrer à la voix" — exactement le même mécanisme que
> l'enregistrement du déclencheur d'un bouton : dis la phrase, elle remplace le texte. Coupe puis rétablit
> l'écoute vocale continue le temps de l'enregistrement si elle tournait déjà, comme pour un bouton.
>
> **Nouveau : commander le téléphone depuis le PC.** Ça n'existait pas du tout avant (tout partait du téléphone vers
> le PC, jamais l'inverse) — c'est la pièce la plus grosse et la plus neuve de cet envoi :
> - Côté téléphone, Réglages → nouvelle section "Commandes depuis le PC" : un interrupteur qui démarre un petit
>   serveur local sur le téléphone (rien n'est exposé sur Internet, seulement sur ton réseau WiFi local), protégé par
>   un jeton généré automatiquement (comme le jeton déjà utilisé dans l'autre sens, téléphone → PC), avec l'adresse IP
>   du téléphone affichée pour la recopier côté PC. **Important, écrit noir sur blanc dans l'écran lui-même** :
>   change le port ou régénère le jeton n'a d'effet sur le serveur réellement lancé qu'après avoir appuyé sur
>   "Enregistrer" en bas de l'écran — comme le réglage vocal existant, cet écran modifie d'abord un brouillon.
> - Côté PC, Réglages → nouvelle section "Téléphone" : adresse IP, port et jeton (recopiés depuis le téléphone), avec
>   un bouton "Tester la connexion".
> - Nouvelle catégorie d'action dans le constructeur de scripts : "Téléphone", avec deux choix — "Sonnerie / alarme"
>   (déclenche exactement le même mécanisme que "Retrouver le téléphone") ou "Ouvrir une application" (par son nom de
>   paquet Android, ex. `com.spotify.music`). L'adresse/port/jeton du moment sont figés dans le script au moment où tu
>   l'enregistres, comme tous les autres réglages du genre (Spotify, volume…) — si tu changes l'adresse du téléphone
>   plus tard, seuls les scripts enregistrés après le changement en tiennent compte.
>
> **Bulles d'info "i" au survol seulement, sur les 3 apps.** Avant, le texte à côté de chaque icône "i" restait
> affiché en permanence — ce qui, avec le reste, prenait beaucoup de place. Il n'apparaît maintenant que quand la
> souris (ou le focus clavier, au clic sur l'icône) est dessus, comme une vraie infobulle. Ça ne touche que les
> vraies bulles d'info (icône "i") — pas les indications de poignée de glisser-déposer, ni les messages de
> confirmation ou d'erreur, qui restent affichés comme avant (ceux-là doivent rester visibles pour que tu voies bien
> que l'action a marché).
>
> **Tâches repliables dans l'éditeur de liste.** Comme tu l'as montré sur ta capture, une tâche avec tous ses champs
> ouverts (description, niveau, échéance, notifications…) prend énormément de place dès qu'une liste en a plusieurs.
> Chaque tâche a maintenant une flèche pour la replier — repliée, elle affiche un résumé sur une ligne (échéance,
> niveau, "a une note"…). Une tâche tout juste ajoutée s'ouvre automatiquement pour que tu puisses la remplir tout de
> suite.
>
> **Éditeur de liste de tâches : page plein écran plutôt que fenêtre.** Comme demandé, il fonctionne maintenant
> exactement comme l'éditeur de scripts — une page dédiée avec bouton "Retour", plutôt qu'une fenêtre par-dessus la
> bibliothèque. Objectif principal : de la place pour de futures options par tâche (par ex. "une fois cette tâche
> terminée, lance ce script" ou "affiche la météo") — pas encore ajoutées dans cet envoi (pas explicitement redemandé
> cette fois), mais la page est prête à les recevoir sans nouveau chantier de ce genre.
>
> **Non vérifié dans cet environnement** : comme toujours, MailKit/Open-Meteo, l'affichage météo/heure dans le monde,
> et tout le code Android — **et cette fois plus que jamais** pour la partie serveur de commandes sur le téléphone :
> c'est un nouveau service Android entier (écoute réseau, ouverture d'application par nom de paquet, nouvelles
> entrées dans le fichier de configuration Android) qui n'a jamais pu tourner une seule fois ici (pas d'appareil ni
> d'émulateur Android disponible) — écrit d'après l'API documentée, comme tout le code Android déjà livré, mais à
> vérifier en conditions réelles avant de t'y fier, en particulier : le serveur démarre-t-il bien et reste-t-il actif
> en tâche de fond, le jeton est-il bien vérifié, l'ouverture d'une application par son nom de paquet fonctionne-t-elle
> sur ton téléphone. Le constructeur de scripts (PC) a été recompilé ici avec succès avant l'envoi, y compris la
> nouvelle page plein écran des tâches et les bulles au survol — mais je ne peux pas voir le rendu réel tourner
> d'ici, donc l'affichage effectif (positionnement des bulles, transitions de la page plein écran) reste à confirmer
> sur ta machine.
>
> **v1.34.12 — modale de détail de tâche reconstruite sans le composant `<Modal>` partagé (toujours cassée malgré la
> v1.34.11) ; la veille ne se déclenche plus tant qu'une bulle du widget est ouverte ; "Actif sur le widget"
> redevient un choix MULTIPLE (3ᵉ et — je l'espère — dernière version de ce réglage) ; réglage vocal dédié "retrouver
> mon téléphone" + sonneries locales (mp3) sur l'appli téléphone.**
>
> **Détail de tâche, toujours cassé après DEUX correctifs précédents (capture reçue : bouton fermer mal positionné
> sous le titre).** Plutôt que de continuer à deviner ce qui, dans le composant `<Modal>` partagé, se comporte mal une
> fois inséré dans le widget (cause jamais identifiée avec certitude — peut-être une règle CSS de la fenêtre
> principale qui s'applique différemment là), cette bulle a été reconstruite avec exactement le même mécanisme que le
> sélecteur d'espace et les notifications — les deux seules bulles jamais remontées comme fonctionnant correctement —
> plutôt que le composant `<Modal>`. Je le dis franchement : après deux ratés sur ce point précis, je préfère ne pas
> réaffirmer que "c'est réglé" sans avoir pu le voir tourner — seulement que ça utilise maintenant un mécanisme déjà
> confirmé chez toi, pas une nouvelle tentative de réglage du même composant.
>
> **La veille ne coupe plus une bulle ouverte.** Avant, le minuteur de veille se déclenchait même avec une bulle
> ouverte (filtre courrier, notifications, sélecteur d'espace, détail de tâche) et fermait tout d'un coup par-dessus
> — remonté "tout buggé". Maintenant, tant qu'une bulle reste ouverte, la veille ne se déclenche simplement pas : le
> délai est reporté (revérifié après le même délai) plutôt que de couper une consultation en cours.
>
> **"Actif sur le widget" — désolé, 3ᵉ tentative sur ce réglage.** Ce que tu as précisé : plusieurs espaces peuvent
> être "actifs" à la fois, mais ça veut seulement dire qu'ils apparaissent dans le sélecteur du widget (le bouton
> pour changer d'espace affiché) — pas que leurs pages se mélangent. Ça a aussi une deuxième conséquence, celle que
> tu décrivais depuis le début et qui ne marchait pas encore : TOUS les espaces actifs contribuent maintenant leurs
> pages "Afficher en veille" au carrousel de veille, affiché ou non — un espace "Bureau" affiché au quotidien et un
> autre dédié aux pages de veille, tous deux actifs, fonctionnent maintenant comme demandé. Un seul espace (parmi les
> actifs) reste "affiché" pour le carrousel normal à la fois, changé via le sélecteur du widget — qui ne liste
> désormais que les espaces actifs. Remplace le détour v1.34.10 (tout fusionné) et v1.34.11 (exclusif + second
> interrupteur séparé) : un seul interrupteur "Actif sur le widget", choix multiple, comme demandé.
>
> **Appli téléphone : réglage vocal "retrouver mon téléphone" + sonneries locales.** Nouvelle section de réglages
> tout en bas de l'écran Réglages (après Sauvegarde) :
> - Une commande vocale dédiée ("retrouve mon telephone" par défaut, modifiable), indépendante de tout bouton posé
>   sur une page — reconnue en plus du mot de réveil + bouton habituel, avec son propre choix "nécessite le mot de
>   réveil".
> - Une liste de sonneries locales (mp3...) ajoutées depuis le téléphone (sélecteur de fichiers), copiées dans
>   l'application pour rester utilisables durablement ; une sélectionnée remplace la sonnerie d'alarme par défaut
>   pour "Retrouver le téléphone" — et reste disponible "pour une future implémentation" comme demandé, même si rien
>   d'autre ne les utilise encore.
>
> **Non vérifié dans cet environnement** : comme toujours, MailKit/Open-Meteo, l'affichage météo/heure dans le monde,
> et tout le code Android — **particulièrement le cas cette fois** pour la partie "appli téléphone" : le service
> d'écoute vocale, le sélecteur de fichiers audio Android (`FilePicker` de MAUI) et la lecture d'une sonnerie
> personnalisée n'ont jamais pu tourner une seule fois ici (pas d'appareil/émulateur Android disponible) — écrits
> d'après l'API documentée, comme le reste du code vocal/Android déjà livré, mais à vérifier en conditions réelles
> avant de t'y fier. Le widget de bureau (Windows) a été recompilé ici avec succès avant l'envoi ; comme toujours, le
> rendu réel de la bulle de détail de tâche reste à confirmer sur ta machine — je ne peux pas la voir tourner d'ici.
>
> Toujours pas traité (pas redemandé dans ton dernier message, donc mis de côté plutôt qu'oublié) : musique
> déclenchée par bouton, et la nouvelle catégorie d'actions "téléphone" dans le constructeur de scripts (sonnerie du
> PC qui déclenche celle du téléphone via le WiFi — demande un nouveau canal de communication PC → téléphone à
> construire depuis zéro). Dis-moi si tu veux que je m'y mette au prochain envoi.

- **Cartes ou liste** : deux affichages (boutons à droite de la recherche), mémorisés. Chaque carte montre le nombre d'actions,
  la description et l'enchaînement des actions en icônes.
- **Dossiers, uniquement dans l'application** : « Nouveau dossier » range les scripts (jusqu'à 3 niveaux : `Jeux/Steam/Solo`),
  mais c'est un rangement propre à Script Builder — les fichiers restent toujours directement dans `Script` (server.ps1
  cherche `Script\<nom>.ps1` sans descendre dans les sous-dossiers). Le nom à taper dans le bouton *Script* du Stream Deck
  est donc toujours le **nom seul** (`Photoshop`, jamais `Adobe/Photoshop`), quel que soit le dossier où il est rangé —
  bouton de copie sur la carte. Comme les noms sont partagés par toute la bibliothèque, deux scripts ne peuvent pas
  porter le même nom même dans des dossiers différents.
- **Glisser-déposer** : fais glisser une carte de script sur un dossier (ou sur « Script » dans le fil d'Ariane) pour la
  ranger ; un dossier peut se glisser sur un autre pour le déplacer. Le bouton <i class="fa-solid fa-folder-tree"></i>
  de chaque carte fait la même chose sans glisser-déposer.
- **Recherche** : dans tous les dossiers à la fois, par nom, dossier, description ou action (« steam.exe », « spotify »…),
  plusieurs mots possibles.
- **Modèles** : « Nouveau script » ouvre une galerie d'enchaînements prêts à compléter (jeu Steam, soirée cinéma, session
  de travail, deux écrans, fin de session, mode nuit…).

Un script écrit à la main ne peut pas être rangé dans un dossier depuis l'application (il n'a pas les métadonnées
nécessaires) : range-le en le déplaçant toi-même dans l'Explorateur, `server.ps1` le retrouvera quand même par son nom
(voir ci-dessous).

## Glisser-déposer (poignée dédiée)

Prends une carte (script ou dossier) par sa **poignée** — l'icône <i class="fa-solid fa-grip-vertical"></i> à gauche —
pour la faire glisser sur un dossier ou sur le fil d'Ariane. Ce n'est pas l'API de glisser-déposer native du navigateur
(qui ne démarrait pas dans cette application) mais un mécanisme entièrement géré par l'application, basé sur les
événements de la souris. Une étiquette suit le curseur pendant le glissement pour montrer ce qui est tenu, et le
dossier survolé se met en évidence.

**Tous les scripts sont rangeables**, y compris ceux écrits à la main : leur fichier n'est jamais modifié (le
rangement est gardé dans la configuration de l'application, pas dans le script). Supprimer un dossier avec son
contenu supprime les scripts créés par le constructeur, mais se contente de désranger (retour à la racine, sans le
toucher) un script écrit à la main.

Le rangement en dossiers reste possible sans glisser-déposer : le bouton <i class="fa-solid fa-folder-tree"></i> de
chaque carte fait la même chose. Réordonner manuellement les scripts (au-delà du rangement en dossiers) n'est pas
encore possible : l'ordre reste alphabétique dans chaque dossier.

## Script hérité d'une version antérieure : introuvable

Si un script créé avec une version antérieure de l'application (qui rangeait vraiment dans des sous-dossiers, ex.
`Script\Adobe\Photoshop.ps1`) donnait une erreur « Could not find file » en cliquant Modifier/Tester/Dupliquer alors
qu'« Afficher dans l'Explorateur » le trouvait bien : c'est corrigé depuis la v1.6.0. L'application retrouve maintenant
le fichier où qu'il soit, et le **déplace automatiquement à plat** (son emplacement normal) dès que tu le modifies,
le dupliques ou le ranges dans un dossier.

## Version affichée

Le numéro de version (ex. « v1.5.0 ») s'affiche en petit à côté du titre « Script Builder ». Compare-le à celui que je
t'indique à chaque livraison pour confirmer que tu utilises bien le dernier build (utile si tu recompiles ou republies
toi-même l'application, ou si l'ancienne fenêtre était restée ouverte).

## Dossier de travail des programmes (ex : OBS)

Quand tu ne précises pas de dossier de travail pour une action « Ouvrir un programme », il est maintenant réglé
automatiquement sur le **dossier du programme lui-même** plutôt que sur celui du serveur — beaucoup de programmes
(dont OBS) en ont besoin pour retrouver leurs propres fichiers (locale, ressources…). Si OBS te disait
« Failed to find locale/en-US.ini », re-génère le script (rouvre-le et réenregistre-le) : c'est corrigé.

## Compatibilité avec un script rangé à la main dans un sous-dossier

`server.ps1` a un filet de sécurité : si un nom simple (sans « / ») n'est pas trouvé directement dans `Script`, il cherche
aussi dans les sous-dossiers et prend le premier `<nom>.ps1` trouvé. Ça couvre les scripts que tu ranges toi-même dans
l'Explorateur, ou ceux créés par une version antérieure de Script Builder qui utilisait de vrais sous-dossiers.

## Premier lancement

1. **Dossier du serveur** : choisis le dossier qui contient `server.ps1` et `Script` (bouton « Le chercher
   automatiquement » possible). L'application vérifie la présence de `NfcNative.ps1` (volume / média) et
   `SpotifyHelper.ps1` (Spotify).
2. **Nouveau script** : donne un nom (c'est le nom à saisir dans le bouton *Script* du Stream Deck), ajoute des actions.
3. **Enregistrer** (ou **Enregistrer et tester** : le script s'exécute vraiment sur le PC, la sortie s'affiche).

## Les actions

| Action | Ce qu'elle fait |
|---|---|
| Ouvrir un programme | `.exe` / `.bat` / raccourci avec arguments, dossier de travail, administrateur ; ou un fichier, un dossier, une adresse (`steam://…`). Option « ne pas relancer s'il tourne déjà », puis gestion de la fenêtre : écran, maximisée, plein écran (F11), réduite, devant. |
| Ouvrir un site web | Nouvelle fenêtre Chrome / Edge / Brave (même si le navigateur est ouvert), placée sur l'écran voulu, maximisée ou en plein écran. |
| Gérer une fenêtre | Déplace / maximise / plein écran / réduit un programme déjà lancé. |
| Attendre | Pause en secondes. |
| Fermer un programme | Par nom de processus, avec ou sans arrêt forcé. |
| Régler le volume | Niveau, muet, son (via `NfcNative.ps1`). |
| Touche média | Lecture/pause, suivant, précédent, stop. |
| Lancer Spotify | Titres likés ou playlist, appareil (via `SpotifyHelper.ps1`). |
| Envoyer des touches | `SendKeys` (`{F11}`, `^s`…). |
| Code PowerShell | Tes propres lignes, insérées telles quelles. |

Les blocs longs sont découpés en **onglets** (Action / Sécurité / Fenêtre) et se **replient** (chevron, ou « Tout replier »).
L'aperçu du script se replie aussi. Pour « Arguments », le bouton **Suggestions** propose les options courantes du
programme choisi (Steam, Chrome, Edge, VLC, OBS, Spotify…).

Chaque action peut être **désactivée** (elle reste dans le script, en commentaire), déplacée, dupliquée, supprimée.
Les erreurs de saisie s'affichent sur la carte de l'action ; l'aperçu du code se met à jour en direct.

## Zones de travail

Une zone de travail est une entité à part — **pas un dossier**. Elle *contient* des dossiers (et des scripts de
premier niveau), un peu comme des tiroirs qui regroupent des dossiers. Il y a toujours une zone « Par défaut » :
au départ, tout y vit ; tu peux ensuite ranger des dossiers ou des scripts de premier niveau dans une nouvelle zone
avec le bouton <i class="fa-solid fa-layer-group"></i> « Déplacer vers une zone de travail » (sur les éléments de
premier niveau uniquement — un élément niché dans un dossier suit toujours la zone de son ancêtre, pas la sienne).

La barre d'onglets en haut de la page (toujours visible, pas seulement à la racine) permet de basculer entre les
zones à tout moment. Créer, renommer, personnaliser (icône, couleur) et supprimer une zone se font depuis cette
barre. Supprimer une zone ne supprime jamais son contenu : tout revient dans « Par défaut ».

## Personnaliser l'icône et la couleur

Bouton <i class="fa-solid fa-palette"></i> sur chaque carte (script ou dossier) : une palette de couleurs et une
sélection d'icônes, ou « automatique » pour revenir à l'icône déduite du contenu. Pour un script écrit à la main ou
un dossier (virtuel), tout est gardé dans la config, sans jamais toucher au fichier.

## Rangement automatique

Bouton <i class="fa-solid fa-arrow-down-a-z"></i> « Trier » dans la barre d'outils : range par ordre alphabétique
le dossier affiché (les dossiers d'abord, puis les scripts). N'agit que dans le dossier courant — les autres
gardent l'ordre choisi à la main.

## La vraie carte suit le curseur (pas une étiquette de texte)

Corrigé : l'étiquette qui suivait le curseur pendant un glissement s'est retrouvée figée en place à la version
précédente (le JS cherchait un élément qui n'existait pas encore au moment où il le cherchait). Plutôt qu'un
correctif ponctuel, le geste est repensé comme dans la plupart des applications : la **carte elle-même** suit le
curseur en temps réel (une copie visuelle, légèrement soulevée et penchée), pendant que l'originale reste à sa
place, juste atténuée — la grille ne bouge pas sous les pieds pendant le geste.

## Cartes de taille uniforme

Les cartes réservent maintenant toutes la même hauteur minimale, et les boutons d'action sont plus compacts :
avant, certaines cartes avec peu de contenu (des dossiers) paraissaient très différentes des cartes avec
beaucoup de boutons (des scripts), et ces derniers pouvaient déborder sur une seconde ligne selon la largeur
disponible. Colonnes légèrement élargies en plus, pour que 3-4 boutons tiennent toujours sur une seule ligne.

## Glisser-déposer directement sur un onglet de zone de travail

Un dossier ou un script de premier niveau, tenu par sa poignée, peut être déposé directement sur un onglet de zone
de travail — pas besoin de passer par le bouton dédié. L'onglet s'allume à l'approche uniquement si l'élément tenu
peut vraiment y être rangé (premier niveau) ; sur un élément niché, l'onglet ne réagit pas.

## Fluidité pendant le glissement

Le curseur qui traîne une carte est maintenant piloté directement par le navigateur, plus par Blazor à chaque
pixel de mouvement — un rendu complet de toute la page à chaque déplacement de souris coûtait cher et donnait une
impression de ralentissement. Seuls les passages d'une zone à une autre (bien moins fréquents) déclenchent encore
un rendu.

## Menu « plus d'actions »

Les actions les moins fréquentes (personnaliser, renommer, déplacer, dupliquer, afficher dans l'Explorateur,
supprimer) sont regroupées derrière l'icône <i class="fa-solid fa-ellipsis-vertical"></i> de chaque carte, pour ne
plus surcharger l'affichage. Les actions du quotidien (modifier/ouvrir, tester, activer/désactiver) restent
visibles directement.

## Activer / désactiver un script ou un dossier (vraiment, côté serveur)

Chaque carte (script ou dossier) a un bouton <i class="fa-solid fa-toggle-on"></i> : désactivé, **le serveur refuse
de le lancer**, même appelé directement depuis Stream Deck — ce n'est pas qu'un réglage cosmétique dans
l'application. Le script reste sur le disque, juste marqué comme refusé (une petite liste,
`Script\.sdb-disabled.txt`, à côté de `server.ps1`) ; il suffit de réactiver pour qu'il refonctionne. Désactiver un
dossier désactive tous les scripts qu'il contient (et ses sous-dossiers) d'un coup, et un nouveau script créé
dedans hérite automatiquement de la coupure.

## Fermer une page précise du navigateur (pas tout le programme)

L'étape « Fermer » propose deux cibles :
- **Un programme entier** (comme avant) : ferme toutes ses fenêtres par son nom de processus.
- **Une page précise du navigateur** : cible par le **titre de la fenêtre** (le texte de l'onglet), pas par
  l'adresse — Chrome et Edge n'exposent pas l'URL au niveau d'une fenêtre Windows, seulement son titre. Ne marche
  que sur une page ouverte en nouvelle fenêtre (ce que fait déjà l'étape « Ouvrir un site web ») : un onglet dans une
  fenêtre existante n'a pas de fenêtre à lui pour être fermé individuellement.

## Glisser-déposer, entièrement refait (v3)

Trois défauts corrigés à la racine :

1. **Un vrai bug de classement** : les dossiers et les scripts étaient départagés en comparant le *chemin complet*
   d'un dossier (« Jeux/Steam ») au *nom nu* d'un script (« Soirée Steam ») — deux choses non comparables, qui
   produisaient un ordre incohérent selon le dossier où se trouvait l'élément. Corrigé : le départage se fait
   toujours sur le nom affiché, jamais sur le chemin.
2. **L'ancien système ne savait dire que « avant » cette carte** — jamais « après ». Impossible d'exprimer
   « à la toute fin de la liste ».
3. **La v2 (zones pleine largeur entre les cartes) cassait la grille** : en vue cartes, chaque zone forçait une
   ligne entière, réduisant l'affichage à une seule carte par rangée. Corrigé en repensant complètement le
   placement : plus aucune zone ne force de saut de ligne.

Le système actuel : chaque carte a deux fines bandes invisibles sur ses bords gauche et droit (visibles seulement
pendant un glissement actif) — gauche = se positionner **avant** cette carte, droite = **après**. Pour un dossier,
le centre de la carte reste la cible « ranger **dedans** » (comme avant), la carte entière s'allumant en orange.
Ces bandes sont positionnées à l'intérieur de chaque carte plutôt qu'entre les cartes : la grille à plusieurs
colonnes reste intacte, quel que soit le nombre de colonnes.

Dossiers et scripts partagent un **ordre commun** : plus de bloc « dossiers d'abord, scripts ensuite », ils se
mélangent librement selon l'ordre choisi. Un nouveau script ou dossier rejoint la fin, pas le début. Les cartes ont
aussi été retravaillées : poignée discrète qui ne s'affiche pleinement qu'au survol, légère élévation au survol et
pendant le glissement. Rien de tout ça ne touche jamais au fichier d'un script écrit à la main (sa position est
gardée dans la config).

## Variables de script ({season}, {episode}...)

Une URL (« Ouvrir un site web ») ou des arguments (« Ouvrir un programme ») peuvent contenir `{nom}` — une variable
que le Stream Deck réglera au moment d'appeler le script. Dès qu'une `{accolade}` apparaît dans le texte d'une
étape, elle est proposée en un clic dans la section **Variables**, en haut de l'éditeur ; choisis son type (Nombre,
avec des bornes min/max, ou Texte, avec une longueur max) pour la déclarer pour de bon. Une variable jamais
utilisée dans le texte d'aucune étape reste sans effet (avertissement affiché, rien ne casse).

Côté génération : un bloc `param()` apparaît en tête du script (un par variable déclarée), et chaque `{nom}` du
texte devient `$nom` — une vraie variable PowerShell, substituée à l'exécution. Un script qui ne déclare aucune
variable n'a aucun bloc `param()` : sa forme ne change pas.

Le réglage de chaque variable pour un bouton donné (valeur fixe, incrémentée automatiquement, ou demandée juste
avant de lancer) se fait côté Stream Deck, dans l'éditeur du bouton — pas ici.

## Envoyer des touches : clavier visuel

Le bouton **Clavier visuel** (à côté du champ « Touches ») ouvre un clavier cliquable : Ctrl / Alt / Maj à cocher,
puis la touche à appuyer avec (F1-F12, lettres, chiffres, flèches, Entrée, Tab…). Plus besoin de connaître ou taper
la syntaxe SendKeys (`^`, `%`, `+`, `{F5}`…) — chaque clic ajoute une combinaison, plusieurs combinaisons peuvent se
suivre (ex. Ctrl+S puis Entrée), et le résultat s'affiche en direct avant de valider. Le champ texte reste disponible
si tu préfères taper directement. La touche Windows ne peut pas être envoyée par ce mécanisme (limitation de
SendKeys, pas de l'application).

## Basculer (marche / arrêt) : « type condition »

Le type d'action **Basculer** répond à un besoin classique : *premier appui sur le bouton → une suite d'actions,
appui suivant → une autre*. Exemple concret (modèle fourni) : ouvrir OBS et démarrer l'enregistrement, puis à l'appui
suivant, l'arrêter.

- Chaque branche (« Au premier appui » et « À l'appui suivant ») contient sa propre suite d'actions, avec les mêmes
  blocs que partout ailleurs (ouvrir un programme, envoyer des touches, attendre…). Une branche peut rester vide.
- **L'état est mémorisé entre deux appuis**, y compris après un redémarrage du PC : chaque appui relance un nouveau
  processus PowerShell (rien ne peut rester "en mémoire" d'un appui à l'autre), donc l'état est gardé dans un petit
  fichier-témoin, dans un dossier `.toggle-state` à côté de `Script`. Il n'y a rien à configurer : c'est automatique.
- Pour la bascule d'enregistrement OBS, configure d'abord dans OBS un raccourci clavier global pour « Start/Stop
  Recording » (Réglages → Raccourcis clavier) — c'est ce raccourci que le script enverra à chaque appui (OBS gère
  lui-même le start/stop selon son propre état). Le modèle propose une action « Envoyer des touches » à compléter
  avec ce raccourci dans les deux branches (souvent le même : c'est la bascule d'OBS elle-même qui fait le travail).
- Imbrication limitée à un niveau : une branche ne peut pas contenir un autre Basculer.

## Macro (souris + clavier)

Le type d'action **Macro** enregistre des mouvements de souris, des clics et des touches au clavier — n'importe où
sur le PC, pas seulement dans cette application — puis les rejoue à l'identique quand le bouton du Stream Deck est
appuyé.

- **Enregistrer** : règle d'abord la durée maximale (secondes), la touche qui arrête l'enregistrement avant cette
  durée (F9 par défaut), et si les déplacements de souris doivent être capturés (désactive-le pour un script plus
  léger si seuls les clics et les touches comptent). Clique **Enregistrer**, puis change de fenêtre et fais les
  actions à répéter normalement — tout est capturé pendant que l'enregistrement est en cours. Appuie sur la touche
  d'arrêt (ou attends la durée maximale) pour terminer ; elle-même n'est jamais incluse dans le résultat, mais
  continue d'atteindre normalement le programme qui a le focus à ce moment-là.
- **Annuler une relecture en cours** : une touche séparée (Échap par défaut, réglable) est vérifiée en permanence
  pendant que le script rejoue la macro — appuie dessus à tout moment pour l'arrêter, où qu'en soit la relecture
  (vérifiée toutes les 20 ms environ, donc quasi immédiat).
- Les coordonnées de souris enregistrées sont **absolues** (position à l'écran) : la macro suppose la même
  résolution/configuration d'écrans qu'au moment de l'enregistrement. Si seuls des clics à des endroits fixes d'une
  fenêtre comptent, enregistre avec cette fenêtre à la même taille/position qu'au moment de la relecture.
- Techniquement : le script généré appelle directement `user32.dll` (`SetCursorPos`, `mouse_event`, `keybd_event`) —
  les mêmes fonctions Windows que n'importe quel logiciel de macro. Rien à installer côté serveur.
- **Arrêter si la souris est déplacée manuellement** (option, désactivée par défaut) : utile surtout pour une macro
  qui déplace elle-même la souris. Activée, la relecture compare en permanence la position réelle de la souris à
  celle que la macro vient elle-même de placer (tolérance de quelques pixels) — reprendre la main dessus pendant la
  relecture l'arrête aussitôt, sans avoir besoin d'appuyer sur la touche d'annulation. Sans effet pour une macro qui
  ne fait que des clics ou des touches (rien à surveiller).

**Limite à connaître** : l'enregistrement a besoin d'observer la souris et le clavier partout sur le PC (« hooks »
globaux Windows) — disponible seulement dans **cette** application (StreamDeck.ScriptBuilder.Windows), jamais dans
le navigateur ni ailleurs. Certains antivirus peuvent avertir sur ce type d'installation (association habituelle
avec les key loggers, même si l'usage ici est légitime et local).

## Planification (déclenchement automatique par le serveur)

Un script peut se déclencher **tout seul**, sans passer par le Stream Deck : ouvre-le dans l'éditeur et ajoute une
planification (juste au-dessus des actions, une fois le script enregistré au moins une fois).

- **Toutes les X minutes** : redéclenché à intervalle régulier, avec en option une plage horaire active dans la
  journée (ex : de 08:00 à 20:00 — laisse les deux champs vides pour toute la journée).
- **À heures fixes** : une ou plusieurs heures précises (HH:mm), une par ligne.
- **Répéter** : le script peut être relancé plusieurs fois de suite à chaque déclenchement, avec une attente entre
  chaque — utile pour une macro qui doit tourner plusieurs fois d'affilée toutes les 30 minutes, par exemple.
- Une planification désactivée ou pointant vers un script désactivé/supprimé est simplement ignorée par le serveur.

Techniquement : les règles vivent dans `Schedules.json`, à côté de `server.ps1` — c'est **le serveur qui vérifie et
déclenche**, entre deux requêtes HTTP (toutes les 15 secondes environ), donc cette application n'a pas besoin d'être
ouverte pour qu'une planification fonctionne, seul le serveur (déjà lancé en permanence sur le PC) doit tourner.
Renommer un script dans l'éditeur met à jour ses planifications automatiquement ; le supprimer les enlève aussi.

**Limite à connaître** : comme pour l'affichage plein écran et le reste de `server.ps1`, cette partie du serveur n'a
pas pu être testée sur un vrai Windows dans l'environnement où elle a été écrite — vérifiée seulement par relecture
attentive et par une analyse syntaxique réelle du PowerShell généré. Teste une planification avec un intervalle
court (1-2 minutes) la première fois, pour t'assurer que tout se comporte comme attendu avant de t'appuyer dessus.

## Raccourcis clavier globaux (Réglages)

Un raccourci clavier peut :
- **Lancer un script** (choisi dans la liste) — comme un appui sur le Stream Deck.
- **Arrêter toutes les macros** en cours de relecture, tous scripts confondus, tout de suite.
- **Redémarrer le serveur.**

Ces raccourcis sont **globaux** : ils marchent même si cette application n'a pas le focus, à condition qu'elle
tourne (réduite dans la barre des tâches, ça suffit). Clique sur « Cliquer pour définir » puis appuie sur la
combinaison voulue (Échap annule). Une combinaison déjà utilisée par Windows ou un autre logiciel installé reste
simplement sans effet ; essaie-la après l'avoir réglée pour vérifier qu'elle marche vraiment.

Le bouton **Arrêter les macros** est aussi disponible directement dans la bibliothèque, sans passer par un raccourci
(pratique pour une macro qui déplace la souris et qu'il faut interrompre tout de suite).

## Raccourcis rapides dans l'éditeur

Toujours dans les Réglages : une seconde liste de raccourcis, cette fois pour **ajouter directement une action**
pendant que tu construis un script (au lieu de passer par le menu « Ajouter une action »). Ceux-ci ne marchent que
pendant que l'éditeur a le focus — pas besoin qu'ils fonctionnent partout sur le PC pour construire un script.

**Limite à connaître** : les raccourcis globaux dépendent de `RegisterHotKey` (Windows) via une fenêtre invisible
dédiée — comme pour l'enregistrement de macro, ce code n'a pas pu être testé sur un vrai Windows dans l'environnement
où il a été écrit (pas de machine Windows disponible). Vérifié seulement par relecture attentive et par une
vérification de syntaxe/signatures réelle. Teste chaque raccourci après l'avoir réglé.

## Widget de bureau

> **v1.34.0 — déménagé hors des Réglages, vers un onglet dédié de la page principale.** Tout ce qui suit (apparence,
> boîtes mail, espaces de travail/pages, touches épinglées) se règle maintenant depuis l'onglet **« Widget »** en haut
> de la page principale (à côté de l'onglet « Scripts », qui affiche la bibliothèque comme avant) — plus dans
> Réglages, qui ne garde que les raccourcis clavier et le dossier du serveur. Les mentions « Réglages -> … » ci-dessous
> qui concernent le widget désignent maintenant cet onglet.
>
> **Sélection des pages en grille de cartes.** Les pages d'un espace de travail s'affichent maintenant comme des
> cartes (icône, nom, résumé du contenu en badges), avec les mêmes codes visuels que la bibliothèque de scripts
> (dossiers/scripts), plutôt qu'une liste à accordéon — clique sur « Modifier » sur une carte pour composer son
> contenu.
>
> **Une page = une liste de blocs, plusieurs du même type possibles.** Avant cette version, chaque type de contenu
> (horloge, contrôles serveur, courrier, touches) ne pouvait apparaître qu'une seule fois par page. Une page est
> maintenant une **liste ordonnée de blocs** : ajoute autant de blocs que tu veux, y compris plusieurs du même type —
> deux boîtes mail différentes l'une sous l'autre, puis des touches, puis encore du courrier, par exemple — le bouton
> « Ajouter » (dans la fenêtre « Modifier » d'une page) propose toujours les quatre types, sans jamais en masquer un
> déjà présent. Des flèches haut/bas repositionnent chaque bloc dans la page. Une configuration d'avant cette version
> (un seul bloc de chaque type par page) est migrée automatiquement au premier lancement, sans rien perdre.
>
> **Touches épinglées : propres à chaque bloc, plus de catalogue partagé (v1.34.1).** Chaque **bloc** de type
> « Touches » crée, modifie et retire maintenant **directement ses propres touches** (script, icône/couleur ou image,
> case « image seule ») — plus de liste globale à cocher/décocher depuis un catalogue partagé. Deux blocs de touches,
> sur la même page ou sur des pages différentes, ont donc toujours des touches complètement indépendantes. Retirer une
> page ou un bloc « Touches » supprime aussi les touches qu'il possédait exclusivement, pour ne jamais en laisser
> d'orphelines dans les réglages.
>
> **Vue « Mise en forme » par défaut à l'ouverture d'un message.** Cliquer sur un message ouvre maintenant directement
> la vue « Mise en forme » (citations `>` repliées, voir plus bas) plutôt que « Détails » (texte brut) — la flèche
> retour, depuis cette vue, ferme le message ; depuis « Détails » ou « Répondre », elle revient à « Mise en forme »
> plutôt que de fermer directement.
>
> **v1.34.1 — cinq changements, en réponse directe au retour reçu :**
> 1. **Apparence du widget et boîtes mail : retour dans les Réglages.** Seule la composition des pages (espaces de
>    travail, pages, blocs) reste dans l'onglet « Widget » — l'apparence (taille, style, police, coins arrondis,
>    toujours au premier plan) et la configuration des boîtes mail (IMAP/SMTP) sont revenues dans les Réglages,
>    section « Widget de bureau » et « Boîtes mail ». Le visuel en grille de cartes de la v1.34.0, jugé pas adapté à
>    ces réglages-là, ne s'applique donc plus qu'à la composition des pages.
> 2. **Plus de catalogue global de touches épinglées.** L'ancien réglage « Scripts épinglés » (une liste unique,
>    piochée par chaque bloc « Touches ») a disparu. Chaque bloc « Touches » d'une page **crée, modifie et retire
>    maintenant directement ses propres touches**, plutôt que de cocher des cases dans une liste partagée — retirer
>    une page ou un bloc « Touches » supprime aussi les touches qu'il possédait, pour ne jamais en laisser d'orphelines.
> 3. **Images des mails enfin affichées correctement.** Un message avec une version HTML (la plupart des mails
>    « pro » — notifications, newsletters…) s'affichait avant uniquement en texte brut, ce qui pouvait donner un mur
>    de liens de suivi bruts illisibles plutôt qu'un message propre. La vue « Mise en forme » affiche maintenant cette
>    version HTML quand elle existe, avec les images intégrées (`cid:`) correctement récupérées et affichées — un
>    nettoyage volontairement prudent retire les balises actives (scripts, cadres, gestionnaires d'évènements…) avant
>    affichage, mais ce n'est **pas un nettoyage HTML certifié/complet** (aucune bibliothèque d'analyse HTML n'est
>    disponible dans l'environnement où ce code a été écrit) : à base d'expressions régulières, donc raisonnablement
>    sûr pour du courrier ordinaire, sans garantie absolue face à un contenu spécifiquement conçu pour le contourner.
> 4. **Écrire un nouveau message**, pas seulement répondre — un bouton dédié (icône crayon) apparaît à côté du bouton
>    d'actualisation de chaque bloc « Courrier », et ouvre une fenêtre de composition avec destinataire et sujet
>    éditables (plutôt que préremplis depuis un message ouvert). Voir plus bas.
> 5. **Programmer l'envoi d'un message**, avec une zone de notifications pour les envois programmés qui échouent
>    (pas de connexion à l'heure prévue, par exemple) — renvoi manuel ou modification possible depuis cette zone.
>    Voir plus bas. **Important** : il n'y a pas de service Windows séparé qui tourne en permanence — un envoi
>    programmé ne part que si le widget (ou l'application) est ouvert au moment prévu ; sinon il part au prochain
>    lancement (rattrapage), ou reste en échec si ça ne fonctionne toujours pas à ce moment-là.

Réglages -> **Ouvrir le widget** : une petite fenêtre séparée, **sans bordure ni barre de titre**, avec l'heure, la
date, un accès rapide au serveur (éteindre/rallumer, redémarrer, arrêter les macros), un bouton pour rouvrir le
constructeur de scripts, et des **touches** — mêmes tuiles (icône + couleur) que sur l'application Stream Deck —
pour lancer directement des scripts épinglés. Sa position est mémorisée pour la retrouver au même endroit au
prochain lancement.

**Puisqu'il n'a pas de barre de titre**, tout se fait depuis le widget lui-même :
- **Glisser** : depuis TOUTE la zone du haut (l'heure et les boutons) — pas besoin d'une petite poignée dédiée,
  seuls les boutons eux-mêmes (épingle, ouvrir, réduire, fermer) ne déclenchent pas de glissement.
- **Réduire / fermer** : les deux boutons à droite de cette zone.
- **Toujours au premier plan** : l'épingle, dans la même zone (aussi réglable dans les Réglages).

**Affichage de l'horloge et des contrôles serveur (v1.33.0 : plus de réglage général du tout)** : il n'y a plus
d'interrupteur « toujours affiché » séparé — l'horloge et les contrôles serveur ne s'affichent que si une page les
ajoute explicitement, exactement comme le courrier ou les touches épinglées (voir « Page = son propre contenu » plus
bas). **Corrige le bug remonté** où désactiver les contrôles ne les faisait jamais vraiment disparaître (l'horloge et
les 3 boutons serveur restaient fixés en haut malgré tout) : la v1.32.0 avait déjà remplacé l'ancien réglage à trois
positions par un simple interrupteur global, mais il restait un réglage « général » séparé des pages, source de
confusion — retiré à son tour. Une configuration où l'horloge/les contrôles étaient « toujours affichés » est migrée
automatiquement au premier lancement vers une page dédiée, pour ne rien perdre de ce qui s'affichait avant.

**Courrier du jour** : configure une ou plusieurs boîtes mail (Réglages -> Boîtes mail, serveur IMAP + identifiant +
mot de passe) pour voir le courrier reçu aujourd'hui directement dans le widget — actualisation automatique (fréquence
réglable en minutes, Réglages -> Boîtes mail -> « Actualisation automatique du courrier », 5 minutes par défaut), ou à
la demande avec le bouton dédié. Le mot de passe est protégé (DPAPI, lié à ton compte Windows) plutôt que stocké en
clair. Pour Gmail (et la plupart des services avec double authentification), il faut un **mot de passe d'application**,
pas ton mot de passe habituel — à créer depuis les paramètres de sécurité de ton compte. Si le serveur ressemble à
Gmail (`imap.gmail.com` ou tout hôte contenant « google ») et que l'authentification échoue, le widget affiche
directement ce message-là (« Google refuse ce mot de passe... ») plutôt que le message d'erreur brut renvoyé par le
serveur, pour éviter de confondre l'alerte de sécurité envoyée par mail par Google avec une vraie erreur applicative.

> **Important — ce n'est plus purement en lecture seule (v1.32.0).** Consulter la liste et lire un message restent en
> lecture seule (rien n'est marqué comme lu). Mais **mettre en favori et supprimer un message (voir plus bas) écrivent
> maintenant réellement sur le serveur** : favori pose/retire le fanion IMAP standard `\Flagged` (l'équivalent de
> l'étoile Gmail et de la plupart des clients mail) ; supprimer pose `\Deleted` puis vide (« expunge ») — sur Gmail et
> la plupart des serveurs, ça déplace le message vers la Corbeille plutôt que de l'effacer définitivement, mais ce
> n'est pas garanti pour tous les fournisseurs IMAP. Ces deux actions ouvrent la boîte en lecture-écriture plutôt qu'en
> lecture seule, contrairement à tout ce qui précède.

- **Une page de courrier par boîte, au choix** : une page « Courrier » (voir les espaces de travail plus bas) peut
  être réglée sur une boîte mail précise plutôt que de toutes les regrouper — utile pour séparer une boîte perso d'une
  boîte pro sur deux pages différentes, dans l'ordre qui t'arrange.
- **Favori, archivage et suppression directement sur la ligne** : trois petits boutons (étoile / archive / corbeille)
  apparaissent sur chaque message de la liste, sans avoir à l'ouvrir — voir l'avertissement ci-dessus, ces trois
  actions écrivent sur le serveur. **Non vérifié avec un vrai serveur IMAP.**
- **Filtre d'affichage (v1.34.3)** : un 3ᵉ bouton (icône entonnoir), à côté d'Actualiser et Nouveau message, ouvre un
  petit menu — Aujourd'hui / 7 derniers jours / 30 derniers jours / Favoris / Archivés. Contrairement à IMAP qui n'a
  pas de notion standard d'« archivé » (à la différence de `\Flagged` pour favori), l'archivage est ici posé comme un
  **mot-clé (« keyword ») personnalisé** `Archived` sur le message plutôt qu'un fanion standard — une pratique courante
  mais propre à ce widget, pas un dossier « Archives » standard IMAP ni celui d'un client mail existant (Gmail,
  Outlook…) ; un message archivé ici ne sera pas forcément reconnu comme tel par un autre client. La vue « Aujourd'hui
  / période » exclut désormais les messages archivés (ils ne disparaissent pas simplement, ils passent dans la vue
  Archivés). **Non vérifié avec un vrai serveur IMAP** — les mots-clés personnalisés sont supportés par la plupart des
  serveurs modernes (dont Gmail) mais pas garantis universellement, contrairement aux fanions standards.
- **Lire le message complet, dans une vraie fenêtre superposée** : cliquer sur un message dans le widget ouvre son
  contenu complet (texte) dans un popup qui recouvre proprement le reste du widget — une reconnexion IMAP a lieu à ce
  moment-là (le contenu complet de 30 messages par boîte n'est pas gardé en mémoire à l'avance, trop lourd). **Corrige
  le bug remonté** où ce popup avait un fond transparent et se retrouvait à chevaucher visuellement le contenu en
  dessous plutôt que de s'afficher correctement par-dessus — c'était un fond CSS `inherit` qui récupérait la couleur
  du mauvais élément parent (transparent) plutôt que la couleur de fond réelle du widget ; corrigé en donnant à ce
  popup un fond opaque explicite, adapté à chaque style de widget (sombre/clair/verre/accent/personnalisé). **Non
  vérifié avec un vrai serveur IMAP**, mais vérifié visuellement dans le code CSS.
- **Trois écrans distincts pour un message (v1.33.0)**, plutôt que tout empilé dans la même vue :
  - **Détails** : ce qui s'ouvre au clic sur un message — objet, expéditeur, texte tel que reçu, et deux boutons,
    « Mise en forme » et « Répondre ».
  - **Mise en forme** : une vue pensée pour les longs fils de discussion, pleins de citations `>`/`>>`/`>>>` qui
    rendaient le texte illisible (remonté explicitement) — le dernier message reste affiché normalement, et tout ce
    qui est cité (n'importe quel niveau d'imbrication `>`) est replié sous « Message(s) précédent(s) cité(s) », à
    déplier au besoin plutôt que de tout lire d'un bloc.
  - **Répondre** : une fenêtre de composition à part (SMTP, si activé pour cette boîte — Réglages -> Boîtes mail ->
    section « Envoi (SMTP) » d'un compte, réutilise le même identifiant/mot de passe que la réception IMAP), plutôt
    qu'une zone de texte ajoutée sous le message déjà affiché. La réponse part avec les en-têtes standards
    `In-Reply-To`/`References` pour rester rattachée au fil de discussion dans la plupart des clients mail.
  **Non vérifié avec un vrai serveur IMAP/SMTP**, comme tout le code mail de cette section — écrit d'après l'API
  documentée de MailKit/MimeKit, jamais exécuté ici pour de vrai ; la mise en forme des citations, elle, ne dépend
  d'aucun serveur et a été vérifiée par relecture du code.
- **Messages HTML avec images intégrées, correctement affichés (v1.34.1)** : la vue « Mise en forme » affiche la
  version HTML du message quand il en a une (la plupart des mails « pro »), plutôt que son texte brut — qui pouvait
  se réduire à un mur de liens de suivi bruts et illisibles pour ce genre de message. Les images intégrées (`cid:`,
  la convention pour les images qui font partie du message plutôt que d'être chargées depuis internet) sont
  récupérées et affichées directement. Un nettoyage retire d'abord les balises actives (`<script>`, `<iframe>`,
  gestionnaires `on...=`, liens `javascript:`…) — **pas un nettoyage HTML certifié/complet** (aucune bibliothèque
  d'analyse HTML disponible dans l'environnement où ce code a été écrit, donc une approche par expressions
  régulières plutôt qu'un vrai analyseur), suffisant pour du courrier ordinaire mais sans garantie absolue face à un
  contenu conçu spécifiquement pour le contourner. Un message sans version HTML continue de s'afficher comme avant
  (texte brut, citations repliées).
- **Écrire un nouveau message (v1.34.1)**, pas seulement répondre à un message déjà reçu : le bouton crayon à côté du
  bouton d'actualisation d'un bloc « Courrier » ouvre une fenêtre de composition avec destinataire et sujet éditables
  (contrairement à « Répondre », qui les préremplit depuis le message ouvert) ; le compte d'envoi se choisit
  explicitement si plusieurs boîtes ont le SMTP activé. Grisé, avec une explication, si aucune boîte n'a l'envoi
  configuré.
- **Programmer l'envoi d'un message (v1.34.1)** : dans la même fenêtre de composition, une case « Programmer l'envoi »
  fait apparaître un sélecteur de date/heure — le message est alors enregistré plutôt qu'envoyé tout de suite, et
  envoyé automatiquement dès que cette date/heure est atteinte. Vérifié au lancement du widget (rattrapage d'un envoi
  dont l'heure est déjà passée) puis toutes les minutes tant qu'il reste ouvert. **Il n'y a pas de service Windows
  séparé toujours actif** : un envoi programmé ne part donc que si le widget (ou l'application) tourne au moment
  prévu — sinon il part au prochain lancement, ou reste en échec (voir juste après) si ça ne fonctionne toujours pas.
- **Zone de notifications pour les envois programmés en échec (v1.34.1)** : la cloche dans l'en-tête du widget
  (avec un badge rouge indiquant le nombre en échec) ouvre la liste des envois programmés qui n'ont pas pu partir
  (pas de connexion à l'heure prévue, boîte mail supprimée depuis, etc.), avec le message d'erreur, et deux actions
  par message : **Renvoyer** (retente l'envoi tel quel, immédiatement) et **Modifier** (rouvre la fenêtre de
  composition, préremplie, pour corriger le contenu et/ou choisir une nouvelle date/heure — remplace l'ancien envoi
  programmé plutôt que d'en garder un doublon). Une icône corbeille permet aussi d'ignorer un envoi en échec sans le
  renvoyer. **Corrige le bug remonté (capture d'écran)** où cette bulle, ancrée à gauche de la cloche (proche du bord
  droit de l'en-tête), débordait hors de la fenêtre du widget plutôt que de rester entièrement visible — ancrée à
  droite depuis v1.34.2, et sa largeur est maintenant bornée à celle, réelle, de la fenêtre du widget.
- **Envoi (SMTP) en échec avec Infomaniak alors que Gmail fonctionne (v1.34.2, remonté explicitement)** : deux
  changements, écrits sans pouvoir reproduire le problème (pas d'accès à un vrai serveur Infomaniak depuis ce bac à
  sable) :
  1. Le message envoyé porte maintenant des en-têtes `Date` et `Message-Id` explicites plutôt que de compter sur
     MimeKit pour les compléter — certains serveurs plus stricts que Gmail sur la conformité du message peuvent
     rejeter un envoi qui ne les a pas.
  2. Le message d'erreur affiché est plus détaillé (inclut le détail de l'exception interne quand il y en a un), et
     un message dédié apparaît spécifiquement pour Infomaniak en cas d'échec d'authentification, avec deux pistes à
     vérifier : que l'adresse d'expéditeur configurée correspond exactement à l'adresse Infomaniak (pas un alias), et
     que l'envoi SMTP n'est pas bloqué séparément dans les réglages de sécurité du compte. **Si ça ne suffit pas**,
     le message d'erreur affiché cette fois-ci (avec son détail technique) sera nécessaire pour aller plus loin.
- **Présentation retravaillée** : chaque message affiche un rond avec l'initiale de l'expéditeur et l'heure de
  réception, en plus de l'expéditeur/objet.
- **Notification Windows au nouveau courrier** : une notification native s'affiche quand le widget détecte un nouveau
  message par rapport à la dernière vérification (rien n'est notifié au tout premier chargement, pour ne pas annoncer
  tout le courrier du jour comme "nouveau" à chaque ouverture du widget). **Non vérifié sur un vrai Windows** — voir la
  section native plus bas.

**Non vérifié avec un vrai serveur IMAP** dans l'environnement où ce code a été écrit (pas d'accès réseau sortant vers
un serveur mail depuis ce bac à sable) — écrit d'après l'API documentée de MailKit, jamais exécuté ici pour de vrai.

**Personnalisation** (Réglages -> Widget de bureau) :
- **Taille** : compacte, normale ou grande (trois tailles fixes), ou **redimensionnement libre** — une case à cocher
  qui permet de tirer les bords du widget à la souris pour choisir sa propre taille ; la dernière taille choisie est
  mémorisée pour la retrouver au prochain lancement, comme la position. **Non vérifié sur un vrai Windows.**
- **Style** : sombre, clair, verre (translucide), accent violet, ou **personnalisé** (couleur de fond et de texte au
  choix, avec un sélecteur de couleur).
- **Police** : système, mono (look « numérique »), élégante, ou **personnalisée** — importe ton propre fichier de
  police (`.ttf`, `.otf`, `.woff`, `.woff2`) via le sélecteur de fichiers Windows. Comme pour l'image d'une touche
  (voir plus bas), seul le chemin est gardé dans les réglages ; le fichier est lu et encodé à la volée pour
  l'affichage. **Non vérifié sur un vrai Windows.**
- **Coins arrondis** : arrondit la carte affichée, et la fenêtre elle-même (voir le correctif natif plus bas).
- **Colonnes par touches épinglées, propres à chaque page (v1.33.0)** : le nombre de colonnes (2 à 6) se choisit
  maintenant à l'endroit où les touches épinglées sont ajoutées à une page (Réglages -> Espaces de travail du widget),
  plutôt qu'un seul réglage général pour toutes les pages — deux pages différentes peuvent afficher une grille
  différente. Une configuration antérieure personnalisée est migrée automatiquement vers chaque page concernée.
- **Touches épinglées** : choisis des scripts à afficher comme des touches sur le widget (avec leur propre icône et
  couleur, comme pour un script ou un dossier dans la bibliothèque), pour les lancer en un clic sans passer par le
  Stream Deck. **Chaque touche peut aussi avoir sa propre image** (lien, ou import via le sélecteur de fichiers
  Windows — sans limite de poids, voir plus bas) à la place de l'icône — toujours affichée carrée, quelle que soit
  l'image. **Image seule** : une case à cocher par touche permet d'afficher uniquement l'image, bord à bord avec des
  coins arrondis, sans le fond/dégradé habituel de la tuile ni le nom du script en dessous. **Masquer une touche** :
  le bouton « œil » à côté de chaque touche la retire du widget sans la supprimer des réglages (pratique pour la
  ranger temporairement sans perdre sa configuration). Les touches épinglées, leur image et leur affichage se mettent
  à jour sur le widget dès que tu changes quelque chose dans les Réglages, sans avoir à le fermer/rouvrir.

**Image d'une touche épinglée : lien plutôt que copie.** Un fichier importé depuis le disque (bouton « Importer ») est
référencé par son chemin (une URI `file://`), pas copié ni encodé dans les réglages — donc pas de limite de poids
(l'ancienne limite de 700 Ko, liée à un encodage en base64, a été retirée). Le retour du dernier test a confirmé le
risque annoncé : le WebView2 qui affiche le widget refusait bien de charger le lien `file://` directement, l'image
restait blanche. Corrigé en lisant le fichier côté C# (pas de restriction là) et en le convertissant à la volée en
image affichable à chaque fois que la touche est dessinée — les réglages, eux, gardent bien juste le chemin (léger,
comme demandé), c'est seulement l'affichage qui est maintenant fiable. Point qui reste vrai : si le fichier est
déplacé, renommé ou supprimé, la touche perd son image (rien n'en garde de copie séparée).

**Ce qui est natif Windows dans cette version** :
- **Sans bordure** : l'API WinUI (`OverlappedPresenter.SetBorderAndTitleBar(false, false)`) et, en plus, la
  modification directe du style Win32 de la fenêtre (`GetWindowLong`/`SetWindowLong` sur `GWL_STYLE`, puis
  `SetWindowPos(..., SWP_FRAMECHANGED)`), appliquées ensemble sur l'évènement `Page.Loaded`. D'après le dernier
  retour, le widget s'affiche bien sans bordure.
- **Glisser sans barre de titre — retour en arrière après un échec sur un vrai Windows** : la version précédente
  remplaçait le déclenchement JS du glissement par une réponse directe de la fenêtre au niveau Windows
  (`WM_NCHITTEST`), en sous-classant le `WNDPROC` de la fenêtre. Résultat réel sur Windows : **plus aucun glissement
  possible**, pire que le défaut que ce changement devait corriger. Cause la plus probable : cette fenêtre héberge du
  contenu Xaml Islands (le `BlazorWebView`, via WinUI 3), et sous-classer son `WNDPROC` semble casser le pont
  d'acheminement des entrées que WinUI met en place pour ce contenu — une opération que WinUI 3 ne semble pas
  supporter, malgré une syntaxe P/Invoke par ailleurs correcte. **Cette version annule entièrement ce changement** et
  revient au mécanisme précédent, plus simple et confirmé fonctionnel : le JS déclenche le glissement au clic
  (`ReleaseCapture` + `SendMessage(WM_NCLBUTTONDOWN, HTCAPTION)`). Le défaut connu de ce mécanisme (il faut relâcher
  le clic pour que le glissement démarre) est donc de retour aussi — non réglé, après quatre tentatives en tout. Cette
  piste plus invasive (sous-classement de fenêtre) ne sera pas retentée sans un vrai moyen de tester sur Windows
  d'abord.
- **Bordure blanche en redimensionnement libre — toujours non résolue** : le correctif tenté en v1.29.0
  (interception de `WM_NCCALCSIZE`) dépendait du même sous-classement de fenêtre qui a cassé le glissement (voir
  ci-dessus) et a été retiré avec lui en v1.30.0. Cette version n'a pas retenté de correctif natif pour ce bug —
  volontairement, pour ne pas rejouer le même risque après la régression du glissement. Reste donc un défaut visuel
  connu, sans nouvelle piste pour l'instant qui n'implique pas de sous-classer la fenêtre.
- **Notification Windows au nouveau courrier** (`WindowsWidgetHost.NotifyNewMail`) : utilise
  `Microsoft.Windows.AppNotifications.AppNotificationManager` (Windows App SDK) — déjà présent dans ce projet MAUI
  Windows (comme les autres API WinUI utilisées ailleurs dans ce fichier), pas de nouveau paquet NuGet ajouté.
  **Non vérifié sur un vrai Windows.**
- **Coins arrondis, cette fois du bon côté** : le retour précédent montrait toujours un angle carré derrière
  l'arrondi (« comme un rectangle noir sous la fenêtre »). L'`overflow: hidden` CSS ajouté dans la version
  précédente ne pouvait pas régler ça : ce n'était pas un débordement visuel, mais la fenêtre Windows elle-même qui
  reste rectangulaire — `DwmSetWindowAttribute(..., DWMWA_WINDOW_CORNER_PREFERENCE, ...)` demande à Windows
  d'arrondir automatiquement, mais cet arrondi automatique semble ne plus s'appliquer une fois qu'on retire
  `WS_CAPTION`/`WS_SYSMENU` du style de la fenêtre (ce que ce widget fait pour supprimer sa bordure). Cette version
  ajoute donc un second mécanisme, plus direct : la forme de la fenêtre est découpée physiquement en rectangle
  arrondi (`CreateRoundRectRgn` + `SetWindowRgn`), avec un rayon ajusté à la résolution d'écran
  (`GetDpiForWindow`) et recalculé chaque fois que la fenêtre change de taille. **Non vérifié sur un vrai Windows**,
  mais c'est un mécanisme fondamentalement différent (et plus radical) que la tentative précédente, qui agit
  directement sur la forme réelle de la fenêtre plutôt que sur son contenu.
- **Ouverture automatique du widget au lancement (v1.34.2)** : `BuilderApp.CreateWindow` ouvre maintenant aussi le
  widget (`IWidgetHost.Open()`, résolu via le conteneur d'injection de dépendances MAUI) juste après avoir créé la
  fenêtre principale, protégé par un `try/catch` qui laisse l'application démarrer normalement même en cas d'échec.
  **Non vérifié sur un vrai Windows.**
- **Réduire / toujours au premier plan** : `OverlappedPresenter.Minimize()` / `IsAlwaysOnTop`.
- **Ramener au premier plan** : `AppWindow.MoveInZOrderAtTop()`.
- **Redimensionnement libre** : quand tu actives cette option, la fenêtre garde le style Win32 `WS_THICKFRAME`
  (celui qui permet de tirer les bords à la souris) au lieu de le retirer, `OverlappedPresenter.IsResizable` passe à
  `true`, et les limites min/max de taille s'élargissent (160 à 900 pixels) au lieu d'être fixées à une valeur unique.
  La dernière taille choisie est mémorisée et réappliquée à la prochaine ouverture. **Non vérifié sur un vrai
  Windows.**
- **Police personnalisée** : le fichier de police n'est pas copié dans l'application — comme pour les images de
  touche, seul son chemin est mémorisé (`file://...`), puis relu depuis le disque et transformé en `data:` URI au
  moment de l'affichage (avec mise en cache), car le WebView ne charge pas les polices en `file://` directement.
  Injectée dans le widget via une règle CSS `@font-face`. **Non vérifié sur un vrai Windows** — même famille de
  correctif que celui qui a réglé l'image de touche locale qui s'affichait en blanc, donc de bonnes chances que ça
  fonctionne, mais pas encore confirmé pour les polices spécifiquement.
- **Import d'image pour une touche** : le sélecteur de fichiers natif de Windows (le même que pour choisir le
  dossier du serveur), pas le composant web `InputFile` — celui-ci laissait l'application grisée sans appliquer
  l'image sur un vrai Windows. Réutilisé à l'identique pour l'import de police.

Autres nouveautés **côté web/CSS, pas natives** (donc plus fiables) :
- **Espaces de travail du widget** (remplace l'ancien réglage unique « Ordre des pages ») : les pages se groupent en
  **espaces de travail** nommés — comme les espaces/écrans d'accueil de l'application mobile — réglés dans Réglages ->
  Widget de bureau -> « Espaces de travail du widget » : ajoute un espace, nomme-le, choisis ses pages (avec des
  flèches haut/bas pour les réordonner), et bascule d'un espace à l'autre directement depuis le widget avec l'icône en
  haut à gauche de l'en-tête (à côté de l'épingle) — pas besoin de repasser par les Réglages. Une configuration
  d'avant cette version est migrée automatiquement au premier lancement.
- **Page = son propre contenu, librement composé (v1.32.0)** : chaque page décide elle-même ce qu'elle affiche —
  horloge, contrôles serveur (avec les boutons de son choix), courrier (d'une boîte précise ou de toutes) et touches
  épinglées (avec leurs propres colonnes, v1.33.0) peuvent tous être activés ensemble sur la même page, plutôt qu'un
  seul type par page comme avant. Une page sans rien dessus (« page vide ») n'apparaît simplement pas dans le
  carrousel du widget.
- **Pages nommées, repliées par défaut (v1.33.0)** : chaque page a maintenant un nom (modifiable, « Page 1 » par
  défaut) affiché dans les Réglages — clique dessus pour voir/modifier son contenu, il reste replié sinon, pour une
  liste de pages plus lisible qu'avant (surtout avec plusieurs espaces de travail bien remplis). Le contenu déjà
  activé apparaît comme des blocs (Horloge, Contrôles serveur, Courrier, Touches épinglées), chacun avec son propre
  bouton « ✕ » pour le retirer ; un bouton **« Ajouter »** propose ce qui n'est pas encore sur la page — c'est là,
  et seulement là, qu'on choisit d'ajouter du courrier ou des scripts (touches épinglées) à une page précise, plutôt
  que par des interrupteurs tous visibles en permanence.
- **Réglages du courrier redessinés** : chaque boîte mail est maintenant une carte repliable, avec ses réglages de
  réception (IMAP) et d'envoi (SMTP) séparés en deux sections claires, plutôt qu'une longue liste plate de champs.
- **Barre de défilement discrète** : la barre de défilement par défaut de Chromium/WebView2 est large et voyante dans
  une aussi petite fenêtre — remplacée par une version fine, purement CSS (donc sans le risque des correctifs de
  fenêtre natifs).
- **Pages/colonnes de touches épinglées** : le nombre de colonnes et de lignes par page se choisit dans les Réglages
  (2 à 6 colonnes, 1 à 4 lignes) ; s'il y a plus de touches que n'en tient une page, des flèches et des points
  apparaissent sous la grille pour naviguer entre les pages, à la manière des pages d'accueil de l'app mobile. La
  page affichée n'est pas mémorisée d'une ouverture à l'autre (elle revient toujours à la première).
- **Flèches/points toujours visibles** : ils étaient placés à l'intérieur de la zone qui défile, donc invisibles tant
  qu'on n'avait pas déjà fait défiler jusqu'en bas — déplacés en dehors, en bas du widget, pour rester accessibles en
  permanence.
- **Taille des touches qui s'adapte** : la taille de chaque touche épinglée était fixée en dur (60px), ce qui pouvait
  déborder (et donc forcer à faire défiler pour tout voir) selon le nombre de colonnes choisi ou la largeur du widget
  en redimensionnement libre. Elle se calcule maintenant à partir de la largeur réellement disponible (CSS
  « container query »).
- **Rafraîchissement immédiat** : le widget écoute maintenant les changements de configuration (`Config.Changed`) et
  se redessine tout seul — plus besoin de fermer/rouvrir le widget après avoir masqué une touche, changé une image,
  etc.
- **Œil pour masquer une touche épinglée** sans la supprimer, dans les Réglages.
- La fenêtre principale (Réglages, bibliothèque…) ne chargeait pas la feuille de style des tuiles (`KeyTile.css`) —
  seule la fenêtre du widget l'avait. C'est ce qui faisait qu'une image de touche épinglée s'affichait beaucoup trop
  grande dans les Réglages. Corrigé en l'ajoutant à `wwwroot/index.html`.
- En taille **compacte**, le contenu pouvait dépasser la hauteur fixe de la fenêtre et se retrouver coupé net. La
  zone sous l'heure défile maintenant (`overflow-y: auto`) si besoin.

Le code Windows listé ci-dessus est **jamais testé sur une machine Windows réelle** dans l'environnement où il a été
écrit (pas de SDK Windows disponible) — vérifié seulement par relecture attentive et par une vérification de
syntaxe/signatures réelle quand c'était possible. Chaque appel natif est protégé : en cas de souci, l'option
concernée reste simplement sans effet plutôt que de faire planter l'application (au pire, retrouve le widget via
alt-tab s'il devait rester introuvable). Les correctifs purement web/CSS de cette liste, eux, ne dépendent d'aucune
API Windows — ce sont les points les plus fiables.

## Le serveur ne s'affiche plus comme une application

Redémarrer le serveur (bouton, raccourci ou widget) le relance désormais **toujours caché** (`-WindowStyle Hidden`) :
aucune fenêtre, même pas un flash de console. Pour qu'il soit déjà lancé, caché, dès l'ouverture de session Windows
(sans dépendre de cette application) : un fichier `StartServerHidden.vbs` est créé automatiquement à côté de
`server.ps1`. Fais-en un raccourci dans le dossier Démarrage de Windows (`Win+R`, tape `shell:startup`, valide) —
le serveur démarrera alors seul, invisible, à chaque ouverture de session.

## Redémarrer le serveur

Le bouton **Redémarrer le serveur** (en haut de la bibliothèque et dans les Réglages) retrouve le processus PowerShell qui
exécute `server.ps1`, l'arrête et le relance avec les mêmes arguments. À utiliser après une mise à jour de `server.ps1` ou
`NfcNative.ps1` (un nouveau script n'en a pas besoin). Si le serveur tourne en administrateur, l'application le détecte et
redemande le redémarrage avec une confirmation Windows ; l'option « Redémarrer en administrateur » (Réglages) force ce
mode. Si le serveur a été lancé à la main dans une console PowerShell, ferme cette console puis lance-le à nouveau.

## Choisir un fichier ou un dossier

« Parcourir » ouvre la fenêtre native de Windows. Si elle ne peut pas s'ouvrir, l'explorateur intégré prend le relais
(le bouton en forme d'arborescence l'ouvre aussi directement).

## Ce qui est écrit sur le disque

- `Script\<nom>.ps1` : le script, en UTF-8 avec BOM (accents corrects dans Windows PowerShell 5.1). Il commence
  comme tes scripts actuels (`$NFCServerDir = Split-Path -Parent $PSScriptRoot`, puis dot-sourcing).
- `BuilderUtils.ps1`, à côté de `server.ps1` : fonctions communes des scripts générés (recherche de fenêtre,
  déplacement multi-écrans, plein écran, ouverture d'un navigateur…). Autonome (ne dépend pas de `WindowUtils.ps1`),
  installé et mis à jour automatiquement.
- En fin de script, quelques lignes de commentaires `#SDB:v1:…` : le plan du script, qui permet de le **rouvrir et le
  modifier** dans l'application. Ne les modifie pas à la main. Un script écrit à la main s'affiche dans la liste
  (« écrit à la main ») mais ne peut pas être rouvert dans l'éditeur.
- `%APPDATA%\StreamDeckScriptBuilder\config.json` : dossier du serveur et PowerShell utilisé pour les tests.

## Architecture

- `StreamDeck.ScriptBuilder` : bibliothèque Razor (modèle, générateur, services, écrans). Réutilise les composants, le
  thème et Font Awesome de `StreamDeck.Shared`.
- `StreamDeck.ScriptBuilder.Windows` : hôte MAUI Windows (une fenêtre, un `BlazorWebView`).
