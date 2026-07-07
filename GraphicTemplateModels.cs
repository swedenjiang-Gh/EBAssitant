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

public sealed class GraphicTemplateBatchRawRow
{
    public int ExcelRowNumber { get; set; }
    public string Sequence { get; set; } = "";
    public string SourceDirectoryPath { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
}

public sealed class GraphicTemplateBatchPreviewRow
{
    public int ExcelRowNumber { get; set; }
    public string Sequence { get; set; } = "";
    public string SourceDirectoryPath { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
    public int SourceTemplateMatchCount { get; set; }
    public string Validation { get; set; } = "";
    public bool IsValid => Validation == "有效";
}

public sealed class CreateGraphicTemplatesRequest
{
    public string DirectoryId { get; set; } = "";
    public string SourceTemplateId { get; set; } = "";
    public int RequestedTotalCount { get; set; }
}

public sealed class GraphicTemplateCreationRecord
{
    public int CopyNumber { get; set; }
    public string ConfirmedTemplateId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class CreateGraphicTemplatesResult
{
    public string Status { get; set; } = "";
    public string DirectoryId { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public string SourceTemplateId { get; set; } = "";
    public string SourceTemplateName { get; set; } = "";
    public int RequestedTotalCount { get; set; }
    public int OriginalCount { get; set; }
    public int CreatedCount { get; set; }
    public int ConfirmedFinalCount { get; set; }
    public List<GraphicTemplateCreationRecord> Records { get; set; } = [];
}

public sealed class CreateNamedGraphicTemplatesRequest
{
    public string DirectoryId { get; set; } = "";
    public string SourceTemplateId { get; set; } = "";
    public List<string> TargetNames { get; set; } = [];
}

public sealed class NamedGraphicTemplateCreationRecord
{
    public string RequestedName { get; set; } = "";
    public string ConfirmedTemplateId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class CreateNamedGraphicTemplatesResult
{
    public string Status { get; set; } = "";
    public string DirectoryId { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public string SourceTemplateId { get; set; } = "";
    public string SourceTemplateName { get; set; } = "";
    public List<NamedGraphicTemplateCreationRecord> Records { get; set; } = [];
}

public sealed class OpenGraphicTemplateWithVisioRequest
{
    public string TemplateId { get; set; } = "";
}

public sealed class OpenGraphicTemplateWithVisioResult
{
    public string TemplateId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string OpenMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class GraphicTemplateVisioBatchRecord
{
    public string TemplateName { get; set; } = "";
    public string TemplateId { get; set; } = "";
    public string SourceShapeName { get; set; } = "";
    public int SourceShapeIndex { get; set; }
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class GraphicTemplateVisioBatchResult
{
    public string SourceVisioPath { get; set; } = "";
    public string TargetDirectoryPath { get; set; } = "";
    public int RequestedCount { get; set; }
    public string BaseTemplateId { get; set; } = "";
    public string BaseTemplateName { get; set; } = "";
    public string Status { get; set; } = "";
    public List<GraphicTemplateVisioBatchRecord> Records { get; set; } = [];
}
