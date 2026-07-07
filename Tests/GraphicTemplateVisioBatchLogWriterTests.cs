namespace EBAssistant.Tests;

internal static class GraphicTemplateVisioBatchLogWriterTests
{
    public static void Run()
    {
        WritesJsonAndTextLogs();
        UsesRequestedCountForCompletedStatus();
        FormatsPartialStatusAsStopped();
    }

    private static void WritesJsonAndTextLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
        var result = new GraphicTemplateVisioBatchResult
        {
            SourceVisioPath = @"D:\work\source.vsdx",
            TargetDirectoryPath = GraphicTemplateVisioBatchPlanner.DefaultTargetDirectoryPath,
            RequestedCount = 2,
            BaseTemplateId = "base-id",
            BaseTemplateName = "基准",
            Status = "partial",
            Records =
            [
                new GraphicTemplateVisioBatchRecord
                {
                    TemplateName = "01",
                    TemplateId = "template-01",
                    SourceShapeName = "shape-1",
                    SourceShapeIndex = 1,
                    Status = "saved",
                    Message = "保存成功。"
                }
            ]
        };

        GraphicTemplateVisioBatchLogWriter.Write(result, root);

        Assert.Equal(1, Directory.GetFiles(root, "graphic-template-visio-batch-*.json").Length);
        Assert.Equal(1, Directory.GetFiles(root, "graphic-template-visio-batch-*.txt").Length);
    }

    private static void UsesRequestedCountForCompletedStatus()
    {
        var result = new GraphicTemplateVisioBatchResult
        {
            RequestedCount = 1,
            Records =
            [
                new GraphicTemplateVisioBatchRecord
                {
                    TemplateName = "01",
                    Status = "saved"
                }
            ]
        };

        GraphicTemplateVisioBatchLogWriter.ApplySummary(result);

        Assert.Equal("completed", result.Status);
    }

    private static void FormatsPartialStatusAsStopped()
    {
        var result = new GraphicTemplateVisioBatchResult
        {
            RequestedCount = 5,
            Status = "partial",
            Records =
            [
                new GraphicTemplateVisioBatchRecord { Status = "saved" },
                new GraphicTemplateVisioBatchRecord { Status = "saved" },
                new GraphicTemplateVisioBatchRecord { Status = "failed" }
            ]
        };

        var text = GraphicTemplateVisioBatchCreateForm.BuildCompletionStatusText(result, @"C:\logs");

        Assert.True(text.Contains("已停止", StringComparison.Ordinal));
        Assert.True(text.Contains("请求 5", StringComparison.Ordinal));
        Assert.True(text.Contains("已处理 3", StringComparison.Ordinal));
        Assert.True(text.Contains("保存成功 2", StringComparison.Ordinal));
        Assert.True(!text.StartsWith("完成", StringComparison.Ordinal));
    }
}
