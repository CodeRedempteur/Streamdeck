using System.Text.Json;

namespace StreamDeck.Models;

public enum ImportMode
{
    /// <summary>Remplace les PC, espaces et réglages d'affichage (le code PIN local est conservé).</summary>
    Replace,

    /// <summary>Ajoute les espaces (et les PC manquants) à ceux qui existent déjà.</summary>
    Merge
}

public record ImportResult(bool Ok, string Message);

/// <summary>Export / import de la configuration sous forme de texte JSON (copier-coller entre appareils).</summary>
public static class ConfigTransfer
{
    public const string Format = "streamdeck-config";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal sealed class Envelope
    {
        public string Format { get; set; } = ConfigTransfer.Format;
        public int Version { get; set; } = DeckConfig.CurrentVersion;
        public DeckConfig? Config { get; set; }
    }

    /// <summary>Texte JSON de la configuration. Le code PIN n'est jamais exporté ; les tokens seulement si demandé.</summary>
    public static string Export(DeckConfig config, bool includeTokens)
    {
        var copy = config.Clone();
        copy.Security = new SecuritySettings();

        if (!includeTokens)
        {
            foreach (var pc in copy.Pcs)
                pc.Token = string.Empty;
        }

        return JsonSerializer.Serialize(new Envelope { Config = copy }, JsonOptions);
    }

    /// <summary>Lit un export. Renvoie null (et un message) si le texte n'est pas une configuration valide.</summary>
    public static DeckConfig? Parse(string? text, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Colle d'abord le texte de la configuration.";
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(text);

            if (envelope?.Config is null || envelope.Format != Format)
            {
                error = "Ce texte n'est pas une configuration Stream Deck.";
                return null;
            }

            if (envelope.Version > DeckConfig.CurrentVersion)
            {
                error = "Cette configuration vient d'une version plus récente de l'application.";
                return null;
            }

            envelope.Config.EnsureValid();
            return envelope.Config;
        }
        catch (JsonException)
        {
            error = "Le texte est incomplet ou abîmé (JSON invalide).";
            return null;
        }
    }

    /// <summary>Applique une configuration importée à la configuration en cours d'édition.</summary>
    public static ImportResult Apply(DeckConfig target, DeckConfig imported, ImportMode mode)
    {
        return mode == ImportMode.Replace
            ? Replace(target, imported)
            : Merge(target, imported);
    }

    private static ImportResult Replace(DeckConfig target, DeckConfig imported)
    {
        // Un token absent de l'export (export sans secrets) ne doit pas effacer celui déjà saisi ici.
        foreach (var pc in imported.Pcs.Where(p => string.IsNullOrEmpty(p.Token)))
        {
            var local = FindLocalPc(target, pc);
            if (local is not null) pc.Token = local.Token;
        }

        target.Pcs = imported.Pcs;
        target.DefaultPcId = imported.DefaultPcId;
        target.Workspaces = imported.Workspaces;
        target.ActiveWorkspaceId = imported.ActiveWorkspaceId;
        target.Display = imported.Display;
        target.EnsureValid();

        var buttons = imported.Workspaces.Sum(w => w.Pages.Sum(p => p.Buttons.Count));
        return new ImportResult(true, $"Configuration importée : {imported.Workspaces.Count} espace(s), {buttons} bouton(s).");
    }

    private static ImportResult Merge(DeckConfig target, DeckConfig imported)
    {
        // PC importés -> PC locaux (même identifiant ou même adresse), sinon ajoutés.
        var map = new Dictionary<Guid, Guid>();
        foreach (var pc in imported.Pcs)
        {
            var local = FindLocalPc(target, pc);
            if (local is null)
            {
                local = new PcProfile { Name = pc.Name, ServerUrl = pc.ServerUrl, Token = pc.Token, Mac = pc.Mac };
                target.Pcs.Add(local);
            }
            else if (string.IsNullOrWhiteSpace(local.Mac) && !string.IsNullOrWhiteSpace(pc.Mac))
            {
                local.Mac = pc.Mac;
            }

            map[pc.Id] = local.Id;
        }

        Guid? Remap(Guid? id) => id is { } value && map.TryGetValue(value, out var mapped) ? mapped : null;

        // Nouveaux identifiants partout : aucune collision avec l'existant.
        foreach (var w in imported.Workspaces)
        {
            w.Id = Guid.NewGuid();
            w.PcId = Remap(w.PcId);

            foreach (var page in w.Pages)
            {
                page.Id = Guid.NewGuid();

                foreach (var b in page.Buttons)
                {
                    b.Id = Guid.NewGuid();
                    b.PcId = Remap(b.PcId);
                    foreach (var step in b.Steps) step.Id = Guid.NewGuid();
                }
            }

            target.Workspaces.Add(w);
        }

        target.EnsureValid();

        var buttons = imported.Workspaces.Sum(w => w.Pages.Sum(p => p.Buttons.Count));
        return new ImportResult(true, $"{imported.Workspaces.Count} espace(s) et {buttons} bouton(s) ajoutés.");
    }

    private static PcProfile? FindLocalPc(DeckConfig target, PcProfile imported) =>
        target.Pcs.FirstOrDefault(p => p.Id == imported.Id)
        ?? target.Pcs.FirstOrDefault(p =>
            string.Equals(p.ServerUrl.TrimEnd('/'), imported.ServerUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
}
