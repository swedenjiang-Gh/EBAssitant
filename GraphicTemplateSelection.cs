namespace EBAssistant;

public static class GraphicTemplateSelection
{
    public static List<GraphicTemplateDirectoryNode> GetLeafDirectories(GraphicTemplateTreeResult tree)
    {
        var result = new List<GraphicTemplateDirectoryNode>();
        foreach (var node in tree.Nodes)
        {
            CollectLeaves(node, result);
        }
        return result;
    }

    public static GraphicTemplateDirectoryNode? FindDirectory(GraphicTemplateTreeResult tree, string id)
    {
        foreach (var node in tree.Nodes)
        {
            var found = FindDirectory(node, id);
            if (found is not null) return found;
        }
        return null;
    }

    public static bool IsLeafDirectory(GraphicTemplateDirectoryNode node)
    {
        return node.Children.Count == 0;
    }

    private static void CollectLeaves(GraphicTemplateDirectoryNode node, List<GraphicTemplateDirectoryNode> result)
    {
        if (IsLeafDirectory(node))
        {
            result.Add(node);
            return;
        }
        foreach (var child in node.Children)
        {
            CollectLeaves(child, result);
        }
    }

    private static GraphicTemplateDirectoryNode? FindDirectory(GraphicTemplateDirectoryNode node, string id)
    {
        if (string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase)) return node;
        foreach (var child in node.Children)
        {
            var found = FindDirectory(child, id);
            if (found is not null) return found;
        }
        return null;
    }
}
