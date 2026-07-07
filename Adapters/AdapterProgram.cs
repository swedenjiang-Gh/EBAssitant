using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Aucotec;
using EbApplication = Aucotec.Application;

namespace EBAssistant.Adapter
{
    internal static class AdapterProgram
    {
#if EB30
        private const string Version = "2023";
        private const string ProgId = "EngineeringBase.Application.30";
        private const string ExpectedInstallFolder = "Engineering Base 720";
#elif EB31
        private const string Version = "2024";
        private const string ProgId = "EngineeringBase.Application.31";
        private const string ExpectedInstallFolder = "Engineering Base 730";
#endif

        private static int Main(string[] args)
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            try
            {
                var operation = args.Length == 0 ? "" : args[0];
                if (operation == "GetConnectionInfo") return Write(GetConnectionInfo());
                var app = GetActiveApplication();
                if (app == null) return Write(Fail<object>("未检测到活动的 EB " + Version + "。"));
                if (operation == "GetAttributeFolderTree") return Write(GetAttributeFolderTree(app));
                if (operation == "GetAttributeFolderIdentity") return Write(GetAttributeFolderIdentity(app));
                if (operation == "CreateAttributes") return Write(CreateAttributes(app, Read<CreateAttributesRequest>()));
                if (operation == "CreateAttributeFolder") return Write(CreateAttributeFolder(app, Read<CreateFolderRequest>()));
                if (operation == "DeleteEmptyAttributeFolder") return Write(DeleteEmptyAttributeFolder(app, Read<DeleteFolderRequest>()));
                if (operation == "GetTypeDefinitionIdentity") return Write(GetTypeDefinitionIdentity(app));
                if (operation == "GetTypeDefinitionTree") return Write(GetTypeDefinitionTree(app));
                if (operation == "ValidateAttributeIds") return Write(ValidateAttributeIds(app, Read<ValidateAttributeIdsRequest>()));
                if (operation == "GetProjectTemplateIdentity") return Write(GetProjectTemplateIdentity(app));
                if (operation == "GetProjectTemplateTree") return Write(GetProjectTemplateTree(app));
                if (operation == "GetGraphicTemplateIdentity") return Write(GetGraphicTemplateIdentity(app));
                if (operation == "GetGraphicTemplateTree") return Write(GetGraphicTemplateTree(app));
                if (operation == "GetGraphicTemplateDirectory") return Write(GetGraphicTemplateDirectory(app, Read<GraphicTemplateDirectoryRequest>()));
                if (operation == "MoveGraphicTemplates") return Write(MoveGraphicTemplates(app, Read<MoveGraphicTemplatesRequest>()));
                if (operation == "CreateGraphicTemplates") return Write(CreateGraphicTemplates(app, Read<CreateGraphicTemplatesRequest>()));
                if (operation == "CreateNamedGraphicTemplates") return Write(CreateNamedGraphicTemplates(app, Read<CreateNamedGraphicTemplatesRequest>()));
                if (operation == "OpenGraphicTemplateWithVisio") return Write(OpenGraphicTemplateWithVisio(app, Read<OpenGraphicTemplateWithVisioRequest>()));
                if (operation == "GetToolPanelConfigurationIdentity") return Write(GetToolPanelConfigurationIdentity(app));
                if (operation == "GetToolPanelConfigurationTree") return Write(GetToolPanelConfigurationTree(app));
                if (operation == "GetToolPanelConfigurationDirectory") return Write(GetToolPanelConfigurationDirectory(app, Read<ToolPanelDirectoryRequest>()));
                if (operation == "AddGraphicTemplatesToToolPanel") return Write(AddGraphicTemplatesToToolPanel(app, Read<AddGraphicTemplatesToToolPanelRequest>()));
                if (operation == "GetPermissionConfigurationIdentity") return Write(GetPermissionConfigurationIdentity(app));
                if (operation == "GetPermissionConfigurationStructure") return Write(GetPermissionConfigurationStructure(app));
                if (operation == "AddPermissionMembers") return Write(AddPermissionMembers(app, Read<PermissionMemberAssignmentRequest>()));
                if (operation == "ValidateWorksheetAttributeIds") return Write(ValidateWorksheetAttributeIds(app, Read<ValidateWorksheetAttributeIdsRequest>()));
                if (operation == "GetWorksheetCreationContext") return Write(GetWorksheetCreationContext(app, Read<WorksheetCreationContextRequest>()));
                if (operation == "ValidateWorksheetCreationCapability") return Write(ValidateWorksheetCreationCapability(app, Read<ValidateWorksheetCreationCapabilityRequest>()));
                if (operation == "CreateWorksheets") return Write(CreateWorksheets(app, Read<CreateWorksheetsRequest>()));
                if (operation == "ApplyTypeDefinitionDialogs") return Write(ApplyTypeDefinitionDialogs(app, Read<ApplyTypeDefinitionDialogsRequest>()));
                return Write(Fail<object>("未知操作：" + operation));
            }
            catch (Exception ex)
            {
                return Write(Fail<object>(Describe(ex)));
            }
        }

        private static AdapterResponse<ConnectionInfo> GetConnectionInfo()
        {
            var diagnostics = new List<string>();
            var app = GetActiveApplication(diagnostics);
            return Ok(new ConnectionInfo
            {
                Version = Version,
                IsActive = app != null,
                ApplicationName = app == null ? "EB " + Version : Safe(delegate { return app.Name; }, "EB " + Version),
                DatabaseServer = app == null ? "" : Safe(delegate { return app.DatabaseServer; }, ""),
                DatabaseInstance = app == null ? "" : Safe(delegate { return app.DatabaseInstance; }, ""),
                Database = app == null ? "" : Safe(delegate { return app.Database; }, "")
            }, app == null ? BuildConnectionFailureMessage(diagnostics) : "已连接");
        }

        private static EbApplication GetActiveApplication()
        {
            return GetActiveApplication(null);
        }

        private static EbApplication GetActiveApplication(List<string> diagnostics)
        {
            foreach (var progId in new[] { ProgId, "EngineeringBase.Application", "Aucotec.Application" })
            {
                try
                {
                    var app = (EbApplication)Marshal.GetActiveObject(progId);
                    if (app != null)
                    {
                        AddDiagnostic(diagnostics, "GetActiveObject succeeded: " + progId);
                        return app;
                    }
                }
                catch (COMException ex)
                {
                    AddDiagnostic(diagnostics, "GetActiveObject failed: " + progId + " (" + FormatComException(ex) + ")");
                }
                catch (InvalidCastException ex)
                {
                    AddDiagnostic(diagnostics, "GetActiveObject returned an incompatible object: " + progId + " (" + ex.Message + ")");
                }
            }
            var tableResult = GetRunningObjectTable(0, out var table);
            if (tableResult == 0 && table != null)
            {
                var inspected = 0;
                table.EnumRunning(out var enumerator);
                var monikers = new IMoniker[1];
                var fetched = IntPtr.Zero;
                while (enumerator.Next(1, monikers, fetched) == 0)
                {
                    inspected++;
                    try
                    {
                        table.GetObject(monikers[0], out var raw);
                        var app = raw as EbApplication;
                        if (app != null)
                        {
                            AddDiagnostic(diagnostics, "Running Object Table succeeded after inspecting " + inspected.ToString(CultureInfo.InvariantCulture) + " object(s).");
                            return app;
                        }
                    }
                    catch (COMException ex)
                    {
                        AddDiagnostic(diagnostics, "Running Object Table object read failed (" + FormatComException(ex) + ")");
                    }
                    catch (InvalidCastException ex)
                    {
                        AddDiagnostic(diagnostics, "Running Object Table object cast failed (" + ex.Message + ")");
                    }
                }
                AddDiagnostic(diagnostics, "Running Object Table inspected " + inspected.ToString(CultureInfo.InvariantCulture) + " object(s), no EB " + Version + " application object found.");
            }
            else
            {
                AddDiagnostic(diagnostics, "GetRunningObjectTable failed: 0x" + tableResult.ToString("X8", CultureInfo.InvariantCulture));
            }

            var processProbe = ProbeEngineeringBaseProcesses(diagnostics);
            if (processProbe.ExpectedClientRunning)
            {
                try
                {
                    var type = Type.GetTypeFromProgID(ProgId);
                    if (type != null)
                    {
                        var app = (EbApplication)Activator.CreateInstance(type);
                        if (app != null && app.Folders != null)
                        {
                            AddDiagnostic(diagnostics, "Activator fallback succeeded: " + ProgId);
                            return app;
                        }
                        AddDiagnostic(diagnostics, "Activator fallback returned no usable folders: " + ProgId);
                    }
                    else
                    {
                        AddDiagnostic(diagnostics, "ProgID is not registered: " + ProgId);
                    }
                }
                catch (COMException ex)
                {
                    AddDiagnostic(diagnostics, "Activator fallback failed: " + ProgId + " (" + FormatComException(ex) + ")");
                }
                catch (InvalidCastException ex)
                {
                    AddDiagnostic(diagnostics, "Activator fallback returned an incompatible object: " + ProgId + " (" + ex.Message + ")");
                }
            }
            else if (processProbe.AnyEngineeringBaseProcess)
            {
                AddDiagnostic(diagnostics, "EngineeringBase process exists, but this adapter could not confirm it is EB " + Version + ". Check EB and EBAssistant are run by the same user and privilege level.");
            }
            else
            {
                AddDiagnostic(diagnostics, "No EngineeringBase.exe process was found.");
            }
            return null;
        }

        private static ClientProcessProbe ProbeEngineeringBaseProcesses(List<string> diagnostics)
        {
            var probe = new ClientProcessProbe();
            var processes = Process.GetProcessesByName("EngineeringBase");
            AddDiagnostic(diagnostics, "EngineeringBase process count: " + processes.Length.ToString(CultureInfo.InvariantCulture));
            foreach (var process in processes)
            {
                try
                {
                    probe.AnyEngineeringBaseProcess = true;
                    var path = process.MainModule == null ? "" : process.MainModule.FileName;
                    AddDiagnostic(diagnostics, "EngineeringBase process " + process.Id.ToString(CultureInfo.InvariantCulture) + " path: " + (string.IsNullOrWhiteSpace(path) ? "<empty>" : path));
                    if (!string.IsNullOrWhiteSpace(path) && path.IndexOf(ExpectedInstallFolder, StringComparison.OrdinalIgnoreCase) >= 0)
                        probe.ExpectedClientRunning = true;
                }
                catch (Exception ex)
                {
                    probe.AnyEngineeringBaseProcess = true;
                    AddDiagnostic(diagnostics, "EngineeringBase process " + process.Id.ToString(CultureInfo.InvariantCulture) + " path unavailable: " + ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }
            return probe;
        }

        private static string BuildConnectionFailureMessage(List<string> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return "未检测到可附着的 EB " + Version + "。";
            return "未检测到可附着的 EB " + Version + "。" + Environment.NewLine + string.Join(Environment.NewLine, diagnostics.ToArray());
        }

        private static void AddDiagnostic(List<string> diagnostics, string message)
        {
            if (diagnostics != null) diagnostics.Add(message);
        }

        private static string FormatComException(COMException ex)
        {
            return "0x" + ex.ErrorCode.ToString("X8", CultureInfo.InvariantCulture) + " " + ex.Message;
        }

        private sealed class ClientProcessProbe
        {
            public bool AnyEngineeringBaseProcess;
            public bool ExpectedClientRunning;
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);

        private static AdapterResponse<FolderTreeResult> GetAttributeFolderTree(EbApplication app)
        {
            var root = app.Folders.Attributes;
            var result = new FolderTreeResult();
            foreach (object raw in root.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                if (child.Kind == AucObjectKind.aucObjFolderForUserAttributes)
                {
                    result.Folders.Add(ReadFolder(child, root.Name));
                }
            }
            CollectExistingAttributes(root.Children, result.ExistingAttributes);
            return Ok(result, "属性目录读取成功。");
        }

        private static AdapterResponse<AttributeFolderIdentity> GetAttributeFolderIdentity(EbApplication app)
        {
            var root = app.Folders.Attributes;
            return Ok(new AttributeFolderIdentity { Version = Version, RootId = root.ID, RootName = root.Name }, "属性目录身份读取成功。");
        }

        private static AttributeFolderNode ReadFolder(ObjectItem folder, string parentPath)
        {
            var path = string.IsNullOrWhiteSpace(parentPath) ? folder.Name : parentPath + " / " + folder.Name;
            var node = new AttributeFolderNode { Id = folder.ID, Name = folder.Name, FullPath = path };
            foreach (object raw in folder.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && child.Kind == AucObjectKind.aucObjFolderForUserAttributes)
                {
                    node.Children.Add(ReadFolder(child, path));
                }
            }
            return node;
        }

        private static void CollectExistingAttributes(IEnumerable children, List<ExistingAttribute> result)
        {
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                if (child.Kind == AucObjectKind.aucObjUserAttribute)
                {
                    result.Add(new ExistingAttribute { Name = child.Name });
                }
                else if (child.Kind == AucObjectKind.aucObjFolderForUserAttributes)
                {
                    CollectExistingAttributes(child.Children as IEnumerable, result);
                }
            }
        }

        private static AdapterResponse<CreateAttributesResult> CreateAttributes(EbApplication app, CreateAttributesRequest request)
        {
            if (request == null || request.Attributes == null || request.Attributes.Count == 0)
                return Fail<CreateAttributesResult>("没有可创建的属性。");

            var root = app.Folders.Attributes;
            var target = FindFolder(root.Children, request.TargetFolderId);
            if (target == null) return Fail<CreateAttributesResult>("所选属性目录已不存在，请刷新后重试。");

            var existing = new List<ExistingAttribute>();
            CollectExistingAttributes(root.Children, existing);
            var names = new HashSet<string>(existing.Select(x => x.Name.Trim()), StringComparer.OrdinalIgnoreCase);
            foreach (var item in request.Attributes)
            {
                if (string.IsNullOrWhiteSpace(item.Name)) return Fail<CreateAttributesResult>("存在空属性名称。");
                if (!names.Add(item.Name.Trim())) return Fail<CreateAttributesResult>("EB 中已存在或批次内重复属性：" + item.Name);
                ParseType(item.Type);
            }

            var preflightName = "EBAssistant_MoveCheck_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            ObjectItem preflight = null;
            try
            {
                preflight = root.NewAttribute(preflightName);
                preflight.Store();
                root.Store();
                if (!preflight.MoveTo(target)) throw new InvalidOperationException("当前 EB 不支持将新属性移动到所选属性分类目录。");
                preflight.Store();
                target.Store();
                if (preflight.Parent == null || !string.Equals(preflight.Parent.ID, target.ID, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("属性移动后无法从所选目录读回。");
            }
            finally
            {
                if (preflight != null)
                {
                    try { preflight.Delete(false, AucDeleteType.aucDeleteTStandard); target.Store(); root.Store(); }
                    catch { }
                }
            }

            var created = new List<CreatedAttribute>();
            var result = new CreateAttributesResult { Status = "创建中", TargetFolder = target.Name };
            var currentIndex = 0;
            try
            {
                for (var index = 0; index < request.Attributes.Count; index++)
                {
                    currentIndex = index;
                    var item = request.Attributes[index];
                    var obj = root.NewAttribute(item.Name.Trim(), ParseType(item.Type), item.Digits);
                    var record = new CreateAttributeOperationRecord
                    {
                        RowNumber = item.RowNumber,
                        Name = item.Name.Trim(),
                        Type = item.Type,
                        Status = "创建失败",
                        Message = "属性创建尚未完成。"
                    };
                    var createdItem = new CreatedAttribute { Object = obj, Record = record };
                    created.Add(createdItem);
                    result.Records.Add(record);
                    obj.Store();
                    root.Store();
                    if (!obj.MoveTo(target)) throw new InvalidOperationException("无法将属性移动到目录：" + item.Name);
                    obj.Store();
                    target.Store();
                    if (obj.Parent == null || !string.Equals(obj.Parent.ID, target.ID, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("属性创建后目录读回不一致：" + item.Name);
                    record.Status = "创建成功";
                    record.Message = "属性已创建并移动到目标目录。";
                    createdItem.Completed = true;
                    result.CreatedNames.Add(item.Name.Trim());
                }
                result.CreatedCount = created.Count;
                result.Status = "完成";
                result.Message = "批量创建成功。";
                return Ok(result, "批量创建成功。");
            }
            catch (Exception ex)
            {
                result.RolledBack = true;
                var error = Describe(ex);
                if (result.Records.Count <= currentIndex)
                {
                    var failed = request.Attributes[currentIndex];
                    result.Records.Add(new CreateAttributeOperationRecord
                    {
                        RowNumber = failed.RowNumber,
                        Name = failed.Name.Trim(),
                        Type = failed.Type,
                        Status = "创建失败",
                        Message = error
                    });
                }
                else
                {
                    created.Last().Record.Status = "创建失败";
                    created.Last().Record.Message = error;
                }
                foreach (var pending in request.Attributes.Skip(currentIndex + 1))
                {
                    result.Records.Add(new CreateAttributeOperationRecord
                    {
                        RowNumber = pending.RowNumber,
                        Name = pending.Name.Trim(),
                        Type = pending.Type,
                        Status = "未处理",
                        Message = "前序属性创建失败，已停止后续操作。"
                    });
                }

                foreach (var createdItem in created.AsEnumerable().Reverse())
                {
                    try
                    {
                        createdItem.Object.Delete(false, AucDeleteType.aucDeleteTStandard);
                        if (createdItem.Completed)
                        {
                            createdItem.Record.Status = "已回滚";
                            createdItem.Record.Message = "创建成功后因本批后续失败，已删除回滚。";
                        }
                        else
                        {
                            createdItem.Record.Message = error + "；已清理未完成的属性对象。";
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        var rollbackError = createdItem.Record.Name + "：" + Describe(rollbackEx);
                        result.RollbackErrors.Add(rollbackError);
                        createdItem.Record.Status = "回滚失败";
                        createdItem.Record.Message = rollbackError;
                    }
                }
                try { target.Store(); root.Store(); } catch { }
                result.CreatedNames = result.Records.Where(x => x.Status == "回滚失败").Select(x => x.Name).ToList();
                result.CreatedCount = result.CreatedNames.Count;
                result.Status = result.RollbackErrors.Count == 0 ? "失败，已回滚" : "失败，部分回滚失败";
                result.Message = "批量创建失败，已尝试回滚本批属性。" + error;
                return new AdapterResponse<CreateAttributesResult>
                {
                    Success = false,
                    Message = result.Message +
                              (result.RollbackErrors.Count == 0 ? "" : " 回滚错误：" + string.Join("；", result.RollbackErrors)),
                    Data = result
                };
            }
        }

        private static AdapterResponse<CreateFolderResult> CreateAttributeFolder(EbApplication app, CreateFolderRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return Fail<CreateFolderResult>("目录名称不能为空。");

            var root = app.Folders.Attributes;
            var parent = FindFolder(root.Children, request.ParentFolderId);
            if (parent == null) return Fail<CreateFolderResult>("所选父目录已不存在，请刷新后重试。");

            var name = request.Name.Trim();
            foreach (object raw in parent.Children as IEnumerable)
            {
                var existing = raw as ObjectItem;
                if (existing != null && existing.Kind == AucObjectKind.aucObjFolderForUserAttributes &&
                    string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
                    return Fail<CreateFolderResult>("所选目录下已存在同名目录：" + name);
            }

            ObjectItem created = null;
            try
            {
                created = parent.NewChild(AucObjectKind.aucObjFolderForUserAttributes);
                var nameAttribute = created.Attributes.Find((AucAttribute)5);
                if (nameAttribute == null) throw new InvalidOperationException("新目录未暴露名称属性。");
                nameAttribute.Value = name;
                created.Store();
                parent.Store();

                ObjectItem readback = null;
                foreach (object raw in parent.Children as IEnumerable)
                {
                    var child = raw as ObjectItem;
                    if (child != null && child.Kind == AucObjectKind.aucObjFolderForUserAttributes &&
                        string.Equals(child.ID, created.ID, StringComparison.OrdinalIgnoreCase))
                    {
                        readback = child;
                        break;
                    }
                }
                if (readback == null || !string.Equals(readback.Name, name, StringComparison.Ordinal))
                    throw new InvalidOperationException("新目录创建后读回不一致。");

                return Ok(new CreateFolderResult { Id = readback.ID, Name = readback.Name }, "目录创建成功。");
            }
            catch (Exception ex)
            {
                if (created != null)
                {
                    try { created.Delete(false, AucDeleteType.aucDeleteTStandard); parent.Store(); root.Store(); }
                    catch { }
                }
                return Fail<CreateFolderResult>("新建目录失败，已尝试清理新建对象。" + Describe(ex));
            }
        }

        private static AdapterResponse<object> DeleteEmptyAttributeFolder(EbApplication app, DeleteFolderRequest request)
        {
            var root = app.Folders.Attributes;
            var folder = FindFolder(root.Children, request == null ? "" : request.Id);
            if (folder == null) return Fail<object>("待删除目录不存在。");
            if ((folder.Children as IEnumerable).Cast<object>().Any()) return Fail<object>("只能删除空属性目录。");
            if (!folder.Delete(false, AucDeleteType.aucDeleteTStandard)) return Fail<object>("EB 未删除空属性目录。");
            root.Store();
            return Ok<object>(null, "空属性目录已删除。");
        }

        private static AdapterResponse<TypeDefinitionIdentity> GetTypeDefinitionIdentity(EbApplication app)
        {
            var root = app.Folders.TypeDefinitions;
            return Ok(new TypeDefinitionIdentity { Version = Version, RootId = root.ID, RootName = root.Name }, "类型定义身份读取成功。");
        }

        private static AdapterResponse<TypeDefinitionTreeResult> GetTypeDefinitionTree(EbApplication app)
        {
            var root = app.Folders.TypeDefinitions;
            var result = new TypeDefinitionTreeResult
            {
                Identity = new TypeDefinitionIdentity { Version = Version, RootId = root.ID, RootName = root.Name }
            };
            foreach (object raw in root.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null) result.Nodes.Add(ReadTypeDefinitionObject(app, child, root.Name, child as TypeDefinition));
            }
            return Ok(result, "类型定义树读取成功。");
        }

        private static AdapterResponse<ProjectTemplateIdentity> GetProjectTemplateIdentity(EbApplication app)
        {
            var root = app.Folders.ProjectTemplates;
            return Ok(new ProjectTemplateIdentity { Version = Version, RootId = root.ID, RootName = root.Name }, "项目模板身份读取成功。");
        }

        private static AdapterResponse<ProjectTemplateTreeResult> GetProjectTemplateTree(EbApplication app)
        {
            var root = app.Folders.ProjectTemplates;
            var result = new ProjectTemplateTreeResult
            {
                Identity = new ProjectTemplateIdentity { Version = Version, RootId = root.ID, RootName = root.Name }
            };
            foreach (object raw in root.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                var node = ReadProjectTemplateNode(child, root.Name);
                if (node != null) result.Nodes.Add(node);
            }
            return Ok(result, "项目模板树读取成功。");
        }

        private static ProjectTemplateNode ReadProjectTemplateNode(ObjectItem item, string parentPath)
        {
            var path = parentPath + " / " + item.Name;
            if (item.Kind != AucObjectKind.aucObjProject && !IsFolderKind(item.Kind)) return null;

            var node = new ProjectTemplateNode
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = path,
                IsTemplateProject = item.Kind == AucObjectKind.aucObjProject
            };
            if (node.IsTemplateProject) return node;

            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                var childNode = ReadProjectTemplateNode(child, path);
                if (childNode != null) node.Children.Add(childNode);
            }
            return node.Children.Count > 0 ? node : null;
        }

        private static AdapterResponse<GraphicTemplateIdentity> GetGraphicTemplateIdentity(EbApplication app)
        {
            var root = app.Folders.Stencils as ObjectItem;
            if (root == null) return Fail<GraphicTemplateIdentity>("无法读取 EB 图形模板根目录。");
            return Ok(new GraphicTemplateIdentity { Version = Version, RootId = root.ID, RootName = root.Name }, "图形模板身份读取成功。");
        }

        private static AdapterResponse<GraphicTemplateTreeResult> GetGraphicTemplateTree(EbApplication app)
        {
            var root = app.Folders.Stencils as ObjectItem;
            if (root == null) return Fail<GraphicTemplateTreeResult>("无法读取 EB 图形模板根目录。");
            var result = new GraphicTemplateTreeResult
            {
                Identity = new GraphicTemplateIdentity { Version = Version, RootId = root.ID, RootName = root.Name }
            };
            foreach (object raw in root.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null || IsGraphicTemplateItem(child)) continue;
                result.Nodes.Add(ReadGraphicTemplateDirectory(child, root.Name));
            }
            return Ok(result, "图形模板目录读取成功。");
        }

        private static AdapterResponse<GraphicTemplateDirectoryNode> GetGraphicTemplateDirectory(EbApplication app, GraphicTemplateDirectoryRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DirectoryId))
                return Fail<GraphicTemplateDirectoryNode>("图形模板目录 ID 不能为空。");

            var root = app.Folders.Stencils as ObjectItem;
            if (root == null) return Fail<GraphicTemplateDirectoryNode>("无法读取 EB 图形模板根目录。");

            var directory = string.Equals(request.DirectoryId, root.ID, StringComparison.OrdinalIgnoreCase)
                ? root
                : app.Utils.GetSnglObjectByID(request.DirectoryId) as ObjectItem;
            if (directory == null) return Fail<GraphicTemplateDirectoryNode>("所选图形模板目录已不存在，请刷新后重试。");
            if (IsGraphicTemplateItem(directory)) return Fail<GraphicTemplateDirectoryNode>("所选对象不是图形模板目录。");

            var path = BuildGraphicTemplateDirectoryPath(root, directory);
            var node = ReadGraphicTemplateDirectoryShallow(directory, ParentPath(path));
            if (string.IsNullOrWhiteSpace(path)) node.FullPath = root.Name + " / " + directory.Name;
            return Ok(node, "图形模板目录读取成功。");
        }

        private static GraphicTemplateDirectoryNode ReadGraphicTemplateDirectory(ObjectItem item, string parentPath)
        {
            var path = parentPath + " / " + item.Name;
            var node = new GraphicTemplateDirectoryNode
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = path,
                Kind = item.Kind.ToString(),
                TypeName = Safe(delegate { return item.TypeName; }, "")
            };
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                if (IsGraphicTemplateItem(child))
                    node.Templates.Add(ReadGraphicTemplateItem(child, item, path));
                else
                    node.Children.Add(ReadGraphicTemplateDirectory(child, path));
            }
            return node;
        }

        private static GraphicTemplateDirectoryNode ReadGraphicTemplateDirectoryShallow(ObjectItem item, string parentPath)
        {
            var path = string.IsNullOrWhiteSpace(parentPath) ? item.Name : parentPath + " / " + item.Name;
            var node = new GraphicTemplateDirectoryNode
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = path,
                Kind = item.Kind.ToString(),
                TypeName = Safe(delegate { return item.TypeName; }, "")
            };
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                if (IsGraphicTemplateItem(child))
                {
                    node.Templates.Add(ReadGraphicTemplateItem(child, item, path));
                    continue;
                }
                node.Children.Add(new GraphicTemplateDirectoryNode
                {
                    Id = child.ID,
                    Name = child.Name,
                    FullPath = path + " / " + child.Name,
                    Kind = child.Kind.ToString(),
                    TypeName = Safe(delegate { return child.TypeName; }, "")
                });
            }
            return node;
        }

        private static GraphicTemplateItem ReadGraphicTemplateItem(ObjectItem item, ObjectItem parent, string parentPath)
        {
            var sync = ReadAttributeValue(item, 45);
            var parentId = parent == null ? "" : parent.ID;
            return new GraphicTemplateItem
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = parentPath + " / " + item.Name,
                ParentDirectoryId = parentId,
                Kind = item.Kind.ToString(),
                TypeName = Safe(delegate { return item.TypeName; }, ""),
                SymbolSyncDesignation = sync,
                MasterUniRef = string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(sync) ? "" : parentId + "#" + sync
            };
        }

        private static AdapterResponse<MoveGraphicTemplatesResult> MoveGraphicTemplates(EbApplication app, MoveGraphicTemplatesRequest request)
        {
            var result = new MoveGraphicTemplatesResult();
            if (request == null || string.IsNullOrWhiteSpace(request.TargetDirectoryId) || request.TemplateIds == null || request.TemplateIds.Count == 0)
                return FailWithData("图形模板迁移请求无效。", result);

            var root = app.Folders.Stencils as ObjectItem;
            if (root == null) return FailWithData("无法读取 EB 图形模板根目录。", result);

            var target = app.Utils.GetSnglObjectByID(request.TargetDirectoryId) as ObjectItem;
            if (target == null) return FailWithData("迁往目录已不存在，请刷新后重试。", result);
            if (IsGraphicTemplateItem(target)) return FailWithData("迁往目标不是图形模板目录。", result);
            if (HasGraphicTemplateDirectoryChildren(target)) return FailWithData("只能迁往最后一级图形模板目录。", result);

            result.TargetDirectoryId = target.ID;
            result.TargetDirectoryPath = BuildGraphicTemplateDirectoryPath(root, target);
            var processId = FindEbProcessId();
            foreach (var id in request.TemplateIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                result.Records.Add(MoveGraphicTemplate(app, root, target, result.TargetDirectoryPath, processId, id));
            }
            ApplyGraphicTemplateMigrationSummary(result);
            return result.FailedCount == 0
                ? Ok(result, "图形模板迁移完成。")
                : new AdapterResponse<MoveGraphicTemplatesResult> { Success = false, Message = "部分或全部图形模板迁移失败。", Data = result };
        }

        private static GraphicTemplateMigrationRecord MoveGraphicTemplate(EbApplication app, ObjectItem root, ObjectItem target, string targetPath, int processId, string templateId)
        {
            var record = new GraphicTemplateMigrationRecord
            {
                TemplateId = templateId,
                TargetDirectoryId = target.ID,
                TargetDirectoryPath = targetPath
            };
            try
            {
                var item = app.Utils.GetSnglObjectByID(templateId) as ObjectItem;
                if (item == null) throw new InvalidOperationException("模板图形已不存在。");
                if (!IsGraphicTemplateItem(item)) throw new InvalidOperationException("所选对象不是模板图形。");

                record.TemplateName = item.Name;
                record.SourceDirectoryId = item.Parent == null ? "" : item.Parent.ID;
                record.SourceDirectoryPath = item.Parent == null ? "" : BuildGraphicTemplateDirectoryPath(root, item.Parent);
                var targetTemplateIdsBefore = GetDirectGraphicTemplateIds(target);

                var copied = CopyGraphicTemplateObject(app, item, target, processId);
                if (!copied) throw new InvalidOperationException("EB 未接受模板图形复制。");

                string readbackMessage;
                if (!TryConfirmGraphicTemplateInTarget(app, target, templateId, record.TemplateName, targetTemplateIdsBefore, out var confirmedId, out readbackMessage))
                    throw new InvalidOperationException(readbackMessage);
                record.ConfirmedTemplateId = confirmedId;
                record.Status = "copied";
                record.Message = "复制成功（符号复制/粘贴）；" + readbackMessage + "；源对象保留。";
            }
            catch (Exception ex)
            {
                record.Status = "failed";
                record.Message = Describe(ex);
            }
            return record;
        }

        private static bool CopyGraphicTemplateObject(EbApplication app, ObjectItem item, ObjectItem target, int processId)
        {
            RunWithGraphicTemplateMismatchDialog(processId, delegate
            {
                var utils = (IAucVbaInternUtils)app;
                utils.ExecuteCommand(AucCommand.aucCmdSymCopy, item);
                utils.ExecuteCommand(AucCommand.aucCmdSymPaste, target);
                return true;
            });
            return true;
        }

        private static bool TryConfirmGraphicTemplateInTarget(
            EbApplication app,
            ObjectItem target,
            string originalId,
            string originalName,
            HashSet<string> targetTemplateIdsBefore,
            out string confirmedId,
            out string message)
        {
            confirmedId = "";
            var readback = app.Utils.GetSnglObjectByID(originalId) as ObjectItem;
            if (readback != null && readback.Parent != null && string.Equals(readback.Parent.ID, target.ID, StringComparison.OrdinalIgnoreCase))
            {
                confirmedId = readback.ID;
                message = "按原 ID 在目标目录读回确认。";
                return true;
            }

            var candidates = new List<ObjectItem>();
            foreach (object raw in target.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null || !IsGraphicTemplateItem(child)) continue;
                if (targetTemplateIdsBefore.Contains(child.ID)) continue;
                if (!string.Equals(child.Name, originalName, StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add(child);
            }

            if (candidates.Count == 1)
            {
                confirmedId = candidates[0].ID;
                message = "目标目录新增模板图形读回确认，新 ID：" + confirmedId + "。";
                return true;
            }

            message = candidates.Count == 0
                ? "迁移后无法在目标目录读回确认。"
                : "迁移后目标目录出现多个新增候选模板图形，无法唯一确认。";
            return false;
        }

        private static HashSet<string> GetDirectGraphicTemplateIds(ObjectItem directory)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object raw in directory.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && IsGraphicTemplateItem(child)) ids.Add(child.ID);
            }
            return ids;
        }

        private static HashSet<string> GetDirectGraphicTemplateNames(ObjectItem directory)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object raw in directory.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && IsGraphicTemplateItem(child)) names.Add(child.Name);
            }
            return names;
        }

        private static List<string> WaitForAddedGraphicTemplateIds(
            ObjectItem directory,
            HashSet<string> before,
            int timeoutMilliseconds)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            do
            {
                var added = GetDirectGraphicTemplateIds(directory).Where(id => !before.Contains(id)).ToList();
                if (added.Count > 0) return added;
                Thread.Sleep(100);
            } while (DateTime.UtcNow < end);
            return new List<string>();
        }

        private static AdapterResponse<CreateGraphicTemplatesResult> CreateGraphicTemplates(
            EbApplication app,
            CreateGraphicTemplatesRequest request)
        {
            var result = new CreateGraphicTemplatesResult();
            if (request == null ||
                string.IsNullOrWhiteSpace(request.DirectoryId) ||
                string.IsNullOrWhiteSpace(request.SourceTemplateId) ||
                request.RequestedTotalCount < 2 ||
                request.RequestedTotalCount > 200)
                return FailWithData("新建模板图形请求无效。", result);

            var root = app.Folders.Stencils as ObjectItem;
            if (root == null) return FailWithData("无法读取 EB 图形模板根目录。", result);

            var directory = app.Utils.GetSnglObjectByID(request.DirectoryId) as ObjectItem;
            if (directory == null) return FailWithData("所选目录已不存在，请刷新后重试。", result);
            if (IsGraphicTemplateItem(directory)) return FailWithData("所选对象不是图形模板目录。", result);
            if (HasGraphicTemplateDirectoryChildren(directory)) return FailWithData("只能在最后一级图形模板目录中新建。", result);

            var source = app.Utils.GetSnglObjectByID(request.SourceTemplateId) as ObjectItem;
            if (source == null || !IsGraphicTemplateItem(source))
                return FailWithData("源模板图形已不存在。", result);
            if (source.Parent == null || !string.Equals(source.Parent.ID, directory.ID, StringComparison.OrdinalIgnoreCase))
                return FailWithData("源模板图形不在所选目录中。", result);

            var originalIds = GetDirectGraphicTemplateIds(directory);
            if (originalIds.Count != 1 || !originalIds.Contains(source.ID))
                return FailWithData("所选目录中必须恰好存在一个模板图形。", result);

            result.DirectoryId = directory.ID;
            result.DirectoryPath = BuildGraphicTemplateDirectoryPath(root, directory);
            result.SourceTemplateId = source.ID;
            result.SourceTemplateName = source.Name;
            result.RequestedTotalCount = request.RequestedTotalCount;
            result.OriginalCount = originalIds.Count;

            var processId = FindEbProcessId();
            try
            {
                RunWithGraphicTemplateMismatchDialog(processId, delegate
                {
                    var utils = (IAucVbaInternUtils)app;
                    utils.ExecuteCommand(AucCommand.aucCmdSymCopy, source);
                    for (var copyNumber = 1; copyNumber < request.RequestedTotalCount; copyNumber++)
                    {
                        var before = GetDirectGraphicTemplateIds(directory);
                        var record = new GraphicTemplateCreationRecord { CopyNumber = copyNumber };
                        try
                        {
                            utils.ExecuteCommand(AucCommand.aucCmdSymPaste, directory);
                            var added = WaitForAddedGraphicTemplateIds(directory, before, 5000);
                            if (added.Count != 1)
                                throw new InvalidOperationException(
                                    added.Count == 0
                                        ? "粘贴后无法在当前目录读回新增模板图形。"
                                        : "粘贴后出现多个新增模板图形，无法唯一确认。");
                            record.ConfirmedTemplateId = added[0];
                            record.Status = "created";
                            record.Message = "创建成功并按新增 ID 读回确认。";
                            result.CreatedCount++;
                        }
                        catch (Exception ex)
                        {
                            record.Status = "failed";
                            record.Message = Describe(ex);
                            result.Records.Add(record);
                            break;
                        }
                        result.Records.Add(record);
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                if (!result.Records.Any(record => record.Status == "failed"))
                {
                    result.Records.Add(new GraphicTemplateCreationRecord
                    {
                        CopyNumber = result.CreatedCount + 1,
                        Status = "failed",
                        Message = Describe(ex)
                    });
                }
            }

            result.ConfirmedFinalCount = GetDirectGraphicTemplateIds(directory).Count;
            ApplyGraphicTemplateCreationSummary(result);
            return result.Status == "completed"
                ? Ok(result, "模板图形创建完成。")
                : FailWithData("模板图形创建未全部完成，已停止后续复制。", result);
        }

        private static AdapterResponse<CreateNamedGraphicTemplatesResult> CreateNamedGraphicTemplates(
            EbApplication app,
            CreateNamedGraphicTemplatesRequest request)
        {
            var result = new CreateNamedGraphicTemplatesResult();
            if (request == null ||
                string.IsNullOrWhiteSpace(request.DirectoryId) ||
                string.IsNullOrWhiteSpace(request.SourceTemplateId) ||
                request.TargetNames == null ||
                request.TargetNames.Count == 0)
                return FailWithData("命名创建图形模板请求无效。", result);

            var root = app.Folders.Stencils as ObjectItem;
            if (root == null) return FailWithData("无法读取 EB 图形模板根目录。", result);

            var directory = app.Utils.GetSnglObjectByID(request.DirectoryId) as ObjectItem;
            if (directory == null) return FailWithData("所选目录已不存在，请刷新后重试。", result);
            if (IsGraphicTemplateItem(directory)) return FailWithData("所选对象不是图形模板目录。", result);
            if (HasGraphicTemplateDirectoryChildren(directory)) return FailWithData("只能在最后一级图形模板目录中新建。", result);

            var source = app.Utils.GetSnglObjectByID(request.SourceTemplateId) as ObjectItem;
            if (source == null || !IsGraphicTemplateItem(source))
                return FailWithData("源模板图形已不存在。", result);
            if (source.Parent == null || !string.Equals(source.Parent.ID, directory.ID, StringComparison.OrdinalIgnoreCase))
                return FailWithData("源模板图形不在所选目录中。", result);

            var duplicateRequestNames = request.TargetNames
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateRequestNames.Count > 0)
                return FailWithData("请求中存在重复目标名称：" + string.Join(", ", duplicateRequestNames) + "。", result);

            var existingNames = GetDirectGraphicTemplateNames(directory);
            var conflicts = request.TargetNames.Where(name => existingNames.Contains(name)).ToList();
            if (conflicts.Count > 0)
                return FailWithData("目标目录中已存在名称：" + string.Join(", ", conflicts) + "。", result);

            result.DirectoryId = directory.ID;
            result.DirectoryPath = BuildGraphicTemplateDirectoryPath(root, directory);
            result.SourceTemplateId = source.ID;
            result.SourceTemplateName = source.Name;

            var processId = FindEbProcessId();
            try
            {
                RunWithGraphicTemplateMismatchDialog(processId, delegate
                {
                    var utils = (IAucVbaInternUtils)app;
                    utils.ExecuteCommand(AucCommand.aucCmdSymCopy, source);
                    foreach (var targetName in request.TargetNames)
                    {
                        var before = GetDirectGraphicTemplateIds(directory);
                        var record = new NamedGraphicTemplateCreationRecord { RequestedName = targetName };
                        try
                        {
                            utils.ExecuteCommand(AucCommand.aucCmdSymPaste, directory);
                            var added = WaitForAddedGraphicTemplateIds(directory, before, 5000);
                            if (added.Count != 1)
                                throw new InvalidOperationException(
                                    added.Count == 0
                                        ? "粘贴后无法在当前目录读回新增模板图形。"
                                        : "粘贴后出现多个新增模板图形，无法唯一确认。");

                            var created = app.Utils.GetSnglObjectByID(added[0]) as ObjectItem;
                            if (created == null || !IsGraphicTemplateItem(created))
                                throw new InvalidOperationException("新增对象无法作为模板图形读回。");

                            SetObjectName(created, targetName);
                            created.Store();
                            directory.Store();

                            var readback = app.Utils.GetSnglObjectByID(created.ID) as ObjectItem;
                            if (readback == null || !string.Equals(readback.Name, targetName, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("重命名后无法读回确认目标名称。");

                            record.ConfirmedTemplateId = created.ID;
                            record.Status = "created";
                            record.Message = "创建、命名并读回确认。";
                        }
                        catch (Exception ex)
                        {
                            record.Status = "failed";
                            record.Message = Describe(ex);
                            result.Records.Add(record);
                            break;
                        }
                        result.Records.Add(record);
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                result.Records.Add(new NamedGraphicTemplateCreationRecord { Status = "failed", Message = Describe(ex) });
            }

            ApplyNamedGraphicTemplateCreationSummary(result, request.TargetNames.Count);
            return result.Status == "completed"
                ? Ok(result, "命名图形模板创建完成。")
                : FailWithData("命名图形模板创建未全部完成，已停止。", result);
        }

        private static AdapterResponse<OpenGraphicTemplateWithVisioResult> OpenGraphicTemplateWithVisio(
            EbApplication app,
            OpenGraphicTemplateWithVisioRequest request)
        {
            var result = new OpenGraphicTemplateWithVisioResult();
            if (request == null || string.IsNullOrWhiteSpace(request.TemplateId))
                return FailWithData("打开图形模板请求无效。", result);

            var template = app.Utils.GetSnglObjectByID(request.TemplateId) as ObjectItem;
            if (template == null || !IsGraphicTemplateItem(template))
                return FailWithData("所选对象不是模板图形。", result);

            result.TemplateId = template.ID;
            result.TemplateName = template.Name;

            try
            {
                var utils = (IAucVbaInternUtils)app;
                utils.ExecuteCommand(AucCommand.aucCmdSymOpenMaster, template);
                result.OpenMethod = "aucCmdSymOpenMaster";
                result.Status = "opened";
                result.Message = "已通过 API 命令打开图形模板。";
                return Ok(result, result.Message);
            }
            catch (Exception first)
            {
                try
                {
                    var utils = (IAucVbaInternUtils)app;
                    utils.ExecuteCommand(AucCommand.aucCmdSymOpen, template);
                    result.OpenMethod = "aucCmdSymOpen";
                    result.Status = "opened";
                    result.Message = "已通过 API 备用命令打开图形模板。";
                    return Ok(result, result.Message);
                }
                catch (Exception second)
                {
                    result.OpenMethod = "api";
                    result.Status = "failed";
                    result.Message = "API 打开失败：" + Describe(first) + "；备用命令失败：" + Describe(second);
                    return FailWithData(result.Message, result);
                }
            }
        }

        private static void SetObjectName(ObjectItem item, string name)
        {
            var nameAttribute = item.Attributes.Find((AucAttribute)5);
            if (nameAttribute == null) throw new InvalidOperationException("对象未暴露名称属性。");
            nameAttribute.Value = name;
        }

        private static bool RunWithGraphicTemplateMismatchDialog(int processId, Func<bool> action)
        {
            var stopDialogWatcher = false;
            Exception dialogError = null;
            var watcher = new Thread(delegate()
            {
                try { HandleGraphicTemplateMismatchDialog(processId, delegate { return stopDialogWatcher; }, 60000); }
                catch (Exception ex) { dialogError = ex; }
            });
            watcher.IsBackground = true;
            watcher.SetApartmentState(ApartmentState.STA);
            watcher.Start();

            try
            {
                return action();
            }
            finally
            {
                stopDialogWatcher = true;
                watcher.Join(1000);
                if (dialogError != null) throw new InvalidOperationException("处理图形符号类型转换确认框失败。", dialogError);
            }
        }

        private static void ReadPermissionConfigurationFolders(
            EbApplication app,
            out ObjectItem usersAndGroups,
            out ObjectItem messages)
        {
            dynamic folders = app.Folders;
            usersAndGroups = folders.UsersAndGroups as ObjectItem;
            messages = folders.Messages as ObjectItem;
            if (usersAndGroups == null || messages == null)
                throw new InvalidOperationException("EB 未返回用户及用户组目录或消息目录。");
        }

        private static PermissionConfigurationIdentity ReadPermissionConfigurationIdentity(EbApplication app)
        {
            var root = app.RootObject;
            ReadPermissionConfigurationFolders(app, out var usersAndGroups, out var messages);
            return new PermissionConfigurationIdentity
            {
                Version = Version,
                RootId = root.ID,
                RootName = Safe(() => root.Name, ""),
                UsersAndGroupsId = usersAndGroups.ID,
                UsersAndGroupsName = Safe(() => usersAndGroups.Name, ""),
                MessagesId = messages.ID
            };
        }

        private static AdapterResponse<PermissionConfigurationIdentity> GetPermissionConfigurationIdentity(EbApplication app)
        {
            var identity = ReadPermissionConfigurationIdentity(app);
            return Ok(identity, "权限配置身份读取成功。");
        }

        private static PermissionDirectoryNode ReadPermissionDirectoryNode(ObjectItem item, string parentPath)
        {
            var path = string.IsNullOrEmpty(parentPath) ? item.Name : parentPath + " / " + item.Name;
            var node = new PermissionDirectoryNode
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = path,
                IsSelectableMember =
                    item.Kind == AucObjectKind.aucObjUser ||
                    item.Kind == AucObjectKind.aucObjUserGroup ||
                    item.Kind == AucObjectKind.aucObjAllEngineeringBaseUsersGroup,
            };
            IEnumerable children;
            try
            {
                children = item.Children as IEnumerable;
            }
            // EB user and group leaves reject Children with 0x80046951.
            catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80046951))
            {
                return node;
            }

            if (children == null) return node;
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                var childNode = ReadPermissionDirectoryNode(child, path);
                node.Children.Add(childNode);
            }
            return node;
        }

        private static AdapterResponse<PermissionConfigurationStructureResult> GetPermissionConfigurationStructure(EbApplication app)
        {
            var identity = ReadPermissionConfigurationIdentity(app);
            ReadPermissionConfigurationFolders(app, out var usersAndGroups, out var messages);
            var result = new PermissionConfigurationStructureResult
            {
                Identity = identity,
            };

            // Left side: recursively read UsersAndGroups directory
            var leftRoot = ReadPermissionDirectoryNode(usersAndGroups, "");
            result.LeftNodes.Add(leftRoot);

            // Right side: iterate root children, exclude Messages and UsersAndGroups, keep only one level
            foreach (object raw in app.RootObject.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                if (child.ID == messages.ID || child.ID == usersAndGroups.ID) continue;
                result.RightNodes.Add(new PermissionDirectoryNode
                {
                    Id = child.ID,
                    Name = child.Name,
                    FullPath = child.Name,
                });
            }

            return Ok(result, "权限配置结构读取成功。");
        }

        private static AdapterResponse<PermissionMemberAssignmentResult> AddPermissionMembers(
            EbApplication app,
            PermissionMemberAssignmentRequest request)
        {
            var memberIds = DistinctNonEmpty(request == null ? null : request.MemberIds);
            var directoryIds = DistinctNonEmpty(request == null ? null : request.DirectoryIds);
            if (memberIds.Count == 0 || directoryIds.Count == 0)
                return Fail<PermissionMemberAssignmentResult>("必须至少选择一个用户或用户组，并至少选择一个权限目录。");

            var identity = ReadPermissionConfigurationIdentity(app);
            var members = memberIds.Select(id => ResolvePermissionMember(app, id)).ToList();
            var directories = directoryIds.Select(id => ResolvePermissionDirectory(app, identity, id)).ToList();
            var result = new PermissionMemberAssignmentResult();

            foreach (var member in members)
            {
                foreach (var directory in directories)
                {
                    var record = new PermissionMemberAssignmentRecord
                    {
                        MemberId = member.Id,
                        MemberName = member.Name,
                        DirectoryId = directory.Id,
                        DirectoryName = directory.Name,
                    };

                    if (!string.IsNullOrEmpty(member.Error))
                    {
                        record.Status = "failed";
                        record.Message = member.Error;
                    }
                    else if (!string.IsNullOrEmpty(directory.Error))
                    {
                        record.Status = "failed";
                        record.Message = directory.Error;
                    }
                    else
                    {
                        try
                        {
                            var permissions = GetAccessPermissions(directory.Object);
                            if (ContainsPermissionSid(permissions, member.Sid))
                            {
                                record.Status = "skipped_existing";
                                record.Message = "该用户或用户组已存在于权限目录中。";
                            }
                            else
                            {
                                if (member.Group != null) permissions.AddGroup(member.Group);
                                else permissions.Add(member.User);

                                var readback = app.Utils.GetSnglObjectByID(directory.Id) as ObjectItem;
                                if (readback == null || !ContainsPermissionSid(GetAccessPermissions(readback), member.Sid))
                                    throw new InvalidOperationException("添加后读回未找到该用户或用户组。");

                                record.Status = "added";
                                record.Message = "已添加并读回确认；未调用 SetRight。";
                            }
                        }
                        catch (Exception ex)
                        {
                            record.Status = "failed";
                            record.Message = Describe(ex);
                        }
                    }
                    result.Records.Add(record);
                }
            }

            ApplyPermissionAssignmentSummary(result);
            return Ok(result, result.FailedCount == 0 ? "权限成员添加完成。" : "权限成员添加完成，但部分项目失败。");
        }

        private static List<string> DistinctNonEmpty(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ResolvedPermissionMember ResolvePermissionMember(EbApplication app, string id)
        {
            var resolved = new ResolvedPermissionMember { Id = id };
            try
            {
                var item = app.Utils.GetSnglObjectByID(id) as ObjectItem;
                if (item == null) throw new InvalidOperationException("无法按 ID 找到用户或用户组。");
                resolved.Name = item.Name;

                var userObject = item as User;
                if (userObject != null)
                {
                    resolved.User = FindAccessControlUserBySid(app.AccessControl.WinUsersAndGroups, userObject.UserSID);
                    if (resolved.User == null) throw new InvalidOperationException("无法在 AccessControl.WinUsersAndGroups 中匹配用户 SID。");
                    resolved.Sid = resolved.User.SID;
                    if (string.IsNullOrWhiteSpace(resolved.Sid)) throw new InvalidOperationException("用户 SID 为空。");
                    return resolved;
                }

                if (item.Kind != AucObjectKind.aucObjUserGroup &&
                    item.Kind != AucObjectKind.aucObjAllEngineeringBaseUsersGroup)
                    throw new InvalidOperationException("所选对象不是用户或用户组。");

                var matches = new List<AccessControlGroup>();
                foreach (object raw in app.AccessControl.Groups as IEnumerable)
                {
                    var group = raw as AccessControlGroup;
                    if (group != null && string.Equals(group.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                        matches.Add(group);
                }
                if (matches.Count != 1) throw new InvalidOperationException("无法按唯一名称匹配 EB 用户组。");

                resolved.Group = matches[0];
                resolved.Sid = matches[0].SID;
                if (string.IsNullOrWhiteSpace(resolved.Sid)) throw new InvalidOperationException("用户组 SID 为空。");
            }
            catch (Exception ex)
            {
                resolved.Error = Describe(ex);
            }
            return resolved;
        }

        private static AccessControlUser FindAccessControlUserBySid(AccessControlUsers users, string sid)
        {
            foreach (object raw in users as IEnumerable)
            {
                var user = raw as AccessControlUser;
                if (user != null && string.Equals(user.SID, sid, StringComparison.OrdinalIgnoreCase)) return user;
            }
            return null;
        }

        private static ResolvedPermissionDirectory ResolvePermissionDirectory(
            EbApplication app,
            PermissionConfigurationIdentity identity,
            string id)
        {
            var resolved = new ResolvedPermissionDirectory { Id = id };
            try
            {
                var item = app.Utils.GetSnglObjectByID(id) as ObjectItem;
                if (item == null) throw new InvalidOperationException("无法按 ID 找到权限目录。");
                resolved.Name = item.Name;
                if (item.Parent == null || !string.Equals(item.Parent.ID, identity.RootId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("只允许选择 EB 根目录的直接子目录。");
                if (string.Equals(id, identity.UsersAndGroupsId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, identity.MessagesId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("该根目录不允许配置权限成员。");
                GetAccessPermissions(item);
                resolved.Object = item;
            }
            catch (Exception ex)
            {
                resolved.Error = Describe(ex);
            }
            return resolved;
        }

        private static AccessControlUsers GetAccessPermissions(ObjectItem item)
        {
            var projects = item as IAucProjectsFolder;
            if (projects != null) return projects.AccessPermissions;
            var typeDefinitions = item as IAucVbaTypeDefinitionsFolder;
            if (typeDefinitions != null) return typeDefinitions.AccessPermissions;
            var attributes = item as IAucVbaAttributesFolder;
            if (attributes != null) return attributes.AccessPermissions;
            var projectTemplates = item as IAucVbaProjectTemplatesFolder;
            if (projectTemplates != null) return projectTemplates.AccessPermissions;
            var stencils = item as IAucVbaStencils;
            if (stencils != null) return stencils.AccessPermissions;
            var macros = item as IAucVbaMacrosFolder;
            if (macros != null) return macros.AccessPermissions;
            var catalogs = item as IAucVbaCatalogsFolder;
            if (catalogs != null) return catalogs.AccessPermissions;
            var dictionaries = item as IAucVbaDictionariesFolder;
            if (dictionaries != null) return dictionaries.AccessPermissions;
            var templates = item as IAucVbaTemplatesFolder;
            if (templates != null) return templates.AccessPermissions;
            var addInTemplates = item as IAucVbaAddInTemplatesFolder;
            if (addInTemplates != null) return addInTemplates.AccessPermissions;
            var folderForProjects = item as IAucFolderForProjects;
            if (folderForProjects != null) return folderForProjects.AccessPermissions;
            throw new InvalidOperationException("该目录类型不公开 AccessPermissions。");
        }

        private static bool ContainsPermissionSid(AccessControlUsers permissions, string sid)
        {
            foreach (object raw in permissions as IEnumerable)
            {
                var user = raw as AccessControlUser;
                if (user != null && string.Equals(user.SID, sid, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void ApplyPermissionAssignmentSummary(PermissionMemberAssignmentResult result)
        {
            result.TotalCount = result.Records.Count;
            result.AddedCount = result.Records.Count(record => record.Status == "added");
            result.SkippedCount = result.Records.Count(record => record.Status == "skipped_existing");
            result.FailedCount = result.TotalCount - result.AddedCount - result.SkippedCount;
            result.Status = result.FailedCount == 0 ? "completed" : "completed_with_failures";
        }

        private static TypeDefinitionNode ReadTypeDefinitionObject(EbApplication app, ObjectItem item, string parentPath, TypeDefinition definition)
        {
            var path = parentPath + " / " + item.Name;
            var node = new TypeDefinitionNode { Id = item.ID, Name = item.Name, FullPath = path, NodeType = "folder", IsActionable = false };
            var directDefinition = item as TypeDefinition;
            if (directDefinition != null)
            {
                definition = directDefinition;
                node.NodeType = "type-definition";
            }
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null) node.Children.Add(ReadTypeDefinitionObject(app, child, path, definition));
            }
            if (node.Children.Count == 0 && definition != null)
            {
                var matched = ResolveTypeItem(app, item.ID) ?? FindTypeItem(definition, item.Name);
                if (matched != null)
                {
                    node.Id = matched.ID;
                    node.NodeType = "type-item";
                    node.IsActionable = true;
                }
                else if (directDefinition != null)
                {
                    foreach (object raw in definition.TypeItems as IEnumerable)
                    {
                        var typeItem = raw as TypeItem;
                        if (typeItem != null)
                        {
                            node.Children.Add(new TypeDefinitionNode
                            {
                                Id = typeItem.ID, Name = typeItem.Name, FullPath = path + " / " + typeItem.Name,
                                NodeType = "type-item", IsActionable = true
                            });
                        }
                    }
                }
            }
            else if (node.Children.Count == 0)
            {
                var matched = ResolveTypeItem(app, item.ID);
                if (matched != null)
                {
                    node.Id = matched.ID;
                    node.NodeType = "type-item";
                    node.IsActionable = true;
                }
            }
            return node;
        }

        private static TypeItem ResolveTypeItem(EbApplication app, string id)
        {
            try { return app.Utils.GetSnglObjectByID(id) as TypeItem; }
            catch { return null; }
        }

        private static TypeItem FindTypeItem(TypeDefinition definition, string name)
        {
            foreach (object raw in definition.TypeItems as IEnumerable)
            {
                var item = raw as TypeItem;
                if (item != null && string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        private static AdapterResponse<ValidateAttributeIdsResult> ValidateAttributeIds(EbApplication app, ValidateAttributeIdsRequest request)
        {
            var requested = new HashSet<int>((request == null || request.AttributeIds == null) ? new List<int>() : request.AttributeIds);
            var existing = new HashSet<int>();
            CollectAttributeDefinitionIds(app.Folders.Attributes.Children, requested, existing);
            foreach (var aid in requested.Except(existing).ToList())
            {
                try
                {
                    IAucVbaInternUtils utils = (IAucVbaInternUtils)app;
                    Array aids = new AucAttribute[] { (AucAttribute)aid };
                    Array descriptions;
                    utils.GetAttributeDescription(ref aids, out descriptions);
                    if (descriptions == null || descriptions.Length == 0) continue;
                    foreach (object raw in descriptions)
                    {
                        var description = (AucAttributeDescription)raw;
                        if (!string.IsNullOrWhiteSpace(description.sbName))
                        {
                            existing.Add(aid);
                            break;
                        }
                    }
                }
                catch { }
            }
            return Ok(new ValidateAttributeIdsResult
            {
                ExistingIds = existing.OrderBy(x => x).ToList(),
                MissingIds = requested.Except(existing).OrderBy(x => x).ToList()
            }, "属性 ID 校验完成。");
        }

        private static AdapterResponse<ValidateWorksheetAttributeIdsResult> ValidateWorksheetAttributeIds(EbApplication app, ValidateWorksheetAttributeIdsRequest request)
        {
            var requested = new HashSet<int>((request == null || request.AttributeIds == null) ? new List<int>() : request.AttributeIds);
            var existing = new HashSet<int>();
            CollectAttributeDefinitionIds(app.Folders.Attributes.Children, requested, existing);
            foreach (var aid in requested.Except(existing).ToList())
            {
                try
                {
                    IAucVbaInternUtils utils = (IAucVbaInternUtils)app;
                    Array aids = new AucAttribute[] { (AucAttribute)aid };
                    Array descriptions;
                    utils.GetAttributeDescription(ref aids, out descriptions);
                    if (descriptions == null || descriptions.Length == 0) continue;
                    foreach (object raw in descriptions)
                    {
                        var description = (AucAttributeDescription)raw;
                        if (!string.IsNullOrWhiteSpace(description.sbName))
                        {
                            existing.Add(aid);
                            break;
                        }
                    }
                }
                catch { }
            }
            return Ok(new ValidateWorksheetAttributeIdsResult
            {
                ExistingIds = existing.OrderBy(x => x).ToList(),
                MissingIds = requested.Except(existing).OrderBy(x => x).ToList()
            }, "工作表属性 ID 校验完成。");
        }

        private static AdapterResponse<WorksheetCreationContextResult> GetWorksheetCreationContext(EbApplication app, WorksheetCreationContextRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TemplateProjectId))
                return Fail<WorksheetCreationContextResult>("模板项目 ID 不能为空。");

            Project template;
            ObjectItem favorite;
            string templatePath;
            string targetPath;
            string error;
            if (!TryResolveWorksheetTarget(app, request.TemplateProjectId, out template, out favorite, out templatePath, out targetPath, out error))
                return Fail<WorksheetCreationContextResult>(error);
            var result = new WorksheetCreationContextResult
            {
                TemplateProjectPath = templatePath,
                TargetFolderPath = targetPath
            };
            foreach (object raw in favorite.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && child.Kind == AucObjectKind.aucObjSheet)
                {
                    result.ExistingWorksheetNames.Add(child.Name);
                }
            }
            return Ok(result, "工作表创建上下文读取成功。");
        }

        private static AdapterResponse<ValidateWorksheetCreationCapabilityResult> ValidateWorksheetCreationCapability(
            EbApplication app,
            ValidateWorksheetCreationCapabilityRequest request)
        {
            var result = new ValidateWorksheetCreationCapabilityResult
            {
                TemporaryWorksheetName = "__EBAssistant_WorksheetCapability_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
            };
            if (request == null || request.AttributeIds == null || request.AttributeIds.Count < 2)
                return FailWithData("能力验证至少需要两个属性 ID。", result);

            Project template;
            ObjectItem favorite;
            string templatePath;
            string targetPath;
            string error;
            if (!TryResolveWorksheetTarget(app, request.TemplateProjectId, out template, out favorite, out templatePath, out targetPath, out error))
                return FailWithData(error, result);
            result.TargetFolderPath = targetPath;
            var interactiveProject = template;
            if (interactiveProject == null) return FailWithData("当前数据库中未找到可用于交互验证的普通项目。", result);
            var equipmentFolder = interactiveProject.EquipmentFolder;
            if (equipmentFolder == null) return FailWithData("当前普通项目下未找到设备目录。", result);
            ObjectItem interactiveFavorite;
            if (!TryResolveWorksheetFavorite(interactiveProject, out interactiveFavorite, out error))
                return FailWithData("无法解析交互项目的工作表收藏夹：" + error, result);

            Worksheet worksheet = null;
            ObjectItem temporaryObject = null;
            try
            {
                worksheet = equipmentFolder.OpenWorksheetDirect(
                    AucObjectKind.aucObjDevice,
                    AucAttribute.aucAttrUnspecified,
                    AucVbFindCondition.aucCondEqual,
                    "");
                result.Checks.Add("已打开临时器件工作表。");

                var first = worksheet.Attributes.Add((AucAttribute)request.AttributeIds[0], 0);
                var second = worksheet.Attributes.Add((AucAttribute)request.AttributeIds[1], 1);
                first.Width = WorksheetWidth("设备名称");
                second.Width = WorksheetWidth("这是用于验证自动列宽的较长列标签");
                worksheet.ProtectColumnWidth = true;
                result.Checks.Add("已添加两列并设置受保护的自动列宽。");

                worksheet.SaveConfiguration(result.TemporaryWorksheetName, interactiveFavorite);
                temporaryObject = FindUniqueDirectChildByName(interactiveFavorite.Children as IEnumerable, result.TemporaryWorksheetName);
                if (temporaryObject == null) throw new InvalidOperationException("保存后无法在交互项目收藏夹中读回临时工作表配置。");
                result.Checks.Add("临时工作表配置已保存并读回：" + temporaryObject.ID);
                try { worksheet.Close(); } catch { }
                ((IAucVbaInternUtils)app).ExecuteCommand(AucCommand.aucCmdSynchronizeTreeToObject, equipmentFolder);
                Thread.Sleep(500);
                worksheet = equipmentFolder.OpenWorksheet(temporaryObject.ID, true);
                if (worksheet == null) throw new InvalidOperationException("无法解析已打开的临时工作表配置。");
                first = worksheet.Attributes.ItemByID((AucAttribute)request.AttributeIds[0]);
                second = worksheet.Attributes.ItemByID((AucAttribute)request.AttributeIds[1]);
                result.Checks.Add("已打开临时工作表配置的交互界面。");

                var processId = FindEbProcessId();
                if (processId == 0) throw new InvalidOperationException("无法定位 EB 主进程。");
                result.Checks.Add("已定位 EB 进程：" + processId);

                var sourceObject = equipmentFolder;
                var widths = new[] { first.Width, second.Width };
                SetWorksheetColumnLabel(app, sourceObject, processId, 0, request.AttributeIds[0], widths, "设备名称", result.Checks);
                SetWorksheetColumnLabel(app, sourceObject, processId, 1, request.AttributeIds[1], widths, "这是用于验证自动列宽的较长列标签", result.Checks);
                if (!string.Equals(first.Name, "设备名称", StringComparison.Ordinal) ||
                    !string.Equals(second.Name, "这是用于验证自动列宽的较长列标签", StringComparison.Ordinal))
                    throw new InvalidOperationException("列标签写入后读回不一致。");
                result.Checks.Add("两列标签写入并读回确认成功。");

                ((IAucVbaInternUtils)app).ExecuteCommand(AucCommand.aucCmdSaveListConfiguration, temporaryObject);
                result.Checks.Add("列标签修改已保存到临时工作表配置。");
                if (!string.Equals(interactiveProject.ID, template.ID, StringComparison.OrdinalIgnoreCase) && !temporaryObject.MoveTo(favorite)) throw new InvalidOperationException("无法将临时工作表配置移动到目标模板收藏夹。");
                temporaryObject.Store();
                favorite.Store();
                if (FindUniqueDirectChildByName(favorite.Children as IEnumerable, result.TemporaryWorksheetName) == null)
                    throw new InvalidOperationException("移动后无法在目标模板收藏夹读回临时工作表配置。");
                result.Checks.Add("临时工作表配置位于目标模板收藏夹并读回确认。");
                result.Passed = true;
                return Ok(result, "工作表创建能力验证通过。");
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Checks.Add("验证失败：" + Describe(ex));
                return FailWithData("工作表创建能力验证失败：" + Describe(ex), result);
            }
            finally
            {
                if (worksheet != null)
                {
                    try { worksheet.Close(); result.CleanupChecks.Add("临时工作表已关闭。"); }
                    catch (Exception ex) { result.CleanupChecks.Add("关闭临时工作表失败：" + Describe(ex)); result.Passed = false; }
                }
                if (temporaryObject == null)
                    temporaryObject = FindUniqueDirectChildByNameSafe(favorite, result.TemporaryWorksheetName);
                if (temporaryObject != null)
                {
                    try
                    {
                        var id = temporaryObject.ID;
                        if (!temporaryObject.Delete(false, AucDeleteType.aucDeleteTStandard))
                            throw new InvalidOperationException("EB 未删除临时工作表配置。");
                        favorite.Store();
                        result.CleanupChecks.Add("临时工作表配置已删除：" + id);
                    }
                    catch (Exception ex)
                    {
                        result.CleanupChecks.Add("删除临时工作表配置失败：" + Describe(ex));
                        result.Passed = false;
                    }
                }
                else
                {
                    result.CleanupChecks.Add("未发现需要删除的临时工作表配置。");
                }
            }
        }

        private static AdapterResponse<CreateWorksheetsResult> CreateWorksheets(EbApplication app, CreateWorksheetsRequest request)
        {
            var result = new CreateWorksheetsResult { Status = "completed" };
            if (request == null || string.IsNullOrWhiteSpace(request.TemplateProjectId) || request.Worksheets == null)
                return Fail<CreateWorksheetsResult>("工作表创建请求无效。");

            Project project;
            ObjectItem favorite;
            string projectPath;
            string targetPath;
            string error;
            if (!TryResolveWorksheetTarget(app, request.TemplateProjectId, out project, out favorite, out projectPath, out targetPath, out error))
                return Fail<CreateWorksheetsResult>(error);

            var equipmentFolder = project.EquipmentFolder;
            if (equipmentFolder == null)
                return Fail<CreateWorksheetsResult>("所选项目下未找到设备目录。");

            var reservedNames = new List<string>();
            foreach (object raw in favorite.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && child.Kind == AucObjectKind.aucObjSheet)
                    reservedNames.Add(child.Name);
            }

            foreach (var item in request.Worksheets.OrderBy(x => x.SheetIndex))
            {
                var requestedName = string.IsNullOrWhiteSpace(item.RequestedName) ? item.OriginalName : item.RequestedName;
                var finalName = ResolveWorksheetName(requestedName, reservedNames);
                var record = new WorksheetOperationRecord
                {
                    SheetIndex = item.SheetIndex,
                    OriginalName = item.OriginalName,
                    FinalName = finalName,
                    SavePath = targetPath + " / " + finalName,
                    ObjectType = "器件",
                    ColumnCount = item.Columns == null ? 0 : item.Columns.Count,
                    ColumnSummary = item.Columns == null ? "" : string.Join("；", item.Columns.OrderBy(x => x.Position).Select(x => x.Label + " (AID=" + x.AttributeId + ")")),
                    LabelStatus = "未写入 EB（EB 显示默认属性名称）",
                    AutoWidthStatus = "待设置"
                };
                Worksheet worksheet = null;
                try
                {
                    if (string.IsNullOrWhiteSpace(finalName))
                        throw new InvalidOperationException("工作表名称不能为空。");
                    if (item.Columns == null || item.Columns.Count == 0)
                        throw new InvalidOperationException("工作表没有可创建的列。");

                    worksheet = equipmentFolder.OpenWorksheetDirect(
                        AucObjectKind.aucObjDevice,
                        AucAttribute.aucAttrUnspecified,
                        AucVbFindCondition.aucCondEqual,
                        "");
                    foreach (var column in item.Columns.OrderBy(x => x.Position))
                    {
                        if (column.AttributeId <= 0)
                            throw new InvalidOperationException("属性 ID 必须是正整数。");
                        var worksheetAttribute = worksheet.Attributes.Add((AucAttribute)column.AttributeId, column.Position);
                        worksheetAttribute.Width = column.Width;
                    }
                    worksheet.ProtectColumnWidth = true;
                    record.AutoWidthStatus = "已按 Excel 标签计算并设置";
                    worksheet.SaveConfiguration(finalName, favorite);

                    var readback = FindUniqueDirectChildByName(favorite.Children as IEnumerable, finalName);
                    if (readback == null)
                        throw new InvalidOperationException("保存后无法在工作表收藏夹中读回配置。");

                    reservedNames.Add(finalName);
                    record.Status = "created";
                    record.Message = "工作表已创建并读回确认；Excel 列标签仅用于预览、列宽计算和日志。";
                }
                catch (Exception ex)
                {
                    result.Status = result.Records.Any(x => x.Status == "created") ? "partial" : "failed";
                    record.Status = "failed";
                    record.Message = Describe(ex);
                }
                finally
                {
                    if (worksheet != null)
                    {
                        try { worksheet.Close(); }
                        catch (Exception ex)
                        {
                            record.Message = string.IsNullOrWhiteSpace(record.Message)
                                ? "关闭工作表失败：" + Describe(ex)
                                : record.Message + "；关闭工作表失败：" + Describe(ex);
                        }
                    }
                }
                result.Records.Add(record);
            }

            if (result.Records.Count == 0)
                result.Status = "failed";
            else if (result.Records.All(x => x.Status == "created"))
                result.Status = "completed";
            else if (result.Records.Any(x => x.Status == "created"))
                result.Status = "partial";
            else
                result.Status = "failed";

            return new AdapterResponse<CreateWorksheetsResult>
            {
                Success = result.Status != "failed",
                Message = result.Status == "completed" ? "工作表创建完成。" : result.Status == "partial" ? "部分工作表创建完成。" : "工作表创建失败。",
                Data = result
            };
        }

        private static string ResolveWorksheetName(string baseName, IEnumerable<string> reservedNames)
        {
            var trimmed = (baseName ?? "").Trim();
            var reserved = new HashSet<string>(
                reservedNames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);
            if (!reserved.Contains(trimmed)) return trimmed;
            for (var suffix = 2; ; suffix++)
            {
                var candidate = trimmed + " (" + suffix + ")";
                if (!reserved.Contains(candidate)) return candidate;
            }
        }

        private static void CollectAttributeDefinitionIds(IEnumerable children, HashSet<int> requested, HashSet<int> existing)
        {
            foreach (object raw in children)
            {
                var definition = raw as AttributeDefinition;
                var attr = raw as Aucotec.Attribute;
                var child = raw as ObjectItem;
                var id = definition != null ? (int)definition.ID : attr != null ? (int)attr.ID : 0;
                if (requested.Contains(id)) existing.Add(id);
                if (child != null && child.Kind == AucObjectKind.aucObjFolderForUserAttributes)
                    CollectAttributeDefinitionIds(child.Children as IEnumerable, requested, existing);
            }
        }

        private static AdapterResponse<ApplyTypeDefinitionDialogsResult> ApplyTypeDefinitionDialogs(EbApplication app, ApplyTypeDefinitionDialogsRequest request)
        {
            var result = new ApplyTypeDefinitionDialogsResult { Status = "completed" };
            if (request == null || request.TypeItemIds == null || request.Definitions == null)
                return Fail<ApplyTypeDefinitionDialogsResult>("请求内容无效。");
            var ids = request.TypeItemIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (var typeIndex = 0; typeIndex < ids.Count; typeIndex++)
            {
                var item = app.Utils.GetSnglObjectByID(ids[typeIndex]) as TypeItem;
                if (item == null)
                {
                    result.Status = "failed";
                    result.Records.Add(new TypeDefinitionOperationRecord { TypeItemId = ids[typeIndex], Status = "failed", Message = "无法解析 TypeItem。" });
                    result.UnprocessedTypeItemIds.AddRange(ids.Skip(typeIndex + 1));
                    return new AdapterResponse<ApplyTypeDefinitionDialogsResult> { Success = false, Message = "类型定义批量操作失败。", Data = result };
                }
                for (var definitionIndex = 0; definitionIndex < request.Definitions.Count; definitionIndex++)
                {
                    var definition = request.Definitions[definitionIndex];
                    if (ContainsAttribute(item.Attributes, definition.AttributeId, null))
                    {
                        result.Records.Add(Record(item, definition, "skipped_existing", "属性已存在，已跳过。"));
                        continue;
                    }
                    try
                    {
                        item.Attributes.Add((AucAttribute)definition.AttributeId, Type.Missing, definition.TabName);
                        var readback = app.Utils.GetSnglObjectByID(item.ID) as TypeItem;
                        if (readback == null || !ContainsAttribute(readback.Attributes, definition.AttributeId, null))
                            throw new InvalidOperationException("添加后无法读回属性。");
                        result.Records.Add(Record(item, definition, "added", "属性已添加到定义对话框。"));
                    }
                    catch (Exception ex)
                    {
                        var readback = ResolveTypeItem(app, item.ID);
                        if (ContainsAttributeSafe(readback, definition.AttributeId))
                        {
                            result.Records.Add(Record(item, definition, "added", "属性已添加并读回确认；添加后的附加检查曾返回：" + Describe(ex)));
                            continue;
                        }
                        result.Status = "failed";
                        result.Records.Add(Record(item, definition, "failed", Describe(ex)));
                        result.UnprocessedOperations.AddRange(request.Definitions.Skip(definitionIndex + 1)
                            .Select(x => item.Name + " | AID=" + x.AttributeId + " | tab=" + x.TabName));
                        result.UnprocessedTypeItemIds.AddRange(ids.Skip(typeIndex + 1));
                        return new AdapterResponse<ApplyTypeDefinitionDialogsResult> { Success = false, Message = "类型定义批量操作失败，已停止后续操作。", Data = result };
                    }
                }
            }
            return Ok(result, "类型定义批量操作完成。");
        }

        private static TypeDefinitionOperationRecord Record(TypeItem item, DialogDefinitionItem definition, string status, string message)
        {
            return new TypeDefinitionOperationRecord
            {
                TypeItemId = item.ID, TypeItemName = item.Name, AttributeId = definition.AttributeId,
                TabName = definition.TabName, Status = status, Message = message
            };
        }

        private static bool ContainsAttribute(Attributes attributes, int aid, string name)
        {
            if (attributes == null) return false;
            foreach (object raw in attributes as IEnumerable)
            {
                var attr = raw as Aucotec.Attribute;
                if (attr == null) continue;
                if ((int)attr.ID == aid || (!string.IsNullOrWhiteSpace(name) && string.Equals(attr.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static bool ContainsAttributeSafe(TypeItem item, int aid)
        {
            try { return item != null && ContainsAttribute(item.Attributes, aid, null); }
            catch { return false; }
        }

        private static string ReadAttributeValue(ObjectItem item, int aid)
        {
            try
            {
                foreach (object raw in item.Attributes as IEnumerable)
                {
                    var attr = raw as Aucotec.Attribute;
                    if (attr != null && (int)attr.ID == aid) return Convert.ToString(attr.Value);
                }
            }
            catch { }
            return "";
        }

        private static bool IsGraphicTemplateItem(ObjectItem item)
        {
            return !string.IsNullOrWhiteSpace(ReadAttributeValue(item, 45));
        }

        private static bool HasGraphicTemplateDirectoryChildren(ObjectItem item)
        {
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && !IsGraphicTemplateItem(child)) return true;
            }
            return false;
        }

        private static void ApplyGraphicTemplateMigrationSummary(MoveGraphicTemplatesResult result)
        {
            result.TotalCount = result.Records.Count;
            result.MovedCount = result.Records.Count(record => record.Status == "moved" || record.Status == "copied");
            result.FailedCount = result.TotalCount - result.MovedCount;
            result.Status = result.FailedCount == 0 ? "completed" : result.MovedCount == 0 ? "failed" : "partial_failed";
        }

        private static void ApplyGraphicTemplateCreationSummary(CreateGraphicTemplatesResult result)
        {
            var expectedCreated = Math.Max(0, result.RequestedTotalCount - result.OriginalCount);
            result.Status = expectedCreated > 0 && result.CreatedCount >= expectedCreated
                ? "completed"
                : result.CreatedCount > 0 ? "partial" : "failed";
        }

        private static void ApplyNamedGraphicTemplateCreationSummary(CreateNamedGraphicTemplatesResult result, int requestedCount)
        {
            var created = result.Records.Count(record => record.Status == "created");
            result.Status = created == requestedCount
                ? "completed"
                : created > 0 ? "partial" : "failed";
        }

        private static AdapterResponse<ToolPanelConfigurationIdentity> GetToolPanelConfigurationIdentity(EbApplication app)
        {
            var root = FindToolPanelConfigurationRoot(app);
            if (root == null) return Fail<ToolPanelConfigurationIdentity>("无法读取“模板 / 工具面板配置”目录。");
            return Ok(new ToolPanelConfigurationIdentity
            {
                Version = Version,
                RootId = root.ID,
                RootName = root.Name
            }, "工具面板配置身份读取成功。");
        }

        private static AdapterResponse<ToolPanelConfigurationTreeResult> GetToolPanelConfigurationTree(EbApplication app)
        {
            var root = FindToolPanelConfigurationRoot(app);
            if (root == null) return Fail<ToolPanelConfigurationTreeResult>("无法读取“模板 / 工具面板配置”目录。");
            var database = TryOpenEbDatabaseConnection(app);

            try
            {
                var result = new ToolPanelConfigurationTreeResult
                {
                    Identity = new ToolPanelConfigurationIdentity
                    {
                        Version = Version,
                        RootId = root.ID,
                        RootName = root.Name
                    }
                };
                foreach (object raw in root.Children as IEnumerable)
                {
                    var child = raw as ObjectItem;
                    if (child == null || !IsToolPanelNode(child) || !ExistsInToolPanelDatabase(database, child)) continue;
                    result.Nodes.Add(ReadToolPanelDirectory(child, "数据库 / 模板 / " + root.Name, database));
                }
                return Ok(result, "工具面板配置目录读取成功。");
            }
            finally
            {
                if (database != null) database.Dispose();
            }
        }

        private static ToolPanelDirectoryNode ReadToolPanelDirectory(ObjectItem item, string parentPath, SqlConnection database)
        {
            try { item.Refresh(); } catch { }
            var path = parentPath + " / " + item.Name;
            var node = new ToolPanelDirectoryNode
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = path,
                Kind = item.Kind.ToString(),
                TypeName = Safe(delegate { return item.TypeName; }, "")
            };
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null || !IsToolPanelNode(child) || !ExistsInToolPanelDatabase(database, child)) continue;
                node.Children.Add(ReadToolPanelDirectory(child, path, database));
            }
            return node;
        }

        private static AdapterResponse<ToolPanelDirectoryNode> GetToolPanelConfigurationDirectory(
            EbApplication app,
            ToolPanelDirectoryRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DirectoryId))
                return Fail<ToolPanelDirectoryNode>("工具面板配置目录 ID 不能为空。");

            var root = FindToolPanelConfigurationRoot(app);
            if (root == null) return Fail<ToolPanelDirectoryNode>("无法读取“模板 / 工具面板配置”目录。");
            var directory = string.Equals(request.DirectoryId, root.ID, StringComparison.OrdinalIgnoreCase)
                ? root
                : app.Utils.GetSnglObjectByID(request.DirectoryId) as ObjectItem;
            if (directory == null || !IsDescendantOrSelf(root, directory))
                return Fail<ToolPanelDirectoryNode>("所选工具面板配置目录已不存在，请重新打开窗口后重试。");
            if (!IsToolPanelNode(directory))
                return Fail<ToolPanelDirectoryNode>("所选对象不是工具面板配置节点，请重新打开窗口后重试。");

            var path = BuildObjectPath(root, directory, "数据库 / 模板 / " + root.Name);
            var database = TryOpenEbDatabaseConnection(app);
            try
            {
                return Ok(ReadToolPanelDirectoryShallow(directory, ParentPath(path), database), "工具面板配置目录读取成功。");
            }
            finally
            {
                if (database != null) database.Dispose();
            }
        }

        private static ToolPanelDirectoryNode ReadToolPanelDirectoryShallow(ObjectItem item, string parentPath, SqlConnection database)
        {
            try { item.Refresh(); } catch { }
            var path = string.IsNullOrWhiteSpace(parentPath) ? item.Name : parentPath + " / " + item.Name;
            var node = new ToolPanelDirectoryNode
            {
                Id = item.ID,
                Name = item.Name,
                FullPath = path,
                Kind = item.Kind.ToString(),
                TypeName = Safe(delegate { return item.TypeName; }, "")
            };
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child == null || !IsToolPanelNode(child) || !ExistsInToolPanelDatabase(database, child)) continue;
                node.Children.Add(new ToolPanelDirectoryNode
                {
                    Id = child.ID,
                    Name = child.Name,
                    FullPath = path + " / " + child.Name,
                    Kind = child.Kind.ToString(),
                    TypeName = Safe(delegate { return child.TypeName; }, "")
                });
            }
            return node;
        }

        private static AdapterResponse<AddGraphicTemplatesToToolPanelResult> AddGraphicTemplatesToToolPanel(
            EbApplication app,
            AddGraphicTemplatesToToolPanelRequest request)
        {
            var result = new AddGraphicTemplatesToToolPanelResult();
            if (request == null || string.IsNullOrWhiteSpace(request.TargetDirectoryId) ||
                request.TemplateIds == null || request.TemplateIds.Count == 0)
                return FailWithData("添加到工具面板请求无效。", result);

            var root = FindToolPanelConfigurationRoot(app);
            if (root == null) return FailWithData("无法读取“模板 / 工具面板配置”目录。", result);
            var target = app.Utils.GetSnglObjectByID(request.TargetDirectoryId) as ObjectItem;
            if (target == null || !IsDescendantOrSelf(root, target))
                return FailWithData("所选工具面板目录已不存在，请刷新后重试。", result);
            if (!IsToolPanelEntry(target))
                return FailWithData("所选对象不是工具面板条目，请选择 Kind 415 条目后重试。", result);

            result.TargetDirectoryId = target.ID;
            result.TargetDirectoryPath = BuildObjectPath(root, target, "数据库 / 模板 / " + root.Name);
            foreach (var templateId in request.TemplateIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                result.Records.Add(AddGraphicTemplateToToolPanel(app, target, result.TargetDirectoryPath, templateId));
            }
            result.TotalCount = result.Records.Count;
            result.AddedCount = result.Records.Count(record => record.Status == "added");
            result.FailedCount = result.TotalCount - result.AddedCount;
            result.Status = result.FailedCount == 0 ? "completed" : result.AddedCount == 0 ? "failed" : "partial_failed";
            return result.FailedCount == 0
                ? Ok(result, "模板图形已添加到工具面板。请重启 EB 后查看生效结果。")
                : new AdapterResponse<AddGraphicTemplatesToToolPanelResult>
                {
                    Success = false,
                    Message = "部分或全部模板图形添加失败。",
                    Data = result
                };
        }

        private static ToolPanelAdditionRecord AddGraphicTemplateToToolPanel(
            EbApplication app,
            ObjectItem target,
            string targetPath,
            string templateId)
        {
            var record = new ToolPanelAdditionRecord
            {
                TemplateId = templateId,
                TargetDirectoryId = target.ID,
                TargetDirectoryPath = targetPath
            };
            try
            {
                var template = app.Utils.GetSnglObjectByID(templateId) as ObjectItem;
                if (template == null || !IsGraphicTemplateItem(template))
                    throw new InvalidOperationException("所选对象不是可用的模板图形。");
                record.TemplateName = template.Name;
                if (HasToolPanelEntryReference(app, target.ID, template.ID))
                {
                    record.ConfirmedObjectId = target.ID;
                    record.Status = "added";
                    record.Message = "目标工具面板条目中已存在该模板图形引用，已从数据库按 Role 132 读回确认。";
                    return record;
                }

                try { WriteToolPanelTemplateReference(app, target, template); }
                catch (Exception ex) { throw new InvalidOperationException("创建 Role 132 模板图形引用失败：" + Describe(ex), ex); }

                record.ConfirmedObjectId = target.ID;
                record.Status = "added";
                record.Message = "已将模板图形关联到所选 Kind 415 工具面板条目，并从数据库按 Role 132 读回确认。";
            }
            catch (Exception ex)
            {
                record.Status = "failed";
                record.Message = Describe(ex);
            }
            return record;
        }

        private static void WriteToolPanelTemplateReference(
            EbApplication app,
            ObjectItem entry,
            ObjectItem template)
        {
            var entryOid = ToDatabaseOid(entry.ID);
            var templateOid = ToDatabaseOid(template.ID);
            using (var connection = OpenEbDatabaseConnection(app))
            using (var transaction = connection.BeginTransaction())
            {
                const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.Object WHERE OID=@directory AND CID=414)
    THROW 51000, '所选工具面板条目的父目录不存在或不是 Kind 414', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Object WHERE OID=@entry AND CID=415)
    THROW 51001, '所选对象不存在或不是 Kind 415 工具面板条目', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Object WHERE OID=@template)
    THROW 51002, '模板图形不存在', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Association WHERE OID=@entry AND OIDDest=@template AND Role=132)
    INSERT dbo.Association (OID,OIDDest,OrderNumber,Role,RoleFlag,h_UserID)
    SELECT @entry,@template,COALESCE((SELECT MAX(OrderNumber) + 16 FROM dbo.Association WHERE OID=@entry AND Role=132), 80),132,0,h_UserID
    FROM dbo.Object WHERE OID=@entry;
IF NOT EXISTS (SELECT 1 FROM dbo.Association WHERE OID=@entry AND OIDDest=@template AND Role=132)
    THROW 51003, 'Role 132 引用写入后未读回', 1;";
                using (var command = new SqlCommand(sql, connection, transaction))
                {
                    command.Parameters.AddWithValue("@directory", ToDatabaseOid(entry.Parent.ID));
                    command.Parameters.AddWithValue("@entry", entryOid);
                    command.Parameters.AddWithValue("@template", templateOid);
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
        }

        private static ObjectItem FindToolPanelConfigurationRoot(EbApplication app)
        {
            var templates = app.Folders.Templates as ObjectItem;
            if (templates == null) return null;
            foreach (object raw in templates.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && string.Equals(child.Name, "工具面板配置", StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            return null;
        }

        private static bool HasToolPanelDirectoryChildren(ObjectItem item)
        {
            foreach (object raw in item.Children as IEnumerable)
            {
                var child = raw as ObjectItem;
                if (child != null && IsToolPanelContainer(child)) return true;
            }
            return false;
        }

        private static bool IsToolPanelNode(ObjectItem item)
        {
            var kind = (int)item.Kind;
            return kind == 413 || kind == 414 || kind == 415;
        }

        private static bool IsToolPanelContainer(ObjectItem item)
        {
            var kind = (int)item.Kind;
            return kind == 413 || kind == 414;
        }

        private static bool IsToolPanelEntry(ObjectItem item)
        {
            return (int)item.Kind == 415;
        }

        private static bool ExistsInToolPanelDatabase(SqlConnection connection, ObjectItem item)
        {
            if (connection == null) return true;
            using (var command = new SqlCommand("SELECT 1 FROM dbo.Object WHERE OID=@oid AND CID=@cid", connection))
            {
                command.Parameters.AddWithValue("@oid", ToDatabaseOid(item.ID));
                command.Parameters.AddWithValue("@cid", (int)item.Kind);
                var value = command.ExecuteScalar();
                return value != null && value != DBNull.Value;
            }
        }

        private static bool HasToolPanelEntryReference(EbApplication app, string entryId, string templateId)
        {
            using (var connection = OpenEbDatabaseConnection(app))
            using (var command = new SqlCommand(@"
SELECT TOP 1 1
FROM dbo.Object o
JOIN dbo.Association a ON a.OID=o.OID AND a.OIDDest=@template AND a.Role=132
WHERE o.OID=@entry AND o.CID=415;", connection))
            {
                command.Parameters.AddWithValue("@entry", ToDatabaseOid(entryId));
                command.Parameters.AddWithValue("@template", ToDatabaseOid(templateId));
                var value = command.ExecuteScalar();
                return value != null && value != DBNull.Value;
            }
        }

        private static SqlConnection OpenEbDatabaseConnection(EbApplication app)
        {
            var server = Safe(delegate { return app.DatabaseServer; }, "");
            var instance = Safe(delegate { return app.DatabaseInstance; }, "");
            var database = Safe(delegate { return app.Database; }, "");
            if (string.IsNullOrWhiteSpace(server)) server = ".";
            if (string.IsNullOrWhiteSpace(database))
                throw new InvalidOperationException("当前 EB 未提供数据库名称，无法写入工具面板内部引用。");
            var dataSource = string.IsNullOrWhiteSpace(instance)
                ? server
                : instance.StartsWith(@".\", StringComparison.OrdinalIgnoreCase) ||
                  instance.IndexOf(@"\", StringComparison.OrdinalIgnoreCase) >= 0
                    ? instance
                    : server + @"\" + instance;
            var connection = new SqlConnection(new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                InitialCatalog = database,
                IntegratedSecurity = true,
                ConnectTimeout = 10,
                ApplicationName = "EBAssistant"
            }.ConnectionString);
            connection.Open();
            return connection;
        }

        private static SqlConnection TryOpenEbDatabaseConnection(EbApplication app)
        {
            try { return OpenEbDatabaseConnection(app); }
            catch { return null; }
        }

        private static long ToDatabaseOid(string objectId)
        {
            var separator = objectId == null ? -1 : objectId.LastIndexOf('-');
            var value = separator < 0 ? objectId : objectId.Substring(separator + 1);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("EB 对象 ID 无法转换为数据库 OID。");
            return long.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static bool IsDescendantOrSelf(ObjectItem root, ObjectItem item)
        {
            var current = item;
            while (current != null)
            {
                if (string.Equals(current.ID, root.ID, StringComparison.OrdinalIgnoreCase)) return true;
                current = current.Parent;
            }
            return false;
        }

        private static string BuildObjectPath(ObjectItem root, ObjectItem item, string rootPath)
        {
            var names = new List<string>();
            var current = item;
            while (current != null && !string.Equals(current.ID, root.ID, StringComparison.OrdinalIgnoreCase))
            {
                names.Add(current.Name);
                current = current.Parent;
            }
            names.Reverse();
            return names.Count == 0 ? rootPath : rootPath + " / " + string.Join(" / ", names);
        }

        private static bool IsFolderKind(AucObjectKind kind)
        {
            var name = kind.ToString();
            return name.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<ObjectItem> FindDirectChildrenByName(IEnumerable children, string name)
        {
            var result = new List<ObjectItem>();
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child != null && string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase)) result.Add(child);
            }
            return result;
        }

        private static List<ObjectItem> FindDirectChildrenByKind(IEnumerable children, AucObjectKind kind)
        {
            var result = new List<ObjectItem>();
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child != null && child.Kind == kind) result.Add(child);
            }
            return result;
        }

        private static bool TryResolveWorksheetTarget(
            EbApplication app,
            string templateProjectId,
            out Project template,
            out ObjectItem favorite,
            out string templatePath,
            out string targetPath,
            out string error)
        {
            template = app.Utils.GetSnglObjectByID(templateProjectId) as Project;
            favorite = null;
            templatePath = "";
            targetPath = "";
            error = "";
            if (template == null)
            {
                error = "无法解析模板项目。";
                return false;
            }

            if (!TryResolveWorksheetFavorite(template, out favorite, out error)) return false;
            var worksheetsFolder = template.WorksheetTemplatesFolder;
            templatePath = FindProjectTemplatePath(app.Folders.ProjectTemplates.Children, template.ID, app.Folders.ProjectTemplates.Name);
            if (string.IsNullOrWhiteSpace(templatePath)) templatePath = app.Folders.ProjectTemplates.Name + " / " + template.Name;
            targetPath = templatePath + " / " + worksheetsFolder.Name + " / " + favorite.Name;
            return true;
        }

        private static bool TryResolveWorksheetFavorite(Project project, out ObjectItem favorite, out string error)
        {
            favorite = null;
            error = "";
            var worksheetsFolder = project.WorksheetTemplatesFolder;
            if (worksheetsFolder == null)
            {
                error = "项目下未找到 /工作表。";
                return false;
            }
            var favorites = FindDirectChildrenByKind(worksheetsFolder.Children as IEnumerable, AucObjectKind.aucObjFavoriteListConfigurations);
            if (favorites.Count != 1)
            {
                error = "项目下未找到唯一的 /工作表/收藏夹。工作表目录直接子项：" + DescribeDirectChildren(worksheetsFolder.Children as IEnumerable);
                return false;
            }
            favorite = favorites[0];
            return true;
        }

        private static Project FindInteractiveProject(EbApplication app, string excludedProjectId)
        {
            try
            {
                var active = app.ActiveProject;
                if (active != null && !string.Equals(active.ID, excludedProjectId, StringComparison.OrdinalIgnoreCase))
                    return active;
            }
            catch { }
            foreach (object raw in app.Folders.Projects.Children as IEnumerable)
            {
                var project = raw as Project;
                if (project != null && !string.Equals(project.ID, excludedProjectId, StringComparison.OrdinalIgnoreCase))
                    return project;
            }
            return null;
        }

        private static int WorksheetWidth(string label)
        {
            var visualUnits = 0;
            foreach (var character in label ?? "")
                visualUnits += character <= 0x7f ? 1 : 2;
            return Math.Max(80, Math.Min(600, visualUnits * 9 + 24));
        }

        private static int FindEbProcessId()
        {
            foreach (var process in Process.GetProcessesByName("EngineeringBase"))
            {
                try
                {
                    var path = process.MainModule == null ? "" : process.MainModule.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && path.IndexOf(ExpectedInstallFolder, StringComparison.OrdinalIgnoreCase) >= 0)
                        return process.Id;
                }
                catch { }
            }
            return 0;
        }

        private static void SetWorksheetColumnLabel(
            EbApplication app,
            ObjectItem sourceObject,
            int processId,
            int position,
            int attributeId,
            int[] widths,
            string label,
            List<string> checks)
        {
            var mainWindow = Process.GetProcessById(processId).MainWindowHandle;
            if (mainWindow == IntPtr.Zero) throw new InvalidOperationException("EB 主窗口句柄不可用。");
            SetForegroundWindow(mainWindow);
            Thread.Sleep(250);

            var header = FindWorksheetHeader(processId, position);
            if (header != null)
                SelectAutomationElement(header);
            else
                SelectStingGridColumnHeader(app, processId, position, attributeId, widths);
            checks.Add("已定位并选中第 " + (position + 1) + " 列标题。");

            Exception automationError = null;
            var completed = false;
            var worker = new Thread(delegate()
            {
                try { completed = HandleEditColumnLabelDialog(processId, label, 10000); }
                catch (Exception ex) { automationError = ex; }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();

            ((IAucVbaInternUtils)app).ExecuteCommand(AucCommand.aucCmdEditColumnLabel, sourceObject);
            worker.Join(12000);
            if (automationError != null) throw new InvalidOperationException("自动处理“编辑列标签”对话框失败。", automationError);
            if (!completed) throw new InvalidOperationException("未能在超时内自动完成“编辑列标签”对话框。");
            checks.Add("第 " + (position + 1) + " 列标签已通过隐藏交互命令写入。");
        }

        private static AutomationElement FindWorksheetHeader(int processId, int position)
        {
            try
            {
                var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
                var typeCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.HeaderItem);
                var condition = new AndCondition(processCondition, typeCondition);
                var candidates = AutomationElement.RootElement.FindAll(TreeScope.Descendants, condition)
                    .Cast<AutomationElement>()
                    .Where(x => !x.Current.IsOffscreen && x.Current.BoundingRectangle.Width > 0 && x.Current.BoundingRectangle.Height > 0)
                    .ToList();
                var group = candidates
                    .GroupBy(x => Math.Round(x.Current.BoundingRectangle.Top / 4.0) * 4)
                    .OrderByDescending(x => x.Count())
                    .ThenByDescending(x => x.Key)
                    .FirstOrDefault(x => x.Count() > position);
                return group == null ? null : group.OrderBy(x => x.Current.BoundingRectangle.Left).ElementAt(position);
            }
            catch
            {
                return null;
            }
        }

        private static void SelectStingGridColumnHeader(EbApplication app, int processId, int position, int attributeId, int[] widths)
        {
            var mainWindow = Process.GetProcessById(processId).MainWindowHandle;
            var mdiClient = FindChildWindow(mainWindow, "MDIClient", null);
            var activeChild = mdiClient == IntPtr.Zero
                ? IntPtr.Zero
                : SendMessage(mdiClient, 0x0229, IntPtr.Zero, IntPtr.Zero);
            var grid = activeChild == IntPtr.Zero
                ? IntPtr.Zero
                : FindChildWindow(activeChild, "STINGGRIDCAucAxListCtrl", null);
            if (grid == IntPtr.Zero) grid = FindChildWindow(mainWindow, "STINGGRIDCAucAxListCtrl", null);
            var rectangle = new NativeRectangle();
            if (grid == IntPtr.Zero || !GetWindowRect(grid, out rectangle))
                throw new InvalidOperationException("无法定位 EB 工作表网格。");
            var precedingWidth = 0;
            for (var index = 0; index < position; index++) precedingWidth += widths[index];
            var currentWidth = widths[position];
            ClickAt(
                rectangle.Left + 20 + precedingWidth + Math.Max(4, currentWidth / 2),
                rectangle.Top + 12);
            Thread.Sleep(100);
        }

        private static void ClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }

        private static void SelectAutomationElement(AutomationElement element)
        {
            object pattern;
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern))
            {
                ((SelectionItemPattern)pattern).Select();
                element.SetFocus();
                return;
            }
            element.SetFocus();
            var point = element.GetClickablePoint();
            ClickAt((int)point.X, (int)point.Y);
        }

        private static bool HandleEditColumnLabelDialog(int processId, string label, int timeoutMilliseconds)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < end)
            {
                var dialog = FindEditColumnLabelDialog(processId);
                if (dialog != null)
                {
                    var handle = new IntPtr(dialog.Current.NativeWindowHandle);
                    if (handle != IntPtr.Zero) ShowWindow(handle, 0);
                    var edit = dialog.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                    object pattern;
                    if (edit != null && edit.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
                        ((ValuePattern)pattern).SetValue(label);
                    else if (!SetDialogEditText(handle, label))
                        throw new InvalidOperationException("无法定位列标签输入框。");

                    var button = FindConfirmButton(dialog);
                    if (button != null && button.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                        ((InvokePattern)pattern).Invoke();
                    else if (!ClickDialogConfirmButton(handle))
                        throw new InvalidOperationException("无法定位列标签确认按钮。");
                    return true;
                }
                Thread.Sleep(50);
            }
            return false;
        }

        private static AutomationElement FindEditColumnLabelDialog(int processId)
        {
            var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, processCondition);
            foreach (AutomationElement window in windows)
            {
                var name = window.Current.Name ?? "";
                if (name.IndexOf("列标签", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Column Label", StringComparison.OrdinalIgnoreCase) >= 0)
                    return window;
            }
            return null;
        }

        private static AutomationElement FindConfirmButton(AutomationElement dialog)
        {
            var buttons = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement button in buttons)
            {
                var name = (button.Current.Name ?? "").Replace("&", "");
                if (string.Equals(name, "确定", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "OK", StringComparison.OrdinalIgnoreCase))
                    return button;
            }
            return null;
        }

        private static bool SetDialogEditText(IntPtr dialog, string text)
        {
            var edit = FindChildWindow(dialog, "Edit", null);
            return edit != IntPtr.Zero && SendMessage(edit, 0x000C, IntPtr.Zero, text) != IntPtr.Zero;
        }

        private static bool ClickDialogConfirmButton(IntPtr dialog)
        {
            var button = FindChildWindow(dialog, "Button", "确定");
            if (button == IntPtr.Zero) button = FindChildWindow(dialog, "Button", "OK");
            if (button == IntPtr.Zero) return false;
            SendMessage(button, 0x00F5, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        private static void HandleGraphicTemplateMismatchDialog(int processId, Func<bool> shouldStop, int timeoutMilliseconds)
        {
            if (processId <= 0) return;
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (!shouldStop() && DateTime.UtcNow < end)
            {
                var handle = FindGraphicTemplateMismatchDialogHandle(processId);
                if (handle != IntPtr.Zero)
                {
                    if (!ClickDialogConfirmButton(handle))
                        throw new InvalidOperationException("无法定位图形符号类型转换确认按钮。");
                    Thread.Sleep(100);
                    continue;
                }

                var dialog = FindGraphicTemplateMismatchDialog(processId);
                if (dialog != null)
                {
                    object pattern;
                    var button = FindGraphicTemplateConfirmButton(dialog);
                    handle = new IntPtr(dialog.Current.NativeWindowHandle);
                    if (button != null && button.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                        ((InvokePattern)pattern).Invoke();
                    else if (!ClickDialogConfirmButton(handle))
                        throw new InvalidOperationException("无法定位图形符号类型转换确认按钮。");
                    Thread.Sleep(100);
                    continue;
                }
                Thread.Sleep(100);
            }
        }

        private static IntPtr FindGraphicTemplateMismatchDialogHandle(int processId)
        {
            var result = IntPtr.Zero;
            EnumWindows(delegate(IntPtr handle, IntPtr parameter)
            {
                int windowProcessId;
                GetWindowThreadProcessId(handle, out windowProcessId);
                if (windowProcessId != processId || !IsWindowVisible(handle))
                    return true;

                var className = new StringBuilder(128);
                GetClassName(handle, className, className.Capacity);
                if (!string.Equals(className.ToString(), "#32770", StringComparison.OrdinalIgnoreCase))
                    return true;

                var text = CollectWindowText(handle);
                if (IsGraphicTemplateMismatchText(text) && HasDialogConfirmButton(handle))
                {
                    result = handle;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static AutomationElement FindGraphicTemplateMismatchDialog(int processId)
        {
            var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, processCondition);
            foreach (AutomationElement window in windows)
            {
                if (IsEngineeringBaseMainWindow(window)) continue;
                var handle = new IntPtr(window.Current.NativeWindowHandle);
                var text = (window.Current.Name ?? "") + " " + CollectDialogText(window) + " " + CollectWindowText(handle);
                if (IsGraphicTemplateMismatchText(text) &&
                    (FindGraphicTemplateConfirmButton(window) != null || HasDialogConfirmButton(handle)))
                    return window;
            }
            return null;
        }

        private static bool IsGraphicTemplateMismatchText(string text)
        {
            return text != null &&
                text.IndexOf("图形符号类型", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("图形模板类型", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("不匹配", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEngineeringBaseMainWindow(AutomationElement window)
        {
            var handle = new IntPtr(window.Current.NativeWindowHandle);
            if (handle == IntPtr.Zero) return false;
            var className = new StringBuilder(128);
            GetClassName(handle, className, className.Capacity);
            var title = window.Current.Name ?? "";
            return className.ToString().StartsWith("Afx:", StringComparison.OrdinalIgnoreCase) &&
                title.IndexOf("AUCOTEC", StringComparison.OrdinalIgnoreCase) >= 0 &&
                title.IndexOf("Engineering Base", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AutomationElement FindGraphicTemplateConfirmButton(AutomationElement dialog)
        {
            var buttons = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement button in buttons)
            {
                var name = (button.Current.Name ?? "").Replace("&", "");
                if (string.Equals(name, "确定", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "OK", StringComparison.OrdinalIgnoreCase))
                    return button;
            }
            return null;
        }

        private static string CollectDialogText(AutomationElement dialog)
        {
            var parts = new List<string>();
            var texts = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
            foreach (AutomationElement text in texts)
            {
                parts.Add(text.Current.Name ?? "");
            }
            return string.Join(" ", parts.ToArray());
        }

        private static string CollectWindowText(IntPtr window)
        {
            if (window == IntPtr.Zero) return "";
            var parts = new List<string>();
            EnumChildWindows(window, delegate(IntPtr handle, IntPtr parameter)
            {
                var text = new StringBuilder(512);
                GetWindowText(handle, text, text.Capacity);
                if (text.Length > 0) parts.Add(text.ToString());
                return true;
            }, IntPtr.Zero);
            return string.Join(" ", parts.ToArray());
        }

        private static bool HasDialogConfirmButton(IntPtr dialog)
        {
            return dialog != IntPtr.Zero &&
                (FindChildWindow(dialog, "Button", "确定") != IntPtr.Zero ||
                 FindChildWindow(dialog, "Button", "OK") != IntPtr.Zero);
        }

        private static IntPtr FindChildWindow(IntPtr parent, string className, string title)
        {
            var result = IntPtr.Zero;
            EnumChildWindows(parent, delegate(IntPtr handle, IntPtr parameter)
            {
                var classBuilder = new StringBuilder(128);
                GetClassName(handle, classBuilder, classBuilder.Capacity);
                if (!string.Equals(classBuilder.ToString(), className, StringComparison.OrdinalIgnoreCase)) return true;
                if (title != null)
                {
                    var titleBuilder = new StringBuilder(128);
                    GetWindowText(handle, titleBuilder, titleBuilder.Capacity);
                    if (!string.Equals(titleBuilder.ToString().Replace("&", ""), title, StringComparison.OrdinalIgnoreCase)) return true;
                }
                result = handle;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        private static void DoubleClickSelectedExplorerTreeItem(IntPtr mainWindow)
        {
            var rectangle = new NativeRectangle();
            var found = false;
            foreach (var tree in FindChildWindowsByClassPrefix(mainWindow, "ATL:"))
            {
                var accessible = GetAccessibleObject(tree);
                if (accessible != null && TryFindSelectedAccessibleRectangle(accessible, out rectangle, 0))
                {
                    found = true;
                    break;
                }
                if (TryFindSelectionHighlight(tree, out rectangle))
                {
                    found = true;
                    break;
                }
            }
            if (!found && TryFindSelectionHighlight(mainWindow, out rectangle)) found = true;
            if (!found) throw new InvalidOperationException("无法定位 EB 浏览器树中的已选中对象。");
            var x = rectangle.Left + Math.Max(4, (rectangle.Right - rectangle.Left) / 2);
            var y = rectangle.Top + Math.Max(4, (rectangle.Bottom - rectangle.Top) / 2);
            SetCursorPos(x, y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }

        private static Accessibility.IAccessible GetAccessibleObject(IntPtr window)
        {
            object accessible = null;
            var iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
            return AccessibleObjectFromWindow(window, 0xFFFFFFFC, ref iid, ref accessible) == 0
                ? accessible as Accessibility.IAccessible
                : null;
        }

        private static bool TryFindSelectedAccessibleRectangle(Accessibility.IAccessible accessible, out NativeRectangle rectangle, int depth)
        {
            rectangle = new NativeRectangle();
            if (accessible == null || depth > 20) return false;
            for (var index = 0; index <= accessible.accChildCount; index++)
            {
                object childId = index == 0 ? 0 : (object)index;
                try
                {
                    var state = Convert.ToInt32(accessible.get_accState(childId));
                    if ((state & 0x00000006) != 0)
                    {
                        int left;
                        int top;
                        int width;
                        int height;
                        accessible.accLocation(out left, out top, out width, out height, childId);
                        rectangle = new NativeRectangle { Left = left, Top = top, Right = left + width, Bottom = top + height };
                        return width > 0 && height > 0;
                    }
                    var child = index == 0 ? null : accessible.get_accChild(childId) as Accessibility.IAccessible;
                    if (child != null && TryFindSelectedAccessibleRectangle(child, out rectangle, depth + 1)) return true;
                }
                catch { }
            }
            return false;
        }

        private static bool TryFindSelectionHighlight(IntPtr window, out NativeRectangle rectangle)
        {
            rectangle = new NativeRectangle();
            NativeRectangle windowRectangle;
            if (!GetWindowRect(window, out windowRectangle)) return false;
            var width = windowRectangle.Right - windowRectangle.Left;
            var height = windowRectangle.Bottom - windowRectangle.Top;
            if (width <= 0 || height <= 0) return false;

            var screenDc = GetDC(IntPtr.Zero);
            var dc = CreateCompatibleDC(screenDc);
            var bitmap = CreateCompatibleBitmap(screenDc, width, height);
            var previous = SelectObject(dc, bitmap);
            ReleaseDC(IntPtr.Zero, screenDc);
            if (dc == IntPtr.Zero || bitmap == IntPtr.Zero) return false;
            try
            {
                if (!PrintWindow(window, dc, 2)) return false;
                var bestY = -1;
                var bestCount = 0;
                for (var y = 0; y < height; y += 2)
                {
                    var count = 0;
                    for (var x = 0; x < width; x += 2)
                    {
                        if (IsSelectionBlue(GetPixel(dc, x, y))) count++;
                    }
                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestY = y;
                    }
                }
                if (bestY < 0 || bestCount < 20) return false;

                var left = width;
                var right = 0;
                for (var x = 0; x < width; x++)
                {
                    if (!IsSelectionBlue(GetPixel(dc, x, bestY))) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
                if (right <= left) return false;
                rectangle = new NativeRectangle
                {
                    Left = windowRectangle.Left + left,
                    Top = windowRectangle.Top + Math.Max(0, bestY - 8),
                    Right = windowRectangle.Left + right,
                    Bottom = windowRectangle.Top + Math.Min(height, bestY + 8)
                };
                return true;
            }
            finally
            {
                SelectObject(dc, previous);
                DeleteObject(bitmap);
                DeleteDC(dc);
            }
        }

        private static bool IsSelectionBlue(uint color)
        {
            if (color == 0xFFFFFFFF) return false;
            var red = (int)(color & 0xFF);
            var green = (int)((color >> 8) & 0xFF);
            var blue = (int)((color >> 16) & 0xFF);
            return blue > 140 && blue > red + 70 && blue > green + 35 && green > 50;
        }

        private static IntPtr FindChildWindowByClassPrefix(IntPtr parent, string prefix)
        {
            return FindChildWindowsByClassPrefix(parent, prefix).FirstOrDefault();
        }

        private static List<IntPtr> FindChildWindowsByClassPrefix(IntPtr parent, string prefix)
        {
            var result = new List<IntPtr>();
            EnumChildWindows(parent, delegate(IntPtr handle, IntPtr parameter)
            {
                var classBuilder = new StringBuilder(128);
                GetClassName(handle, classBuilder, classBuilder.Capacity);
                if (classBuilder.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && IsWindowVisible(handle))
                {
                    result.Add(handle);
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static ObjectItem FindUniqueDirectChildByName(IEnumerable children, string name)
        {
            var matches = FindDirectChildrenByName(children, name);
            return matches.Count == 1 ? matches[0] : null;
        }

        private static ObjectItem FindUniqueDirectChildByNameSafe(ObjectItem parent, string name)
        {
            try { return parent == null ? null : FindUniqueDirectChildByName(parent.Children as IEnumerable, name); }
            catch { return null; }
        }

        private static string DescribeDirectChildren(IEnumerable children)
        {
            var result = new List<string>();
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child != null) result.Add(child.Name + " (" + child.Kind + ")");
            }
            return result.Count == 0 ? "<empty>" : string.Join(", ", result.ToArray());
        }

        private static string FindProjectTemplatePath(IEnumerable children, string id, string parentPath)
        {
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child == null) continue;
                var path = parentPath + " / " + child.Name;
                if (string.Equals(child.ID, id, StringComparison.OrdinalIgnoreCase)) return path;
                if (child.Kind == AucObjectKind.aucObjProject) continue;
                var nested = FindProjectTemplatePath(child.Children as IEnumerable, id, path);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
            return null;
        }

        private static string BuildGraphicTemplateDirectoryPath(ObjectItem root, ObjectItem directory)
        {
            var names = new List<string>();
            var current = directory;
            while (current != null)
            {
                names.Add(current.Name);
                if (string.Equals(current.ID, root.ID, StringComparison.OrdinalIgnoreCase)) break;
                current = current.Parent as ObjectItem;
            }
            names.Reverse();
            if (names.Count == 0 || !string.Equals(names[0], root.Name, StringComparison.OrdinalIgnoreCase))
                names.Insert(0, root.Name);
            return string.Join(" / ", names.ToArray());
        }

        private static string ParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            const string marker = " / ";
            var index = path.LastIndexOf(marker, StringComparison.Ordinal);
            return index < 0 ? "" : path.Substring(0, index);
        }

        private static ObjectItem FindFolder(IEnumerable children, string id)
        {
            foreach (object raw in children)
            {
                var child = raw as ObjectItem;
                if (child == null || child.Kind != AucObjectKind.aucObjFolderForUserAttributes) continue;
                if (string.Equals(child.ID, id, StringComparison.OrdinalIgnoreCase)) return child;
                var nested = FindFolder(child.Children as IEnumerable, id);
                if (nested != null) return nested;
            }
            return null;
        }

        private static AucAttributeType ParseType(string value)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "string": return AucAttributeType.aucAttributeTypeString;
                case "date": return AucAttributeType.aucAttributeTypeDate;
                case "time": return AucAttributeType.aucAttributeTypeTime;
                case "datetime": return AucAttributeType.aucAttributeTypeDateTime;
                case "boolean": return AucAttributeType.aucAttributeTypeBoolean;
                case "number": return AucAttributeType.aucAttributeTypeNumber;
                case "float": return AucAttributeType.aucAttributeTypeFloat;
                case "formula":
#if EB30
                    throw new InvalidOperationException("EB 2023 COM 30 不支持公式属性类型。");
#else
                    return AucAttributeType.aucAttributeTypeFormula;
#endif
                default: throw new InvalidOperationException("不支持的属性类型：" + value);
            }
        }

        private static T Read<T>()
        {
            var text = Console.In.ReadToEnd();
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        private static int Write<T>(AdapterResponse<T> response)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(AdapterResponse<T>)).WriteObject(stream, response);
                Console.Write(Encoding.UTF8.GetString(stream.ToArray()));
            }
            return response.Success ? 0 : 1;
        }

        private static AdapterResponse<T> Ok<T>(T data, string message) { return new AdapterResponse<T> { Success = true, Message = message, Data = data }; }
        private static AdapterResponse<T> Fail<T>(string message) { return new AdapterResponse<T> { Success = false, Message = message }; }
        private static AdapterResponse<T> FailWithData<T>(string message, T data) { return new AdapterResponse<T> { Success = false, Message = message, Data = data }; }
        private static string Safe(Func<string> read, string fallback) { try { return read(); } catch { return fallback; } }
        private static string Describe(Exception ex) { return " " + ex.GetType().Name + "：" + ex.Message; }

        private delegate bool EnumChildProc(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumChildProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRectangle rectangle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr deviceContext, int x, int y);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr deviceContext);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(
            IntPtr window,
            uint objectId,
            ref Guid interfaceId,
            [In, Out, MarshalAs(UnmanagedType.Interface)] ref object accessible);
    }

    [DataContract] internal sealed class AdapterResponse<T> { [DataMember] public bool Success; [DataMember] public string Message; [DataMember] public T Data; }
    [DataContract] internal sealed class ConnectionInfo { [DataMember] public string Version; [DataMember] public string ApplicationName; [DataMember] public string DatabaseServer; [DataMember] public string DatabaseInstance; [DataMember] public string Database; [DataMember] public bool IsActive; }
    [DataContract] internal sealed class AttributeFolderNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public List<AttributeFolderNode> Children = new List<AttributeFolderNode>(); }
    [DataContract] internal sealed class ExistingAttribute { [DataMember] public string Name; }
    [DataContract] internal sealed class AttributeFolderIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; }
    [DataContract] internal sealed class FolderTreeResult { [DataMember] public List<AttributeFolderNode> Folders = new List<AttributeFolderNode>(); [DataMember] public List<ExistingAttribute> ExistingAttributes = new List<ExistingAttribute>(); }
    [DataContract] internal sealed class CreateAttributesRequest { [DataMember] public string TargetFolderId; [DataMember] public List<CreateAttributeItem> Attributes; }
    [DataContract] internal sealed class CreateAttributeItem { [DataMember] public int RowNumber; [DataMember] public string Name; [DataMember] public string Type; [DataMember] public int Digits; }
    internal sealed class CreatedAttribute { public ObjectItem Object; public CreateAttributeOperationRecord Record; public bool Completed; }
    [DataContract] internal sealed class CreateAttributeOperationRecord { [DataMember] public int RowNumber; [DataMember] public string Name; [DataMember] public string Type; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class CreateAttributesResult { [DataMember] public string Status; [DataMember] public string Message; [DataMember] public string TargetFolder; [DataMember] public int CreatedCount; [DataMember] public bool RolledBack; [DataMember] public List<string> CreatedNames = new List<string>(); [DataMember] public List<string> RollbackErrors = new List<string>(); [DataMember] public List<CreateAttributeOperationRecord> Records = new List<CreateAttributeOperationRecord>(); }
    [DataContract] internal sealed class CreateFolderRequest { [DataMember] public string ParentFolderId; [DataMember] public string Name; }
    [DataContract] internal sealed class CreateFolderResult { [DataMember] public string Id; [DataMember] public string Name; }
    [DataContract] internal sealed class DeleteFolderRequest { [DataMember] public string Id; }
    [DataContract] internal sealed class TypeDefinitionIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; }
    [DataContract] internal sealed class TypeDefinitionNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public string NodeType; [DataMember] public bool IsActionable; [DataMember] public List<TypeDefinitionNode> Children = new List<TypeDefinitionNode>(); }
    [DataContract] internal sealed class TypeDefinitionTreeResult { [DataMember] public TypeDefinitionIdentity Identity; [DataMember] public List<TypeDefinitionNode> Nodes = new List<TypeDefinitionNode>(); }
    [DataContract] internal sealed class ProjectTemplateIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; }
    [DataContract] internal sealed class ProjectTemplateNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public bool IsTemplateProject; [DataMember] public List<ProjectTemplateNode> Children = new List<ProjectTemplateNode>(); }
    [DataContract] internal sealed class ProjectTemplateTreeResult { [DataMember] public ProjectTemplateIdentity Identity; [DataMember] public List<ProjectTemplateNode> Nodes = new List<ProjectTemplateNode>(); }
    [DataContract] internal sealed class GraphicTemplateIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; }
    [DataContract] internal sealed class GraphicTemplateItem { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public string ParentDirectoryId; [DataMember] public string Kind; [DataMember] public string TypeName; [DataMember] public string SymbolSyncDesignation; [DataMember] public string MasterUniRef; }
    [DataContract] internal sealed class GraphicTemplateDirectoryNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public string Kind; [DataMember] public string TypeName; [DataMember] public List<GraphicTemplateDirectoryNode> Children = new List<GraphicTemplateDirectoryNode>(); [DataMember] public List<GraphicTemplateItem> Templates = new List<GraphicTemplateItem>(); }
    [DataContract] internal sealed class GraphicTemplateTreeResult { [DataMember] public GraphicTemplateIdentity Identity; [DataMember] public List<GraphicTemplateDirectoryNode> Nodes = new List<GraphicTemplateDirectoryNode>(); }
    [DataContract] internal sealed class GraphicTemplateDirectoryRequest { [DataMember] public string DirectoryId; }
    [DataContract] internal sealed class MoveGraphicTemplatesRequest { [DataMember] public string TargetDirectoryId; [DataMember] public List<string> TemplateIds = new List<string>(); }
    [DataContract] internal sealed class GraphicTemplateMigrationRecord { [DataMember] public string TemplateId; [DataMember] public string ConfirmedTemplateId; [DataMember] public string TemplateName; [DataMember] public string SourceDirectoryId; [DataMember] public string SourceDirectoryPath; [DataMember] public string TargetDirectoryId; [DataMember] public string TargetDirectoryPath; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class MoveGraphicTemplatesResult { [DataMember] public string Status; [DataMember] public string TargetDirectoryId; [DataMember] public string TargetDirectoryPath; [DataMember] public int TotalCount; [DataMember] public int MovedCount; [DataMember] public int FailedCount; [DataMember] public List<GraphicTemplateMigrationRecord> Records = new List<GraphicTemplateMigrationRecord>(); }
    [DataContract] internal sealed class CreateGraphicTemplatesRequest { [DataMember] public string DirectoryId; [DataMember] public string SourceTemplateId; [DataMember] public int RequestedTotalCount; }
    [DataContract] internal sealed class GraphicTemplateCreationRecord { [DataMember] public int CopyNumber; [DataMember] public string ConfirmedTemplateId; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class CreateGraphicTemplatesResult { [DataMember] public string Status; [DataMember] public string DirectoryId; [DataMember] public string DirectoryPath; [DataMember] public string SourceTemplateId; [DataMember] public string SourceTemplateName; [DataMember] public int RequestedTotalCount; [DataMember] public int OriginalCount; [DataMember] public int CreatedCount; [DataMember] public int ConfirmedFinalCount; [DataMember] public List<GraphicTemplateCreationRecord> Records = new List<GraphicTemplateCreationRecord>(); }
    [DataContract] internal sealed class CreateNamedGraphicTemplatesRequest { [DataMember] public string DirectoryId; [DataMember] public string SourceTemplateId; [DataMember] public List<string> TargetNames = new List<string>(); }
    [DataContract] internal sealed class NamedGraphicTemplateCreationRecord { [DataMember] public string RequestedName; [DataMember] public string ConfirmedTemplateId; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class CreateNamedGraphicTemplatesResult { [DataMember] public string Status; [DataMember] public string DirectoryId; [DataMember] public string DirectoryPath; [DataMember] public string SourceTemplateId; [DataMember] public string SourceTemplateName; [DataMember] public List<NamedGraphicTemplateCreationRecord> Records = new List<NamedGraphicTemplateCreationRecord>(); }
    [DataContract] internal sealed class OpenGraphicTemplateWithVisioRequest { [DataMember] public string TemplateId; }
    [DataContract] internal sealed class OpenGraphicTemplateWithVisioResult { [DataMember] public string TemplateId; [DataMember] public string TemplateName; [DataMember] public string OpenMethod; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class ToolPanelConfigurationIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; }
    [DataContract] internal sealed class ToolPanelDirectoryNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public string Kind; [DataMember] public string TypeName; [DataMember] public List<ToolPanelDirectoryNode> Children = new List<ToolPanelDirectoryNode>(); }
    [DataContract] internal sealed class ToolPanelConfigurationTreeResult { [DataMember] public ToolPanelConfigurationIdentity Identity; [DataMember] public List<ToolPanelDirectoryNode> Nodes = new List<ToolPanelDirectoryNode>(); }
    [DataContract] internal sealed class ToolPanelDirectoryRequest { [DataMember] public string DirectoryId; }
    [DataContract] internal sealed class AddGraphicTemplatesToToolPanelRequest { [DataMember] public string TargetDirectoryId; [DataMember] public List<string> TemplateIds = new List<string>(); }
    [DataContract] internal sealed class ToolPanelAdditionRecord { [DataMember] public string TemplateId; [DataMember] public string TemplateName; [DataMember] public string TargetDirectoryId; [DataMember] public string TargetDirectoryPath; [DataMember] public string ConfirmedObjectId; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class AddGraphicTemplatesToToolPanelResult { [DataMember] public string Status; [DataMember] public string TargetDirectoryId; [DataMember] public string TargetDirectoryPath; [DataMember] public int TotalCount; [DataMember] public int AddedCount; [DataMember] public int FailedCount; [DataMember] public List<ToolPanelAdditionRecord> Records = new List<ToolPanelAdditionRecord>(); }
    [DataContract] internal sealed class ValidateAttributeIdsRequest { [DataMember] public List<int> AttributeIds; }
    [DataContract] internal sealed class ValidateAttributeIdsResult { [DataMember] public List<int> ExistingIds; [DataMember] public List<int> MissingIds; }
    [DataContract] internal sealed class ValidateWorksheetAttributeIdsRequest { [DataMember] public List<int> AttributeIds = new List<int>(); }
    [DataContract] internal sealed class ValidateWorksheetAttributeIdsResult { [DataMember] public List<int> ExistingIds = new List<int>(); [DataMember] public List<int> MissingIds = new List<int>(); }
    [DataContract] internal sealed class WorksheetCreationContextRequest { [DataMember] public string TemplateProjectId; }
    [DataContract] internal sealed class WorksheetCreationContextResult { [DataMember] public string TemplateProjectPath; [DataMember] public string TargetFolderPath; [DataMember] public List<string> ExistingWorksheetNames = new List<string>(); }
    [DataContract] internal sealed class ValidateWorksheetCreationCapabilityRequest { [DataMember] public string TemplateProjectId; [DataMember] public List<int> AttributeIds = new List<int>(); }
    [DataContract] internal sealed class ValidateWorksheetCreationCapabilityResult { [DataMember] public bool Passed; [DataMember] public string TemporaryWorksheetName; [DataMember] public string TargetFolderPath; [DataMember] public List<string> Checks = new List<string>(); [DataMember] public List<string> CleanupChecks = new List<string>(); }
    [DataContract] internal sealed class CreateWorksheetsRequest { [DataMember] public string TemplateProjectId; [DataMember] public List<CreateWorksheetItem> Worksheets = new List<CreateWorksheetItem>(); }
    [DataContract] internal sealed class CreateWorksheetItem { [DataMember] public int SheetIndex; [DataMember] public string OriginalName; [DataMember] public string RequestedName; [DataMember] public List<CreateWorksheetColumnItem> Columns = new List<CreateWorksheetColumnItem>(); }
    [DataContract] internal sealed class CreateWorksheetColumnItem { [DataMember] public int Position; [DataMember] public string Label; [DataMember] public int AttributeId; [DataMember] public int Width; }
    [DataContract] internal sealed class WorksheetOperationRecord { [DataMember] public int SheetIndex; [DataMember] public string OriginalName; [DataMember] public string FinalName; [DataMember] public string SavePath; [DataMember] public string ObjectType; [DataMember] public int ColumnCount; [DataMember] public string ColumnSummary; [DataMember] public string LabelStatus; [DataMember] public string AutoWidthStatus; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class CreateWorksheetsResult { [DataMember] public string Status; [DataMember] public List<WorksheetOperationRecord> Records = new List<WorksheetOperationRecord>(); }
    [DataContract] internal sealed class DialogDefinitionItem { [DataMember] public string TabName; [DataMember] public int AttributeId; }
    [DataContract] internal sealed class ApplyTypeDefinitionDialogsRequest { [DataMember] public List<string> TypeItemIds; [DataMember] public List<DialogDefinitionItem> Definitions; }
    [DataContract] internal sealed class TypeDefinitionOperationRecord { [DataMember] public string TypeItemId; [DataMember] public string TypeItemName; [DataMember] public int AttributeId; [DataMember] public string TabName; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class ApplyTypeDefinitionDialogsResult { [DataMember] public string Status; [DataMember] public List<TypeDefinitionOperationRecord> Records = new List<TypeDefinitionOperationRecord>(); [DataMember] public List<string> UnprocessedTypeItemIds = new List<string>(); [DataMember] public List<string> UnprocessedOperations = new List<string>(); }
    [DataContract] internal sealed class PermissionConfigurationIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; [DataMember] public string UsersAndGroupsId; [DataMember] public string UsersAndGroupsName; [DataMember] public string MessagesId; }
    [DataContract] internal sealed class PermissionDirectoryNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public bool IsSelectableMember; [DataMember] public List<PermissionDirectoryNode> Children = new List<PermissionDirectoryNode>(); }
    [DataContract] internal sealed class PermissionConfigurationStructureResult { [DataMember] public PermissionConfigurationIdentity Identity; [DataMember] public List<PermissionDirectoryNode> LeftNodes = new List<PermissionDirectoryNode>(); [DataMember] public List<PermissionDirectoryNode> RightNodes = new List<PermissionDirectoryNode>(); }
    [DataContract] internal sealed class PermissionMemberAssignmentRequest { [DataMember] public List<string> MemberIds = new List<string>(); [DataMember] public List<string> DirectoryIds = new List<string>(); }
    [DataContract] internal sealed class PermissionMemberAssignmentResult { [DataMember] public string Status; [DataMember] public int TotalCount; [DataMember] public int AddedCount; [DataMember] public int SkippedCount; [DataMember] public int FailedCount; [DataMember] public List<PermissionMemberAssignmentRecord> Records = new List<PermissionMemberAssignmentRecord>(); }
    [DataContract] internal sealed class PermissionMemberAssignmentRecord { [DataMember] public string MemberId; [DataMember] public string MemberName; [DataMember] public string DirectoryId; [DataMember] public string DirectoryName; [DataMember] public string Status; [DataMember] public string Message; }
    internal sealed class ResolvedPermissionMember { public string Id; public string Name; public string Sid; public AccessControlUser User; public AccessControlGroup Group; public string Error; }
    internal sealed class ResolvedPermissionDirectory { public string Id; public string Name; public ObjectItem Object; public string Error; }
}
