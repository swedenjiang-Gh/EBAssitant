using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class GraphicTemplateCreationLogWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Write(CreateGraphicTemplatesResult result)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "GraphicTemplates");
        return Write(result, root);
    }

    public static string Write(CreateGraphicTemplatesResult result, string rootDirectory)
    {
        GraphicTemplateCreation.ApplySummary(result);
        Directory.CreateDirectory(rootDirectory);
        var stem = $"graphic-template-creation-{DateTime.Now:yyyyMMdd-HHmmss-fff}";
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

    private static string BuildText(CreateGraphicTemplatesResult result)
    {
        var lines = new List<string>
        {
            "新建模板图形结果",
            "状态：" + result.Status,
            "目录：" + result.DirectoryPath,
            "源模板：" + result.SourceTemplateName,
            "请求总数量：" + result.RequestedTotalCount,
            "原有数量：" + result.OriginalCount,
            "成功创建：" + result.CreatedCount,
            "最终确认数量：" + result.ConfirmedFinalCount,
            "",
            "复制序号\t状态\t确认模板ID\t说明"
        };
        lines.AddRange(result.Records.Select(
            record => $"{record.CopyNumber}\t{record.Status}\t{record.ConfirmedTemplateId}\t{record.Message}"));
        return string.Join(Environment.NewLine, lines);
    }
}
