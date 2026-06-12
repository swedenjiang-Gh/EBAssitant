# 工作表创建能力验证

日期：2026-06-13

## 结论

`Passed=false`。

本次 Task 6 停止在公开 API 门禁：目前仓库和 EB SDK 证据没有确认“非 UI、非反射、非 dynamic 的强类型公共 API”可以写入工作表自定义列标签。因此不继续执行 Task 7 正式批量创建工作表，避免用底层属性名称冒充 Excel 第一行列标签。

## 已确认能力

- `Project.OpenWorksheetDirect(...)` 可返回工作表对象，兄弟项目已有只读验证记录。
- `Worksheet.Attributes.Add(attrId, position)` 可添加工作表列。
- `WorksheetAttribute.Position` 可读写。
- `WorksheetAttribute.Width` 可读写。
- `Worksheet.ProtectColumnWidth` 可读写。
- `Worksheet.SaveConfiguration(name, targetFolder)` 是公开保存入口。

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

该入口是 UI/命令式交互操作，不满足 Task 6 限制：

- 不弹交互框。
- 不使用 UI 自动化。
- 不使用隐藏反射或 late-bound `dynamic`。
- 必须是强类型公共 API。

## 未执行的操作

由于公开 API 门禁未通过，本次没有创建临时工作表对象，也没有执行保存和删除清理。这样不会在 EB 数据库中留下验证残留。

## 后续规则

除非后续从 Aucotec 官方 SDK、COM 类型库或 EngineeringBaseCodemap 的真实运行验证中找到可写列标签的强类型公共 API，否则：

- 不实施 `CreateWorksheets` 正式写入。
- 不把 Excel 第一行列标签降级为属性名称。
- 不使用 `EditColumnLabel`、UI 自动化、反射隐藏成员或 `dynamic` 绕过限制。
