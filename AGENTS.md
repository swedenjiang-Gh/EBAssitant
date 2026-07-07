# EBAssistant 项目说明

## 0. 设计规则

读取的目录结构均要保存缓存，以便下次直接读取；只有用户刷新时才重新读取并更新缓存。
导入的数据需要做只读预览。
涉及 EB 写入的任务完成后，都要给用户展示结果、保存日志并提供日志路径入口。
兄弟文件夹 `EngineeringBaseCodemap` 是知识库，可供开发参考，但不要把它的治理体系搬进本桌面程序。

## 1. 项目定位

EBAssistant 是面向 Aucotec Engineering Base（EB）的 Windows 桌面辅助工具，使用 C# 和 Windows Forms 开发。

主界面包含六个功能入口，当前六个入口均已接入实际功能窗口：

- 属性
- 类型定义
- 工作表
- 权限配置
- 图形模板
- 工具面板配置

主界面还包含：

- 文件 / 下载模板：扫描输出目录 `Templates` 下所有 `.xlsx` 和 `.xls` 文件并复制给用户。
- 文件 / 日志：打开 `%LOCALAPPDATA%\EBAssistant\Logs`。
- 关于 / 帮助：打开 `Templates\帮助手册.pdf`。
- 关于 / 版本信息：读取 `Templates\版本信息.txt`。

## 2. 目录与技术栈

项目根目录：`D:\开发\代码仓\EB\EBAssistant`

主要技术：

- 主程序：WinForms、`net10.0-windows`、x86
- EB 2023/2024 适配器：强类型 Aucotec COM Reference、`.NET Framework 4.6.2`、x86
- EB 2025 适配器：`net462` 占位程序；当前明确报错，不连接 COM
- Excel：`ExcelDataReader`
- 主程序与适配器通信：标准输入/标准输出 JSON
- 主命名空间：`EBAssistant`
- COM 适配器命名空间：`EBAssistant.Adapter`

主程序通过 `EBAssistant.csproj` 排除 `Adapters/**/*.cs`，适配器独立构建，不能直接编译进 WinForms 主程序。

## 3. 核心架构

### 3.1 主程序

入口为 `Program.cs`，启动时注册代码页编码提供程序，然后打开 `MainForm`。

`MainForm`：

- 窗口标题为 `EB Assistant`。
- 六个功能入口均使用 `Form.Show()` 打开独立非模态窗口。
- 主界面和已打开的功能窗口可以同时操作。
- `_openWindows` 用于持有功能窗口引用，避免窗口被提前回收。

### 3.2 EB 适配器客户端

`EbAdapterClient.cs` 负责：

- 按 2023、2024、2025 顺序寻找适配器 EXE。
- 调用适配器的 `GetConnectionInfo` 判断当前活动 EB。
- 通过命令行参数传递操作名。
- 通过标准输入发送 JSON 请求。
- 通过标准输出读取 JSON 响应。
- 使用统一的 `AdapterResponse<T>` 响应结构。

主程序不会直接引用 Aucotec COM 类型，也不使用 `dynamic`。

### 3.3 COM 适配器

EB 2023 和 2024 共用 `Adapters/AdapterProgram.cs`，通过编译常量区分：

- `EB30`：EB 2023，ProgID 为 `EngineeringBase.Application.30`
- `EB31`：EB 2024，ProgID 为 `EngineeringBase.Application.31`

连接活动 EB 的尝试顺序：

1. `Marshal.GetActiveObject`，依次尝试版本 ProgID、`EngineeringBase.Application`、`Aucotec.Application`
2. 枚举 Running Object Table
3. 检测对应 EB 客户端进程后，使用版本 ProgID 的 `Activator.CreateInstance`

所有 EB COM 调用都使用 Aucotec 强类型接口和显式类型转换。

适配器当前提供的内部操作：

- `GetConnectionInfo`
- `GetAttributeFolderTree`
- `GetAttributeFolderIdentity`
- `CreateAttributes`
- `CreateAttributeFolder`
- `GetTypeDefinitionIdentity`
- `GetTypeDefinitionTree`
- `ValidateAttributeIds`
- `ApplyTypeDefinitionDialogs`
- `GetProjectTemplateIdentity`
- `GetProjectTemplateTree`
- `ValidateWorksheetAttributeIds`
- `GetWorksheetCreationContext`
- `ValidateWorksheetCreationCapability`
- `CreateWorksheets`
- `GetPermissionConfigurationIdentity`
- `GetPermissionConfigurationStructure`
- `AddPermissionMembers`
- `GetGraphicTemplateIdentity`
- `GetGraphicTemplateTree`
- `GetGraphicTemplateDirectory`
- `MoveGraphicTemplates`
- `CreateGraphicTemplates`
- `GetToolPanelConfigurationIdentity`
- `GetToolPanelConfigurationTree`
- `GetToolPanelConfigurationDirectory`
- `AddGraphicTemplatesToToolPanel`

## 4. “属性”功能

### 4.1 属性目录窗口

入口文件：`AttributeFoldersForm.cs`

行为：

- 连接当前活动 EB。
- 多个 EB 版本同时活动时，弹窗让用户选择。
- 递归读取 `Application.Folders.Attributes` 下的用户属性目录。
- 只显示目录，不显示具体属性。
- 当前目录树加载后默认全部展开。
- 提供“刷新”按钮。
- 目录右键菜单提供“创建属性”和“新建目录”。

### 4.2 新建属性目录

调用适配器 `CreateAttributeFolder`：

- 在所选目录下使用 `NewChild(aucObjFolderForUserAttributes)` 创建目录。
- 通过 AID 5 设置目录名称。
- `Store()` 后重新枚举父目录并读回确认。
- 创建失败时尽力删除新建对象。

### 4.3 批量创建属性

入口文件：

- `CreateAttributesForm.cs`
- `ExcelAttributeImporter.cs`
- `AttributeTypeMappingForm.cs`
- `AttributeTypeMappingStore.cs`

Excel 规则：

- 支持 `.xlsx` 和 `.xls`。
- 读取首个工作表。
- 第一行为标题。
- 从第二行开始读取。
- 第二列为属性名称。
- 第三列为用户填写的属性类型。
- 名称和类型都为空的行会被忽略。

导入时校验：

- 属性名称为空
- 属性类型为空
- 类型无法映射
- 表格内名称重复
- EB 中已经存在同名属性

存在任意错误时禁用“确定”按钮。

属性类型通过 `%LOCALAPPDATA%\EBAssistant\attribute-type-mappings.json` 配置。默认支持文本、字符串、日期、时间、日期时间、布尔、数字、浮点、公式及部分中英文别名。

创建流程：

1. 适配器重新校验名称和类型。
2. 创建临时属性，验证“创建后移动到所选属性目录”的能力。
3. 立即删除临时属性。
4. 使用 `AttributesFolder.NewAttribute(name, type, digits)` 批量创建。
5. 将属性移动到目标目录。
6. 通过父目录 ID 读回确认。
7. 中途失败时停止，并尽力删除本批已创建属性。

注意：EB 2023 适配器明确不支持公式属性类型。

## 5. “类型定义”功能

### 5.1 类型定义树

入口文件：

- `TypeDefinitionsForm.cs`
- `TypeDefinitionCache.cs`

行为：

- 连接活动 EB 后先读取类型定义根目录身份。
- 缓存按“EB 版本 + 类型定义根目录 ID”隔离。
- 缓存协议版本当前为 `3`。
- 缓存存在且协议一致时直接展示。
- “重新读取”按钮会重新遍历 EB 并覆盖缓存。
- 树默认全部折叠。
- 每个节点都有复选框。
- 勾选父节点会递归勾选全部子节点。
- 取消子节点后会重新计算父节点状态。
- 实际操作只作用于 `IsActionable=true` 的末级 TypeItem，并按 ID 去重。

缓存目录：

`%LOCALAPPDATA%\EBAssistant\Cache\TypeDefinitions`

类型树由 `Application.Folders.TypeDefinitions.Children` 按 EB 返回顺序递归读取。末级节点会通过对象 ID 或同名 TypeItem 映射为真正可操作的 TypeItem。

注意：类型定义窗口在检测到多个活动 EB 时当前直接选择第一个，没有像属性窗口一样弹出版本选择框。

### 5.2 批量定义对话框

入口文件：

- `TypeDefinitionDialogForm.cs`
- `DialogDefinitionExcelImporter.cs`
- `TypeDefinitionResultForm.cs`
- `TypeDefinitionLogWriter.cs`

Excel 规则：

- 支持 `.xlsx` 和 `.xls`。
- 读取首个工作表。
- 第一行为标题。
- 从第二行开始读取。
- 第二列为选项卡名称。
- 第三列为正整数属性 ID（AID）。
- “选项卡名称 + AID”重复行会合并。

AID 校验同时使用：

- 递归枚举属性目录中的属性定义
- `IAucVbaInternUtils.GetAttributeDescription`

写入行为：

- 对每个目标 TypeItem 逐项处理。
- 已存在的 AID 会跳过，不调整原有选项卡。
- 新属性使用 `TypeItem.Attributes.Add((AucAttribute)aid, Type.Missing, tabName)` 添加。
- `Store()` 后重新解析 TypeItem，并枚举属性确认 AID 可见。
- 任一写入失败时停止后续操作。
- 已经成功的写入保留，不做整体回滚。

操作结果状态：

- `added`
- `skipped_existing`
- `failed`

日志同时保存 JSON 和文本格式：

`%LOCALAPPDATA%\EBAssistant\Logs\TypeDefinitions`

## 6. “工作表”功能

入口文件：

- `ProjectTemplatesForm.cs`
- `CreateWorksheetsForm.cs`
- `WorksheetPreparation.cs`
- `WorksheetCreationLogWriter.cs`
- `WorksheetCreationResultForm.cs`
- `WorksheetModels.cs`

行为：

- 项目模板树来自 `Application.Folders.ProjectTemplates`。
- 缓存按“EB 版本 + 项目模板根目录 ID”隔离。
- 树默认折叠。
- 只有用户点击“刷新”才重新读取 EB 并覆盖缓存。
- 只有模板项目节点允许右键“新建工作表”。
- 新建工作表窗口支持 `.xlsx` 和 `.xls` 导入、只读预览、AID 校验和最终名称预览。
- 每个 Excel 页签对应一个 EB 工作表。
- 第一列忽略；从第二列开始，每列对应工作表中的一列。
- 第一行是列标签；EB2023 下创建后写入工作表内部列标签记录。
- 第二行是属性 ID。
- 工作表对象类型当前固定为器件。

正式写入路径：

1. 重新解析所选模板项目。
2. 使用该项目自己的 `EquipmentFolder.OpenWorksheetDirect(...)` 创建器件工作表。
3. 调用 `Worksheet.Attributes.Add(...)` 添加列。
4. 设置 `WorksheetAttribute.Width` 和 `Worksheet.ProtectColumnWidth`。
5. 保存到同一项目 `Project.WorksheetTemplatesFolder` 下唯一 `aucObjFavoriteListConfigurations`。
6. 从收藏夹读回确认。

工作表保存位置显示为：

`项目模板 / 所选模板项目 / 工作表 / 收藏夹`

列标签写入当前使用 EB2023 受控 SQL 内部列记录路线：保存工作表配置并读回对象 ID 后，按配置 OID 精确更新其 `CID=19` 子记录的 `Designation`，使用事务、`@@ROWCOUNT` 和读回确认。正式 `CreateWorksheets` 不调用 `aucCmdEditColumnLabel`，不做 UI Automation。EB2024 暂未开放该列标签写入路径。

日志目录：

`%LOCALAPPDATA%\EBAssistant\Logs\Worksheets`

## 7. “权限配置”功能

入口文件：

- `PermissionConfigurationForm.cs`
- `PermissionConfigurationCache.cs`
- `PermissionConfigurationLogWriter.cs`
- `PermissionAssignmentPreviewForm.cs`
- `PermissionAssignmentResultForm.cs`

行为：

- 读取并缓存 EB 权限配置结构、用户和用户组。
- 缓存按“EB 版本 + 权限配置根目录 ID”隔离。
- 支持选择权限目录、用户和用户组。
- 写入前展示预览和确认。
- 当前写入语义是把选中的用户或用户组添加到选中的权限目录中。
- 当前不设置具体权限位。
- 写入后读回确认，区分 `added`、`skipped_existing`、`failed`。
- 每次操作保存 JSON/TXT 日志并展示结果。

日志目录：

`%LOCALAPPDATA%\EBAssistant\Logs\PermissionConfiguration`

边界：

- `permission.read` 已有确认路径。
- 当前实现是成员添加，不是完整权限位配置。
- 后续如要写具体权限位，必须先做受控验证，不能直接推广现有成员添加逻辑。

## 8. “图形模板”功能

入口文件：

- `GraphicTemplatesForm.cs`
- `GraphicTemplateCache.cs`
- `GraphicTemplateMigrationLogWriter.cs`
- `GraphicTemplateMigrationResultForm.cs`
- `GraphicTemplateCreationLogWriter.cs`
- `GraphicTemplateCreationResultForm.cs`
- `GraphicTemplateBatchMigrationForm.cs`
- `GraphicTemplateBatchExcelImporter.cs`

行为：

- 图形模板根目录来自 `app.Folders.Stencils`。
- 左侧显示图形模板目录树，右侧显示所选目录下的模板图形。
- 缓存按“EB 版本 + 图形模板根目录 ID”隔离。
- 初次无缓存时完整读取，存在缓存时直接加载。
- “刷新”只浅层刷新当前所选目录，不全树重读。

已实现写入：

- 勾选模板图形后复制到目标末级目录，源对象保留。
- 使用 EB 命令 `aucCmdSymCopy` 和 `aucCmdSymPaste`。
- 复制后通过目标目录新增对象 ID 读回确认。
- 遇到已白名单化的“图形符号类型与图形模板类型不匹配”确认框时，优先用 Win32 对话框识别并点击“确定”；未知对话框不自动处理。
- “新建模板图形”要求当前末级目录中恰好存在一个模板图形，按该模板补齐到用户输入的总数量。
- 新建模板图形已成功部分保留，不回滚、不删除。

按表格迁移：

- 支持导入 `Templates/迁移模板图形模板.xlsx` 做只读预览。
- 当前只校验源目录、目标目录、末级目录状态和同名模板匹配数量。
- 当前“确定”不执行实际迁移。

日志目录：

`%LOCALAPPDATA%\EBAssistant\Logs\GraphicTemplates`

边界：

- 当前语义是复制，不删除源对象。
- 跨大类复制仍是不稳定或未确认能力，不能写成已支持。
- 不尝试未经验证的底层类型转换。

## 9. “工具面板配置”功能

入口文件：

- `ToolPanelConfigurationForm.cs`
- `ToolPanelConfigurationCache.cs`
- `ToolPanelConfigurationLogWriter.cs`
- `ToolPanelConfigurationResultForm.cs`

行为：

- 左侧和中间复用图形模板目录缓存与模板图形读取逻辑。
- 右侧读取并缓存 `数据库 / 模板 / 工具面板配置` 下的目录和条目。
- 右侧树显示 `Kind 413 / 414 / 415`。
- 只有选中已有 `Kind 415` 条目时才能执行“添加到工具面板”。
- 当前语义是把选中的图形模板关联到已有工具面板配置条目。
- 不复制模板图形，不移动模板图形，不新建 `Kind 415` 条目。
- 当前 EB2023 实现依据为数据库关联：`Kind 415` 条目通过 `Role 132` 指向真实图形模板对象。
- 写入后必须读回确认。
- 添加成功后提示用户重启 EB，因为 EB 当前进程可能缓存工具面板配置。

日志目录：

`%LOCALAPPDATA%\EBAssistant\Logs\ToolPanelConfiguration`

边界：

- 不得删除用户已有的非目标面板。
- 不要把图形模板迁移逻辑复用成工具面板配置写入逻辑。
- 后续扩展前必须先确认目标对象是目录、`Kind 415` 条目，还是模板图形引用关系。

## 10. 数据模型与协议

共享主程序模型集中在 `Models.cs`。

适配器为保持 `.NET Framework 4.6.2` 兼容，在 `Adapters/AdapterProgram.cs` 底部维护一套对应的 `[DataContract]` 模型。

修改请求或响应结构时，必须同步修改两侧模型，否则 JSON 通信可能静默丢字段或解析失败。

主程序使用 `System.Text.Json`；适配器使用 `DataContractJsonSerializer`。

## 11. 构建、运行与验证

主程序构建：

```powershell
dotnet build .\EBAssistant.csproj
```

完整构建入口：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

测试入口：

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

当前完整构建会：

1. 使用 `dotnet build` 构建主程序。
2. 使用 Visual Studio MSBuild 构建 EB 2023/2024 的 `.NET Framework` COM 适配器。
3. 使用 `dotnet build` 构建 EB 2025 占位适配器。

主程序输出目标：

`bin\Debug\net10.0-windows\EBAssistant.exe`

适配器输出目标：

- `bin\Debug\net10.0-windows\Adapters\2023\EBAssistant.Adapter2023.exe`
- `bin\Debug\net10.0-windows\Adapters\2024\EBAssistant.Adapter2024.exe`
- `bin\Debug\net10.0-windows\Adapters\2025\EBAssistant.Adapter2025.exe`

注意：如果 `EBAssistant.exe` 或 adapter EXE 正在运行，构建可能因文件锁失败。验证前先关闭正在运行的本程序和卡住的 adapter 进程。

## 12. 当前已知边界与风险

1. EB 2025 目前只是明确报错的占位适配器，不支持实际连接。
2. 属性窗口和类型定义窗口在多个活动 EB 场景中的版本选择行为不一致。
3. 工作表列标签在 EB2023 中采用受控 SQL 内部列记录路线；该路线必须限定刚保存并读回确认的工作表配置对象，不得按名称泛化写入。
4. 权限配置当前只添加成员，不设置具体权限位。
5. 图形模板“按表格迁移”当前只做导入预览，不执行批量迁移写入。
6. 图形模板跨大类复制仍是不稳定或未确认能力。
7. 工具面板配置当前通过 `Role 132` 数据库关联实现；扩展前必须重新确认 EB 版本和目标对象边界。
8. 当前自动测试覆盖导入、预览、日志、缓存和部分协议逻辑；真实 EB 写入验证只能在受控数据库中执行，不能默认在用户生产数据库中试写。

## 13. 开发约束与建议

- 保持所有 EB COM 代码在版本适配器中，主程序不要直接引用 Aucotec COM。
- 不要使用 `dynamic` 操作 EB COM 对象。
- EB 适配器继续保持 x86 和 `.NET Framework 4.6.2`。
- 主程序与适配器新增操作时，保持 JSON 请求和响应模型两侧一致。
- 写入 EB 后必须读回确认。
- 批量写入需要明确失败策略和结果日志。
- 对可能留下数据库对象的验证，必须立即清理并验证清理结果。
- 不要删除或覆盖用户已有的 EB 数据。
- 所有窗口继续使用非模态方式打开，除非窗口本身是输入或选择对话框。
- 新增中文文本文件时使用 UTF-8 无 BOM，避免通过可能改变编码的 PowerShell 文本管道批量重写源码。
- 修改缓存结构后应提升对应缓存 `ProtocolVersion`，使旧缓存自动失效。

## 14. 快速文件索引

- `Program.cs`：应用入口
- `MainForm.cs`：主界面、菜单和六个功能入口
- `EbAdapterClient.cs`：适配器发现、进程调用和 JSON 通信
- `Models.cs`：主程序共享协议模型
- `AttributeFoldersForm.cs`：属性目录树、刷新和右键菜单
- `CreateAttributesForm.cs`：Excel 批量创建属性界面
- `ExcelAttributeImporter.cs`：属性 Excel 读取与预校验
- `AttributeTypeMappingStore.cs`：属性类型映射持久化
- `TypeDefinitionsForm.cs`：类型定义树、缓存加载和复选联动
- `TypeDefinitionDialogForm.cs`：定义对话框批量配置界面
- `DialogDefinitionExcelImporter.cs`：定义对话框 Excel 读取与校验
- `ProjectTemplatesForm.cs`：项目模板树和工作表入口
- `CreateWorksheetsForm.cs`：Excel 批量创建工作表界面
- `PermissionConfigurationForm.cs`：权限配置读取和成员添加
- `GraphicTemplatesForm.cs`：图形模板读取、复制、新建和批量预览入口
- `ToolPanelConfigurationForm.cs`：工具面板配置关联写入
- `Adapters/AdapterProgram.cs`：EB 2023/2024 强类型 COM 核心实现
- `Adapters/2025/Program.cs`：EB 2025 占位适配器
- `build.ps1`：完整构建入口
- `Tests/EBAssistant.Tests.csproj`：轻量自动测试入口
- `Templates/`：Excel 模板、帮助文档和版本信息
- `docs/操作手册.md`：用户操作手册
- `README.md`：项目总览
- `docs/worksheet-development-handoff.md`：工作表功能最终路线、失败路径和交接
- `docs/engineering-base-automation-lessons-from-codex-dialog.md`：EB 自动化通用经验、列标签诊断边界和受控路线

## 15. EngineeringBaseCodemap 可借鉴知识

兄弟目录 `../EngineeringBaseCodemap` 是 EB API 验证、能力边界和真实运行证据知识库。开发 EBAssistant 时应优先查询其中已经验证的知识，但不要把其 Harness、MCP、Gateway、feature list 等治理结构直接搬入本桌面程序。

### 15.1 推荐查询顺序

遇到 EB API、对象类型、写入行为或版本兼容性问题时，按以下顺序查询：

1. `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/`
2. `../EngineeringBaseCodemap/docs/references/engineering-base/evidence/`
3. `../EngineeringBaseCodemap/docs/codemap-cards/platforms/engineering-base/`
4. `../EngineeringBaseCodemap/tools/engineering-base-*` 和 `tools/engineering-base-eb2023-*`
5. 必要时再做小范围、可清理的真实 EB 探测

只把 `confirmed` 或明确版本适用的结论当作实现依据。`partial`、`failed`、`unverified` 只能用于说明边界或设计后续验证。

### 15.2 已确认的公共 EB API 配方

属性定义与类型定义对话框相关的确认配方：

```text
AttributesFolder.NewAttribute(...)
ObjectItem.Store()
AttributesFolder.Store()
从返回 ObjectItem.Attributes 的 AID 3“属性 ID”读取新 AID
TypeItem.Attributes.Add(AID, Missing, tabName)
枚举 TypeItem.Attributes 读回确认
TypeItem.Attributes.Remove(AID)
ObjectItem.Delete(false, aucDeleteTStandard)
再次读回确认关联和定义均已清理
```

重要边界：

- `TypeItem.Attributes` 是已确认的关联读回依据；不要把 `TypeItem.AttributeDefinitions` 当作该写入路径的唯一验证依据。
- AID 13 Length 和 AID 14 Format 在 EB2023/2024 的验证场景中是只读边界。
- `UnitGroup` 没有确认可通过 `AttributesFolder.NewAttribute` 返回对象公开设置。
- `Application.Dialogs.Properties` 等交互式对话框不是已确认的自动化写入路径。
- 管理员模式/类型定义写入属于高影响数据库结构写入，不能无人值守执行。

工作表确认配方：

```text
Project.WorksheetTemplatesFolder
EquipmentFolder.OpenWorksheetDirect(...)
Worksheet.Attributes.Add(...)
WorksheetAttribute.Width
Worksheet.ProtectColumnWidth
Worksheet.SaveConfiguration(...)
从收藏夹读回确认
```

工具面板配置当前实现边界：

```text
Kind 415 工具面板条目
Role 132 -> 图形模板对象
写入后读回确认
成功后提示重启 EB
```

### 15.3 启动、连接和构建可借鉴规则

- EB COM 项目使用 `net462`、x86 和对应版本 Aucotec COM Reference。
- 禁止使用 C# `dynamic` 访问 EB COM。
- 对象 ID 解析优先使用 `app.Utils.GetSnglObjectByID(id)`。
- 使用 Visual Studio MSBuild 构建带 COM Reference 的 `.NET Framework` 项目；不能只凭 `dotnet build` 判断 COM 适配器可构建。
- EB 自动启动后可能需要分钟级等待 `Application.Name` / `Folders` 就绪，短超时不能证明 API 不可用。
- 控制台适配器输出使用 UTF-8 无 BOM 和结构化 JSON。

EBAssistant 当前产品语义是“连接用户正在运行的 EB”，因此应优先附着活动实例。不应因为附着失败就静默启动新的 EB；任何创建新 Application 的回退都必须防止反复启动，并等待就绪后再使用。

### 15.4 写入安全与验证策略

可借鉴兄弟项目的“操作、读回、清理、再读回”闭环：

1. 写入前完整预校验。
2. 写入后从 EB 重新解析对象并确认结果。
3. 临时验证对象必须使用明显前缀。
4. 清理时不能只相信 API 返回值，必须再次读回确认对象或关联已消失。
5. 异常路径同样执行显式清理，并记录清理是否成功。
6. 对数据库结构写入，应在界面中要求用户明确确认目标、数量和变更内容。

UndoScope 可以作为额外保护，但不能作为唯一清理手段。兄弟项目的管理员属性定义验证曾遇到 UndoScope 无法开启的场景，最终依赖 `TypeItem.Attributes.Remove`、`ObjectItem.Delete` 和清理读回完成闭环。

EBAssistant 当前不同写入路径的失败策略不同：

- 批量创建属性：中途失败时停止，并尽力删除本批已创建属性。
- 类型定义批量关联：任一写入失败时停止后续操作，已成功的写入保留。
- 工作表批量创建：单个工作表失败后继续其他有效工作表。
- 图形模板复制/新建：已成功创建的对象保留，不做批量删除。
- 权限成员添加和工具面板关联：逐项记录成功、跳过或失败，写入后读回确认。

以后扩展这些路径时，应保持各自失败策略清晰，不要混用。

### 15.5 不应照搬的内容

以下内容属于 EngineeringBaseCodemap 自身治理体系，不是 EBAssistant 的运行架构：

- Harness 状态文件和 feature list 生命周期
- MCP/Gateway 工具暴露
- Codemap/Ontology 建模
- 每个功能都创建 KR、Evidence、Card 的仓库流程

EBAssistant 可以借鉴其“证据优先、版本隔离、显式边界、读回清理”原则，并在本项目中用更轻量的方式落实：代码注释、`AGENTS.md`、操作日志、受控测试和真实 EB 验证记录。
