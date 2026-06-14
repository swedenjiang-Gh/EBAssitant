namespace EBAssistant.Tests;

internal static class GraphicTemplateMigrationResultTests
{
    public static void Run()
    {
        CalculatesResultCounts();
        WritesJsonAndTextLogs();
        WritesSeparateLogFilesForEachRun();
    }

    private static void CalculatesResultCounts()
    {
        var result = new MoveGraphicTemplatesResult
        {
            Records =
            [
                new GraphicTemplateMigrationRecord { Status = "moved" },
                new GraphicTemplateMigrationRecord { Status = "failed" },
                new GraphicTemplateMigrationRecord { Status = "copied" }
            ]
        };

        GraphicTemplateMigrationSummary.Apply(result);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.MovedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal("partial_failed", result.Status);
    }

    private static void WritesJsonAndTextLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
        var result = new MoveGraphicTemplatesResult
        {
            TargetDirectoryPath = "图形模板 / 器件 / 常规",
            Records =
            [
                new GraphicTemplateMigrationRecord
                {
                    TemplateName = "M1",
                    SourceDirectoryPath = "图形模板 / 器件",
                    TargetDirectoryPath = "图形模板 / 器件 / 常规",
                    Status = "moved",
                    Message = "迁移成功。"
                }
            ]
        };

        var directory = GraphicTemplateMigrationLogWriter.Write(result, root);

        Assert.Equal(root, directory);
        Assert.True(Directory.GetFiles(directory, "graphic-template-migration-*.json").Length == 1);
        Assert.True(Directory.GetFiles(directory, "graphic-template-migration-*.txt").Length == 1);
    }

    private static void WritesSeparateLogFilesForEachRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
        var result = new MoveGraphicTemplatesResult
        {
            Records =
            [
                new GraphicTemplateMigrationRecord { TemplateName = "M1", Status = "moved" }
            ]
        };

        GraphicTemplateMigrationLogWriter.Write(result, root);
        GraphicTemplateMigrationLogWriter.Write(result, root);

        Assert.Equal(2, Directory.GetFiles(root, "graphic-template-migration-*.json").Length);
        Assert.Equal(2, Directory.GetFiles(root, "graphic-template-migration-*.txt").Length);
    }
}
