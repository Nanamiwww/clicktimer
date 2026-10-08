# ClickTimer 鼠标定时点击器

一个 Windows 桌面小工具：等待指定时间后点击一次，再按设定间隔点击第二次，最后将鼠标移到预先设置的位置。两次点击可以使用同一个位置，也可以分别设置。

## 下载

- [下载 v1.2 Windows 便携版](https://github.com/Nanamiwww/clicktimer/releases/download/v1.2/ClickTimer-v1.2-windows.zip)：解压后运行 `ClickTimer.exe`。
- [下载 v1.2 原始源码包](https://github.com/Nanamiwww/clicktimer/releases/download/v1.2/ClickTimer-source-v1.2.zip)。
- [查看全部版本及 SHA-256 校验文件](https://github.com/Nanamiwww/clicktimer/releases)。

运行环境：Windows，已安装或启用 .NET Framework 4.x。便携包不包含 .NET 运行时。GitHub 自动生成的 **Source code** 下载项是仓库源码快照；直接使用请下载 **ClickTimer-v1.2-windows.zip**。

## 功能

- 设置第一次点击前的延迟，以及两次点击之间的间隔；间隔最短为 **0.1 秒**。
- 定格屏幕拾取坐标，配有放大镜、方向键微调和位置显示。
- 支持两次点击同一位置，或使用不同的第二次点击位置。
- 点击结束后自动移开鼠标，运行中显示状态、剩余时间和操作日志。
- **F9** 停止尚未执行的点击；热键被其他程序占用时，使用窗口内的停止按钮。
- 自动保存位置、时间和置顶等设置，设置文件位于 `%APPDATA%\ClickTimer\settings.ini`。

## 使用

1. 解压便携版，打开 `ClickTimer.exe`。
2. 设置第一次点击位置和鼠标移开位置。需要不同的第二次点击位置时，启用对应选项并设置坐标。
3. 点击“拾取”后，在定格画面中选择目标；可使用方向键微调，按 Esc 取消。
4. 设置延迟与间隔，点击“开始”。一次运行执行两次点击，不会无限循环。
5. 需要中止时按 F9 或点击“停止”。已经按下的那次点击会先尝试完成抬起。

若界面提示鼠标主按钮可能仍按着，请按界面提示在原地松开；原地松开可能完成那次点击。点击日志中的“已发出”表示程序发出了输入，不代表目标程序已经完成相应操作。

计时采用独立线程和 `Stopwatch`，实际误差仍受系统负载、线程调度及目标程序响应影响。日志记录实际延迟与间隔。

## 从源码构建

解压源码包，或下载此仓库，然后在 Windows 下运行 `build.bat`，在同一目录生成 `ClickTimer.exe`。构建脚本使用 .NET Framework 的 `csc.exe`，不需要 NuGet 依赖。若提示找不到编译器，请先安装或启用包含该编译器的 .NET Framework。

| 文件 | 作用 |
| --- | --- |
| `Program.cs` | 入口、版本信息和设置保存 |
| `MainForm.cs` / `MainForm.Layout.cs` | 主窗口及交互逻辑 |
| `Engine.cs` | 计时、点击和停止处理 |
| `Picker.cs` | 坐标拾取、放大镜和位置标记 |
| `Native.cs` | Windows 输入、DPI 和热键接口 |
| `Theme.cs` / `app.ico` | 界面样式和图标 |
| `build.bat` | Windows 本地构建脚本 |
| `README.txt` | 原始源码包说明 |

## v1.2

根据随源码提供的版本记录，本版更新了界面，补充拾取过程的点击拦截和鼠标按钮未确认松开时的恢复流程，并修订点击结果与计时说明。

发布的可执行文件由随附 v1.2 源码编译生成，文件版本为 `1.2.0.0`。本次发布已完成编译与文件完整性检查，未进行真实鼠标点击的端到端测试。

## 校验

在 PowerShell 中运行 `Get-FileHash .\ClickTimer-v1.2-windows.zip -Algorithm SHA256`，将结果与同一版本的 `SHA256SUMS.txt` 比较。

## 许可

当前提供的源码未附带开源许可证，本仓库保留这一状态。
