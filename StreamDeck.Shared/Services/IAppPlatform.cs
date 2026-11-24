namespace StreamDeck.Services;

/// <summary>Ce qui change d'une plateforme à l'autre (Linux/Windows, Android). Implémenté par chaque hôte.</summary>
public interface IAppPlatform
{
    /// <summary>Dossier où est stocké config.json.</summary>
    string ConfigDirectory { get; }

    /// <summary>true si l'app peut se fermer elle-même (impossible / inutile sur Android).</summary>
    bool CanQuit { get; }

    void Quit();

    /// <summary>true si l'appareil sait vibrer (retour au toucher).</summary>
    bool CanVibrate { get; }

    /// <summary>Courte vibration de retour au toucher. Ne doit jamais lever d'exception.</summary>
    void Vibrate();

    /// <summary>true si la plateforme sait écouter en continu (commande vocale) — Android uniquement pour l'instant.</summary>
    bool CanListen { get; }

    /// <summary>
    /// Démarre (après avoir demandé la permission micro si besoin) ou arrête l'écoute vocale en continu.
    /// Renvoie faux si la permission a été refusée — l'appelant doit alors remettre le réglage à désactivé.
    /// Ne fait rien et renvoie faux si <see cref="CanListen"/> est faux.
    /// </summary>
    Task<bool> SetVoiceListeningAsync(bool enabled);

    /// <summary>
    /// Écoute une seule phrase courte (quelques secondes) et renvoie exactement ce qui a été reconnu — pour
    /// enregistrer une phrase déclencheuse fidèle à ce que la reconnaissance vocale entend vraiment, plutôt que de
    /// la deviner en la tapant. Renvoie null si annulé, si rien n'a été compris, ou si <see cref="CanListen"/> est
    /// faux. Demande la permission micro si besoin (comme <see cref="SetVoiceListeningAsync"/>).
    /// </summary>
    Task<string?> RecognizeSpeechOnceAsync();

    /// <summary>true si la plateforme sait sonner l'appareil pour le retrouver (bouton "Retrouver le téléphone",
    /// appui ou commande vocale) — Android uniquement pour l'instant.</summary>
    bool CanFindPhone { get; }

    /// <summary>
    /// Retrouve le téléphone : passe l'appareil en mode sonnerie (best effort) et joue une alarme en boucle au
    /// volume maximum. S'arrête tout seul après <paramref name="autoStopSeconds"/> si <see cref="StopFindPhone"/>
    /// n'a pas déjà été appelé entre-temps ; dans ce cas <paramref name="onAutoStopped"/> est appelé (sur le thread
    /// UI) pour que l'appelant mette à jour son affichage. Ne fait rien si <see cref="CanFindPhone"/> est faux.
    /// </summary>
    /// <param name="ringtonePath">
    /// v1.35 : chemin complet d'un fichier audio local à jouer à la place de la sonnerie par défaut de l'appareil
    /// (voir <see cref="VoiceSettings.CustomRingtoneFiles"/>/<see cref="VoiceSettings.SelectedRingtoneFile"/>) — null
    /// ou fichier introuvable = comportement d'origine (sonnerie d'alarme par défaut).
    /// </param>
    void TriggerFindPhone(int autoStopSeconds, Action onAutoStopped, string? ringtonePath = null);

    /// <summary>Arrête immédiatement une sonnerie déclenchée par <see cref="TriggerFindPhone"/>. Sans effet si
    /// aucune sonnerie n'est en cours.</summary>
    void StopFindPhone();

    // ----- Sonneries personnalisées (v1.35) : un simple sélecteur de fichier audio (mp3/etc.), copié dans le dossier
    // de l'app pour rester lisible durablement (un URI Android "content://" choisi par l'utilisateur n'est pas
    // garanti de rester accessible après redémarrage). -----

    /// <summary>true si la plateforme sait choisir un fichier audio local — Android uniquement pour l'instant.</summary>
    bool CanPickAudioFile { get; }

    /// <summary>
    /// Ouvre le sélecteur de fichiers pour choisir un audio local, le copie dans le sous-dossier "Ringtones" de
    /// <see cref="ConfigDirectory"/>, et renvoie son NOM de fichier (pas le chemin complet — à ajouter tel quel à
    /// <see cref="VoiceSettings.CustomRingtoneFiles"/>). Renvoie null si annulé, en échec, ou si
    /// <see cref="CanPickAudioFile"/> est faux.
    /// </summary>
    Task<string?> PickAudioFileAsync();

    /// <summary>Chemin complet d'un fichier de sonnerie ajouté via <see cref="PickAudioFileAsync"/>, à partir de son nom de fichier.</summary>
    string RingtoneFilePath(string fileName);

    // ----- Serveur de commandes PC → téléphone (v1.34.13, voir PhoneServerSettings) : Android uniquement. -----

    /// <summary>true si la plateforme sait faire tourner le petit serveur de commandes (voir <see cref="SetPhoneServerAsync"/>) — Android uniquement pour l'instant.</summary>
    bool CanRunPhoneServer { get; }

    /// <summary>Démarre/arrête le service qui écoute les commandes du PC (voir PhoneServerSettings.Port/Token).
    /// Renvoie faux si <see cref="CanRunPhoneServer"/> est faux ; ne fait rien d'autre en cas d'échec (pas de
    /// permission spéciale à demander ici, contrairement au micro).</summary>
    Task<bool> SetPhoneServerAsync(bool enabled);

    /// <summary>Adresse IP locale (Wi-Fi) la plus probable, pour l'afficher à copier côté PC — au mieux, peut être
    /// nulle (pas de réseau, ou introuvable). Ne garantit pas que ce soit la bonne interface sur un appareil à
    /// plusieurs cartes réseau actives.</summary>
    string? LocalIpAddress { get; }
}
