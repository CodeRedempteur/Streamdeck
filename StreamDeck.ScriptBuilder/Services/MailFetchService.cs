using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Récupère, pour chaque boîte mail configurée (<see cref="BuilderConfigService"/> → <c>MailAccounts</c>), les
/// messages reçus aujourd'hui via IMAP (MailKit) — affichés dans le widget comme une ou plusieurs pages (voir
/// Widget.razor et <c>WidgetPageEntry.MailAccountId</c>). Chaque compte est isolé : une boîte en échec (identifiants
/// invalides, serveur injoignable…) n'empêche pas les autres de s'afficher.
/// </summary>
/// <remarks>
/// <para><b>Non vérifié avec un vrai serveur IMAP/SMTP</b> dans l'environnement où ce fichier a été écrit (pas
/// d'accès réseau sortant vers un serveur mail depuis ce bac à sable) — écrit d'après l'API publique documentée de
/// MailKit, une bibliothèque .NET très largement utilisée et stable pour ça, mais jamais exécutée ici pour de vrai.
/// Pour Gmail (et la plupart des services avec double authentification), un <b>mot de passe d'application</b> est
/// nécessaire — Google refuse l'authentification par mot de passe normal depuis 2022 ; ce mot de passe se crée depuis
/// les paramètres de sécurité du compte Google, pas le mot de passe habituel.</para>
/// <para><b>Ce qui reste en lecture seule vs. ce qui écrit désormais sur le serveur (v1.32.0)</b> : lister/lire le
/// courrier et son contenu complet (<see cref="FetchAsync"/>/<see cref="FetchBodyAsync"/>) reste en
/// lecture seule (<c>FolderAccess.ReadOnly</c>), rien n'est marqué comme lu. En revanche, marquer favori
/// (<see cref="SetFavoriteAsync"/>) et supprimer (<see cref="DeleteAsync"/>) ouvrent la boîte en écriture
/// (<c>FolderAccess.ReadWrite</c>) et modifient réellement le message sur le serveur — un favori pose le drapeau IMAP
/// standard <c>\Flagged</c> (ce que la plupart des clients mail appellent "suivi"/"étoile", y compris Gmail) ;
/// supprimer pose le drapeau <c>\Deleted</c> puis expurge — la plupart des serveurs (dont Gmail) déplacent alors le
/// message vers la corbeille plutôt que de l'effacer définitivement, comme un client mail normal, mais ce
/// comportement dépend du serveur et n'est pas garanti partout.</para>
/// </remarks>
public class MailFetchService(BuilderConfigService config)
{
    /// <summary>
    /// Récupère les messages selon le filtre demandé par le widget (v1.34.3 — remplace l'ancien <c>FetchTodayAsync</c>,
    /// qui ne savait faire que "aujourd'hui") : une période de réception, uniquement les favoris, ou uniquement les
    /// archivés (voir <see cref="MailFilter"/>). Les vues "Favoris" et "Archivés" ne sont pas bornées dans le temps —
    /// potentiellement plus lent sur une très grosse boîte, mais rester simple (pas de pagination dans un widget) l'a
    /// emporté ; à revoir si ça se révèle trop lent en usage réel.
    /// </summary>
    public async Task<List<MailAccountResult>> FetchAsync(MailFilter filter, CancellationToken ct = default)
    {
        var accounts = config.Current.MailAccounts.Where(a => a.Enabled).ToList();
        var results = new List<MailAccountResult>();
        foreach (var account in accounts)
            results.Add(await FetchOneAsync(account, filter, ct));
        return results;
    }

    private static async Task<MailAccountResult> FetchOneAsync(MailAccount account, MailFilter filter, CancellationToken ct)
    {
        var result = new MailAccountResult
        {
            AccountId = account.Id,
            AccountName = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName
        };

        var password = MailCredentialProtector.Unprotect(account.ProtectedPassword);
        if (string.IsNullOrWhiteSpace(account.ImapHost) || string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(password))
        {
            result.Error = "Identifiants incomplets.";
            return result;
        }

        using var client = new ImapClient();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15)); // un serveur injoignable ne doit pas geler tout le widget

            await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, timeoutCts.Token);
            await client.AuthenticateAsync(account.Username, password, timeoutCts.Token);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, timeoutCts.Token);

            // Construction de la requête selon le filtre demandé (v1.34.3) : la période exclut toujours les archivés
            // (ils n'apparaissent que dans la vue "Archivés" dédiée) ; "Favoris"/"Archivés" ne sont pas bornés dans le
            // temps. "Archivé" n'est pas un concept IMAP standard (contrairement à \Flagged pour "favori") — modélisé
            // ici avec un mot-clé personnalisé ("Archived"), la même mécanique que les favoris mais avec un drapeau
            // que le serveur doit accepter comme mot-clé arbitraire (RFC 3501/5788) plutôt que comme un des drapeaux
            // système standard ; la plupart des serveurs modernes (dont Gmail et Infomaniak) l'acceptent, mais ça
            // reste un point à confirmer avec un vrai serveur.
            var query = filter.Scope switch
            {
                MailViewScope.Favorites => SearchQuery.Flagged,
                MailViewScope.Archived => SearchQuery.HasKeyword("Archived"),
                _ => SearchQuery.DeliveredAfter(DateTime.Now.Date.AddDays(-(Math.Max(1, filter.PeriodDays) - 1)))
            };
            if (filter.Scope == MailViewScope.Period) query = query.And(SearchQuery.NotKeyword("Archived"));

            var uids = await inbox.SearchAsync(query, timeoutCts.Token);
            var take = uids.OrderByDescending(u => u.Id).Take(30).ToList(); // reste léger dans un widget, les plus récents d'abord

            if (take.Count > 0)
            {
                var items = await inbox.FetchAsync(take, MessageSummaryItems.Envelope | MessageSummaryItems.Flags, timeoutCts.Token);
                foreach (var item in items)
                {
                    if (item.Envelope is null) continue;
                    var flags = item.Flags ?? MessageFlags.None;
                    result.Messages.Add(new MailSummary(
                        account.Id,
                        result.AccountName,
                        item.Envelope.From.FirstOrDefault()?.ToString() ?? "?",
                        item.Envelope.From.Mailboxes.FirstOrDefault()?.Address,
                        string.IsNullOrWhiteSpace(item.Envelope.Subject) ? "(sans objet)" : item.Envelope.Subject,
                        item.Envelope.Date ?? DateTimeOffset.Now,
                        !flags.HasFlag(MessageFlags.Seen),
                        flags.HasFlag(MessageFlags.Flagged),
                        item.UniqueId.Id,
                        item.Keywords is not null && item.Keywords.Contains("Archived")));
                }
            }

            result.Messages = result.Messages.OrderByDescending(m => m.Received).ToList();
            result.UnreadToday = result.Messages.Count(m => m.IsUnread);
            result.Success = true;
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            result.Error = DescribeError(account, ex);
            try { if (client.IsConnected) await client.DisconnectAsync(false, CancellationToken.None); }
            catch (Exception) { /* déjà en échec de toute façon */ }
        }

        return result;
    }

    /// <summary>
    /// Récupère le corps complet d'un message précis (appelé quand on clique sur un message dans le widget) — pas
    /// gardé en mémoire à l'avance avec le reste (trop lourd pour 30 messages par boîte), donc une reconnexion IMAP
    /// à chaque clic. Toujours en lecture seule. Renvoie le texte brut (vue « Détails ») et, quand le message a une
    /// version HTML, cette version nettoyée avec ses images intégrées (<c>cid:</c>) remplacées par des data URI —
    /// voir <see cref="EmbedInlineImages"/> et <see cref="SanitizeHtml"/> (v1.34.1 : les mails avec images
    /// s'affichaient sans elles jusqu'ici, seul le texte était extrait). Renvoie aussi l'identifiant du message
    /// (<c>Message-Id</c>) pour lier une réponse au fil d'origine (<see cref="SendReplyAsync"/>).
    /// </summary>
    public async Task<MailBodyResult> FetchBodyAsync(string accountId, uint uid, CancellationToken ct = default)
    {
        var account = config.Current.MailAccounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return new MailBodyResult { Error = "Cette boîte mail n'existe plus." };

        var password = MailCredentialProtector.Unprotect(account.ProtectedPassword);
        if (string.IsNullOrWhiteSpace(account.ImapHost) || string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(password))
            return new MailBodyResult { Error = "Identifiants incomplets." };

        using var client = new ImapClient();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, timeoutCts.Token);
            await client.AuthenticateAsync(account.Username, password, timeoutCts.Token);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, timeoutCts.Token);

            var message = await inbox.GetMessageAsync(new UniqueId(uid), timeoutCts.Token);

            var text = message.TextBody;
            if (string.IsNullOrWhiteSpace(text)) text = StripHtml(message.HtmlBody);
            if (string.IsNullOrWhiteSpace(text)) text = "(message sans contenu texte)";

            string? html = null;
            if (!string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                html = EmbedInlineImages(message, message.HtmlBody);
                html = SanitizeHtml(html);
            }

            await client.DisconnectAsync(true, ct);
            return new MailBodyResult { Success = true, Text = text, Html = html, MessageId = message.MessageId };
        }
        catch (Exception ex)
        {
            try { if (client.IsConnected) await client.DisconnectAsync(false, CancellationToken.None); }
            catch (Exception) { /* déjà en échec de toute façon */ }
            return new MailBodyResult { Error = DescribeError(account, ex) };
        }
    }

    /// <summary>
    /// Remplace chaque référence <c>cid:xxx</c> du HTML (attribut <c>src</c> d'une balise <c>&lt;img&gt;</c>,
    /// typiquement) par une data URI encodant directement l'image jointe correspondante (retrouvée par son
    /// <c>Content-Id</c> parmi les parties du message) — sans ça, ces images (le cas le plus courant d'image dans un
    /// mail HTML : logo de signature, image intégrée au corps) ne se chargent jamais, puisque leur "URL" n'en est pas
    /// une en dehors du message lui-même. Une image manquante ou non décodable est simplement laissée telle quelle
    /// (le <c>cid:</c> ne se charge pas, comme avant ce correctif, plutôt que de faire échouer tout l'affichage).
    /// </summary>
    private static string EmbedInlineImages(MimeMessage message, string html)
    {
        foreach (var part in message.BodyParts.OfType<MimePart>())
        {
            var cid = part.ContentId;
            if (string.IsNullOrEmpty(cid) || part.Content is null) continue;
            if (!string.Equals(part.ContentType?.MediaType, "image", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                using var ms = new MemoryStream();
                part.Content.DecodeTo(ms);
                var dataUri = $"data:{part.ContentType!.MimeType};base64,{Convert.ToBase64String(ms.ToArray())}";
                html = html.Replace($"cid:{cid}", dataUri, StringComparison.OrdinalIgnoreCase);
                // Certains messages entourent l'identifiant de chevrons dans les en-têtes mais pas dans le HTML — au cas où l'inverse arrive :
                html = html.Replace($"cid:<{cid}>", dataUri, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { /* image illisible : laissée non chargée plutôt que de faire échouer tout l'affichage */ }
        }
        return html;
    }

    /// <summary>
    /// Nettoyage HTML minimal, volontairement simple (expressions régulières, pas de vraie bibliothèque de parsing
    /// HTML — aucune n'est déjà une dépendance de ce projet, et en ajouter une passerait par `api.nuget.org`,
    /// injoignable dans le bac à sable où ce fichier a été écrit) : retire &lt;script&gt;/&lt;iframe&gt; avec leur
    /// contenu, les balises &lt;object&gt;/&lt;embed&gt;/&lt;link&gt;/&lt;meta&gt;/&lt;form&gt;, les attributs
    /// gestionnaires d'évènements (`onclick=`, `onerror=`…) et les liens `javascript:`. Réduit le risque qu'un mail
    /// HTML malveillant exécute du script dans le widget, mais **ce n'est pas un sanitizeur HTML complet et certifié**
    /// — non vérifié avec un vrai message piégé, comme tout le code mail de cette classe.
    /// </summary>
    private static string SanitizeHtml(string html)
    {
        html = System.Text.RegularExpressions.Regex.Replace(html, "<script[^>]*>.*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        html = System.Text.RegularExpressions.Regex.Replace(html, "<iframe[^>]*>.*?</iframe>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        html = System.Text.RegularExpressions.Regex.Replace(html, "</?(object|embed|link|meta|form)[^>]*>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"(href|src)\s*=\s*(""|')\s*javascript:[^""']*\2", "$1=\"#\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return html;
    }

    /// <summary>
    /// Marque/démarque un message comme favori — pose ou retire le drapeau IMAP standard <c>\Flagged</c> (voir la
    /// remarque de classe). Seule action de cette classe, avec <see cref="DeleteAsync"/>, à écrire réellement sur le
    /// serveur.
    /// </summary>
    public Task<MailActionResult> SetFavoriteAsync(string accountId, uint uid, bool favorite, CancellationToken ct = default) =>
        WithReadWriteInboxAsync(accountId, async (inbox, id, token) =>
        {
            if (favorite) await inbox.AddFlagsAsync(id, MessageFlags.Flagged, silent: true, token);
            else await inbox.RemoveFlagsAsync(id, MessageFlags.Flagged, silent: true, token);
        }, uid, ct);

    /// <summary>
    /// Archive/désarchive un message (v1.34.3) — pose ou retire un mot-clé IMAP personnalisé ("Archived", voir la
    /// remarque de <see cref="FetchOneAsync"/>) plutôt qu'un des drapeaux système standard, qui n'a pas d'équivalent
    /// "archivé" prévu par la norme IMAP.
    /// </summary>
    public Task<MailActionResult> SetArchivedAsync(string accountId, uint uid, bool archived, CancellationToken ct = default) =>
        WithReadWriteInboxAsync(accountId, async (inbox, id, token) =>
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Archived" };
            if (archived) await inbox.AddFlagsAsync(id, MessageFlags.None, keywords, silent: true, token);
            else await inbox.RemoveFlagsAsync(id, MessageFlags.None, keywords, silent: true, token);
        }, uid, ct);

    /// <summary>
    /// Supprime un message — pose le drapeau IMAP <c>\Deleted</c> puis expurge le dossier (voir la remarque de
    /// classe : la plupart des serveurs, dont Gmail, déplacent alors le message vers la corbeille plutôt que de
    /// l'effacer immédiatement et définitivement, mais ça reste une action destructrice qui écrit sur le serveur).
    /// </summary>
    public Task<MailActionResult> DeleteAsync(string accountId, uint uid, CancellationToken ct = default) =>
        WithReadWriteInboxAsync(accountId, async (inbox, id, token) =>
        {
            await inbox.AddFlagsAsync(id, MessageFlags.Deleted, silent: true, token);
            await inbox.ExpungeAsync(new[] { id }, token);
        }, uid, ct);

    private async Task<MailActionResult> WithReadWriteInboxAsync(string accountId, Func<IMailFolder, UniqueId, CancellationToken, Task> action, uint uid, CancellationToken ct)
    {
        var account = config.Current.MailAccounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return new MailActionResult { Error = "Cette boîte mail n'existe plus." };

        var password = MailCredentialProtector.Unprotect(account.ProtectedPassword);
        if (string.IsNullOrWhiteSpace(account.ImapHost) || string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(password))
            return new MailActionResult { Error = "Identifiants incomplets." };

        using var client = new ImapClient();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            await client.ConnectAsync(account.ImapHost, account.ImapPort, account.UseSsl, timeoutCts.Token);
            await client.AuthenticateAsync(account.Username, password, timeoutCts.Token);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, timeoutCts.Token);
            await action(inbox, new UniqueId(uid), timeoutCts.Token);

            await client.DisconnectAsync(true, ct);
            return new MailActionResult { Success = true };
        }
        catch (Exception ex)
        {
            try { if (client.IsConnected) await client.DisconnectAsync(false, CancellationToken.None); }
            catch (Exception) { /* déjà en échec de toute façon */ }
            return new MailActionResult { Error = DescribeError(account, ex) };
        }
    }

    /// <summary>
    /// Envoie une réponse (ou un nouveau message) par SMTP (v1.32.0, voir <see cref="MailAccount.SmtpEnabled"/>) —
    /// réutilise le même identifiant/mot de passe que l'IMAP. MimeKit (dont dépend déjà MailKit) fournit la
    /// construction du message, pas de paquet NuGet supplémentaire. Lie la réponse au message d'origine via les
    /// en-têtes standard <c>In-Reply-To</c>/<c>References</c> quand son <c>Message-Id</c> est connu (voir
    /// <see cref="FetchBodyAsync"/>). Depuis v1.34.5 : plusieurs destinataires à la fois (<paramref name="toAddresses"/>),
    /// pièces jointes optionnelles et signature optionnelle (voir <see cref="BuildSignatureHtml"/>) — les deux
    /// obligent le corps du message à être envoyé en HTML plutôt qu'en texte brut.
    /// </summary>
    public async Task<MailActionResult> SendReplyAsync(string accountId, IReadOnlyList<string> toAddresses, string subject, string body,
        string? inReplyToMessageId, bool includeSignature = false, IReadOnlyList<MailAttachmentData>? attachments = null, CancellationToken ct = default)
    {
        var account = config.Current.MailAccounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return new MailActionResult { Error = "Cette boîte mail n'existe plus." };
        if (!account.SmtpEnabled) return new MailActionResult { Error = "L'envoi (SMTP) n'est pas activé pour cette boîte — active-le dans les Réglages." };

        var password = MailCredentialProtector.Unprotect(account.ProtectedPassword);
        if (string.IsNullOrWhiteSpace(account.SmtpHost) || string.IsNullOrWhiteSpace(account.Username) || string.IsNullOrWhiteSpace(password))
            return new MailActionResult { Error = "Identifiants SMTP incomplets." };

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(account.Username.Contains('@') ? account.Username : $"{account.Username}@{account.ImapHost}"));
            // Plusieurs destinataires (v1.34.5) : une adresse invalide dans le lot est ignorée plutôt que de faire
            // échouer tout l'envoi — l'app a déjà vérifié en amont (widget) qu'elles ressemblent à des adresses.
            foreach (var addr in toAddresses)
            {
                if (string.IsNullOrWhiteSpace(addr)) continue;
                try { message.To.Add(MailboxAddress.Parse(addr.Trim())); } catch (Exception) { /* adresse mal formée : ignorée */ }
            }
            if (message.To.Count == 0) return new MailActionResult { Error = "Adresse du destinataire introuvable." };
            message.Subject = subject;
            // Explicites plutôt que laissés à MimeKit : certains serveurs (Infomaniak notamment, remonté en échec —
            // voir DescribeError plus bas) sont plus stricts que Gmail sur la conformité RFC 5322 d'un message entrant
            // et peuvent rejeter un message sans Date ou Message-Id valides plutôt que de les compléter eux-mêmes.
            message.Date = DateTimeOffset.Now;
            message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId(account.Username.Contains('@') ? account.Username.Split('@')[1] : account.ImapHost);
            if (!string.IsNullOrEmpty(inReplyToMessageId))
            {
                message.InReplyTo = inReplyToMessageId;
                message.References.Add(inReplyToMessageId);
            }

            // Signature (v1.34.5) et pièces jointes obligent à construire un corps HTML plutôt que du texte brut (le
            // logo de la signature ne peut s'afficher que dans un corps HTML) — voir BuildSignatureHtml plus bas.
            var builder = new BodyBuilder { TextBody = body };
            var htmlBody = System.Net.WebUtility.HtmlEncode(body).Replace("\n", "<br>\n");
            if (includeSignature && account.HasSignature)
            {
                htmlBody += BuildSignatureHtml(builder, account);
                if (!string.IsNullOrWhiteSpace(account.SignatureText)) builder.TextBody += "\n\n" + account.SignatureText;
            }
            builder.HtmlBody = htmlBody;
            if (attachments is not null)
            {
                foreach (var att in attachments)
                {
                    MimeKit.ContentType contentType;
                    try { contentType = MimeKit.ContentType.Parse(att.ContentType); }
                    catch (Exception) { contentType = new MimeKit.ContentType("application", "octet-stream"); }
                    builder.Attachments.Add(att.FileName, att.Bytes, contentType);
                }
            }
            message.Body = builder.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(20));

            await client.ConnectAsync(account.SmtpHost, account.SmtpPort, MailKit.Security.SecureSocketOptions.Auto, timeoutCts.Token);
            await client.AuthenticateAsync(account.Username, password, timeoutCts.Token);
            await client.SendAsync(message, timeoutCts.Token);
            await client.DisconnectAsync(true, timeoutCts.Token);

            return new MailActionResult { Success = true };
        }
        catch (Exception ex)
        {
            return new MailActionResult { Error = DescribeError(account, ex) };
        }
    }

    /// <summary>
    /// Construit le HTML de la signature (logo + texte de coordonnées côte à côte, voir <see cref="MailAccount.HasSignature"/>)
    /// et, si le logo est un fichier local (<c>file://…</c>), l'ajoute au message comme ressource intégrée (<c>cid:</c>)
    /// via <paramref name="builder"/> — un lien <c>https://…</c> est utilisé tel quel dans le <c>&lt;img&gt;</c> (chargé
    /// depuis le serveur distant à l'ouverture du message, comme dans la plupart des emails avec logo). <b>Non
    /// vérifié avec un vrai serveur IMAP/SMTP</b> ; l'affichage d'un logo intégré en <c>cid:</c> dépend aussi du
    /// client mail du destinataire (Outlook/Gmail/etc. le supportent tous en pratique, mais pas garanti à 100 %).
    /// </summary>
    private static string BuildSignatureHtml(BodyBuilder builder, MailAccount account)
    {
        var textHtml = string.IsNullOrWhiteSpace(account.SignatureText)
            ? ""
            : System.Net.WebUtility.HtmlEncode(account.SignatureText).Replace("\n", "<br>");

        var imgHtml = "";
        if (!string.IsNullOrWhiteSpace(account.SignatureLogoUrl))
        {
            try
            {
                if (account.SignatureLogoUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    var path = new Uri(account.SignatureLogoUrl).LocalPath;
                    if (File.Exists(path))
                    {
                        var bytes = File.ReadAllBytes(path);
                        var resource = builder.LinkedResources.Add(Path.GetFileName(path), bytes);
                        resource.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId();
                        imgHtml = $"<img src=\"cid:{resource.ContentId}\" alt=\"\" style=\"max-height:60px;max-width:180px;display:block;border:0\" />";
                    }
                }
                else
                {
                    imgHtml = $"<img src=\"{System.Net.WebUtility.HtmlEncode(account.SignatureLogoUrl)}\" alt=\"\" style=\"max-height:60px;max-width:180px;display:block;border:0\" />";
                }
            }
            catch (Exception) { /* logo introuvable/illisible : la signature reste utilisable avec le texte seul */ }
        }

        if (imgHtml.Length == 0 && textHtml.Length == 0) return "";
        return "<table cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top:18px;border-top:1px solid #ddd;padding-top:10px;font-family:sans-serif;font-size:13px;color:#333\"><tr>"
             + (imgHtml.Length > 0 ? $"<td style=\"padding-right:14px;vertical-align:top\">{imgHtml}</td>" : "")
             + (textHtml.Length > 0 ? $"<td style=\"vertical-align:top;white-space:nowrap\">{textHtml}</td>" : "")
             + "</tr></table>";
    }

    /// <summary>
    /// Message plus utile que l'exception brute pour le cas le plus fréquent : Gmail (et la plupart des services avec
    /// double authentification) refuse l'authentification par mot de passe normal et répond par une erreur
    /// d'authentification (accompagnée d'une alerte de sécurité par mail) plutôt que d'expliquer la cause — un mot de
    /// passe d'application est nécessaire à la place. Partagé entre toutes les méthodes de cette classe qui se
    /// connectent à un serveur (IMAP ou SMTP).
    /// </summary>
    private static string DescribeError(MailAccount account, Exception ex)
    {
        var looksLikeGoogle = account.ImapHost.Contains("gmail", StringComparison.OrdinalIgnoreCase)
                               || account.ImapHost.Contains("google", StringComparison.OrdinalIgnoreCase)
                               || account.SmtpHost.Contains("gmail", StringComparison.OrdinalIgnoreCase)
                               || account.SmtpHost.Contains("google", StringComparison.OrdinalIgnoreCase);
        var looksLikeInfomaniak = account.ImapHost.Contains("infomaniak", StringComparison.OrdinalIgnoreCase)
                                  || account.SmtpHost.Contains("infomaniak", StringComparison.OrdinalIgnoreCase);
        var looksLikeAuthFailure = ex is MailKit.Security.AuthenticationException
                                    || ex.Message.Contains("authenticat", StringComparison.OrdinalIgnoreCase)
                                    || ex.Message.Contains("credentials", StringComparison.OrdinalIgnoreCase);

        if (looksLikeGoogle && looksLikeAuthFailure)
            return "Google refuse ce mot de passe (courriel « Alerte de sécurité » reçu ?) — utilise un mot de passe d'application (myaccount.google.com/apppasswords), pas ton mot de passe habituel.";

        // Détail complet plutôt que le seul message de plus haut niveau (v1.34.2, remonté en échec pour Infomaniak
        // — SMTP en échec alors que l'IMAP, avec le même mot de passe, fonctionne) : une SmtpCommandException/
        // AuthenticationException de MailKit n'inclut pas toujours la réponse texte brute du serveur dans son
        // Message de premier niveau, qui se retrouve parfois seulement dans l'exception interne.
        var detail = ex.Message;
        if (ex.InnerException is not null && !detail.Contains(ex.InnerException.Message, StringComparison.OrdinalIgnoreCase))
            detail += $" ({ex.InnerException.Message})";

        return looksLikeInfomaniak && looksLikeAuthFailure
            ? $"Infomaniak refuse l'envoi (SMTP) alors que la réception (IMAP) fonctionne avec le même mot de passe — vérifie que l'adresse d'expéditeur configurée correspond exactement à l'adresse mail Infomaniak (pas un alias), et que l'envoi SMTP n'est pas bloqué séparément dans les réglages de sécurité du compte Infomaniak. Détail technique : {detail}"
            : detail;
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<br\\s*/?>|</p>|</div>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, "[ \\t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\n\\s*\\n\\s*\\n+", "\n\n");
        return text.Trim();
    }
}
