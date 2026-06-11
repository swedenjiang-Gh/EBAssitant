using System.Text.Json;

namespace EBAssistant;

public static class TypeDefinitionLogWriter
{
    public static string Write(ApplyTypeDefinitionDialogsResult result)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EBAssistant", "Logs", "TypeDefinitions");
        Directory.CreateDirectory(dir);
        var stem = $"type-definition-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.WriteAllText(Path.Combine(dir, stem + ".json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        var lines = new List<string> { $"状态: {result.Status}", $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "" };
        lines.AddRange(result.Records.Select(x => $"{x.Status}\t{x.TypeItemName}\t{x.TypeItemId}\tAID={x.AttributeId}\t选项卡={x.TabName}\t{x.Message}"));
        if (result.UnprocessedTypeItemIds.Count > 0)
        {
            lines.Add("");
            lines.Add("未处理 TypeItem:");
            lines.AddRange(result.UnprocessedTypeItemIds);
        }
        if (result.UnprocessedOperations.Count > 0)
        {
            lines.Add("");
            lines.Add("未处理操作:");
            lines.AddRange(result.UnprocessedOperations);
        }
        File.WriteAllLines(Path.Combine(dir, stem + ".txt"), lines);
        return dir;
    }
}
