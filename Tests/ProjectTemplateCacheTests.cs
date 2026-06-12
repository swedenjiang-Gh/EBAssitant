namespace EBAssistant.Tests;

internal static class ProjectTemplateCacheTests
{
    public static void Run()
    {
        SeparatesCacheByVersionAndRootId();
        ReturnsNullWhenProtocolVersionDoesNotMatch();
        DoesNotCollapseDifferentRootIdsWhenSanitizingFileName();
    }

    private static void SeparatesCacheByVersionAndRootId()
    {
        var root = NewRootDirectory();
        var identityA = new ProjectTemplateIdentity { Version = "2023", RootId = "A", RootName = "Root" };
        var identityB = new ProjectTemplateIdentity { Version = "2024", RootId = "A", RootName = "Root" };
        var identityC = new ProjectTemplateIdentity { Version = "2023", RootId = "B", RootName = "Root" };
        var tree = new ProjectTemplateTreeResult
        {
            Identity = identityA,
            Nodes =
            [
                new ProjectTemplateNode { Id = "P1", Name = "Template", FullPath = "Root / Template", IsTemplateProject = true }
            ]
        };

        ProjectTemplateCache.Save(tree, root);

        var loadedA = ProjectTemplateCache.Load(identityA, root);
        var loadedB = ProjectTemplateCache.Load(identityB, root);
        var loadedC = ProjectTemplateCache.Load(identityC, root);

        Assert.NotNull(loadedA);
        Assert.Equal("2023", loadedA!.Identity.Version);
        Assert.Equal(1, loadedA.Nodes.Count);
        Assert.Equal("P1", loadedA.Nodes[0].Id);
        Assert.Null(loadedB);
        Assert.Null(loadedC);
    }

    private static void ReturnsNullWhenProtocolVersionDoesNotMatch()
    {
        var root = NewRootDirectory();
        var identity = new ProjectTemplateIdentity { Version = "2023", RootId = "A", RootName = "Root" };
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "2023_A.json"),
            """
            {
              "ProtocolVersion": 0,
              "Tree": {
                "Identity": { "Version": "2023", "RootId": "A", "RootName": "Root" },
                "Nodes": []
              }
            }
            """);

        Assert.Null(ProjectTemplateCache.Load(identity, root));
    }

    private static void DoesNotCollapseDifferentRootIdsWhenSanitizingFileName()
    {
        var root = NewRootDirectory();
        var dashed = new ProjectTemplateIdentity { Version = "2023", RootId = "A-B", RootName = "Root" };
        var plain = new ProjectTemplateIdentity { Version = "2023", RootId = "AB", RootName = "Root" };

        ProjectTemplateCache.Save(new ProjectTemplateTreeResult { Identity = dashed }, root);

        Assert.NotNull(ProjectTemplateCache.Load(dashed, root));
        Assert.Null(ProjectTemplateCache.Load(plain, root));
    }

    private static string NewRootDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
    }
}
