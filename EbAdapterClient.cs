using System.Diagnostics;
using System.Text.Json;

namespace EBAssistant;

public sealed class EbAdapterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _adapterPath;

    public static string LastDiscoveryMessage { get; private set; } = string.Empty;

    private EbAdapterClient(string adapterPath, ConnectionInfo connection)
    {
        _adapterPath = adapterPath;
        Connection = connection;
    }

    public ConnectionInfo Connection { get; }

    public override string ToString() => $"{Connection.ApplicationName} ({Connection.Version})";

    public static async Task<List<EbAdapterClient>> FindActiveAsync()
    {
        var result = new List<EbAdapterClient>();
        var diagnostics = new List<string>();
        foreach (var version in new[] { "2023", "2024", "2025" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Adapters", version, $"EBAssistant.Adapter{version}.exe");
            if (!File.Exists(path))
            {
                diagnostics.Add($"EB {version} 适配器不存在：{path}");
                continue;
            }

            var response = await InvokeAsync<ConnectionInfo>(path, "GetConnectionInfo", null);
            if (response.Success && response.Data?.IsActive == true)
            {
                result.Add(new EbAdapterClient(path, response.Data));
            }
            else
            {
                diagnostics.Add($"EB {version}：{response.Message}");
            }
        }
        LastDiscoveryMessage = string.Join(Environment.NewLine, diagnostics);
        return result;
    }

    public Task<AdapterResponse<FolderTreeResult>> GetAttributeFolderTreeAsync() =>
        InvokeAsync<FolderTreeResult>(_adapterPath, "GetAttributeFolderTree", null);

    public Task<AdapterResponse<AttributeFolderIdentity>> GetAttributeFolderIdentityAsync() =>
        InvokeAsync<AttributeFolderIdentity>(_adapterPath, "GetAttributeFolderIdentity", null);

    public Task<AdapterResponse<CreateAttributesResult>> CreateAttributesAsync(CreateAttributesRequest request) =>
        InvokeAsync<CreateAttributesResult>(_adapterPath, "CreateAttributes", request);

    public Task<AdapterResponse<CreateFolderResult>> CreateAttributeFolderAsync(CreateFolderRequest request) =>
        InvokeAsync<CreateFolderResult>(_adapterPath, "CreateAttributeFolder", request);

    public Task<AdapterResponse<TypeDefinitionIdentity>> GetTypeDefinitionIdentityAsync() =>
        InvokeAsync<TypeDefinitionIdentity>(_adapterPath, "GetTypeDefinitionIdentity", null);

    public Task<AdapterResponse<TypeDefinitionTreeResult>> GetTypeDefinitionTreeAsync() =>
        InvokeAsync<TypeDefinitionTreeResult>(_adapterPath, "GetTypeDefinitionTree", null);

    public Task<AdapterResponse<ValidateAttributeIdsResult>> ValidateAttributeIdsAsync(IEnumerable<int> ids) =>
        InvokeAsync<ValidateAttributeIdsResult>(_adapterPath, "ValidateAttributeIds", new ValidateAttributeIdsRequest { AttributeIds = ids.Distinct().ToList() });

    public Task<AdapterResponse<ApplyTypeDefinitionDialogsResult>> ApplyTypeDefinitionDialogsAsync(ApplyTypeDefinitionDialogsRequest request) =>
        InvokeAsync<ApplyTypeDefinitionDialogsResult>(_adapterPath, "ApplyTypeDefinitionDialogs", request);

    public Task<AdapterResponse<ProjectTemplateIdentity>> GetProjectTemplateIdentityAsync() =>
        InvokeAsync<ProjectTemplateIdentity>(_adapterPath, "GetProjectTemplateIdentity", null);

    public Task<AdapterResponse<ProjectTemplateTreeResult>> GetProjectTemplateTreeAsync() =>
        InvokeAsync<ProjectTemplateTreeResult>(_adapterPath, "GetProjectTemplateTree", null);

    public Task<AdapterResponse<GraphicTemplateIdentity>> GetGraphicTemplateIdentityAsync() =>
        InvokeAsync<GraphicTemplateIdentity>(_adapterPath, "GetGraphicTemplateIdentity", null);

    public Task<AdapterResponse<GraphicTemplateTreeResult>> GetGraphicTemplateTreeAsync() =>
        InvokeAsync<GraphicTemplateTreeResult>(_adapterPath, "GetGraphicTemplateTree", null);

    public Task<AdapterResponse<GraphicTemplateDirectoryNode>> GetGraphicTemplateDirectoryAsync(string directoryId) =>
        InvokeAsync<GraphicTemplateDirectoryNode>(
            _adapterPath,
            "GetGraphicTemplateDirectory",
            new GraphicTemplateDirectoryRequest { DirectoryId = directoryId });

    public Task<AdapterResponse<MoveGraphicTemplatesResult>> MoveGraphicTemplatesAsync(MoveGraphicTemplatesRequest request) =>
        InvokeAsync<MoveGraphicTemplatesResult>(_adapterPath, "MoveGraphicTemplates", request);

    public Task<AdapterResponse<CreateGraphicTemplatesResult>> CreateGraphicTemplatesAsync(CreateGraphicTemplatesRequest request) =>
        InvokeAsync<CreateGraphicTemplatesResult>(_adapterPath, "CreateGraphicTemplates", request);

    public Task<AdapterResponse<PermissionConfigurationIdentity>> GetPermissionConfigurationIdentityAsync() =>
        InvokeAsync<PermissionConfigurationIdentity>(_adapterPath, "GetPermissionConfigurationIdentity", null);

    public Task<AdapterResponse<PermissionConfigurationStructureResult>> GetPermissionConfigurationStructureAsync() =>
        InvokeAsync<PermissionConfigurationStructureResult>(_adapterPath, "GetPermissionConfigurationStructure", null);

    public Task<AdapterResponse<PermissionMemberAssignmentResult>> AddPermissionMembersAsync(PermissionMemberAssignmentRequest request) =>
        InvokeAsync<PermissionMemberAssignmentResult>(_adapterPath, "AddPermissionMembers", request);

    public Task<AdapterResponse<ValidateWorksheetAttributeIdsResult>> ValidateWorksheetAttributeIdsAsync(IEnumerable<int> ids) =>
        InvokeAsync<ValidateWorksheetAttributeIdsResult>(
            _adapterPath,
            "ValidateWorksheetAttributeIds",
            new ValidateWorksheetAttributeIdsRequest { AttributeIds = ids.Distinct().ToList() });


    public Task<AdapterResponse<WorksheetCreationContextResult>> GetWorksheetCreationContextAsync(string templateProjectId) =>
        InvokeAsync<WorksheetCreationContextResult>(
            _adapterPath,
            "GetWorksheetCreationContext",
            new WorksheetCreationContextRequest { TemplateProjectId = templateProjectId });

    public Task<AdapterResponse<ValidateWorksheetCreationCapabilityResult>> ValidateWorksheetCreationCapabilityAsync(
        ValidateWorksheetCreationCapabilityRequest request) =>
        InvokeAsync<ValidateWorksheetCreationCapabilityResult>(
            _adapterPath,
            "ValidateWorksheetCreationCapability",
            request);

    public Task<AdapterResponse<CreateWorksheetsResult>> CreateWorksheetsAsync(CreateWorksheetsRequest request) =>
        InvokeAsync<CreateWorksheetsResult>(_adapterPath, "CreateWorksheets", request);

    private static async Task<AdapterResponse<T>> InvokeAsync<T>(string path, string operation, object? request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = operation,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new AdapterResponse<T> { Message = "无法启动 EB 适配器。" };
        }

        if (request is not null)
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request, JsonOptions));
        }
        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;

        if (string.IsNullOrWhiteSpace(output))
        {
            return new AdapterResponse<T> { Message = string.IsNullOrWhiteSpace(error) ? "EB 适配器没有返回结果。" : error.Trim() };
        }

        try
        {
            return JsonSerializer.Deserialize<AdapterResponse<T>>(output, JsonOptions)
                ?? new AdapterResponse<T> { Message = "无法解析 EB 适配器结果。" };
        }
        catch (JsonException ex)
        {
            return new AdapterResponse<T> { Message = $"EB 适配器结果格式错误：{ex.Message}" };
        }
    }
}
