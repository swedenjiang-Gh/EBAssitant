using System.Text;
using System.Text.Json;

namespace EBAssistant;

public sealed class PermissionConfigurationReadLog
{
    public string Source { get; set; } = "";
    public string EbVersion { get; set; } = "";
    public string IdentityRootName { get; set; } = "";
    public string IdentityUsersAndGroupsName { get; set; } = "";
    public int LeftNodeCount { get; set; }
    public int RightNodeCount { get; set; }
    public List<string> LeftNodeNames { get; set; } = [];
    public List<string> RightNodeNames { get; set; } = [];
}

public static class PermissionConfigurationLogWriter
{
    public static string Write(PermissionConfigurationReadLog log)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "PermissionConfiguration");
        Directory.CreateDirectory(directory);

        var timestamp = DateTime.Now;
        var stem = $"permission-read-{timestamp:yyyyMMdd-HHmmss-fff}";

        File.WriteAllText(
            Path.Combine(directory, stem + ".json"),
            JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        var lines = new List<string>
        {
            $"来源：{log.Source}",
            $"EB 版本：{log.EbVersion}",
            $"根目录：{log.IdentityRootName}",
            $"用户及用户组目录：{log.IdentityUsersAndGroupsName}",
            $"用户及用户组节点数：{log.LeftNodeCount}",
            $"权限控制目录数：{log.RightNodeCount}",
            $"时间：{timestamp:yyyy-MM-dd HH:mm:ss}",
            "",
            "用户及用户组节点："
        };
        lines.AddRange(log.LeftNodeNames);
        lines.Add("");
        lines.Add("权限控制目录：");
        lines.AddRange(log.RightNodeNames);

        File.WriteAllLines(Path.Combine(directory, stem + ".txt"), lines, new UTF8Encoding(false));
        return directory;
    }

    public static string GetDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "PermissionConfiguration");
    }
}
