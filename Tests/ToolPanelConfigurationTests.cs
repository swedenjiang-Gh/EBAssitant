namespace EBAssistant.Tests;

internal static class ToolPanelConfigurationTests
{
    public static void Run()
    {
        RecognizesToolPanelEntries();
        SeparatesCacheByVersionAndRootId();
        MergesSelectedDirectoryWithoutDroppingGrandchildren();
    }

    private static void RecognizesToolPanelEntries()
    {
        Assert.True(ToolPanelConfigurationSelection.IsToolPanelEntry(new ToolPanelDirectoryNode { Kind = "415" }));
        Assert.True(!ToolPanelConfigurationSelection.IsToolPanelEntry(new ToolPanelDirectoryNode { Kind = "414" }));
        Assert.True(!ToolPanelConfigurationSelection.IsToolPanelEntry(new ToolPanelDirectoryNode
        {
            Children = [new ToolPanelDirectoryNode()]
        }));
        Assert.True(!ToolPanelConfigurationSelection.IsToolPanelEntry(null));
    }

    private static void SeparatesCacheByVersionAndRootId()
    {
        var root = Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
        var identity = new ToolPanelConfigurationIdentity { Version = "2023", RootId = "A", RootName = "工具面板配置" };
        ToolPanelConfigurationCache.Save(new ToolPanelConfigurationTreeResult
        {
            Identity = identity,
            Nodes = [new ToolPanelDirectoryNode { Id = "P1", Name = "Panel" }]
        }, root);

        Assert.NotNull(ToolPanelConfigurationCache.Load(identity, root));
        Assert.Null(ToolPanelConfigurationCache.Load(
            new ToolPanelConfigurationIdentity { Version = "2024", RootId = "A" },
            root));
        Assert.Null(ToolPanelConfigurationCache.Load(
            new ToolPanelConfigurationIdentity { Version = "2023", RootId = "B" },
            root));
    }

    private static void MergesSelectedDirectoryWithoutDroppingGrandchildren()
    {
        var tree = new ToolPanelConfigurationTreeResult
        {
            Nodes =
            [
                new ToolPanelDirectoryNode
                {
                    Id = "P1",
                    Name = "面板",
                    Children =
                    [
                        new ToolPanelDirectoryNode
                        {
                            Id = "D1",
                            Name = "旧目录",
                            Children = [new ToolPanelDirectoryNode { Id = "L1", Name = "缓存末级" }]
                        }
                    ]
                }
            ]
        };
        var refreshed = new ToolPanelDirectoryNode
        {
            Id = "P1",
            Name = "面板",
            Children =
            [
                new ToolPanelDirectoryNode { Id = "D1", Name = "新目录名" },
                new ToolPanelDirectoryNode { Id = "D2", Name = "新增目录" }
            ]
        };

        Assert.True(ToolPanelConfigurationCache.MergeDirectoryShallow(tree, refreshed));
        Assert.Equal("新目录名", tree.Nodes[0].Children[0].Name);
        Assert.Equal("L1", tree.Nodes[0].Children[0].Children[0].Id);
        Assert.Equal("D2", tree.Nodes[0].Children[1].Id);
    }
}
