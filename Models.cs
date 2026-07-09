using System.Text.Json.Serialization;

namespace EBAssistant;

public sealed class AdapterResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
}

public sealed class ConnectionInfo
{
    public string Version { get; set; } = "";
    public string ApplicationName { get; set; } = "";
    public string DatabaseServer { get; set; } = "";
    public string DatabaseInstance { get; set; } = "";
    public string Database { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class AttributeFolderNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public List<AttributeFolderNode> Children { get; set; } = [];
}

public sealed class AttributeFolderIdentity
{
    public string Version { get; set; } = "";
    public string RootId { get; set; } = "";
    public string RootName { get; set; } = "";
}

public sealed class ExistingAttribute
{
    public string Name { get; set; } = "";
}

public sealed class FolderTreeResult
{
    public List<AttributeFolderNode> Folders { get; set; } = [];
    public List<ExistingAttribute> ExistingAttributes { get; set; } = [];
}

public sealed class CreateAttributesRequest
{
    public string TargetFolderId { get; set; } = "";
    public List<CreateAttributeItem> Attributes { get; set; } = [];
}

public sealed class CreateAttributeItem
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Comment { get; set; } = "";
    public int Digits { get; set; }
}

public sealed class CreateAttributesResult
{
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string TargetFolder { get; set; } = "";
    public int CreatedCount { get; set; }
    public bool RolledBack { get; set; }
    public List<string> CreatedNames { get; set; } = [];
    public List<string> RollbackErrors { get; set; } = [];
    public List<CreateAttributeOperationRecord> Records { get; set; } = [];
}

public sealed class CreateAttributeOperationRecord
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Comment { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class CreateFolderRequest
{
    public string ParentFolderId { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class CreateFolderResult
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class AttributeTypeMapping
{
    public string UserType { get; set; } = "";
    public string EbType { get; set; } = "";
}

public sealed class ImportedAttributeRow
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string EbType { get; set; } = "";
    public string Comment { get; set; } = "";
    public string Validation { get; set; } = "";

    [JsonIgnore]
    public bool IsValid => Validation == "有效";
}

public sealed class TypeDefinitionIdentity
{
    public string Version { get; set; } = "";
    public string RootId { get; set; } = "";
    public string RootName { get; set; } = "";
}

public sealed class TypeDefinitionNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string NodeType { get; set; } = "";
    public bool IsActionable { get; set; }
    public List<TypeDefinitionNode> Children { get; set; } = [];
}

public sealed class TypeDefinitionTreeResult
{
    public TypeDefinitionIdentity Identity { get; set; } = new();
    public List<TypeDefinitionNode> Nodes { get; set; } = [];
}

public sealed class ValidateAttributeIdsRequest
{
    public List<int> AttributeIds { get; set; } = [];
}

public sealed class ValidateAttributeIdsResult
{
    public List<int> ExistingIds { get; set; } = [];
    public List<int> MissingIds { get; set; } = [];
}

public sealed class DialogDefinitionRow
{
    public int RowNumber { get; set; }
    public string TabName { get; set; } = "";
    public string AttributeIdText { get; set; } = "";
    public int AttributeId { get; set; }
    public string Validation { get; set; } = "";
    public bool IsDuplicate { get; set; }

    [JsonIgnore]
    public bool IsValid => Validation is "有效" or "重复行，已合并";
}

public sealed class DialogDefinitionItem
{
    public string TabName { get; set; } = "";
    public int AttributeId { get; set; }
}

public sealed class ApplyTypeDefinitionDialogsRequest
{
    public List<string> TypeItemIds { get; set; } = [];
    public List<DialogDefinitionItem> Definitions { get; set; } = [];
}

public sealed class TypeDefinitionOperationRecord
{
    public string TypeItemId { get; set; } = "";
    public string TypeItemName { get; set; } = "";
    public int AttributeId { get; set; }
    public string TabName { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class ApplyTypeDefinitionDialogsResult
{
    public string Status { get; set; } = "";
    public List<TypeDefinitionOperationRecord> Records { get; set; } = [];
    public List<string> UnprocessedTypeItemIds { get; set; } = [];
    public List<string> UnprocessedOperations { get; set; } = [];
}
public sealed class PermissionConfigurationIdentity
{
    public string Version { get; set; } = "";
    public string RootId { get; set; } = "";
    public string RootName { get; set; } = "";
    public string UsersAndGroupsId { get; set; } = "";
    public string UsersAndGroupsName { get; set; } = "";
    public string MessagesId { get; set; } = "";
}

public sealed class PermissionDirectoryNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsSelectableMember { get; set; }
    public List<PermissionDirectoryNode> Children { get; set; } = [];
}

public sealed class PermissionConfigurationStructureResult
{
    public PermissionConfigurationIdentity Identity { get; set; } = new();
    public List<PermissionDirectoryNode> LeftNodes { get; set; } = [];
    public List<PermissionDirectoryNode> RightNodes { get; set; } = [];
}

public sealed class PermissionMemberAssignmentRequest
{
    public List<string> MemberIds { get; set; } = [];
    public List<string> DirectoryIds { get; set; } = [];
}

public sealed class PermissionMemberAssignmentResult
{
    public string Status { get; set; } = "";
    public int TotalCount { get; set; }
    public int AddedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<PermissionMemberAssignmentRecord> Records { get; set; } = [];
}

public sealed class PermissionMemberAssignmentRecord
{
    public string MemberId { get; set; } = "";
    public string MemberName { get; set; } = "";
    public string DirectoryId { get; set; } = "";
    public string DirectoryName { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}
