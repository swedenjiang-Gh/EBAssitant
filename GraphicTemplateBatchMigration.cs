namespace EBAssistant;

public static class GraphicTemplateBatchMigration
{
    public static string NormalizeRelativePath(string path)
    {
        return string.Join(
            "/",
            (path ?? "")
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));
    }

    public static List<GraphicTemplateDirectoryNode> FindDirectories(GraphicTemplateTreeResult tree, string relativePath)
    {
        var expected = NormalizeRelativePath(relativePath);
        if (expected.Length == 0) return [];

        var result = new List<GraphicTemplateDirectoryNode>();
        foreach (var node in tree.Nodes)
        {
            CollectDirectories(node, "", expected, result);
        }
        return result;
    }

    public static int MatchTemplateCount(GraphicTemplateTreeResult tree, string sourcePath, string templateName)
    {
        var name = (templateName ?? "").Trim();
        if (name.Length == 0) return 0;
        return FindDirectories(tree, sourcePath)
            .SelectMany(directory => directory.Templates)
            .Count(template => string.Equals(template.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public static List<GraphicTemplateBatchPreviewRow> BuildPreview(
        IEnumerable<GraphicTemplateBatchRawRow> rows,
        GraphicTemplateTreeResult tree)
    {
        return rows.Select(row => BuildPreviewRow(row, tree)).ToList();
    }

    private static GraphicTemplateBatchPreviewRow BuildPreviewRow(
        GraphicTemplateBatchRawRow row,
        GraphicTemplateTreeResult tree)
    {
        var sourcePath = NormalizeRelativePath(row.SourceDirectoryPath);
        var targetPath = NormalizeRelativePath(row.TargetDirectoryPath);
        var templateName = row.TemplateName.Trim();
        var errors = new List<string>();
        var sourceMatches = sourcePath.Length == 0 ? [] : FindDirectories(tree, sourcePath);
        var targetMatches = targetPath.Length == 0 ? [] : FindDirectories(tree, targetPath);
        var templateMatchCount = 0;

        if (sourcePath.Length == 0) errors.Add("源目录为空");
        if (templateName.Length == 0) errors.Add("模板图形名称为空");
        if (targetPath.Length == 0) errors.Add("目标目录为空");

        if (sourcePath.Length > 0)
        {
            if (sourceMatches.Count == 0) errors.Add("源目录不存在");
            else if (sourceMatches.Count > 1) errors.Add("源目录匹配到多个目录");
            else if (!GraphicTemplateSelection.IsLeafDirectory(sourceMatches[0])) errors.Add("源目录不是最后一级目录");
            else if (templateName.Length > 0)
            {
                templateMatchCount = sourceMatches[0].Templates.Count(
                    template => string.Equals(template.Name.Trim(), templateName, StringComparison.OrdinalIgnoreCase));
                if (templateMatchCount == 0) errors.Add("源目录中未找到模板图形");
                else if (templateMatchCount > 1) errors.Add("源目录中匹配到多个同名模板图形");
            }
        }

        if (targetPath.Length > 0)
        {
            if (targetMatches.Count == 0) errors.Add("目标目录不存在");
            else if (targetMatches.Count > 1) errors.Add("目标目录匹配到多个目录");
            else if (!GraphicTemplateSelection.IsLeafDirectory(targetMatches[0])) errors.Add("目标目录不是最后一级目录");
        }

        return new GraphicTemplateBatchPreviewRow
        {
            ExcelRowNumber = row.ExcelRowNumber,
            Sequence = row.Sequence.Trim(),
            SourceDirectoryPath = sourcePath,
            TemplateName = templateName,
            TargetDirectoryPath = targetPath,
            SourceTemplateMatchCount = templateMatchCount,
            Validation = errors.Count == 0 ? "有效" : string.Join("；", errors)
        };
    }

    private static void CollectDirectories(
        GraphicTemplateDirectoryNode node,
        string parentPath,
        string expected,
        List<GraphicTemplateDirectoryNode> result)
    {
        var path = NormalizeRelativePath(parentPath.Length == 0 ? node.Name : parentPath + "/" + node.Name);
        if (string.Equals(path, expected, StringComparison.OrdinalIgnoreCase)) result.Add(node);
        foreach (var child in node.Children)
        {
            CollectDirectories(child, path, expected, result);
        }
    }
}
