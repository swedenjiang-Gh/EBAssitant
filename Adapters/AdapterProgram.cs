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
using Aucotec;
using EbApplication = Aucotec.Application;

namespace EBAssist.Adapter
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
                if (operation == "CreateAttributes") return Write(CreateAttributes(app, Read<CreateAttributesRequest>()));
                if (operation == "CreateAttributeFolder") return Write(CreateAttributeFolder(app, Read<CreateFolderRequest>()));
                if (operation == "DeleteEmptyAttributeFolder") return Write(DeleteEmptyAttributeFolder(app, Read<DeleteFolderRequest>()));
                if (operation == "GetTypeDefinitionIdentity") return Write(GetTypeDefinitionIdentity(app));
                if (operation == "GetTypeDefinitionTree") return Write(GetTypeDefinitionTree(app));
                if (operation == "ValidateAttributeIds") return Write(ValidateAttributeIds(app, Read<ValidateAttributeIdsRequest>()));
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

            var preflightName = "EBAssist_MoveCheck_" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
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

            var created = new List<ObjectItem>();
            var result = new CreateAttributesResult();
            try
            {
                foreach (var item in request.Attributes)
                {
                    var obj = root.NewAttribute(item.Name.Trim(), ParseType(item.Type), item.Digits);
                    obj.Store();
                    root.Store();
                    if (!obj.MoveTo(target)) throw new InvalidOperationException("无法将属性移动到目录：" + item.Name);
                    obj.Store();
                    target.Store();
                    if (obj.Parent == null || !string.Equals(obj.Parent.ID, target.ID, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("属性创建后目录读回不一致：" + item.Name);
                    created.Add(obj);
                    result.CreatedNames.Add(item.Name.Trim());
                }
                result.CreatedCount = created.Count;
                return Ok(result, "批量创建成功。");
            }
            catch (Exception ex)
            {
                result.RolledBack = true;
                foreach (var obj in created.AsEnumerable().Reverse())
                {
                    try { obj.Delete(false, AucDeleteType.aucDeleteTStandard); }
                    catch (Exception rollbackEx) { result.RollbackErrors.Add(obj.Name + "：" + Describe(rollbackEx)); }
                }
                try { target.Store(); root.Store(); } catch { }
                return new AdapterResponse<CreateAttributesResult>
                {
                    Success = false,
                    Message = "批量创建失败，已尝试回滚本批属性。" + Describe(ex) +
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
                        item.Store();
                        var readback = app.Utils.GetSnglObjectByID(item.ID) as TypeItem;
                        if (readback == null || !ContainsAttribute(readback.Attributes, definition.AttributeId, null))
                            throw new InvalidOperationException("添加后无法读回属性。");
                        result.Records.Add(Record(item, definition, "added", "属性已添加到定义对话框。"));
                    }
                    catch (Exception ex)
                    {
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
        private static string Safe(Func<string> read, string fallback) { try { return read(); } catch { return fallback; } }
        private static string Describe(Exception ex) { return " " + ex.GetType().Name + "：" + ex.Message; }
    }

    [DataContract] internal sealed class AdapterResponse<T> { [DataMember] public bool Success; [DataMember] public string Message; [DataMember] public T Data; }
    [DataContract] internal sealed class ConnectionInfo { [DataMember] public string Version; [DataMember] public string ApplicationName; [DataMember] public bool IsActive; }
    [DataContract] internal sealed class AttributeFolderNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public List<AttributeFolderNode> Children = new List<AttributeFolderNode>(); }
    [DataContract] internal sealed class ExistingAttribute { [DataMember] public string Name; }
    [DataContract] internal sealed class FolderTreeResult { [DataMember] public List<AttributeFolderNode> Folders = new List<AttributeFolderNode>(); [DataMember] public List<ExistingAttribute> ExistingAttributes = new List<ExistingAttribute>(); }
    [DataContract] internal sealed class CreateAttributesRequest { [DataMember] public string TargetFolderId; [DataMember] public List<CreateAttributeItem> Attributes; }
    [DataContract] internal sealed class CreateAttributeItem { [DataMember] public int RowNumber; [DataMember] public string Name; [DataMember] public string Type; [DataMember] public int Digits; }
    [DataContract] internal sealed class CreateAttributesResult { [DataMember] public int CreatedCount; [DataMember] public bool RolledBack; [DataMember] public List<string> CreatedNames = new List<string>(); [DataMember] public List<string> RollbackErrors = new List<string>(); }
    [DataContract] internal sealed class CreateFolderRequest { [DataMember] public string ParentFolderId; [DataMember] public string Name; }
    [DataContract] internal sealed class CreateFolderResult { [DataMember] public string Id; [DataMember] public string Name; }
    [DataContract] internal sealed class DeleteFolderRequest { [DataMember] public string Id; }
    [DataContract] internal sealed class TypeDefinitionIdentity { [DataMember] public string Version; [DataMember] public string RootId; [DataMember] public string RootName; }
    [DataContract] internal sealed class TypeDefinitionNode { [DataMember] public string Id; [DataMember] public string Name; [DataMember] public string FullPath; [DataMember] public string NodeType; [DataMember] public bool IsActionable; [DataMember] public List<TypeDefinitionNode> Children = new List<TypeDefinitionNode>(); }
    [DataContract] internal sealed class TypeDefinitionTreeResult { [DataMember] public TypeDefinitionIdentity Identity; [DataMember] public List<TypeDefinitionNode> Nodes = new List<TypeDefinitionNode>(); }
    [DataContract] internal sealed class ValidateAttributeIdsRequest { [DataMember] public List<int> AttributeIds; }
    [DataContract] internal sealed class ValidateAttributeIdsResult { [DataMember] public List<int> ExistingIds; [DataMember] public List<int> MissingIds; }
    [DataContract] internal sealed class DialogDefinitionItem { [DataMember] public string TabName; [DataMember] public int AttributeId; }
    [DataContract] internal sealed class ApplyTypeDefinitionDialogsRequest { [DataMember] public List<string> TypeItemIds; [DataMember] public List<DialogDefinitionItem> Definitions; }
    [DataContract] internal sealed class TypeDefinitionOperationRecord { [DataMember] public string TypeItemId; [DataMember] public string TypeItemName; [DataMember] public int AttributeId; [DataMember] public string TabName; [DataMember] public string Status; [DataMember] public string Message; }
    [DataContract] internal sealed class ApplyTypeDefinitionDialogsResult { [DataMember] public string Status; [DataMember] public List<TypeDefinitionOperationRecord> Records = new List<TypeDefinitionOperationRecord>(); [DataMember] public List<string> UnprocessedTypeItemIds = new List<string>(); [DataMember] public List<string> UnprocessedOperations = new List<string>(); }
}
