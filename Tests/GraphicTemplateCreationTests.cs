namespace EBAssistant.Tests;

internal static class GraphicTemplateCreationTests
{
    public static void Run()
    {
        ValidatesRequestedTotalCount();
        CalculatesCreationStatus();
        WritesCreationLogs();
    }

    private static void ValidatesRequestedTotalCount()
    {
        Assert.True(!GraphicTemplateCreation.TryParseTotalCount("1", out _, out _));
        Assert.True(GraphicTemplateCreation.TryParseTotalCount("2", out var minimum, out _));
        Assert.Equal(2, minimum);
        Assert.True(GraphicTemplateCreation.TryParseTotalCount("200", out var maximum, out _));
        Assert.Equal(200, maximum);
        Assert.True(!GraphicTemplateCreation.TryParseTotalCount("201", out _, out _));
        Assert.True(!GraphicTemplateCreation.TryParseTotalCount("abc", out _, out _));
    }

    private static void CalculatesCreationStatus()
    {
        var completed = new CreateGraphicTemplatesResult { RequestedTotalCount = 3, OriginalCount = 1, CreatedCount = 2 };
        GraphicTemplateCreation.ApplySummary(completed);
        Assert.Equal("completed", completed.Status);

        var partial = new CreateGraphicTemplatesResult { RequestedTotalCount = 4, OriginalCount = 1, CreatedCount = 1 };
        GraphicTemplateCreation.ApplySummary(partial);
        Assert.Equal("partial", partial.Status);

        var failed = new CreateGraphicTemplatesResult { RequestedTotalCount = 4, OriginalCount = 1, CreatedCount = 0 };
        GraphicTemplateCreation.ApplySummary(failed);
        Assert.Equal("failed", failed.Status);
    }

    private static void WritesCreationLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
        var result = new CreateGraphicTemplatesResult
        {
            DirectoryPath = "图形模版 / 器件 / 常规",
            RequestedTotalCount = 2,
            OriginalCount = 1,
            CreatedCount = 1,
            ConfirmedFinalCount = 2,
            Records =
            [
                new GraphicTemplateCreationRecord
                {
                    CopyNumber = 1,
                    ConfirmedTemplateId = "new-id",
                    Status = "created",
                    Message = "创建成功。"
                }
            ]
        };

        GraphicTemplateCreationLogWriter.Write(result, root);

        Assert.Equal(1, Directory.GetFiles(root, "graphic-template-creation-*.json").Length);
        Assert.Equal(1, Directory.GetFiles(root, "graphic-template-creation-*.txt").Length);
    }
}
