# 权限配置成员添加设计

## 目标

在现有“权限配置”窗口中，让用户选择一个或多个 EB 用户/用户组和一个或多个权限目录，将每个选中的用户或用户组加入每个选中目录的 `AccessPermissions` 集合。

首版只添加权限成员，不设置具体权限位，不调用 `SetRight`。

## 已确认语义

- 左侧用户及用户组树保留复选框。
- 右侧权限目录树增加复选框。
- 勾选用户组时，只添加该用户组本身，不展开并添加组内用户。
- 勾选父节点时递归勾选全部子节点；取消子节点后重新计算父节点状态。
- 每个“用户或用户组 + 权限目录”组合独立处理。
- 单项失败后继续处理剩余组合，最终汇总全部结果。
- 已经存在于目标目录 `AccessPermissions` 中的用户或用户组不重复添加，记录为跳过。
- 不修改已有权限成员，不删除任何权限成员，不尝试整体回滚。

## 能力边界

EngineeringBaseCodemap 当前只确认 `Project.AccessPermissions` 和
`Project.Configuration.AccessPermissions` 的只读枚举以及 `CheckRight`。
`permission.write` 尚无已确认运行证据。

因此实现必须把权限添加视为受控、高影响写入：

- 用户必须在只读预览中确认目标和数量后才能写入。
- 不得静默启动新的 EB。
- 不得调用 `SetRight`。
- 不得删除或修改目标目录中已有的权限成员。
- 每次添加后必须重新枚举目标目录的 `AccessPermissions`，按用户或用户组身份读回确认。
- 目录不支持 `AccessPermissions`、成员对象无法解析或添加失败时，只记录该组合失败并继续。

## 界面设计

### 树与选择

- `_usersTree`：保留复选框和现有父子联动。
- `_directoriesTree`：启用复选框，并使用与左侧一致的父子联动。
- 实际写入对象按 ID 去重。
- “用户及用户组”根节点只作为批量选择入口，不作为可添加权限成员。
- 右侧只处理当前展示的权限目录节点。

### 操作入口

在窗口顶部增加“应用权限配置”按钮。

按钮仅在以下条件都满足时启用：

- 已连接活动 EB。
- 至少选择一个可添加的用户或用户组。
- 至少选择一个权限目录。
- 当前未执行读取或写入操作。

### 只读预览与确认

点击“应用权限配置”后先显示只读预览对话框，内容包括：

- 选中的用户和用户组名称、完整路径、数量。
- 选中的权限目录名称、完整路径、数量。
- 预计处理组合数量：用户/组数量乘以目录数量。
- 明确提示：只添加权限成员，不设置具体权限位；单项失败后继续；不自动回滚。

用户确认后才调用适配器写入操作。

### 结果展示

完成后显示结果窗口，包含：

- 总组合数。
- 添加成功数。
- 已存在跳过数。
- 失败数。
- 每个组合的目录、成员、状态和消息。
- 日志目录按钮。

## 适配器协议

新增操作 `AddPermissionMembers`。

请求模型：

```text
PermissionMemberAssignmentRequest
  MemberIds: string[]
  DirectoryIds: string[]
```

响应模型：

```text
PermissionMemberAssignmentResult
  Status: completed | completed_with_failures
  TotalCount: int
  AddedCount: int
  SkippedCount: int
  FailedCount: int
  Records: PermissionMemberAssignmentRecord[]

PermissionMemberAssignmentRecord
  MemberId: string
  MemberName: string
  DirectoryId: string
  DirectoryName: string
  Status: added | skipped_existing | failed
  Message: string
```

主程序 `Models.cs` 与适配器 `Adapters/AdapterProgram.cs` 底部的
`[DataContract]` 模型必须同步修改。

## 适配器处理流程

1. 校验请求非空，并对成员 ID、目录 ID 去重。
2. 使用 `app.Utils.GetSnglObjectByID(id)` 重新解析所有成员和目录，禁止信任缓存对象。
3. 验证成员属于当前数据库“用户及用户组”树，且不是“用户及用户组”根节点。
4. 验证目录属于当前数据库根目录下允许展示的权限目录范围。
5. 对每个目录获取其 `AccessPermissions` 集合；不支持该集合的目录，对该目录的所有组合记录失败。
6. 对每个组合先枚举 `AccessPermissions`，按稳定身份判断是否已存在。
7. 已存在时记录 `skipped_existing`。
8. 不存在时调用 `AccessPermissions.Add(...)` 添加成员。
9. 添加后重新解析目录并重新枚举 `AccessPermissions`；确认成员可见后记录 `added`。
10. 任一组合失败时记录 `failed`，继续处理剩余组合。

适配器不得调用：

- `SetRight`
- 权限成员删除方法
- 用户或用户组创建、删除或修改方法
- 与本次请求无关的 `Store()` 或其他写入方法

## 身份与读回

成员和目录必须使用对象 ID 作为请求身份。

添加后的读回确认必须同时满足：

- 重新通过目录 ID 解析目标目录。
- 重新取得该目录的 `AccessPermissions` 集合。
- 在集合中命中所添加成员的稳定身份。

名称只用于界面和日志展示，不作为唯一判断依据。

## 失败策略

- 请求整体格式错误：整个操作失败，不开始写入。
- 某个成员或目录 ID 无法解析：与其相关的组合记录失败，其他组合继续。
- 某个目录不支持 `AccessPermissions`：该目录的全部组合记录失败，其他目录继续。
- 已存在：记录 `skipped_existing`，不修改已有权限位。
- 添加 API 抛出异常或读回未命中：记录 `failed`，继续处理。
- 已经成功添加的成员保留，不回滚。

## 日志

每次执行保存 JSON 和文本日志到：

`%LOCALAPPDATA%\EBAssistant\Logs\PermissionConfiguration`

日志记录：

- EB 版本和数据库根目录身份。
- 操作时间。
- 用户确认后的成员和目录清单。
- 总组合数与各状态数量。
- 每个组合的身份、名称、状态和消息。
- 明确记录 `SetRight` 未调用、自动回滚未执行。

日志不得记录密码、令牌或其他私密配置。

## 缓存与刷新

- 现有权限结构缓存仅用于展示和选择。
- 写入前始终通过对象 ID 从当前 EB 重新解析对象。
- 写入完成后自动从 EB 重新读取权限配置结构并覆盖缓存。
- 写入结果不得仅依赖缓存判断。

## 测试与验证

### 自动验证

- 左右树父子复选联动。
- 根节点不作为可添加权限成员。
- 成员和目录 ID 去重。
- 请求/响应 JSON 两侧模型一致。
- 结果汇总正确计算 `added`、`skipped_existing`、`failed`。
- 单项失败后继续处理剩余组合。
- 日志包含全部组合结果。

### 构建验证

- `dotnet build .\EBAssistant.csproj`
- 使用 Visual Studio MSBuild 构建 EB 2023/2024 适配器。
- 构建 EB 2025 占位适配器。

### 真实 EB 验证

真实验证必须由用户明确确认目标成员和目标目录后执行：

1. 选择一个不会影响生产使用的测试用户或用户组和测试权限目录。
2. 执行一次添加，确认状态为 `added`，并从 EB 读回命中。
3. 对同一组合再次执行，确认状态为 `skipped_existing`。
4. 使用一个不支持 `AccessPermissions` 的目录验证失败后继续策略。
5. 确认未调用 `SetRight`，未删除或修改任何已有权限成员。

真实验证产生的权限成员不会自动移除；需要移除时必须另行获得用户明确授权。
