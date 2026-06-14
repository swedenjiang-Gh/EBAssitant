namespace EBAssistant.Tests;

internal static class GraphicTemplateSelectionTests
{
    public static void Run()
    {
        ReturnsOnlyLeafDirectories();
        FindsDirectoryById();
    }

    private static void ReturnsOnlyLeafDirectories()
    {
        var tree = new GraphicTemplateTreeResult
        {
            Nodes =
            [
                new GraphicTemplateDirectoryNode
                {
                    Id = "A",
                    Name = "器件",
                    Children =
                    [
                        new GraphicTemplateDirectoryNode { Id = "B", Name = "常规" },
                        new GraphicTemplateDirectoryNode
                        {
                            Id = "C",
                            Name = "管道",
                            Children =
                            [
                                new GraphicTemplateDirectoryNode { Id = "D", Name = "三通" }
                            ]
                        }
                    ]
                }
            ]
        };

        var leaves = GraphicTemplateSelection.GetLeafDirectories(tree).Select(node => node.Id).ToList();

        Assert.Equal(2, leaves.Count);
        Assert.Equal("B", leaves[0]);
        Assert.Equal("D", leaves[1]);
    }

    private static void FindsDirectoryById()
    {
        var tree = new GraphicTemplateTreeResult
        {
            Nodes =
            [
                new GraphicTemplateDirectoryNode
                {
                    Id = "A",
                    Children =
                    [
                        new GraphicTemplateDirectoryNode { Id = "B" }
                    ]
                }
            ]
        };

        Assert.NotNull(GraphicTemplateSelection.FindDirectory(tree, "B"));
        Assert.Null(GraphicTemplateSelection.FindDirectory(tree, "missing"));
    }
}
