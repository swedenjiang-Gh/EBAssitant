namespace EBAssistant.Tests;

internal static class AdapterDiscoveryLogWriterTests
{
    public static void Run()
    {
        BuildsReadableDiagnosticsText();
        WritesDiagnosticsFile();
    }

    private static void BuildsReadableDiagnosticsText()
    {
        var text = AdapterDiscoveryLogWriter.BuildText(new[]
        {
            "EB 2024：未检测到可附着的 EB 2024。",
            "GetActiveObject failed: EngineeringBase.Application.31"
        });

        Assert.True(text.Contains("EBAssistant EB 连接诊断", StringComparison.Ordinal));
        Assert.True(text.Contains("程序目录：", StringComparison.Ordinal));
        Assert.True(text.Contains("EB 2024", StringComparison.Ordinal));
        Assert.True(text.Contains("GetActiveObject failed", StringComparison.Ordinal));
    }

    private static void WritesDiagnosticsFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));

        var path = AdapterDiscoveryLogWriter.Write(new[] { "diagnostic line" }, root);

        Assert.True(File.Exists(path));
        Assert.True(File.ReadAllText(path).Contains("diagnostic line", StringComparison.Ordinal));
    }
}
