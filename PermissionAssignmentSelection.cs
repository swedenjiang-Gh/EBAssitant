namespace EBAssistant;

public sealed class PermissionAssignmentSelectionResult
{
    public PermissionMemberAssignmentRequest Request { get; set; } = new();
    public List<PermissionDirectoryNode> Members { get; set; } = [];
    public List<PermissionDirectoryNode> Directories { get; set; } = [];
    public int CombinationCount => Members.Count * Directories.Count;
    public bool CanApply => Members.Count > 0 && Directories.Count > 0;
}

public static class PermissionAssignmentSelection
{
    public static PermissionAssignmentSelectionResult Build(
        string usersAndGroupsRootId,
        IEnumerable<PermissionDirectoryNode> selectedMembers,
        IEnumerable<PermissionDirectoryNode> selectedDirectories)
    {
        var members = DistinctById(
            selectedMembers.Where(node =>
                !string.Equals(node.Id, usersAndGroupsRootId, StringComparison.OrdinalIgnoreCase)));
        var directories = DistinctById(selectedDirectories);

        return new PermissionAssignmentSelectionResult
        {
            Request = new PermissionMemberAssignmentRequest
            {
                MemberIds = members.Select(node => node.Id).ToList(),
                DirectoryIds = directories.Select(node => node.Id).ToList()
            },
            Members = members,
            Directories = directories
        };
    }

    private static List<PermissionDirectoryNode> DistinctById(IEnumerable<PermissionDirectoryNode> nodes)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PermissionDirectoryNode>();

        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Id) && ids.Add(node.Id))
            {
                result.Add(node);
            }
        }

        return result;
    }
}

public static class PermissionAssignmentResultSummary
{
    public static void Apply(PermissionMemberAssignmentResult result)
    {
        result.TotalCount = result.Records.Count;
        result.AddedCount = result.Records.Count(record => record.Status == "added");
        result.SkippedCount = result.Records.Count(record => record.Status == "skipped_existing");
        result.FailedCount = result.TotalCount - result.AddedCount - result.SkippedCount;
        result.Status = result.FailedCount == 0 ? "completed" : "completed_with_failures";
    }

    public static PermissionMemberAssignmentResult FromFailure(
        PermissionAssignmentSelectionResult selection,
        string message)
    {
        var result = new PermissionMemberAssignmentResult();

        foreach (var member in selection.Members)
        {
            foreach (var directory in selection.Directories)
            {
                result.Records.Add(new PermissionMemberAssignmentRecord
                {
                    MemberId = member.Id,
                    MemberName = member.Name,
                    DirectoryId = directory.Id,
                    DirectoryName = directory.Name,
                    Status = "failed",
                    Message = message
                });
            }
        }

        Apply(result);
        return result;
    }
}
