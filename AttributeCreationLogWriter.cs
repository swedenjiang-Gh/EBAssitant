using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class AttributeCreationLogWriter
{
    public static string Write(CreateAttributesResult result)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "Attributes");
        Directory.CreateDirectory(directory);

        var timestamp = DateTime.Now;
        var stem = $"attribute-creation-{timestamp:yyyyMMdd-HHmmss-fff}";
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(directory, stem + ".json"),
            JsonSerializer.Serialize(result, options),
            new UTF8Encoding(false));

        var lines = new List<string>
        {
            $"状态：{result.Status}",
            $"时间：{timestamp:yyyy-MM-dd HH:mm:ss}",
            $"目标目录：{result.TargetFolder}",
            $"结果说明：{result.Message}",
            $"创建成功：{result.Records.Count(x => x.Status == "创建成功")}",
            $"创建失败：{result.Records.Count(x => x.Status == "创建失败")}",
            $"已回滚：{result.Records.Count(x => x.Status == "已回滚")}",
            $"回滚失败：{result.Records.Count(x => x.Status == "回滚失败")}",
            $"未处理：{result.Records.Count(x => x.Status == "未处理")}",
            "",
            "Excel行号\t属性名称\t属性类型\t状态\t说明"
        };
        lines.AddRange(result.Records.Select(x => $"{x.RowNumber}\t{x.Name}\t{x.Type}\t{x.Status}\t{x.Message}"));
        if (result.RollbackErrors.Count > 0)
        {
            lines.Add("");
            lines.Add("回滚错误：");
            lines.AddRange(result.RollbackErrors);
        }

        File.WriteAllLines(Path.Combine(directory, stem + ".txt"), lines, new UTF8Encoding(false));
        return directory;
    }
}
