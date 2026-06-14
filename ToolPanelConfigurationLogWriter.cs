using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class ToolPanelConfigurationLogWriter
{
    public static string Write(AddGraphicTemplatesToToolPanelResult result)
    {
        var directory = GetDirectory();
        Directory.CreateDirectory(directory);
        var timestamp = DateTime.Now;
        var stem = $"tool-panel-add-{timestamp:yyyyMMdd-HHmmss-fff}";

        File.WriteAllText(
            Path.Combine(directory, stem + ".json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        var lines = new List<string>
        {
            $"时间：{timestamp:yyyy-MM-dd HH:mm:ss}",
            $"状态：{result.Status}",
            $"目标条目：{result.TargetDirectoryPath}",
            $"总数：{result.TotalCount}",
            $"新增：{result.AddedCount}",
            $"失败：{result.FailedCount}",
            "提示：添加成功后需要重启 EB 才能查看生效结果。",
            "",
            "明细："
        };
        lines.AddRange(result.Records.Select(record =>
            $"{record.Status}\t{record.TemplateName}\t{record.ConfirmedObjectId}\t{record.Message}"));
        File.WriteAllLines(Path.Combine(directory, stem + ".txt"), lines, new UTF8Encoding(false));
        return directory;
    }

    public static string GetDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "ToolPanelConfiguration");
}
