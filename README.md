# EBAssistant

EBAssistant 是面向 Aucotec Engineering Base 的 Windows 桌面辅助工具，用 C#、Windows Forms 和版本化 EB COM 适配器实现。主程序负责界面、Excel 读取、预览、缓存和日志；EB 读写集中在独立适配器进程中，通过 JSON 标准输入/标准输出通信。

## 功能概览

主界面提供六个入口：

- 属性
- 类型定义
- 工作表
- 权限配置
- 图形模板
- 工具面板配置

当前能力：

| 入口 | 已实现内容 |
| --- | --- |
| 属性 | 读取并缓存属性目录树；新建属性目录；从 Excel 批量创建属性并写入注释；支持属性类型映射；结果写入本地日志。 |
| 类型定义 | 读取并缓存类型定义树；父子复选联动；从 Excel 批量把 AID 添加到叶级 TypeItem 的定义对话框选项卡；结果写入本地日志。 |
| 工作表 | 读取并缓存项目模板树；在模板项目中从 Excel 批量创建器件工作表；保存到所选项目自己的 `工作表 / 收藏夹`；EB2023 下将 Excel 第一行写入列标签；结果写入本地日志。 |
| 权限配置 | 读取并缓存“用户及用户组”和“权限控制目录”；把选中的用户或用户组添加到选中的权限目录；只添加成员，不设置具体权限位；结果写入本地日志。 |
| 图形模板 | 读取并缓存图形模板目录；刷新单个目录；复制模板图形到目标末级目录；按已有单个模板补齐指定总数量；批量迁移表格当前用于导入预览。 |
| 工具面板配置 | 共用图形模板缓存，读取并缓存工具面板配置目录；把选中的模板图形关联到已有 Kind 415 工具面板条目；结果写入本地日志。 |

EB 2023 和 EB 2024 通过强类型 COM 适配器连接。EB 2025 适配器目前是占位程序，会明确提示本机未注册 EB 2025 COM 32 类型库，当前不执行实际连接。

## 架构

- 主程序：`net10.0-windows`、WinForms、x86。
- EB 2023/2024 适配器：`.NET Framework 4.6.2`、x86、强类型 Aucotec COM Reference。
- EB 2025 适配器：`net462` 占位程序。
- Excel 读取：`ExcelDataReader`。
- 通信协议：主程序调用适配器 EXE，传入操作名，通过 stdin/stdout 交换 JSON。
- 主命名空间：`EBAssistant`。
- 适配器命名空间：`EBAssistant.Adapter`。

主程序不会直接引用 Aucotec COM 类型，也不使用 `dynamic` 操作 EB。新增适配器操作时，需要同步维护主程序模型和 `Adapters/AdapterProgram.cs` 底部的 `[DataContract]` 模型。

## 目录结构

```text
EBAssistant/
  Adapters/                 EB 2023/2024/2025 适配器项目
  Templates/                Excel 模板
  Tests/                    本地轻量测试项目
  docs/                     设计记录、复盘和操作文档
  CHANGELOG.md              版本更新日志
  EBAssistant.csproj        WinForms 主程序
  build.ps1                 主程序与适配器完整构建入口
```

常用源码入口：

- `Program.cs`：应用入口。
- `MainForm.cs`：主界面、菜单和六个功能入口。
- `EbAdapterClient.cs`：适配器发现、进程调用和 JSON 通信。
- `Models.cs`：主程序共享协议模型。
- `Adapters/AdapterProgram.cs`：EB 2023/2024 COM 适配器核心实现。
- `Adapters/2025/Program.cs`：EB 2025 占位适配器。

## 构建与运行

完整构建：

```powershell
.\build.ps1
```

构建脚本会依次构建：

1. WinForms 主程序。
2. EB 2023 适配器。
3. EB 2024 适配器。
4. EB 2025 占位适配器。

仅构建主程序：

```powershell
dotnet build .\EBAssistant.csproj
```

运行主程序：

```powershell
dotnet run --project .\EBAssistant.csproj
```

完整构建后也可以直接启动：

```powershell
.\bin\Debug\net10.0-windows\EBAssistant.exe
```

本地测试：

```powershell
dotnet run --project .\Tests\EBAssistant.Tests.csproj
```

## 使用前提

- Windows 桌面环境。
- 已安装 .NET SDK，支持 `net10.0-windows`。
- 构建 EB 2023/2024 适配器需要 Visual Studio MSBuild 和对应 EB COM Reference。
- 使用 EB 功能前，需要先打开 Engineering Base 并连接目标数据库。
- 对 EB 结构有写入影响的操作应先确认目标、数量和预览结果。

## 模板、缓存与日志

内置 Excel 模板位于 `Templates/`，主界面“文件 / 下载模板”可复制到用户选择的目录。主界面“关于 / 帮助”打开 `docs/操作手册.md`。

缓存默认位于：

```text
%LOCALAPPDATA%\EBAssistant\Cache
```

日志默认位于：

```text
%LOCALAPPDATA%\EBAssistant\Logs
```

各批量写入功能会保存 JSON 和 TXT 日志，并在结果窗口中提供日志目录入口。

## 功能边界

- 图形模板迁移使用复制/粘贴语义，源模板保留。
- 图形模板“按表格迁移”当前只完成导入预览，确认写入尚未开放。
- 工作表列标签在 EB2023 中通过受控 SQL 内部列记录写入；EB2024 暂未开放该写入路径。
- 权限配置当前只添加权限成员，不调用 `SetRight` 设置具体权限位。
- 工具面板配置写入后，EB 当前进程可能需要重启才能看到工具面板配置变化。
- EB 2025 当前不可用，不能当作已支持版本。

详细操作步骤见 [docs/操作手册.md](docs/操作手册.md)。
