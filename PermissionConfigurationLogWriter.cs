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

public sealed class PermissionAssignmentLog
{
    public string EbVersion { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public bool SetRightCalled { get; set; }
    public bool RollbackPerformed { get; set; }
    public PermissionMemberAssignmentResult Result { get; set; } = new();
}

public static class PermissionAssignmentLogWriter
{
    public static string Write(PermissionAssignmentLog log)
    {
        var directory = PermissionConfigurationLogWriter.GetDirectory();
        Directory.CreateDirectory(directory);
        var stem = $"permission-assignment-{log.Timestamp:yyyyMMdd-HHmmss-fff}";

        File.WriteAllText(
            Path.Combine(directory, stem + ".json"),
            JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        var lines = new List<string>
        {
            $"EB 版本：{log.EbVersion}",
            $"时间：{log.Timestamp:yyyy-MM-dd HH:mm:ss}",
            $"状态：{log.Result.Status}",
            $"总数：{log.Result.TotalCount}",
            $"新增：{log.Result.AddedCount}",
            $"已存在跳过：{log.Result.SkippedCount}",
            $"失败：{log.Result.FailedCount}",
            $"调用 SetRight：{log.SetRightCalled}",
            $"执行回滚：{log.RollbackPerformed}",
            "",
            "明细："
        };
        lines.AddRange(log.Result.Records.Select(record =>
            $"{record.Status}\t{record.MemberName}\t{record.DirectoryName}\t{record.Message}"));
        File.WriteAllLines(Path.Combine(directory, stem + ".txt"), lines, new UTF8Encoding(false));
        return directory;
    }
}
