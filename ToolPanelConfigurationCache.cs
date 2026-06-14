using System.Text.Json;

namespace EBAssistant;

public static class ToolPanelConfigurationCache
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static ToolPanelConfigurationTreeResult? Load(ToolPanelConfigurationIdentity identity)
    {
        return Load(identity, GetDefaultRootDirectory());
    }

    public static ToolPanelConfigurationTreeResult? Load(ToolPanelConfigurationIdentity identity, string rootDirectory)
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

    public static void Save(ToolPanelConfigurationTreeResult tree)
    {
        Save(tree, GetDefaultRootDirectory());
    }

    public static void Save(ToolPanelConfigurationTreeResult tree, string rootDirectory)
    {
        var path = GetPath(tree.Identity, rootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope
        {
            ProtocolVersion = ProtocolVersion,
            Tree = tree
        }, Options));
    }

    public static bool MergeDirectoryShallow(ToolPanelConfigurationTreeResult tree, ToolPanelDirectoryNode refreshed)
    {
        for (var index = 0; index < tree.Nodes.Count; index++)
        {
            if (string.Equals(tree.Nodes[index].Id, refreshed.Id, StringComparison.OrdinalIgnoreCase))
            {
                MergeNodeShallow(tree.Nodes[index], refreshed);
                return true;
            }
            if (MergeDirectoryShallow(tree.Nodes[index].Children, refreshed)) return true;
        }
        if (string.Equals(tree.Identity.RootId, refreshed.Id, StringComparison.OrdinalIgnoreCase))
        {
            MergeNodeListShallow(tree.Nodes, refreshed.Children);
            return true;
        }
        return false;
    }

    private static bool MergeDirectoryShallow(List<ToolPanelDirectoryNode> nodes, ToolPanelDirectoryNode refreshed)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Id, refreshed.Id, StringComparison.OrdinalIgnoreCase))
            {
                MergeNodeShallow(node, refreshed);
                return true;
            }
            if (MergeDirectoryShallow(node.Children, refreshed)) return true;
        }
        return false;
    }

    private static void MergeNodeShallow(ToolPanelDirectoryNode existing, ToolPanelDirectoryNode refreshed)
    {
        existing.Name = refreshed.Name;
        existing.FullPath = refreshed.FullPath;
        existing.Kind = refreshed.Kind;
        existing.TypeName = refreshed.TypeName;
        MergeNodeListShallow(existing.Children, refreshed.Children);
    }

    private static void MergeNodeListShallow(List<ToolPanelDirectoryNode> existing, List<ToolPanelDirectoryNode> refreshed)
    {
        var merged = new List<ToolPanelDirectoryNode>();
        foreach (var child in refreshed)
        {
            var cached = existing.FirstOrDefault(item => string.Equals(item.Id, child.Id, StringComparison.OrdinalIgnoreCase));
            if (cached is null)
            {
                merged.Add(child);
                continue;
            }
            cached.Name = child.Name;
            cached.FullPath = child.FullPath;
            cached.Kind = child.Kind;
            cached.TypeName = child.TypeName;
            merged.Add(cached);
        }
        existing.Clear();
        existing.AddRange(merged);
    }

    private static string GetDefaultRootDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Cache",
            "ToolPanelConfiguration");
    }

    private static string GetPath(ToolPanelConfigurationIdentity identity, string rootDirectory)
    {
        var safeId = Uri.EscapeDataString(identity.RootId);
        return Path.Combine(rootDirectory, $"{identity.Version}_{safeId}.json");
    }

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public ToolPanelConfigurationTreeResult Tree { get; set; } = new();
    }
}
