namespace EBAssistant.Tests;

internal static class GraphicTemplateBatchMigrationTests
{
    public static void Run()
    {
        NormalizesAndMatchesRelativePaths();
        ValidatesPreviewRows();
    }

    private static void NormalizesAndMatchesRelativePaths()
    {
        var tree = CreateTree();

        Assert.Equal("器件/常规", GraphicTemplateBatchMigration.NormalizeRelativePath(" 器件 / 常规 "));
        Assert.Equal(1, GraphicTemplateBatchMigration.FindDirectories(tree, "器件/常规").Count);
        Assert.Equal(2, GraphicTemplateBatchMigration.MatchTemplateCount(tree, "器件/常规", "A"));
        Assert.Equal(0, GraphicTemplateBatchMigration.FindDirectories(tree, "图形模版/器件/常规").Count);
    }

    private static void ValidatesPreviewRows()
    {
        var rows = new[]
        {
            new GraphicTemplateBatchRawRow
            {
                ExcelRowNumber = 2,
                Sequence = "1",
                SourceDirectoryPath = "器件/仪表",
                TemplateName = "B",
                TargetDirectoryPath = "器件/常规"
            },
            new GraphicTemplateBatchRawRow
            {
                ExcelRowNumber = 3,
                Sequence = "2",
                SourceDirectoryPath = "器件/常规",
                TemplateName = "A",
                TargetDirectoryPath = "器件/仪表"
            },
            new GraphicTemplateBatchRawRow
            {
                ExcelRowNumber = 4,
                SourceDirectoryPath = "器件",
                TemplateName = "B",
                TargetDirectoryPath = "缺失"
            },
            new GraphicTemplateBatchRawRow { ExcelRowNumber = 5 }
        };

        var preview = GraphicTemplateBatchMigration.BuildPreview(rows, CreateTree());

        Assert.Equal("有效", preview[0].Validation);
        Assert.Equal(1, preview[0].SourceTemplateMatchCount);
        Assert.Equal("源目录中匹配到多个同名模板图形", preview[1].Validation);
        Assert.Equal(2, preview[1].SourceTemplateMatchCount);
        Assert.Equal("源目录不是最后一级目录；目标目录不存在", preview[2].Validation);
        Assert.Equal("源目录为空；模板图形名称为空；目标目录为空", preview[3].Validation);
    }

    private static GraphicTemplateTreeResult CreateTree() => new()
    {
        Nodes =
        [
            new GraphicTemplateDirectoryNode
            {
                Id = "devices",
                Name = "器件",
                FullPath = "图形模版 / 器件",
                Children =
                [
                    new GraphicTemplateDirectoryNode
                    {
                        Id = "general",
                        Name = "常规",
                        FullPath = "图形模版 / 器件 / 常规",
                        Templates =
                        [
                            new GraphicTemplateItem { Name = "A" },
                            new GraphicTemplateItem { Name = "A" }
                        ]
                    },
                    new GraphicTemplateDirectoryNode
                    {
                        Id = "meters",
                        Name = "仪表",
                        FullPath = "图形模版 / 器件 / 仪表",
                        Templates =
                        [
                            new GraphicTemplateItem { Name = "B" }
                        ]
                    }
                ]
            }
        ]
    };
}
