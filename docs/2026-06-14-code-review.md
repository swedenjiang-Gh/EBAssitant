# EBAssistant 项目代码审查结果

审查日期：2026-06-14

## 1. 审查范围

本次审查以静态代码检查为主，重点检查：

- EB 适配器发现、连接和进程通信
- EB 写入、读回和清理闭环
- 多活动 EB 实例选择
- 主程序与适配器的架构约束
- 自动测试和完整构建状态

本次没有连接受控 EB 数据库执行真实写入，因此涉及 COM 持久化和运行时行为的结论仍需在受控数据库中验证。

## 2. 审查发现

### P1：适配器可能连接错误 EB 版本，甚至静默启动新 EB 实例

位置：

- `Adapters/AdapterProgram.cs:99`
- `Adapters/AdapterProgram.cs:155`

问题：

- 适配器除版本专属 ProgID 外，还尝试通用 ProgID 和 Running Object Table 中的任意可转换 EB Application。
- 返回 Application 后没有验证实际 EB 版本。
- 附着失败且检测到对应进程时，会通过 `Activator.CreateInstance` 创建 Application。

影响：

- EB 2023 适配器可能附着到其他版本的 EB。
- 连接用户现有 EB 失败时，可能启动新的 EB 实例。
- 后续写入可能作用于错误实例或错误数据库。

建议：

- 只接受能够确认版本匹配的活动 EB 实例。
- 禁止连接流程静默创建新的 Application。
- 无法确认实例版本时应明确报错，并要求用户处理。

### P1：适配器调用没有超时，COM 卡死会永久挂起功能窗口

位置：

- `EbAdapterClient.cs:149`
- `EbAdapterClient.cs:178`

问题：

- 主程序启动适配器后无限等待 `WaitForExitAsync()`。
- 没有超时、取消令牌或超时后的适配器进程终止策略。

影响：

- COM 调用卡住、未知 EB 对话框阻塞或适配器死锁时，功能窗口会永久保持忙碌状态。
- 若写入已经部分完成，用户只能强制关闭程序，无法可靠获得结果和日志。
- 用户重试可能产生重复写入。

建议：

- 为读取操作和写入操作分别设置合理超时。
- 超时时明确报告“结果未知”，保存可用的请求上下文和诊断日志。
- 谨慎终止适配器进程，并提示用户先检查 EB 中的实际结果再重试。

### P1：临时属性清理失败被静默忽略，且没有清理读回确认

位置：

- `Adapters/AdapterProgram.cs:337`
- `Adapters/AdapterProgram.cs:417`

问题：

- 属性创建能力预检结束后，删除临时属性的异常被空 `catch` 忽略。
- 批量创建失败后的回滚只调用 `Delete`，没有再次读回确认对象已经消失。

影响：

- 可能留下 `EBAssistant_MoveCheck_*` 临时对象。
- 回滚可能被记录为成功，但对象实际仍然存在。
- 不符合项目要求的“清理并再次读回确认”闭环。

建议：

- 临时对象清理失败时停止正式写入，并向用户明确报告。
- 删除后重新按 ID 或父目录枚举读回，确认对象已消失。
- 将清理检查结果写入操作日志。

### P1：类型定义关联写入缺少明确的持久化步骤

位置：

- `Adapters/AdapterProgram.cs:1719`

问题：

- 调用 `item.Attributes.Add(...)` 后立即重新解析并枚举确认，但没有显式调用 `Store()`。

影响：

- 当前读回可能只反映同一 COM 会话中的状态，不能充分证明关联已经持久化。
- EB 重启后可能出现关联丢失，但日志已经记录为 `added`。

建议：

- 按已确认的 EB 配方补充 `TypeItem.Store()`。
- 从 EB 重新解析 TypeItem 并枚举 `TypeItem.Attributes` 读回确认。
- 在受控数据库中验证 EB 2023 和 EB 2024 的持久化行为。

### P2：类型定义窗口在多个活动 EB 场景中直接选择第一个实例

位置：

- `TypeDefinitionsForm.cs:46`

问题：

- 检测到多个活动 EB 时直接使用 `active[0]`。
- 其他高影响功能窗口会要求用户选择活动 EB，行为不一致。

影响：

- 用户无法确认类型定义写入的目标实例。
- 类型定义属于高影响数据库结构写入，选错实例风险较高。

建议：

- 复用其他窗口的活动 EB 选择行为。
- 在正式写入确认界面中显示 EB 版本和数据库名称。

### P2：权限适配器使用 `dynamic`，违反项目强类型 COM 约束

位置：

- `Adapters/AdapterProgram.cs:972`

问题：

- `ReadPermissionConfigurationFolders` 使用 `dynamic folders = app.Folders`。
- 项目约束明确要求不得使用 `dynamic` 操作 EB COM 对象。

影响：

- 失去编译期类型检查。
- EB 版本差异或接口变化只能在运行时暴露。

建议：

- 使用已确认的强类型 Aucotec COM 接口访问用户及用户组目录和消息目录。
- 如果 COM Reference 未公开所需成员，应明确记录边界，不应以 `dynamic` 绕过。

## 3. 测试缺口

现有自动测试主要覆盖导入、预览、缓存和纯逻辑辅助类，未覆盖：

- 适配器发现与多版本实例选择
- 适配器进程超时和异常收口
- 主程序与适配器 JSON 协议一致性
- 类型定义关联持久化
- 临时对象清理后的读回确认
- COM 写入发生部分成功时的恢复与日志行为

建议优先为可脱离真实 EB 的进程通信和协议逻辑增加测试。真实 COM 写入只能在受控数据库中验证。

## 4. 本次验证结果

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`
  - 最终结果：通过
  - 主程序、EB 2023/2024 适配器和 EB 2025 占位适配器均构建成功
  - 0 warning，0 error
- `dotnet run --project .\Tests\EBAssistant.Tests.csproj`
  - 结果：`PASS: 14 test groups`
- `git diff --check`
  - 结果：通过

本次审查只新增本文档，没有修改产品代码。
