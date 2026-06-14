namespace EBAssistant;

public sealed class GraphicTemplateIdentity
{
    public string Version { get; set; } = "";
    public string RootId { get; set; } = "";
    public string RootName { get; set; } = "";
}

public sealed class GraphicTemplateItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string ParentDirectoryId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string SymbolSyncDesignation { get; set; } = "";
    public string MasterUniRef { get; set; } = "";
}

public sealed class GraphicTemplateDirectoryNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string Kind { get; set; } = "";
    public string TypeName { get; set; } = "";
    public List<GraphicTemplateDirectoryNode> Children { get; set; } = [];
    public List<GraphicTemplateItem> Templates { get; set; } = [];
}

public sealed class GraphicTemplateTreeResult
{
    public GraphicTemplateIdentity Identity { get; set; } = new();
    public List<GraphicTemplateDirectoryNode> Nodes { get; set; } = [];
}

public sealed class GraphicTemplateDirectoryRequest
{
    public string DirectoryId { get; set; } = "";
}

public sealed class MoveGraphicTemplatesRequest
{
    public string TargetDirectoryId { get; set; } = "";
    public List<string> TemplateIds { get; set; } = [];
}

public sealed class GraphicTemplateMigrationRecord
{
    public string TemplateId { get; set; } = "";
    public string ConfirmedTemplateId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string SourceDirectoryId { get; set; } = "";
    public string SourceDirectoryPath { get; set; } = "";
    public string TargetDirectoryId { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class MoveGraphicTemplatesResult
{
    public string Status { get; set; } = "";
    public string TargetDirectoryId { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
    public int TotalCount { get; set; }
    public int MovedCount { get; set; }
    public int FailedCount { get; set; }
    public List<GraphicTemplateMigrationRecord> Records { get; set; } = [];
}
