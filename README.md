# AstralParty.Toys

## 企鹅：1078464597

`AstralParty.Toys` 是面向《吉星派对》（Astral Party）的 Windows 本地工具箱，整合离线回放分析、协议帧查看、筹码复盘、游戏素材展示、**Mod 加载器（[CesiumLoader](https://github.com/higashitaniyume/CesiumLoader)）与模组管理**和变速器管理。所有数据处理均在本地完成。

## 下载与版本选择

从 [GitHub Releases](https://github.com/higashitaniyume/AstralParty.Toys/releases/latest) 下载发布版。每个版本提供 2 种架构、2 种运行时模式和 2 种打包方式，共 8 个可执行产物；GitHub 自动生成的 `Source code` 压缩包只是源码，不能直接运行。

各版本变更记录见 [CHANGELOG.md](CHANGELOG.md)。

文件名格式为：

```text
AstralParty.Toys-<版本>-<架构>-<运行时模式>-<打包方式>
```

| 文件名字段 | 可选值 | 如何选择 |
| --- | --- | --- |
| 架构 | `win-x64` / `win-x86` | 绝大多数用户选择 `win-x64`；仅 32 位 Windows 选择 `win-x86` |
| 运行时模式 | `self-contained` | 已包含 .NET 8，文件较大；不确定电脑是否安装 .NET 时选择它 |
| 运行时模式 | `framework-dependent` | 文件较小，但必须先安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 打包方式 | `portable.zip` | **推荐**；解压完整目录后运行，启动直接、便于排查问题，不能只复制其中的 EXE |
| 打包方式 | `single-file.exe` | 只需下载一个 EXE，但启动时需要把捆绑内容解包到系统临时目录 |

快速选择：

- 一般 64 位 Windows 用户：`win-x64-self-contained-portable.zip`
- 已安装 .NET 8、希望下载更小：`win-x64-framework-dependent-portable.zip`
- 必须只携带一个 EXE：选择对应的 `single-file.exe`；是否自带 .NET 仍由 `self-contained` / `framework-dependent` 决定

`single-file.exe` 表示“以单个文件分发”，并不表示运行时完全不落盘。为了把本项目的 WebUI、游戏数据和依赖全部收进一个 EXE，发布任务启用了 .NET 的完整自解压兼容模式，因此单文件版启动时会先把内容解包到 Windows 的 `%TEMP%\.net\` 缓存目录；这是正常机制，不是又安装了一份程序。若不希望产生这类解包缓存，请选择 `portable.zip`。

无论选择哪一种产物，主界面仍需要 Microsoft Edge WebView2 Evergreen Runtime（Windows 10/11 通常已经安装）。

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

### 模组（Mod）

内置 **CesiumLoader** 加载器与 3 个内置 mod，把 mod 系统装进游戏目录。CesiumLoader 是**本项目的配套加载器**（同一个作者维护，源码在独立仓库 [higashitaniyume/CesiumLoader](https://github.com/higashitaniyume/CesiumLoader)：C++ 原生加载器 + 托管 SDK）。

它是 Doorstop 思路的 `version.dll` 代理：靠 Windows 自带的 DLL 劫持在游戏启动最开始就拿到控制权，用 MinHook 挂原生 hook，等游戏的 HybridCLR 热更程序集就绪后，再用 `Assembly.Load(byte[])` 把托管 mod 拉起来。所以它能做到普通 mod 做不到的事 —— 例如在 AOT 的 `SteamManager.Awake` 之前拦下「非 Steam 客户端启动就退出游戏」。写 mod 用的 SDK（`netstandard2.0`）、示例 mod、CLI 工具与文档都在那个仓库里；Steam 绕过的原理见 [docs/steam-bypass.md](https://github.com/higashitaniyume/CesiumLoader/blob/master/docs/steam-bypass.md)。

- 一键安装 / 更新 / 卸载加载器（`version.dll` + `AstralParty_ModLoader\`）；写入前做 SHA-256 校验，
  不会盲目覆盖游戏目录里其它工具的 `version.dll`，目录里已有同名文件时需勾选「允许覆盖」
- 「内嵌版本 / 已装版本 / 最新版本」三栏对照（当前内嵌 **2.2.1**），可一键从 GitHub 更新到最新发布版
- 加载器设置可视化编辑（`doorstop_config.json`）：总开关、控制台窗口与置顶、日志转发、各项等待超时、
  **变速基础倍速**、「允许 mod 热键变速」，以及 **Steam 绕过**一整组开关（`steamBypass*`：「不装 Steam 也能启动游戏」
  「修建房 / 加入房间没反应」「修退房 / 被踢报错」等，默认全开；其中「方案B 备用安全网」实测会让游戏启动崩溃，标注了⚠并保持关闭）
- **启动方式二选一**：主界面「启动游戏」按钮下方可直接选「从 Steam 启动」或「绕过 Steam 启动」。
  选「绕过 Steam 启动」会直接拉起游戏主程序（不经过 Steam），并把加载器的 Steam 绕过**整套**写开；
  选「从 Steam 启动」则交给 `steam://`，并把那一整套写关 —— 完全回到原版行为，Steam 大厅 / 好友邀请进房恢复正常。
  为什么是"一整套"而不是一个开关：加载器的 hook 是**启动时无条件安装、各自只受自己的键控制**的，
  所以只关主开关并不足以让 Steam 大厅恢复（`steamBypassEnabled` 只管住"非 Steam 启动会自己退出"这道门，
  建房 / 进房与房间列表查询分别由 `steamBypassMatchmaking`、`steamBypassLobbyQuery` 控制）
- 写入加载器配置时**只改这几个字面量**：注释（包括注释里出现的同名字面量）、其它键、缩进原样保留
  （不走 `SaveLoaderConfig` 的白名单重写），写完先自检（仍能解析 + 读回来就是目标值）才落盘；
  在「加载器设置」里改这些开关，首页的单选框也会跟着同步；没装加载器时选择照样记住，只是同步会提示一句
- 游戏变速（加载器内置功能）：界面上直接开关并设定基础倍速（`1.0` = 正常，进游戏即生效），
  不必手动改配置文件
- 模组管理：列出 `mods\` 下每个 mod（名称 / 版本 / 能力声明），可启用或禁用（重启游戏生效）、
  编辑该 mod 的配置、删除；也能导入外部 mod 的 DLL
- 一键打开 `mods\` / `sdk\` / `logs\` 目录
- 自带 3 个内置 mod：
  - **行为日志**：把出牌 / 投骰 / 回合等行为写进 `logs\activity-mod.log`，并转发到加载器控制台
  - **自由相机**：进游戏不接管镜头；**鼠标滚轮**沿当前视线拉近 / 拉远（视角完全不变），`F1`（可改键）恢复原来的视角
  - **变速**：`Delete` 在 `1.0x` 与"刚才的倍率"之间来回切（按一次退回 `1.0x`，再按一次回到刚才的倍率），
    `Alt`+`=` / `Alt`+`-` 游戏内实时调倍率（默认每次 0.5，按住连调），屏幕上提示当前倍率

上面这些热键**都能在 UI 里改**：模组页每个 mod 的 `⚙` 按钮打开配置，字段名以 `Key` 结尾的会显示成
按键按钮 —— 点一下，再按下想绑的键即可（**键盘任意键，也包括鼠标左/右/中键与侧键**），`Esc` 取消；
改完重启游戏生效。键位写在 mod 自己的 `config.json` 里，和手改 JSON 完全一样。

倍率范围 `[1.0, 100]`：**低于 1 倍（减速）不允许**，界面上填小于 1 的值会被拒绝。倍速只改变游戏感知的时间
（动画 / 演出 / 回合 / 网络超时），**别把倍率调太高**（建议 ≤3x）。

### 游戏工具（旧版独立变速器）

内置 [speedhack-rs](https://github.com/Hirtol/speedhack-rs) x64 版，并提供图形化管理：

- 自动检测 Steam 游戏目录或手动选择目录
- 安装、更新和卸载 `version.dll` 与 `speedhack_config.json`
- 安装与卸载时使用 SHA-256 校验，避免无意覆盖或删除其它工具的 `version.dll`
- 配置进入游戏后自动启用的基础倍速，无需按快捷键
- 管理多个快捷键倍速档位、点按切换/按住生效、启动阶段加速、配置重载热键和挂接延迟
- DLL 与默认配置模板保存在 `Resources/SpeedhackTools`，并作为程序集资源嵌入 `AstralParty.Toys.dll`；发布目录不需要额外携带这两个源文件

安装、卸载前应完全退出游戏；游戏内需关闭垂直同步。

> ⚠️ 这一页装的是**旧版独立变速器**（speedhack-rs），它和「模组」页的加载器都叫 `version.dll`，
> **两者只能装一个**（同时装会互相覆盖，游戏只会加载其中一个）。页面顶部为此有互斥警告框：
> 只想要变速就留在本页；想要模组功能（也包括游戏内热键变速）请到「模组」页 —— 加载器本身就内置了变速。

### 素材与界面

- 地图、角色、怪物、筹码与常用 UI 图片以无损 WebP 形式嵌入程序集
- 应用通过本地虚拟地址直接读取资源流，不生成素材缓存
- 前端页面（HTML/CSS/JavaScript）同样整包内嵌进 EXE，发布目录里没有 `WebUI` 文件夹
- 想改前端又不想重新编译：用 **Debug** 构建（Visual Studio 默认/F5），在 exe 同目录放一份 `WebUI\`，
  改完刷新即可生效；Release 构建始终使用内嵌页面，不受同目录文件夹影响
- 主界面不可用时可以切换至备用界面，回放分析与变速器管理均可使用
- 维基页面可在应用内浏览，也可交给系统默认浏览器打开

## 环境要求

- Windows 10 或 Windows 11（发布页同时提供 `win-x64` 与 `win-x86`）
- `framework-dependent` 版本需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)；`self-contained` 版本已自带 .NET 8
- Microsoft Edge WebView2 Evergreen Runtime（Windows 10/11 通常已经安装）

从源码构建还需要 .NET 8 SDK 或更高版本。

## 运行

在工作区根目录执行：

```powershell
dotnet run --project .\replaytool\src\AstralParty.Toys\AstralParty.Toys.csproj
```

也可以打开 `replaytool\AstralParty.Toys.slnx` 后从 Visual Studio 启动（解决方案里 `src\` 是应用、`tests\` 是测试）。

## 测试

```powershell
dotnet test .\replaytool\tests\AstralParty.Toys.Tests\AstralParty.Toys.Tests.csproj
```

当前 110 个用例。测试自带合成回放数据（用游戏自己的 protobuf 生成类构造），**不依赖真实录像，也不需要联网**；
`Protocol\` 与 `GameData\` 会随项目引用自动复制到测试输出目录。需要真实录像的用例在没有数据时
报告为「已跳过」，把回放放进游戏回放目录或设置 `ASTRAL_TEST_REPLAY` 指向文件即可启用。
覆盖范围与合成夹具的说明见 `docs\replay-format.md` 第 8 节。

## 构建与发布

构建 Debug 版本：

```powershell
dotnet build .\replaytool\src\AstralParty.Toys\AstralParty.Toys.csproj
```

生成 Windows x64 发布目录：

```powershell
dotnet publish .\replaytool\src\AstralParty.Toys\AstralParty.Toys.csproj `
  -c Release -r win-x64 --self-contained false
```

默认发布入口位于：

```text
replaytool\src\AstralParty.Toys\bin\Release\net8.0-windows\win-x64\publish\AstralParty.Toys.exe
```

## 数据与配置位置

- 游戏回放：`%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay`
- 工具配置（全部集中在这一个文件夹）：`%USERPROFILE%\Documents\AstralPartyReplays`
  - 变速器主配置：`speedhack\speedhack_config.json`
  - 游戏目录记忆：`speedhack-state.json`
  - 回放库设置：`replay-library.json`（回放库目录默认就是同一个文件夹）
- WebView2 浏览器数据与缓存：`%LOCALAPPDATA%\AstralParty.Toys\WebView2`
- 游戏安装目录中的变速器：`version.dll` 与 `speedhack_config.json`
- 游戏安装目录中的加载器 / mod：`AstralParty_ModLoader\`（`doorstop_config.json`、`mods\`、`sdk\`、`logs\`）；
  变速控制文件通道在其中的 `speed\`，并镜像一份到 `%LOCALAPPDATA%\AstralParty_ModLoader\speed`

文档目录不可写时会回退到 `%APPDATA%\AstralParty.Toys`；首次运行会把旧的 `%APPDATA%` 配置迁移到文档目录，
但**不覆盖**文档目录里已有的设置。

## 回放格式

回放帧使用大端序：

```text
[cmdId:int16][payloadLength:int32][payload]
```

协议类型来自游戏的 HybridCLR 热更新程序集，主要解析逻辑位于 `src/AstralParty.Toys/Services/ReplayAnalyzer.cs` 与 `src/AstralParty.Toys/Services/GameProtocolContext.cs`。

## 素材开发

辅助项目和脚本包括：

- `replaytool-assetpack`：按游戏配置和精确文件名生成素材清单
- `scripts/pack_webp.py`：转换为无损 WebP，并进行像素级回读校验
- 外部素材只按配置字段、文件名与分类匹配，不使用图片识别；缺失素材不会影响回放解析
