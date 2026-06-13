# 工作表创建能力验证

日期：2026-06-13

## 结论

状态：`验证未通过，已确认创建和保存能力，交互打开与列标签仍阻塞`。

此前 Task 6 停止在公开 API 门禁。2026-06-13 用户已明确允许使用 `aucCmdEditColumnLabel = 57515` 交互命令，并允许执行期间短暂切换焦点到 EB；无需用户操作的交互界面必须由程序隐藏并自动处理。因此旧 `Passed=false` 结论不再作为最终门槛，Task 6 需要按新方案重新执行真实 EB 验证。

真实 EB2023 实验的完整记录、已排除路径、当前代码状态和下一步最小验证见 `docs/worksheet-development-handoff.md`。

## 已确认能力

- `Project.OpenWorksheetDirect(...)` 可返回工作表对象，兄弟项目已有只读验证记录。
- `Worksheet.Attributes.Add(attrId, position)` 可添加工作表列。
- `WorksheetAttribute.Position` 可读写。
- `WorksheetAttribute.Width` 可读写。
- `Worksheet.ProtectColumnWidth` 可读写。
- `Worksheet.SaveConfiguration(name, targetFolder)` 是公开保存入口。
- `Project.WorksheetTemplatesFolder` 可定位项目的“工作表”目录。
- `AucObjectKind.aucObjFavoriteListConfigurations` 可定位“收藏夹”。
- 工作表配置保存和打开必须使用同一个所选项目；从该项目自己的 `EquipmentFolder` 打开。

这些能力足够创建列、调整列顺序、设置列宽和保存配置，但不足以满足用户要求的“第一行作为工作表显示列标签”。

## 阻塞证据

### COM 30 / EB2023

`Interop.Aucotec (30.0.0.0).cs` 中 `IAucWorksheetAttribute` 的公开成员显示：

- `AttributeID` 只有 getter。
- `AttributeName` 只有 getter。
- `Name` 只有 getter。
- `Position`、`TextAlignment`、`TextStyle`、`ColumnFlags`、`Width` 有 setter。

未发现 `Label`、`Caption`、`Header` 或等价的可写列标签属性。

### COM 31 / EB2024

`Interop.Aucotec (31.0.0.0).cs` 中 `IAucWorksheetAttribute` 与 COM 30 一致：

- `AttributeID` 只有 getter。
- `AttributeName` 只有 getter。
- `Name` 只有 getter。
- `Position`、`TextAlignment`、`TextStyle`、`ColumnFlags`、`Width` 有 setter。

未发现强类型可写列标签入口。

### EB SDK Runtime 文档

SDK 的 `WorksheetColumn` 成员表显示：

- `Name`：Returns the column name。
- `AttributeName`：Returns the attribute name。
- `Width`：Gets or Sets the width of the column。
- `Position`：Gets or Sets the position of the attribute。

SDK 只将 `Name` 描述为返回列名，没有提供可写标签 setter。

### 仅发现交互命令

SDK 和 COM 枚举中存在：

- `EditColumnLabel = 57515`
- 描述：Edit column label

该入口是 UI/命令式交互操作，现已被允许用于解除公开 API 门槛。新的验证约束是：

- 使用强类型 `IAucVbaInternUtils.ExecuteCommand` 调用命令。
- 程序自动唯一定位列标题和“编辑列标签”对话框。
- 对话框出现后立即隐藏，由程序写入并确认。
- 允许短暂切换焦点到 EB，但不要求用户操作。
- 不使用隐藏反射、late-bound `dynamic`、固定坐标或盲目 `SendKeys`。

## EB2023 真实验证结果

测试目标：

```text
项目模板 / xxxx模板项目 / 工作表 / 收藏夹
模板项目 ID：00000000-0000-0000-0000-00000038E386
```

已经确认：

- 临时器件工作表创建成功。
- AID 5 和 AID 25 列添加成功。
- 列宽和 `ProtectColumnWidth` 写入成功。
- 临时配置直接保存到目标收藏夹并读回成功。
- 所有已执行验证的临时配置均已删除；最后读回 `ExistingWorksheetNames` 为空。

已排除：

- 从配置对象本身直接打开会报 `COMException：该对象不允许使用方法!`
- `aucCmdOpenSheet` 只打开设备目录普通列表，不会选择指定工作表配置。
- `EquipmentFolder.OpenWorksheet(配置名称)` 可返回 COM Worksheet，但 UI 不会切换到交互工作表。
- `EquipmentFolder.OpenWorksheet(配置名称, true)` 报 `COMException：该对象不允许使用方法!`
- 在普通设备目录列表执行 `aucCmdEditColumnLabel` 会弹出“要编辑该数据，请先保存工作表模板，然后重试！”

SDK 文档指出 `OpenWorksheet(string, ...)` 的 string 参数是工作表配置对象 ID，不是显示名称。中断前代码已改为：

```csharp
equipmentFolder.OpenWorksheet(temporaryObject.ID, true)
```

该调用尚未重新构建和真实验证，不能记录为成功。

## 自动化风险

- `ExecuteCommand` 可能被 EB 模态提示同步阻塞。
- 当前自动化只识别“编辑列标签”对话框，尚不能识别并关闭通用 EB 信息提示。
- UI 显示设备目录普通列表时，即使找到了列标题，也不能执行列标签命令。
- `Worksheet.Close()` 在部分失败状态下会返回 `COMException`，但临时配置删除仍必须继续执行并读回确认。

## 后续规则

只有新的真实 EB 验证明确 `Passed=true` 后，才实施 `CreateWorksheets` 正式写入。若验证失败：

- 不实施正式写入。
- 不把 Excel 第一行列标签降级为属性名称。
- 记录失败的窗口定位、命令调用、标签读回或清理步骤。
- 不继续尝试已排除的跨项目打开、配置对象直接打开或按配置名称交互打开路径。
