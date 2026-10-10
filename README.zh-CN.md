# ChatGPT Desktop Theme Studio

> 独立社区项目，与 OpenAI 无关联，也未获其认可。

[![Windows CI](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/alvinczq7-cell/chatgpt-desktop-theme-studio)](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/releases)
[![MIT License](https://img.shields.io/github/license/alvinczq7-cell/chatgpt-desktop-theme-studio)](LICENSE)

一个面向新版 ChatGPT Windows 桌面应用的本地主题设计与管理器，主题编辑覆盖 Chat、Work 和 Codex 三个视图；应用到官方客户端的实时适配仍在验证中。

[English](README.md) · [简体中文](README.zh-CN.md)

![Aurora Motion 动态背景预览](samples/media/aurora-drift.gif)

想体验动态背景？下载 [Aurora Motion 主题包](samples/packages/aurora-motion-1.0.0.tstheme)，再在 Theme Studio 中导入。

## 当前状态

**主题管理器已通过本机 Release 构建、12 项自动化测试、样例主题生成、安装器构建与安装，并从便携目录和安装目录分别启动检查 10 秒。ChatGPT 实际客户端适配仍未验证并保持禁用。** 管理器支持本地主题库、主题包导入/导出、仿真预览和动态背景，但目前不会声称能把主题实时应用到 ChatGPT。

运行目标为 Windows 11 x64、.NET 10 和 Windows App SDK。下载 [最新预览版](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/releases/latest)。完整进度见 [进度检查点](docs/progress-2026-10-10.md)。

## MVP 功能

- Chat、Work、Codex 分别配置主题。
- 本地图片、GIF、MP4、WebM 背景。
- 按界面区域调整颜色、透明度、模糊、字体、圆角和控件状态。
- 静音循环播放；支持手动暂停、省电和减少动态效果暂停策略。
- 可携带媒体的本地主题包；不允许脚本或任意 CSS。
- 仿真预览，方便导出前比较主题设置。
- 故障关闭的客户端适配器：通过具体 ChatGPT 版本验证前保持禁用。

如果你觉得这个项目有用，欢迎点 Star，帮助更多 ChatGPT 桌面端用户发现它。你也可以[报告问题](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/issues/new?template=bug_report.yml)、[提交兼容性结果](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/issues/new?template=compatibility_report.yml)或[建议新功能](https://github.com/alvinczq7-cell/chatgpt-desktop-theme-studio/issues/new?template=feature_request.yml)。请勿在公开反馈中贴入对话内容、账户信息、私人媒体或个人文件路径。

## 构建

使用 Visual Studio 2026 或带 Windows App SDK 组件的 .NET 10 SDK 打开 `ThemeStudio.sln`。应用使用非 MSIX 打包模型。Release 工作流会生成便携目录 ZIP 和 Inno Setup 安装器。

```powershell
dotnet build ThemeStudio.sln -c Release -p:Platform=x64
dotnet test tests/ThemeStudio.Tests/ThemeStudio.Tests.csproj -c Release -p:Platform=x64
```

安装器目前不签名；发布工作流会生成样例主题与 SHA-256 校验文件。Live 主题注入和安全启动仍保持关闭，直到适配器通过版本专项验证。

## 主题包

主题包是 ZIP 容器，包含 `manifest.json`、`theme.json` 和可选的 `assets/` 目录。仅接受声明过的图片/视频媒体；导入时校验路径和扩展名。格式说明见 `docs/theme-package-v1.md`。

## 兼容性与安全

- 首发目标为 Windows 11 x64 和新版 ChatGPT 桌面应用。
- 首版不支持 ChatGPT Classic、浏览器版、Windows 10、ARM64 或 macOS。
- 实时适配器默认关闭；只有完成具体客户端版本的验证后才会启用对应映射。
- 不读取或上传对话内容。计划中的 CDP 连接仅限本机回环地址；CDP 是高权限调试接口，不得暴露到网络。
- 不修改官方应用包中的文件。

## 许可证

应用代码使用 MIT 许可证。主题媒体仍受原作者许可证约束，未经许可不得再分发。
