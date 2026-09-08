# AstralParty.Toys

`AstralParty.Toys` 是面向《吉星派对》（Astral Party）的 Windows 本地工具箱，整合离线回放分析、协议帧查看、筹码复盘、游戏素材展示和变速器管理。所有数据处理均在本地完成。

## 功能

### 回放分析

- 自动读取 `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay`，列出最近 100 个回放
- 也可手动选择或拖入无扩展名回放文件
- 展示对局摘要：地图、结果、游戏版本、时间、回合、进度、Boss 与奖励
- 统计玩家表现：角色、生命、攻防、星币、伤害、承伤、治疗、移动、卡牌、技能与筹码
- 按回合查看事件时间线并按玩家或事件筛选
- 记录筹码候选、刷新、选择、品质与来源
- 逐帧查看 protobuf JSON；未知消息显示十六进制载荷
- 导出完整回放 JSON、筹码 JSON/CSV，以及适合 AI 复盘的自然语言文本

### 游戏工具

内置 [speedhack-rs](https://github.com/Hirtol/speedhack-rs) x64 版，并提供图形化管理：

- 自动检测 Steam 游戏目录或手动选择目录
- 安装、更新和卸载 `version.dll` 与 `speedhack_config.json`
- 安装与卸载时使用 SHA-256 校验，避免无意覆盖或删除其它工具的 `version.dll`
- 配置进入游戏后自动启用的基础倍速，无需按快捷键
- 管理多个快捷键倍速档位、点按切换/按住生效、启动阶段加速、配置重载热键和挂接延迟
- DLL 与默认配置模板保存在 `Resources/SpeedhackTools`，并作为程序集资源嵌入 `AstralParty.Toys.dll`；发布目录不需要额外携带这两个源文件

安装、卸载前应完全退出游戏；游戏内需关闭垂直同步。

### 素材与界面

- 地图、角色、怪物、筹码与常用 UI 图片以无损 WebP 形式嵌入程序集
- 应用通过本地虚拟地址直接读取资源流，不生成素材缓存
- 主界面不可用时可以切换至备用界面，回放分析与变速器管理均可使用
- 维基页面可在应用内浏览，也可交给系统默认浏览器打开

## 环境要求

- Windows 10 或 Windows 11（x64）
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Microsoft Edge WebView2 Evergreen Runtime（Windows 10/11 通常已经安装）

从源码构建还需要 .NET 8 SDK 或更高版本。

## 运行

在工作区根目录执行：

```powershell
dotnet run --project .\replaytool\AstralParty.Toys.csproj
```

也可以打开 `replaytool\AstralParty.Toys.slnx` 后从 Visual Studio 启动。

## 构建与发布

构建 Debug 版本：

```powershell
dotnet build .\replaytool\AstralParty.Toys.csproj
```

生成 Windows x64 发布目录：

```powershell
dotnet publish .\replaytool\AstralParty.Toys.csproj `
  -c Release -r win-x64 --self-contained false
```

默认发布入口位于：

```text
replaytool\bin\Release\net8.0-windows\win-x64\publish\AstralParty.Toys.exe
```

## 数据与配置位置

- 游戏回放：`%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay`
- 工具配置：`%APPDATA%\AstralParty.Toys`
- 变速器主配置：`%APPDATA%\AstralParty.Toys\speedhack\speedhack_config.json`
- 游戏安装目录中的变速器：`version.dll` 与 `speedhack_config.json`

从旧版 `AstralParty.ReplayTool` 首次启动新版时，工具会尝试把 `%APPDATA%\AstralParty.ReplayTool` 迁移到 `%APPDATA%\AstralParty.Toys`；如果目录正被占用，则继续使用旧目录，避免丢失配置。

## 回放格式

回放帧使用大端序：

```text
[cmdId:int16][payloadLength:int32][payload]
```

协议类型来自游戏的 HybridCLR 热更新程序集，主要解析逻辑位于 `Services/ReplayAnalyzer.cs` 与 `Services/GameProtocolContext.cs`。

## 素材开发

辅助项目和脚本包括：

- `replaytool-assetpack`：按游戏配置和精确文件名生成素材清单
- `scripts/pack_webp.py`：转换为无损 WebP，并进行像素级回读校验
- 外部素材只按配置字段、文件名与分类匹配，不使用图片识别；缺失素材不会影响回放解析
