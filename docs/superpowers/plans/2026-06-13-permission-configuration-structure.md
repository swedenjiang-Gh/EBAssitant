# 权限配置只读结构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 EBAssistant 中实现“权限配置”首版只读窗口：左侧展示带复选框的“用户及用户组”递归目录树，右侧展示排除“信息”和“用户及用户组”的数据库一级目录。

**Architecture:** WinForms 主程序负责缓存、树形展示、复选框联动、日志和右键占位提示；EB 2023/2024 x86 `.NET Framework 4.6.2` 适配器使用强类型 COM 读取 `Application.Folders.UsersAndGroups`、`Application.Folders.Messages` 和 `Application.RootObject.Children`。主程序与适配器通过现有 JSON stdin/stdout 协议通信，全流程只读。

**Tech Stack:** C#、WinForms、`.NET 10.0-windows`、强类型 Aucotec COM 30/31、`.NET Framework 4.6.2` x86、System.Text.Json、DataContractJsonSerializer

---

## 实施边界

- 项目根目录：`D:\开发\代码仓\EB\EBAssistant`
- 参考知识库：`D:\开发\代码仓\EB\EngineeringBaseCodemap`
- 不使用 `Application.AccessControl`，不读取具体权限。
- 不调用 `Store()`、`SetRight`、创建、删除或任何权限写入 API。
- 左侧只读取 `Application.Folders.UsersAndGroups` 及其 `Children`。
- 右侧只读取 `Application.RootObject.Children`，并按对象 ID 排除 `Application.Folders.Messages` 与 `Application.Folders.UsersAndGroups`。
- 左侧默认折叠，父节点勾选递归作用于子节点；父节点仅在全部直接子节点勾选时保持勾选。
- 右侧“展开”菜单本轮只显示提示，不读取子目录。
- 左右结构按“EB 版本 + 数据库根目录 ID”缓存，只有用户点击“刷新”才重新读取。
- 初始加载和刷新均保存 JSON/TXT 读取日志，并提供“打开日志目录”按钮。
- 不整理或修复与本功能无关的既有源码、乱码文本或格式。

## 文件结构

**新增主程序文件**

- `PermissionRootDirectoryFilter.cs`：按确定对象 ID 排除右侧目录的纯逻辑。
- `PermissionConfigurationCache.cs`：按 EB 版本和数据库根目录 ID 保存结构缓存。
- `PermissionConfigurationLogWriter.cs`：保存读取结果 JSON/TXT 日志。
- `PermissionConfigurationForm.cs`：左右树、连接、缓存、刷新、复选框联动和右键菜单。

**新增测试文件**

- `Tests/PermissionRootDirectoryFilterTests.cs`
- `Tests/PermissionConfigurationCacheTests.cs`

**修改现有文件**

- `Models.cs`：增加权限配置共享协议模型。
- `EbAdapterClient.cs`：增加权限配置身份与结构读取调用。
- `MainForm.cs`：把“权限配置”入口连接到非模态窗口。
- `Adapters/AdapterProgram.cs`：增加强类型只读 COM 路由、遍历和对应 DataContract 模型。
- `Tests/Program.cs`：注册新增测试组。
- `README.md`：记录权限配置首版范围、缓存和日志位置。

### Task 1: 建立权限结构模型与右侧目录筛选

**Files:**
- Modify: `Models.cs`
- Create: `PermissionRootDirectoryFilter.cs`
- Create: `Tests/PermissionRootDirectoryFilterTests.cs`
- Modify: `Tests/Program.cs`

- [ ] **Step 1: 写右侧目录筛选失败测试**

创建 `Tests/PermissionRootDirectoryFilterTests.cs`：

```csharp
namespace EBAssistant.Tests;

internal static class PermissionRootDirectoryFilterTests
{
    public static void Run()
    {
        ExcludesMessagesAndUsersAndGroupsById();
        DoesNotExcludeSameNamedDifferentObjects();
    }

    private static void ExcludesMessagesAndUsersAndGroupsById()
    {
        var directories = new List<PermissionDirectoryNode>
        {
            new() { Id = "PROJECTS", Name = "项目" },
            new() { Id = "MESSAGES", Name = "信息" },
            new() { Id = "USERS", Name = "用户及用户组" }
        };

        var result = PermissionRootDirectoryFilter.Filter(directories, "MESSAGES", "USERS");

        Assert.Equal(1, result.Count);
        Assert.Equal("PROJECTS", result[0].Id);
    }

    private static void DoesNotExcludeSameNamedDifferentObjects()
    {
        var directories = new List<PermissionDirectoryNode>
        {
            new() { Id = "OTHER", Name = "信息" }
        };

        var result = PermissionRootDirectoryFilter.Filter(directories, "MESSAGES", "USERS");

        Assert.Equal(1, result.Count);
        Assert.Equal("OTHER", result[0].Id);
    }
}
```

在 `Tests/Program.cs` 中，将 `PermissionRootDirectoryFilterTests.Run();` 加到现有测试组之后，并把成功输出数量从 `5` 更新为 `6`。

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 构建失败，提示 `PermissionDirectoryNode` 或 `PermissionRootDirectoryFilter` 不存在。

- [ ] **Step 3: 定义权限配置模型**

在 `Models.cs` 末尾增加：

```csharp
namespace EBAssistant;

public sealed class PermissionConfigurationIdentity
{
    public string Version { get; set; } = "";
    public string DatabaseRootId { get; set; } = "";
    public string DatabaseRootName { get; set; } = "";
    public string UsersAndGroupsId { get; set; } = "";
    public string UsersAndGroupsName { get; set; } = "";
    public string MessagesId { get; set; } = "";
}

public sealed class PermissionDirectoryNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int Kind { get; set; }
    public List<PermissionDirectoryNode> Children { get; set; } = [];
}

public sealed class PermissionConfigurationStructureResult
{
    public PermissionConfigurationIdentity Identity { get; set; } = new();
    public PermissionDirectoryNode UsersAndGroupsRoot { get; set; } = new();
    public List<PermissionDirectoryNode> RootDirectories { get; set; } = [];
}

public sealed class PermissionConfigurationReadLog
{
    public string Status { get; set; } = "";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string Version { get; set; } = "";
    public string DatabaseRootId { get; set; } = "";
    public string DatabaseRootName { get; set; } = "";
    public string UsersAndGroupsId { get; set; } = "";
    public string MessagesId { get; set; } = "";
    public int UsersAndGroupsNodeCount { get; set; }
    public int RootDirectoryCount { get; set; }
}
```

- [ ] **Step 4: 实现最小目录筛选器**

创建 `PermissionRootDirectoryFilter.cs`：

```csharp
namespace EBAssistant;

public static class PermissionRootDirectoryFilter
{
    public static List<PermissionDirectoryNode> Filter(
        IEnumerable<PermissionDirectoryNode> directories,
        string messagesId,
        string usersAndGroupsId)
    {
        return directories
            .Where(node =>
                !string.Equals(node.Id, messagesId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(node.Id, usersAndGroupsId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
```

- [ ] **Step 5: 运行测试并确认通过**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: `PASS: 6 test groups`。

- [ ] **Step 6: 提交模型与筛选器**

```powershell
git add Models.cs PermissionRootDirectoryFilter.cs Tests/PermissionRootDirectoryFilterTests.cs Tests/Program.cs
git commit -m "feat: add permission structure models"
```

### Task 2: 实现权限结构缓存与读取日志

**Files:**
- Create: `PermissionConfigurationCache.cs`
- Create: `PermissionConfigurationLogWriter.cs`
- Create: `Tests/PermissionConfigurationCacheTests.cs`
- Modify: `Tests/Program.cs`

- [ ] **Step 1: 写缓存隔离失败测试**

创建 `Tests/PermissionConfigurationCacheTests.cs`，测试以下三个场景：

```csharp
namespace EBAssistant.Tests;

internal static class PermissionConfigurationCacheTests
{
    public static void Run()
    {
        SeparatesCacheByVersionAndDatabaseRootId();
        ReturnsNullWhenProtocolVersionDoesNotMatch();
        DoesNotCollapseDifferentRootIdsWhenEscapingFileName();
    }

    private static void SeparatesCacheByVersionAndDatabaseRootId()
    {
        var root = NewRootDirectory();
        var identityA = Identity("2023", "ROOT-A");
        var identityB = Identity("2024", "ROOT-A");
        var identityC = Identity("2023", "ROOT-B");
        PermissionConfigurationCache.Save(Structure(identityA), root);

        Assert.NotNull(PermissionConfigurationCache.Load(identityA, root));
        Assert.Null(PermissionConfigurationCache.Load(identityB, root));
        Assert.Null(PermissionConfigurationCache.Load(identityC, root));
    }

    private static void ReturnsNullWhenProtocolVersionDoesNotMatch()
    {
        var root = NewRootDirectory();
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "2023_ROOT-A.json"), """{"ProtocolVersion":0,"Structure":{}}""");
        Assert.Null(PermissionConfigurationCache.Load(Identity("2023", "ROOT-A"), root));
    }

    private static void DoesNotCollapseDifferentRootIdsWhenEscapingFileName()
    {
        var root = NewRootDirectory();
        var dashed = Identity("2023", "ROOT-A");
        var plain = Identity("2023", "ROOTA");
        PermissionConfigurationCache.Save(Structure(dashed), root);
        Assert.NotNull(PermissionConfigurationCache.Load(dashed, root));
        Assert.Null(PermissionConfigurationCache.Load(plain, root));
    }

    private static PermissionConfigurationIdentity Identity(string version, string rootId) =>
        new() { Version = version, DatabaseRootId = rootId, DatabaseRootName = "Database" };

    private static PermissionConfigurationStructureResult Structure(PermissionConfigurationIdentity identity) =>
        new() { Identity = identity, UsersAndGroupsRoot = new PermissionDirectoryNode { Id = "USERS", Name = "用户及用户组" } };

    private static string NewRootDirectory() =>
        Path.Combine(Path.GetTempPath(), "EBAssistantTests", Guid.NewGuid().ToString("N"));
}
```

在 `Tests/Program.cs` 中注册 `PermissionConfigurationCacheTests.Run();`，成功输出数量更新为 `7`。

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 构建失败，提示 `PermissionConfigurationCache` 不存在。

- [ ] **Step 3: 实现缓存**

创建 `PermissionConfigurationCache.cs`，沿用 `ProjectTemplateCache` 模式：

```csharp
using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class PermissionConfigurationCache
{
    private const int ProtocolVersion = 1;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static PermissionConfigurationStructureResult? Load(PermissionConfigurationIdentity identity) =>
        Load(identity, GetDefaultRootDirectory());

    public static PermissionConfigurationStructureResult? Load(PermissionConfigurationIdentity identity, string rootDirectory)
    {
        var path = GetPath(identity, rootDirectory);
        if (!File.Exists(path)) return null;
        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(File.ReadAllText(path, Encoding.UTF8), Options);
            return envelope?.ProtocolVersion == ProtocolVersion ? envelope.Structure : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(PermissionConfigurationStructureResult structure) =>
        Save(structure, GetDefaultRootDirectory());

    public static void Save(PermissionConfigurationStructureResult structure, string rootDirectory)
    {
        var path = GetPath(structure.Identity, rootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CacheEnvelope
        {
            ProtocolVersion = ProtocolVersion,
            Structure = structure
        }, Options), new UTF8Encoding(false));
    }

    private static string GetDefaultRootDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant", "Cache", "PermissionConfiguration");

    private static string GetPath(PermissionConfigurationIdentity identity, string rootDirectory) =>
        Path.Combine(rootDirectory, $"{identity.Version}_{Uri.EscapeDataString(identity.DatabaseRootId)}.json");

    private sealed class CacheEnvelope
    {
        public int ProtocolVersion { get; set; }
        public PermissionConfigurationStructureResult Structure { get; set; } = new();
    }
}
```

- [ ] **Step 4: 实现双格式读取日志**

创建 `PermissionConfigurationLogWriter.cs`：

```csharp
using System.Text;
using System.Text.Json;

namespace EBAssistant;

public static class PermissionConfigurationLogWriter
{
    public static string GetDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EBAssistant", "Logs", "PermissionConfiguration");

    public static string Write(PermissionConfigurationReadLog log)
    {
        var directory = GetDirectory();
        Directory.CreateDirectory(directory);
        var timestamp = DateTime.Now;
        var stem = $"permission-configuration-read-{timestamp:yyyyMMdd-HHmmss-fff}";
        File.WriteAllText(Path.Combine(directory, stem + ".json"),
            JsonSerializer.Serialize(log, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(directory, stem + ".txt"),
        [
            $"状态：{log.Status}",
            $"时间：{timestamp:yyyy-MM-dd HH:mm:ss}",
            $"来源：{log.Source}",
            $"EB 版本：{log.Version}",
            $"数据库：{log.DatabaseRootName} ({log.DatabaseRootId})",
            $"用户及用户组 ID：{log.UsersAndGroupsId}",
            $"信息目录 ID：{log.MessagesId}",
            $"左侧节点数：{log.UsersAndGroupsNodeCount}",
            $"右侧目录数：{log.RootDirectoryCount}",
            $"说明：{log.Message}"
        ], new UTF8Encoding(false));
        return directory;
    }
}
```

- [ ] **Step 5: 运行测试并确认通过**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: `PASS: 7 test groups`。

- [ ] **Step 6: 提交缓存与日志**

```powershell
git add PermissionConfigurationCache.cs PermissionConfigurationLogWriter.cs Tests/PermissionConfigurationCacheTests.cs Tests/Program.cs
git commit -m "feat: cache and log permission structures"
```

### Task 3: 增加强类型适配器只读协议

**Files:**
- Modify: `EbAdapterClient.cs`
- Modify: `Adapters/AdapterProgram.cs`

- [ ] **Step 1: 增加主程序适配器客户端方法**

在 `EbAdapterClient.cs` 增加：

```csharp
public Task<AdapterResponse<PermissionConfigurationIdentity>> GetPermissionConfigurationIdentityAsync() =>
    InvokeAsync<PermissionConfigurationIdentity>(_adapterPath, "GetPermissionConfigurationIdentity", null);

public Task<AdapterResponse<PermissionConfigurationStructureResult>> GetPermissionConfigurationStructureAsync() =>
    InvokeAsync<PermissionConfigurationStructureResult>(_adapterPath, "GetPermissionConfigurationStructure", null);
```

- [ ] **Step 2: 增加适配器路由**

在 `Adapters/AdapterProgram.cs` 的只读路由区域加入：

```csharp
if (operation == "GetPermissionConfigurationIdentity") return Write(GetPermissionConfigurationIdentity(app));
if (operation == "GetPermissionConfigurationStructure") return Write(GetPermissionConfigurationStructure(app));
```

- [ ] **Step 3: 实现确定对象身份读取**

在 `Adapters/AdapterProgram.cs` 增加：

```csharp
private static PermissionConfigurationIdentity ReadPermissionConfigurationIdentity(EbApplication app)
{
    var databaseRoot = app.RootObject;
    var usersAndGroups = app.Folders.UsersAndGroups;
    var messages = app.Folders.Messages;
    return new PermissionConfigurationIdentity
    {
        Version = Version,
        DatabaseRootId = databaseRoot.ID,
        DatabaseRootName = databaseRoot.Name,
        UsersAndGroupsId = usersAndGroups.ID,
        UsersAndGroupsName = usersAndGroups.Name,
        MessagesId = messages.ID
    };
}

private static AdapterResponse<PermissionConfigurationIdentity> GetPermissionConfigurationIdentity(EbApplication app)
{
    return Ok(ReadPermissionConfigurationIdentity(app), "权限配置目录身份读取成功。");
}
```

- [ ] **Step 4: 实现递归节点读取与右侧一级目录读取**

增加只读递归方法：

```csharp
private static PermissionDirectoryNode ReadPermissionDirectoryNode(ObjectItem item, string parentPath)
{
    var path = string.IsNullOrWhiteSpace(parentPath) ? item.Name : parentPath + " / " + item.Name;
    var node = new PermissionDirectoryNode
    {
        Id = item.ID,
        Name = item.Name,
        FullPath = path,
        Kind = (int)item.Kind
    };
    foreach (object raw in item.Children as IEnumerable)
    {
        var child = raw as ObjectItem;
        if (child != null) node.Children.Add(ReadPermissionDirectoryNode(child, path));
    }
    return node;
}
```

增加结构读取方法。右侧节点只保存一级对象，不遍历其 `Children`：

```csharp
private static AdapterResponse<PermissionConfigurationStructureResult> GetPermissionConfigurationStructure(EbApplication app)
{
    var identity = ReadPermissionConfigurationIdentity(app);
    var usersAndGroups = app.Folders.UsersAndGroups;
    var result = new PermissionConfigurationStructureResult
    {
        Identity = identity,
        UsersAndGroupsRoot = ReadPermissionDirectoryNode(usersAndGroups, "")
    };
    foreach (object raw in app.RootObject.Children as IEnumerable)
    {
        var child = raw as ObjectItem;
        if (child == null) continue;
        if (string.Equals(child.ID, identity.MessagesId, StringComparison.OrdinalIgnoreCase)) continue;
        if (string.Equals(child.ID, identity.UsersAndGroupsId, StringComparison.OrdinalIgnoreCase)) continue;
        result.RootDirectories.Add(new PermissionDirectoryNode
        {
            Id = child.ID,
            Name = child.Name,
            FullPath = app.RootObject.Name + " / " + child.Name,
            Kind = (int)child.Kind
        });
    }
    return Ok(result, "权限配置只读结构读取成功。");
}
```

- [ ] **Step 5: 同步适配器 DataContract 模型**

在 `Adapters/AdapterProgram.cs` 底部增加与主程序模型字段完全一致的类型：

```csharp
[DataContract] internal sealed class PermissionConfigurationIdentity
{
    [DataMember] public string Version;
    [DataMember] public string DatabaseRootId;
    [DataMember] public string DatabaseRootName;
    [DataMember] public string UsersAndGroupsId;
    [DataMember] public string UsersAndGroupsName;
    [DataMember] public string MessagesId;
}

[DataContract] internal sealed class PermissionDirectoryNode
{
    [DataMember] public string Id;
    [DataMember] public string Name;
    [DataMember] public string FullPath;
    [DataMember] public int Kind;
    [DataMember] public List<PermissionDirectoryNode> Children = new List<PermissionDirectoryNode>();
}

[DataContract] internal sealed class PermissionConfigurationStructureResult
{
    [DataMember] public PermissionConfigurationIdentity Identity;
    [DataMember] public PermissionDirectoryNode UsersAndGroupsRoot;
    [DataMember] public List<PermissionDirectoryNode> RootDirectories = new List<PermissionDirectoryNode>();
}
```

- [ ] **Step 6: 构建主程序与适配器**

Run:

```powershell
.\build.ps1
```

Expected: 主程序、EB 2023、EB 2024 和 EB 2025 占位适配器均构建成功。

- [ ] **Step 7: 在活动 EB 上验证只读结构输出**

先运行身份读取：

```powershell
& .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe GetPermissionConfigurationIdentity
```

再运行结构读取：

```powershell
& .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe GetPermissionConfigurationStructure
```

Expected:

- `Success=true`。
- `Identity.DatabaseRootId`、`UsersAndGroupsId`、`MessagesId` 均非空。
- `UsersAndGroupsRoot.Id` 等于 `Identity.UsersAndGroupsId`。
- `RootDirectories` 不包含 `MessagesId` 或 `UsersAndGroupsId`。
- 运行前后 EB 数据库无对象变化。

- [ ] **Step 8: 提交只读适配器协议**

```powershell
git add EbAdapterClient.cs Adapters/AdapterProgram.cs
git commit -m "feat: read permission configuration structures"
```

### Task 4: 实现权限配置窗口与主界面入口

**Files:**
- Create: `PermissionConfigurationForm.cs`
- Modify: `MainForm.cs`

- [ ] **Step 1: 创建左右分栏只读窗口**

创建 `PermissionConfigurationForm.cs`，字段固定为：

```csharp
private readonly TreeView _usersTree = new()
{
    Dock = DockStyle.Fill,
    CheckBoxes = true,
    HideSelection = false,
    ShowNodeToolTips = true
};
private readonly TreeView _directoriesTree = new()
{
    Dock = DockStyle.Fill,
    HideSelection = false,
    ShowNodeToolTips = true
};
private readonly Button _refresh = new() { Text = "刷新", Width = 100, Height = 34 };
private readonly Button _openLogs = new() { Text = "打开日志目录", Width = 130, Height = 34 };
private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8) };
private readonly ContextMenuStrip _directoryMenu = new();
private EbAdapterClient? _client;
private PermissionConfigurationIdentity? _identity;
private bool _updatingChecks;
```

构造函数使用 `SplitContainer` 创建左右区域，标题分别为“用户及用户组”和“权限控制目录”；右侧 TreeView 绑定 `_directoryMenu`；窗口 `Shown` 时调用 `ConnectAndLoadAsync()`。

- [ ] **Step 2: 实现活动 EB 选择与缓存优先加载**

`ConnectAndLoadAsync()` 按以下顺序实现：

```text
1. EbAdapterClient.FindActiveAsync()
2. 无活动 EB 时提示用户先打开 EB 并连接数据库
3. 多活动版本时显示选择对话框
4. GetPermissionConfigurationIdentityAsync()
5. PermissionConfigurationCache.Load(identity)
6. 有缓存则 DisplayStructure(cached, "缓存") 并写读取日志
7. 无缓存则 LoadFromEbAsync()
```

选择对话框沿用 `AttributeFoldersForm.SelectAdapter` 的非持久实现，不新增通用抽象。

- [ ] **Step 3: 实现刷新、显示和节点计数**

`LoadFromEbAsync()` 调用 `GetPermissionConfigurationStructureAsync()`；成功后再次通过：

```csharp
response.Data.RootDirectories = PermissionRootDirectoryFilter.Filter(
    response.Data.RootDirectories,
    response.Data.Identity.MessagesId,
    response.Data.Identity.UsersAndGroupsId);
```

执行防御性排除，然后保存缓存并显示。

`DisplayStructure`：

- 左侧只添加一个 `UsersAndGroupsRoot` 根节点，并递归添加子节点。
- 右侧添加 `RootDirectories` 一级节点，不添加子节点。
- 两棵树加载后均调用 `CollapseAll()`。
- 节点 `Tag` 保存对应 `PermissionDirectoryNode`，`ToolTipText` 使用 `FullPath`。

节点计数使用单独私有递归方法：

```csharp
private static int CountNodes(PermissionDirectoryNode node) =>
    1 + node.Children.Sum(CountNodes);
```

- [ ] **Step 4: 实现左侧复选框父子联动**

绑定 `_usersTree.AfterCheck`，实现：

```csharp
private void UsersTreeAfterCheck(object? sender, TreeViewEventArgs e)
{
    if (_updatingChecks || e.Node is null) return;
    _updatingChecks = true;
    try
    {
        SetChildrenChecked(e.Node, e.Node.Checked);
        UpdateParents(e.Node.Parent);
    }
    finally
    {
        _updatingChecks = false;
    }
}
```

`SetChildrenChecked` 递归设置全部子节点；`UpdateParents` 向上循环，并使用 `node.Nodes.Cast<TreeNode>().All(x => x.Checked)` 计算父节点状态。

- [ ] **Step 5: 实现右侧“展开”菜单占位行为**

右键选中右侧节点后显示 `_directoryMenu`。菜单项文字固定为“展开”，点击时显示：

```csharp
MessageBox.Show(this, "展开功能将在后续开发中实现。", "权限配置",
    MessageBoxButtons.OK, MessageBoxIcon.Information);
```

本处理器不得调用适配器或修改树节点。

- [ ] **Step 6: 实现状态、日志与打开日志目录**

每次缓存加载、EB 读取成功或读取失败时构造 `PermissionConfigurationReadLog` 并调用 `PermissionConfigurationLogWriter.Write`。成功状态栏格式：

```text
已从{来源}加载；用户及用户组节点 N 个；权限控制目录 M 个。
```

日志写入失败时显示独立错误消息，但保留已展示数据。`_openLogs` 点击处理器：

```csharp
var directory = PermissionConfigurationLogWriter.GetDirectory();
Directory.CreateDirectory(directory);
Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
```

- [ ] **Step 7: 从主界面打开非模态窗口**

在 `MainForm.OpenFunction` 中，在工作表分支之后增加“权限配置”分支，模式与其他功能窗口一致：

```csharp
if (functionName == "权限配置")
{
    var form = new PermissionConfigurationForm();
    _openWindows.Add(form);
    form.FormClosed += (_, _) => _openWindows.Remove(form);
    form.Show();
    return;
}
```

必须使用源码中实际的“权限配置”字符串，不修改其余五个入口。

- [ ] **Step 8: 构建并人工验证窗口行为**

Run:

```powershell
dotnet build .\EBAssistant.csproj
```

Expected: 构建成功。

启动：

```powershell
& .\bin\Debug\net10.0-windows\EBAssistant.exe
```

人工验证：

1. “权限配置”打开独立非模态窗口。
2. 左侧根节点为“用户及用户组”，加载后默认折叠且有复选框。
3. 勾选父节点会勾选全部子节点；取消子节点会取消父节点。
4. 右侧只显示数据库一级目录，不显示“信息”和“用户及用户组”。
5. 右键“展开”只显示提示，树不变化。
6. 状态栏显示来源与数量。
7. “打开日志目录”打开正确目录。

- [ ] **Step 9: 提交权限配置窗口**

```powershell
git add PermissionConfigurationForm.cs MainForm.cs
git commit -m "feat: add permission configuration window"
```

### Task 5: 文档、回归与真实 EB 验收

**Files:**
- Modify: `README.md`

- [ ] **Step 1: 更新 README**

在 `README.md` 中增加“权限配置”首版说明：

```text
- 左侧递归读取“用户及用户组”目录，默认折叠并支持复选框父子联动。
- 右侧显示数据库一级目录，排除“信息”和“用户及用户组”。
- 当前只读；右侧“展开”和具体权限读写尚未实现。
- 缓存目录：%LOCALAPPDATA%\EBAssistant\Cache\PermissionConfiguration
- 日志目录：%LOCALAPPDATA%\EBAssistant\Logs\PermissionConfiguration
```

- [ ] **Step 2: 运行全部自动测试**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: `PASS: 7 test groups`。

- [ ] **Step 3: 运行完整构建**

Run:

```powershell
.\build.ps1
```

Expected: 主程序、EB 2023、EB 2024 与 EB 2025 占位适配器均构建成功。

- [ ] **Step 4: 扫描禁止项与范围外实现**

Run:

```powershell
rg -n "AccessControl|SetRight|Store\\(|Delete\\(|NewChild|dynamic" PermissionConfiguration*.cs EbAdapterClient.cs Adapters\AdapterProgram.cs
```

Expected:

- 新增权限配置主程序文件不包含 `AccessControl`、`SetRight`、`Store(`、`Delete(`、`NewChild` 或 `dynamic`。
- `Adapters/AdapterProgram.cs` 的既有无关命中可以存在；新增权限配置方法仅使用强类型只读属性与 `Children`。

- [ ] **Step 5: 检查新增中文文本编码**

使用 PowerShell 检查新增文件前三字节，确认不是 UTF-8 BOM `EF BB BF`；再使用明确 UTF-8 读取，确认不包含替换字符 `U+FFFD`：

```powershell
$files = @(
  "Models.cs",
  "PermissionRootDirectoryFilter.cs",
  "PermissionConfigurationCache.cs",
  "PermissionConfigurationLogWriter.cs",
  "PermissionConfigurationForm.cs",
  "Tests\PermissionRootDirectoryFilterTests.cs",
  "Tests\PermissionConfigurationCacheTests.cs"
)
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
foreach ($file in $files) {
  $bytes = [System.IO.File]::ReadAllBytes((Join-Path $PWD $file))
  if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$file has UTF-8 BOM" }
  [void]$utf8.GetString($bytes)
}
```

Expected: 命令无错误输出。

- [ ] **Step 6: 完成真实 EB 2023/2024 验收**

在可用的 EB 2023 和 EB 2024 活动数据库中分别验证：

1. 左侧结构与 EB 数据库“用户及用户组”目录一致。
2. 左侧默认折叠，复选框父子联动正确。
3. 右侧一级目录与数据库根目录一致，且排除两个确定目录。
4. 首次读取后产生 JSON/TXT 日志。
5. 关闭并重新打开窗口时从缓存加载，不重新遍历 EB。
6. 点击“刷新”重新读取并覆盖缓存。
7. 读取前后数据库对象和权限均未变化。

- [ ] **Step 7: 检查变更范围**

Run:

```powershell
git status --short
git diff --stat
git diff --check
```

Expected: `git diff --check` 无错误；所有变更均能追溯到本计划；不包含无关清理。

- [ ] **Step 8: 提交文档与最终回归修改**

```powershell
git add README.md
git commit -m "docs: document permission configuration structure"
```

## 完成标准

- “权限配置”入口打开独立非模态窗口。
- 左侧只展示 `Application.Folders.UsersAndGroups` 的完整递归结构，包含根节点，默认折叠并支持复选框父子联动。
- 右侧只展示 `Application.RootObject.Children` 的一级目录，并按对象 ID 排除“信息”和“用户及用户组”。
- 右侧“展开”只显示提示，不读取任何子目录。
- 初始加载优先使用按数据库身份隔离的缓存；只有“刷新”重新读取 EB。
- 每次加载或刷新保存 JSON/TXT 日志，按钮可打开日志目录。
- 主程序不引用 Aucotec COM；适配器新增代码使用强类型只读 COM，不包含权限写入。
- 自动测试、完整构建和真实 EB 验收通过。
