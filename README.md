<p align="center">
  <img src="https://github.com/user-attachments/assets/3d6fe00d-e424-460a-87a4-39cbb401137f" alt="ClickTimer v1.2 鼠标定时点击器功能展示" width="960">
</p>

ClickTimer 是一个 Windows 桌面小工具：设好位置、延迟和间隔，按顺序完成**两次点击**，随后将鼠标移到指定位置。两次点击可以落在同一点，也可以分别设置。

**[下载 Windows 便携版 · v1.2](https://github.com/Nanamiwww/clicktimer/releases/download/v1.2/ClickTimer-v1.2-windows.zip)**　·　[源码包](https://github.com/Nanamiwww/clicktimer/releases/download/v1.2/ClickTimer-source-v1.2.zip)　·　[SHA-256 校验](https://github.com/Nanamiwww/clicktimer/releases/download/v1.2/SHA256SUMS.txt)　·　[版本说明](https://github.com/Nanamiwww/clicktimer/releases/tag/v1.2)

解压后运行 `ClickTimer.exe`。需要 **Windows 与 .NET Framework 4.x**，便携包不包含运行时。

## 一次运行，两次点击

**延迟**决定开始后多久执行第一次点击，**间隔**决定两次点击相隔多久。第二次点击完成后，鼠标自动移开，本次任务结束；两次点击的最短间隔为 **0.1 秒**。

<p align="center">
  <img src="https://github.com/user-attachments/assets/3e25f96f-8140-48b6-b906-95ac0040e5d0" alt="延迟等待、第一次点击、间隔等待、第二次点击、鼠标移开的操作流程" width="960">
</p>
<p align="center"><sub>封面与流程图均为 AI 生成的功能示意，非实际软件界面。</sub></p>

## 选点、计时与停止

- **定格画面选点**：在静止的屏幕画面中拾取坐标，配合放大镜与方向键微调；按 Esc 取消。
- **运行状态可见**：显示当前阶段、剩余时间与操作日志，方便查看执行过程。
- **停止后续点击**：按 **F9** 或点击窗口内的“停止”；若热键被其他程序占用，可使用停止按钮。
- **保留常用设置**：自动保存位置、时间与窗口置顶等设置，便于下次继续使用。

## 快速上手

1. **打开程序**：下载并解压 Windows 便携版，运行 `ClickTimer.exe`。
2. **设置位置**：拾取第一次点击位置和鼠标移开位置；需要点击两个不同位置时，启用第二次点击的独立位置选项。
3. **设置时间**：填写第一次点击前的延迟，以及两次点击之间的间隔。
4. **开始执行**：点击“开始”，查看状态与日志；需要中止时按 F9 或点击“停止”。

## v1.2 更新重点

- **更新主窗口界面**，整理位置、时间与运行状态的呈现。
- **完善拾取过程**，补充准备与定格阶段的点击拦截和取消处理。
- **补充异常恢复流程**，处理鼠标按钮可能仍按着的状态，并修订点击结果与计时说明。

<details>
<summary>计时、停止与点击结果说明</summary>

- 实际延迟与间隔会受系统负载、线程调度和目标程序响应影响，日志中记录运行时的实际时间。
- 停止操作用于取消尚未执行的点击。已经按下的那次点击会先尝试完成抬起。
- 日志中的“已发出”表示程序发出了输入，不代表目标程序已经完成相应操作。
- 若界面提示鼠标主按钮可能仍按着，请按界面提示在原地松开；原地松开可能完成那次点击。
- 设置文件保存在 `%APPDATA%\ClickTimer\settings.ini`。

</details>

<details>
<summary>下载包、源码与校验说明</summary>

- **ClickTimer-v1.2-windows.zip**：Windows 便携版，包含 `ClickTimer.exe` 与使用说明。
- **ClickTimer-source-v1.2.zip**：原始 v1.2 源码，附 `build.bat`，可用 .NET Framework 的 C# 编译器在 Windows 下构建。
- **SHA256SUMS.txt**：以上两个压缩包的 SHA-256 校验值。

直接使用请下载上方的 Windows 便携版。GitHub 自动生成的 `Source code` 附件是仓库源码快照。

现有发布程序由原始 v1.2 源码编译，文件版本为 **1.2.0.0**。发布时已完成编译和文件完整性检查，未进行真实鼠标点击的端到端测试。

</details>

<details>
<summary>从源码构建与文件说明</summary>

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

</details>

## 许可

当前提供的源码未附带开源许可证，本仓库保留这一状态。
