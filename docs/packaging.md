# 安装包打包说明

本文记录 EBAssistant 离线安装引导和 MSI 的生成方式及边界。

## 产物

打包脚本会生成：

```text
artifacts/installer/EBAssistant-<version>-x86.msi
artifacts/installer/EBAssistant-<version>-x86-Setup.exe
```

MSI 安装界面允许用户选择安装路径，并创建开始菜单快捷方式。

分发给用户时优先提供 Setup.exe：它使用 WiX Burn 原生安装窗口，不依赖尚未安装的 Framework。窗口显示当前 Framework 版本及最低要求 4.6.2；已满足要求时直接安装 EBAssistant，保留现有 Framework。

版本不足时必须勾选同意补装 4.6.2，才能继续。微软安装程序使用自身的交互窗口确认许可条款，取消或失败会停止后续 MSI 安装。MSI 本身仍检测最低版本，防止绕过引导造成依赖缺失。退出码 3010 表示需要重启，1641 表示安装程序要求立即重启；不会把它们当作普通错误。卸载本程序不会卸载 Framework。

## 离线组件准备

将以下微软官方运行时安装程序放到 `artifacts/installer/prerequisites`，保持原文件名。打包前验证 SHA256 和微软数字签名，不会在打包电脑上安装它们：

| 文件 | 官方下载入口 | SHA256 |
| --- | --- | --- |
| NDP462-KB3151800-x86-x64-AllOS-ENU.exe | [4.6.2 离线运行时](https://go.microsoft.com/fwlink/?linkid=2099468) | 8550E370DB2400AEDB4397E9958F12041DEF3BB63C03CC7625CE07E09F42303E |

微软更新下载文件后若校验失败，应重新核对官方来源、签名与版本，再更新固定哈希；不要跳过校验。组件存放在已忽略的 artifacts 目录，不提交二进制文件。

## 包含内容

安装包包含：

- `EBAssistant.exe`
- `net10.0-windows` WinForms 自包含运行时文件
- EB 2023 adapter：`Adapters/2023/EBAssistant.Adapter2023.exe`
- EB 2024 adapter：`Adapters/2024/EBAssistant.Adapter2024.exe`
- EB 2025 占位 adapter：`Adapters/2025/EBAssistant.Adapter2025.exe`
- `Templates/` 下的 Excel 模板
- `docs/操作手册.md`
- 根目录 `CHANGELOG.md`

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

未传入 `-Version` 时，脚本会读取 `EBAssistant.csproj` 中的 `<Version>`。显式传入 `-Version` 时，主程序、MSI 和 Setup.exe 使用同一版本号；正式提交仍须同步项目版本和更新日志。

脚本会：

1. 还原 WiX 本地工具和 UI 扩展。
2. 发布主程序为 `win-x86` self-contained。
3. 构建 EB 2023/2024/2025 adapter 到发布目录。
4. 生成 WiX 源文件。
5. 构建 MSI。
6. 校验微软离线组件，生成包含 MSI 和 4.6.2 的 Setup.exe。

只重新构建安装引导时，可运行 `packaging/package-setup.ps1 -MsiPath <MSI路径> -Version <版本>`。其版本必须与输入 MSI 一致。

## 验证命令

可以用 administrative install 解包验证 MSI 内容，不会真正安装到系统：

```powershell
$msi = Resolve-Path .\artifacts\installer\EBAssistant-1.1.2-x86.msi
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
PFiles/EBAssistant/docs/操作手册.md
PFiles/EBAssistant/CHANGELOG.md
```

开发时可运行 `packaging/Tests/New-SetupUiFixtures.ps1 -Version <版本>` 生成“版本不足”和“已满足要求”的界面测试程序。它们使用正式主题，但禁用所有安装操作，仅用于检查界面，不随安装包分发。真实补装、取消和重启流程仍需在缺少 Framework 的测试电脑上验证。

## 目标机器排查

如果目标机器已经打开 EB 但 EBAssistant 提示未检测到 EB：

- 确认 EB 和 EBAssistant 使用同一 Windows 用户、同一权限级别运行。
- 检查安装目录下是否存在 `Adapters/2024/EBAssistant.Adapter2024.exe` 和同目录 `Interop.Aucotec.dll`。
- 打开提示中的诊断日志，默认位于 `%LOCALAPPDATA%\EBAssistant\Logs\Diagnostics`。
- 诊断日志会记录适配器路径、COM ProgID 是否注册、适配器位数、运行用户、是否管理员、EB 进程路径和适配器返回信息。
