namespace EBAssistant;

public static class ToolPanelConfigurationSelection
{
    public static bool IsToolPanelEntry(ToolPanelDirectoryNode? node) =>
        node is not null && string.Equals(node.Kind, "415", StringComparison.OrdinalIgnoreCase);
}
