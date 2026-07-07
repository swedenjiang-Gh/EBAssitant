using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class GraphicTemplateVisioBatchLogWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Write(GraphicTemplateVisioBatchResult result)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "GraphicTemplates");
        return Write(result, root);
    }

    public static string Write(GraphicTemplateVisioBatchResult result, string rootDirectory)
    {
        ApplySummary(result);
        Directory.CreateDirectory(rootDirectory);
        var stem = $"graphic-template-visio-batch-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
        File.WriteAllText(
            Path.Combine(rootDirectory, stem + ".json"),
            JsonSerializer.Serialize(result, Options),
            new UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(rootDirectory, stem + ".txt"),
            BuildText(result),
            new UTF8Encoding(false));
        return rootDirectory;
    }

    public static void ApplySummary(GraphicTemplateVisioBatchResult result)
    {
        result.Status = result.RequestedCount > 0 &&
            result.Records.Count == result.RequestedCount &&
            result.Records.All(record => record.Status == "saved")
            ? "completed"
            : result.Records.Any(record => record.Status == "saved") ? "partial" : "failed";
    }

    private static string BuildText(GraphicTemplateVisioBatchResult result)
    {
        var lines = new List<string>
        {
            "图形模板 Visio 批量创建结果",
            "状态：" + result.Status,
            "源 Visio 文件：" + result.SourceVisioPath,
            "目标目录：" + result.TargetDirectoryPath,
            "请求数量：" + result.RequestedCount,
            "基准模板：" + result.BaseTemplateName + " (" + result.BaseTemplateId + ")",
            "",
            "状态\t模板名称\t模板ID\t源组合图形序号\t源组合图形名称\t说明"
        };

        lines.AddRange(result.Records.Select(record =>
            string.Join(
                "\t",
                record.Status,
                record.TemplateName,
                record.TemplateId,
                record.SourceShapeIndex,
                record.SourceShapeName,
                record.Message)));

        return string.Join(Environment.NewLine, lines);
    }
}
