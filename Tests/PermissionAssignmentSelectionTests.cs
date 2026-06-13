namespace EBAssistant.Tests;

internal static class PermissionAssignmentSelectionTests
{
    public static void Run()
    {
        ExcludesRootAndDeduplicatesById();
        ExcludesNonSelectableMemberNodes();
        ExcludesMembersAndDirectoriesWithoutIds();
        RequiresMembersAndDirectories();
    }

    private static void ExcludesRootAndDeduplicatesById()
    {
        var root = new PermissionDirectoryNode { Id = "ROOT", Name = "Users and groups" };
        var firstUser = new PermissionDirectoryNode { Id = "U1", Name = "User 1", FullPath = "Users and groups / User 1", IsSelectableMember = true };
        var duplicateUser = new PermissionDirectoryNode { Id = "u1", Name = "Duplicate User 1", IsSelectableMember = true };
        var secondUser = new PermissionDirectoryNode { Id = "U2", Name = "User 2", IsSelectableMember = true };
        var firstDirectory = new PermissionDirectoryNode { Id = "D1", Name = "Projects", FullPath = "Projects" };
        var duplicateDirectory = new PermissionDirectoryNode { Id = "d1", Name = "Duplicate Projects" };
        var secondDirectory = new PermissionDirectoryNode { Id = "D2", Name = "Attributes" };

        var selection = PermissionAssignmentSelection.Build(
            root.Id,
            [root, firstUser, duplicateUser, secondUser],
            [firstDirectory, duplicateDirectory, secondDirectory]);

        Assert.Equal(2, selection.Request.MemberIds.Count);
        Assert.Equal("U1", selection.Request.MemberIds[0]);
        Assert.Equal("U2", selection.Request.MemberIds[1]);
        Assert.Equal("User 1", selection.Members[0].Name);
        Assert.Equal(2, selection.Request.DirectoryIds.Count);
        Assert.Equal("D1", selection.Request.DirectoryIds[0]);
        Assert.Equal("D2", selection.Request.DirectoryIds[1]);
        Assert.Equal("Projects", selection.Directories[0].Name);
        Assert.Equal(4, selection.CombinationCount);
        Assert.True(selection.CanApply);
    }

    private static void ExcludesNonSelectableMemberNodes()
    {
        var selection = PermissionAssignmentSelection.Build(
            "ROOT",
            [
                new PermissionDirectoryNode { Id = "F1", Name = "Folder" },
                new PermissionDirectoryNode { Id = "G1", Name = "Group", IsSelectableMember = true }
            ],
            [new PermissionDirectoryNode { Id = "D1" }]);

        Assert.Equal(1, selection.Members.Count);
        Assert.Equal("G1", selection.Request.MemberIds[0]);
    }

    private static void ExcludesMembersAndDirectoriesWithoutIds()
    {
        var validUser = new PermissionDirectoryNode { Id = "U1", IsSelectableMember = true };
        var validDirectory = new PermissionDirectoryNode { Id = "D1" };

        var selection = PermissionAssignmentSelection.Build(
            "ROOT",
            [
                new PermissionDirectoryNode { Id = null!, IsSelectableMember = true },
                new PermissionDirectoryNode { Id = "", IsSelectableMember = true },
                new PermissionDirectoryNode { Id = " ", IsSelectableMember = true },
                validUser
            ],
            [
                new PermissionDirectoryNode { Id = null! },
                new PermissionDirectoryNode { Id = "" },
                new PermissionDirectoryNode { Id = "\t" },
                validDirectory
            ]);

        Assert.Equal(1, selection.Members.Count);
        Assert.Equal("U1", selection.Request.MemberIds[0]);
        Assert.Equal(1, selection.Directories.Count);
        Assert.Equal("D1", selection.Request.DirectoryIds[0]);
        Assert.Equal(1, selection.CombinationCount);
    }

    private static void RequiresMembersAndDirectories()
    {
        var root = new PermissionDirectoryNode { Id = "ROOT" };
        var user = new PermissionDirectoryNode { Id = "U1", IsSelectableMember = true };
        var directory = new PermissionDirectoryNode { Id = "D1" };

        Assert.Equal(false, PermissionAssignmentSelection.Build(root.Id, [root], [directory]).CanApply);
        Assert.Equal(false, PermissionAssignmentSelection.Build(root.Id, [user], []).CanApply);
    }
}
