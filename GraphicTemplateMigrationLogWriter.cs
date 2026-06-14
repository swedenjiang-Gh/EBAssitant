using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class GraphicTemplateMigrationSummary
{
    public static void Apply(MoveGraphicTemplatesResult result)
    {
        result.TotalCount = result.Records.Count;
        result.MovedCount = result.Records.Count(record => record.Status is "moved" or "copied");
        result.FailedCount = result.TotalCount - result.MovedCount;
        result.Status = result.FailedCount == 0
            ? "completed"
            : result.MovedCount == 0 ? "failed" : "partial_failed";
    }
}

public static class GraphicTemplateMigrationLogWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Write(MoveGraphicTemplatesResult result)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "GraphicTemplates");
        return Write(result, root);
    }

    public static string Write(MoveGraphicTemplatesResult result, string rootDirectory)
    {
        GraphicTemplateMigrationSummary.Apply(result);
        Directory.CreateDirectory(rootDirectory);
        var stem = $"graphic-template-migration-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
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

    private static string BuildText(MoveGraphicTemplatesResult result)
    {
        var lines = new List<string>
        {
            "图形模板迁移结果",
            "状态：" + result.Status,
            "目标目录：" + result.TargetDirectoryPath,
            "总数：" + result.TotalCount,
            "成功：" + result.MovedCount,
            "失败：" + result.FailedCount,
            "",
            "状态\t模板名称\t原模板ID\t确认模板ID\t迁移路径\t说明"
        };
        foreach (var record in result.Records)
        {
            lines.Add($"{record.Status}\t{record.TemplateName}\t{record.TemplateId}\t{record.ConfirmedTemplateId}\t{record.SourceDirectoryPath} -> {record.TargetDirectoryPath}\t{record.Message}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
