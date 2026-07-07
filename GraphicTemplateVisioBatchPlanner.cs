namespace EBAssistant;

public sealed record BatchValidationResult(bool IsValid, string Message);

public sealed record VisioGroupShapeInfo(
    string Name,
    int SourceIndex,
    double PinX,
    double PinY,
    double Width,
    double Height);

public sealed record GraphicTemplateVisioBatchPair(
    string TemplateId,
    string TemplateName,
    string SourceShapeName,
    int SourceShapeIndex);

public sealed record GraphicTemplateVisioBatchStep(
    string TemplateName,
    string SourceShapeName,
    int SourceShapeIndex);

public static class GraphicTemplateVisioBatchPlanner
{
    public const int DefaultRequestedCount = 20;
    public const int MaxRequestedCount = 200;
    public const string DefaultTargetDirectoryPath = "图形模板 / 器件 / 常规 / 新建图形模板";

    public static bool TryParseRequestedCount(string text, out int count, out string validation)
    {
        if (!int.TryParse(text, out count) || count < 1 || count > MaxRequestedCount)
        {
            count = 0;
            validation = $"请输入 1-{MaxRequestedCount} 之间的正整数。";
            return false;
        }

        validation = "有效";
        return true;
    }

    public static BatchValidationResult ValidateSourceVisioPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new BatchValidationResult(false, "请选择源 Visio 文件。");
        }

        var extension = Path.GetExtension(path.Trim());
        return extension.Equals(".vsd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vsdx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vsdm", StringComparison.OrdinalIgnoreCase)
            ? new BatchValidationResult(true, "有效")
            : new BatchValidationResult(false, "源文件必须是 Visio 文件（.vsd、.vsdx、.vsdm）。");
    }

    public static List<string> GenerateTemplateNames(int count)
    {
        return Enumerable.Range(1, count).Select(index => index.ToString("00")).ToList();
    }

    public static BatchValidationResult ValidateTargetDirectory(GraphicTemplateDirectoryNode? directory, int count)
    {
        if (directory is null)
        {
            return new BatchValidationResult(false, "未选择图形模板目录。");
        }

        if (!GraphicTemplateSelection.IsLeafDirectory(directory))
        {
            return new BatchValidationResult(false, "目标目录必须是最后一级图形模板目录。");
        }

        if (directory.Templates.Count == 0)
        {
            return new BatchValidationResult(false, "目标目录中没有可复制的基准图形模板。");
        }

        var requiredNames = GenerateTemplateNames(count).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var conflicts = directory.Templates
            .Where(item => requiredNames.Contains(item.Name))
            .Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (conflicts.Count > 0)
        {
            return new BatchValidationResult(false, "目标目录中已存在名称：" + string.Join(", ", conflicts) + "。");
        }

        return new BatchValidationResult(true, "有效");
    }

    public static List<VisioGroupShapeInfo> SortGroupShapes(IEnumerable<VisioGroupShapeInfo> shapes, double rowTolerance)
    {
        return shapes
            .OrderByDescending(shape => QuantizeRow(shape.PinY, rowTolerance))
            .ThenBy(shape => shape.PinX)
            .ThenBy(shape => shape.SourceIndex)
            .ToList();
    }

    public static BatchValidationResult ValidateSourceShapes(IReadOnlyCollection<VisioGroupShapeInfo> shapes, int requiredCount)
    {
        return shapes.Count >= requiredCount
            ? new BatchValidationResult(true, "有效")
            : new BatchValidationResult(false, $"源 Visio 文件中组合图形数量为 {shapes.Count}，少于请求数量 {requiredCount}。");
    }

    public static List<GraphicTemplateVisioBatchPair> BuildPairs(
        IReadOnlyList<GraphicTemplateItem> templates,
        IReadOnlyList<VisioGroupShapeInfo> sortedShapes)
    {
        if (templates.Count != sortedShapes.Count)
        {
            throw new InvalidOperationException("模板数量与源组合图形数量不一致。");
        }

        var orderedTemplates = templates.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<GraphicTemplateVisioBatchPair>();
        for (var index = 0; index < orderedTemplates.Count; index++)
        {
            result.Add(new GraphicTemplateVisioBatchPair(
                orderedTemplates[index].Id,
                orderedTemplates[index].Name,
                sortedShapes[index].Name,
                sortedShapes[index].SourceIndex));
        }

        return result;
    }

    public static List<GraphicTemplateVisioBatchStep> BuildSteps(
        IReadOnlyList<string> targetNames,
        IReadOnlyList<VisioGroupShapeInfo> sortedShapes)
    {
        if (targetNames.Count != sortedShapes.Count)
        {
            throw new InvalidOperationException("目标名称数量与源组合图形数量不一致。");
        }

        var result = new List<GraphicTemplateVisioBatchStep>();
        for (var index = 0; index < targetNames.Count; index++)
        {
            result.Add(new GraphicTemplateVisioBatchStep(
                targetNames[index],
                sortedShapes[index].Name,
                sortedShapes[index].SourceIndex));
        }

        return result;
    }

    private static double QuantizeRow(double pinY, double rowTolerance)
    {
        if (rowTolerance <= 0)
        {
            return pinY;
        }

        return Math.Round(pinY / rowTolerance) * rowTolerance;
    }

}
