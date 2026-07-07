namespace EBAssistant.Tests;

internal static class GraphicTemplateVisioBatchPlannerTests
{
    public static void Run()
    {
        ParsesRequestedCountFromUserInput();
        ValidatesSelectedSourceVisioPath();
        GeneratesNames01To20();
        AllowsSelectedLeafDirectoryOutsideDefaultPath();
        DetectsNameConflicts();
        AcceptsMoreSourceShapesThanRequested();
        RejectsInsufficientSourceShapes();
        PairsCreatedTemplatesWithSourceShapes();
        BuildsSequentialStepsBeforeTemplatesExist();
        SortsShapePositionsTopToBottomLeftToRight();
    }

    private static void ParsesRequestedCountFromUserInput()
    {
        Assert.True(GraphicTemplateVisioBatchPlanner.TryParseRequestedCount("3", out var count, out _));
        Assert.Equal(3, count);
        Assert.True(GraphicTemplateVisioBatchPlanner.TryParseRequestedCount("200", out var maximum, out _));
        Assert.Equal(200, maximum);
        Assert.True(!GraphicTemplateVisioBatchPlanner.TryParseRequestedCount("0", out _, out _));
        Assert.True(!GraphicTemplateVisioBatchPlanner.TryParseRequestedCount("201", out _, out _));
        Assert.True(!GraphicTemplateVisioBatchPlanner.TryParseRequestedCount("abc", out _, out _));
    }

    private static void ValidatesSelectedSourceVisioPath()
    {
        Assert.True(GraphicTemplateVisioBatchPlanner.ValidateSourceVisioPath(@"D:\work\source.vsdx").IsValid);
        Assert.True(GraphicTemplateVisioBatchPlanner.ValidateSourceVisioPath(@"D:\work\source.vsd").IsValid);
        Assert.True(GraphicTemplateVisioBatchPlanner.ValidateSourceVisioPath(@"D:\work\source.vsdm").IsValid);
        Assert.True(!GraphicTemplateVisioBatchPlanner.ValidateSourceVisioPath("").IsValid);
        Assert.True(!GraphicTemplateVisioBatchPlanner.ValidateSourceVisioPath(@"D:\work\source.xlsx").IsValid);
    }

    private static void GeneratesNames01To20()
    {
        var names = GraphicTemplateVisioBatchPlanner.GenerateTemplateNames(20);
        Assert.Equal("01", names[0]);
        Assert.Equal("09", names[8]);
        Assert.Equal("10", names[9]);
        Assert.Equal("20", names[19]);
    }

    private static void AllowsSelectedLeafDirectoryOutsideDefaultPath()
    {
        var directory = new GraphicTemplateDirectoryNode
        {
            FullPath = "图形模版 / 器件 / 常规 / 新建图形模板",
            Templates =
            [
                new GraphicTemplateItem { Id = "base", Name = "基准" }
            ]
        };

        var validation = GraphicTemplateVisioBatchPlanner.ValidateTargetDirectory(directory, 20);

        Assert.True(validation.IsValid);
        Assert.Equal("有效", validation.Message);
    }

    private static void DetectsNameConflicts()
    {
        var directory = new GraphicTemplateDirectoryNode
        {
            FullPath = "图形模板 / 器件 / 常规 / 新建图形模板",
            Templates =
            [
                new GraphicTemplateItem { Id = "base", Name = "基准" },
                new GraphicTemplateItem { Id = "existing", Name = "03" }
            ]
        };

        var validation = GraphicTemplateVisioBatchPlanner.ValidateTargetDirectory(directory, 20);

        Assert.True(!validation.IsValid);
        Assert.Equal("目标目录中已存在名称：03。", validation.Message);
    }

    private static void AcceptsMoreSourceShapesThanRequested()
    {
        var shapes = Enumerable.Range(1, 5)
            .Select(index => new VisioGroupShapeInfo("shape-" + index, index, index, index, 1, 1))
            .ToList();

        var validation = GraphicTemplateVisioBatchPlanner.ValidateSourceShapes(shapes, requiredCount: 3);

        Assert.True(validation.IsValid);
        Assert.Equal("有效", validation.Message);
    }

    private static void RejectsInsufficientSourceShapes()
    {
        var shapes = Enumerable.Range(1, 2)
            .Select(index => new VisioGroupShapeInfo("shape-" + index, index, index, index, 1, 1))
            .ToList();

        var validation = GraphicTemplateVisioBatchPlanner.ValidateSourceShapes(shapes, requiredCount: 3);

        Assert.True(!validation.IsValid);
        Assert.Equal("源 Visio 文件中组合图形数量为 2，少于请求数量 3。", validation.Message);
    }

    private static void PairsCreatedTemplatesWithSourceShapes()
    {
        var templates = Enumerable.Range(1, 20)
            .Select(index => new GraphicTemplateItem { Id = "template-" + index, Name = index.ToString("00") })
            .ToList();
        var shapes = Enumerable.Range(1, 20)
            .Select(index => new VisioGroupShapeInfo("shape-" + index, index, index, 20 - index, 1, 1))
            .ToList();

        var pairs = GraphicTemplateVisioBatchPlanner.BuildPairs(templates, shapes);

        Assert.Equal(20, pairs.Count);
        Assert.Equal("01", pairs[0].TemplateName);
        Assert.Equal("shape-1", pairs[0].SourceShapeName);
        Assert.Equal("20", pairs[19].TemplateName);
        Assert.Equal("shape-20", pairs[19].SourceShapeName);
    }

    private static void BuildsSequentialStepsBeforeTemplatesExist()
    {
        var names = GraphicTemplateVisioBatchPlanner.GenerateTemplateNames(20);
        var shapes = Enumerable.Range(1, 20)
            .Select(index => new VisioGroupShapeInfo("shape-" + index, index, index, 20 - index, 1, 1))
            .ToList();

        var steps = GraphicTemplateVisioBatchPlanner.BuildSteps(names, shapes);

        Assert.Equal(20, steps.Count);
        Assert.Equal("01", steps[0].TemplateName);
        Assert.Equal("shape-1", steps[0].SourceShapeName);
        Assert.Equal(1, steps[0].SourceShapeIndex);
        Assert.Equal("20", steps[19].TemplateName);
        Assert.Equal("shape-20", steps[19].SourceShapeName);
        Assert.Equal(20, steps[19].SourceShapeIndex);
    }

    private static void SortsShapePositionsTopToBottomLeftToRight()
    {
        var shapes = new List<VisioGroupShapeInfo>
        {
            new("bottom-left", 1, 1, 2, 1, 1),
            new("top-right", 2, 5, 10.01, 1, 1),
            new("top-left", 3, 1, 10, 1, 1),
            new("bottom-right", 4, 6, 2.03, 1, 1)
        };

        var sorted = GraphicTemplateVisioBatchPlanner.SortGroupShapes(shapes, rowTolerance: 0.1);

        Assert.Equal("top-left", sorted[0].Name);
        Assert.Equal("top-right", sorted[1].Name);
        Assert.Equal("bottom-left", sorted[2].Name);
        Assert.Equal("bottom-right", sorted[3].Name);
    }
}
