# Classworks Desktop

Classworks Desktop 是 Classworks 的桌面端实现，基于 **WinUI 3** 和 **.NET 8** 构建，提供原生 Windows 应用体验。

## 主要特性

- **作业展示** — 自动获取并展示当日作业，支持学科分类与完成状态标记
- **考勤管理** — 课堂考勤记录与展示
- **轮播展示** — 作业/通知信息轮播显示，适合投屏或副屏场景
- **随机点名** — 随机抽取学生姓名，辅助课堂互动
- **实时通知** — 通过 Socket.IO 与服务端保持长连接，接收即时通知与紧急消息
- **系统托盘** — 最小化至系统托盘，后台常驻运行
- **丰富设置** — 涵盖账号、显示、刷新、通知、编辑偏好、学科表、花名册、性能监控、自动更新等

## 技术栈

- **框架**: .NET 8.0 / WinUI 3 (Windows App SDK 2.0.1)
- **通信**: Socket.IO (SocketIOClient)
- **数据**: 本地 KV 存储引擎 / 远程 KV 存储服务
- **系统信息**: System.Management (性能监控)
- **UI**: WinUI 3 原生控件 / Material Design Icons / 自定义动画

## 项目结构

```
CSD/
├── Assets/          # 静态资源（图片、图标、字体、音效）
├── Helpers/         # 工具类（动画、Markdown 渲染等）
├── Models/          # 数据模型与配置实体
├── Services/        # 核心服务（Socket.IO、更新、日志、性能等）
├── Settings/        # 设置模块（各分类设置面板）
├── Views/           # 窗口与 UI 层
├── CSD.Package/     # MSIX 打包工程
└── CSD.Tests/       # 单元测试
```

## 构建与运行

### 前置条件

- Windows 10 版本 1809 (10.0.17763) 或更高
- [Visual Studio 2022](https://visualstudio.microsoft.com/)（需安装"通用 Windows 平台开发"工作负载）
- 或安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 和 [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)

### 构建

```bash
dotnet build CSD.csproj -c Release
```

### 发布

```bash
# 自包含发布（非单文件）
dotnet publish CSD.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

## 配置

应用设置存储在本地 `AppData/Local/CSD` 目录下，主要配置项包括：

- **服务器地址** — 远程 KV 存储服务地址（默认 `https://kv-service.wuyuan.dev`）
- **Token** — 身份认证令牌
- **自动刷新** — 作业数据轮询间隔
- **轮播设置** — 轮播切换间隔、字体大小
- **通知音效** — 可自定义通知提示音