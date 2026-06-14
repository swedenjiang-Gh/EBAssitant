namespace EBAssistant.Tests;

internal static class GraphicTemplateCacheTests
{
    public static void Run()
    {
        SeparatesCacheByVersionAndRootId();
        ReturnsNullWhenProtocolVersionDoesNotMatch();
        ReplacesOnlyTheRefreshedDirectory();
        MergesShallowRefreshWithoutDroppingCachedGrandchildren();
    }

    private static void SeparatesCacheByVersionAndRootId()
    {
        var root = NewRootDirectory();
        var identityA = new GraphicTemplateIdentity { Version = "2023", RootId = "A", RootName = "图形模板" };
        var identityB = new GraphicTemplateIdentity { Version = "2024", RootId = "A", RootName = "图形模板" };
        var identityC = new GraphicTemplateIdentity { Version = "2023", RootId = "B", RootName = "图形模板" };

        GraphicTemplateCache.Save(new GraphicTemplateTreeResult
        {
            Identity = identityA,
            Nodes =
            [
                new GraphicTemplateDirectoryNode { Id = "D1", Name = "器件", FullPath = "图形模板 / 器件" }
            ]
        }, root);

        Assert.NotNull(GraphicTemplateCache.Load(identityA, root));
        Assert.Null(GraphicTemplateCache.Load(identityB, root));
        Assert.Null(GraphicTemplateCache.Load(identityC, root));
    }

    private static void ReturnsNullWhenProtocolVersionDoesNotMatch()
    {
        var root = NewRootDirectory();
        var identity = new GraphicTemplateIdentity { Version = "2023", RootId = "A", RootName = "图形模板" };
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "2023_A.json"),
            """
            {
              "ProtocolVersion": 0,
              "Tree": {
                "Identity": { "Version": "2023", "RootId": "A", "RootName": "图形模板" },
                "Nodes": []
              }
            }
            """);

        Assert.Null(GraphicTemplateCache.Load(identity, root));
    }

    private static void ReplacesOnlyTheRefreshedDirectory()
    {
        var root = NewRootDirectory();
        var identity = new GraphicTemplateIdentity { Version = "2023", RootId = "A", RootName = "图形模板" };
        var tree = new GraphicTemplateTreeResult
        {
            Identity = identity,
            Nodes =
            [
                new GraphicTemplateDirectoryNode
                {
                    Id = "D1",
                    Name = "器件",
                    FullPath = "图形模板 / 器件",
                    Children =
                    [
                        new GraphicTemplateDirectoryNode { Id = "D1A", Name = "旧目录", FullPath = "图形模板 / 器件 / 旧目录" }
                    ],
                    Templates =
                    [
                        new GraphicTemplateItem { Id = "M1", Name = "旧模板", ParentDirectoryId = "D1" }
                    ]
                },
                new GraphicTemplateDirectoryNode { Id = "D2", Name = "管道", FullPath = "图形模板 / 管道" }
            ]
        };
        var refreshed = new GraphicTemplateDirectoryNode
        {
            Id = "D1",
            Name = "器件",
            FullPath = "图形模板 / 器件",
            Children =
            [
                new GraphicTemplateDirectoryNode { Id = "D1B", Name = "新目录", FullPath = "图形模板 / 器件 / 新目录" }
            ],
            Templates =
            [
                new GraphicTemplateItem { Id = "M2", Name = "新模板", ParentDirectoryId = "D1" }
            ]
        };

        Assert.True(GraphicTemplateCache.ReplaceDirectory(tree, refreshed));

        Assert.Equal(2, tree.Nodes.Count);
        Assert.Equal("D1B", tree.Nodes[0].Children[0].Id);
        Assert.Equal("M2", tree.Nodes[0].Templates[0].Id);
        Assert.Equal("D2", tree.Nodes[1].Id);
    }

    private static void MergesShallowRefreshWithoutDroppingCachedGrandchildren()
    {
        var tree = new GraphicTemplateTreeResult
        {
            Nodes =
            [
                new GraphicTemplateDirectoryNode
                {
                    Id = "D1",
                    Name = "器件",
                    FullPath = "图形模板 / 器件",
                    Children =
                    [
                        new GraphicTemplateDirectoryNode
                        {
                            Id = "D1A",
                            Name = "常规",
                            FullPath = "图形模板 / 器件 / 常规",
                            Children =
                            [
                                new GraphicTemplateDirectoryNode { Id = "D1A1", Name = "缓存下级", FullPath = "图形模板 / 器件 / 常规 / 缓存下级" }
                            ]
                        }
                    ],
                    Templates =
                    [
                        new GraphicTemplateItem { Id = "M1", Name = "旧模板", ParentDirectoryId = "D1" }
                    ]
                }
            ]
        };
        var refreshed = new GraphicTemplateDirectoryNode
        {
            Id = "D1",
            Name = "器件",
            FullPath = "图形模板 / 器件",
            Children =
            [
                new GraphicTemplateDirectoryNode { Id = "D1A", Name = "常规", FullPath = "图形模板 / 器件 / 常规" },
                new GraphicTemplateDirectoryNode { Id = "D1B", Name = "新增", FullPath = "图形模板 / 器件 / 新增" }
            ],
            Templates =
            [
                new GraphicTemplateItem { Id = "M2", Name = "新模板", ParentDirectoryId = "D1" }
            ]
        };

        Assert.True(GraphicTemplateCache.MergeDirectoryShallow(tree, refreshed));

        Assert.Equal("M2", tree.Nodes[0].Templates[0].Id);
        Assert.Equal(2, tree.Nodes[0].Children.Count);
        Assert.Equal("D1A1", tree.Nodes[0].Children[0].Children[0].Id);
        Assert.Equal("D1B", tree.Nodes[0].Children[1].Id);
    }

    private static string NewRootDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
    }
}
