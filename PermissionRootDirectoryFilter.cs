namespace EBAssistant;

public static class PermissionRootDirectoryFilter
{
    public static List<PermissionDirectoryNode> Filter(
        List<PermissionDirectoryNode> rootDirectories,
        string messagesId,
        string usersAndGroupsId)
    {
        return rootDirectories.Where(d =>
            d.Id != messagesId &&
            d.Id != usersAndGroupsId).ToList();
    }
}
