using System.Text.Json;

namespace EBAssistant;

public static class PermissionConfigurationCache
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static PermissionConfigurationStructureResult? Load(PermissionConfigurationIdentity identity)
    {
        var path = GetPath(identity);
        if (!File.Exists(path)) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(path), Options);
            return envelope?.ProtocolVersion == ProtocolVersion ? envelope.Structure : null;
        }
        catch { return null; }
    }

    public static void Save(PermissionConfigurationStructureResult structure)
    {
        var path = GetPath(structure.Identity);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope { ProtocolVersion = ProtocolVersion, Structure = structure }, Options));
    }

    private static string GetPath(PermissionConfigurationIdentity identity)
    {
        var safeId = string.Concat(identity.RootId.Where(char.IsLetterOrDigit));
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Cache",
            "PermissionConfiguration",
            $"{identity.Version}_{safeId}.json");
    }

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public PermissionConfigurationStructureResult Structure { get; set; } = new();
    }
}
