using System.Text;

namespace EBAssistant;

public static class AdapterDiscoveryLogWriter
{
    public static string GetDefaultDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant",
            "Logs",
            "Diagnostics");
    }

    public static string Write(IEnumerable<string> diagnostics, string? directory = null)
    {
        var targetDirectory = string.IsNullOrWhiteSpace(directory) ? GetDefaultDirectory() : directory;
        Directory.CreateDirectory(targetDirectory);
        var path = Path.Combine(targetDirectory, $"eb-discovery-{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");
        File.WriteAllText(path, BuildText(diagnostics), new UTF8Encoding(false));
        return path;
    }

    public static string BuildText(IEnumerable<string> diagnostics)
    {
        var lines = new List<string>
        {
            "EBAssistant EB 连接诊断",
            "时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "程序目录：" + AppContext.BaseDirectory,
            "进程位数：" + (Environment.Is64BitProcess ? "64-bit" : "32-bit"),
            ""
        };
        lines.AddRange(diagnostics.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.Join(Environment.NewLine, lines);
    }
}
