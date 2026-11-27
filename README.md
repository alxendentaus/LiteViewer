# LiteViewer · 轻量日志 / 代码查看器

一个 **轻量、快速、可编辑、多主题** 的 Windows 桌面日志与代码查看器。采用 C# / .NET 9 WinForms 编写，核心是自绘的虚拟滚动文本控件，打开上百 MB 的大日志依然流畅。界面风格对齐 VS Code，支持系统右键菜单直接打开，并能一键运行 Python / C++ 代码查看输出。

> 作者 / 署名：**alxendentaus** · 开源仓库：<https://github.com/alxendentaus>

---

## 功能特性

- **虚拟滚动，大文件友好**：自绘文本区只渲染可见行，几百 MB 日志也不卡顿；支持纵向 + 横向滚动，超长行可横向查看。
- **可编辑**：像记事本一样直接修改内容，支持光标、选区、撤销/重做、复制/粘贴、保存 / 另存为，未保存有 `*` 与 `●` 提示。
- **语法与日志高亮**：日志级别（ERROR/WARN/INFO/DEBUG/TRACE）、时间、标签、代码关键字/字符串/注释/数字分色显示，配色随主题。
- **搜索与过滤**：关键字查找（F3 下一个 / Shift+F3 上一个）、支持正则与大小写切换、仅显示匹配行、按日志级别过滤。
- **12 套主题**：VS Code 暗色/亮色、One Dark、Dracula、Nord、Monokai、Solarized 暗/亮、GitHub 暗色、护眼豆沙绿、午夜蓝、羊皮纸（暖色）；菜单与活动栏按钮均可切换，偏好自动保存。
- **字体样式**：系统字体对话框设置字体 / 字形 / 字号，行高与行号区自动适配；可一键恢复默认（Consolas 12）。
- **混合编码智能识别**：默认「自动」模式按行识别 UTF-8 / GBK，兼容 UTF-8 与 GBK 混排的日志；也可手动指定编码；自动过滤 NUL 字节。
- **系统右键菜单**：安装后可在资源管理器中右键任意文件用 LiteViewer 打开（写入 HKCU，无需管理员权限）。
- **简易代码运行**：F5 运行当前 Python / C++ 代码，底部输出面板显示结果与红色报错（含行号）；有未保存修改时运行临时副本，不污染原文件。
- **自动跟随**：可选「自动跟随最新日志」，尾部追加内容时自动滚动到底。
- **主题统一的滚动条**：主视图与输出面板均为自绘滚动条，颜色跟随当前主题。
- **署名信息**：状态栏右下角显示作者署名，「工具 → 关于 / 署名」查看作者与开源仓库，点击署名或作者名可打开仓库页面。

## 界面预览

左侧为 VS Code 风格活动栏（打开文件 / 查找 / 运行 / 切换主题 / 设置），顶部为编辑器标签条与查找条，中部为文本区，底部为状态栏（含署名）。整体配色随主题切换。

## 目录结构

```
.
├── NativeLogViewer/          # WinForms 主程序源码（.NET 9）
│   ├── Program.cs            # 入口、命令行参数、全局异常捕获
│   ├── Form1.cs              # 主界面：菜单 / 工具栏 / 状态栏 / 主题 / 设置 / 文件
│   ├── Form1.Run.cs          # 代码运行面板：Python / C++ 运行器与输出捕获
│   ├── LogView.cs            # 核心自绘文本控件：虚拟滚动 / 编辑 / 高亮 / 搜索 / 选区
│   ├── Theme.cs              # 12 套主题配色与定义
│   ├── ThemeRenderer.cs      # 扁平化菜单 / 工具栏渲染器
│   ├── ThemedScrollBar.cs    # 自绘、跟随主题的滚动条
│   ├── VsCodeUI.cs           # 活动栏 ActivityBar 与标签条 EditorTabs
│   ├── FontSettings.cs       # 字体设置数据模型
│   ├── AboutForm.cs          # 关于 / 署名窗口
│   ├── Resources/app.ico     # 应用图标（透明背景）
│   └── NativeLogViewer.csproj
├── tools/
│   ├── setup.iss             # Inno Setup 安装包脚本
│   └── make_icon.py          # 图标生成脚本（Pillow）
└── 更新历史.md               # 版本演进记录
```

## 编译与使用

### 环境要求

- .NET 9 SDK（编译需要）
- Windows 10 / 11
- 运行轻量版需要本机安装 .NET 9 运行时；独立版不需要任何运行时

### 编译

```bash
# 轻量版（约 285 KB，依赖本机 .NET 9）
dotnet publish NativeLogViewer/NativeLogViewer.csproj -c Release -r win-x64 \
  --self-contained false -p:PublishSingleFile=true -o publish-light

# 独立版（约 49 MB，含压缩后的 .NET 运行时，开箱即用）
dotnet publish NativeLogViewer/NativeLogViewer.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish-standalone
```

> 说明：WinForms 不支持剪裁（`<PublishTrimmed>` 会触发 NETSDK1175），独立版通过 `EnableCompressionInSingleFile` 压缩来控制体积。

### 安装包

使用 Inno Setup 6 编译 `tools/setup.iss`，生成 `installer/LiteViewer-Setup-v1.0.exe`。安装时可选创建桌面 / 开始菜单快捷方式与右键菜单；卸载会清理设置文件。

### 打开方式

- 直接双击 exe 启动
- 命令行传入文件路径：`NativeLogViewer.exe "D:\logs\app.log"`
- 安装右键菜单后：资源管理器中右键文件 → 用 LiteViewer 打开
- 启动后拖拽文件到窗口也可打开

## 快捷键

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl + O` | 打开文件 |
| `Ctrl + S` / `Ctrl + Shift + S` | 保存 / 另存为 |
| `Ctrl + Z` / `Ctrl + Y` | 撤销 / 重做 |
| `Ctrl + F` | 聚焦查找框 |
| `F3` / `Shift + F3` | 下一个 / 上一个匹配 |
| `F5` | 运行当前代码 |
| `Ctrl + End` | 跳到文件末尾 |

## 配置文件

设置保存在 `%LOCALAPPDATA%\LiteViewer\settings.json`，包含主题、编码、过滤、字体、代码运行工具路径等偏好。删除该文件即可恢复默认设置。

## 关于代码运行

- Python：自动从 PATH 探测 `python.exe` / `python3.exe` / `py.exe`，设置 `PYTHONIOENCODING=utf-8` 避免中文乱码。
- C++：探测 `g++` / `clang++` / `cl`；GCC/Clang 使用 `-static-libgcc -static-libstdc++ -static` 静态链接，避免运行时缺少 MinGW DLL。
- 也可以在「运行 → 运行工具设置」中手动指定解释器 / 编译器路径。

## 上传到 GitHub

源码已整理为本地 git 仓库。若要推送到你自己的 GitHub：

1. 到 <https://github.com/new> 创建一个名为 `LiteViewer` 的**空**公开仓库（不要勾选 README / .gitignore / license）。
2. 在本目录执行（会自动检测仓库并用本机 GitHub 凭据登录）：

   ```bash
   git remote add origin https://github.com/alxendentaus/LiteViewer.git
   git push -u origin main
   ```

   Windows 用户也可直接双击运行 `一键上传到GitHub.bat`，它会引导你创建仓库并完成推送。

   > 若你的系统 PATH 里没有 git（cmd 执行 `git` 报"不是内部或外部命令"），用本机 WorkBuddy 自带的 git 完整路径：
   > ```
   > "C:\Program Files\WorkBuddy\resources\vendor\PortableGit\mingw64\bin\git.exe" push -u origin main
   > ```
   > 一键脚本已自动处理这个问题，会查找 git.exe 的完整路径。

> 若尚未登录 GitHub，可安装 GitHub Desktop 登录后重试，或配置 Personal Access Token。

## 许可

本项目仅供学习与个人使用。作者：alxendentaus。

---

*最后更新：2026-11-27*
