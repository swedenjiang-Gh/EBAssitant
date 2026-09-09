using System.Text;

namespace EBAssistant;

public sealed class GraphicTemplateReadDiagnostics
{
    public const string ProgressPrefix = "[GraphicTemplateRead] ";
    public string LastProgress { get; private set; } = "正在启动适配器";
    public string LogPath { get; }
    public string LogError { get; private set; } = "";

    public static bool IsReadOperation(string operation) =>
        operation is "GetGraphicTemplateTree" or "GetGraphicTemplateDirectory";

    public static int GetTimeoutMilliseconds(string operation) => operation switch
    {
        "GetGraphicTemplateTree" => 180000,
        "GetGraphicTemplateDirectory" => 60000,
        "GetAttributeFolderTree" => 60000,
        _ => 30000
    };

    public GraphicTemplateReadDiagnostics(string operation, string adapterPath, string? directory = null)
    {
        directory ??= Path.Combine(AdapterDiscoveryLogWriter.GetDefaultDirectory(), "GraphicTemplateReads");
        LogPath = Path.Combine(directory, $"graphic-read-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.txt");
        Append($"操作：{operation}；适配器：{adapterPath}；超时上限：{GetTimeoutMilliseconds(operation) / 1000} 秒");
    }

    public bool AcceptLine(string line)
    {
        if (!line.StartsWith(ProgressPrefix, StringComparison.Ordinal)) return false;
        LastProgress = line[ProgressPrefix.Length..];
        Append(LastProgress);
        return true;
    }

    public void Append(string message)
    {
        if (!string.IsNullOrEmpty(LogError)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}", new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogError = ex.Message;
        }
    }

    public string LogLocation => string.IsNullOrEmpty(LogError)
        ? $"读取日志：{LogPath}"
        : $"读取日志保存失败：{LogError}";

    public string TimeoutMessage(string operation) =>
        $"图形模板读取超时：{operation} 超过 {GetTimeoutMilliseconds(operation) / 1000} 秒未完成。{Environment.NewLine}" +
        $"最后进度：{LastProgress}{Environment.NewLine}请查看读取日志定位耗时阶段；本次未生成完整读取结果。{Environment.NewLine}{LogLocation}";
}
