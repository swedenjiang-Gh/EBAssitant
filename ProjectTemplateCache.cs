using System.Text.Json;

namespace EBAssistant;

public static class ProjectTemplateCache
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static ProjectTemplateTreeResult? Load(ProjectTemplateIdentity identity)
    {
        return Load(identity, GetDefaultRootDirectory());
    }

    public static ProjectTemplateTreeResult? Load(ProjectTemplateIdentity identity, string rootDirectory)
    {
        var path = GetPath(identity, rootDirectory);
        if (!File.Exists(path)) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(path), Options);
            return envelope?.ProtocolVersion == ProtocolVersion ? envelope.Tree : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(ProjectTemplateTreeResult tree)
    {
        Save(tree, GetDefaultRootDirectory());
    }

    public static void Save(ProjectTemplateTreeResult tree, string rootDirectory)
    {
        var path = GetPath(tree.Identity, rootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope
        {
            ProtocolVersion = ProtocolVersion,
            Tree = tree
        }, Options));
    }

    private static string GetDefaultRootDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Cache",
            "ProjectTemplates");
    }

    private static string GetPath(ProjectTemplateIdentity identity, string rootDirectory)
    {
        var safeId = string.Concat(identity.RootId.Where(char.IsLetterOrDigit));
        return Path.Combine(rootDirectory, $"{identity.Version}_{safeId}.json");
    }

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public ProjectTemplateTreeResult Tree { get; set; } = new();
    }
}
