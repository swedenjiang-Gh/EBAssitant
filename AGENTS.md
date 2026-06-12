# EBAssistant 项目说明

## 1. 项目定位

EBAssistant 是一个面向 Aucotec Engineering Base（EB）的 Windows 桌面辅助工具，使用 C# 和 Windows Forms 开发。

主界面包含六个功能入口：

- 属性
- 类型定义
- 工作表
- 权限配置
- 图形模板
- 工具面板配置

当前已经实现“属性”和“类型定义”。其余四个入口目前只显示“将在后续开发中实现”的提示。

## 2. 目录与技术栈

项目根目录：`D:\开发\代码仓\EB\EBAssistant`

主要技术：

- 主程序：WinForms、`net10.0-windows`、x86
- EB 2023/2024 适配器：强类型 Aucotec COM Reference、`.NET Framework 4.6.2`、x86
- EB 2025 适配器：`net462` 占位程序；当前不会连接 COM 32
- Excel：`ExcelDataReader`
- 主程序与适配器通信：标准输入/标准输出 JSON
- 主命名空间：`EBAssistant`
- COM 适配器命名空间：`EBAssistant.Adapter`

主程序通过 `EBAssistant.csproj` 排除 `Adapters/**/*.cs`，适配器需要独立构建，不能直接编译进 WinForms 主程序。

## 3. 核心架构

### 3.1 主程序

入口为 `Program.cs`，启动时注册代码页编码提供程序，然后打开 `MainForm`。

`MainForm`：

- 窗口标题为 `EB Assistant`。
- “属性”和“类型定义”使用 `Form.Show()` 打开独立非模态窗口。
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
- `CreateAttributes`
- `CreateAttributeFolder`
- `DeleteEmptyAttributeFolder`
- `GetTypeDefinitionIdentity`
- `GetTypeDefinitionTree`
- `ValidateAttributeIds`
- `ApplyTypeDefinitionDialogs`

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

适配器还实现了 `DeleteEmptyAttributeFolder`，但当前主程序客户端和界面没有暴露删除目录入口。

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

## 6. 数据模型与协议

共享主程序模型集中在 `Models.cs`。

适配器为保持 `.NET Framework 4.6.2` 兼容，在 `Adapters/AdapterProgram.cs` 底部维护一套对应的 `[DataContract]` 模型。

修改请求或响应结构时，必须同步修改两侧模型，否则 JSON 通信可能静默丢字段或解析失败。

主程序使用 `System.Text.Json`；适配器使用 `DataContractJsonSerializer`。

## 7. 构建与运行

主程序构建：

```powershell
dotnet build .\EBAssistant.csproj
```

完整构建设计入口：

```powershell
.\build.ps1
```

`build.ps1` 的设计意图是：

1. 使用 `dotnet build` 构建主程序。
2. 使用 Visual Studio MSBuild 构建 EB 2023/2024 的旧式 `.NET Framework` COM 项目。
3. 使用 `dotnet build` 构建 EB 2025 占位适配器。

主程序输出目标为：

`bin\Debug\net10.0-windows\EBAssistant.exe`

## 8. 当前已知不一致与风险

继续开发前应优先核对以下内容：

1. `build.ps1` 引用的适配器项目名是 `EBAssistant.Adapter2023/2024/2025.csproj`，但目录内实际文件名仍是 `EBAssist.Adapter2023/2024/2025.csproj`。
2. 适配器项目内部的 `AssemblyName`、`RootNamespace` 和输出文件名仍使用 `EBAssist.Adapter...`，但 `EbAdapterClient` 查找的是 `EBAssistant.Adapter...exe`。
3. EB 2023/2024/2025 适配器的 `OutputPath` 仍指向 `net9.0-windows`，而主程序当前目标和输出目录是 `net10.0-windows`。
4. `README.md` 标题仍为 `EBAssist`，且内容没有完全反映当前 `EBAssistant` 命名和 `net10.0-windows` 状态。
5. 当前没有独立测试项目；验证主要依赖构建和真实 EB 环境中的手动或适配器调用测试。
6. EB 2025 目前只是明确报错的占位适配器，不支持实际连接。
7. 属性窗口和类型定义窗口在多个活动 EB 场景中的版本选择行为不一致。

这些问题是通读代码后发现的当前状态；不要在未验证实际构建输出和用户意图前擅自假设它们已经修复。

## 9. 开发约束与建议

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
- 修改类型定义树结构后应提升 `TypeDefinitionCache.ProtocolVersion`，使旧缓存自动失效。

## 10. 快速文件索引

- `Program.cs`：应用入口
- `MainForm.cs`：主界面及六个功能入口
- `EbAdapterClient.cs`：适配器发现、进程调用和 JSON 通信
- `Models.cs`：主程序共享协议模型
- `AttributeFoldersForm.cs`：属性目录树、刷新和右键菜单
- `CreateAttributesForm.cs`：Excel 批量创建属性界面
- `ExcelAttributeImporter.cs`：属性 Excel 读取与预校验
- `AttributeTypeMappingStore.cs`：属性类型映射持久化
- `TypeDefinitionsForm.cs`：类型定义树、缓存加载和复选联动
- `TypeDefinitionDialogForm.cs`：定义对话框批量配置界面
- `DialogDefinitionExcelImporter.cs`：定义对话框 Excel 读取与校验
- `TypeDefinitionCache.cs`：类型定义树缓存
- `TypeDefinitionLogWriter.cs`：类型定义操作日志
- `TypeDefinitionResultForm.cs`：类型定义操作结果展示
- `Adapters/AdapterProgram.cs`：EB 2023/2024 强类型 COM 核心实现
- `Adapters/2025/Program.cs`：EB 2025 占位适配器
- `build.ps1`：完整构建入口

## 11. EngineeringBaseCodemap 可借鉴知识

兄弟目录 `../EngineeringBaseCodemap` 是 EB API 验证、能力边界和真实运行证据知识库。开发 EBAssistant 时应优先查询其中已经验证的知识，但不要把其 Harness、MCP、Gateway、feature list 等治理结构直接搬入本桌面程序。

### 11.1 推荐查询顺序

遇到 EB API、对象类型、写入行为或版本兼容性问题时，按以下顺序查询：

1. `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/`
2. `../EngineeringBaseCodemap/docs/references/engineering-base/evidence/`
3. `../EngineeringBaseCodemap/docs/codemap-cards/platforms/engineering-base/`
4. `../EngineeringBaseCodemap/tools/engineering-base-*` 和 `tools/engineering-base-eb2023-*`
5. 必要时再做小范围、可清理的真实 EB 探测

只把 `confirmed` 或明确版本适用的结论当作实现依据。`partial`、`failed`、`unverified` 只能用于说明边界或设计后续验证。

### 11.2 当前功能可直接借鉴的确认结论

兄弟项目已经用真实 EB 运行验证以下能力：

- EB2024 / COM 31：属性定义创建、TypeItem 关联、解除关联、定义删除，以及类型定义对话框选项卡关联。
- EB2023 / COM 30：同样确认属性定义创建、TypeItem 关联、解除关联、定义删除和类型定义对话框关联。
- EB2023/2024 均确认属性定义 AID 5 名称和 AID 25 注释可以修改、读回和恢复。

确认的公共 API 配方：

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

相关权威记录：

- `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/KR-0069-eb2024-com31-attribute-definition-write.json`
- `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/KR-0070-eb2024-com31-type-definition-dialog-edit.json`
- `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/KR-0080-eb2023-com30-attribute-definition-write.json`
- `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/KR-0081-eb2023-com30-type-definition-dialog-edit.json`
- `../EngineeringBaseCodemap/docs/codemap-cards/platforms/engineering-base/OOE-039.attribute-definition-write.md`
- `../EngineeringBaseCodemap/docs/codemap-cards/platforms/engineering-base/OOE-040.type-definition-dialog-edit.md`

### 11.3 启动、连接和构建可借鉴规则

兄弟项目确认或要求：

- EB COM 项目使用 `net462`、x86 和对应版本 Aucotec COM Reference。
- 禁止使用 C# `dynamic` 访问 EB COM。
- 对象 ID 解析优先使用 `app.Utils.GetSnglObjectByID(id)`。
- 使用 Visual Studio MSBuild 构建带 COM Reference 的 `.NET Framework` 项目；不能只凭 `dotnet build` 判断 COM 适配器可构建。
- EB 自动启动后可能需要分钟级等待 `Application.Name` / `Folders` 就绪，短超时不能证明 API 不可用。
- 控制台适配器输出使用 UTF-8 无 BOM 和结构化 JSON。

启动策略需要结合 EBAssistant 的产品语义区别处理：

- 通用自动化工具若允许启动 EB，可优先强类型 `new Application()`，随后长等待，并保留 `GetActiveObject` 回退。
- EBAssistant 的当前需求是“连接用户正在运行的 EB”，因此应优先附着活动实例。
- 不应因为附着失败就静默启动新的 EB；任何创建新 Application 的回退都必须防止反复启动，并等待就绪后再使用。

开发前可参考：

- `../EngineeringBaseCodemap/docs/codemap-cards/platforms/engineering-base/OOE-000.developer-setup.md`
- `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/KR-0052-eb2024-com31-developer-setup.json`
- `../EngineeringBaseCodemap/harness/knowledge/validated/platforms/engineering-base/KR-0007-eb2024-com31-automation-startup.json`

### 11.4 写入安全与验证策略

可借鉴兄弟项目的“操作、读回、清理、再读回”闭环：

1. 写入前完整预校验。
2. 写入后从 EB 重新解析对象并确认结果。
3. 临时验证对象必须使用明显前缀。
4. 清理时不能只相信 API 返回值，必须再次读回确认对象或关联已消失。
5. 异常路径同样执行显式清理，并记录清理是否成功。
6. 对数据库结构写入，应在界面中要求用户明确确认目标、数量和变更内容。

UndoScope 可以作为额外保护，但不能作为唯一清理手段。兄弟项目的管理员属性定义验证曾遇到 UndoScope 无法开启的场景，最终依赖 `TypeItem.Attributes.Remove`、`ObjectItem.Delete` 和清理读回完成闭环。

EBAssistant 当前批量创建属性已经具备失败回滚；类型定义批量关联按需求保留此前成功结果。以后扩展这两条路径时，应保持其不同失败策略清晰，不要混用。

### 11.5 后续四个功能入口的已知能力边界

#### 工作表

- 工作表查询、打开和导出已有确认路径。
- `worksheet.create.with.columns` 仍属于候选/未开始能力。
- 开发“工作表”入口时，可以先做查询、打开、导出；不要直接假设创建和列配置 API 已确认。

#### 权限配置

- `permission.read` 已有 EB2024 确认证据。
- `permission.write` 仍是候选/未确认能力。
- 首版应优先只读展示，写权限配置前必须另做受控验证。

#### 图形模板

- 通过已存在的图形模板执行 `template.instantiate` 在 EB2023/2024 已有确认路径。
- 自动创建图形模板/模板图例 `template.graphic.create` 在 EB2023/2024 均有失败证据，当前应视为不支持或受阻。
- 不要把模板实例化成功误认为模板创建成功。

#### 工具面板配置

- EB2023/2024 已确认受控路径：定位“工具面板配置”，复制已有面板，修改 AID 5/25，读回，删除临时面板并确认清理。
- 该证据支持以“复制现有配置后修改”为基础设计，不支持任意猜测新的内部面板 API。
- 不得删除用户已有的非目标面板。

### 11.6 不应照搬的内容

以下内容属于 EngineeringBaseCodemap 自身治理体系，不是 EBAssistant 的运行架构：

- Harness 状态文件和 feature list 生命周期
- MCP/Gateway 工具暴露
- Codemap/Ontology 建模
- 每个功能都创建 KR、Evidence、Card 的仓库流程

EBAssistant 可以借鉴其“证据优先、版本隔离、显式边界、读回清理”原则，并在本项目中用更轻量的方式落实：代码注释、`AGENTS.md`、操作日志、受控测试和真实 EB 验证记录。
