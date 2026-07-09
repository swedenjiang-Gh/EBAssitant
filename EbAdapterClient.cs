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
    public static string LastDiscoveryLogPath { get; private set; } = string.Empty;

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
        LastDiscoveryLogPath = string.Empty;
        diagnostics.Add($"EBAssistant 版本：{AppDisplay.MainTitle}");
        diagnostics.Add($"运行目录：{AppContext.BaseDirectory}");
        foreach (var version in new[] { "2023", "2024", "2025" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Adapters", version, $"EBAssistant.Adapter{version}.exe");
            diagnostics.Add($"EB {version} 适配器路径：{path}");
            if (!File.Exists(path))
            {
                diagnostics.Add($"EB {version} 适配器不存在：{path}");
                continue;
            }

            AdapterResponse<ConnectionInfo> response;
            try
            {
                response = await InvokeAsync<ConnectionInfo>(path, "GetConnectionInfo", null);
            }
            catch (Exception ex)
            {
                diagnostics.Add($"EB {version} 适配器调用异常：{ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if (response.Success && response.Data?.IsActive == true)
            {
                result.Add(new EbAdapterClient(path, response.Data));
            }
            else
            {
                diagnostics.Add($"EB {version}：{response.Message}");
            }
        }
        if (result.Count == 0)
        {
            try
            {
                LastDiscoveryLogPath = AdapterDiscoveryLogWriter.Write(diagnostics);
                diagnostics.Add($"诊断日志：{LastDiscoveryLogPath}");
            }
            catch (Exception ex)
            {
                diagnostics.Add($"诊断日志写入失败：{ex.GetType().Name}: {ex.Message}");
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

    public Task<AdapterResponse<CreateNamedGraphicTemplatesResult>> CreateNamedGraphicTemplatesAsync(CreateNamedGraphicTemplatesRequest request) =>
        InvokeAsync<CreateNamedGraphicTemplatesResult>(_adapterPath, "CreateNamedGraphicTemplates", request);

    public Task<AdapterResponse<OpenGraphicTemplateWithVisioResult>> OpenGraphicTemplateWithVisioAsync(OpenGraphicTemplateWithVisioRequest request) =>
        InvokeAsync<OpenGraphicTemplateWithVisioResult>(_adapterPath, "OpenGraphicTemplateWithVisio", request);

    public Task<AdapterResponse<ToolPanelConfigurationIdentity>> GetToolPanelConfigurationIdentityAsync() =>
        InvokeAsync<ToolPanelConfigurationIdentity>(_adapterPath, "GetToolPanelConfigurationIdentity", null);

    public Task<AdapterResponse<ToolPanelConfigurationTreeResult>> GetToolPanelConfigurationTreeAsync() =>
        InvokeAsync<ToolPanelConfigurationTreeResult>(_adapterPath, "GetToolPanelConfigurationTree", null);

    public Task<AdapterResponse<ToolPanelDirectoryNode>> GetToolPanelConfigurationDirectoryAsync(string directoryId) =>
        InvokeAsync<ToolPanelDirectoryNode>(
            _adapterPath,
            "GetToolPanelConfigurationDirectory",
            new ToolPanelDirectoryRequest { DirectoryId = directoryId });

    public Task<AdapterResponse<AddGraphicTemplatesToToolPanelResult>> AddGraphicTemplatesToToolPanelAsync(AddGraphicTemplatesToToolPanelRequest request) =>
        InvokeAsync<AddGraphicTemplatesToToolPanelResult>(_adapterPath, "AddGraphicTemplatesToToolPanel", request);

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
        const int adapterTimeoutMilliseconds = 30000;
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

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            return new AdapterResponse<T> { Message = $"无法启动 EB 适配器：{path}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}" };
        }

        using (process)
        {
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
        var waitTask = process.WaitForExitAsync();
        if (await Task.WhenAny(waitTask, Task.Delay(adapterTimeoutMilliseconds)) != waitTask)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new AdapterResponse<T>
            {
                Message = $"EB 适配器执行超时：{Path.GetFileName(path)} {operation} 超过 {adapterTimeoutMilliseconds / 1000} 秒未返回。请确认 EB 与 EBAssistant 使用同一用户和同一权限级别运行。"
            };
        }

        await waitTask;
        var output = await outputTask;
        var error = await errorTask;

        if (string.IsNullOrWhiteSpace(output))
        {
            var exit = process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new AdapterResponse<T> { Message = string.IsNullOrWhiteSpace(error) ? $"EB 适配器没有返回结果。ExitCode={exit}" : $"EB 适配器错误。ExitCode={exit}{Environment.NewLine}{error.Trim()}" };
        }

        try
        {
            return JsonSerializer.Deserialize<AdapterResponse<T>>(output, JsonOptions)
                ?? new AdapterResponse<T> { Message = "无法解析 EB 适配器结果。" };
        }
        catch (JsonException ex)
        {
            return new AdapterResponse<T> { Message = $"EB 适配器结果格式错误：{ex.Message}{Environment.NewLine}输出片段：{TakeSnippet(output)}{Environment.NewLine}错误输出：{TakeSnippet(error)}" };
        }
        }
    }

    private static string TakeSnippet(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = value.Trim();
        return trimmed.Length <= 800 ? trimmed : trimmed[..800] + "...";
    }
}
