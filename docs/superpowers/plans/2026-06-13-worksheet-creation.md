# “工作表”批量创建功能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 EBAssistant 中实现带项目模板树缓存、Excel 多页签预览校验、器件工作表批量创建、自动列宽和完整日志的“工作表”功能。

**Architecture:** WinForms 主程序继续只负责缓存、Excel 解析、预览校验和结果展示；所有 EB COM 读取与写入继续放在 2023/2024 强类型 x86 `.NET Framework 4.6.2` 适配器中，并通过现有 JSON stdin/stdout 协议调用。正式写入前先以独立开发操作完成一次受控能力验证；只有列标签、列顺序、列宽、保存位置、读回和清理全部通过，才实施并启用正式 `CreateWorksheets`。

**Tech Stack:** C#、WinForms、`.NET 10.0-windows`、ExcelDataReader、强类型 Aucotec COM 30/31、`.NET Framework 4.6.2` x86、System.Text.Json、DataContractJsonSerializer

---

## 实施前提与边界

- 当前工作区已有与“属性”“类型定义”和重命名有关的未提交修改；实施时不得回退、覆盖或顺带整理这些修改。
- 本计划中的提交命令假定执行阶段使用隔离工作树。若直接在当前脏工作区执行，共享文件含有实施前修改时不得整文件暂存；只提交可明确隔离的任务文件，无法安全隔离的共享文件延后提交。
- 项目根目录为 `D:\开发\代码仓\EB\EBAssistant`，兄弟知识库为 `D:\开发\代码仓\EB\EngineeringBaseCodemap`。
- 主程序不得引用 Aucotec COM，不得使用 `dynamic`；COM 代码只进入 `Adapters/AdapterProgram.cs`。
- 项目模板树来自 `Application.Folders.ProjectTemplates`，不得误用 `WorksheetTemplatesFolder`。
- 固定工作表对象类型为 `AucObjectKind.aucObjDevice`。
- 已确认公共 COM 可提供：
  - `Project.OpenWorksheetDirect(AucObjectKind, AucAttribute, AucVbFindCondition, object, params object[])`
  - `Worksheet.Attributes.Add(object, int)`
  - `WorksheetAttribute.Position`
  - `WorksheetAttribute.Width`
  - `Worksheet.SaveConfiguration(name, targetFolder)`
  - `Worksheet.ConfigurationObject`
  - `Worksheet.Close()`
- 当前公开强类型 COM 中 `WorksheetAttribute.Name` 只有 getter，自定义列标签的写入配方尚未在仓库证据中明确记录。Task 6 必须先验证准确配方；若验证失败，停止正式写入实现并报告，不得用属性名称冒充 Excel 第一行标签。
- 正式功能不执行临时能力验证，也不重复读回列宽；一次性能力验证只在开发阶段显式调用。
- 自动列宽采用确定性计算后写入 `WorksheetAttribute.Width`：ASCII 字符按 1 个单位、非 ASCII 字符按 2 个单位，结果为 `Math.Clamp(visualUnits * 9 + 24, 80, 600)`。Task 6 必须在真实 EB 中确认该宽度足以完整显示长标签。
- 只删除一次性验证创建的明确临时工作表对象；不得删除用户已有工作表。

## 文件结构

**新增主程序文件**

- `WorksheetModels.cs`：工作表功能的主程序协议模型和纯逻辑模型。
- `WorksheetNameResolver.cs`：最小可用同名后缀计算。
- `WorksheetColumnWidthCalculator.cs`：确定性列宽计算。
- `WorksheetImportBuilder.cs`：把 Excel 页签原始数据转换为预览模型并做本地校验。
- `WorksheetExcelImporter.cs`：使用 ExcelDataReader 读取全部页签的前两行和第二列起的数据。
- `ProjectTemplateCache.cs`：按 EB 版本和项目模板根目录 ID 缓存树。
- `ProjectTemplatesForm.cs`：项目模板树、刷新和右键“新建工作表”。
- `CreateWorksheetsForm.cs`：Excel 导入、EB AID 校验、预览和确认创建。
- `WorksheetCreationLogWriter.cs`：保存 JSON/TXT 日志。
- `WorksheetCreationResultForm.cs`：展示结果和打开日志目录。

**新增测试文件**

- `Tests/EBAssistant.Tests.csproj`：无第三方测试包的轻量测试可执行项目。
- `Tests/Program.cs`：测试入口和断言辅助。
- `Tests/WorksheetNameResolverTests.cs`
- `Tests/WorksheetColumnWidthCalculatorTests.cs`
- `Tests/WorksheetImportBuilderTests.cs`
- `Tests/ProjectTemplateCacheTests.cs`

**修改现有文件**

- `EBAssistant.csproj`：排除 `Tests/**/*.cs`，避免测试源码编入 WinForms 主程序。
- `Models.cs`：不继续堆叠工作表模型；仅保留现有模型，工作表模型放入新文件。
- `EbAdapterClient.cs`：增加项目模板树、AID 校验、能力验证和创建工作表调用。
- `MainForm.cs`：把“工作表”按钮连接到独立非模态窗口。
- `Adapters/AdapterProgram.cs`：增加所有项目模板树读取、一次性能力验证和正式工作表创建的强类型 COM 实现及 DataContract 模型。
- `README.md`：补充工作表 Excel 格式和日志位置。

### Task 1: 建立轻量测试入口和纯逻辑基础

**Files:**
- Create: `Tests/EBAssistant.Tests.csproj`
- Create: `Tests/Program.cs`
- Create: `Tests/WorksheetNameResolverTests.cs`
- Create: `Tests/WorksheetColumnWidthCalculatorTests.cs`
- Create: `WorksheetNameResolver.cs`
- Create: `WorksheetColumnWidthCalculator.cs`
- Modify: `EBAssistant.csproj`

- [ ] **Step 1: 创建测试项目并阻止测试源码进入主程序**

在 `EBAssistant.csproj` 的现有 `<ItemGroup>` 中增加：

```xml
<Compile Remove="Tests\**\*.cs" />
```

创建 `Tests/EBAssistant.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\EBAssistant.csproj" />
  </ItemGroup>
</Project>
```

创建 `Tests/Program.cs`，按顺序执行测试类；失败时输出测试名并返回非零退出码：

```csharp
using EBAssistant.Tests;

var tests = new Action[]
{
    WorksheetNameResolverTests.Run,
    WorksheetColumnWidthCalculatorTests.Run
};

try
{
    foreach (var test in tests) test();
    Console.WriteLine($"PASS: {tests.Length} test groups");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
```

- [ ] **Step 2: 先写名称和列宽失败测试**

创建 `Tests/WorksheetNameResolverTests.cs`，覆盖：

```csharp
Assert.Equal("设备清单", WorksheetNameResolver.Resolve(" 设备清单 ", []));
Assert.Equal("设备清单 (2)", WorksheetNameResolver.Resolve("设备清单", ["设备清单"]));
Assert.Equal("设备清单 (3)", WorksheetNameResolver.Resolve("设备清单", ["设备清单", "设备清单 (2)"]));
Assert.Equal("设备清单 (2)", WorksheetNameResolver.Resolve("设备清单", ["设备清单", "设备清单 (3)"]));
```

创建 `Tests/WorksheetColumnWidthCalculatorTests.cs`，覆盖：

```csharp
Assert.Equal(80, WorksheetColumnWidthCalculator.Calculate("A"));
Assert.True(WorksheetColumnWidthCalculator.Calculate("设备名称") > WorksheetColumnWidthCalculator.Calculate("Name"));
Assert.Equal(600, WorksheetColumnWidthCalculator.Calculate(new string('长', 100)));
```

在 `Tests/Program.cs` 内提供公共静态 `Assert.Equal`、`Assert.True`、`Assert.Null` 和 `Assert.NotNull`，失败时抛出 `InvalidOperationException`。

- [ ] **Step 3: 运行测试并确认失败**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 构建失败，提示 `WorksheetNameResolver` 和 `WorksheetColumnWidthCalculator` 不存在。

- [ ] **Step 4: 实现最小名称解析和列宽计算**

创建 `WorksheetNameResolver.cs`：

```csharp
namespace EBAssistant;

public static class WorksheetNameResolver
{
    public static string Resolve(string requestedName, IEnumerable<string> reservedNames)
    {
        var baseName = requestedName.Trim();
        var reserved = new HashSet<string>(
            reservedNames.Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (!reserved.Contains(baseName)) return baseName;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";
            if (!reserved.Contains(candidate)) return candidate;
        }
    }
}
```

创建 `WorksheetColumnWidthCalculator.cs`：

```csharp
namespace EBAssistant;

public static class WorksheetColumnWidthCalculator
{
    public static int Calculate(string label)
    {
        var visualUnits = label.Sum(ch => ch <= 0x7f ? 1 : 2);
        return Math.Clamp(visualUnits * 9 + 24, 80, 600);
    }
}
```

- [ ] **Step 5: 运行测试并确认通过**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: `PASS: 2 test groups`。

- [ ] **Step 6: 提交纯逻辑基础**

```powershell
git add EBAssistant.csproj Tests/EBAssistant.Tests.csproj Tests/Program.cs Tests/WorksheetNameResolverTests.cs Tests/WorksheetColumnWidthCalculatorTests.cs WorksheetNameResolver.cs WorksheetColumnWidthCalculator.cs
git commit -m "test: add worksheet logic test harness"
```

### Task 2: 定义工作表模型并实现 Excel 页签构建校验

**Files:**
- Create: `WorksheetModels.cs`
- Create: `WorksheetImportBuilder.cs`
- Create: `Tests/WorksheetImportBuilderTests.cs`
- Modify: `Tests/Program.cs`

- [ ] **Step 1: 定义主程序工作表模型**

在 `WorksheetModels.cs` 中定义以下类型，属性均使用公开 getter/setter 和非空默认值：

```csharp
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
```

同文件继续定义协议模型：

```csharp
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
    public string AutoWidthStatus { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class CreateWorksheetsResult
{
    public string Status { get; set; } = "";
    public List<WorksheetOperationRecord> Records { get; set; } = [];
}
```

- [ ] **Step 2: 先写导入构建器失败测试**

在 `Tests/WorksheetImportBuilderTests.cs` 中构造内存页签并验证：

```csharp
var sheets = new List<WorksheetRawSheet>
{
    new()
    {
        SheetIndex = 1,
        SheetName = " 设备清单 ",
        Columns =
        [
            new() { ExcelColumnNumber = 2, Label = "设备名称", AttributeIdText = "5" },
            new() { ExcelColumnNumber = 3, Label = "说明", AttributeIdText = "25" }
        ]
    }
};

var result = WorksheetImportBuilder.Build(sheets);
Assert.Equal("设备清单", result[0].OriginalName);
Assert.Equal(2, result[0].Columns.Count);
Assert.Equal(0, result[0].Columns[0].Position);
Assert.Equal(5, result[0].Columns[0].AttributeId);
Assert.Equal("待 EB 校验", result[0].Validation);
```

增加无效场景：空页签名、无列、空标签、非正整数 AID、同页签重复 AID；断言整页签 `Validation` 为具体错误文本。

同时在 `Tests/Program.cs` 的 `tests` 数组末尾增加 `WorksheetImportBuilderTests.Run`。

- [ ] **Step 3: 运行测试并确认失败**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 构建失败，提示 `WorksheetImportBuilder` 不存在。

- [ ] **Step 4: 实现本地校验构建器**

创建 `WorksheetImportBuilder.cs`，实现：

```csharp
namespace EBAssistant;

public static class WorksheetImportBuilder
{
    public static List<WorksheetImportSheet> Build(IEnumerable<WorksheetRawSheet> rawSheets)
    {
        var result = new List<WorksheetImportSheet>();
        foreach (var raw in rawSheets.OrderBy(x => x.SheetIndex))
        {
            var sheet = new WorksheetImportSheet
            {
                SheetIndex = raw.SheetIndex,
                OriginalName = raw.SheetName.Trim()
            };
            var errors = new List<string>();
            if (sheet.OriginalName.Length == 0) errors.Add("页签名称为空");
            if (sheet.OriginalName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) errors.Add("页签名称包含无效字符");
            if (raw.Columns.Count == 0) errors.Add("没有可创建的列");

            var seenIds = new HashSet<int>();
            foreach (var rawColumn in raw.Columns.OrderBy(x => x.ExcelColumnNumber))
            {
                var label = rawColumn.Label.Trim();
                var aidText = rawColumn.AttributeIdText.Trim();
                var columnErrors = new List<string>();
                if (label.Length == 0) columnErrors.Add("列标签为空");
                if (!int.TryParse(aidText, out var aid) || aid <= 0) columnErrors.Add("属性 ID 必须为正整数");
                if (aid > 0 && !seenIds.Add(aid)) columnErrors.Add("同一页签内属性 ID 重复");
                if (columnErrors.Count > 0) errors.Add($"第 {rawColumn.ExcelColumnNumber} 列：{string.Join("；", columnErrors)}");

                sheet.Columns.Add(new WorksheetColumnDefinition
                {
                    ExcelColumnNumber = rawColumn.ExcelColumnNumber,
                    Position = sheet.Columns.Count,
                    Label = label,
                    AttributeId = aid,
                    Width = WorksheetColumnWidthCalculator.Calculate(label),
                    Validation = columnErrors.Count == 0 ? "待 EB 校验" : string.Join("；", columnErrors)
                });
            }
            sheet.Validation = errors.Count == 0 ? "待 EB 校验" : string.Join("；", errors);
            result.Add(sheet);
        }
        return result;
    }
}
```

- [ ] **Step 5: 运行测试并确认通过**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 所有测试组通过。

- [ ] **Step 6: 提交模型和本地校验**

```powershell
git add WorksheetModels.cs WorksheetImportBuilder.cs Tests/WorksheetImportBuilderTests.cs
git commit -m "feat: add worksheet import models and validation"
```

### Task 3: 读取 Excel 全部页签

**Files:**
- Create: `WorksheetExcelImporter.cs`
- Modify: `Tests/WorksheetImportBuilderTests.cs`

- [ ] **Step 1: 增加 Excel 原始页签读取实现**

创建 `WorksheetExcelImporter.cs`。只读取每个页签前两行；忽略第一列和第三行以后内容；保持页签和列顺序：

```csharp
using ExcelDataReader;

namespace EBAssistant;

public static class WorksheetExcelImporter
{
    public static List<WorksheetRawSheet> Read(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var sheets = new List<WorksheetRawSheet>();
        var sheetIndex = 0;
        do
        {
            sheetIndex++;
            var firstRow = new List<string>();
            var secondRow = new List<string>();
            if (reader.Read()) firstRow = ReadRow(reader);
            if (reader.Read()) secondRow = ReadRow(reader);
            var fieldCount = Math.Max(firstRow.Count, secondRow.Count);
            var sheet = new WorksheetRawSheet { SheetIndex = sheetIndex, SheetName = reader.Name ?? "" };
            for (var index = 1; index < fieldCount; index++)
            {
                var label = index < firstRow.Count ? firstRow[index] : "";
                var aid = index < secondRow.Count ? secondRow[index] : "";
                if (label.Length == 0 && aid.Length == 0) continue;
                sheet.Columns.Add(new WorksheetRawColumn
                {
                    ExcelColumnNumber = index + 1,
                    Label = label,
                    AttributeIdText = aid
                });
            }
            sheets.Add(sheet);
        }
        while (reader.NextResult());
        return sheets;
    }

    private static List<string> ReadRow(IExcelDataReader reader) =>
        Enumerable.Range(0, reader.FieldCount)
            .Select(index => Convert.ToString(reader.GetValue(index))?.Trim() ?? "")
            .ToList();
}
```

- [ ] **Step 2: 增加人工验证清单**

在 `Tests/WorksheetImportBuilderTests.cs` 顶部注释中记录必须人工验证的固定样例：

```text
worksheet-import.xlsx / worksheet-import.xls:
- 2 个页签，顺序为 设备清单、备用设备
- 第一列内容不进入预览
- 第 2 列起第一行为标签、第二行为 AID
- 第三行故意填写内容但不进入预览
```

不要向仓库提交含业务数据的 Excel；使用临时脱敏样例完成验证后删除该单个临时文件。

- [ ] **Step 3: 构建并人工验证 `.xlsx` 与 `.xls`**

Run:

```powershell
dotnet build .\EBAssistant.csproj
```

Expected: 构建成功。随后在实现 Task 8 的导入界面后，用上述脱敏样例确认两个格式结果一致。

- [ ] **Step 4: 提交 Excel 读取器**

```powershell
git add WorksheetExcelImporter.cs Tests/WorksheetImportBuilderTests.cs
git commit -m "feat: read worksheet definitions from excel sheets"
```

### Task 4: 实现项目模板树协议、缓存和只读适配器操作

**Files:**
- Create: `ProjectTemplateCache.cs`
- Create: `Tests/ProjectTemplateCacheTests.cs`
- Modify: `Tests/Program.cs`
- Modify: `EbAdapterClient.cs`
- Modify: `Adapters/AdapterProgram.cs`

- [ ] **Step 1: 先写缓存隔离失败测试**

在 `Tests/ProjectTemplateCacheTests.cs` 中通过可注入目录测试：

```csharp
var identityA = new ProjectTemplateIdentity { Version = "2023", RootId = "A", RootName = "项目模板" };
var identityB = new ProjectTemplateIdentity { Version = "2024", RootId = "A", RootName = "项目模板" };
var tree = new ProjectTemplateTreeResult { Identity = identityA };
ProjectTemplateCache.Save(tree, tempDirectory);
Assert.NotNull(ProjectTemplateCache.Load(identityA, tempDirectory));
Assert.Null(ProjectTemplateCache.Load(identityB, tempDirectory));
```

再写协议版本不匹配返回 `null` 的测试。

同时在 `Tests/Program.cs` 的 `tests` 数组末尾增加 `ProjectTemplateCacheTests.Run`。

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 构建失败，提示 `ProjectTemplateCache` 不存在。

- [ ] **Step 3: 实现项目模板树缓存**

创建 `ProjectTemplateCache.cs`，协议版本从 `1` 开始；公开生产重载使用：

```text
%LOCALAPPDATA%\EBAssistant\Cache\ProjectTemplates\{version}_{rootId}.json
```

测试重载接收显式根目录。缓存 envelope 必须同时保存 `ProtocolVersion` 和 `ProjectTemplateTreeResult`，读取异常返回 `null`。

- [ ] **Step 4: 扩展主程序适配器客户端**

在 `EbAdapterClient.cs` 增加：

```csharp
public Task<AdapterResponse<ProjectTemplateIdentity>> GetProjectTemplateIdentityAsync() =>
    InvokeAsync<ProjectTemplateIdentity>(_adapterPath, "GetProjectTemplateIdentity", null);

public Task<AdapterResponse<ProjectTemplateTreeResult>> GetProjectTemplateTreeAsync() =>
    InvokeAsync<ProjectTemplateTreeResult>(_adapterPath, "GetProjectTemplateTree", null);

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
```

- [ ] **Step 5: 在适配器中实现项目模板身份和树**

在 `Adapters/AdapterProgram.cs` 操作路由中增加：

```csharp
if (operation == "GetProjectTemplateIdentity") return Write(GetProjectTemplateIdentity(app));
if (operation == "GetProjectTemplateTree") return Write(GetProjectTemplateTree(app));
if (operation == "ValidateWorksheetAttributeIds") return Write(ValidateWorksheetAttributeIds(app, Read<ValidateWorksheetAttributeIdsRequest>()));
if (operation == "GetWorksheetCreationContext") return Write(GetWorksheetCreationContext(app, Read<WorksheetCreationContextRequest>()));
```

实现规则：

```csharp
private static AdapterResponse<ProjectTemplateIdentity> GetProjectTemplateIdentity(EbApplication app)
{
    var root = app.Folders.ProjectTemplates;
    return Ok(new ProjectTemplateIdentity
    {
        Version = Version,
        RootId = root.ID,
        RootName = root.Name
    }, "项目模板身份读取成功。");
}
```

`GetProjectTemplateTree` 从 `app.Folders.ProjectTemplates.Children` 按枚举顺序递归。节点只保留：

- `AucObjectKind.aucObjProject`：`IsTemplateProject=true`，作为叶节点，不遍历其内部 `/工作表/收藏`。
- 其他拥有子节点的项目模板目录对象：`IsTemplateProject=false`，继续递归。

不得按名称排序，完整路径使用 `父路径 + " / " + 当前名称`。

`ValidateWorksheetAttributeIds` 复用现有 `ValidateAttributeIds` 的两条验证路径，但使用独立请求/响应类型，避免未来工作表校验语义与类型定义耦合。

`GetWorksheetCreationContext` 必须重新解析模板项目，定位唯一 `/工作表/收藏`，并返回模板项目完整路径、收藏完整路径和按 EB 原始顺序枚举的已有工作表名称。只读上下文读取失败时返回失败，不进行任何写入。

- [ ] **Step 6: 同步适配器 DataContract 模型**

在 `Adapters/AdapterProgram.cs` 底部增加与 `WorksheetModels.cs` 字段完全一致的：

```csharp
[DataContract]
internal sealed class ProjectTemplateIdentity
{
    [DataMember] public string Version;
    [DataMember] public string RootId;
    [DataMember] public string RootName;
}

[DataContract]
internal sealed class ProjectTemplateNode
{
    [DataMember] public string Id;
    [DataMember] public string Name;
    [DataMember] public string FullPath;
    [DataMember] public bool IsTemplateProject;
    [DataMember] public List<ProjectTemplateNode> Children = new List<ProjectTemplateNode>();
}

[DataContract]
internal sealed class ProjectTemplateTreeResult
{
    [DataMember] public ProjectTemplateIdentity Identity;
    [DataMember] public List<ProjectTemplateNode> Nodes = new List<ProjectTemplateNode>();
}

[DataContract]
internal sealed class ValidateWorksheetAttributeIdsRequest
{
    [DataMember] public List<int> AttributeIds = new List<int>();
}

[DataContract]
internal sealed class ValidateWorksheetAttributeIdsResult
{
    [DataMember] public List<int> ExistingIds = new List<int>();
    [DataMember] public List<int> MissingIds = new List<int>();
}

[DataContract]
internal sealed class WorksheetCreationContextRequest
{
    [DataMember] public string TemplateProjectId;
}

[DataContract]
internal sealed class WorksheetCreationContextResult
{
    [DataMember] public string TemplateProjectPath;
    [DataMember] public string TargetFolderPath;
    [DataMember] public List<string> ExistingWorksheetNames = new List<string>();
}
```

所有列表字段在声明处初始化为 `new List<T>()`。

- [ ] **Step 7: 运行测试和完整构建**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
.\build.ps1
```

Expected: 测试通过；主程序和 2023/2024/2025 适配器构建成功。

- [ ] **Step 8: 在活动 EB 上只读验证树**

直接调用当前活动版本适配器：

```powershell
& .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe GetProjectTemplateIdentity
& .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe GetProjectTemplateTree
```

Expected: 返回项目模板根 ID；树保持 EB 顺序；模板项目为叶节点；结果不包含模板项目内部“工作表”“收藏”和已有工作表。

- [ ] **Step 9: 提交只读树和缓存**

```powershell
git add ProjectTemplateCache.cs Tests/ProjectTemplateCacheTests.cs EbAdapterClient.cs Adapters/AdapterProgram.cs
git commit -m "feat: read and cache project template tree"
```

### Task 5: 实现项目模板窗口和主界面入口

**Files:**
- Create: `ProjectTemplatesForm.cs`
- Modify: `MainForm.cs`

- [ ] **Step 1: 创建项目模板窗口**

`ProjectTemplatesForm` 按 `AttributeFoldersForm` 的连接和多活动版本选择模式实现，但行为固定为：

- 标题：“工作表”
- 工具栏按钮：“刷新”
- 首次连接先调用 `GetProjectTemplateIdentityAsync`
- 有缓存则立即显示缓存
- 无缓存或用户点击“刷新”时调用 `GetProjectTemplateTreeAsync` 并覆盖缓存
- `DisplayTree` 结束后调用 `_tree.CollapseAll()`
- 右键时仅当 `ProjectTemplateNode.IsTemplateProject` 为 `true` 才显示“新建工作表”
- 创建完成后不刷新树和缓存

关键右键判断：

```csharp
private void TreeNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
{
    if (e.Button != MouseButtons.Right) return;
    _tree.SelectedNode = e.Node;
    if (e.Node.Tag is ProjectTemplateNode { IsTemplateProject: true })
        _menu.Show(_tree, e.Location);
}
```

- [ ] **Step 2: 把主界面按钮接入非模态窗口**

在 `MainForm.OpenFunction` 中增加与现有属性/类型定义一致的分支：

```csharp
if (functionName == "工作表")
{
    var form = new ProjectTemplatesForm();
    _openWindows.Add(form);
    form.FormClosed += (_, _) => _openWindows.Remove(form);
    form.Show();
    return;
}
```

保持主窗口和其他功能窗口可同时操作。

- [ ] **Step 3: 构建并人工验证树窗口**

Run:

```powershell
dotnet build .\EBAssistant.csproj
```

Expected: 构建成功。打开主界面后，工作表窗口非模态；首次读取后写缓存；关闭再打开从缓存加载；默认折叠；点击刷新才重读 EB；目录节点无右键创建入口，模板项目节点有。

- [ ] **Step 4: 提交项目模板窗口**

```powershell
git add ProjectTemplatesForm.cs MainForm.cs
git commit -m "feat: add project template worksheet window"
```

### Task 6: 完成一次性真实 EB 工作表能力验证

**Files:**
- Modify: `WorksheetModels.cs`
- Modify: `EbAdapterClient.cs`
- Modify: `Adapters/AdapterProgram.cs`
- Create: `docs/superpowers/verification/2026-06-13-worksheet-create-capability.md`

- [ ] **Step 1: 增加仅开发阶段显式调用的验证协议**

在主程序和适配器两侧定义：

```csharp
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
```

适配器操作名固定为 `ValidateWorksheetCreationCapability`。主程序 UI 不调用它；只通过命令行显式调用。

- [ ] **Step 2: 实现安全定位 `/工作表/收藏`**

在适配器中通过 `app.Utils.GetSnglObjectByID(request.TemplateProjectId)` 重新解析模板项目并强制转换为 `Project`。按 EB 返回顺序遍历其子对象，定位名称为“工作表”的容器，再定位名称为“收藏”的目标目录。任何一级不存在、重名或对象不是可保存目标时返回失败，不创建临时对象。

- [ ] **Step 3: 实现临时创建、列配置和保存**

临时名称使用明确前缀：

```csharp
var temporaryName = "__EBAssistant_WorksheetCapability_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
```

创建配方以强类型公共 COM 为唯一允许路径：

```csharp
var worksheet = templateProject.OpenWorksheetDirect(
    AucObjectKind.aucObjDevice,
    AucAttribute.aucAttrUnspecified,
    AucVbFindCondition.aucCondEqual,
    "");

var first = worksheet.Attributes.Add((AucAttribute)request.AttributeIds[0], 0);
var second = worksheet.Attributes.Add((AucAttribute)request.AttributeIds[1], 1);
first.Width = WorksheetWidth("设备名称");
second.Width = WorksheetWidth("这是用于验证自动列宽的较长列标签");
worksheet.ProtectColumnWidth = true;
worksheet.SaveConfiguration(temporaryName, favoritesFolder);
```

其中适配器内 `WorksheetWidth` 使用与主程序完全相同的算法，不能引用主程序程序集。

- [ ] **Step 4: 验证自定义列标签写入的准确公共 API 配方**

先读取并记录新列的 `AttributeID`、`Position`、`Name`、`AttributeName` 和 `Width`。然后仅尝试 EngineeringBaseCodemap 或已安装 EB SDK 中能够以强类型调用、且不会弹交互框的公开 API 配方。

通过标准必须同时满足：

- 第一列读回显示标签为“设备名称”
- 第二列读回显示标签为“这是用于验证自动列宽的较长列标签”
- 标签与底层属性名称可明确区分
- 不使用 `dynamic`、反射调用隐藏成员或 UI 自动化

若没有满足条件的公开 API，立即关闭临时 Worksheet、清理已保存临时配置并将 `Passed=false`；停止 Task 7 及后续正式写入任务，向用户报告“公开强类型 API 未确认列标签写入能力”。

- [ ] **Step 5: 读回验证保存结果**

保存后通过目标“收藏”目录重新枚举并解析唯一临时工作表配置，打开该配置并验证：

- 保存路径为所选模板项目 `/工作表/收藏`
- 名称等于临时名称
- 对象类型为器件
- AID 顺序与请求一致
- 显示标签与请求一致
- 每列 `Width` 等于计算值，长标签在 EB 界面完整显示

每项检查写入 `Checks`。

- [ ] **Step 6: 清理并确认无残留**

在 `finally` 中关闭 Worksheet；仅删除通过临时名称和对象 ID 双重确认的临时配置对象；再次枚举“收藏”，确认临时名称和 ID 均不存在。清理过程写入 `CleanupChecks`；清理未确认时结果必须为失败并突出显示残留对象 ID。

- [ ] **Step 7: 构建并显式执行一次验证**

Run:

```powershell
.\build.ps1
$templateProjectId = Read-Host "输入从 GetProjectTemplateTree 只读结果中确认的模板项目 ID"
$request = @{ TemplateProjectId = $templateProjectId; AttributeIds = @(5, 25) } | ConvertTo-Json -Compress
$request | & .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe ValidateWorksheetCreationCapability
```

Expected: `Passed=true`，全部创建、标签、顺序、列宽、保存位置、读回和清理检查通过；“收藏”目录无临时残留。

- [ ] **Step 8: 记录准确验证证据**

创建 `docs/superpowers/verification/2026-06-13-worksheet-create-capability.md`，记录：

- EB 版本
- 使用的模板项目路径和 ID
- 实际成功的强类型 API 调用顺序
- 列标签写入的准确公开 API
- 列宽计算值和界面确认结果
- 临时对象 ID
- 清理读回结果

不得记录业务敏感数据。

- [ ] **Step 9: 提交能力验证**

```powershell
git add WorksheetModels.cs EbAdapterClient.cs Adapters/AdapterProgram.cs docs/superpowers/verification/2026-06-13-worksheet-create-capability.md
git commit -m "test: verify worksheet creation capability"
```

### Task 7: 实现正式批量创建工作表适配器操作

**前置门槛:** Task 6 的验证文档必须明确 `Passed=true` 并记录列标签写入配方；否则不执行本任务。

**Files:**
- Modify: `EbAdapterClient.cs`
- Modify: `Adapters/AdapterProgram.cs`

- [ ] **Step 1: 增加正式创建客户端调用**

在 `EbAdapterClient.cs` 增加：

```csharp
public Task<AdapterResponse<CreateWorksheetsResult>> CreateWorksheetsAsync(CreateWorksheetsRequest request) =>
    InvokeAsync<CreateWorksheetsResult>(_adapterPath, "CreateWorksheets", request);
```

- [ ] **Step 2: 增加适配器路由和 DataContract 模型**

路由：

```csharp
if (operation == "CreateWorksheets") return Write(CreateWorksheets(app, Read<CreateWorksheetsRequest>()));
```

在 `Adapters/AdapterProgram.cs` 底部定义与 `WorksheetModels.cs` 完全一致的 `CreateWorksheetsRequest`、`CreateWorksheetItem`、`CreateWorksheetColumnItem`、`WorksheetOperationRecord` 和 `CreateWorksheetsResult`。

- [ ] **Step 3: 实现目标重新解析和已有名称收集**

正式操作开始时：

1. 重新解析 `TemplateProjectId` 为模板 `Project`。
2. 重新定位唯一 `/工作表/收藏`。
3. 枚举收藏下已有工作表名称，使用 `StringComparer.OrdinalIgnoreCase` 建立保留集合。
4. 对请求中的每个有效工作表按请求顺序调用同等算法计算最终名称；每算出一个名称立刻加入保留集合，避免批次内冲突。

连接中断、模板项目不存在或收藏目录定位失败时，停止后续创建，并为剩余项写入 `Status="unprocessed"`。

- [ ] **Step 4: 实现逐工作表创建**

对每个请求项单独 `try/catch/finally`：

1. 使用 `templateProject.OpenWorksheetDirect(AucObjectKind.aucObjDevice, AucAttribute.aucAttrUnspecified, AucVbFindCondition.aucCondEqual, "")` 创建空器件工作表。
2. 按 `Position` 顺序调用 `worksheet.Attributes.Add((AucAttribute)column.AttributeId, column.Position)`。
3. 使用 Task 6 已验证的准确公共 API 设置 `column.Label`。
4. 设置 `WorksheetAttribute.Width = column.Width`。
5. 设置 `worksheet.ProtectColumnWidth = true`。
6. 调用 `worksheet.SaveConfiguration(finalName, favoritesFolder)`。
7. 保存成功后记录完整路径，格式为“项目模板 / 模板项目完整路径 / 工作表 / 收藏 / 最终工作表名称”。
8. `finally` 中调用 `worksheet.Close()`。

任一列配置、标签、列宽或保存步骤失败时，该工作表记录 `Status="failed"`，继续下一个工作表。正式流程不创建临时验证对象、不重复验证列宽。

- [ ] **Step 5: 设置稳定结果状态**

每条记录只使用：

```text
created
failed
validation_skipped
unprocessed
```

汇总状态：

- 全部创建：`completed`
- 有创建也有失败/跳过：`partial`
- 无任何创建：`failed`

- [ ] **Step 6: 完整构建并真实小批量验证**

Run:

```powershell
.\build.ps1
```

使用一个脱敏的两页签样例在受控模板项目中验证：

- 两个工作表均保存到“收藏”
- 对象类型为器件
- 列顺序、标签和列宽正确
- 同名时使用最小可用后缀
- 单个失败不阻止后续工作表

- [ ] **Step 7: 提交正式适配器写入**

```powershell
git add EbAdapterClient.cs Adapters/AdapterProgram.cs
git commit -m "feat: create device worksheets in project templates"
```

### Task 8: 实现新建工作表导入预览窗口

**Files:**
- Create: `CreateWorksheetsForm.cs`
- Modify: `ProjectTemplatesForm.cs`

- [ ] **Step 1: 创建非模态新建工作表窗口**

构造函数接收 `EbAdapterClient` 和选中的 `ProjectTemplateNode`。界面包含：

- 只读目标模板项目路径
- “导入表格”
- “确定”
- 只读预览表格

预览使用一行一个 Excel 页签，并至少显示：

```text
SheetIndex, OriginalName, FinalName, ColumnCount, ColumnsSummary, ObjectType, AutoWidthStatus, Validation
```

其中 `ColumnsSummary` 格式为：

```text
1:设备名称(AID=5); 2:说明(AID=25)
```

- [ ] **Step 2: 实现导入和本地校验**

“导入表格”只允许 `.xlsx`、`.xls`。调用：

```csharp
var rawSheets = WorksheetExcelImporter.Read(dialog.FileName);
_sheets = WorksheetImportBuilder.Build(rawSheets);
```

Excel 无法读取时清空预览、禁用“确定”并显示错误。

- [ ] **Step 3: 实现 EB AID 校验和最终名称预览**

先调用 `GetWorksheetCreationContextAsync(_templateProject.Id)`，使用返回的 `ExistingWorksheetNames` 和当前批次前面已经保留的名称，按页签顺序调用 `WorksheetNameResolver.Resolve` 计算并显示每个有效页签的 `FinalName`；使用返回的 `TargetFolderPath` 显示真实保存位置。

然后收集本地校验通过页签的所有正整数 AID，调用 `ValidateWorksheetAttributeIdsAsync`。对缺失 AID：

- 对应列 `Validation="EB 中不存在该属性 ID"`
- 整页签 `Validation` 包含具体 AID 和 Excel 列号

本地与 EB 校验均通过的页签标记为 `Validation="有效"`。预览最终名称基于导入时的当前收藏内容计算；适配器必须在写入时基于最新收藏目录再次计算，并以适配器返回的 `FinalName` 为最终结果。

“确定”启用条件：至少有一个有效页签；无效页签不阻止其他有效页签。

- [ ] **Step 4: 实现确认创建请求**

用户点击“确定”后，把有效页签转换为：

```csharp
new CreateWorksheetItem
{
    SheetIndex = sheet.SheetIndex,
    OriginalName = sheet.OriginalName,
    RequestedName = sheet.OriginalName,
    Columns = sheet.Columns.Select(column => new CreateWorksheetColumnItem
    {
        Position = column.Position,
        Label = column.Label,
        AttributeId = column.AttributeId,
        Width = column.Width
    }).ToList()
}
```

无效页签先转换成 `WorksheetOperationRecord`，状态为 `validation_skipped`。调用适配器后将跳过记录与适配器记录按 `SheetIndex` 合并。

- [ ] **Step 5: 从项目模板树右键打开窗口**

在 `ProjectTemplatesForm` 的“新建工作表”处理器中创建并 `Show()` `CreateWorksheetsForm`；使用子窗口列表保留引用。窗口关闭后不调用刷新、不修改项目模板缓存。

- [ ] **Step 6: 构建并人工验证导入**

Run:

```powershell
dotnet build .\EBAssistant.csproj
```

Expected:

- `.xlsx` 和 `.xls` 都能读取全部页签
- 第一列忽略、第三行以后忽略
- 页签和列顺序保持
- 无效页签明确显示原因并跳过
- 有效页签可继续创建
- 创建后项目模板树不刷新

- [ ] **Step 7: 提交导入预览窗口**

```powershell
git add CreateWorksheetsForm.cs ProjectTemplatesForm.cs
git commit -m "feat: add worksheet import preview workflow"
```

### Task 9: 实现工作表创建结果和日志

**Files:**
- Create: `WorksheetCreationLogWriter.cs`
- Create: `WorksheetCreationResultForm.cs`
- Modify: `CreateWorksheetsForm.cs`

- [ ] **Step 1: 实现双格式日志**

`WorksheetCreationLogWriter.Write` 接收 `CreateWorksheetsResult`，创建：

```text
%LOCALAPPDATA%\EBAssistant\Logs\Worksheets
```

文件名：

```text
worksheet-create-yyyyMMdd-HHmmss.json
worksheet-create-yyyyMMdd-HHmmss.txt
```

JSON 使用缩进格式。TXT 每条记录输出：

```text
状态 | Excel页签 | 最终名称 | 保存路径 | 对象类型 | 列数 | 自动列宽 | 消息
```

方法返回日志目录；日志写入失败时抛出异常，由界面明确提示，但不得改变 EB 已完成结果。

- [ ] **Step 2: 实现结果窗口**

`WorksheetCreationResultForm` 使用只读 `DataGridView` 展示所有记录，顶部汇总 `created`、`failed`、`validation_skipped`、`unprocessed` 数量，底部按钮文字固定为“打开日志目录”，通过：

```csharp
Process.Start(new ProcessStartInfo { FileName = logDirectory, UseShellExecute = true });
```

打开目录。

- [ ] **Step 3: 在每次确认后保存并显示结果**

`CreateWorksheetsForm` 在每次用户点击“确定”后，无论创建全部成功、部分成功或全部失败，都：

1. 合并校验跳过记录和适配器记录。
2. 调用 `WorksheetCreationLogWriter.Write`。
3. `Show()` 非模态 `WorksheetCreationResultForm`。
4. 日志写入失败时显示单独错误消息，结果窗口仍显示记录。

- [ ] **Step 4: 构建并验证日志一致性**

Run:

```powershell
dotnet build .\EBAssistant.csproj
```

Expected: 每次确认后均出现结果窗口；JSON、TXT 和窗口记录数量及状态一致；按钮可打开正确目录；每个成功项显示完整“/工作表/收藏/最终工作表名称”路径和自动列宽结果。

- [ ] **Step 5: 提交结果和日志**

```powershell
git add WorksheetCreationLogWriter.cs WorksheetCreationResultForm.cs CreateWorksheetsForm.cs
git commit -m "feat: log worksheet creation results"
```

### Task 10: 文档、回归和最终验收

**Files:**
- Modify: `README.md`
- Modify only if required by direct worksheet changes: `AGENTS.md`

- [ ] **Step 1: 更新用户文档**

在 `README.md` 增加“工作表”章节，明确：

- 项目模板树使用缓存，只有“刷新”重读 EB
- 仅模板项目可右键“新建工作表”
- 每个 Excel 页签对应一个工作表
- 第一列忽略；第二列起第一行为标签、第二行为 AID
- 工作表固定为器件并保存到 `/工作表/收藏`
- 日志目录 `%LOCALAPPDATA%\EBAssistant\Logs\Worksheets`

- [ ] **Step 2: 运行全部纯逻辑测试**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: 所有测试组通过。

- [ ] **Step 3: 运行完整构建**

Run:

```powershell
.\build.ps1
```

Expected: 主程序、EB2023、EB2024 和 EB2025 占位适配器全部构建成功。

- [ ] **Step 4: 扫描禁止项和占位文本**

Run:

```powershell
rg -n "\bdynamic\b|TODO|TBD|implement later|fill in details" . -g "*.cs" -g "*.md" -g "!EngineeringBaseCodemap/**"
```

Expected: 新增工作表代码不含 `dynamic` 和占位文本；已有无关命中只记录，不顺带修改。

- [ ] **Step 5: 检查新增文本文件编码**

使用 PowerShell 读取新增文本文件前三字节，确认不是 UTF-8 BOM `EF BB BF`；对所有包含中文的新增文件确认以 UTF-8 无 BOM 正确读取，不出现 Unicode 替换字符 `U+FFFD`。

- [ ] **Step 6: 完成真实 EB 验收**

在 EB2023 和可用的 EB2024 中分别验证：

1. 主界面可同时打开“属性”“类型定义”“工作表”。
2. 工作表树首次读取并缓存，后续秒开，刷新才重读。
3. 树顺序和 EB 一致，默认折叠，不显示模板项目内部工作表。
4. 两页签 Excel 创建两个器件工作表到所选模板项目 `/工作表/收藏`。
5. 标签、AID、列顺序和自动列宽正确。
6. 同名使用最小可用后缀。
7. 一个无效页签被跳过，其他有效页签继续。
8. 一个创建失败后，后续工作表继续。
9. 结果窗口、JSON 和 TXT 日志一致。
10. 创建后项目模板树和缓存不刷新。

- [ ] **Step 7: 查看变更范围**

Run:

```powershell
git status --short
git diff --stat
git diff --check
```

Expected: `git diff --check` 无错误；每个工作表相关改动都能追溯到本计划；用户原有未提交修改仍保留。

- [ ] **Step 8: 提交文档和最终回归修改**

```powershell
git add README.md
git commit -m "docs: document worksheet creation workflow"
```

## 完成标准

- “工作表”窗口独立非模态，项目模板树默认折叠并按身份缓存。
- 只有刷新按钮重新读取 EB；创建工作表不刷新树或缓存。
- 只有模板项目节点可右键“新建工作表”。
- Excel 全页签、第二列起、第一行标签、第二行 AID 的规则正确实现。
- 无效页签跳过，其他有效页签继续；AID 在当前 EB 中准确校验。
- 正式创建固定使用器件类型，保存到所选模板项目 `/工作表/收藏`。
- 自定义列标签、列顺序和自动列宽经过一次性真实 EB 能力验证，正式流程不重复探测。
- 同名使用最小可用后缀；单个创建失败不阻止后续工作表。
- 每次确认自动保存 JSON/TXT 日志并显示结果窗口和日志路径按钮。
- 主程序、全部适配器构建通过；新增工作表代码无 `dynamic`；新增文本为 UTF-8 无 BOM。
