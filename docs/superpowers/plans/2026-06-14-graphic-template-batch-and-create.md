# Graphic Template Batch Migration Preview And Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only Excel migration preview and a controlled workflow that creates 2-200 template graphics by copying the only template in a selected leaf directory.

**Architecture:** Keep Excel parsing, cache matching, quantity validation, and result summarization in small main-program classes that can be tested without EB. Add one adapter JSON operation that performs all `n-1` copy/paste iterations in a single EB connection, confirms each new object by ID, stops on the first failure, and reuses the existing mismatch-dialog watcher.

**Tech Stack:** C# WinForms `net10.0-windows`, ExcelDataReader, JSON stdin/stdout adapter protocol, EB 2023/2024 strong-typed COM adapter on `.NET Framework 4.6.2`, existing console test harness.

---

### Task 1: Define Batch Preview Models And Cache Matching

**Files:**
- Create: `GraphicTemplateBatchMigration.cs`
- Modify: `GraphicTemplateModels.cs`
- Create: `Tests/GraphicTemplateBatchMigrationTests.cs`
- Modify: `Tests/Program.cs`

- [ ] **Step 1: Write failing cache-matching tests**

Add `GraphicTemplateBatchMigrationTests.Run()` covering:

```csharp
var tree = new GraphicTemplateTreeResult
{
    Nodes =
    [
        new GraphicTemplateDirectoryNode
        {
            Name = "器件",
            FullPath = "图形模版 / 器件",
            Children =
            [
                new GraphicTemplateDirectoryNode
                {
                    Name = "常规",
                    FullPath = "图形模版 / 器件 / 常规",
                    Templates =
                    [
                        new GraphicTemplateItem { Name = "A" },
                        new GraphicTemplateItem { Name = "A" }
                    ]
                }
            ]
        }
    ]
};

Assert.Equal("器件/常规", GraphicTemplateBatchMigration.NormalizeRelativePath(" 器件 / 常规 "));
Assert.Equal(1, GraphicTemplateBatchMigration.FindDirectories(tree, "器件/常规").Count);
Assert.Equal(2, GraphicTemplateBatchMigration.MatchTemplateCount(tree, "器件/常规", "A"));
```

Also test missing directories, non-leaf directories, blank fields, unique matching template, and duplicate matching templates.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj -p:BaseOutputPath="$env:TEMP\EBAssistantTests\"
```

Expected: build failure because `GraphicTemplateBatchMigration` and its preview models do not exist.

- [ ] **Step 3: Add minimal models and matcher**

Add these main-program models to `GraphicTemplateModels.cs`:

```csharp
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
```

Create `GraphicTemplateBatchMigration` with:

```csharp
public static string NormalizeRelativePath(string path);
public static List<GraphicTemplateDirectoryNode> FindDirectories(GraphicTemplateTreeResult tree, string relativePath);
public static int MatchTemplateCount(GraphicTemplateTreeResult tree, string sourcePath, string templateName);
public static List<GraphicTemplateBatchPreviewRow> BuildPreview(
    IEnumerable<GraphicTemplateBatchRawRow> rows,
    GraphicTemplateTreeResult tree);
```

`BuildPreview` must require one source leaf directory, one target leaf directory, and exactly one matching template name.

- [ ] **Step 4: Run tests and verify GREEN**

Run the test command from Step 2.

Expected: all test groups pass and the reported group count increases by one.

- [ ] **Step 5: Commit**

```powershell
git add GraphicTemplateBatchMigration.cs GraphicTemplateModels.cs Tests/GraphicTemplateBatchMigrationTests.cs Tests/Program.cs
git commit -m "test: define graphic template batch preview matching"
```

### Task 2: Add Excel Import And Read-Only Preview Window

**Files:**
- Create: `GraphicTemplateBatchExcelImporter.cs`
- Create: `GraphicTemplateBatchMigrationForm.cs`
- Create: `Tests/GraphicTemplateBatchExcelImporterTests.cs`
- Modify: `Tests/Program.cs`
- Modify: `GraphicTemplatesForm.cs`

- [ ] **Step 1: Write failing Excel importer test**

Create a small `.xlsx` fixture at runtime using `System.IO.Compression` with rows equivalent to:

```text
序号 | 模板图形目录 | 模板图形名称 | 目标目录
1    | 器件/常规   | A            | 器件/仪表
     |             |              |
```

Assert that `GraphicTemplateBatchExcelImporter.Read(path)` returns one row with `ExcelRowNumber == 2`, reads A-D correctly, and ignores the empty row.

- [ ] **Step 2: Run tests and verify RED**

Run the full test command.

Expected: build failure because `GraphicTemplateBatchExcelImporter` does not exist.

- [ ] **Step 3: Implement Excel importer**

Create:

```csharp
public static class GraphicTemplateBatchExcelImporter
{
    public static List<GraphicTemplateBatchRawRow> Read(string path);
}
```

Use `ExcelDataReader`, read the first worksheet, skip row 1, read columns A-D, trim values, and ignore rows where B-D are all blank.

- [ ] **Step 4: Implement preview window and toolbar entry**

Create `GraphicTemplateBatchMigrationForm`:

- Constructor accepts the current `GraphicTemplateTreeResult` cache.
- “导入表格” opens `.xlsx/.xls`.
- Imported rows pass through `GraphicTemplateBatchMigration.BuildPreview`.
- Read-only `DataGridView` displays row, sequence, source path, name, target path, match count, and validation.
- “确定” is enabled after a successful import and only displays `按表格迁移功能暂未开发。`
- The window does not invoke EB and does not write an operation log.

Modify `GraphicTemplatesForm`:

- Add “按表格迁移” to the toolbar.
- Open the form non-modally and retain it in `_childWindows`.
- Disable the button while the main window is busy.

- [ ] **Step 5: Run tests and build**

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj -p:BaseOutputPath="$env:TEMP\EBAssistantTests\"
dotnet build .\EBAssistant.csproj
```

Expected: all tests pass; build succeeds with zero errors.

- [ ] **Step 6: Commit**

```powershell
git add GraphicTemplateBatchExcelImporter.cs GraphicTemplateBatchMigrationForm.cs GraphicTemplatesForm.cs Tests/GraphicTemplateBatchExcelImporterTests.cs Tests/Program.cs
git commit -m "feat: add graphic template batch migration preview"
```

### Task 3: Define Quantity Validation And Creation Result Logging

**Files:**
- Modify: `GraphicTemplateModels.cs`
- Create: `GraphicTemplateCreation.cs`
- Create: `GraphicTemplateCreationLogWriter.cs`
- Create: `GraphicTemplateCreationResultForm.cs`
- Create: `Tests/GraphicTemplateCreationTests.cs`
- Modify: `Tests/Program.cs`

- [ ] **Step 1: Write failing validation and result tests**

Test:

```csharp
Assert.True(!GraphicTemplateCreation.TryParseTotalCount("1", out _, out _));
Assert.True(GraphicTemplateCreation.TryParseTotalCount("2", out var minimum, out _));
Assert.Equal(2, minimum);
Assert.True(GraphicTemplateCreation.TryParseTotalCount("200", out var maximum, out _));
Assert.Equal(200, maximum);
Assert.True(!GraphicTemplateCreation.TryParseTotalCount("201", out _, out _));
Assert.True(!GraphicTemplateCreation.TryParseTotalCount("abc", out _, out _));
```

Test result summary:

```csharp
var completed = new CreateGraphicTemplatesResult { RequestedTotalCount = 3, CreatedCount = 2 };
GraphicTemplateCreation.ApplySummary(completed);
Assert.Equal("completed", completed.Status);

var partial = new CreateGraphicTemplatesResult { RequestedTotalCount = 4, CreatedCount = 1 };
GraphicTemplateCreation.ApplySummary(partial);
Assert.Equal("partial", partial.Status);

var failed = new CreateGraphicTemplatesResult { RequestedTotalCount = 4, CreatedCount = 0 };
GraphicTemplateCreation.ApplySummary(failed);
Assert.Equal("failed", failed.Status);
```

Also verify JSON/TXT logs are written as separate timestamped files under a supplied temporary directory.

- [ ] **Step 2: Run tests and verify RED**

Run the full test command.

Expected: build failure because creation models and helpers do not exist.

- [ ] **Step 3: Add main-program creation models**

Add:

```csharp
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
```

Implement `GraphicTemplateCreation.TryParseTotalCount` and `ApplySummary`.

- [ ] **Step 4: Add creation log writer and result form**

Write timestamped JSON/TXT files under:

```text
%LOCALAPPDATA%\EBAssistant\Logs\GraphicTemplates
```

Use file stem `graphic-template-creation-yyyyMMdd-HHmmss-fff`.

The result form displays status, requested total, created count, confirmed final count, per-copy records, and an “打开日志目录” button.

- [ ] **Step 5: Run tests and verify GREEN**

Run the full test command.

Expected: all tests pass and the group count increases.

- [ ] **Step 6: Commit**

```powershell
git add GraphicTemplateModels.cs GraphicTemplateCreation.cs GraphicTemplateCreationLogWriter.cs GraphicTemplateCreationResultForm.cs Tests/GraphicTemplateCreationTests.cs Tests/Program.cs
git commit -m "test: define graphic template creation results"
```

### Task 4: Add Adapter Batch-Copy Operation

**Files:**
- Modify: `EbAdapterClient.cs`
- Modify: `Adapters/AdapterProgram.cs`

- [ ] **Step 1: Add the main-program client call**

Add:

```csharp
public Task<AdapterResponse<CreateGraphicTemplatesResult>> CreateGraphicTemplatesAsync(CreateGraphicTemplatesRequest request) =>
    InvokeAsync<CreateGraphicTemplatesResult>(_adapterPath, "CreateGraphicTemplates", request);
```

- [ ] **Step 2: Add synchronized adapter protocol models**

At the bottom of `Adapters/AdapterProgram.cs`, add `[DataContract]` equivalents for:

- `CreateGraphicTemplatesRequest`
- `GraphicTemplateCreationRecord`
- `CreateGraphicTemplatesResult`

Add operation dispatch:

```csharp
if (operation == "CreateGraphicTemplates")
    return Write(CreateGraphicTemplates(app, Read<CreateGraphicTemplatesRequest>()));
```

- [ ] **Step 3: Implement adapter prevalidation**

`CreateGraphicTemplates` must fail before copying unless:

- `RequestedTotalCount` is 2-200.
- Directory exists and is a leaf graphic-template directory.
- Source template exists, is a graphic template, and belongs to the directory.
- Directory currently contains exactly one graphic template.

Populate directory path, source name, requested count, and original count in the result.

- [ ] **Step 4: Implement copy/paste loop**

Within one `RunWithGraphicTemplateMismatchDialog` call:

1. Execute `aucCmdSymCopy` once on the source.
2. Repeat `aucCmdSymPaste` on the directory `RequestedTotalCount - 1` times.
3. Before each paste, capture direct template IDs.
4. After each paste, re-enumerate direct templates and require exactly one new ID.
5. Add a `created` record with the confirmed ID.
6. On the first exception or readback failure, add one `failed` record and stop.
7. Re-enumerate the directory once more to set `ConfirmedFinalCount`.
8. Return success only for `completed`; return `FailWithData` for `partial` and `failed`.

Reuse the existing known-dialog Win32 watcher. Do not click unknown dialogs and do not delete created objects.

- [ ] **Step 5: Compile adapters**

Build the 2023 and 2024 adapter projects using Visual Studio MSBuild when available. If unavailable, use the existing Roslyn `csc.dll` command with `.NET Framework v4.6.2` references and the existing `Interop.Aucotec.dll` for each version.

Expected: both adapter EXEs compile and their timestamps update.

- [ ] **Step 6: Commit**

```powershell
git add EbAdapterClient.cs Adapters/AdapterProgram.cs
git commit -m "feat: add adapter graphic template creation operation"
```

### Task 5: Add Quantity Dialog And Wire Creation Workflow

**Files:**
- Create: `GraphicTemplateCreationCountForm.cs`
- Modify: `GraphicTemplatesForm.cs`

- [ ] **Step 1: Implement the count input dialog**

Create a centered modal dialog containing:

- Numeric input constrained to `2-200`.
- Label explaining that the number includes the existing template graphic.
- “确定” and “取消” buttons.
- Public `TotalCount` property.

Use `NumericUpDown` so invalid non-numeric input cannot be submitted.

- [ ] **Step 2: Add “新建模板图形” toolbar button and eligibility check**

The button remains clickable whenever the window is not busy.

On click:

```csharp
var selected = _tree.SelectedNode?.Tag as GraphicTemplateDirectoryNode;
if (selected is null ||
    !GraphicTemplateSelection.IsLeafDirectory(selected) ||
    selected.Templates.Count != 1)
{
    MessageBox.Show(
        this,
        "需要选择最后一级目录，并且目录中只能存在一个模板图形。",
        "新建模板图形",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
    return;
}
```

- [ ] **Step 3: Invoke adapter and display result**

After the count dialog returns `OK`:

- Call `CreateGraphicTemplatesAsync`.
- If no result payload exists, show an error.
- Otherwise write creation logs and open `GraphicTemplateCreationResultForm`.
- Refresh only the selected directory with `RefreshDirectoryByIdAsync`.
- Restore non-busy state even when the adapter call or log writing fails.

- [ ] **Step 4: Run tests and build**

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj -p:BaseOutputPath="$env:TEMP\EBAssistantTests\"
dotnet build .\EBAssistant.csproj
git diff --check
```

Expected: all tests pass, build succeeds with zero errors, and diff check reports no errors.

- [ ] **Step 5: Commit**

```powershell
git add GraphicTemplateCreationCountForm.cs GraphicTemplatesForm.cs
git commit -m "feat: add graphic template creation workflow"
```

### Task 6: Final Verification In Real EB

**Files:**
- Modify only if verification reveals a defect directly related to this feature.

- [ ] **Step 1: Verify batch preview manually**

Open the main UI, enter “图形模板”, click “按表格迁移”, and import `Templates/迁移模板图形模板.xlsx`.

Verify:

- Preview is read-only.
- Relative paths match against cache.
- Duplicate template names display their match count and validation error.
- Clicking “确定” only shows the not-developed message.

- [ ] **Step 2: Verify creation preconditions**

Verify the exact warning appears for:

- No selected directory.
- Selected non-leaf directory.
- Selected leaf directory with zero templates.
- Selected leaf directory with more than one template.

- [ ] **Step 3: Verify controlled creation**

In a controlled leaf directory containing exactly one expendable template graphic:

- Request total count `2`.
- Confirm one new template appears.
- Confirm the current directory cache refreshes.
- Confirm JSON/TXT logs and result window are produced.

Then test a larger count and confirm the final count equals the requested total.

- [ ] **Step 4: Verify failure stop behavior**

Use a controlled scenario that triggers a paste failure or mismatch dialog:

- Confirm the known mismatch dialog is automatically clicked.
- Confirm the first failure stops later paste attempts.
- Confirm previously created templates remain.
- Confirm the result and logs report `partial` or `failed`.

- [ ] **Step 5: Run final automated verification**

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj -p:BaseOutputPath="$env:TEMP\EBAssistantTests\"
dotnet build .\EBAssistant.csproj
git diff --check
git status --short
```

Expected: all tests pass, build has zero errors, diff check is clean, and only intentional feature files are modified.
