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

运行环境：Windows 10/11 x64、Microsoft Edge WebView2 Evergreen Runtime。框架依赖版本还需 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。安装时所选目录必须为空（升级已有安装除外）。安装器说明见 [安装文档](installer/README.md)。

如果本机没有安装 Evergreen 运行时，或者想固定使用某个版本，可以把环境变量 `WEBVIEW2_BROWSER_EXECUTABLE_FOLDER` 指向固定版本运行时的目录（该目录里应有 `msedgewebview2.exe`，即微软固定版本分发包解压后的文件夹）。设置后工具箱优先使用它；变量指向的目录无效时回退到系统安装的运行时，并在运行日志里记一条警告。

软件更新按当前这份程序的形式来：安装版运行安装程序；单文件版替换手头的 EXE；便携版把新文件覆盖进程序文件夹（你自己放进去的文件不动）。后两种会先关闭程序、替换完成后自动重新打开，替换失败会还原成原来的版本。也可以选「改用安装版」，把程序装到「本机应用」目录并加一个桌面快捷方式，以后按安装版更新。没法原地替换时（例如程序所在目录没有写入权限），设置页会说明原因并给出下载页地址，不会另外装一份。

> **Linux / Proton（实验性）**：参见 [Linux 安装部署指南](docs/linux-proton.md)。
> 此方法通过 Proton 运行 Windows 版本，不提供原生 Linux 构建。

## 功能

| 模块 | 能力 |
| --- | --- |
| 回放库 | 扫描、归档、放回游戏、版本升级、搜索与整理；按回放 ID 从官方 CDN 下载，支持区服选择 |
| 对局复盘 | 对局摘要、踩中刷怪地块并触发刷新的统计、玩家表现（曲线与逐回合数据）、按回合事件，以及筹码候选、刷新、选择、品质与来源 |
| 专业分析 | protobuf 消息、原始帧、JSON 导出与 AI 摘要 |
| 角色皮肤 | 多人物方案、贴图裁切、精灵动画、模板与 GIF 导出、旧式 Unity bundle 皮肤包导入 |
| 卡牌皮肤 | 自定义卡面与多方案管理；导入视频/GIF 制作动态卡面 |
| 主页背景 | 用图片或视频替换游戏主页背景；内置 TenkinMori 的 HomeBackgroundMod，图片自动裁成 16:9，视频只接受 H.264 / VP8 编码，放不出来时自动改用图片背景 |
| 模组管理 | 安装、更新、卸载 CesiumLoader；支持启动门就绪即启动机制；导入模组包（ZIP）或单个 DLL、编辑多语言配置说明与绑定热键；升级保留配置与停用状态、旧版本残留移入回收站，降级需确认 |
| 模组方案 | 把当前启用状态、每个模组的配置与加载器设置保存成一套方案，随时一键切换；概览页选中即套用，方案全局共享；每次操作都能在「历史与回滚」里按原始内容还原 |
| 资源模组 | 导入 RML 格式 ZIP；内置 RML 引擎解析 `.bundle`、`mod.json`、`replace.txt`、图片与 Patch，改写 Addressables catalog；显示进度、文件日志，可暂停/取消并还原；支持启停、删除与还原官方资源 |
| 市场 | 浏览社区模组目录：搜索、标签筛选、排序与分页（默认按最近更新，记住你选过的排序方式）；点开模组进入独立详情页，按「详情 / 版本 / README / 许可证」分区查看简介、封面（完整比例展示）、下载地址、许可信息、开源仓库与各版本更新说明（README 与更新说明按 GitHub 风格排版）；下载后校验哈希再安装，更新与降级都有确认，保留配置与停用状态；安装时右下角显示步骤与进度；首次使用需阅读并同意第三方内容说明 |
| 启动与变速 | 管理多个游戏版本，选择 Steam 或绕过 Steam 启动，设置基础倍率与游戏内变速热键，支持控制台 Debug Shell 开关 |
| 游戏多开 | 管理多套账号档案（profile）：新建、改名、删除（进回收站）、打开目录，页面上方可直接选择当前使用的档案；切换后由加载器把账号 / 存档 / 回放目录重定向过去，可直接用选中的档案启动游戏 |
| 数据与日志 | 配置、缓存、日志与回放集中在同一个可自定义的数据文件夹；运行日志按天保存，可切换级别并一键打开日志文件夹 |

CesiumLoader 随附 26 个内置模组：行为日志、自由相机、变速热键、回合倒计时、战斗胜率助手、卡牌皮肤、音频替换、自动感谢、岔路标记、回合置前、隐藏 HUD、快捷短语、消息停留、主页底栏、帧率解锁、多语言支持、PVE 波次预告、星币商店一键购买、角色半透明、商店与筹码看板、选人地图预览、关键筹码置顶、PVE 面板、天使模式解锁、加载器更新提示、系统字体。功能与配置详见 [加载器文档](https://github.com/higashitaniyume/CesiumLoader#内置模组)。模组加载支持启动门机制，就绪后立刻启动；安装与模组配置变更通常需要重启游戏。

变速范围为 **1–100 倍**，建议不超过 3 倍；倍率会影响动画、演出、回合与网络超时。默认 `Delete` 切换正常速度与上次倍率，`Alt` + `=` / `Alt` + `-` 调整倍率。

「游戏多开」按档案管理账号数据：每个档案是一套独立的本地账号缓存、存档与回放，切换后由加载器在游戏进程内重定向目录，游戏文件不复制。页面顶部可以直接选择当前使用的档案。档案名只支持英文字母、数字和下划线（最多 32 个字符），输入时会实时提示是否合法或重名；每个档案可以写一条备注（保存在档案目录的 `.profile.json` 里）。启动方式上，**默认档案**可选「从 Steam 启动」或「绕过 Steam 启动」，其它档案固定用**绕过 Steam 启动**（换档案就是换账号，不再让 Steam 决定用哪个会话），启动时会同步加载器的 Steam 绕过开关。切换在下次启动游戏时生效；首次使用某个档案时会自动把下载缓存硬链接过去，不必重新下载。PlayerPrefs（分辨率、自动登录标记）与 `Player.log` 不随档案隔离；同时运行多个实例需要不同的 exe 路径，同一账号不能同时在线。

市场默认连接 `https://forge.hiynet.com`，可在「系统设置 → 市场」换成其它部署地址。首次进入会展示第三方模组说明，同意后才会连接市场服务；关闭说明只对本次生效，下次进入会再次询问。模组包可能由作者托管、也可能由市场直接保存，工具箱只负责下载、校验 SHA-256 并安装；随附的内置模组不能从市场安装或覆盖。安装时勾选注册 `aspatoys://` 协议后，网页可通过 `aspatoys://install-mod?id=<模组ID>&version=<版本>`（版本可省略）唤起工具箱打开对应模组详情，由用户确认后再安装；单文件版和便携版不注册该协议。工具箱对外打开的网页链接会带上 `?from=app.aspatoys.hiynet.localhost` 标识来源。

### 模组方案

「模组方案」把当前状态存成一套可切换的组合：哪些模组启用、每个模组的 `config.json` 内容、加载器设置（倍速、控制台、Steam / TapTap 绕过、日志与排查开关等；不含随加载器版本走的 `sdkVersion`，也不含加载器停用开关与多开档案）。方案保存在数据文件夹里、全局共享，可以套用到任意游戏目录；方案里没有装上的模组会被跳过，没记录在当前状态里的新模组不会被改动。

在模组页「保存当前为新方案」；「概览 → 模组方案」和模组页的下拉都能**选中即套用**，套用前会列出会启用 / 停用哪些模组、改写几份配置、加载器有哪些项会变，确认后才执行，重启游戏生效。方案不包含模组本体（DLL 与资源），换机或重装后需要先装好模组再套用。「游戏多开」页只切换账号档案，不提供模组方案入口——模组状态是所有账号共用的一份。

所有方案操作（保存、更新、改名、删除、套用）、多开页切换当前档案、保存加载器设置与变速，都会写一条「历史与回滚」记录。套用方案与加载器配置按操作前的原始字节还原（含注释与 BOM），会把模组目录位置、模组配置和加载器配置恢复到那一刻；保存、更新、改名、删除、备注这类只撤销这一条，不会影响之后新建的其它方案。套用方案的回滚不会改动多开档案选择，切档案的回滚也只还原加载器配置。操作之后被外部改过的文件会先提示冲突，确认后才覆盖；记录保留最近 20 条 / 30 天。

### 动态卡面

导入视频/GIF 后，先裁切预览，再转换为 **404 × 400、15 fps** 的 PNG 序列帧；最长 20 秒，无音频。普通卡牌与异画分别绑定，动态卡面使用普通卡框。需要 CardSkinMod 1.2.0 或更新版本，应用方案后重启游戏生效。

转换使用 FFmpeg，可通过 `ASTRAL_FFMPEG_PATH` 或本机 PATH 指定；未找到时从 [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) 下载并缓存，素材在本机转换。

## 数据与配置

| 内容 | 默认位置 |
| --- | --- |
| 数据文件夹（配置、缓存、日志、回放） | `%USERPROFILE%\Documents\AstralParty.Toys` |
| 游戏回放缓存（国服） | `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay` |
| 账号档案（游戏多开） | `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN_profiles\<档案名>` |
| WebView2 数据 | `%LOCALAPPDATA%\AstralParty.Toys\WebView2` |
| 加载器、模组与日志 | 游戏目录下的 `version.dll` 与 `AstralParty_ModLoader\` |
| 模组方案与回滚记录 | 数据文件夹下的 `state\mod-profiles.json` 与 `state\rollback\` |
| 资源模组与备份 | 游戏目录下的 `AstralParty_ResourceMods\` |

数据文件夹里包含统一配置 `AstralParty.Toys.Config.json`、回放归档 `replays\`、可随时删除的缓存 `cache\`、运行日志 `logs\`（按天分文件，默认保留 7 天，单文件上限 10 MB）以及安装历史 `install-history\`。设置页可以把整个数据文件夹换到任意空目录，现有内容会先复制校验、再删除源目录，搬完立刻生效。

**归档会移动回放，放回游戏会复制并保留库内副本。** 自动整理需要在设置中开启；安装器卸载不会删除回放库或游戏目录中的模组。

数据文件夹不可写时会回退到 `%LOCALAPPDATA%\AstralParty.Toys\data`；首次运行会把旧的 `文档\AstralPartyReplays` 与 `%APPDATA%\AstralParty.Toys` 配置迁移进来，不覆盖已有设置。

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



