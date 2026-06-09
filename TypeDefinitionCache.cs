using System.Text.Json;

namespace EBAssist;

public static class TypeDefinitionCache
{
    private const int ProtocolVersion = 3;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static TypeDefinitionTreeResult? Load(TypeDefinitionIdentity identity)
    {
        var path = GetPath(identity);
        if (!File.Exists(path)) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(path), Options);
            return envelope?.ProtocolVersion == ProtocolVersion ? envelope.Tree : null;
        }
        catch { return null; }
    }

    public static void Save(TypeDefinitionTreeResult tree)
    {
        var path = GetPath(tree.Identity);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope { ProtocolVersion = ProtocolVersion, Tree = tree }, Options));
    }

    private static string GetPath(TypeDefinitionIdentity identity)
    {
        var safeId = string.Concat(identity.RootId.Where(char.IsLetterOrDigit));
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EBAssist", "Cache", "TypeDefinitions", $"{identity.Version}_{safeId}.json");
    }

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public TypeDefinitionTreeResult Tree { get; set; } = new();
    }
}
