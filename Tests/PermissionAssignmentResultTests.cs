namespace EBAssistant.Tests;

internal static class PermissionAssignmentResultTests
{
    public static void Run()
    {
        AggregatesStatuses();
        CompletesWithoutFailures();
        TreatsUnknownStatusesAsFailures();
        BuildsFailureForEveryCombination();
    }

    private static void AggregatesStatuses()
    {
        var result = new PermissionMemberAssignmentResult
        {
            Records =
            [
                new() { Status = "added" },
                new() { Status = "skipped_existing" },
                new() { Status = "failed" }
            ]
        };

        PermissionAssignmentResultSummary.Apply(result);

        Assert.Equal("completed_with_failures", result.Status);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(3, result.TotalCount);
    }

    private static void CompletesWithoutFailures()
    {
        var result = new PermissionMemberAssignmentResult
        {
            Records =
            [
                new() { Status = "added" },
                new() { Status = "skipped_existing" }
            ]
        };

        PermissionAssignmentResultSummary.Apply(result);

        Assert.Equal("completed", result.Status);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, result.TotalCount);
    }

    private static void TreatsUnknownStatusesAsFailures()
    {
        var result = new PermissionMemberAssignmentResult
        {
            Records =
            [
                new() { Status = "added" },
                new() { Status = "unexpected_status" }
            ]
        };

        PermissionAssignmentResultSummary.Apply(result);

        Assert.Equal("completed_with_failures", result.Status);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal("unexpected_status", result.Records[1].Status);
    }

    private static void BuildsFailureForEveryCombination()
    {
        var selection = PermissionAssignmentSelection.Build(
            "ROOT",
            [
                new PermissionDirectoryNode { Id = "U1", Name = "User 1" },
                new PermissionDirectoryNode { Id = "G1", Name = "Group 1" }
            ],
            [
                new PermissionDirectoryNode { Id = "D1", Name = "Projects" },
                new PermissionDirectoryNode { Id = "D2", Name = "Attributes" }
            ]);

        var result = PermissionAssignmentResultSummary.FromFailure(selection, "Adapter failed.");

        Assert.Equal("completed_with_failures", result.Status);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal(4, result.FailedCount);
        Assert.Equal("U1", result.Records[0].MemberId);
        Assert.Equal("D1", result.Records[0].DirectoryId);
        Assert.Equal("User 1", result.Records[0].MemberName);
        Assert.Equal("Projects", result.Records[0].DirectoryName);
        Assert.Equal("failed", result.Records[0].Status);
        Assert.Equal("Adapter failed.", result.Records[0].Message);
        Assert.Equal("G1", result.Records[3].MemberId);
        Assert.Equal("D2", result.Records[3].DirectoryId);
    }
}
