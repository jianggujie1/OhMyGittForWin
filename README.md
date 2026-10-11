# OhMyGittForWin 🪐

<div align="center">

![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square&logo=windows)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![WPF](https://img.shields.io/badge/GUI-WPF%20XAML-239120?style=flat-square)
![Git](https://img.shields.io/badge/Git-LibGit2Sharp%20%2B%20CLI-F05032?style=flat-square&logo=git)
![Style](https://img.shields.io/badge/Style-Dark%20Neomorphism-FF7700?style=flat-square)

**新拟态视觉美学 · IntelliJ IDEA 级双栏 Diff · 3-Way 冲突解决器 · 极简高效的 Windows 原生 Git 桌面引擎**

</div>

---

## 📖 简介 (Introduction)

**OhMyGittForWin** 是一款为 Windows 开发者量身打造的现代化 Git GUI 桌面客户端。

它融合了**沉浸式暗色新拟态 (Dark Neomorphic)** 视觉设计与 **IntelliJ IDEA** 严谨专业的工作流体验，提供极速响应的双栏代码对比、直观可视化的三方冲突解决、一键式远程仓库与分支管理，彻底告别平庸臃肿的传统 Git 工具。

---

## ✨ 核心特色 (Key Features)

### 1. 🎨 沉浸式暗色新拟态美学 (Dark Neomorphic UI)
* **光影浮雕质感**：精心调配的双重内外阴影（Soft Outer & Sunken Inner Shadows），在深空蓝灰底色上呈现立体且克制的高级质感。
* **暖橙流光点缀**：以渐变霓虹橙（`#FF5500` ~ `#FF8800`）作为视觉焦点与交互引导，兼具现代感与辨识度。
* **流畅动效反馈**：按钮凹陷悬浮状态切换自然，减少长时间高强度开发工作中的视觉疲劳。

### 2. 🔍 IntelliJ IDEA 级双栏 Diff 比较器 (Side-by-Side Diff)
* **严谨 50/50 分栏布局**：两侧视口宽度严格等分，正中锁定 124px 行号与增删改状态装订槽，杜绝任何错位或溢出覆盖。
* **双向联动横向滚动**：长代码行无需折行破坏结构，通过底部同步滑块或 `Shift + 鼠标滚轮` 实现左右双栏绝对同步平滑滑动。
* **字符级精细高亮 (Intra-line Word Highlighting)**：精准识别修改行内部的新增、修改与删除字符片段，与 IDEA 经典配色深度对齐。
* **智能改动聚合**：连续修改行整体打包展示，告别碎裂凌乱的 Diff 块。

### 3. ⚡ 3-Way 经典三方冲突合并器 (Three-Way Merge Resolver)
* **三栏合并视窗**：左侧本地修改（Ours）、中间实时合并结果预览与编辑区（Result）、右侧远程传入修改（Theirs）。
* **一键采纳解决**：针对冲突代码块支持一键应用左侧或右侧版本，自动剔除 `<<<<<<<`、`=======`、`>>>>>>>` 冲突标记并安全写入文件。

### 4. 🌐 远程仓库与分支管理 (Remote & Branch Operations)
* **一键推送并建立追踪**：支持一键执行 `git push -u origin <branch>`，自动在远程创建同名远程分支并建立上游追踪关系。
* **GitHub Token 绑定**：内置 GitHub Personal Access Token (PAT) 直达配置通道，一键打开权限预选页面，彻底解决 GitHub 废止密码推送后的认证痛点。
* **分支快捷操作**：支持创建新本地分支、双击快速切换检出、检出远程分支自动创建本地跟踪分支、安全删除分支。
* **远程同步拉取**：一键执行 `git fetch --prune` 实时同步远程分支变更。

### 5. 📦 工作区状态与提交历史 (Log & Commit Workspace)
* **实时状态检视**：清晰展示暂存（Staged）与未暂存（Unstaged）文件清单，支持单文件/全部一键暂存与撤销。
* **即时过滤搜索**：支持在提交树中按提交信息（Commit Message）、作者（Author）及短哈希（Commit SHA）进行毫秒级实时过滤。

---

## 🛠️ 技术栈 (Tech Stack)

| 模块 | 技术选型 | 说明 |
| :--- | :--- | :--- |
| **运行时框架** | .NET 8.0 (`net8.0-windows`) | 依托现代 .NET 8 卓越的运行时性能与 Windows 深度集成 |
| **GUI 架构** | WPF (Windows Presentation Foundation) | 纯矢量 XAML 布局，GPU 硬件加速与高 DPI 自适应 |
| **设计模式** | MVVM (Model-View-ViewModel) | 采用官方 `CommunityToolkit.Mvvm` 8.4 实现轻量解耦 |
| **Git 核心引擎** | `LibGit2Sharp` (v0.31.0) | 高性能原生 C++ Git 内核绑定，极速读取仓库索引与提交历史 |
| **认证与交互** | Git for Windows CLI 通道 | 原生联动系统级 `Git Credential Manager` (GCM) 与 SSH 凭据 |

---

## 🚀 快速上手 (Getting Started)

### 环境依赖
1. **操作系统**：Windows 10 (1809+) / Windows 11
2. **运行环境**：[.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
3. **Git 工具**：[Git for Windows](https://git-scm.com/download/win)（建议 2.30+）

### 获取源码并编译运行
```bash
# 1. 克隆本仓库
git clone https://github.com/jianggujie1/OhMyGittForWin.git
cd OhMyGittForWin

# 2. 编译项目
dotnet build

# 3. 运行应用程序
dotnet run --project src/OhMyGittForWin/OhMyGittForWin.csproj
```

编译输出目录位于：
```
src/OhMyGittForWin/bin/Debug/net8.0-windows/OhMyGittForWin.exe
```

---

## 💡 使用指南 (Usage Guide)

### 1. 打开仓库
* 启动软件后默认尝试加载当前工作目录；
* 亦可点击右上角 **`📂 打开仓库`** 按钮，选择任意包含 `.git` 文件夹的本地代码根目录。

### 2. 比较文件差异 (Diff)
* 在 **LOG** 选项卡中，点击任意提交历史，并在右侧文件列表选择具体修改文件，即刻展现 IDEA 级双栏 Diff；
* 在 **COMMIT** 选项卡中，点击修改中的工作区文件，可直观对比暂存前后的变更；
* 当单行代码较长时，按住 **`Shift` 滚动鼠标滚轮** 或拖动底部滑块，双侧将同步横向滚动。

### 3. 添加远程并推送到 GitHub
1. 点击顶部栏 **`🌐 远程管理`**；
2. 在下方输入远程源名称（如 `origin`）及你的仓库地址（如 `https://github.com/your-name/your-repo.git`），点击 **`＋ 添加远程`**；
3. **绑定 Token**（如需）：点击选中 `origin` -> 点击 **`🔑 绑定 Token`** -> 点击直达按钮生成带 `repo` 权限的 GitHub Token 粘贴并确认；
4. 点击主界面的 **`⬆ 推送 (Push)`** 或弹窗内的 **`🚀 推送并创建远程分支 (Push -u)`** 即可顺利推送到远程。

### 4. 解决代码冲突 (3-Way Merge)
* 当合并代码遇到冲突时，点击顶部栏 **`⚡ 3-Way 冲突解决器`**；
* 选择带有冲突标记的文件，在三栏界面中审阅 Ours 与 Theirs 修改，点击解决按钮完成合并并保存。

---

## 📂 项目结构 (Project Structure)

```text
OhMyGittForWin/
├── src/
│   └── OhMyGittForWin/
│       ├── Converters/       # XAML 转换器 (Diff 类型配色、布尔值等)
│       ├── Models/           # 数据实体 (GitBranchItem, GitCommitItem, IdeaFileDiff 等)
│       ├── Services/         # 业务逻辑服务 (GitService, DiffService, MergeService)
│       ├── Styles/           # 新拟态画刷、发光阴影与控件样式字典
│       ├── ViewModels/       # 主窗体与弹窗视图模型 (MainViewModel)
│       ├── Views/            # 界面视图 (MainWindow, IdeaDiffViewer, ManageRemotes 等)
│       └── App.xaml          # 全局应用入口与资源聚合
├── OhMyGittForWin.slnx       # 解决方案定义文件
├── .gitignore                # Git 忽略配置
└── README.md                 # 项目说明文档
```

---

## 🤝 贡献与反馈 (Contributing)

欢迎提交 Issue 与 Pull Request！如果你在使用过程中遇到任何体验问题或有新的功能建议，欢迎畅所欲言。

---

## 📄 开源许可证 (License)

本项目采用 [MIT License](LICENSE) 开源许可证。
