using System.Reflection;

namespace EBAssistant.Tests;

internal static class GraphicTemplateReadDiagnosticsTests
{
    public static void Run()
    {
        Assert.Equal(180000, GraphicTemplateReadDiagnostics.GetTimeoutMilliseconds("GetGraphicTemplateTree"));
        Assert.Equal(60000, GraphicTemplateReadDiagnostics.GetTimeoutMilliseconds("GetGraphicTemplateDirectory"));
        Assert.Equal(30000, GraphicTemplateReadDiagnostics.GetTimeoutMilliseconds("GetConnectionInfo"));
        Assert.Equal(30000, GraphicTemplateReadDiagnostics.GetTimeoutMilliseconds("CreateGraphicTemplates"));
        ReportsAndPersistsProgressBeforeProcessExit();
        LogFailureDoesNotDiscardLastProgress();
    }

    private static void ReportsAndPersistsProgressBeforeProcessExit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EBAssistant.Tests", Guid.NewGuid().ToString("N"));
        var log = new GraphicTemplateReadDiagnostics("GetGraphicTemplateTree", "adapter.exe", directory);
        using var reader = new PendingReader();
        var received = new List<string>();
        var method = typeof(EbAdapterClient).GetMethod("ReadErrorAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var task = (Task<string>)method.Invoke(null, [reader, log, new InlineProgress(received.Add)])!;

        Assert.True(!task.IsCompleted);
        Assert.Equal(1, received.Count);
        Assert.Equal("读取目录：器件 | 目录 2，模板 17", log.LastProgress);
        Assert.True(File.ReadAllText(log.LogPath).Contains(log.LastProgress));
        Assert.True(!File.ReadAllText(log.LogPath).Contains("native warning"));
        var timeout = log.TimeoutMessage("GetGraphicTemplateTree");
        Assert.True(timeout.Contains(log.LastProgress) && timeout.Contains(log.LogPath));
        Assert.True(!timeout.Contains("权限级别"));

        reader.End.TrySetResult(null);
        Assert.Equal("native warning" + Environment.NewLine, task.GetAwaiter().GetResult());
        Assert.True(!File.ReadAllBytes(log.LogPath).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    private static void LogFailureDoesNotDiscardLastProgress()
    {
        var directoryFile = Path.GetTempFileName();
        var log = new GraphicTemplateReadDiagnostics("GetGraphicTemplateDirectory", "adapter.exe", directoryFile);
        Assert.True(log.AcceptLine(GraphicTemplateReadDiagnostics.ProgressPrefix + "浅层读取目录：器件"));
        Assert.Equal("浅层读取目录：器件", log.LastProgress);
        Assert.True(log.LogLocation.Contains("日志保存失败"));
    }

    private sealed class InlineProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    private sealed class PendingReader() : StreamReader(Stream.Null)
    {
        private int index;
        public TaskCompletionSource<string?> End { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task<string?> ReadLineAsync() => index++ switch
        {
            0 => Task.FromResult<string?>("native warning"),
            1 => Task.FromResult<string?>(GraphicTemplateReadDiagnostics.ProgressPrefix + "读取目录：器件 | 目录 2，模板 17"),
            _ => End.Task
        };
    }
}
