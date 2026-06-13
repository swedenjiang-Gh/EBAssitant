# 工作表功能开发经验与交接

日期：2026-06-13

## 1. 正确业务流程

工作表配置保存在哪个项目，就必须从同一个项目的“设备”目录打开该工作表，然后编辑列标签、调整列宽并保存。

完整闭环：

1. 在所选模板项目中创建器件工作表。
2. 将配置保存到该项目的：

```text
项目模板 / 所选模板项目 / 工作表 / 收藏夹
```

3. 从该项目自己的“设备”目录打开已保存工作表。
4. 在真正打开的工作表交互窗口中编辑列标签、调整列宽。
5. 保存工作表配置。

不得寻找另一个普通项目作为打开上下文，也不得先保存到另一个项目后再移动回来。

## 2. 已确认事实

### 2.1 项目和目标目录

EB2023 当前测试项目：

```text
名称：xxxx模板项目
ID：00000000-0000-0000-0000-00000038E386
目标：项目模板 / xxxx模板项目 / 工作表 / 收藏夹
```

- `Project.WorksheetTemplatesFolder` 可以取得项目的“工作表”目录。
- `AucObjectKind.aucObjFavoriteListConfigurations` 可以唯一定位“收藏夹”。
- 所选项目自身的 `EquipmentFolder` 是打开该项目工作表的正确上下文。

### 2.2 工作表配置创建

- 从所选项目的 `EquipmentFolder` 调用 `OpenWorksheetDirect(...)` 可以创建临时器件工作表对象。
- `Worksheet.Attributes.Add(...)` 可以添加属性列。
- `WorksheetAttribute.Width` 和 `Worksheet.ProtectColumnWidth` 可以写入。
- `Worksheet.SaveConfiguration(name, favoriteFolder)` 可以把配置直接保存到同一项目的“工作表 / 收藏夹”。
- 保存后可以从收藏夹枚举并读回配置对象。
- 多次真实验证创建的临时配置均已删除；最后通过 `GetWorksheetCreationContext` 确认收藏夹中没有临时名称残留。

### 2.3 列标签能力边界

- 强类型 COM 的 `WorksheetAttribute.Name`、`AttributeName` 只有 getter。
- 可用的交互命令为：

```text
AucCommand.aucCmdEditColumnLabel = 57515
```

- 该命令只有在 EB 当前显示的是真正工作表交互窗口，并且列标题被正确选中时才有意义。
- 在普通设备目录列表上执行该命令，会弹出提示：

```text
要编辑该数据，请先保存工作表模板，然后重试！
```

## 3. 已排除的错误路径

以下路径已通过真实 EB2023 实验确认不可作为正式方案：

1. 从模板收藏夹中的工作表配置对象本身直接打开工作表。
   - `Project.OpenWorksheet(configuration, true)` 报 `COMException：该对象不允许使用方法!`
   - `EquipmentFolder.OpenWorksheet(configuration, true)` 报同类错误。
2. 使用另一个普通项目创建或打开，再把配置移动回目标模板项目。
   - 这不符合用户要求，且增加跨项目上下文错误。
3. 对设备目录执行 `aucCmdOpenSheet` 后认为目标工作表已打开。
   - 实际只打开或停留在设备目录普通列表，右侧列为“名称 / 注释”。
4. 使用配置名称调用非交互 `EquipmentFolder.OpenWorksheet(name)` 后认为 UI 已切换。
   - 可以返回 COM `Worksheet`，但 EB UI 仍停留在普通设备目录列表。
5. 使用配置名称调用 `EquipmentFolder.OpenWorksheet(name, true)`。
   - EB2023 返回 `COMException：该对象不允许使用方法!`
6. 在普通设备目录列表上选择列头后调用 `aucCmdEditColumnLabel`。
   - 弹出“请先保存工作表模板”提示，不会出现“编辑列标签”对话框。

## 4. 关键 API 语义教训

SDK 文档对 `ObjectItem.OpenWorksheet(string, bool, WorksheetLoadBehavior)` 的说明指出：

- `string worksheetConfiguration` 是工作表配置对象 ID。
- `interactiveOnly=true` 表示打开交互工作表。
- `interactiveOnly=false` 表示打开供插件交互的工作表。

因此不能把工作表显示名称和配置对象 ID 混用。当前下一条最小验证路径应为：

```csharp
equipmentFolder.OpenWorksheet(temporaryObject.ID, true)
```

中断前代码已改成这一调用，但尚未重新构建和真实验证。它是待验证假设，不是已确认配方。

## 5. 当前代码状态

工作区存在未提交改动，不能回退：

- `Adapters/AdapterProgram.cs` 已加入一次性工作表能力验证和 UI 自动化实验代码。
- `Adapters/2023/EBAssistant.Adapter2023.csproj`、`Adapters/2024/EBAssistant.Adapter2024.csproj` 已加入 UI Automation、WindowsBase 和 Accessibility 引用。
- `WorksheetModels.cs`、`EbAdapterClient.cs` 已加入验证协议。
- 工作表设计、计划和验证文档已有未提交修改。

当前 `Adapters/AdapterProgram.cs` 中：

- 已将交互项目改为所选模板项目本身。
- 已停止跨项目移动配置。
- 最后一次中断前将打开调用改为 `equipmentFolder.OpenWorksheet(temporaryObject.ID, true)`。
- 该最后改动尚未构建和实测。

正式 `CreateWorksheets` 仍未实现，不能声称工作表功能已完成。

## 6. 真实验证记录

### 验证 A：同项目保存并按名称非交互打开

结果：

- 临时配置保存成功。
- 临时配置读回成功。
- `OpenWorksheet(name)` 返回工作表对象。
- EB UI 仍显示 `项目模板 / xxxx模板项目 / 设备`。
- 右侧仍是普通设备目录列表。
- 执行编辑列标签命令后弹出“请先保存工作表模板”。
- 临时配置删除成功。

### 验证 B：按名称交互打开

调用：

```csharp
equipmentFolder.OpenWorksheet(temporaryWorksheetName, true)
```

结果：

```text
COMException：该对象不允许使用方法!
```

临时配置关闭和删除成功。

### 验证 C：命令上下文改为配置对象

结果：

- `ExecuteCommand` 被 EB 模态提示阻塞，适配器进程不能正常返回。
- 自动化线程只寻找“编辑列标签”对话框，没有识别通用 EB 信息提示。
- 最终关闭提示并终止明确的适配器进程。
- 随后读回确认目标收藏夹没有临时配置残留。

## 7. 自动化与清理教训

- `ExecuteCommand` 可能同步阻塞在 EB 模态对话框上；不能假设调用会及时返回。
- 对话框处理器不能只识别预期的“编辑列标签”，还必须识别 EB 通用错误/信息提示并立即记录失败、自动关闭。
- 在确认真正工作表窗口已经显示前，不得点击任何列标题或执行编辑命令。
- UI Automation 找到一个 HeaderItem 不等于它属于目标工作表；必须同时验证当前窗口身份和列集合。
- `Worksheet.Close()` 在部分状态下会报 `COMException：该对象不允许使用方法!`，清理逻辑仍必须继续删除明确的临时配置对象并读回确认。
- 每次真实验证必须使用唯一临时名称，只删除本次创建且能通过名称和对象 ID 双重确认的对象。
- 验证超时后要检查并结束明确的适配器进程，不能结束 EB 主进程。

## 8. 下一步最小验证

1. 构建当前 EB2023 适配器。
2. 执行 `ValidateWorksheetCreationCapability`。
3. 只验证 `equipmentFolder.OpenWorksheet(temporaryObject.ID, true)` 是否真正打开交互工作表。
4. 在调用编辑列标签命令前截图或检查窗口结构，确认右侧列是临时工作表列，而不是“名称 / 注释”。
5. 若仍失败，停止尝试 `OpenWorksheet` 参数组合，转向自动化 EB 的“打开工作表”菜单，并按临时配置名选择。
6. 在编辑列标签验证通过前，不实施正式 `CreateWorksheets`。

## 9. 不再重复的错误

- 不再跨项目保存、打开或移动工作表配置。
- 不再从收藏夹配置对象本身直接打开工作表。
- 不再把 `aucCmdOpenSheet` 视为已选择指定工作表配置。
- 不再把配置显示名称当作配置对象 ID。
- 不再在未确认工作表交互窗口时执行编辑列标签命令。
- 不再让适配器无限等待未知 EB 模态对话框。
