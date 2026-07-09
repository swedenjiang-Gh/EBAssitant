# 安装包打包说明

本文记录 EBAssistant 当前 MSI 安装包的生成方式和边界。

## 产物

打包脚本会生成：

```text
artifacts/installer/EBAssistant-<version>-x86.msi
```

MSI 安装界面允许用户选择安装路径，并创建开始菜单快捷方式。

## 包含内容

安装包包含：

- `EBAssistant.exe`
- `net10.0-windows` WinForms 自包含运行时文件
- EB 2023 adapter：`Adapters/2023/EBAssistant.Adapter2023.exe`
- EB 2024 adapter：`Adapters/2024/EBAssistant.Adapter2024.exe`
- EB 2025 占位 adapter：`Adapters/2025/EBAssistant.Adapter2025.exe`
- `Templates/` 下的 Excel 模板、帮助 PDF、版本信息

安装包不包含 Engineering Base 本体，也不注册 Aucotec COM。目标机器仍需预先安装对应版本 EB，并能正常打开目标数据库。

## 本机要求

打包机需要：

- .NET SDK 10
- Visual Studio MSBuild
- EB 2023/2024 COM Reference，用于构建版本 adapter
- 网络可访问 NuGet，首次执行时用于还原 WiX 本地工具

WiX 通过仓库根目录的 `dotnet-tools.json` 还原。`.wix/` 是本机扩展缓存，不提交仓库。

## 打包命令

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\packaging\package-installer.ps1
```

未传入 `-Version` 时，脚本会读取 `EBAssistant.csproj` 中的 `<Version>` 作为 MSI 版本号。需要临时指定版本时仍可显式传入 `-Version 1.1.1`。

脚本会：

1. 还原 WiX 本地工具和 UI 扩展。
2. 发布主程序为 `win-x86` self-contained。
3. 构建 EB 2023/2024/2025 adapter 到发布目录。
4. 生成 WiX 源文件。
5. 构建 MSI。

## 验证命令

可以用 administrative install 解包验证 MSI 内容，不会真正安装到系统：

```powershell
$msi = Resolve-Path .\artifacts\installer\EBAssistant-1.1.1-x86.msi
$target = Join-Path (Resolve-Path .\artifacts\installer) "admin-test\manual"
New-Item -ItemType Directory -Path $target -Force | Out-Null
Start-Process msiexec.exe -ArgumentList @('/a', $msi.Path, '/qn', "TARGETDIR=$target") -Wait
```

然后检查：

```text
PFiles/EBAssistant/EBAssistant.exe
PFiles/EBAssistant/hostfxr.dll
PFiles/EBAssistant/coreclr.dll
PFiles/EBAssistant/System.Windows.Forms.dll
PFiles/EBAssistant/Adapters/2023/EBAssistant.Adapter2023.exe
PFiles/EBAssistant/Adapters/2024/EBAssistant.Adapter2024.exe
PFiles/EBAssistant/Adapters/2025/EBAssistant.Adapter2025.exe
PFiles/EBAssistant/Templates/创建属性模板.xlsx
PFiles/EBAssistant/Templates/工作表模板.xlsx
PFiles/EBAssistant/Templates/权限配置模板.xlsx
PFiles/EBAssistant/Templates/类型定义模板.xlsx
PFiles/EBAssistant/Templates/迁移模板图形模板.xlsx
PFiles/EBAssistant/Templates/帮助手册.pdf
PFiles/EBAssistant/Templates/版本信息.txt
```

## 目标机器排查

如果目标机器已经打开 EB 但 EBAssistant 提示未检测到 EB：

- 确认 EB 和 EBAssistant 使用同一 Windows 用户、同一权限级别运行。
- 检查安装目录下是否存在 `Adapters/2024/EBAssistant.Adapter2024.exe` 和同目录 `Interop.Aucotec.dll`。
- 打开提示中的诊断日志，默认位于 `%LOCALAPPDATA%\EBAssistant\Logs\Diagnostics`。
- 诊断日志会记录适配器路径、COM ProgID 是否注册、适配器位数、运行用户、是否管理员、EB 进程路径和适配器返回信息。
