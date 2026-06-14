using System.Text.Json;

namespace EBAssistant;

public static class GraphicTemplateCache
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static GraphicTemplateTreeResult? Load(GraphicTemplateIdentity identity)
    {
        return Load(identity, GetDefaultRootDirectory());
    }

    public static GraphicTemplateTreeResult? Load(GraphicTemplateIdentity identity, string rootDirectory)
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

    public static void Save(GraphicTemplateTreeResult tree)
    {
        Save(tree, GetDefaultRootDirectory());
    }

    public static void Save(GraphicTemplateTreeResult tree, string rootDirectory)
    {
        var path = GetPath(tree.Identity, rootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope
        {
            ProtocolVersion = ProtocolVersion,
            Tree = tree
        }, Options));
    }

    public static bool ReplaceDirectory(GraphicTemplateTreeResult tree, GraphicTemplateDirectoryNode refreshed)
    {
        for (var index = 0; index < tree.Nodes.Count; index++)
        {
            if (string.Equals(tree.Nodes[index].Id, refreshed.Id, StringComparison.OrdinalIgnoreCase))
            {
                tree.Nodes[index] = refreshed;
                return true;
            }
            if (ReplaceDirectory(tree.Nodes[index].Children, refreshed)) return true;
        }
        if (string.Equals(tree.Identity.RootId, refreshed.Id, StringComparison.OrdinalIgnoreCase))
        {
            tree.Nodes = refreshed.Children;
            return true;
        }
        return false;
    }

    public static bool MergeDirectoryShallow(GraphicTemplateTreeResult tree, GraphicTemplateDirectoryNode refreshed)
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

    private static bool ReplaceDirectory(List<GraphicTemplateDirectoryNode> nodes, GraphicTemplateDirectoryNode refreshed)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (string.Equals(nodes[index].Id, refreshed.Id, StringComparison.OrdinalIgnoreCase))
            {
                nodes[index] = refreshed;
                return true;
            }
            if (ReplaceDirectory(nodes[index].Children, refreshed)) return true;
        }
        return false;
    }

    private static bool MergeDirectoryShallow(List<GraphicTemplateDirectoryNode> nodes, GraphicTemplateDirectoryNode refreshed)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (string.Equals(nodes[index].Id, refreshed.Id, StringComparison.OrdinalIgnoreCase))
            {
                MergeNodeShallow(nodes[index], refreshed);
                return true;
            }
            if (MergeDirectoryShallow(nodes[index].Children, refreshed)) return true;
        }
        return false;
    }

    private static void MergeNodeShallow(GraphicTemplateDirectoryNode existing, GraphicTemplateDirectoryNode refreshed)
    {
        existing.Name = refreshed.Name;
        existing.FullPath = refreshed.FullPath;
        existing.Kind = refreshed.Kind;
        existing.TypeName = refreshed.TypeName;
        existing.Templates = refreshed.Templates;
        MergeNodeListShallow(existing.Children, refreshed.Children);
    }

    private static void MergeNodeListShallow(List<GraphicTemplateDirectoryNode> existing, List<GraphicTemplateDirectoryNode> refreshed)
    {
        var merged = new List<GraphicTemplateDirectoryNode>();
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
            cached.Templates = child.Templates.Count == 0 ? cached.Templates : child.Templates;
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
            "GraphicTemplates");
    }

    private static string GetPath(GraphicTemplateIdentity identity, string rootDirectory)
    {
        var safeId = Uri.EscapeDataString(identity.RootId);
        return Path.Combine(rootDirectory, $"{identity.Version}_{safeId}.json");
    }

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public GraphicTemplateTreeResult Tree { get; set; } = new();
    }
}
