using System.Text.Json;

namespace EBAssistant;

public static class AttributeFolderCache
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static FolderTreeResult? Load(AttributeFolderIdentity identity)
    {
        var path = GetPath(identity);
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

    public static void Save(AttributeFolderIdentity identity, FolderTreeResult tree)
    {
        var path = GetPath(identity);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope
        {
            ProtocolVersion = ProtocolVersion,
            Identity = identity,
            Tree = tree
        }, Options));
    }

    private static string GetPath(AttributeFolderIdentity identity)
    {
        var safeId = string.Concat(identity.RootId.Where(char.IsLetterOrDigit));
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Cache",
            "AttributeFolders",
            $"{identity.Version}_{safeId}.json");
    }

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public AttributeFolderIdentity Identity { get; set; } = new();
        public FolderTreeResult Tree { get; set; } = new();
    }
}
