using System.Text;
using System.Text.Json;
using StreamDeck.ScriptBuilder.Models;

namespace StreamDeck.ScriptBuilder.Services;

/// <summary>
/// Lit et écrit Schedules.json, à côté de server.ps1 (comme config.txt) — jamais dans le dossier Script, pour ne
/// jamais être confondu avec un script. server.ps1 relit ce fichier tout seul (voir <c>Invoke-NfcScheduleCheck</c>),
/// donc les noms de champs C# (PascalCase) doivent rester lisibles par PowerShell : sans problème, l'accès aux
/// propriétés d'un objet PowerShell est insensible à la casse.
/// </summary>
public class ScheduleStore(BuilderConfigService config)
{
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private string ServerFolder => config.Current.ServerFolder;
    private string FilePath => Path.Combine(ServerFolder, "Schedules.json");

    private ScheduleFile Load()
    {
        if (!File.Exists(FilePath)) return new ScheduleFile();
        try
        {
            return JsonSerializer.Deserialize<ScheduleFile>(File.ReadAllText(FilePath), JsonOptions) ?? new ScheduleFile();
        }
        catch (Exception)
        {
            // Fichier illisible (modifié à la main, en cours d'écriture par le serveur…) : on ne perd rien tant
            // qu'on ne réenregistre pas — mais la liste apparaît vide pour cette lecture.
            return new ScheduleFile();
        }
    }

    private void SaveFile(ScheduleFile file)
    {
        if (string.IsNullOrWhiteSpace(ServerFolder)) throw new InvalidOperationException("Le dossier du serveur n'est pas configuré (Réglages).");

        Directory.CreateDirectory(ServerFolder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, JsonOptions), Utf8Bom);
        File.Move(tmp, FilePath, overwrite: true);
    }

    public IReadOnlyList<ScheduleRule> ListFor(string scriptName) =>
        Load().Schedules.Where(r => r.ScriptName.Equals(scriptName, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<ScheduleRule> ListAll() => Load().Schedules;

    public ScheduleRule Create(string scriptName)
    {
        var file = Load();
        var rule = new ScheduleRule { ScriptName = scriptName };
        file.Schedules.Add(rule);
        SaveFile(file);
        return rule;
    }

    /// <summary>Enregistre une règle déjà connue (modifiée en mémoire) : remplace celle de même identifiant.</summary>
    public void Save(ScheduleRule rule)
    {
        var file = Load();
        var index = file.Schedules.FindIndex(r => r.Id == rule.Id);
        if (index < 0) file.Schedules.Add(rule);
        else file.Schedules[index] = rule;
        SaveFile(file);
    }

    public void Delete(string id)
    {
        var file = Load();
        if (file.Schedules.RemoveAll(r => r.Id == id) > 0) SaveFile(file);
    }

    /// <summary>Un script a été renommé : ses règles le suivent (appelé par l'éditeur après un renommage réussi).</summary>
    public void RenameScript(string oldName, string newName)
    {
        if (oldName.Equals(newName, StringComparison.OrdinalIgnoreCase)) return;

        var file = Load();
        var changed = false;
        foreach (var r in file.Schedules.Where(r => r.ScriptName.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
        {
            r.ScriptName = newName;
            changed = true;
        }
        if (changed) SaveFile(file);
    }

    /// <summary>Un script a été supprimé : ses règles ne servent plus à rien, autant les enlever.</summary>
    public void DeleteForScript(string scriptName)
    {
        var file = Load();
        if (file.Schedules.RemoveAll(r => r.ScriptName.Equals(scriptName, StringComparison.OrdinalIgnoreCase)) > 0) SaveFile(file);
    }
}
