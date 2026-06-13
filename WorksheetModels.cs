namespace EBAssistant;

public sealed class ProjectTemplateIdentity
{
    public string Version { get; set; } = "";
    public string RootId { get; set; } = "";
    public string RootName { get; set; } = "";
}

public sealed class ProjectTemplateNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsTemplateProject { get; set; }
    public List<ProjectTemplateNode> Children { get; set; } = [];
}

public sealed class ProjectTemplateTreeResult
{
    public ProjectTemplateIdentity Identity { get; set; } = new();
    public List<ProjectTemplateNode> Nodes { get; set; } = [];
}

public sealed class WorksheetRawColumn
{
    public int ExcelColumnNumber { get; set; }
    public string Label { get; set; } = "";
    public string AttributeIdText { get; set; } = "";
}

public sealed class WorksheetRawSheet
{
    public int SheetIndex { get; set; }
    public string SheetName { get; set; } = "";
    public List<WorksheetRawColumn> Columns { get; set; } = [];
}

public sealed class WorksheetColumnDefinition
{
    public int ExcelColumnNumber { get; set; }
    public int Position { get; set; }
    public string Label { get; set; } = "";
    public int AttributeId { get; set; }
    public int Width { get; set; }
    public string Validation { get; set; } = "";
}

public sealed class WorksheetImportSheet
{
    public int SheetIndex { get; set; }
    public string OriginalName { get; set; } = "";
    public string FinalName { get; set; } = "";
    public string Validation { get; set; } = "";
    public List<WorksheetColumnDefinition> Columns { get; set; } = [];
    public bool IsValid => Validation == "有效";
}

public sealed class ValidateWorksheetAttributeIdsRequest
{
    public List<int> AttributeIds { get; set; } = [];
}

public sealed class ValidateWorksheetAttributeIdsResult
{
    public List<int> ExistingIds { get; set; } = [];
    public List<int> MissingIds { get; set; } = [];
}

public sealed class WorksheetCreationContextRequest
{
    public string TemplateProjectId { get; set; } = "";
}

public sealed class WorksheetCreationContextResult
{
    public string TemplateProjectPath { get; set; } = "";
    public string TargetFolderPath { get; set; } = "";
    public List<string> ExistingWorksheetNames { get; set; } = [];
}

public sealed class ValidateWorksheetCreationCapabilityRequest
{
    public string TemplateProjectId { get; set; } = "";
    public List<int> AttributeIds { get; set; } = [];
}

public sealed class ValidateWorksheetCreationCapabilityResult
{
    public bool Passed { get; set; }
    public string TemporaryWorksheetName { get; set; } = "";
    public string TargetFolderPath { get; set; } = "";
    public List<string> Checks { get; set; } = [];
    public List<string> CleanupChecks { get; set; } = [];
}

public sealed class CreateWorksheetsRequest
{
    public string TemplateProjectId { get; set; } = "";
    public List<CreateWorksheetItem> Worksheets { get; set; } = [];
}

public sealed class CreateWorksheetItem
{
    public int SheetIndex { get; set; }
    public string OriginalName { get; set; } = "";
    public string RequestedName { get; set; } = "";
    public List<CreateWorksheetColumnItem> Columns { get; set; } = [];
}

public sealed class CreateWorksheetColumnItem
{
    public int Position { get; set; }
    public string Label { get; set; } = "";
    public int AttributeId { get; set; }
    public int Width { get; set; }
}

public sealed class WorksheetOperationRecord
{
    public int SheetIndex { get; set; }
    public string OriginalName { get; set; } = "";
    public string FinalName { get; set; } = "";
    public string SavePath { get; set; } = "";
    public string ObjectType { get; set; } = "器件";
    public int ColumnCount { get; set; }
    public string ColumnSummary { get; set; } = "";
    public string LabelStatus { get; set; } = "";
    public string AutoWidthStatus { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class CreateWorksheetsResult
{
    public string Status { get; set; } = "";
    public List<WorksheetOperationRecord> Records { get; set; } = [];
}
