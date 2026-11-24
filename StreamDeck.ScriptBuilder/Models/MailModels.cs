namespace StreamDeck.ScriptBuilder.Models;

/// <summary>
/// Une boîte mail IMAP (+ SMTP optionnel, pour répondre) configurée pour le widget (voir <c>MailFetchService</c>) —
/// plusieurs comptes possibles, chacun indépendant (un compte en échec n'empêche pas les autres de s'afficher). Le
/// mot de passe n'est jamais stocké en clair dans config.json : voir <see cref="MailCredentialProtector"/>. Le SMTP
/// (v1.32.0) réutilise le même identifiant/mot de passe que l'IMAP — vrai pour la plupart des fournisseurs (dont
/// Gmail avec un mot de passe d'application), mais pas garanti pour tous.
/// </summary>
public class MailAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string Username { get; set; } = "";

    /// <summary>Mot de passe (ou mot de passe d'application) protégé via DPAPI (voir MailCredentialProtector) — vide tant qu'aucun n'a été saisi. Partagé entre IMAP et SMTP.</summary>
    public string ProtectedPassword { get; set; } = "";

    public bool Enabled { get; set; } = true;

    /// <summary>Active la possibilité de répondre depuis le widget (voir <c>MailFetchService.SendReplyAsync</c>) — désactivé par défaut (boîte en lecture seule tant que ça n'a pas été explicitement configuré).</summary>
    public bool SmtpEnabled { get; set; }
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;

    // ----- Signature (v1.34.5) : ajoutée en fin de message à l'envoi, cochée ou non par défaut selon SignatureEnabled
    // — mais toujours modifiable au cas par cas dans la fenêtre de composition (voir Widget.razor, ComposeSignatureOn). -----

    /// <summary>Coché par défaut dans la fenêtre de composition quand une signature est configurée (logo et/ou texte non vides) — sans effet si les deux sont vides.</summary>
    public bool SignatureEnabled { get; set; } = true;

    /// <summary>Logo affiché à côté du texte de signature — même convention qu'une image de touche épinglée (<c>PinImageResolver</c>) :
    /// un chemin local choisi via le sélecteur de fichier est mémorisé en <c>file://…</c> (résolu à l'affichage/l'envoi
    /// en image encodée, le WebView2 du widget refusant de charger un lien file:// directement), un lien <c>https://…</c>
    /// passe tel quel. Vide = pas de logo.</summary>
    public string SignatureLogoUrl { get; set; } = "";

    /// <summary>Texte affiché à côté du logo (nom, titre, téléphone, adresse…), une ligne par ligne de coordonnées — vide = pas de texte.</summary>
    public string SignatureText { get; set; } = "";

    public bool HasSignature => !string.IsNullOrWhiteSpace(SignatureLogoUrl) || !string.IsNullOrWhiteSpace(SignatureText);
}

/// <summary>Résumé d'un message reçu aujourd'hui, pour l'affichage dans le widget. <see cref="Uid"/> (identifiant IMAP
/// du message dans son dossier) permet de retrouver le message complet, de le marquer favori ou de le supprimer
/// ensuite (voir <c>MailFetchService</c>). <see cref="FromAddress"/> est l'adresse seule (sans le nom), utilisée
/// comme destinataire quand on répond.</summary>
public record MailSummary(string AccountId, string AccountName, string From, string? FromAddress, string Subject, DateTimeOffset Received, bool IsUnread, bool IsFavorite, uint Uid, bool IsArchived = false);

/// <summary>Ce que le widget demande d'afficher (v1.34.3) : une période de réception (aujourd'hui / 7 / 30 derniers
/// jours), uniquement les favoris, ou uniquement les archivés — trois vues qui s'excluent (voir <c>Widget.razor</c>,
/// bouton "filtrer" à côté de "Actualiser").</summary>
public enum MailViewScope { Period, Favorites, Archived }

/// <summary>Filtre courant du widget pour <c>MailFetchService.FetchAsync</c> — jamais persisté (repart de "Aujourd'hui" à chaque ouverture du widget).</summary>
public class MailFilter
{
    public MailViewScope Scope { get; set; } = MailViewScope.Period;

    /// <summary>Utilisé seulement quand <see cref="Scope"/> vaut <see cref="MailViewScope.Period"/> : 1 = aujourd'hui, 7 ou 30.</summary>
    public int PeriodDays { get; set; } = 1;
}

/// <summary>Résultat d'une tentative de récupération pour un compte — succès ou message d'erreur, jamais d'exception qui remonte à l'appelant.</summary>
public class MailAccountResult
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int UnreadToday { get; set; }
    public List<MailSummary> Messages { get; set; } = new();
}

/// <summary>Résultat d'une tentative de récupération du corps complet d'un message précis (voir <c>MailFetchService.FetchBodyAsync</c>), affiché dans le lecteur du widget quand on clique sur un message. <see cref="MessageId"/> permet de lier une réponse au message d'origine (en-têtes In-Reply-To/References).</summary>
public class MailBodyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Text { get; set; }

    /// <summary>Corps HTML nettoyé (balises dangereuses retirées) avec les images intégrées (cid:) remplacées par des
    /// data URI — null si le message n'a pas de version HTML, auquel cas <see cref="Text"/> reste la seule option
    /// d'affichage (v1.34.1, voir <c>MailFetchService.FetchBodyAsync</c>).</summary>
    public string? Html { get; set; }

    public string? MessageId { get; set; }
}

/// <summary>Résultat d'une action sur un message (favori, suppression) ou d'un envoi (réponse) — jamais d'exception qui remonte à l'appelant.</summary>
public class MailActionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>Une pièce jointe en mémoire (fichier choisi via le sélecteur, ou image collée directement dans le
/// message) prête à être envoyée — v1.34.5, voir <c>MailFetchService.SendReplyAsync</c>. N'existe que pour la durée
/// de la composition : rien n'est écrit sur le disque tant que le message n'est pas envoyé. Uniquement pour un envoi
/// immédiat — pas encore supporté pour un envoi programmé (<see cref="ScheduledEmail"/>), pour ne pas alourdir
/// config.json avec des pièces jointes potentiellement volumineuses encodées en base64.</summary>
public class MailAttachmentData
{
    public string FileName { get; set; } = "";
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}

/// <summary>État d'un envoi programmé (v1.34.1, voir <see cref="ScheduledEmail"/>).</summary>
public enum ScheduledEmailStatus { Pending, Sent, Failed }

/// <summary>
/// Un message écrit depuis le widget (nouveau ou réponse) mais dont l'envoi est programmé pour plus tard, plutôt
/// qu'envoyé immédiatement — stocké dans <c>BuilderConfig.ScheduledEmails</c> (voir <c>BuilderConfigService.Update</c>,
/// qui doit être tenu à jour de tout nouveau champ ici). Envoyé par le widget lui-même dès qu'il tourne et que l'heure
/// est passée (pas de service séparé toujours actif) : voir <c>Widget.razor</c>, <c>CheckScheduledEmails</c>. Un envoi
/// qui échoue (pas de connexion au moment prévu, par exemple) repasse en <see cref="ScheduledEmailStatus.Failed"/>
/// plutôt que d'être perdu — visible et renvoyable/modifiable depuis la zone de notifications du widget.
/// </summary>
public class ScheduledEmail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string AccountId { get; set; } = "";
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? InReplyToMessageId { get; set; }
    public DateTimeOffset ScheduledAt { get; set; }
    public ScheduledEmailStatus Status { get; set; } = ScheduledEmailStatus.Pending;
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }
    public int Attempts { get; set; }
}
