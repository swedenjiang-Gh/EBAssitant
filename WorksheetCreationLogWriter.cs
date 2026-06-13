using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class WorksheetCreationLogWriter
{
    public static string Write(CreateWorksheetsResult result)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "Worksheets");
        Directory.CreateDirectory(directory);

        var timestamp = DateTime.Now;
        var stem = $"worksheet-creation-{timestamp:yyyyMMdd-HHmmss-fff}";
        File.WriteAllText(
            Path.Combine(directory, stem + ".json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        var lines = new List<string>
        {
            $"状态：{result.Status}",
            $"时间：{timestamp:yyyy-MM-dd HH:mm:ss}",
            $"创建成功：{result.Records.Count(x => x.Status == "created")}",
            $"创建失败：{result.Records.Count(x => x.Status == "failed")}",
            $"校验跳过：{result.Records.Count(x => x.Status == "validation_skipped")}",
            $"未处理：{result.Records.Count(x => x.Status == "unprocessed")}",
            "",
            "Excel页签\t原页签名称\t最终工作表名称\t保存位置\t对象类型\t列数\t列配置\t列标签状态\t列宽状态\t状态\t说明"
        };
        lines.AddRange(result.Records.Select(x =>
            $"{x.SheetIndex}\t{x.OriginalName}\t{x.FinalName}\t{x.SavePath}\t{x.ObjectType}\t{x.ColumnCount}\t{x.ColumnSummary}\t{x.LabelStatus}\t{x.AutoWidthStatus}\t{x.Status}\t{x.Message}"));
        File.WriteAllLines(Path.Combine(directory, stem + ".txt"), lines, new UTF8Encoding(false));
        return directory;
    }
}
