# 更新日志 / Changelog

本文件按版本记录 `AstralParty.Toys` 的功能变更。版本号与 GitHub 发布 tag 一一对应（如 `v0.2.0`），发布产物由 [release.yml](.github/workflows/release.yml) 在打 tag 时自动构建。

## [v0.4.0] - 2026-09-20

### ✨ 内置 CesiumLoader 升级到 2.1.0（加载器 + SDK + 内置 mod）

内嵌加载器组件整体升级到 CesiumLoader 2.1.0，工具开箱即用的 mod 能力一次性补齐：

- **SDK 2.1.0**：新增相机接管（`CameraService` / 相机状态快照与还原 / Cinemachine 接入）、输入与光标（`InputService`）、
  UI 登记（`UiService`）、场景与协程（`SceneService` / `CoroutineService`）、运行时反射（`RuntimeAssemblyService`）、
  诊断转储（`SdkDiagnostics.Dump`）；
- **引擎调用隔离**：SDK 全部 Unity 引擎调用收敛到 `UnityCall` 两层隔离层——引擎 API 在特定时机不可用时
  **降级返回默认值**，而不是把异常抛进游戏主循环打断 mod / 游戏；
- **内置 mod 变为两个**：行为日志（ActivityLogMod）+ **自由相机（FreeCameraMod）**。
  自由相机提供固定俯瞰视角，默认只绑定 **F1** 开关 / **F2** 复位，进游戏不会自动夺走镜头；
- **版本号**：加载器 / SDK `2.0.0 → 2.1.0`，AstralParty.Toys `0.3.0 → 0.4.0`。

### 🔧 内置 mod 不再写死为单个

- `ModManager` 的内置 mod 由「单个示例 mod」泛化为**内置 mod 列表**（`BuiltInModIds`），
  安装时逐个写入 `mods\{ModId}\{ModId}.dll` + sidecar，以后新增/移除内置 mod 不需要改安装逻辑；
- 从发布包安装（`InstallPackage`）改为**包内有什么 mod 就装什么**，并按清单逐个校验 SHA256；
- 安装/更新时同名内置 mod 文件会被覆盖为随包版本，升级工具即一并升级内置 mod；
- 模组页勾选项文案更新为「同时安装内置 mod（行为日志 + 自由相机，含 sidecar 元数据）」。

### 🔒 安全

- `InstallPackage` 解包前校验条目相对路径（不含 `..`、非绝对路径），
  避免构造过的发布包把文件写到 `mods\` 之外。

### 🐛 修复

- **CI 打 tag 会取不到内置 mod**：`release.yml` 仍按旧扁平路径 `mods/ActivityLogMod.dll` 取值，
  而发布包已是「每 mod 一个文件夹」，导致 Release 构建直接失败。现在按 `mods/{ModId}/` 目录同步，包内 mod 全部带上。
- 内置 `doorstop_config.json` 控制台默认改为**不置顶**（`consoleTopmost: false`），避免控制台窗口一直压住游戏。

### ♻️ 顺带补齐 v0.3.0 更新日志（当时发布漏写）

- 变速改为**加载器内置能力**（`doorstop_config.json` 的 `speedhackBaseSpeed`），模组页开关直接控制；
  加载器与独立变速器共用 `version.dll`，安装时互相检测并提示；
- mod 能力声明化：取消权限门控，声明「操作游戏」的 mod 仅显示来源警告；
- 修 mod 页进入即自动弹窗、配置被安装/更新重置、加载器设置保存失效等问题；
- 内嵌加载器从 `winmm.dll`（旧代理）改为 `version.dll`（Doorstop 式布局）；
- mod 目录改为「每 mod 一个文件夹」（`mods\{ModId}\{ModId}.dll`），旧扁平布局仍兼容读取。

## [v0.2.0] - 2026-09-11

### ✨ 新增：商店记录时间线（与筹码时间线并列）

回放页面新增「商店记录」标签页，与「筹码记录」并列，展示整局所有玩家的卡牌商店进店与购买情况：

- **候选卡牌**：服务器广播的商店在售卡牌清单（PVE 商店 / PVP 商店），与进店时游戏画面完全一致；
- **购买结果**：结合服务器购买回执（`BuyCards[]` 槽位下标）把「买了几号位、买了哪张卡」翻译成可读文本；
- **已售出**：逐槽位标注该格卡牌已被买走（对应服务器 `Alreadys[]`）；
- **售价与折扣**：单价与折后价（PVE 折扣场景，如「3（折后 2）」）；
- **去重口径**：同一候选组被服务器重复广播时按 `Action.Sn` 与候选内容合并为一次进店，不重复计数；
- **筛选与导出**：支持按玩家 / 商店类型 / 卡牌名筛选，一键导出 CSV（含 BOM，Excel 直接打开不乱码）。

同时补全了商店所需的两张配表（`Card.bin` / `STRCard.bin`），原生 WPF 回退界面也同步增加了「商店记录」页签。

### 🐛 修复：商店「代付」字段误读

此前将 `PVEShopBuyS2C.AssistPlayer` 当作「队友代付买牌」的玩家 ID 展示，经查证为误读：

- `AssistGold` 是 ATM 转账金额（实测恒为 5 星币），`AssistPlayer` 是转账关联标识（回执中为操作关联的大数字，非玩家 ID）；
- PVE 商店唯一的转账入口是 ATM（一次转账 5 星币），商店购买请求的 `assistPlayer` 参数恒为 0；
- 已删除「代付」列，售罄信息以「已售出」逐槽位正确呈现，相关协议文档同步更正。

### 🔧 其他

- 回放解析新增商店协议登记：Action `5029 / 5215 / 5323`，回执 `5030 / 5216 / 5324`；
- 真实回放测试扩为 52 项，覆盖商店进店 / 购买 / 去重 / 售罄合成用例，并通过 10 局真实回放数据验证。

---

## [v0.1.0] - 2026-09-11

### ✨ 新增

- 应用图标（Windows 资源管理器 / 任务栏显示）。
