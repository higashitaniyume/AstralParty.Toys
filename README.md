<div align="center">

# AstralParty.Toys

## 感谢您选择 AstralParty.Toys！

### 反馈问题与建议：企鹅群 **1078464597** ｜ [Discord](https://discord.gg/SZaYSwGX94)

<img width="128" height="128" alt="UT_AccountHeadShot_70902" src="https://github.com/user-attachments/assets/5033cec1-86a9-41ef-8bda-af56f36a3a0e" />


回放复盘 · 皮肤创作 · 模组管理 · 游戏变速

[![Release](https://img.shields.io/github/v/release/higashitaniyume/AstralParty.Toys?include_prereleases&style=flat-square&label=release)](https://github.com/higashitaniyume/AstralParty.Toys/releases)
[![Release CI](https://img.shields.io/github/actions/workflow/status/higashitaniyume/AstralParty.Toys/release.yml?style=flat-square&label=Release%20CI)](https://github.com/higashitaniyume/AstralParty.Toys/actions/workflows/release.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-555?style=flat-square)](#安装)
[![License](https://img.shields.io/badge/license-AGPL--3.0-555?style=flat-square)](LICENSE)

![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![WebView2](https://img.shields.io/badge/WebView2-555?style=flat-square)

[下载最新版](https://github.com/higashitaniyume/AstralParty.Toys/releases/latest) · [更新记录](CHANGELOG.md) · [CesiumLoader](https://github.com/higashitaniyume/CesiumLoader)

</div>

---

整合回放库、离线分析、角色与卡牌皮肤编辑，以及配套加载器 CesiumLoader。回放解析与素材转换在本机完成，下载回放、获取更新和部分模组功能需要联网。

## 安装

<!-- 通过 WinGet 安装：

```powershell
winget install Hiynet.AstralParty.Toys
```

更新已安装版本：

```powershell
winget upgrade Hiynet.AstralParty.Toys
```

也可从 [Releases](https://github.com/higashitaniyume/AstralParty.Toys/releases/latest) 下载，优先选择 **x64 自包含安装包**。 -->

下载时看文件名：**`self-contained` = 自包含，自带 .NET 8**；**`framework-dependent` = 框架依赖，需要另行安装 .NET 8 Desktop Runtime**。不确定选哪个时，推荐 `win-x64-self-contained-installer.exe`。

所有文件名均以 `AstralParty.Toys-<版本>-` 开头，后缀对应如下：

| 版本 | 文件名后缀 | 适用场景 |
| --- | --- | --- |
| 自包含安装包 | `win-x64-self-contained-installer.exe` | 自带 .NET 8，支持选择安装目录与快捷方式 |
| 框架依赖安装包 | `win-x64-framework-dependent-installer.exe` | 体积更小，需要 .NET 8 Desktop Runtime |
| 自包含便携版 | `win-x64-self-contained-portable.zip` | 自带 .NET 8，解压运行 |
| 框架依赖便携版 | `win-x64-framework-dependent-portable.zip` | 解压运行，需要 .NET 8 Desktop Runtime |
| 自包含单文件版 | `win-x64-self-contained-single-file.exe` | 自带 .NET 8，单个 EXE 分发 |
| 框架依赖单文件版 | `win-x64-framework-dependent-single-file.exe` | 单个 EXE 分发，需要 .NET 8 Desktop Runtime |

运行环境：Windows 10/11 x64、Microsoft Edge WebView2 Evergreen Runtime。框架依赖版本还需 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。安装器说明见 [安装文档](installer/README.md)。

## 功能

| 模块 | 能力 |
| --- | --- |
| 回放库 | 扫描、归档、放回游戏、版本升级、搜索与整理；按回放 ID 从官方 CDN 下载，支持区服选择 |
| 对局复盘 | 对局摘要、踩中刷怪地块并触发刷新的统计、玩家表现、按回合事件，以及筹码候选、刷新、选择与来源 |
| 专业分析 | protobuf 消息、原始帧、JSON 导出与 AI 摘要 |
| 角色皮肤 | 多人物方案、贴图裁切、精灵动画、模板与 GIF 导出、旧式 Unity bundle 皮肤包导入 |
| 卡牌皮肤 | 自定义卡面与多方案管理；导入视频/GIF 制作动态卡面 |
| 模组管理 | 安装、更新、卸载 CesiumLoader；导入模组包（ZIP）或单个 DLL、编辑配置与绑定热键；升级保留配置与停用状态、旧版本残留移入回收站，降级需确认 |
| 资源模组 | 导入 RML 格式 ZIP；内置 RML 引擎解析 `.bundle`、`mod.json`、`replace.txt`、图片与 Patch，改写 Addressables catalog；显示进度、文件日志，可暂停/取消并还原；支持启停、删除与还原官方资源 |
| 创意工坊 | 浏览社区模组目录：搜索、标签筛选、排序与分页；查看版本、SHA-256、权限、依赖与下载量；下载后校验哈希再安装，更新与降级都有确认，保留配置与停用状态；安装时右下角显示步骤与进度；首次使用需阅读并同意第三方内容说明 |
| 启动与变速 | 管理多个游戏版本，选择 Steam 或绕过 Steam 启动，设置基础倍率与游戏内变速热键 |

CesiumLoader 随附 16 个内置模组：行为日志、自由相机、变速热键、回合倒计时、战斗胜率助手、卡牌皮肤、角色皮肤、自动感谢、岔路标记、回合置前、隐藏 HUD、快捷短语、消息停留、主页底栏、帧率解锁与多语言支持。功能与配置详见 [加载器文档](https://github.com/higashitaniyume/CesiumLoader#内置模组)。安装与模组配置变更通常需要重启游戏。

变速范围为 **1–100 倍**，建议不超过 3 倍；倍率会影响动画、演出、回合与网络超时。默认 `Delete` 切换正常速度与上次倍率，`Alt` + `=` / `Alt` + `-` 调整倍率。

创意工坊默认连接 `http://localhost:15555`，可在「系统设置 → 创意工坊」换成正式地址。首次进入会展示第三方模组说明，同意后才会连接工坊服务；关闭说明只对本次生效，下次进入会再次询问。模组包由作者托管，玩具箱只负责下载、校验 SHA-256 并安装；随附的内置模组不能从创意工坊安装或覆盖。

### 动态卡面

导入视频/GIF 后，先裁切预览，再转换为 **404 × 400、15 fps** 的 PNG 序列帧；最长 20 秒，无音频。普通卡牌与异画分别绑定，动态卡面使用普通卡框。需要 CardSkinMod 1.2.0 或更新版本，应用方案后重启游戏生效。

转换使用 FFmpeg，可通过 `ASTRAL_FFMPEG_PATH` 或本机 PATH 指定；未找到时从 [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) 下载并缓存，素材在本机转换。

## 数据与配置

| 内容 | 默认位置 |
| --- | --- |
| 回放库与工具配置 | `%USERPROFILE%\Documents\AstralPartyReplays` |
| 游戏回放缓存（国服） | `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay` |
| WebView2 数据 | `%LOCALAPPDATA%\AstralParty.Toys\WebView2` |
| 创意工坊封面缓存 | `%LOCALAPPDATA%\AstralParty.Toys\workshop-covers` |
| 加载器、模组与日志 | 游戏目录下的 `version.dll` 与 `AstralParty_ModLoader\` |
| 资源模组与备份 | 游戏目录下的 `AstralParty_ResourceMods\` |

**归档会移动回放，放回游戏会复制并保留库内副本。** 自动整理需要在设置中开启；安装器卸载不会删除回放库或游戏目录中的模组。

文档目录不可写时，配置回退到 `%APPDATA%\AstralParty.Toys`；首次运行会复制旧配置，不覆盖已有设置。更改回放库位置不会改变工具配置目录。

## 开发

技术栈：**.NET 8 · WPF · WebView2 · HTML/CSS/JavaScript**。页面、配表与展示素材内嵌分发。

在本仓库根目录执行，需要 .NET 8 SDK 或更高版本：

```powershell
dotnet run --project src/AstralParty.Toys/AstralParty.Toys.csproj
dotnet test tests/AstralParty.Toys.Tests/AstralParty.Toys.Tests.csproj -c Release
dotnet publish src/AstralParty.Toys/AstralParty.Toys.csproj -c Release -r win-x64 --self-contained false
```

测试自带合成回放；依赖真实回放或在线下载的用例默认跳过，可用 `ASTRAL_TEST_REPLAY` 或 `ASTRAL_TEST_REPLAY_ID` 启用。Debug 构建支持从程序旁的 `WebUI/` 加载页面，Release 使用内嵌资源。

- [回放格式与解析口径](docs/replay-format.md)
- [角色皮肤方案](docs/character-skin-schemes.md)
- [资源模组安装与恢复](docs/resource-mods.md)
- [加载器恢复与排障](docs/loader-recovery.md)
- [安装器构建](installer/README.md)

## 交流与许可

企鹅：**1078464597** ／ Discord：<https://discord.gg/SZaYSwGX94>。反馈问题时可在程序「版本信息」中复制构建与环境信息。

原创代码以 [GNU AGPL-3.0](LICENSE) 发布。Copyright © 2026 Yume Higashitani。

资源模组安装器内置 [zxy19/ResourceModLoader](https://github.com/zxy19/ResourceModLoader) 的 Mod 引擎（MIT），其许可与版权声明见 [src/ResourceModEngine/LICENSE.txt](src/ResourceModEngine/LICENSE.txt)。

内嵌的 Astral Party 协议程序集、配表与游戏素材归原权利人所有，不属于本项目的授权范围。CesiumLoader 各组件遵循其自身许可，详见 [加载器许可说明](https://github.com/higashitaniyume/CesiumLoader#许可)。



