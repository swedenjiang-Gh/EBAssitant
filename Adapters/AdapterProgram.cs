using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
            var app = GetActiveApplication();
            return Ok(new ConnectionInfo
            {
                Version = Version,
                IsActive = app != null,
                ApplicationName = app == null ? "EB " + Version : Safe(delegate { return app.Name; }, "EB " + Version)
            }, app == null ? "未运行" : "已连接");
        }

        private static EbApplication GetActiveApplication()
        {
            foreach (var progId in new[] { ProgId, "EngineeringBase.Application", "Aucotec.Application" })
            {
                try
                {
                    var app = (EbApplication)Marshal.GetActiveObject(progId);
                    if (app != null) return app;
                }
                catch (COMException) { }
                catch (InvalidCastException) { }
            }
            var tableResult = GetRunningObjectTable(0, out var table);
            if (tableResult == 0 && table != null)
            {
                table.EnumRunning(out var enumerator);
                var monikers = new IMoniker[1];
                var fetched = IntPtr.Zero;
                while (enumerator.Next(1, monikers, fetched) == 0)
                {
                    try
                    {
                        table.GetObject(monikers[0], out var raw);
                        var app = raw as EbApplication;
                        if (app != null) return app;
                    }
                    catch (COMException) { }
                    catch (InvalidCastException) { }
                }
            }
            if (IsExpectedClientRunning())
            {
                try
                {
                    var type = Type.GetTypeFromProgID(ProgId);
                    if (type != null)
                    {
                        var app = (EbApplication)Activator.CreateInstance(type);
                        if (app != null && app.Folders != null) return app;
                    }
                }
                catch (COMException) { }
                catch (InvalidCastException) { }
            }
            return null;
        }

        private static bool IsExpectedClientRunning()
        {
            foreach (var process in Process.GetProcessesByName("EngineeringBase"))
            {
                try
                {
                    var path = process.MainModule == null ? "" : process.MainModule.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && path.IndexOf(ExpectedInstallFolder, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                catch { }
            }
            return false;
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
    [DataContract] internal sealed class ConnectionInfo { [DataMember] public string Version; [DataMember] public string ApplicationName; [DataMember] public bool IsActive; }
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
}
