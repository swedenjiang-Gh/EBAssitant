namespace EBAssistant;

public sealed class ToolPanelConfigurationIdentity
{
    public string Version { get; set; } = "";
    public string RootId { get; set; } = "";
    public string RootName { get; set; } = "";
}

public sealed class ToolPanelDirectoryNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Kind { get; set; } = "";
    public string TypeName { get; set; } = "";
    public List<ToolPanelDirectoryNode> Children { get; set; } = [];
}

public sealed class ToolPanelConfigurationTreeResult
{
    public ToolPanelConfigurationIdentity Identity { get; set; } = new();
    public List<ToolPanelDirectoryNode> Nodes { get; set; } = [];
}

public sealed class ToolPanelDirectoryRequest
{
    public string DirectoryId { get; set; } = "";
}

public sealed class AddGraphicTemplatesToToolPanelRequest
{
    public string TargetDirectoryId { get; set; } = "";
    public List<string> TemplateIds { get; set; } = [];
}

public sealed class ToolPanelAdditionRecord
{
    public string TemplateId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string TargetDirectoryId { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
    public string ConfirmedObjectId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class AddGraphicTemplatesToToolPanelResult
{
    public string Status { get; set; } = "";
    public string TargetDirectoryId { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
    public int TotalCount { get; set; }
    public int AddedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ToolPanelAdditionRecord> Records { get; set; } = [];
}
