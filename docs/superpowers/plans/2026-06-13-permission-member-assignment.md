# Permission Member Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add checked EB users and user groups to the `AccessPermissions` collections of checked permission directories without setting rights or stopping after individual failures.

**Architecture:** Keep selection, preview, result presentation, and logging in the WinForms application. Send only deduplicated member and directory IDs to the versioned x86 adapter. The adapter re-resolves every object, maps user-tree objects to `AccessControlUser` or `AccessControlGroup`, obtains each target directory's `AccessControlUsers` collection through one controlled late-bound boundary, adds with `Add` or `AddGroup`, and verifies each result by SID.

**Tech Stack:** C# WinForms `net10.0-windows` x86, EB 2023/2024 Aucotec COM interop `.NET Framework 4.6.2` x86, JSON stdin/stdout adapter protocol, existing console test runner.

---

## File Structure

- Modify `Adapters/AdapterProgram.cs`: add protocol dispatch, typed member resolution, controlled directory `AccessPermissions` access, add/skip/fail processing, and adapter-side protocol models.
- Modify `Adapters/2023/EBAssistant.Adapter2023.csproj`: retain the `Microsoft.CSharp` reference required by the controlled late-bound permission boundaries.
- Modify `Adapters/2024/EBAssistant.Adapter2024.csproj`: retain the same reference for COM31.
- Modify `Models.cs`: add shared request/result/record models.
- Modify `EbAdapterClient.cs`: expose `AddPermissionMembersAsync`.
- Create `PermissionAssignmentSelection.cs`: build a deduplicated request and preview rows from selected tree models while excluding the user/group root.
- Create `PermissionAssignmentPreviewForm.cs`: read-only confirmation dialog.
- Create `PermissionAssignmentResultForm.cs`: result grid, summary, and log-directory button.
- Create `PermissionAssignmentLogWriter.cs`: JSON/TXT operation logs.
- Modify `PermissionConfigurationForm.cs`: right-tree checkboxes, selection linkage, apply flow, refresh after write.
- Create `Tests/PermissionAssignmentSelectionTests.cs`: test root exclusion, deduplication, and combination count.
- Create `Tests/PermissionAssignmentResultTests.cs`: test result status and count aggregation.
- Modify `Tests/Program.cs`: run the new test groups.

## Confirmed EB Interop Signatures

The local EB2023 interop exposes:

```text
AccessControlUsers.Add(AccessControlUser)
AccessControlUsers.AddGroup(AccessControlGroup)
AccessControlUser.SID
AccessControlGroup.SID
Application.AccessControl.WinUsersAndGroups
Application.AccessControl.Groups
```

The right-side directory nodes are resolved as generic `ObjectItem` instances. Access to a directory's class-specific `AccessPermissions` property must remain inside one explicitly named controlled late-bound helper. No code in this feature may call `SetRight`, `Remove`, `RemoveUsers`, or delete methods.

### Task 1: Preserve And Commit The Permission-Read Runtime Fix

**Files:**
- Modify: `Adapters/AdapterProgram.cs`
- Modify: `Adapters/2023/EBAssistant.Adapter2023.csproj`
- Modify: `Adapters/2024/EBAssistant.Adapter2024.csproj`

- [ ] **Step 1: Verify the current permission-read fix is narrowly scoped**

Run:

```powershell
git diff --check
rg -n "ReadPermissionConfigurationFolders|0x80046951|dynamic folders|Microsoft.CSharp" Adapters
```

Expected: the only `0x80046951` catch treats user/group leaf `Children` access as an empty child collection; the only new `dynamic` use reads `AppFolders.UsersAndGroups` and `Messages`.

- [ ] **Step 2: Build the adapters with Visual Studio MSBuild**

Run:

```powershell
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' .\Adapters\2023\EBAssistant.Adapter2023.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' .\Adapters\2024\EBAssistant.Adapter2024.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
```

Expected: both adapters build with 0 errors.

- [ ] **Step 3: Verify the current live EB read operation**

Run:

```powershell
$raw = & .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe GetPermissionConfigurationStructure
$result = $raw | ConvertFrom-Json
if (-not $result.Success) { throw $result.Message }
$result.Message
```

Expected: `权限配置结构读取成功。`

- [ ] **Step 4: Commit the runtime fix separately**

```powershell
git add Adapters/AdapterProgram.cs Adapters/2023/EBAssistant.Adapter2023.csproj Adapters/2024/EBAssistant.Adapter2024.csproj
git commit -m "fix: read permission configuration user leaves"
```

### Task 2: Add Selection And Result Domain Logic With Tests

**Files:**
- Modify: `Models.cs`
- Create: `PermissionAssignmentSelection.cs`
- Create: `Tests/PermissionAssignmentSelectionTests.cs`
- Create: `Tests/PermissionAssignmentResultTests.cs`
- Modify: `Tests/Program.cs`

- [ ] **Step 1: Write failing selection tests**

Create tests covering:

```csharp
var root = new PermissionDirectoryNode { Id = "ROOT", Name = "用户及用户组" };
var user = new PermissionDirectoryNode { Id = "U1", Name = "User 1", FullPath = "用户及用户组 / User 1" };
var directory = new PermissionDirectoryNode { Id = "D1", Name = "项目", FullPath = "项目" };

var selection = PermissionAssignmentSelection.Build(
    root.Id,
    [root, user, user],
    [directory, directory]);

Assert.Equal(1, selection.Request.MemberIds.Count);
Assert.Equal("U1", selection.Request.MemberIds[0]);
Assert.Equal(1, selection.Request.DirectoryIds.Count);
Assert.Equal(1, selection.CombinationCount);
```

Also test that no members or no directories returns `CanApply == false`.

- [ ] **Step 2: Write failing result-status tests**

Test the exact aggregation:

```csharp
var result = new PermissionMemberAssignmentResult
{
    Records =
    [
        new() { Status = "added" },
        new() { Status = "skipped_existing" },
        new() { Status = "failed" }
    ]
};

PermissionAssignmentResultSummary.Apply(result);

Assert.Equal("completed_with_failures", result.Status);
Assert.Equal(1, result.AddedCount);
Assert.Equal(1, result.SkippedCount);
Assert.Equal(1, result.FailedCount);
Assert.Equal(3, result.TotalCount);
```

- [ ] **Step 3: Register the new test groups and verify failure**

Modify `Tests/Program.cs` to run:

```csharp
PermissionAssignmentSelectionTests.Run();
PermissionAssignmentResultTests.Run();
```

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: FAIL because the new production types do not exist.

- [ ] **Step 4: Implement minimal pure domain helpers**

Add the request/result/record models from Task 3, Step 1 to `Models.cs` first so the pure helpers and tests compile.

Create:

```csharp
public sealed class PermissionAssignmentSelectionResult
{
    public PermissionMemberAssignmentRequest Request { get; set; } = new();
    public List<PermissionDirectoryNode> Members { get; set; } = [];
    public List<PermissionDirectoryNode> Directories { get; set; } = [];
    public int CombinationCount => Members.Count * Directories.Count;
    public bool CanApply => Members.Count > 0 && Directories.Count > 0;
}
```

`PermissionAssignmentSelection.Build` must:

- exclude `usersAndGroupsRootId`;
- deduplicate both sets by ID using `StringComparer.OrdinalIgnoreCase`;
- preserve first-seen display order;
- return IDs in the same order as the preview models.

`PermissionAssignmentResultSummary.Apply` must calculate counts and set:

- `completed` when no record failed;
- `completed_with_failures` when at least one record failed.

Also implement:

```csharp
public static PermissionMemberAssignmentResult FromFailure(
    PermissionAssignmentSelectionResult selection,
    string message)
```

It must create one `failed` record per selected member/directory combination so an adapter-level failure still produces a complete user-visible result and log.

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

Expected: PASS with 7 test groups.

- [ ] **Step 6: Commit**

```powershell
git add Models.cs PermissionAssignmentSelection.cs Tests/PermissionAssignmentSelectionTests.cs Tests/PermissionAssignmentResultTests.cs Tests/Program.cs
git commit -m "test: define permission assignment selection behavior"
```

### Task 3: Add The Shared Adapter Protocol

**Files:**
- Modify: `EbAdapterClient.cs`
- Modify: `Adapters/AdapterProgram.cs`

- [ ] **Step 1: Verify the main-program models from Task 2**

Confirm `Models.cs` contains:

```csharp
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
```

- [ ] **Step 2: Add the client method**

Add to `EbAdapterClient`:

```csharp
public Task<AdapterResponse<PermissionMemberAssignmentResult>> AddPermissionMembersAsync(
    PermissionMemberAssignmentRequest request) =>
    InvokeAsync<PermissionMemberAssignmentResult>(_adapterPath, "AddPermissionMembers", request);
```

- [ ] **Step 3: Add adapter dispatch and synchronized DataContract models**

Add dispatch:

```csharp
if (operation == "AddPermissionMembers")
    return Write(AddPermissionMembers(app, Read<PermissionMemberAssignmentRequest>()));
```

Add matching `[DataContract]` request, result, and record types at the bottom of `AdapterProgram.cs`. Every field from `Models.cs` must have a matching `[DataMember]`.

- [ ] **Step 4: Build protocol consumers**

Run:

```powershell
dotnet build .\EBAssistant.csproj
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' .\Adapters\2023\EBAssistant.Adapter2023.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
```

Expected: both builds pass.

- [ ] **Step 5: Commit**

```powershell
git add EbAdapterClient.cs Adapters/AdapterProgram.cs
git commit -m "feat: add permission member assignment protocol"
```

### Task 4: Implement Adapter-Side Validation, Add, And Readback

**Files:**
- Modify: `Adapters/AdapterProgram.cs`

- [ ] **Step 1: Add controlled permission helper boundaries**

Implement helpers with these exact responsibilities:

```csharp
private static AccessControlUsers ReadAccessPermissions(ObjectItem directory)
{
    dynamic target = directory;
    AccessControlUsers permissions = target.AccessPermissions;
    return permissions ?? throw new InvalidOperationException("目标目录不支持 AccessPermissions。");
}

private static bool ContainsPermissionSid(AccessControlUsers permissions, string sid)
{
    foreach (object raw in permissions as IEnumerable)
    {
        var user = raw as AccessControlUser;
        if (user != null && string.Equals(user.SID, sid, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}
```

No other helper in this feature may use late binding for target-directory permissions.

- [ ] **Step 2: Resolve selected tree objects to access-control identities before writing**

Create an internal resolved-member type containing:

```csharp
internal sealed class ResolvedPermissionMember
{
    public string ObjectId;
    public string Name;
    public string Sid;
    public AccessControlUser User;
    public AccessControlGroup Group;
    public bool IsGroup;
}
```

Resolution rules:

- re-resolve the selected tree object with `app.Utils.GetSnglObjectByID(memberId)`;
- reject the users/groups root ID;
- for a user object, read `UserSID`, then find a matching entry in `app.AccessControl.WinUsersAndGroups`;
- for an EB group, find the matching `AccessControlGroup` in `app.AccessControl.Groups` by stable SID; use name only as a diagnostic fallback and fail if not unique;
- do not expand `AccessControlGroup.Members`;
- collect all invalid members before any call to `Add` or `AddGroup`.

- [ ] **Step 3: Resolve and prevalidate all selected directories before writing**

For every deduplicated directory ID:

- resolve with `app.Utils.GetSnglObjectByID`;
- verify it is a direct child of the current database root and not UsersAndGroups or Messages;
- call `ReadAccessPermissions` once to confirm the directory supports permission assignment;
- retain resolution failures as per-directory errors so all related combinations become `failed`;
- finish the full prevalidation pass before the first write.

- [ ] **Step 4: Implement per-combination processing**

For every requested member/directory pair:

```csharp
if (ContainsPermissionSid(permissions, member.Sid))
{
    record.Status = "skipped_existing";
    record.Message = "权限成员已存在，未重复添加。";
}
else
{
    if (member.IsGroup)
        permissions.AddGroup(member.Group);
    else
        permissions.Add(member.User);

    var readbackDirectory = app.Utils.GetSnglObjectByID(directory.Id) as ObjectItem;
    var readbackPermissions = ReadAccessPermissions(readbackDirectory);
    if (!ContainsPermissionSid(readbackPermissions, member.Sid))
        throw new InvalidOperationException("添加后未从 AccessPermissions 读回成员。");

    record.Status = "added";
    record.Message = "权限成员已添加并读回确认；未设置具体权限位。";
}
```

Wrap each combination independently. On exception, set `failed` and continue. Never call `SetRight`, `Remove`, `RemoveUsers`, or any delete API.

- [ ] **Step 5: Finalize result counts and messages inside the adapter**

Calculate `TotalCount`, `AddedCount`, `SkippedCount`, and `FailedCount` directly from the adapter records. The adapter must return `Success=true` after a valid request is processed, even when individual records failed. Use `Status=completed_with_failures` and per-record failures for partial results. Return `Success=false` only when the request itself is empty/unparseable or the adapter cannot establish the EB context.

- [ ] **Step 6: Build both version adapters**

Run:

```powershell
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' .\Adapters\2023\EBAssistant.Adapter2023.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' .\Adapters\2024\EBAssistant.Adapter2024.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal
```

Expected: both pass with 0 errors.

- [ ] **Step 7: Run a read-only invalid-request check**

Run:

```powershell
'{"MemberIds":[],"DirectoryIds":[]}' | .\bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe AddPermissionMembers
```

Expected: `Success=false`, message states no members/directories were supplied, and EB data is unchanged.

- [ ] **Step 8: Commit**

```powershell
git add Adapters/AdapterProgram.cs
git commit -m "feat: add permission members in EB adapter"
```

### Task 5: Add Preview, Logging, And Result Presentation

**Files:**
- Create: `PermissionAssignmentPreviewForm.cs`
- Create: `PermissionAssignmentResultForm.cs`
- Create: `PermissionAssignmentLogWriter.cs`

- [ ] **Step 1: Implement the read-only preview form**

The form must show:

- selected members with full paths;
- selected directories with full paths;
- exact combination count;
- warning text: `仅添加权限成员，不设置具体权限位；单项失败后继续；不自动回滚。`
- `确认应用` and `取消` buttons.

Set `DialogResult.OK` only on `确认应用`.

- [ ] **Step 2: Implement result form**

Bind `result.Records` to a read-only `DataGridView`. Summary text must use the four result counts. Add an enabled log-directory button only when the log path is non-empty.

- [ ] **Step 3: Implement JSON/TXT writer**

Write to `%LOCALAPPDATA%\EBAssistant\Logs\PermissionConfiguration` with stem:

```text
permission-assignment-yyyyMMdd-HHmmss-fff
```

The text log must include:

```text
状态
时间
总组合数
添加成功
已存在跳过
失败
SetRight：未调用
自动回滚：未执行
成员ID / 成员名称 / 目录ID / 目录名称 / 状态 / 说明
```

Use `new UTF8Encoding(false)` for both JSON and TXT.

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build .\EBAssistant.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add PermissionAssignmentPreviewForm.cs PermissionAssignmentResultForm.cs PermissionAssignmentLogWriter.cs
git commit -m "feat: show permission assignment preview and results"
```

### Task 6: Wire The Permission Configuration UI

**Files:**
- Modify: `PermissionConfigurationForm.cs`

- [ ] **Step 1: Enable right-tree checkboxes and shared linkage**

Set:

```csharp
private readonly TreeView _directoriesTree = new()
{
    Dock = DockStyle.Fill,
    CheckBoxes = true,
    HideSelection = false,
    ShowNodeToolTips = true
};
```

Use one shared `AfterCheck` handler for both trees. Preserve the existing parent/child behavior and guard with `_updatingChecks`.

- [ ] **Step 2: Add the apply button and busy state**

Add `_apply` with text `应用权限配置`. Disable it during load/write. Recalculate its enabled state after checks and after structures display.

- [ ] **Step 3: Implement selection collection and preview**

Collect checked nodes whose `Tag` is `PermissionDirectoryNode`. Build selection with:

```csharp
var selection = PermissionAssignmentSelection.Build(
    _identity.UsersAndGroupsId,
    CollectCheckedModels(_usersTree.Nodes),
    CollectCheckedModels(_directoriesTree.Nodes));
```

If `CanApply` is false, show a direct message and do not call the adapter. Otherwise show `PermissionAssignmentPreviewForm`.

- [ ] **Step 4: Implement confirmed apply flow**

After preview confirmation:

```csharp
SetBusy(true);
var response = await _client.AddPermissionMembersAsync(selection.Request);
var result = response.Data ?? PermissionAssignmentResultSummary.FromFailure(
    selection,
    response.Message);
PermissionAssignmentResultSummary.Apply(result);
var logDirectory = PermissionAssignmentLogWriter.Write(result);
new PermissionAssignmentResultForm(result, logDirectory).Show();
await LoadFromEbAsync();
```

If log writing fails, show a warning but still show results. Do not close the permission window after the operation.

- [ ] **Step 5: Build and run tests**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
dotnet build .\EBAssistant.csproj
```

Expected: tests and build pass.

- [ ] **Step 6: Commit**

```powershell
git add PermissionConfigurationForm.cs
git commit -m "feat: apply checked permission members"
```

### Task 7: Full Verification And Controlled Live EB Validation

**Files:**
- Verify all changed files
- Do not create cleanup code or remove permissions without separate user authorization

- [ ] **Step 1: Run the complete build**

Run:

```powershell
PowerShell -ExecutionPolicy Bypass -File .\build.ps1
```

Expected: main app, EB2023 adapter, EB2024 adapter, and EB2025 placeholder all build; only existing package warnings may remain.

- [ ] **Step 2: Run all automatic tests**

Run:

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
git diff --check
```

Expected: all test groups pass and no whitespace errors.

- [ ] **Step 3: Verify UI without writing**

Launch `EBAssistant.exe`, open `权限配置`, verify:

- both trees show checkboxes;
- user/group root is excluded from the preview request;
- preview shows exact member, directory, and combination counts;
- cancel closes preview without invoking `AddPermissionMembers`.

- [ ] **Step 4: Request the user's explicit live target confirmation**

Before any successful `Add`/`AddGroup` call, show or report the exact selected member/user-group names, target directory names, and combination count. Wait for the user to confirm that these are safe test targets.

- [ ] **Step 5: Perform one controlled live add**

After explicit confirmation, execute one member/group plus one directory combination. Verify:

- result is `added`;
- the adapter re-read contains the same SID;
- result/log explicitly state `SetRight` was not called;
- no existing permission member was removed or modified.

- [ ] **Step 6: Verify idempotence**

Run the same combination again. Expected: `skipped_existing`; no duplicate is added.

- [ ] **Step 7: Verify continue-on-failure**

Only if a user-approved non-supporting target is available, include it with a known valid target. Expected: unsupported target records `failed`, valid target still reaches `added` or `skipped_existing`.

- [ ] **Step 8: Final review**

Run:

```powershell
rg -n "SetRight|RemoveUsers|\\.Remove\\(|Delete\\(" Adapters/AdapterProgram.cs
git status --short
git log --oneline -8
```

Expected: no new permission mutation path except `Add` and `AddGroup`; the working tree contains no temporary diagnostics or generated logs.
