// i18n.source.js - source-text catalog for AstralParty.Toys.
// Keys are the Simplified Chinese strings used in the HTML/JS; values are the
// translated UI text. Strings not present here fall back to the Chinese source.
(function () {
  window.I18N_SOURCE = {
    "en": {
      "—— 点「检查更新」看看 GitHub 上有没有新版。": "— Click Check for Updates to see if a new version is on GitHub.",
      "—— 同时安装会互相覆盖，游戏只会加载其中一个。": "— Installing both overwrites one another; the game only loads one of them.",
      "——安装加载器、变速、导入模组都作用于当前这个。": "— Installing the loader, Speedhack, and importing mods all apply to this one.",
      "——这是客户端写死的上限，和服务器无关：超了只是游戏拒绝保存新回放。把回放归档到下面的库目录即可腾出席位，需要时再一键放回游戏。": "— This limit is hardcoded in the client and unrelated to the server: once exceeded, the game simply refuses to save new replays. Archive replays to the library folder below to free up slots, then restore them to the game with one click when needed.",
      "，按": ", press",
      "，比随程序内置的 v": ", newer than the bundled v",
      "，点「更新到 v": ", click Update to v",
      "，或在游戏内按重载热键（默认 Ctrl+Shift+R）。": ", or press the reload hotkey in game (Ctrl+Shift+R by default).",
      "，两个只能装一个": ", only one of the two can be installed",
      "，请更新到": ", please update to",
      "，然后重启游戏以使用最新修复。": ", then restart the game to apply the latest fixes.",
      "，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。": ", matching the bundled version — click Check for Updates to confirm whether GitHub has a newer version.",
      "，sidecar 与配置（": ", sidecar and config (",
      "；加载器本身已内置变速，装它就够了。": "; the loader already includes the Speedhack, so installing it is enough.",
      "：不用打开游戏，也不占游戏内的 10 个席位。": ": no need to launch the game, and it doesn't use any of the game's 10 slots.",
      "：启用 = 启动游戏即按该倍速运行（全程保持）， 禁用 = 恢复 1.0 正常速度。最低 1.0（加载器不支持减速），重启游戏生效。": ": Enabled = the game runs at this speed from launch (held for the whole session); Disabled = back to normal 1.0 speed. Minimum 1.0 (the loader does not support slowing down); takes effect after a game restart.",
      "：已装": ": installed",
      "：游戏库（多游戏目录 / 当前游戏）、变速器与加载器状态、回放库设置都存在这里。把库目录改到别处只会移动回放归档，上述配置文件仍留在默认目录。": ": the game library (multiple game folders / current game), Speedhack and loader state, and replay library settings all live here. Pointing the library folder elsewhere only moves replay archives; those config files stay in the default folder.",
      "…（卸载时按此校验，防止误删他人文件）": "… (checked during uninstall so other people's files aren't deleted by mistake)",
      "。 确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。": ". To restore anyway, click the button below; the library copy is kept regardless.",
      "。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。": ". Click Restore version.dll; if the local file is missing, the matching release package is downloaded.",
      "· 按住生效": "· Hold to apply",
      "· 本地构建": "· Local build",
      "· 编辑此人物的自定义素材": "· Edit this character's custom assets",
      "· 等级": "· Level",
      "· 第": "· Round ",
      "· 点按切换": "· Click to toggle",
      "· 累计转账": "· Total transferred",
      "· 模组": "· Mods",
      "· 提交": "· Commit",
      "· 游戏运行中": "· Game running",
      "· 最终首领": "· Final boss",
      "‹ 返回大厅": "‹ Back to Lobby",
      "\" alt=\"第": "\" alt=\"No. ",
      "\" aria-pressed=\"false\" aria-label=\"选择第": "\" aria-pressed=\"false\" aria-label=\"Select No. ",
      "（本地构建）": "(Local build)",
      "（本页的旧版变速器自带独立的配置与热键，功能到此为止。）": "(The legacy standalone speedhack on this page has its own config and hotkeys; its features stop there.)",
      "（创建后不可修改）": "(can't be changed after creation)",
      "（构建于 2024-04-13）。": "(built 2024-04-13).",
      "（回退 AppData）": "(AppData fallback)",
      "（文档目录）": "(Documents folder)",
      "（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；": "(same folder as the mod's DLL, read and written by the mod's SdkConfig; takes effect after a game restart). Checkbox = toggle, number field = value, text field = text;",
      "（CesiumLoader，Doorstop 式代理）放到游戏 exe 旁边；游戏启动时由 UnityPlayer 自动加载，再读取": "(CesiumLoader, a Doorstop-style proxy) next to the game exe; UnityPlayer loads it automatically at startup, and it then reads",
      "(SFW 审查版本)": "(SFW-reviewed version)",
      ") 恢复为应用内置的原始卡面吗？": ") back to the app's built-in original card art?",
      "）， 模组加载器用的也是同一个文件名": "), and the mod loader uses the same file name",
      "）都在同一个文件夹里。": ") are all in the same folder.",
      "）为准：安装或更新时写入，界面读它显示「已安装」。看不出（0.4.7 之前的老安装、或清单被删）时界面会如实显示「版本未知」——重新安装一次即可纳入版本管理。": ") as the source of truth: written on install or update, and read by the UI to show Installed. When it can't be determined (old installs before 0.4.7, or a deleted manifest), the UI honestly shows Version Unknown — reinstall once to bring it under version tracking.",
      "）做依赖排序与 SDK 版本协商，然后逐个加载，并在控制台窗口按颜色分级打印加载报告。不会修改游戏文件本身。": ") to sort dependencies and negotiate SDK versions, then loads them one by one and prints a color-coded load report in the console window. It doesn't modify the game files themselves.",
      "[当前生效]": "[Active]",
      "」改这个模组自己的配置。": "\" to edit this mod's own settings.",
      "→ 包内": "→ In package",
      "→ 请到「": "→ Go to \"",
      "→ 最新 v": "→ Latest v",
      "＋ 按键": "+ Key",
      "＋ 打开回放": "+ Open Replay",
      "＋ 导入模组": "+ Import Mod",
      "＋ 添加倍速档位": "+ Add Speed Preset",
      "＋ 新建方案": "+ New Profile",
      "＋ 选择回放文件": "+ Choose Replay File",
      "= 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。": "= click, then press the key to bind (any keyboard key or left/right/middle mouse button, side buttons; Esc to cancel).",
      "= 全程 2 倍速；进游戏后还能用": "= 2× speed throughout; in game you can still use",
      "= 正常，": "= normal,",
      "⚠ 方案B 备用安全网 —— 实测开启会导致游戏启动崩溃（0xC0000005），请保持关闭": "⚠ Plan B fallback safety net — enabling it crashed the game at startup (0xC0000005) in testing; keep it off",
      "0 张卡面": "0 card faces",
      "1.0 = 正常速度": "1.0 = normal speed",
      "1.0 = 正常速度；最低 1.0，不支持减速": "1.0 = normal speed; minimum 1.0, slowing down not supported",
      "10 个席位": "10 slots",
      "安装": "Install",
      "安装：": "Install: ",
      "安装」即可。": "Install\" to finish.",
      "安装加载器": "Install Loader",
      "安装加载器时一并装上内置模组（行为日志 + 自由相机 + 变速）": "Install the built-in mods together with the loader (activity log + free camera + Speedhack)",
      "安装角色皮肤模组": "Install Character Skin Mod",
      "安装清单": "Installation Manifest",
      "安装与覆盖选项": "Install & Overwrite Options",
      "安装中…": "Installing…",
      "按 ID 下载": "Download by ID",
      "按编号导入动态卡面": "Import dynamic card art by number",
      "按编号导入动态卡面…": "Import dynamic card art by number…",
      "按对局时间": "By match time",
      "按回放 ID": "By replay ID",
      "按回放 ID 下载到回放库": "Download to Replay Library by Replay ID",
      "按键按钮": "Key Button",
      "按来源筛选": "Filter by Source",
      "按玩家、回合、技能或动作搜索...": "Search by player, round, skill, or action...",
      "按文件大小": "By file size",
      "按下按键…（Esc 取消）": "Press a key… (Esc to cancel)",
      "按资源比例裁剪，或保留完整图片": "Crop to asset ratio, or keep the full image",
      "把模组日志转发到控制台": "Forward mod logs to the console",
      "把内置": "Put the built-in",
      "把喜欢的人物放进同一个方案": "Put your favorite characters in one profile",
      "版本": "Version",
      "版本读取中…": "Reading version…",
      "版本未知": "Version unknown",
      "版本详情": "Version Details",
      "版本信息": "Version Info",
      "版本信息只以": "Version info comes only from",
      "保持选中帧的画布尺寸；透明边缘和脚底位置会影响对齐。": "Keep the canvas size of the selected frame; transparent edges and foot position affect alignment.",
      "保存布局与动作参数": "Save Layout and Motion Settings",
      "保存裁剪": "Save Crop",
      "保存尺寸": "Save Size",
      "保存动作参数": "Save Motion Settings",
      "保存多个游戏位置（国服 / 国际服 / TapTap 端…）。在「模组」页顶部可切换": "Save multiple game locations (CN / Global / TapTap…). Switch at the top of the Mods page",
      "保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。": "Takes effect after saving and restarting the game; day to day you only need Enable Loader or Base Speed at Launch.",
      "保存配置": "Save Config",
      "保存设置": "Save Settings",
      "保存失败": "Save Failed",
      "保存失败，请重试。": "Save failed, please try again.",
      "保存为透明 PNG，尺寸": "Save as transparent PNG, size",
      "保存文案": "Save Text",
      "保存信息": "Save Info",
      "保留的组件无法确定加载器版本，请重新安装或更新加载器。": "The kept components don't reveal the loader version. Please reinstall or update the loader.",
      "备份并删除中…": "Backing up and deleting…",
      "备份后只删除 version.dll，保留所有模组与配置": "After backing up, delete only version.dll and keep all mods and configs",
      "背景物品图标": "Background Item Icon",
      "倍速": "Speed",
      "倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度；加载器不支持减速）": "Speed must be between 1.0 and 100 (1.0 = normal speed; the loader does not support slowing down)",
      "倍速档位（满足按键组合时切换速度）": "Speed presets (switch speed while the key combo is held)",
      "倍速基于修改进程时间函数，需在游戏设置里": "Speedhack works by modifying process time functions; in the game settings you need to",
      "被占用时写入或删除会失败——那种情况先完全退出游戏再操作。": "Writing or deleting fails while the file is in use — fully exit the game first in that case.",
      "本程序缺少内置加载器文件，无法安装 —— 请使用完整发布包（推荐 portable.zip）。": "This build is missing the bundled loader files and can't install — please use the full release package (portable.zip recommended).",
      "本程序缺少内置加载器资源，安装按钮不会生效 —— 请重新下载完整发布包（推荐 portable.zip，single-file 版偶发解包不完整）。": "This build is missing the bundled loader assets, so the Install button won't work — please download the full release package again (portable.zip recommended; the single-file build occasionally unpacks incompletely).",
      "本地构建": "Local Build",
      "本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，": "This tool can't read their settlement frame (its bundled protocol version may be older than the game's). If the game can't read them either,",
      "本回合转账": "Transferred This Round",
      "本局地图": "This Match's Map",
      "本局没有商店进店记录": "No shop visits recorded this match",
      "本页安装的是": "This page installs",
      "比 GitHub 上的 v": "than GitHub's v",
      "比内置的 v": "than the bundled v",
      "编辑方案信息": "Edit Profile Info",
      "编辑配置 JSON": "Edit Config JSON",
      "编辑人物皮肤配置": "Edit Character Skin Config",
      "变速器已就位": "Speedhack Ready",
      "变速器已就位；游戏正在运行，新写入的文件要重启游戏才加载": "Speedhack ready; the game is running, so newly written files load only after a restart",
      "变速已禁用": "Speedhack disabled",
      "变速已启用": "Speedhack enabled",
      "表现分析": "Performance Analysis",
      "播放预览": "Play Preview",
      "不裁剪，等比缩放并透明补边": "No cropping; scale proportionally and pad with transparency",
      "不兼容": "Incompatible",
      "不是本工具装的文件 —— 见下方「高级与排障」": "Not installed by this tool — see Advanced & Troubleshooting below",
      "不添加自定义覆盖": "Don't add custom overrides",
      "不装 Steam 也能启动游戏（首页「启动方式」把这一套三个开关一起开 / 关）": "Launch the game without Steam installed (Launch Method on the Home page toggles all three switches together)",
      "不装任何 mod、只用加速 → 就留在本页，按下面卡片里的「": "No mods, just speedup → stay on this page and use \"",
      "才生效，而且 DLL 被占用时写入/删除会失败——那种情况先完全退出游戏再操作即可。": "takes effect after the game restarts; writing/deleting also fails while the DLL is in use — fully exit the game first in that case.",
      "才生效；修改配置后": "takes effect; after changing the config,",
      "裁剪角色素材": "Crop Character Asset",
      "裁剪卡面": "Crop Card Art",
      "裁剪为目标比例": "Crop to Target Ratio",
      "裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置": "Crop selection: drag to move, drag a corner to resize, arrow keys to nudge",
      "裁剪预览": "Crop Preview",
      "参战玩家 (": "Players (",
      "操作": "Actions",
      "操作失败。": "Operation failed.",
      "操作完成。": "Done.",
      "操作系统": "Operating System",
      "查看": "View",
      "查看完整更新日志 ›": "View Full Changelog ›",
      "查看详细版本信息": "View Detailed Version Info",
      "常见位置": "Common Locations",
      "常用": "Common",
      "称号": "Title",
      "承受伤害": "Damage Taken",
      "程序集版本": "Assembly Version",
      "程序集文件时间": "Assembly File Time",
      "程序目录": "App Folder",
      "程序缺少变速器文件": "App is missing Speedhack files",
      "程序缺少内置加载器文件": "App is missing the bundled loader files",
      "尺寸来自游戏原资源": "Size from the game's original asset",
      "筹码": "Relics",
      "筹码记录": "Relic Records",
      "筹码记录共": "Relic records: ",
      "筹码名称": "Relic Name",
      "筹码选择品质分布": "Relic Pick Quality Distribution",
      "出牌": "Card Play",
      "出牌次数": "Cards Played",
      "处理方式": "Handling",
      "处理失败": "Processing Failed",
      "传说": "Legendary",
      "创建": "Create",
      "创建成功后自动添加到当前方案，并进入此人物的图片、文案和动画编辑器。": "Once created, it's added to the current profile automatically and opens this character's image, text, and animation editor.",
      "创建方案": "Create Profile",
      "创建后会自动加入当前方案，接着编辑图片、动画和文案。": "It's added to the current profile automatically after creation; then edit images, animations, and text.",
      "创建失败": "Create Failed",
      "此动作没有自制帧。导入帧序列后可逐帧查看和替换。": "This action has no custom frames. Import a frame sequence to view and replace frames one by one.",
      "此动作使用原精灵映射：替换单帧会同步游戏映射；同一原帧被重复引用时会一并更新。重新导入整组帧会改为独立帧序列。": "This action uses the original sprite mapping: replacing a single frame syncs the game mapping; repeated references to the same original frame are updated together. Re-importing the whole set switches to an independent frame sequence.",
      "此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信": "This mod declares that it can drive the game (simulating card plays / dice rolls / movement). Permissions are no longer enforced; this is only a reminder: make sure the source is trustworthy",
      "此人物的相同原皮肤已有规则，请编辑原规则。": "A rule for the same original skin of this character already exists; please edit that rule.",
      "此人物的相同原皮肤已有配置，请编辑已有的人物皮肤。": "The same original skin of this character is already configured; please edit the existing character skin.",
      "此人物还没有皮肤包。可导入 ZIP，或选择「新建自定义皮肤」。": "This character has no skin pack yet. Import a ZIP or choose New Custom Skin.",
      "次": " times",
      "次 · 平均每回合": " times · avg per round",
      "次（其中": " times (of which ",
      "次商店进店": " shop visits",
      "次有购买）": " with purchases)",
      "次掷骰 ·": " dice rolls · ",
      "从 GitHub 更新": "Update from GitHub",
      "从 Steam 启动": "Launch via Steam",
      "从 ZIP 导入此人物皮肤包": "Import This Character's Skin Pack from ZIP",
      "从方案中创建会自动加入该方案。你可以导出原版素材作为起点，也可以取消勾选，从空白皮肤开始。": "Creating from a profile adds it to that profile automatically. You can export the original assets as a starting point, or clear the checkbox to start from a blank skin.",
      "从列表移除（不会删除游戏文件）": "Remove from list (doesn't delete game files)",
      "从内置模板新建…": "New from Built-in Template…",
      "从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。": "Create a brand-new profile from the app's built-in default pack without using images already modified on disk. No network needed; you can replace individual card faces afterwards.",
      "从游戏导出官方皮肤素材": "Export Official Skin Assets from Game",
      "从游戏缓存中选择一局，或手动打开回放文件": "Pick a match from the game cache, or open a replay file manually",
      "错误：": "Error: ",
      "打开": "Open",
      "打开 mods 目录": "Open Mods Folder",
      "打开 sdk 目录": "Open SDK Folder",
      "打开本方案目录": "Open This Profile's Folder",
      "打开变速器自带的日志控制台窗口": "Open the speedhack's built-in log console window",
      "打开方案目录": "Open Profile Folder",
      "打开库目录": "Open Library Folder",
      "打开目录": "Open Folder",
      "打开目录修复": "Open Folder to Fix",
      "打开皮肤目录": "Open Skins Folder",
      "打开日志目录": "Open Logs Folder",
      "大小:": "Size:",
      "单次播放": "Play Once",
      "当前": "Current",
      "当前报告没有逐回合数据，请重新打开原始回放。": "This report has no per-round data; please open the original replay again.",
      "当前方案已切换，请重新打开规则编辑。": "The current profile has changed; please reopen rule editing.",
      "当前方案已切换，请重新添加人物。": "The current profile has changed; please add the character again.",
      "当前生效中": "Currently active",
      "当前星币": "Current Star Coins",
      "当前选择 · 重启游戏生效": "Current selection · takes effect after a game restart",
      "当前游戏": "Current game",
      "当前正在操作的游戏": "The game you're working with",
      "档位": "Preset",
      "导出 AI 文本 (TXT)": "Export AI Text (TXT)",
      "导出 JSON": "Export JSON",
      "导出 ZIP": "Export ZIP",
      "导出表格 (CSV)": "Export Table (CSV)",
      "导出当前动作 GIF": "Export Current Action GIF",
      "导出哪套官方皮肤": "Which Official Skin to Export",
      "导出所选皮肤并创建": "Export Selected Skin and Create",
      "导出所选皮肤的图片与二维动画作为编辑起点。方案中的替换范围保持原设置；独立创建的包默认对应所选皮肤。资源未下载时，请先在游戏中查看该皮肤。": "Export the selected skin's images and 2D animations as an editing starting point. Replacement scope in a profile keeps its original setting; a standalone pack maps to the selected skin by default. If assets aren't downloaded yet, view that skin in the game first.",
      "导入 ZIP": "Import ZIP",
      "导入本地 cesium-loader 发布 ZIP，安装加载器、SDK 与包内模组": "Import a local cesium-loader release ZIP to install the loader, SDK, and bundled mods",
      "导入单个模组 DLL，或整个模组包（ZIP，解压到 mods 目录）": "Import a single mod DLL or a whole mod pack (ZIP, extracted to the mods folder)",
      "导入发布包": "Import Release Package",
      "导入方案 ZIP": "Import Profile ZIP",
      "导入回放": "Import Replay",
      "导入结束。": "Import finished.",
      "导入旧式皮肤包": "Import Legacy Skin Pack",
      "导入皮肤包 (.zip)": "Import Skin Pack (.zip)",
      "导入人物包": "Import Character Pack",
      "导入视频 / GIF": "Import Video / GIF",
      "导入与管理": "Import & Manage",
      "导入帧序列": "Import Frame Sequence",
      "导入中…": "Importing…",
      "倒计时 / 地图像素头像": "Countdown / Map Pixel Avatar",
      "地块标志": "Tile Marker",
      "地图人物布局": "Map Character Layout",
      "地图事件": "Map Event",
      "地图事件 (": "Map Event (",
      "的": "'s",
      "的皮肤": "'s skin",
      "的自制帧和原精灵映射，重启游戏后恢复原动作。": "'s custom frames and original sprite mappings; restart the game to restore the original action.",
      "等待 GameAssembly.dll 秒数": "Seconds to wait for GameAssembly.dll",
      "等待 HybridCLR 热更秒数": "Seconds to wait for the HybridCLR hot update",
      "等待托管域初始化秒数": "Seconds to wait for managed domain init",
      "等待游戏配置": "Waiting for game config",
      "等待中": "Waiting",
      "第": "No. ",
      "点 · 受控": " pips · controlled ",
      "点「安装加载器」把加载器写进游戏目录": "Click Install Loader to write the loader into the game folder",
      "点「安装加载器」装的就是这一份": "Clicking Install Loader installs exactly this copy",
      "点「查看完整更新日志」了解本次更新": "Click View Full Changelog to see what changed",
      "点「检查更新」获取": "Click Check for Updates to get",
      "点「检查更新」向 GitHub 查询": "Click Check for Updates to query GitHub",
      "点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。": "Click to select or deselect; the choice applies to this preset once confirmed. A preset can use several keys (e.g. Ctrl + Shift).",
      "调试控制台": "Debug Console",
      "调试控制台 console": "Debug Console console",
      "调整界面偏好并查看应用信息": "Adjust interface preferences and view app info",
      "动画": "Animation",
      "动画偏好": "Animation Preferences",
      "动画帧预览": "Animation Frame Preview",
      "动画逐帧图片": "Animation Frame Images",
      "动态": "Animated",
      "动态卡面 ·": "Animated Card Art · ",
      "动作": "Action",
      "读不到更新日志内容。": "Can't read the changelog.",
      "读不到加载器更新日志（内置为空，联网获取也没成功）。点「": "Can't read the loader changelog (nothing bundled, and fetching online failed). Click \"",
      "对局表现分析": "Match Performance Analysis",
      "对局回放分析工具": "Replay Analysis Tool",
      "对局结果": "Match Result",
      "对局摘要": "Match Summary",
      "对战回放": "Match Replay",
      "多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。": "It's most likely from another tool (such as the legacy standalone speedhack on the Game Tools page) or a third-party file.",
      "多人物搭配 ·": "Multi-character Lineup · ",
      "二维精灵动画": "2D Sprite Animation",
      "发布包完整路径": "Full Release Package Path",
      "发布包正在导入，请稍候。": "The release package is being imported, please wait.",
      "发现新版本：已装 v": "New version available: installed v",
      "繁體中文": "繁體中文",
      "反馈问题时把这段信息一起贴上，能省很多来回。": "Paste this info along with your bug report to save a lot of back-and-forth.",
      "返回当前方案": "Back to Current Profile",
      "返回吉星派对大厅": "Back to Astral Party Lobby",
      "方案：": "Profile: ",
      "方案标识：": "Profile ID: ",
      "方案会移入 .deleted 文件夹，引用的自定义皮肤包会保留。": "The profile moves to the .deleted folder; referenced custom skin packs are kept.",
      "方案简介": "Profile Summary",
      "方案描述（可选）：": "Profile description (optional):",
      "方案名称": "Profile Name",
      "方案内图片": "Images in Profile",
      "方案设置": "Profile Settings",
      "方案显示名称：": "Profile display name:",
      "方案英文目录名（唯一标识）：": "English folder name (unique ID):",
      "方案中的人物": "Characters in Profile",
      "方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。": "Use the arrow keys to nudge; hold Shift to move 10 pixels at a time. The original image is not modified.",
      "放到游戏 exe 旁边，游戏启动时自动加载。游戏运行中也允许安装/卸载，只是本次写入要": "Place it next to the game exe; it loads automatically when the game starts. Installing/uninstalling while the game is running is allowed, but this write only",
      "放回游戏": "Restore to Game",
      "份）": " copies)",
      "附加保险：忽略 Steam 的“请从 Steam 启动”重启请求": "Extra safeguard: ignore Steam's “please launch from Steam” restart request",
      "复制版本信息": "Copy Version Info",
      "覆盖已有 ZIP？": "Overwrite Existing ZIP?",
      "该角色全部原皮肤": "All original skins of this character",
      "该人物全部原皮肤": "All original skins of this character",
      "该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。": "This player's transfer increase within the round's snapshot window; multiple shop visits in the same round show the same value and aren't counted twice.",
      "高级": "Advanced",
      "高级：编辑完整清单": "Advanced: Edit Full Manifest",
      "高级：用系统默认编辑器直接改 JSON": "Advanced: edit the JSON directly in your default editor",
      "高级编辑…": "Advanced Edit…",
      "高级设置：替换范围与混合搭配": "Advanced: replacement scope and mixing",
      "高级选项（Steam 绕过细节 · 引导 · 超时）": "Advanced options (Steam bypass details · bootstrap · timeouts)",
      "高级与排障": "Advanced & Troubleshooting",
      "个）": " selected)",
      "个候选：": " candidates: ",
      "个进行中": " in progress",
      "个人物": " characters",
      "个人物 ·": " characters · ",
      "个位置": " locations",
      "更换图片": "Replace Image",
      "更新，无需更新。": "checked for updates; no update needed.",
      "更新: —": "Update: —",
      "更新（从 GitHub 更新过）": "Updated (from GitHub)",
      "更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。": "Updated (this copy came from a GitHub update) — click Check for Updates to see if there's anything newer.",
      "更新到 v": "Update to v",
      "更新到最新": "Update to Latest",
      "更新角色皮肤模组": "Update Character Skin Mod",
      "更新日志": "Changelog",
      "更新时允许覆盖游戏目录里不属于本工具的 version.dll": "Allow overwriting a version.dll in the game folder that this tool didn't install when updating",
      "更新时允许降级（仅在发布包比已装版本旧时才需要）": "Allow downgrades when updating (only needed if the release package is older than the installed version)",
      "更新中…": "Updating…",
      "工具来源：": "Tool source: ",
      "工具箱已更新": "Toolbox Updated",
      "功能键": "Keys",
      "攻击 / 防御": "Attack / Defense",
      "共": "Total ",
      "共 0 张卡面": "0 card faces total",
      "共筛选出": "Filtered: ",
      "构建配置": "Build Configuration",
      "购买结果": "Purchase Result",
      "挂接延迟 wait_with_hook": "Hook delay wait_with_hook",
      "挂接延迟（毫秒）": "Hook delay (ms)",
      "关闭": "Close",
      "关闭垂直同步": "Disable V-Sync",
      "关闭消息": "Close Message",
      "关闭自动倍速时固定为 1.0；快捷键档位仍可临时切换速度": "With auto speed off, speed is fixed at 1.0; hotkey presets can still change it temporarily",
      "关卡进度": "Stage Progress",
      "关于": "About",
      "官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。": "Replaces images and character animations from official sources. Voice, video, effects, and special cutscenes stay original.",
      "官方皮肤": "Official Skin",
      "官方皮肤基础": "Official Skin Base",
      "管理游戏目录…": "Manage Game Folders…",
      "归档": "Archive",
      "归档最旧的": "Archive Oldest",
      "归档最旧的 3 局": "Archive Oldest 3 Matches",
      "国服": "CN",
      "国服（测试服）": "CN (Test)",
      "国服（正式服）": "CN (Live)",
      "国际服": "Global",
      "国际服（测试服）": "Global (Test)",
      "国际服（正式服）": "Global (Live)",
      "还没有倍速档位——点下面「＋ 添加倍速档位」创建一个，例如 Ctrl = 2 倍速。": "No speed presets yet — click + Add Speed Preset below to create one, e.g. Ctrl = 2× speed.",
      "还没有回放。在游戏里打完一局后，到「战绩」里对那条记录点「保存回放」，再回来刷新即可。": "No replays yet. Finish a match in game, click Save Replay on that record under Match History, then come back and refresh.",
      "还没有模组 —— 点「导入模组」添加 DLL 或 ZIP 模组包，或重新安装加载器时勾选内置模组": "No mods yet — click Import Mod to add a DLL or ZIP mod pack, or tick the built-in mods when reinstalling the loader",
      "还没有配置文件（mods\\": "No config file yet (mods\\",
      "还没有添加人物": "No characters added yet",
      "还没有添加游戏目录。": "No game folders added yet.",
      "还没找到游戏目录 —— 展开「高级与排障」点「自动检测」，或手动选择游戏 exe 所在的文件夹。": "Game folder not found yet — expand Advanced & Troubleshooting and click Auto-Detect, or select the folder containing the game exe manually.",
      "合计": "Total",
      "后台皮肤任务": "Background Skin Tasks",
      "候选卡牌": "Candidate Cards",
      "候选选项": "Candidate Options",
      "恢复 version.dll": "Restore version.dll",
      "恢复此动作": "Restore This Action",
      "恢复此动作？": "Restore this action?",
      "恢复当前版本；本地缺失时下载对应版本发布包": "Restore the current version; if missing locally, download the release package for that version",
      "恢复默认模板": "Restore Default Template",
      "恢复内置卡面": "Restore Built-in Card Art",
      "恢复内置默认包": "Restore Built-in Default Pack",
      "恢复原版": "Restore Original",
      "恢复中…": "Restoring…",
      "回车": "Enter",
      "回放": "Replay",
      "回放 ID": "Replay ID",
      "回放 ID 只能是数字——就是游戏战绩里那一长串数字。": "A replay ID can only contain digits — it's the long number in your in-game match history.",
      "回放分析与复盘工具": "Replay Analysis & Review Tool",
      "回放服务器": "Replay Server",
      "回放服务器（不确定就按当前游戏版本选）": "Replay server (if unsure, match the current game version)",
      "回放复盘": "Replay Review",
      "回放库": "Replay Library",
      "回放库操作结果": "Replay Library Result",
      "回放列表": "Replay List",
      "回放文件解析成功！": "Replay file parsed successfully!",
      "回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。 填上回放 ID 就能直接下载进": "Replays are recorded by the server and stored on the official replay server; Save Replay in game pulls from there too. Enter a replay ID to download it straight into",
      "回合": "Round",
      "回合 ·": " · ",
      "回合：": ": ",
      "或其中的卡面图片。": "or the card art inside it.",
      "击杀": "Kills",
      "击杀数量（怪物）": "Kills (monsters)",
      "及其目录下的所有卡面图片吗？此操作不可撤销。": "and all card art images in its folder? This can't be undone.",
      "吉星派对 · BWiki 维基攻略": "Astral Party · BWiki Guides",
      "吉星派对回放与游戏工具箱": "Astral Party Replay & Game Toolbox",
      "集成回放分析、协议帧查看、维基浏览和游戏变速器管理，数据均在本地处理。": "Replay analysis, protocol frame inspection, wiki browsing, and Speedhack management in one place — all data is processed locally.",
      "技能使用": "Skill Usage",
      "继续创建皮肤包": "Continue Creating Skin Pack",
      "继续导入会用包里的旧版本覆盖现有文件。玩家改过的": "Continuing the import will overwrite existing files with the older versions in the pack. Player-modified",
      "加载器": "Loader",
      "加载器发布 ZIP 将一起安装加载器、SDK、bootstrap 和包内模组，保留已有加载器配置。仅使用可信的官方发布文件，重启游戏后生效。": "The loader release ZIP installs the loader, SDK, bootstrap, and bundled mods together, keeping your existing loader config. Only use trusted official release files; takes effect after a game restart.",
      "加载器更新日志": "Loader Changelog",
      "加载器内置功能": "Built-in Loader Features",
      "加载器缺失且版本未知": "Loader missing, version unknown",
      "加载器设置": "Loader Settings",
      "加载器已就位": "Loader is in place",
      "加载器与旧版独立变速器（「游戏工具」页）共用": "Shared with the legacy standalone speedhack (Game Tools page)",
      "检测到版本降级": "Version downgrade detected",
      "检测到游戏正在运行——安装 / 卸载不会被拦，但先看清下面的后果：": "The game is running — install/uninstall won't be blocked, but read the consequences below first:",
      "检测中": "Checking",
      "检查更新": "Check for Updates",
      "检查更新失败：": "Update check failed: ",
      "检查失败": "Check Failed",
      "检查失败：": "Check failed: ",
      "检查中…": "Checking…",
      "简体中文": "简体中文",
      "简要说明此方案的特色…": "Briefly describe what makes this profile special…",
      "将": "Will",
      "将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。": "This restores the card face from the app's built-in default pack, overwriting the same-named image in the current profile; a modified default on disk won't be used as the original template.",
      "将从应用内置资源恢复当前游戏的 default 方案，覆盖同名图片并移除 default 中的动态覆盖。不会改动其他方案，也不会自动切换当前选择。": "This restores the current game's default profile from the app's built-in assets, overwriting same-named images and removing animated overrides in default. Other profiles are untouched, and the current selection won't switch automatically.",
      "将从最新 Release 下载默认包并安装到当前游戏，覆盖 default 中的同名图片。下载不会自动切换当前方案。": "This downloads the default pack from the latest Release and installs it into the current game, overwriting same-named images in default. The download won't switch the current profile automatically.",
      "将含 bundle 的旧式卡面 ZIP 转换为卡牌皮肤方案": "Convert a legacy card-art ZIP containing bundles into a card skin profile",
      "将含 bundle 的旧式皮肤 ZIP 转换为自定义皮肤包": "Convert a legacy skin ZIP containing bundles into a custom skin pack",
      "交给 Steam 启动（DRM / 云存档 / 更新检查都走 Steam），并关闭加载器的 Steam 绕过 —— Steam 大厅 / 好友邀请进房恢复正常（默认）": "Let Steam launch it (DRM / cloud saves / update checks all go through Steam) and turn off the loader's Steam bypass — Steam lobby / friend invites work normally again (default)",
      "角色": "Character",
      "角色简介": "Character Bio",
      "角色名字": "Character Name",
      "角色皮肤": "Character Skin",
      "角色皮肤 · 图片 / 文案 / 精灵动画": "Character Skin · Images / Text / Sprite Animation",
      "角色皮肤管理": "Character Skin Management",
      "角色皮肤模组已安装": "Character skin mod installed",
      "角色缩略图": "Character Thumbnail",
      "角色头像": "Character Avatar",
      "角色文案": "Character Text",
      "脚底锚点 X": "Foot Anchor X",
      "脚底锚点 Y": "Foot Anchor Y",
      "阶段3 兜底（实测已作废、hook 从未命中；保持开启即可）": "Stage 3 fallback (obsolete in testing; the hook never fires; just leave it on)",
      "结构可疑": "Suspicious structure",
      "结算奖励": "Settlement Rewards",
      "结算快照中的完整属性与行动统计，记录本局关键数据指标。": "Full stats and action counts from the settlement snapshot, recording this match's key metrics.",
      "结算快照中的总伤害、承伤、治疗、转账与行动次数。": "Total damage, damage taken, healing, transfers, and action counts from the settlement snapshot.",
      "解析不出结算帧，文件可能已损坏或来自不兼容的版本。": "Couldn't parse a settlement frame; the file may be corrupted or from an incompatible version.",
      "解析失败": "Parse Failed",
      "解析展示失败:": "Failed to parse for display:",
      "介绍一下这套搭配，例如适合的玩法或角色组合。": "Describe this lineup, e.g. the playstyle or character combo it suits.",
      "界面动态效果": "Interface animations",
      "界面动态效果已": "Interface animations: ",
      "界面与配表语言": "Interface & Game Data Language",
      "仅回放库": "Replay library only",
      "仅删除 version.dll": "Delete version.dll only",
      "仅替换": "Replace only",
      "仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。": "Only counts movement dice results actually received in the replay; every die in multi-dice rolls is kept, controlled moves are marked separately, and combat or check dice are excluded.",
      "仅游戏内": "In-game only",
      "仅支持英文、数字、下划线及减号": "Only letters, digits, underscores, and hyphens are allowed",
      "进入游戏自动倍速": "Auto speed on entering the game",
      "进游戏后等待再挂接时间函数的毫秒数（防崩溃）": "Milliseconds to wait after entering the game before hooking time functions (crash prevention)",
      "旧 —— 点「重新安装内置版本」可装回内置版": "Old — click Reinstall Built-in Version to go back to the bundled build",
      "旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。": "Old — click Reinstall Built-in Version to go back to the bundled build, or Check for Updates to see if a newer one exists.",
      "旧版独立变速器": "Legacy Standalone Speedhack",
      "旧配置已保留，但暂未转换：": "Old config kept but not converted yet:",
      "居中重置": "Reset to Center",
      "局": " matches",
      "局 ·": " matches · ",
      "局）": " matches)",
      "局）。": " matches).",
      "橘雪莉": "Shirley",
      "卡面详情预览": "Card Art Details Preview",
      "卡牌 / 技能": "Cards / Skills",
      "卡牌 / 异画编号": "Card / Alt-Art Number",
      "卡牌编号:": "Card number:",
      "卡牌名称:": "Card name:",
      "卡牌皮肤": "Card Skin",
      "卡牌皮肤 · 方案与卡面管理": "Card Skin · Profiles & Card Art Management",
      "卡牌使用": "Cards Used",
      "开局 / 全局事件": "Opening / Global Events",
      "开启": "On",
      "开启看板立绘与页面切换动画": "Enable key art and page transition animations",
      "开启自动托管（保留": "Turn on auto-manage (keep",
      "开始深扫": "Start Deep Scan",
      "开始下载": "Start Download",
      "颗骰子 · 平均": " dice · avg ",
      "可更新": "Update available",
      "可以更新到 v": "Can update to v",
      "空格": "Space",
      "控制台窗口置顶": "Keep console window on top",
      "口径": "Basis",
      "库目录": "Library Folder",
      "库目录不可用": "Library folder unavailable",
      "快速扫描没找到，试试下面的深度扫描。": "Quick scan didn't find it; try the deep scan below.",
      "来源": "Source",
      "来源：随本程序内置（打包时的版本）": "Source: bundled with this app (version at packaging time)",
      "来源：GitHub 最新 Release": "Source: latest GitHub Release",
      "来自回合快照中的累计转账；未记录收款对象时不做推断。": "From cumulative transfers in round snapshots; no inference is made when the recipient isn't recorded.",
      "类别:": "Category:",
      "累计": "Cumulative",
      "累计伤害": "Total Damage",
      "累计死亡": "Total Deaths",
      "累计星币": "Total Star Coins",
      "累计转账": "Total Transferred",
      "离上限只差一点。开启自动托管后，每次打开本工具都会自动把超出的旧回放归档进库。": "Just short of the limit. With auto-manage on, every time you open this tool the extra old replays are archived to the library automatically.",
      "立绘物品图标": "Key Art Item Icon",
      "立即复制创建": "Copy and Create Now",
      "立即整理一次": "Tidy Up Now",
      "例如 10001": "e.g. 10001",
      "例如：我的日常搭配": "e.g. My Daily Lineup",
      "两边都删": "Delete Both",
      "浏览…": "Browse…",
      "浏览器打开": "Open in Browser",
      "浏览添加…": "Browse to Add…",
      "锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。": "Anchors use 0–1, with Y measured up from the bottom of the canvas; scale multiplies PPU and doesn't alter the game's character collision or movement.",
      "没检测到标准 Unity 结构": "No standard Unity structure detected",
      "没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）": "No standard Unity game structure detected (missing *_Data or UnityPlayer.dll)",
      "没有检测到桌面宿主，读不到程序集版本": "No desktop host detected; can't read the assembly version",
      "没有内嵌发布包": "No embedded release package",
      "没有内嵌发布包（本地开发构建）": "No embedded release package (local development build)",
      "没有匹配的方案。": "No matching profiles.",
      "没有匹配的皮肤包。可新建皮肤包或导入 ZIP。": "No matching skin packs. Create a skin pack or import a ZIP.",
      "每次打开本工具时，把超出保留名额的旧回放自动归档进库；最新的几局永远不会被动。": "Every time you open this tool, replays beyond the retention count are archived to the library automatically; the newest matches are never touched.",
      "每次进入游戏自动启用设定倍速": "Automatically apply the set speed each time you enter the game",
      "每单位像素 PPU": "PPU (pixels per unit)",
      "每个模组是引用 CesiumLoader.SDK 的 .NET DLL，一个模组一个文件夹：": "Each mod is a .NET DLL referencing CesiumLoader.SDK, one folder per mod:",
      "每回合新增": "Per round",
      "秒循环": "s loop",
      "名)": " players)",
      "名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。": "The name and bio use the character's shared text and may affect the character's other skins.",
      "命运卡": "Destiny Card",
      "命运卡 (": "Destiny Card (",
      "模组": "Mods",
      "模组」页面。": "Mods\" page.",
      "模组管理": "Mod Management",
      "模组页面内置换速开关：": "Speedhack toggles on the Mods page:",
      "模组与配置已保留，可恢复加载器": "Mods and config are kept; the loader can be restored",
      "模组状态与使用说明": "Mod status and usage notes",
      "默认：": "Default: ",
      "默认：文档\\AstralPartyReplays": "Default: Documents\\AstralPartyReplays",
      "默认方案不可删除": "The default profile can't be deleted",
      "默认方案预览": "Default Profile Preview",
      "默认目录": "Default folder",
      "默认皮肤方案 default 禁止删除": "The default skin profile \"default\" can't be deleted",
      "默认替换此人物的全部原皮肤。自定义素材优先，未修改的部分沿用所选基础。": "By default, replaces all of this character's original skins. Custom assets take priority; unmodified parts keep the selected base.",
      "目标角色": "Target character",
      "目录里是其它 version.dll": "The folder contains a different version.dll",
      "目录内检测到游戏主程序（AstralParty*.exe）": "Game executable found in folder (AstralParty*.exe)",
      "目录内未找到 AstralParty 游戏程序（仍可安装，但请确认目录正确）": "No AstralParty game executable found in the folder (you can still install, but double-check the folder)",
      "难度": "Difficulty",
      "内置": "Built-in",
      "内置 SHA-256": "Bundled SHA-256",
      "内置加载器": "Bundled Loader",
      "内置文件就绪：version.dll": "Bundled files ready: version.dll",
      "昵称，可留空": "Nickname, optional",
      "您的昵称": "Your nickname",
      "排序方式": "Sort by",
      "配置 ·": "Config · ",
      "配置保存在": "Config is saved in",
      "配置文件夹": "Config Folder",
      "配置写入后需按重载热键或重启游戏生效。": "After writing the config, press the reload hotkey or restart the game to apply it.",
      "皮肤": "Skin",
      "皮肤包操作": "Skin Pack Actions",
      "皮肤包素材库": "Skin Pack Asset Library",
      "皮肤短描述": "Short skin description",
      "皮肤方案管理与切换": "Manage and switch skin profiles",
      "皮肤故事": "Skin Story",
      "皮肤类型": "Skin Type",
      "偏移": "Offset",
      "品质": "Quality",
      "普通": "Common",
      "启动 Astral Party（吉星派对）": "Launch Astral Party",
      "启动 Astral Party（吉星派对）· 从 Steam 启动": "Launch Astral Party · via Steam",
      "启动 Astral Party（吉星派对）· 绕过 Steam：直接启动游戏主程序": "Launch Astral Party · bypass Steam: start the game executable directly",
      "启动版本": "Launch version",
      "启动方式": "Launch Method",
      "启动加速 startup_state": "Launch boost startup_state",
      "启动加速倍率": "Launch boost rate",
      "启动加速时长（秒）": "Launch boost duration (s)",
      "启动时基础倍速（1.0 = 正常；2.0 = 全程 2 倍速；也可留 1.0 由游戏内热键变速。不支持减速，最小 1.0）": "Base speed at launch (1.0 = normal; 2.0 = 2× throughout; leaving 1.0 lets in-game hotkeys change speed. Slowing down isn't supported; minimum 1.0)",
      "启动时基础倍速（1.0 = 正常；不支持减速，最小 1.0）": "Base speed at launch (1.0 = normal; slowing down isn't supported, minimum 1.0)",
      "启动时基础倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度，不支持减速）": "Base speed at launch must be between 1.0 and 100 (1.0 = normal speed; slowing down isn't supported)",
      "启动游戏": "Launch Game",
      "启用 / 禁用（重启游戏后生效）": "Enable / disable (takes effect after a game restart)",
      "启用 / 禁用与配置修改都在": "Enable/disable and config changes are all on",
      "启用此皮肤": "Enable This Skin",
      "启用后，启动游戏即按该倍速运行（全程保持）": "When enabled, the game runs at this speed from launch (held throughout)",
      "启用后无需按快捷键，游戏加载完成就持续使用下方倍速": "No hotkey needed once enabled; the speed below applies as soon as the game finishes loading",
      "启用加载器": "Enable Loader",
      "启用启动加速": "Enable Launch Boost",
      "强制卸载（version.dll 不属于本工具时也删除）": "Force uninstall (also delete version.dll if it wasn't installed by this tool)",
      "强制卸载（version.dll 与内置不一致时也删除）": "Force uninstall (also delete version.dll if it doesn't match the bundled copy)",
      "清单 JSON 无效：": "Invalid manifest JSON: ",
      "清空搜索": "Clear Search",
      "请等待当前操作完成。": "Please wait for the current operation to finish.",
      "请稍候": "Please wait",
      "请输入方案名称。": "Please enter a profile name.",
      "请输入方案目录名": "Please enter a profile folder name",
      "请输入方案显示名称": "Please enter a profile display name",
      "请输入有效的数字编号": "Please enter a valid numeric ID",
      "请先创建或选择一个有效方案。": "Please create or select a valid profile first.",
      "请先等待或取消当前转换": "Please wait for or cancel the current conversion first",
      "请先选择外部皮肤方案": "Please select an external skin profile first",
      "请选择发布包或粘贴完整路径。": "Please select a release package or paste its full path.",
      "请选择官方皮肤基础或自定义皮肤包。": "Please select an official skin base or a custom skin pack.",
      "请选择一个要深度扫描的盘符。": "Please select a drive to deep scan.",
      "请在左侧点击任意协议帧进行查看解码数据": "Click any protocol frame on the left to view its decoded data",
      "请展开模组状态查看详情。": "Expand mod status for details.",
      "区服": "Region",
      "取消": "Cancel",
      "取消排队": "Cancel Queue",
      "取消下载": "Cancel Download",
      "取消转换": "Cancel Conversion",
      "去模组页面（推荐）": "Go to Mods Page (Recommended)",
      "去重玩家动作统计": "Deduplicated Player Action Stats",
      "全部来源": "All Sources",
      "全部类别": "All Categories",
      "全部原皮肤": "All original skins",
      "全队转账": "Team Transfers",
      "缺少内置文件": "Missing bundled files",
      "缺失": "Missing",
      "确定（已选": "Confirm (",
      "确定删除模组「": "Delete mod \"",
      "确定要彻底删除方案": "Permanently delete profile",
      "确定要将": "Are you sure you want to",
      "确认并导入": "Confirm and Import",
      "确认还原": "Confirm Restore",
      "确认继续": "Confirm and Continue",
      "确认删除": "Confirm Delete",
      "确认删除皮肤方案": "Confirm Deleting Skin Profile",
      "确认卸载": "Confirm Uninstall",
      "绕过 Steam 启动": "Bypass Steam Launch",
      "热更新资源缓存": "Hot-update asset cache",
      "人物": "Character",
      "人物皮肤": "Character Skin",
      "人物皮肤包": "Character Skin Pack",
      "人物皮肤编辑分类": "Character skin edit categories",
      "人物设置": "Character Settings",
      "人物缩放": "Character Scale",
      "任务加入后台队列后可继续新建；右下角查看进度或终止。": "Tasks continue in the background queue while you create more; check progress or stop them in the bottom-right.",
      "仍然导入": "Import Anyway",
      "仍要放回（": "Restore Anyway (",
      "日本語": "日本語",
      "如 二次元萌化包": "e.g. Anime Moe Pack",
      "扫描系统": "Scan System",
      "筛选筹码、玩家、品质、来源...": "Filter by relic, player, quality, source...",
      "筛选商店类型、玩家、卡牌、购买结果...": "Filter by shop type, player, card, purchase result...",
      "删除": "Delete",
      "删除此方案": "Delete This Profile",
      "删除方案": "Delete Profile",
      "删除该方案": "Delete That Profile",
      "删除回放": "Delete Replay",
      "删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。": "Deletion goes to the Windows Recycle Bin first and can still be undone. Deleting only the in-game copy is the usual way to free a slot; the library copy is your long-term archive.",
      "删除角色皮肤？": "Delete character skin?",
      "删除皮肤包": "Delete Skin Pack",
      "删除整个方案？": "Delete the whole profile?",
      "伤害": "Damage",
      "商店": "Shop",
      "商店记录": "Shop Records",
      "商店进店共": "Shop visits: ",
      "上次游戏未能应用方案：": "The profile couldn't be applied last game session: ",
      "上面没有你的游戏？深度扫描某个盘（较慢，可取消）：": "Your game isn't listed? Deep scan a drive (slower, cancellable):",
      "上一帧": "Previous Frame",
      "尚未安装；游戏正在运行，本次安装重启游戏后才加载": "Not installed yet; the game is running, so this install loads only after a restart",
      "尚未安装到游戏": "Not installed to the game yet",
      "尚未安装加载器": "Loader not installed yet",
      "尚未安装加载器 —— 点「安装加载器」，加载器与内置模组会一起装进游戏目录。": "Loader not installed yet — click Install Loader to install the loader and built-in mods into the game folder together.",
      "尚未安装加载器 / 目录为空": "Loader not installed / folder empty",
      "尚未安装角色皮肤模组": "Character skin mod not installed yet",
      "尚未安装角色皮肤模组，请从左侧管理菜单安装。": "The character skin mod isn't installed yet; install it from the management menu on the left.",
      "尚未打开回放文件": "No replay file opened yet",
      "尚未定位游戏目录": "Game folder not located yet",
      "尚未启用": "Not enabled yet",
      "尚未选择游戏目录": "No game folder selected yet",
      "尚未选择游戏目录（可点「自动检测」）": "No game folder selected yet (you can click Auto-Detect)",
      "稍后再说": "Maybe Later",
      "设为当前": "Set as Current",
      "设为当前皮肤": "Set as Current Skin",
      "设置": "Settings",
      "深度扫描": "Deep Scan",
      "深度扫描没找到 AstralParty*.exe。": "The deep scan didn't find AstralParty*.exe.",
      "生命": "Health",
      "生效；「": "to take effect; \"",
      "时长": "Duration",
      "时长(秒)": "Duration (s)",
      "实时调整（最低 1 倍，不支持减速）。": "Adjust in real time (minimum 1×, slowing down isn't supported).",
      "使用方式": "How to Use",
      "使用托管引导（实验性，一般保持关闭）": "Use managed bootstrap (experimental; normally keep it off)",
      "使用须知": "Usage Notes",
      "使用原版": "Use Original",
      "事件卡": "Event Card",
      "事件卡 (": "Event Card (",
      "事件时间线": "Event Timeline",
      "视频 / GIF 按选定区域裁剪为普通卡面的 404 × 400，所有帧使用同一选区。首次使用可能下载 FFmpeg。": "Videos / GIFs are cropped to the normal card face size of 404 × 400 using the selected area, with all frames sharing the same selection. FFmpeg may be downloaded on first use.",
      "适用于此人物全部原皮肤": "Applies to all of this character's original skins",
      "适用原皮肤": "Applies to original skin",
      "手动打开任意回放文件，本地解析玩家、回合、动作、筹码流转与底层协议帧。": "Open any replay file manually to parse players, rounds, actions, relic flow, and raw protocol frames locally.",
      "手牌卡": "Hand Card",
      "手牌卡 (": "Hand Card (",
      "售价": "Price",
      "输出伤害": "Damage Dealt",
      "输出预览": "Output Preview",
      "输入方案名称": "Enter profile name",
      "输入角色名或皮肤名": "Enter a character or skin name",
      "数据统计": "Statistics",
      "数字": "Number",
      "刷新": "Refresh",
      "刷新方案与状态": "Refresh Profiles and Status",
      "刷新记录": "Refresh Records",
      "刷新列表": "Refresh List",
      "双击在资源管理器中打开": "Double-click to open in File Explorer",
      "死亡计数": "Death Count",
      "搜索方案": "Search profiles",
      "搜索回放": "Search replays",
      "搜索回放 ID / 地图 / 玩家…": "Search replay ID / map / player…",
      "搜索角色 / 皮肤包": "Search characters / skin packs",
      "搜索卡名、卡号 #101 或标识…": "Search card name, card number #101, or ID…",
      "搜索帧号、CMD、消息名...": "Search frame number, CMD, message name...",
      "随本程序内置": "Bundled with this app",
      "随加载器一起安装的内置模组": "Built-in mods installed with the loader",
      "所有数据均在本地安全离线处理": "All data is processed locally and offline",
      "它是怎么工作的？": "How does it work?",
      "台词": "Lines",
      "提交": "Commit",
      "替换": "Replace",
      "替换哪些原皮肤": "Which original skins to replace",
      "替换选中帧": "Replace Selected Frame",
      "添加": "Add",
      "添加到多人物方案": "Add to Multi-character Profile",
      "添加到方案": "Add to Profile",
      "添加第一个人物": "Add First Character",
      "添加或修改人物后，完全退出并重启游戏以应用搭配。": "After adding or changing characters, fully exit and restart the game to apply the lineup.",
      "添加人物皮肤": "Add Character Skin",
      "填写普通卡牌编号，或异画自己的编号。普通卡牌绑定只替换普通版；异画需要单独指定。": "Enter the normal card number, or the alt-art's own number. A normal-card binding only replaces the normal version; alt arts must be specified separately.",
      "条": " entries",
      "条)": " entries)",
      "条筹码流转": " relic transfers",
      "条事件记录": " event records",
      "跳转到帧": "Go to Frame",
      "停用并移入皮肤目录中的 .deleted 文件夹。": "Disable and move to the .deleted folder in the skins folder.",
      "停用此方案": "Disable This Profile",
      "同步配置：": "Sync config: ",
      "同步配置到游戏": "Sync Config to Game",
      "同时也是工具箱的": "and also the toolbox's ",
      "图片": "Images",
      "图片预览加载失败": "Failed to load image preview",
      "图片资源": "Image assets",
      "退格": "Backspace",
      "托管引导程序集": "Managed Bootstrap Assembly",
      "拖动或用方向键移动裁剪区域": "Drag or use the arrow keys to move the crop area",
      "拖动选区移动位置，拖动四角调整大小。": "Drag the selection to move it, or drag a corner to resize.",
      "拖动选区移动位置，用滑块调整大小。透明背景会保留。": "Drag the selection to move it, or use the slider to resize. Transparent backgrounds are preserved.",
      "完整立绘": "Full Key Art",
      "完整卸载加载器": "Fully Uninstall Loader",
      "玩家": "Player",
      "玩家表现": "Player Performance",
      "玩家数据对照": "Player Comparison",
      "玩家转账贡献": "Player Transfer Contribution",
      "维基攻略": "Wiki Guides",
      "未安装": "Not installed",
      "未安装 · 游戏运行中": "Not installed · Game running",
      "未保存修改": "Unsaved changes",
      "未记录": "Not recorded",
      "未记录骰面点数": "Dice values not recorded",
      "未检测": "Not checked",
      "未检测到游戏回放目录": "No game replay folder detected",
      "未检测到游戏回放目录。你仍然可以用左侧按钮直接打开任意回放文件。": "No game replay folder detected. You can still open any replay file directly with the button on the left.",
      "未检查": "Not checked",
      "未命名方案": "Unnamed Profile",
      "未启用": "Not enabled",
      "未设置（用 mod 默认）": "Not set (use the mod default)",
      "未设置按键": "No key set",
      "未识别": "Unrecognized",
      "未识别的皮肤": "Unrecognized skin",
      "未提供，使用原版": "Not provided; using the original",
      "未添加游戏 —— 去设置页添加": "No game added — add one in Settings",
      "未选择方案": "No profile selected",
      "未找到匹配的卡牌": "No matching cards found",
      "未找到原资源尺寸，使用受限的预设尺寸": "Original asset size not found; using a limited preset size",
      "未知": "Unknown",
      "未知版本": "Unknown version",
      "未知筹码": "Unknown Relic",
      "文案": "Text",
      "文档\\AstralPartyReplays": "Documents\\AstralPartyReplays",
      "文件": "File",
      "文件不属于本工具": "File not from this tool",
      "文件不一致": "File mismatch",
      "文件名称:": "File name:",
      "文件已选择，确认上方安装说明后点击「确认并导入」。": "File selected; review the install notes above, then click Confirm and Import.",
      "文件有误": "Invalid file",
      "我的方案": "My Profiles",
      "无": "None",
      "无 —— 不是从 git 仓库构建的": "None — not built from a git repository",
      "无法加载图片，请取消后重新选择。": "Can't load the image; cancel and choose again.",
      "无法解析": "Unreadable",
      "无符合条件的事件记录": "No events match the filters",
      "稀有": "Rare",
      "系统": "System",
      "系统设置": "System Settings",
      "下每个模组的 sidecar（同名": " the sidecar of each mod (same-named ",
      "下面这些模组，包里的版本比游戏目录里已装的更旧：": "These mods in the package are older than the versions installed in the game folder:",
      "下一帧": "Next Frame",
      "下载默认包": "Download Default Pack",
      "下载默认皮肤包": "Download Default Skin Pack",
      "下载失败。": "Download failed.",
      "下载完成": "Download complete",
      "下载完成。": "Download complete.",
      "下载中…": "Downloading…",
      "先安装加载器": "Install the Loader First",
      "先安装角色皮肤模组，方案才能在游戏中生效。": "Install the character skin mod first so profiles can take effect in game.",
      "先给搭配起个名字，再添加喜欢的人物皮肤。": "Give the lineup a name, then add the character skins you like.",
      "显示": "Show",
      "显示控制台窗口（模组日志）": "Show console window (mod logs)",
      "显示名称": "Display name",
      "相机工具 / 活动日志 / 模组管理 / 权限声明 / 游戏内热键变速…": "Camera tools / Activity log / Mod management / Permission notices / In-game hotkey speed…",
      "详情": "Details",
      "想要新功能": "Want new features?",
      "项目主页": "Project Homepage",
      "消息类型": "Message Type",
      "校验并保存清单": "Validate and Save Manifest",
      "协议命令统计": "Protocol Command Stats",
      "协议载荷解码详情": "Protocol Payload Decode Details",
      "协议帧共": "Protocol frames: ",
      "写入": "Write",
      "卸载：": "Uninstall: ",
      "卸载变速器": "Uninstall Speedhack",
      "卸载加载器": "Uninstall Loader",
      "卸载将删除加载器及 AstralParty_ModLoader 目录，包括模组、配置和日志。此操作无法通过工具撤销。": "Uninstalling deletes the loader and the AstralParty_ModLoader folder, including mods, configs, and logs. This can't be undone from the tool.",
      "卸载中…": "Uninstalling…",
      "新版本": "New version",
      "新建第一个方案": "Create Your First Profile",
      "新建方案": "New Profile",
      "新建皮肤方案": "New Skin Profile",
      "新建一个方案，再添加人物皮肤。之后可以一键切换整套搭配。": "Create a profile, then add character skins. You can then switch the whole lineup with one click.",
      "新建一个角色皮肤包，或导入 ZIP 开始制作。": "Create a character skin pack, or import a ZIP to get started.",
      "新建自定义皮肤": "New Custom Skin",
      "新建自定义皮肤包": "New Custom Skin Pack",
      "新写入的 version.dll 要重启游戏才会加载，且运行中的游戏可能占用该文件导致覆盖失败。": "A newly written version.dll only loads after a game restart, and a running game may hold the file and cause the overwrite to fail.",
      "新增击杀": "New Kills",
      "新增伤害": "New Damage",
      "新增转账": "New Transfers",
      "星币": "Star Coins",
      "星币 · 击杀": "Star Coins · Kills",
      "修复点“创建 / 加入房间”没反应（大厅匹配改为直接返回空结果）": "Fixed Create / Join Room doing nothing (lobby matchmaking now returns an empty result directly)",
      "修复退房 / 被踢时报错（房间列表查询返回空列表）": "Fixed an error when leaving or being kicked from a room (room list query returns an empty list)",
      "修改后完全退出并重启游戏生效。图片、文案和地图动画已提供；媒体、特效及特殊演出保留原版，运行时效果待游戏验证。": "Fully exit and restart the game for changes to apply. Images, text, and map animations are provided; media, effects, and special cutscenes stay original, and runtime appearance awaits in-game verification.",
      "修改时间:": "Modified:",
      "修改显示名称、作者和方案描述": "Change the display name, author, and profile description",
      "修饰键": "Modifier keys",
      "需要修复": "Needs repair",
      "选区保持普通卡面的 404 : 400 比例，所有视频帧使用同一裁剪范围。": "The selection keeps the normal card face's 404 : 400 ratio, and all video frames use the same crop.",
      "选区边长（原图像素）": "Selection side (source pixels)",
      "选区大小": "Selection size",
      "选区宽度（原图像素）": "Selection width (source pixels)",
      "选区始终为正方形。": "The selection is always square.",
      "选取你的卡面": "Pick Your Card Art",
      "选择": "Select",
      "选择…": "Choose…",
      "选择按键组合 ·": "Choose key combo · ",
      "选择当前正在操作的游戏": "Select the game you're working with",
      "选择动画帧": "Select animation frame",
      "选择发布包或粘贴完整路径": "Choose a release package or paste the full path",
      "选择回放文件": "Choose Replay File",
      "选择皮肤方案：": "Select skin profile:",
      "选择视频 / GIF": "Choose Video / GIF",
      "选择文件…": "Choose File…",
      "选择文件后，点击「确认并导入」才会安装。SDK 工具包或单独 SDK DLL 仅更新 SDK。": "Nothing installs until you click Confirm and Import after choosing a file. An SDK toolkit or standalone SDK DLL only updates the SDK.",
      "选择要启动的游戏版本": "Select the game version to launch",
      "选择要深度扫描的盘符": "Select a drive to deep scan",
      "选择一个皮肤包，或新建方案开始制作。": "Pick a skin pack, or create a profile to get started.",
      "选择已有皮肤包、制作自定义皮肤，或搭配官方皮肤。": "Pick an existing skin pack, make a custom skin, or combine official skins.",
      "循环": "Loop",
      "沿用原皮肤": "Keep Original Skin",
      "要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；": "To switch to CesiumLoader, tick Allow overwriting a version.dll in the game folder that this tool didn't install below, then click Install Loader;",
      "也包括变速本身": "including the Speedhack itself",
      "也可以导入新的皮肤包。": "You can also import a new skin pack.",
      "依赖：": "Dependencies: ",
      "移除": "Remove",
      "移除此人物规则？": "Remove the rule for this character?",
      "移动点数": "Movement Roll",
      "移动骰子统计": "Movement Dice Stats",
      "已安装": "Installed",
      "已安装 · 游戏运行中": "Installed · Game running",
      "已安装 v": "Installed v",
      "已安装，但这一份没有版本清单，读不出准确版本 —— 重新安装一次即可纳入版本管理。": "Installed, but this copy has no version manifest, so the exact version can't be read — reinstall once to bring it under version tracking.",
      "已安装（内置版本号读取失败）": "Installed (couldn't read the bundled version)",
      "已安装的模组": "Installed Mods",
      "已持有": "Owned",
      "已存在，继续将覆盖它。": "Already exists; continuing will overwrite it.",
      "已禁用": "Disabled",
      "已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近": "Auto-manage is on: from now on, every time you open this tool, replays beyond the slot limit are archived to the library automatically (the game keeps the latest",
      "已启用": "Enabled",
      "已启用 · 重启游戏后生效": "Enabled · Takes effect after a game restart",
      "已切换语言": "Language switched",
      "已设为": "Set as ",
      "已设为当前方案": "Set as current profile",
      "已是最新版本 v": "Already up to date (v",
      "已售出": "Sold",
      "已添加": "Added",
      "已停用": "Disabled",
      "已停用或缺失，恢复时使用此版本": "Disabled or missing; this version is used for restore",
      "已同步": "Synced",
      "已完成": "Completed",
      "已下载": "Downloaded",
      "已应用": "Applied",
      "已有皮肤包": "Existing Skin Pack",
      "已终止": "Stopped",
      "已装的 v": "Installed v",
      "已自定义": "Customized",
      "以后自动保持最近 10 局": "Keep the latest 10 matches from now on",
      "异画道具": "Alt-Art Item",
      "异画道具 (": "Alt-Art Item (",
      "引导方法": "Bootstrap method",
      "引导类型": "Bootstrap type",
      "引导与超时（一般不用改）": "Bootstrap & timeouts (usually leave as is)",
      "应用版本": "App version",
      "应用变速": "Apply Speed",
      "应用此方案": "Apply This Profile",
      "应用失败": "Apply failed",
      "用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。": "Compare the match with damage, support, and action data; roles differ, so players aren't judged on total damage alone.",
      "用右侧「浏览添加」手动选，或「扫描系统」自动找。": "Use Browse to Add on the right to pick manually, or Scan System to find it automatically.",
      "游戏变速": "Game Speedhack",
      "游戏大厅": "Game Lobby",
      "游戏工具": "Game Tools",
      "游戏加速（变速器）": "Game Acceleration (Speedhack)",
      "游戏进程未运行，安装 / 卸载会立刻生效": "The game isn't running, so installs/uninstalls apply immediately",
      "游戏默认方案仅使用游戏内部卡面，无需外部图片。": "The game's default profile uses only the game's built-in card art and needs no external images.",
      "游戏默认方案没有可编辑的方案信息": "The game's default profile has no editable profile info",
      "游戏目录 · 覆盖与降级 · SDK 依赖 · 卸载": "Game folder · Overwrite & downgrade · SDK dependencies · Uninstall",
      "游戏目录 speedhack_config.json 存在": "speedhack_config.json exists in the game folder",
      "游戏目录 version.dll 与内置不一致——可能来自其它工具": "The game folder's version.dll doesn't match the bundled copy — possibly from another tool",
      "游戏目录 version.dll 与内置一致": "The game folder's version.dll matches the bundled copy",
      "游戏目录（多个位置）": "Game Folders (multiple)",
      "游戏目录（version.dll 的安装位置）": "Game folder (where version.dll is installed)",
      "游戏目录里的 version.dll 不是本工具安装的（可能是旧版独立变速器或第三方文件）——安装或更新前请先确认来源。": "The version.dll in the game folder wasn't installed by this tool (it may be the legacy standalone speedhack or a third-party file) — verify its source before installing or updating.",
      "游戏目录里的 version.dll 不是本工具装的": "The version.dll in the game folder wasn't installed by this tool",
      "游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：": "The version.dll in the game folder is neither the bundled copy nor recorded in the install manifest:",
      "游戏目录里暂无 version.dll，可直接安装": "No version.dll in the game folder; you can install directly",
      "游戏目录暂无 speedhack_config.json": "No speedhack_config.json in the game folder yet",
      "游戏内": "In-game",
      "游戏内保留局数": "In-game retention",
      "游戏内回放席位快满了（": "In-game replay slots are almost full (",
      "游戏内回放席位已满": "In-game replay slots are full",
      "游戏内回放席位已满（": "In-game replay slots are full (",
      "游戏内席位": "In-game slots",
      "游戏启动后自动以指定倍速加速一段时长（跳过开场动画）": "Automatically run at the set speed for a while after launch (skips the opening animation)",
      "游戏启动中，稍候即可再切换启动方式": "The game is starting; you can switch launch methods again shortly",
      "游戏维基（Starengine Wiki）": "Game Wiki (Starengine Wiki)",
      "游戏运行中也能安装 / 卸载，但": "Installing/uninstalling works while the game is running, but",
      "游戏在「本地回放」列表里遇到这种目录会把它整个删掉": "The game deletes such folders entirely when it encounters them in the Local Replays list",
      "游戏正在运行：安装 / 更新 / 卸载都要重启游戏才生效。": "The game is running: installs/updates/uninstalls only take effect after a restart.",
      "游戏正在运行：归档/放回随时可用，但别在游戏里正播着回放时操作": "The game is running: archiving/restoring works anytime, but don't do it while a replay is playing in game",
      "游戏中的角色皮肤模组为": "The character skin mod in game is",
      "游戏自带的「本地回放」只有": "The game's built-in Local Replays only has",
      "有新版本 v": "New version v",
      "右Alt": "Right Alt",
      "右Ctrl": "Right Ctrl",
      "右Shift": "Right Shift",
      "与「已禁用」状态仍会保留， 被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。": " and the Disabled state are kept; replaced old files go to the Recycle Bin (kept if it's unavailable). If you just want to upgrade, use a newer mod pack instead.",
      "与随程序内置的版本一致": "matches the bundled version",
      "原理：把内置": "How it works: the built-in",
      "原始协议帧": "Raw Protocol Frames",
      "原图": "Original",
      "源码提交": "Source Commit",
      "远野汉娜": "Hannah Tono",
      "允许安装较旧版本（降级）": "Allow installing an older version (downgrade)",
      "允许把已装的加载器降级成内置的较旧版本": "Allow downgrading the installed loader to the older bundled version",
      "允许覆盖不属于本工具的 version.dll": "Allow overwriting a version.dll not installed by this tool",
      "允许覆盖游戏目录里不属于本工具的 version.dll": "Allow overwriting a version.dll in the game folder not installed by this tool",
      "允许覆盖游戏目录里其它 version.dll（用于强行更新安装）": "Allow overwriting other version.dll files in the game folder (for forced reinstall)",
      "允许游戏内热键变速（Delete 在 1.0x 与刚才的倍率之间切换，Alt+= / Alt+- 调倍率）": "Allow in-game speed hotkeys (Delete toggles between 1.0× and the previous rate, Alt+= / Alt+- adjust it)",
      "运行环境": "Environment",
      "运行时": "Runtime",
      "再次导入": "Import Again",
      "再在游戏里点「保存」会提示已达上限。归档最旧的几局就能立刻腾出席位——回放不会丢，它们会进上面的回放库。": "Clicking Save in game again will say the limit is reached. Archiving the oldest matches frees slots immediately — nothing is lost; they go to the replay library above.",
      "在 AstralParty.Toys 程序窗口里打开这一页才能查看更新日志。": "Open this page inside the AstralParty.Toys app window to view the changelog.",
      "在 AstralParty.Toys 程序窗口里打开这一页就能看到版本信息。": "Open this page inside the AstralParty.Toys app window to see version info.",
      "在系统默认浏览器中打开": "Open in Default Browser",
      "在资源管理器中打开": "Open in File Explorer",
      "载荷大小:": "Payload size:",
      "载荷字节": "Payload bytes",
      "暂停": "Pause",
      "暂无方案描述": "No profile description",
      "暂无描述说明": "No description",
      "暂无统计数据": "No statistics yet",
      "窄幅角色图": "Narrow Character Image",
      "战斗半身图 1": "Battle Portrait 1",
      "战斗半身图 2": "Battle Portrait 2",
      "战绩里那一长串数字": "the long number in your match history",
      "张卡面": " card faces",
      "账号背景": "Account Background",
      "找到": "Found ",
      "这份安装没有版本清单，重新安装一次即可记录": "This install has no version manifest; reinstall once to record it",
      "这个键不支持，请换一个": "That key isn't supported; try another",
      "这个目录已经不在了": "That folder no longer exists",
      "这个鼠标键不支持，请换一个": "That mouse button isn't supported; try another",
      "这是本地构建：版本号来自项目文件，提交号与文件时间说明它对应哪一次源码。": "This is a local build: the version comes from the project file, and the commit ID and file time show which source revision it matches.",
      "这些回放解析不出结算帧": "These replays have no parseable settlement frame",
      "帧": " frames",
      "帧 ·": " · ",
      "帧\" title=\"": " frames\" title=\"",
      "帧/秒 ·": " fps · ",
      "帧号:": "Frame number:",
      "帧预览": "Frame Preview",
      "正在保存…": "Saving…",
      "正在处理": "Processing",
      "正在处理，请稍候": "Processing, please wait",
      "正在从 GitHub 拉取最新加载器更新日志…": "Fetching the latest loader changelog from GitHub…",
      "正在读取…": "Reading…",
      "正在读取版本信息…": "Reading version info…",
      "正在读取更新日志…": "Reading changelog…",
      "正在读取加载器更新日志…": "Reading loader changelog…",
      "正在检测…": "Detecting…",
      "正在检测安装状态…": "Checking install status…",
      "正在检测回放目录…": "Detecting replay folder…",
      "正在解码 Protobuf 载荷…": "Decoding Protobuf payload…",
      "正在解析回放…": "Parsing replay…",
      "正在清理…": "Cleaning up…",
      "正在取消下载…": "Canceling download…",
      "正在统计事件…": "Counting events…",
      "正在下载…": "Downloading…",
      "正在校验并安装发布包…": "Verifying and installing release package…",
      "正在终止": "Stopping",
      "正在转换…": "Converting…",
      "正在准备资源": "Preparing assets",
      "支持 MP4、WebM、MOV、AVI、MKV 和 GIF，最多 20 秒。先裁剪为 404 × 400，再在普通卡框中循环播放。": "Supports MP4, WebM, MOV, AVI, MKV, and GIF, up to 20 seconds. It's cropped to 404 × 400 and loops in the normal card frame.",
      "支持简体中文、English、日本語、繁體中文": "Supports 简体中文, English, 日本語, and 繁體中文",
      "支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。": "Supports original sprite name mapping (SpriteMappings) and custom asset addresses (asset:). Media files can be stored in the pack but aren't applied yet.",
      "知道回放 ID（游戏战绩里那一串数字）时，直接从官方回放服务器下载进回放库": "If you know the replay ID (the long number in your in-game match history), download it straight from the official replay server into the replay library",
      "知道了，这一版不再提示": "Got it, don't show this again for this version",
      "直接启动游戏主程序，完全不经过 Steam；会打开加载器的 Steam 绕过（没装 Steam 也能进游戏），代价是 Steam 大厅 / 好友邀请进房失效": "Start the game executable directly, bypassing Steam entirely; this turns on the loader's Steam bypass (the game runs without Steam), at the cost of Steam lobby / friend invites",
      "只能装一个": "Only one can be installed",
      "只删库内副本（游戏里仍能看）": "Delete only the library copy (still viewable in game)",
      "只删游戏内副本（腾出席位）": "Delete only the in-game copy (frees a slot)",
      "只想清掉就勾选「强制卸载」。": "If you just want it cleared, tick Force Uninstall.",
      "只想要变速": "Just Want the Speedhack",
      "只想用旧版变速器？忽略这段，照下面操作即可。": "Only want the legacy speedhack? Skip this and follow the steps below.",
      "只修改方案对外展示的信息，不会改变方案目录名": "Only changes the profile's public info; the profile folder name stays the same",
      "只移除当前方案中的规则，自定义皮肤包会保留。": "Only removes the rule from the current profile; custom skin packs are kept.",
      "指标": "Metric",
      "治疗": "Healing",
      "终止任务": "Stop Task",
      "重启游戏": "Restart Game",
      "重启游戏后": "after a game restart",
      "重试导入": "Retry Import",
      "重新安装内置版本": "Reinstall Built-in Version",
      "重新导入帧序列会替换整个动作；替换选中帧保留帧数、顺序和播放参数。": "Re-importing a frame sequence replaces the whole action; replacing the selected frame keeps the frame count, order, and playback settings.",
      "重新载入维基页面": "Reload Wiki Page",
      "重载配置热键": "Reload Config Hotkey",
      "重载配置热键 reload_config_keys": "Reload config hotkey reload_config_keys",
      "重置筛选条件": "Reset Filters",
      "逐回合数据表": "Per-round data table",
      "主配置：": "Main config: ",
      "注册表": "Registry",
      "注意：这个「变速器」和模组系统": "Note: this speedhack and the mod system",
      "专业信息": "Technical Info",
      "转出": "Transferred Out",
      "转换并保存": "Convert and Save",
      "转换动态卡面": "Convert Animated Card Art",
      "转账": "Transfers",
      "转账贡献": "Transfer Contribution",
      "转账星币": "Star Coins Transferred",
      "准备界面": "Ready Screen",
      "字节": "bytes",
      "字母": "Letters",
      "自定义": "Custom",
      "自定义 + 官方基础": "Custom + Official Base",
      "自定义皮肤": "Custom Skin",
      "自定义皮肤包": "Custom Skin Pack",
      "自动保持游戏内最近": "Auto-keep the latest ",
      "自动保持游戏内最近 10 局": "Auto-keep the latest 10 matches in game",
      "自动倍速": "Auto Speed",
      "自动倍速倍率": "Auto speed rate",
      "自动检测": "Auto-Detect",
      "自动托管已": "Auto-manage is ",
      "自动托管已开启（保留": "Auto-manage is on (keep",
      "总承伤": "Total Damage Taken",
      "总伤害": "Total Damage",
      "左Alt": "Left Alt",
      "左Ctrl": "Left Ctrl",
      "左Shift": "Left Shift",
      "作者": "Author",
      "作者:": "Author:",
      "作者：": "Author: ",
      "作者: —": "Author: —",
      "作者签名（可选）：": "Author signature (optional):",
      "AstralParty.Toys · 吉星派对工具箱": "AstralParty.Toys · Astral Party Toolbox",
      "CardSkinMod · 方案管理 / 导入 / 单卡可视化替换": "CardSkinMod · Profile management / import / per-card visual replacement",
      "CesiumLoader · 加载器 / 模组 / 变速": "CesiumLoader · Loader / Mods / Speedhack",
      "com.unity.addressables 目录": "com.unity.addressables folder",
      "DLL 缺失": "DLL missing",
      "DLL 缺失，无法确定恢复版本": "DLL missing; can't determine the restore version",
      "GitHub 最新": "Latest on GitHub",
      "GitHub 最新 Release": "Latest GitHub Release",
      "HUD 角色图": "HUD Character Image",
      "Mod 加载器": "Mod Loader",
      "Mod 加载器与模组管理": "Mod Loader & Mod Management",
      "PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。": "PNG/JPG frames are sorted by file name. Idle for idle, Walk forward, Walk-back backward; other action names follow the original character.",
      "PVE商店": "PVE Shop",
      "SDK 版本（只读，随包更新）": "SDK version (read-only, updated with the package)",
      "sdk 目录为空（重新安装加载器会写入 CesiumLoader.SDK.dll）": "The sdk folder is empty (reinstalling the loader writes CesiumLoader.SDK.dll)",
      "SDK 依赖（sdk\\ 目录）": "SDK dependencies (sdk\\ folder)",
      "SFW 半身图 1": "SFW Portrait 1",
      "SFW 半身图 2": "SFW Portrait 2",
      "SFW 立绘": "SFW Key Art",
      "SFW 缩略图": "SFW Thumbnail",
      "SFW 窄幅图": "SFW Narrow Image",
      "SFW 准备图": "SFW Ready Image",
      "speedhack_config.json 配置": "speedhack_config.json config",
      "Steam 绕过细节 —— 要从 Steam 启动并使用真大厅，这三个开关必须全部关闭": "Steam bypass details — to launch from Steam and use the real lobby, all three switches must be off",
      "Task 构造方式（auto / direct / invoke，一般保持 auto）": "Task construction (auto / direct / invoke; usually keep auto)",
      "Unity 结构": "Unity structure",
      "version.dll 已被游戏占用，删除可能失败；重启游戏后才恢复正常速度。": "version.dll is in use by the game, so deleting it may fail; normal speed returns after a game restart.",
      "version.dll 已停用或缺失，保留版本": "version.dll disabled or missing; version kept",
      "x（重启游戏生效）": "x (takes effect after a game restart)",
      "。修改配置后，重启游戏并刷新可查看新结果。": ". Restart the game and refresh after changing the config to see the new result.",
      "个筹码：": " relics: ",
      "决胜": "Decisive",
      "可操作游戏": "Can operate game",
      "处理进度": "Progress",
      "拉取最新": "Fetch Latest",
      "拉取最新」重试。": "Fetch Latest\" to retry.",
      "曲线": "Chart",
      "最近一次 Mod 记录：": "Latest Mod record: ",
      "目录已失效": "Folder missing",
      "预览": "Preview",
      "(未选择)": "(None selected)",
      "- 动作类型: 【最终选定筹码】": "- Action type: [Final relic pick]",
      "- 动作类型: 【首次呈现候选】": "- Action type: [First candidate offer]",
      "- 选定筹码品质分布:": "- Picked relic quality distribution:",
      "- 选定筹码来源分布:": "- Picked relic source distribution:",
      "1. 【流派与契合度评估】请结合每位玩家的角色技能与定位，分析他们选取的筹码是否形成了有效联动（Synergy）？有没有错失更优质的候选筹码？": "1. [Build & synergy assessment] Considering each player's character skills and role, did their relic picks form effective synergies? Did anyone miss better candidate relics?",
      "2. 【刷新时机与策略分析】请评价各位玩家在各回合中的刷新决策：是否存在盲目刷新浪费机会，或是在关键轮次赌出了核心关键筹码？": "2. [Reroll timing & strategy] Evaluate each player's reroll decisions per round: did anyone reroll blindly and waste chances, or gamble into a core relic in a key round?",
      "4. 【改进建议】如果给表现落后或伤害不足的玩家提供改进策略，在筹码获取和资源配置上应该做出哪些针对性调整？": "4. [Improvement suggestions] For players who fell behind or dealt little damage, what targeted changes to relic acquisition and resource allocation would help?",
      "AstralParty_ModLoader\\doorstop_config.json 里的 speedhackBaseSpeed 即可变速。": "Set speedhackBaseSpeed in AstralParty_ModLoader\\doorstop_config.json to change speed.",
      "CesiumLoader 还没有发布过任何 Release（打 modloader-* tag 触发 GitHub Action 即可）。": "CesiumLoader has no Releases yet (push a modloader-* tag to trigger the GitHub Action).",
      "FPS 必须大于零": "FPS must be greater than zero",
      "GIF 已保存：": "GIF saved: ",
      "SDK 不是有效的托管程序集。": "The SDK is not a valid managed assembly.",
      "SDK 不是有效的程序集。": "The SDK is not a valid assembly.",
      "SDK 压缩包中的文件为空或超过 16 MB。": "The file in the SDK archive is empty or larger than 16 MB.",
      "SDK 导入文件不能超过 64 MB。": "SDK import files can't exceed 64 MB.",
      "SDK 文件为空或超过 16 MB。": "The SDK file is empty or larger than 16 MB.",
      "SDK 的 PDB 符号与 DLL 不属于同一次构建。": "The SDK's PDB symbols and DLL are from different builds.",
      "SDK 符号文件过大。": "The SDK symbol file is too large.",
      "SDK 解压后的文件超过 16 MB。": "The extracted SDK file exceeds 16 MB.",
      "Steam 绕过主开关: 无 Steam 启动游戏(拦截 SteamManager.Awake)": "Steam bypass master switch: launch without Steam (intercept SteamManager.Awake)",
      "Texture2D 尺寸超出 Unity 上限（最长边 16384）。": "Texture2D size exceeds Unity's limit (longest side 16384).",
      "UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：": "Invalid UnityFS size declaration, or over 2 GiB and can't be loaded into memory: ",
      "UnityFS 头不完整：": "Incomplete UnityFS header: ",
      "UnityFS 存在无法解释的尾部数据，拒绝修复：": "UnityFS has unexplained trailing data; refusing to repair: ",
      "UnityFS 版本头无效：": "Invalid UnityFS version header: ",
      "ZIP 不允许符号链接。": "ZIP files can't contain symbolic links.",
      "ZIP 中没有 Unity .bundle 卡牌资源。": "The ZIP contains no Unity .bundle card assets.",
      "ZIP 中没有 Unity .bundle 资源。": "The ZIP contains no Unity .bundle assets.",
      "ZIP 包含未引用的文件": "The ZIP contains unreferenced files",
      "ZIP 存在重复路径": "The ZIP contains duplicate paths",
      "ZIP 存在重复路径：": "The ZIP contains duplicate paths: ",
      "ZIP 必须包含且仅包含一份 character-skin.json": "The ZIP must contain exactly one character-skin.json",
      "ZIP 文件与目录路径冲突：": "ZIP file and folder paths conflict: ",
      "ZIP 文件长度不完整：": "Incomplete ZIP file length: ",
      "ZIP 解压大小超过声明。": "The ZIP's extracted size exceeds its declaration.",
      "ZIP 路径无效：": "Invalid ZIP path: ",
      "[★ 最终首领击杀者/MVP]": "[★ Final boss killer / MVP]",
      "mod 包不能超过 64 MB。": "Mod packs can't exceed 64 MB.",
      "mod 包内文件数量超过限制。": "The mod pack contains too many files.",
      "mod 包必须是 .zip 文件。": "A mod pack must be a .zip file.",
      "mod 包解压后超过大小限制。": "The mod pack exceeds the size limit once extracted.",
      "mod 必须是 .dll 文件（模组包请用 .zip）。": "A mod must be a .dll file (use .zip for mod packs).",
      "skin.json 根节点不是对象": "The root node of skin.json isn't an object",
      "skin.json 格式无效，请先修复后再编辑方案信息": "skin.json is invalid; fix it before editing profile info",
      "version.dll 已停用或缺失，模组与配置已保留；点击恢复，缺少本地备份时会下载当前版本的加载器包。": "version.dll is disabled or missing; mods and config are kept. Click Restore — if no local backup exists, the loader package for the current version is downloaded.",
      "version.dll 已缺失；其他文件未改动，可点击恢复。": "version.dll is missing; other files are unchanged. You can click Restore.",
      "version.dll 来源不明，不能作为 CesiumLoader 停用。请先确认文件来源。": "The version.dll source is unknown, so it can't be disabled as CesiumLoader. Verify where the file came from first.",
      "version.dll 缺失且版本未知，请重新安装或更新完整加载器。": "version.dll is missing and its version is unknown. Please reinstall or update the full loader.",
      "{ // 配置说明见对应 mod 文档; 公开字段(public field)才会被读取 }": "{ // See the mod's docs for config details; only public fields are read }",
      "· 卡面转换导入": "· Card art conversion import",
      "· 转换导入": "· Conversion import",
      "。可将此皮肤包添加到人物方案。": ". You can add this skin pack to a character profile.",
      "。确认要降级后再导入（玩家配置与停用状态仍会保留）。": ". Import only after confirming the downgrade (player config and disabled state are kept).",
      "一、对局背景基本信息": "1. Match Background",
      "三、全局筹码统计摘要": "3. Global Relic Stats Summary",
      "下载内容超过大小限制。": "The download exceeds the size limit.",
      "下载包版本与当前安装版本不一致。": "The downloaded package version doesn't match the installed version.",
      "下载期间安装状态发生变化，请刷新后重试。": "The install state changed during the download; refresh and try again.",
      "下载默认包失败：": "Failed to download the default pack: ",
      "不支持或重复的视频资源：": "Unsupported or duplicate video asset: ",
      "不支持的 MonoBehaviour：": "Unsupported MonoBehaviour: ",
      "不支持的皮肤方案版本": "Unsupported skin profile version",
      "不支持的角色皮肤格式版本": "Unsupported character skin format version",
      "不支持的角色资源类型：": "Unsupported character asset type: ",
      "不是 UnityFS bundle：": "Not a UnityFS bundle: ",
      "个追加字节；原文件保存在 source/。": " appended bytes identical to the end of the directory block; the original file is saved in source/.",
      "二、参战玩家与角色战绩概况": "2. Players and Character Results",
      "五、供 AI 智能分析与复盘的建议 Prompt 方向": "5. Suggested Prompt Angles for AI Analysis and Review",
      "人物已变更，请从方案重新添加皮肤包": "The character changed; add the skin pack from the profile again",
      "人物皮肤包已创建并添加到方案": "Character skin pack created and added to the profile",
      "人物规则不存在，请刷新方案": "The character rule doesn't exist; refresh the profile",
      "人物配置为空": "Character config is empty",
      "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。": "Only version.dll was deleted; mods, config, SDK, and logs were kept.",
      "仅使用游戏内部卡面，停用外部图片。重启游戏后生效。": "Uses only the game's built-in card art; external images are disabled. Takes effect after a game restart.",
      "仅支持静态 Texture2D 替换包；包内含动画、精灵或其他资源，不能部分导入。": "Only static Texture2D replacement packs are supported; this pack contains animations, sprites, or other assets and can't be partially imported.",
      "从 Toys 导出的皮肤方案": "Skin profile exported from Toys",
      "从旧式 Unity bundle 卡牌替换包转换。原作者文件和原 bundle 保存在 source/。": "Converted from a legacy Unity bundle card replacement pack. The original author files and bundle are saved in source/.",
      "从旧式 Unity bundle 皮肤包转换。原作者说明和原文件保存在 source/。": "Converted from a legacy Unity bundle skin pack. The original author notes and files are saved in source/.",
      "任务已完成，皮肤列表刷新失败：": "Task completed, but refreshing the skin list failed: ",
      "作者最长 100 个字符": "Author can be at most 100 characters",
      "你可以直接复制以下 Prompt 向大语言模型提问：": "You can copy the prompt below and ask a large language model:",
      "保存卡面方案与原作者文件": "Save card art profile and original author files",
      "保存旧包转换结果": "Save legacy pack conversion result",
      "倍速档位的速度必须大于 0。": "Preset speed must be greater than 0.",
      "全局 / 开局阶段": "Global / Opening Phase",
      "内置角色模组缺失": "Bundled character mod is missing",
      "初始化本地界面": "Initialize local UI",
      "删除 version.dll 失败，请退出游戏并检查文件权限后重试。": "Failed to delete version.dll. Exit the game, check file permissions, and try again.",
      "加载器与变速器共用 version.dll，不能同时安装：装变速器会覆盖加载器，所有 mod 将失效。": "The loader and the Speedhack share version.dll and can't both be installed: installing the Speedhack overwrites the loader and all mods stop working.",
      "加载器发布包": "Loader release package",
      "加载器已卸载（游戏恢复正常）。": "Loader uninstalled (the game is back to normal).",
      "加载器已卸载；当前正在运行的游戏进程仍加载着加载器，重启游戏后恢复正常。": "Loader uninstalled; the running game process still has it loaded and returns to normal after a restart.",
      "加载器已安装到游戏目录。启动游戏后会自动加载 mods\\ 目录下的所有 mod": "Loader installed into the game folder. All mods in the mods\\ folder load automatically when the game starts",
      "加载器已完整卸载，请重新安装。": "The loader was fully uninstalled; please reinstall it.",
      "加载器操作正在进行，请稍候。": "A loader operation is in progress, please wait.",
      "加载器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载 mod。": "Loader files were written to the game folder. The game is running, so this won't take effect immediately — mods load after a restart.",
      "加载器目录已缺失，请使用安装功能恢复完整组件。": "The loader folder is missing; use Install to restore the full set of components.",
      "动作名无效": "Invalid action name",
      "动作名称无效或重复：": "Invalid or duplicate action name: ",
      "动作帧或帧率无效:": "Invalid action frames or frame rate:",
      "动作引用未知精灵：": "Action references an unknown sprite: ",
      "动作没有可转换的精灵帧：": "Action has no convertible sprite frames: ",
      "动态卡面导入失败：": "Failed to import animated card art: ",
      "动态卡面尺寸或总像素超过限制。": "Animated card art dimensions or total pixels exceed the limit.",
      "动态卡面已裁剪为 404×400。选择该方案并重启游戏后生效，需要新版 CardSkinMod。": "Animated card art was cropped to 404×400. Select this profile and restart the game to apply it; the new CardSkinMod is required.",
      "动态卡面最多 20 秒（300 帧），请先剪辑视频或 GIF 后重试。": "Animated card art can be at most 20 seconds (300 frames); trim the video or GIF and try again.",
      "动态卡面裁剪区域无效，请重新选择。": "The animated card art crop area is invalid; select it again.",
      "动态卡面转换失败：": "Failed to convert animated card art: ",
      "动态覆盖已移除，重启游戏后使用原有静态卡面或游戏原图。": "Animated override removed; after a restart the original static card art or game image is used.",
      "动画或技能视频属于其他角色，不能混合导入。": "The animation or skill video belongs to another character and can't be imported together.",
      "包目录与标识不一致": "Pack folder and ID don't match",
      "协议": "Protocol",
      "协议程序集不可用，无法解析结算信息。": "The protocol assembly is unavailable; can't parse settlement info.",
      "单个资源包超过 2 GiB，无法读入内存：": "A single asset bundle exceeds 2 GiB and can't be loaded into memory: ",
      "卡牌": "Card",
      "卡牌文件名不能为空": "The card file name can't be empty",
      "卡面尺寸超出 Unity 上限（最长边 16384）：": "Card art size exceeds Unity's limit (longest side 16384): ",
      "卡面必须是正方形": "Card art must be square",
      "卡面绑定名称不正确。": "The card art binding name is incorrect.",
      "即使删除成功，当前这局也已经加载了加载器，要重启游戏才会恢复正常。": "Even if deletion succeeds, this session already has the loader loaded; restart the game to return to normal.",
      "即使删除成功，当前这局也已经加载了变速器，要重启游戏才会恢复正常速度。": "Even if deletion succeeds, this session already has the Speedhack loaded; restart the game to return to normal speed.",
      "压缩包内没有任何文件。": "The archive contains no files.",
      "压缩包包含不安全或重复的路径。": "The archive contains unsafe or duplicate paths.",
      "压缩包包含不安全的相对路径。": "The archive contains an unsafe relative path.",
      "压缩包里没有找到模组：需要 <ModId>/<ModId>.dll 这样的结构，或者把 <ModId>.dll 直接放在包根目录。": "No mod found in the archive: use a structure like <ModId>/<ModId>.dll, or put <ModId>.dll directly in the archive root.",
      "原安装版本": "Original installed version",
      "原精灵名称重复：": "Duplicate original sprite name: ",
      "参数缺失": "Missing parameter",
      "发布包 DLL 哈希校验失败。": "Release package DLL hash check failed.",
      "发布包不能超过 64 MB。": "Release packages can't exceed 64 MB.",
      "发布包包含不安全或重复的路径。": "The release package contains unsafe or duplicate paths.",
      "发布包包含不安全路径、重复文件或超过大小限制。": "The release package has an unsafe path, duplicate files, or exceeds the size limit.",
      "发布包必须包含唯一的 CesiumLoader.SDK.dll（SDK 工具包或加载器发布包）。": "The release package must contain exactly one CesiumLoader.SDK.dll (SDK toolkit or loader release package).",
      "发布包文件超过大小限制。": "A file in the release package exceeds the size limit.",
      "发布包未提供 version.dll 的有效哈希。": "The release package has no valid hash for version.dll.",
      "发布包缺少 version.dll。": "The release package is missing version.dll.",
      "发布包缺少清单（cesium-loader.json），已停止安装。": "The release package is missing its manifest (cesium-loader.json); installation stopped.",
      "发布包缺少版本清单。": "The release package has no version manifest.",
      "发布包解压大小或文件数量超过限制。": "The release package's extracted size or file count exceeds the limit.",
      "发布包里没有任何 mod（AstralParty_ModLoader/mods/ 为空），已停止安装。": "The release package has no mods (AstralParty_ModLoader/mods/ is empty); installation stopped.",
      "变速器已卸载（游戏恢复正常速度）。": "Speedhack uninstalled (the game is back to normal speed).",
      "变速器已卸载；当前正在运行的游戏进程仍加载着变速器，重启游戏后恢复正常速度。": "Speedhack uninstalled; the running game process still has it loaded and returns to normal speed after a restart.",
      "变速器已安装到游戏目录。进入游戏后按配置的快捷键即可变速（需关闭垂直同步）。": "Speedhack installed into the game folder. Press the configured hotkey in game to change speed (V-Sync must be off).",
      "变速器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载变速器。": "Speedhack files were written to the game folder. The game is running, so this won't take effect immediately — the Speedhack loads after a restart.",
      "可在「模组 → 加载器设置」里打开它）": "you can enable it under Mods → Loader Settings)",
      "可在「模组」页一键安装加载器，或改选「从 Steam 启动」）": "install the loader from the Mods page with one click, or switch to Launch via Steam)",
      "可随时点击恢复 version.dll。": "You can click Restore version.dll at any time.",
      "吉星派对": "Astral Party",
      "同名方案已存在": "A profile with the same name already exists",
      "同名方案已存在，请修改包的 Id 或先删除原方案": "A profile with the same name already exists; change the pack's ID or delete the original profile first",
      "同名自定义包已存在:": "A custom pack with the same name already exists:",
      "向当前方案添加人物皮肤包": "Add character skin pack to current profile",
      "四、按回合详细筹码流转与决策时间线 (Chronological Relic Timeline)": "4. Per-Round Relic Flow and Decision Timeline (Chronological Relic Timeline)",
      "回放库目录与游戏回放目录不能是同一个路径，操作已拒绝。请先在设置里修改库目录。": "The replay library and the game replay folder can't be the same path; the operation was rejected. Change the library folder in Settings first.",
      "回放库目录与游戏回放目录被设置成了同一个路径，已停用所有归档操作。请在设置里改成别的目录。": "The replay library and game replay folder are set to the same path, so all archiving is disabled. Point the library somewhere else in Settings.",
      "图片不存在:": "Image not found:",
      "图片必须是 PNG/JPG，GIF 请先转换为帧序列": "Images must be PNG/JPG; convert GIFs to a frame sequence first",
      "图片槽位无效": "Invalid image slot",
      "基于应用内置默认包创建": "Created from the app's built-in default pack",
      "基础倍速必须大于 0。": "Base speed must be greater than 0.",
      "备份哈希无效。": "Invalid backup hash.",
      "复制后校验失败，目标文件已清理，源文件保持不动。": "Verification failed after copying; the destination file was cleaned up and the source left untouched.",
      "多人物方案已保存。完全退出并重启游戏后生效。": "Multi-character profile saved. Fully exit and restart the game to apply it.",
      "大厅匹配绕过: 修「点创建/加入房间没反应」": "Lobby matchmaking bypass: fixes Create/Join Room doing nothing",
      "如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。": "If the game can't read it either, don't restore it to the game folder — the game deletes the whole folder when it lists it.",
      "如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。": "If the game can't read it either, it deletes the whole folder when it lists this replay. Confirm the restore once more if you want to proceed.",
      "如确定要覆盖，请勾选「允许覆盖其它 version.dll」。": "If you're sure you want to overwrite, tick \"Allow overwriting other version.dll files\".",
      "如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。": "If you really want to downgrade, tick \"Allow installing an older loader\"; usually it's better to use a newer version of AstralParty.Toys.",
      "媒体文件不存在:": "Media file not found:",
      "存在重复卡牌资源名：": "Duplicate card asset name: ",
      "安装文件包含符号链接，无法可靠备份。": "The install files contain symbolic links and can't be reliably backed up.",
      "定位安装目录和热更新缓存资源": "Locate the install folder and hot-update cache assets",
      "对局": "Match",
      "对应版本的在线发布包": "Online release package for that version",
      "导入多人物皮肤方案": "Import multi-character skin profile",
      "导入旧式卡牌皮肤包失败：": "Failed to import legacy card skin pack: ",
      "导入旧式卡牌皮肤包（自动转换 bundle）": "Import legacy card skin pack (auto-convert bundles)",
      "导入旧式角色皮肤包（自动转换 bundle）": "Import legacy character skin pack (auto-convert bundles)",
      "导入角色皮肤包": "Import character skin pack",
      "导出商店时间线表格 (CSV)": "Export shop timeline table (CSV)",
      "导出回放完整数据": "Export full replay data",
      "导出失败：": "Export failed: ",
      "导出当前动作为 GIF": "Export current action as GIF",
      "导出方案及自定义皮肤包": "Export profile and custom skin packs",
      "导出来源皮肤无效": "The export source skin is invalid",
      "导出皮肤方案 ZIP": "Export skin profile ZIP",
      "导出确认已过期，请重新导出。": "The export confirmation expired; export again.",
      "导出筹码时间线 (自然语言 AI 文本)": "Export relic timeline (natural-language AI text)",
      "导出筹码时间线数据 (JSON)": "Export relic timeline data (JSON)",
      "导出筹码时间线表格 (CSV)": "Export relic timeline table (CSV)",
      "导出角色皮肤包": "Export character skin pack",
      "尚未安装：点「安装加载器」，把加载器和内置模组装进游戏目录。": "Not installed yet: click Install Loader to put the loader and built-in mods into the game folder.",
      "尚未安装：点击「安装变速器」把文件复制到游戏目录。": "Not installed yet: click Install Speedhack to copy the files into the game folder.",
      "尚未找到游戏目录：可点击「选择游戏目录」手动指定安装位置。": "Game folder not found yet: click Choose Game Folder to set the install location manually.",
      "尚未找到游戏目录：点「自动检测」，或手动选择游戏 exe 所在的文件夹。": "Game folder not found yet: click Auto-Detect, or select the folder containing the game exe manually.",
      "尚未选择有效的游戏目录，请先自动检测或手动选择。": "No valid game folder selected; auto-detect or select one manually first.",
      "已交给 Steam 启动 Astral Party，稍等一下。": "Handed off to Steam to launch Astral Party; give it a moment.",
      "已从应用内置默认包恢复 default，重启游戏后生效。": "Restored default from the app's built-in default pack; takes effect after a game restart.",
      "已保存加载器设置（重启游戏生效）": "Loader settings saved (takes effect after a game restart)",
      "已切换到从 Steam 启动：加载器的 Steam 绕过已整套关闭，Steam 大厅联机恢复正常（重启游戏生效）": "Switched to launching via Steam: the loader's Steam bypass is fully off and Steam lobby multiplayer works again (takes effect after a game restart)",
      "已切换到绕过 Steam：加载器的 Steam 绕过已打开（重启游戏生效）": "Switched to bypassing Steam: the loader's Steam bypass is on (takes effect after a game restart)",
      "已加入后台卡面转换队列，右下角可查看进度或终止。": "Added to the background card art conversion queue; check progress or stop it in the bottom-right.",
      "已加入后台导出队列": "Added to the background export queue",
      "已加入后台导出队列，完成后自动添加到方案": "Added to the background export queue; it's added to the profile automatically when done",
      "已加入后台转换队列，右下角可查看进度或终止。": "Added to the background conversion queue; check progress or stop it in the bottom-right.",
      "已取消": "Canceled",
      "已取消深度扫描。": "Deep scan canceled.",
      "已取消读取，原卡面保留。": "Read canceled; the original card art is kept.",
      "已取消转换，原卡面保留。": "Conversion canceled; the original card art is kept.",
      "已恢复默认模板（Ctrl 切换 2 倍速）。": "Default template restored (Ctrl toggles 2× speed).",
      "已按请求的 ID 入库，需要的话可以改名后再放回游戏。": "Archived under the requested ID; rename it before restoring to the game if needed.",
      "已有卡面正在处理，请等待或取消。": "Card art is already being processed; wait or cancel it.",
      "已有卡面正在转换。": "Card art is already being converted.",
      "已有发布包正在导入，请稍候。": "A release package is already being imported, please wait.",
      "已禁用变速（恢复 1.0 正常速度，重启游戏生效）": "Speedhack disabled (back to normal 1.0 speed; takes effect after a game restart)",
      "已终止任务，临时文件已清理": "Task stopped; temporary files cleaned up",
      "已经有一个回放在下载了，等它结束再试。": "A replay is already downloading; wait for it to finish and try again.",
      "已绕过 Steam，直接启动游戏主程序。": "Bypassed Steam and launched the game executable directly.",
      "已记住这个选择，但没能同步到加载器配置（还没装加载器？在「模组」页安装一次就会同步）。": "Choice saved, but it couldn't be synced to the loader config (loader not installed yet? install it once on the Mods page to sync).",
      "帧号,回合,玩家ID,玩家名称,玩家角色,商店类型,候选卡牌,售价,折扣,免费卡,购买结果,已售出槽位,是否关闭": "Frame,Round,Player ID,Player Name,Player Character,Shop Type,Candidate Cards,Price,Discount,Free Card,Purchase Result,Sold Slots,Closed",
      "帧号,回合,玩家ID,玩家名称,玩家角色,操作类型,档位,筹码ID,筹码名称,品质,来源,刷新状态,候选选项": "Frame,Round,Player ID,Player Name,Player Character,Action Type,Preset,Relic ID,Relic Name,Quality,Source,Reroll Status,Candidate Options",
      "帧序列必须是 PNG/JPG": "Frame sequences must be PNG/JPG",
      "帧时长必须在 0～655350 ms 之间": "Frame duration must be between 0 and 655350 ms",
      "帧时长无效": "Invalid frame duration",
      "开始处理": "Start Processing",
      "当前加载器版本未知或格式无效，无法下载对应发布包。请重新安装或更新加载器。": "The current loader version is unknown or malformed, so the matching release package can't be downloaded. Please reinstall or update the loader.",
      "当前动作没有动画帧": "This action has no animation frames",
      "当前动作过大，请缩小帧尺寸或减少帧数后导出 GIF": "This action is too large; reduce the frame size or count before exporting a GIF",
      "当前游戏已切换，请重新导出。": "The current game changed; export again.",
      "当前游戏已切换，请重新添加人物皮肤包": "The current game changed; add the character skin pack again",
      "当前游戏已切换，请重新选择图片。": "The current game changed; select the image again.",
      "当前游戏已切换，请重新选择素材": "The current game changed; select the asset again",
      "当前游戏已切换，请重新选择视频。": "The current game changed; select the video again.",
      "当前游戏进程仍加载着加载器，下次启动才停用。": "The running game process still has the loader loaded; it's disabled on the next launch.",
      "恢复默认包失败：": "Failed to restore the default pack: ",
      "所选目录里没有找到游戏主程序（AstralParty*.exe），确认这是游戏 exe 所在的目录？": "No game executable (AstralParty*.exe) found in the selected folder. Is this the folder containing the game exe?",
      "找不到 WebUI/index.html（内嵌资源里也没有）。": "Can't find WebUI/index.html (it isn't in the embedded resources either).",
      "找不到要导入的 mod 包。": "The mod pack to import wasn't found.",
      "找不到要导入的 mod 文件。": "The mod file to import wasn't found.",
      "技能视频不是可识别的 CRI USM：": "The skill video isn't a recognizable CRI USM: ",
      "挂接延迟不能为负数。": "Hook delay can't be negative.",
      "按配置中的完整资源名匹配目标皮肤": "Match the target skin by the full asset name in the config",
      "提交完整卡牌方案": "Commit full card profile",
      "提示：CesiumLoader 已内置变速引擎——请勿安装本变速器，直接改加载器的": "Tip: CesiumLoader already includes a Speedhack engine — don't install this standalone speedhack; just change the loader's",
      "整个模组包（.zip）请用 ImportModPackage 导入，这里只接收单个 DLL。": "Import whole mod packs (.zip) with ImportModPackage; this only accepts a single DLL.",
      "文案槽位或长度无效:": "Invalid text slot or length:",
      "新方案标识不能为空": "The new profile ID can't be empty",
      "新方案标识包含非法字符": "The new profile ID contains invalid characters",
      "方案B 备用安全网 —— 实测开启会导致启动崩溃, 必须保持 false": "Plan B fallback safety net — enabling it crashes the game at startup in testing; must stay false",
      "方案C 调用方式: auto / direct / invoke": "Plan C invocation: auto / direct / invoke",
      "方案为空": "Profile is empty",
      "方案信息已保存。": "Profile info saved.",
      "方案共享图片冲突:": "Profile shared image conflict:",
      "方案共享文案冲突:": "Profile shared text conflict:",
      "方案名不能为空": "Profile name can't be empty",
      "方案名称不正确。": "The profile name is incorrect.",
      "方案名称不能为空": "Profile name can't be empty",
      "方案名称最长 100 个字符": "Profile name can be at most 100 characters",
      "方案存在空字段": "The profile has empty fields",
      "方案已存在": "The profile already exists",
      "方案描述最长 2000 个字符": "Profile description can be at most 2000 characters",
      "方案操作失败：": "Profile operation failed: ",
      "方案显示名称不能为空": "Profile display name can't be empty",
      "方案显示名称最长 100 个字符": "Profile display name can be at most 100 characters",
      "方案标识与保存目标不一致": "The profile ID doesn't match the save target",
      "方案标识仅支持字母、数字、下划线和短横线，最长 80 字符。": "Profile IDs may contain only letters, digits, underscores, and hyphens, up to 80 characters.",
      "方案目录与标识不一致": "Profile folder and ID don't match",
      "方案简介最长 2000 个字符": "Profile summary can be at most 2000 characters",
      "方案缺少自定义包:": "Profile is missing a custom pack:",
      "无效的 PNG 帧。": "Invalid PNG frame.",
      "无法保存加载器版本清单，本次安装未完整完成。": "Couldn't save the loader version manifest; this installation didn't complete fully.",
      "无法启动 FFmpeg。": "Couldn't start FFmpeg.",
      "无法完整读取卡牌资源包": "Couldn't read the card asset bundle completely",
      "无法完整读取旧资源包": "Couldn't read the legacy asset bundle completely",
      "无法确定回放 ID。": "Couldn't determine the replay ID.",
      "无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。": "FFmpeg is unavailable. Put ffmpeg.exe in a tools folder next to the app, or set ASTRAL_FFMPEG_PATH.",
      "无法设置首次安装的 Steam 启动方式，加载器配置不完整。": "Couldn't set the Steam launch method for the first install; the loader config is incomplete.",
      "无法读取 bundle 内的资源文件：": "Couldn't read the asset files inside the bundle: ",
      "无法读取视频首帧：": "Couldn't read the video's first frame: ",
      "旧版启用皮肤": "Legacy enabled skin",
      "最新 Release 尚未提供默认皮肤包，请先发布包含 cesium-default-skins.zip 的版本。": "The latest Release doesn't provide a default skin pack yet; publish a release containing cesium-default-skins.zip first.",
      "未刷新": "No reroll",
      "未刷新，直接从初始候选选择": "No reroll; picked directly from the initial candidates",
      "未知动画附属 MonoBehaviour，不能部分导入：": "Unknown animation-attached MonoBehaviour; can't import partially: ",
      "未知图片槽位:": "Unknown image slot:",
      "未知消息": "Unknown message",
      "未知玩家": "Unknown player",
      "未知的方案操作": "Unknown profile operation",
      "未知视频实现：": "Unknown video implementation: ",
      "未购买，关闭商店": "No purchase; shop closed",
      "本地备份": "Local backup",
      "本地备份校验失败，已停止操作。": "Local backup verification failed; the operation stopped.",
      "来源未记录": "Source not recorded",
      "标识必须是 1～80 个字母、数字、下划线或短横线": "The ID must be 1–80 letters, digits, underscores, or hyphens",
      "校验 PNG 和皮肤清单": "Validate PNGs and the skin manifest",
      "校验并保存模板": "Validate and save template",
      "检查旧式卡牌皮肤 ZIP": "Inspect legacy card skin ZIP",
      "检查旧皮肤 ZIP 的路径与可用空间": "Check paths and free space for the legacy skin ZIP",
      "检测到已安装 CesiumLoader Mod 加载器（AstralParty_ModLoader\\doorstop_config.json 存在）。": "CesiumLoader Mod loader detected (AstralParty_ModLoader\\doorstop_config.json found).",
      "检测到游戏正在运行——安装或卸载前请先退出游戏。": "The game is running — exit it before installing or uninstalling.",
      "模组包比游戏目录里已装的更旧：": "The mod pack is older than what's installed in the game folder: ",
      "正在下载最新 CesiumLoader 并安装…": "Downloading and installing the latest CesiumLoader…",
      "正在准备协议和配置…": "Preparing protocol and config…",
      "正在导入模组，请稍候。": "Importing mods, please wait.",
      "正在导出商店时间线…": "Exporting shop timeline…",
      "正在导出完整协议数据…": "Exporting full protocol data…",
      "正在导出筹码时间线…": "Exporting relic timeline…",
      "正在扫描 Steam 库、注册表与常见安装位置…": "Scanning Steam libraries, the registry, and common install locations…",
      "正在终止并清理临时文件…": "Stopping and cleaning up temporary files…",
      "正在读取视频首帧，准备裁剪…": "Reading the video's first frame for cropping…",
      "正在转换动态卡面（15 帧/秒，最多 20 秒）…": "Converting animated card art (15 fps, up to 20 seconds)…",
      "此人物的相同原皮肤已在方案中，请编辑已有的人物皮肤": "This character's same original skin is already in the profile; edit the existing character skin",
      "每帧时长数量与帧数不一致": "The number of frame durations doesn't match the frame count",
      "没有可删除的回放。": "No replays to delete.",
      "没能启动游戏：没有找到游戏主程序。请先在「变速器」页选择游戏目录，或确认游戏已安装。": "Couldn't launch the game: no game executable found. Select the game folder on the Speedhack page first, or make sure the game is installed.",
      "清单为空": "Manifest is empty",
      "游戏文件操作正在进行（安装 / 更新 / 导入），请等待完成后再修改。": "A game file operation is in progress (install / update / import); wait for it to finish before making changes.",
      "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再安装。": "version.dll is locked while the game is running and can't be overwritten — fully exit the game before installing.",
      "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再更新。": "version.dll is locked while the game is running and can't be overwritten — fully exit the game before updating.",
      "游戏正在运行时也能卸：version.dll 已被游戏加载占用，删除通常会失败并提示文件被占用；": "Uninstalling also works while the game is running: version.dll is loaded and locked by the game, so deletion usually fails with a file-in-use error;",
      "游戏正在运行时也能同步：配置会写进游戏目录，已加载的变速器需要按重载热键（默认 Ctrl+Shift+R）重新读取，否则重启游戏后生效。": "Syncing also works while the game is running: the config is written to the game folder, and the loaded Speedhack must reload it with the reload hotkey (Ctrl+Shift+R by default); otherwise it applies after a game restart.",
      "游戏正在运行时也能装：version.dll 是游戏启动时加载的，本次写进去要重启游戏才会生效；": "Installing also works while the game is running: version.dll loads at game startup, so what's written now takes effect after a restart;",
      "游戏正在运行时也能装：version.dll 由游戏启动时加载，本次写进去要重启游戏才会生效；": "Installing also works while the game is running: version.dll is loaded by the game at startup, so what's written now takes effect after a restart;",
      "游戏正在运行时该文件已被加载占用，请完全退出游戏后再卸载（本次没有改动任何文件）。": "That file is loaded and locked while the game is running; fully exit the game before uninstalling (no files were changed this time).",
      "游戏正在运行，重启游戏后生效。": "The game is running; takes effect after a game restart.",
      "游戏正在运行：安装、更新、停用或恢复要重启游戏才生效，version.dll 被占用时请完全退出游戏再操作。": "The game is running: installs, updates, disabling, and restores take effect after a restart; if version.dll is locked, fully exit the game first.",
      "游戏正在运行：本次写入要重启游戏后才会加载。": "The game is running: what's written now loads after a restart.",
      "游戏目录不存在，请先安装加载器或选择游戏目录。": "The game folder doesn't exist; install the loader or select the game folder first.",
      "游戏目录中没有加载器文件，无需卸载。": "There are no loader files in the game folder; nothing to uninstall.",
      "游戏目录中没有变速器文件，无需卸载。": "There are no Speedhack files in the game folder; nothing to uninstall.",
      "游戏目录存在其它 version.dll（与内置文件不同）——覆盖或卸载前请先确认来源。": "The game folder has a different version.dll (not the bundled one) — verify its source before overwriting or uninstalling.",
      "游戏目录尚未安装变速器，请先安装。": "The Speedhack isn't installed in the game folder yet; install it first.",
      "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的，如 CesiumLoader Mod 加载器）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。": "The game folder already has a version.dll that differs from the bundled one (possibly from another tool, such as the CesiumLoader Mod loader) — if you're sure you want to overwrite it, tick \"Allow overwriting other version.dll files\".",
      "游戏目录已有 version.dll，未覆盖任何文件。请刷新状态并确认来源。": "The game folder already has a version.dll; no files were overwritten. Refresh the status and verify its source.",
      "游戏目录或 mod 文件名无效。": "The game folder or mod file name is invalid.",
      "游戏目录里没有可归档的回放。": "There are no replays to archive in the game folder.",
      "游戏目录里的 version.dll 不是本工具装的（来源不明）——可能是旧版独立变速器或第三方文件，安装或卸载前请先确认。": "The version.dll in the game folder wasn't installed by this tool (unknown source) — it may be the legacy standalone speedhack or a third-party file; verify before installing or uninstalling.",
      "游戏目录里的 version.dll 不是本工具装的（来源不明）——未删除任何文件。如确认要删除请勾选「强制卸载」。": "The version.dll in the game folder wasn't installed by this tool (unknown source) — no files were deleted. If you're sure, tick Force Uninstall.",
      "游戏目录里的 version.dll 不是本工具装的（来源不明，可能是旧版独立变速器或第三方文件）——": "The version.dll in the game folder wasn't installed by this tool (unknown source; it may be the legacy standalone speedhack or a third-party file) —",
      "游戏配置没有可匹配的角色皮肤。": "The game config has no matching character skin.",
      "游戏默认": "Game default",
      "游戏默认方案不可修改": "The game's default profile can't be modified",
      "游戏默认方案无需恢复外部卡面。": "The game's default profile has no external card art to restore.",
      "游戏默认方案没有外部图片可导出。": "The game's default profile has no external images to export.",
      "源图片文件不存在": "The source image file doesn't exist",
      "物品": "Item",
      "皮肤不属于当前角色:": "The skin doesn't belong to the current character:",
      "皮肤动画总帧数超过 10000": "The skin's total animation frames exceed 10000",
      "皮肤包已存在": "The skin pack already exists",
      "皮肤压缩包不存在": "The skin archive doesn't exist",
      "皮肤方案不存在:": "Skin profile doesn't exist:",
      "皮肤方案名称不能为空": "Skin profile name can't be empty",
      "皮肤方案已导出为 ZIP，可通过导入 ZIP 使用。": "The skin profile was exported as a ZIP; use Import ZIP to use it.",
      "皮肤方案目录不存在。": "The skin profile folder doesn't exist.",
      "皮肤清单存在空字段": "The skin manifest has empty fields",
      "相同角色和目标皮肤存在重复规则": "Duplicate rules exist for the same character and target skin",
      "确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。": "To switch to the CDN copy, delete the old library copy first (or use a different ID).",
      "移除动态卡面失败：": "Failed to remove animated card art: ",
      "程序集名称不是 CesiumLoader.SDK。": "The assembly name isn't CesiumLoader.SDK.",
      "程序集缺少内置加载器资源（ModLoader.version.dll），请重新编译发布版本。": "The assembly is missing the bundled loader asset (ModLoader.version.dll); rebuild the release build.",
      "程序集缺少内置加载器资源（version.dll），无法安装。": "The assembly is missing the bundled loader asset (version.dll); can't install.",
      "程序集缺少内置变速器资源（SpeedhackTools.version.dll / speedhack_config.json），请重新编译发布版本。": "The assembly is missing bundled Speedhack assets (SpeedhackTools.version.dll / speedhack_config.json); rebuild the release build.",
      "程序集缺少内置变速器资源（version.dll / speedhack_config.json），无法安装。": "The assembly is missing bundled Speedhack assets (version.dll / speedhack_config.json); can't install.",
      "程序集缺少内置配置模板资源，无法恢复默认。": "The assembly is missing the bundled config template asset; can't restore defaults.",
      "空白皮肤包已创建": "Blank skin pack created",
      "等待处理": "Waiting",
      "精灵名称无效或重复：": "Invalid or duplicate sprite name: ",
      "精灵帧没有完整动作信息。": "The sprite frame has incomplete action info.",
      "纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：": "Texture size exceeds Unity's limit (longest side 16384) or the decoded data is incomplete: ",
      "纹理或动画帧尺寸超出 Unity 上限（最长边 16384）。": "Texture or animation frame size exceeds Unity's limit (longest side 16384).",
      "纹理资源名为空或重复：": "Texture asset name is empty or duplicated: ",
      "绑定规则必须指定自定义包": "A binding rule must specify a custom pack",
      "继续保存新回放会失败。播放不受影响，建议归档几局。": "Saving new replays will keep failing. Playback is unaffected; consider archiving a few matches.",
      "编码并保存 GIF": "Encode and save GIF",
      "而且正在运行的游戏往往占用着 version.dll，覆盖会失败并提示文件被占用——那样就先完全退出游戏再装。": "and a running game usually holds version.dll, so overwriting fails with a file-in-use error — in that case, fully exit the game and install again.",
      "自动检测未找到游戏目录，请点「选择…」手动指定。": "Auto-detect didn't find the game folder; click Choose… to set it manually.",
      "自定义包与规则角色不匹配": "The custom pack doesn't match the rule's character",
      "自定义包已导入但绑定失败，回滚未完成；包可能被其他方案引用，已保留包。": "The custom pack was imported but binding failed and the rollback didn't complete; the pack may be referenced by other profiles, so it was kept.",
      "自定义包被皮肤方案引用，请先移除方案规则:": "The custom pack is referenced by a skin profile; remove the profile rule first:",
      "自定义包角色与规则不匹配:": "The custom pack's character doesn't match the rule:",
      "裁剪区域必须是图片范围内的正方形": "The crop area must be a square within the image bounds",
      "裁剪卡面已保存，重启游戏后生效。": "Cropped card art saved; takes effect after a game restart.",
      "裁剪已取消或过期，请重新选择图片。": "The crop was canceled or expired; select the image again.",
      "裁剪已取消或过期，请重新选择视频。": "The crop was canceled or expired; select the video again.",
      "裁剪已过期，请重新选择图片": "The crop expired; select the image again",
      "裁剪范围或输出尺寸无效": "Invalid crop area or output size",
      "规则为空": "Rule is empty",
      "规则标识重复:": "Duplicate rule ID:",
      "规则至少需要一种资源来源": "A rule needs at least one asset source",
      "规则角色或皮肤 ID 无效": "Invalid rule character or skin ID",
      "视频资源引用结构无效：": "Invalid video asset reference structure: ",
      "视频转换产生不一致的帧尺寸。": "The video conversion produced inconsistent frame sizes.",
      "视频转换失败：": "Video conversion failed: ",
      "角色不存在:": "Character doesn't exist:",
      "角色或原皮肤 ID 无效": "Invalid character or original skin ID",
      "角色皮肤已保存。游戏内修改在完全退出并重启后生效。": "Character skin saved. In-game changes take effect after fully exiting and restarting the game.",
      "角色皮肤操作失败：": "Character skin operation failed: ",
      "解析不出结算帧（cmd 1016），文件可能已损坏或来自不兼容的版本。": "Couldn't parse a settlement frame (cmd 1016); the file may be corrupted or from an incompatible version.",
      "设置已保存。": "Settings saved.",
      "该 version.dll 与内置文件不同，可能不是本工具安装的——未删除任何文件。如确认要删除请勾选「强制卸载」。": "This version.dll differs from the bundled one and may not have been installed by this tool — no files were deleted. If you're sure, tick Force Uninstall.",
      "该帧已发生变化，请重新选择替换": "That frame has changed; select a replacement again",
      "该方案已经在导出队列中": "That profile is already in the export queue",
      "该方案没有可导出的卡面。": "That profile has no card art to export.",
      "请先在模组页安装 CesiumLoader。": "Install CesiumLoader on the Mods page first.",
      "请先安装加载器，再导入 SDK。": "Install the loader before importing the SDK.",
      "请先等待或取消当前动态卡面转换。": "Wait for or cancel the current animated card art conversion first.",
      "请先等待或取消当前动态卡面转换，再修改或导出皮肤方案。": "Wait for or cancel the current animated card art conversion before editing or exporting skin profiles.",
      "请先等待或取消当前转换。": "Wait for or cancel the current conversion first.",
      "请先选择外部皮肤方案。": "Select an external skin profile first.",
      "请先选择游戏目录。": "Select the game folder first.",
      "请填写回放 ID。": "Enter a replay ID.",
      "请填写存在的发布包完整路径。": "Enter the full path of an existing release package.",
      "请将适合本机的 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH 后重试。": "Put a suitable ffmpeg.exe in a tools folder next to the app, or set ASTRAL_FFMPEG_PATH and try again.",
      "请检查文件权限后重试（本次没有改动任何文件）。": "Check file permissions and try again (no files were changed this time).",
      "请确认回放 ID 与区服是否对应。": "Check that the replay ID matches the region.",
      "请选择 1～2000 张图片": "Select 1–2000 images",
      "请选择 PNG/JPG 图片": "Select a PNG/JPG image",
      "请选择 SDK 发布 ZIP 或 CesiumLoader.SDK.dll。": "Select an SDK release ZIP or CesiumLoader.SDK.dll.",
      "请选择有效的游戏主程序目录。": "Select a valid game executable folder.",
      "请选择视频或 GIF 文件。": "Select a video or GIF file.",
      "读取动画顺序与播放参数": "Read animation order and playback settings",
      "读取游戏档案，首次使用时检测 Steam 游戏目录": "Load game profile; detect Steam game folders on first use",
      "读取角色与皮肤配置": "Read character and skin config",
      "资源不是支持的卡牌贴图，拒绝部分导入：": "The asset isn't a supported card texture; partial import refused: ",
      "资源包没有可转换的 Texture2D：": "The asset bundle has no convertible Texture2D: ",
      "资源包没有可转换的图片、动作或已知技能视频。": "The asset bundle has no convertible images, actions, or known skill videos.",
      "资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：": "The assets match multiple characters or skins and can't be identified automatically; use a skin pack with a clear target key art. Matches: ",
      "资源无法对应一个角色皮肤，或包中混有多个目标的资源。": "The assets don't map to a single character skin, or the pack mixes assets from several targets.",
      "资源路径包含无效目录": "The asset path contains an invalid folder",
      "资源路径必须位于皮肤包内": "The asset path must be inside the skin pack",
      "资源路径越界": "The asset path escapes the pack",
      "转换工具下载超过 256 MB。": "The conversion tool download exceeded 256 MB.",
      "转换工具体积不符合预期。": "The conversion tool's size doesn't match expectations.",
      "这个回放找不到了（可能已被移动或删除），点「刷新列表」重新扫描。": "That replay can't be found (it may have been moved or deleted); click Refresh List to scan again.",
      "这个文件不是能解出结算帧的回放，已拒绝导入。": "This file isn't a replay with a parseable settlement frame; the import was rejected.",
      "这个目录已经不在了。": "That folder no longer exists.",
      "这个目录已经不在了，或里面没有 AstralParty*.exe。": "That folder no longer exists, or it doesn't contain AstralParty*.exe.",
      "这个目录里没有检测到游戏主程序。请确认所选目录包含游戏 exe、UnityPlayer.dll 和 *_Data 文件夹。": "No game executable detected in that folder. Make sure the selected folder contains the game exe, UnityPlayer.dll, and the *_Data folder.",
      "这是 SDK 包，请用「导入发布包」更新 SDK。": "This is an SDK package; use Import Release Package to update the SDK.",
      "这是 Toys 皮肤包，请使用标准皮肤导入。": "This is a Toys skin pack; use the standard skin import.",
      "这是加载器发布包（包含 version.dll），请用「导入发布包」安装。": "This is a loader release package (containing version.dll); use Import Release Package to install it.",
      "这是模组包（含 <ModId>/<ModId>.dll），请用「导入模组」安装；「导入发布包」只用于加载器与 SDK。": "This is a mod pack (containing <ModId>/<ModId>.dll); use Import Mod to install it. Import Release Package is only for the loader and SDK.",
      "退房兜底: 修退房/被踢时 await 之后的 NRE": "Leave-room fallback: fixes the NRE after await when leaving or being kicked",
      "选中的动画帧不存在": "The selected animation frame doesn't exist",
      "选择 CesiumLoader 发布包": "Select CesiumLoader release package",
      "选择动画帧（按文件名排序）": "Select animation frames (sorted by file name)",
      "选择吉星派对回放文件": "Select Astral Party replay file",
      "选择吉星派对游戏目录（AstralParty exe 所在文件夹）": "Select the Astral Party game folder (the folder containing AstralParty.exe)",
      "选择回放库目录（不要和游戏的 Temp\\Replay 选成同一个）": "Select the replay library folder (don't pick the game's Temp\\Replay)",
      "选择的角色皮肤与包内资源的准确配置匹配结果不符。": "The selected character skin doesn't match the exact config match for the pack's assets.",
      "选择皮肤压缩包 (支持 cesium-default-skins 或自定义皮肤 ZIP)": "Select skin archive (cesium-default-skins or a custom skin ZIP)",
      "选择目标必须同时指定角色与皮肤 ItemID。": "The selection target must specify both the character and the skin ItemID.",
      "选择要导入回放库的回放文件": "Select a replay file to import into the replay library",
      "选择要导入的模组（DLL 或 ZIP 模组包）": "Select the mod to import (DLL or ZIP mod pack)",
      "选择视频 / GIF，下一步裁剪普通卡面（404×400）": "Select a video / GIF; next, crop the normal card face (404×400)",
      "选择角色图片": "Select character image",
      "部分精灵没有被动作引用，不能部分导入。": "Some sprites aren't referenced by any action, so partial import isn't possible.",
      "配置内容为空，无法保存。": "The config is empty and can't be saved.",
      "配置已保存到本地主配置，安装变速器时会一并复制到游戏目录。": "Config saved to the local main config; it's copied into the game folder when the Speedhack is installed.",
      "配置已同步到游戏目录，重启游戏后生效。": "Config synced to the game folder; takes effect after a game restart.",
      "配置文件不是有效的 JSON，请先用「用编辑器打开」修复，或点「恢复默认」。": "The config file isn't valid JSON. Fix it with Open in Editor first, or click Restore Defaults.",
      "锚点、缩放或 PPU 超出允许范围": "Anchor, scale, or PPU is out of the allowed range",
      "阶段3(实测作废、hook 从未命中; 保留无害)": "Stage 3 (obsolete in testing; the hook never fires; harmless to keep)",
      "附加保险: 让 SteamAPI_RestartAppIfNecessary 恒返回 0": "Extra safeguard: make SteamAPI_RestartAppIfNecessary always return 0",
      "首次使用：正在下载 FFmpeg 转换工具…": "First use: downloading the FFmpeg conversion tool…",
      "默认包中没有卡面图片": "The default pack contains no card art images",
      "默认包内容不符合预期": "The default pack's contents aren't as expected",
      "默认包解压体积过大": "The default pack's extracted size is too large",
      "默认方案 (default) 禁止删除。": "The default profile (default) can't be deleted.",
      "默认皮肤包已下载并安装，可选择 default 方案使用。": "The default skin pack was downloaded and installed; select the default profile to use it.",
      "默认皮肤包超过 256 MB": "The default skin pack exceeds 256 MB",
      "（提示：Steam 没在运行，加载器的「Steam 绕过」是关的，游戏可能会在启动早期自己退出 ——": "(Tip: Steam isn't running and the loader's Steam bypass is off, so the game may exit early during startup —",
      "（提示：Steam 没在运行，而且还没安装加载器 —— 缺了它的 Steam 绕过，游戏会在启动早期自己退出。": "(Tip: Steam isn't running and the loader isn't installed — without its Steam bypass, the game exits early during startup.",
      "（这个版本没有填写发布说明。）": "(This version has no release notes.)",
      "，已添加到方案": ", added to the profile",
      "，并在控制台窗口显示日志。": ", and show logs in the console window.",
      "：移除与目录块尾部完全相同的": ": removed the ",
      "；缺失：": "; missing: "
    },
    "ja": {
      "—— 点「检查更新」看看 GitHub 上有没有新版。": "—— 「更新を確認」を押して、GitHub に新しいバージョンがないか見てみましょう。",
      "—— 同时安装会互相覆盖，游戏只会加载其中一个。": "—— 同時にインストールすると互いに上書きされ、ゲームはどちらか一方しか読み込みません。",
      "——安装加载器、变速、导入模组都作用于当前这个。": "—— ローダーのインストール、変速、Mod のインポートは、すべて現在のこのディレクトリに適用されます。",
      "——这是客户端写死的上限，和服务器无关：超了只是游戏拒绝保存新回放。把回放归档到下面的库目录即可腾出席位，需要时再一键放回游戏。": "—— これはクライアント側で固定された上限で、サーバーとは関係ありません。超えるとゲームが新しいリプレイの保存を拒否するだけです。リプレイを下の保管庫ディレクトリに保管すれば枠が空き、必要になったらワンクリックでゲームに戻せます。",
      "，按": "、",
      "，比随程序内置的 v": "、アプリ内蔵の v",
      "，点「更新到 v": "、「最新版 v",
      "，或在游戏内按重载热键（默认 Ctrl+Shift+R）。": "、またはゲーム内でリロード用ホットキー（既定は Ctrl+Shift+R）を押してください。",
      "，两个只能装一个": "、2つは同時にインストールできません",
      "，请更新到": "、次へ更新してください: ",
      "，然后重启游戏以使用最新修复。": "、その後ゲームを再起動すると最新の修正が適用されます。",
      "，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。": "、アプリ内蔵のバージョンと一致しています —— 「更新を確認」を押すと、GitHub に新しいバージョンがあるか確認できます。",
      "，sidecar 与配置（": "、sidecar と設定（",
      "；加载器本身已内置变速，装它就够了。": "；ローダー自体に変速機能が内蔵されているので、これをインストールするだけで十分です。",
      "：不用打开游戏，也不占游戏内的 10 个席位。": "：ゲームを起動する必要がなく、ゲーム内の10枠も消費しません。",
      "：启用 = 启动游戏即按该倍速运行（全程保持）， 禁用 = 恢复 1.0 正常速度。最低 1.0（加载器不支持减速），重启游戏生效。": "：有効 = ゲーム起動時からその倍速で動作（セッション中ずっと維持）、無効 = 通常の 1.0 倍に戻ります。最低 1.0（ローダーは減速に非対応）、ゲーム再起動後に反映されます。",
      "：已装": "：インストール済み",
      "：游戏库（多游戏目录 / 当前游戏）、变速器与加载器状态、回放库设置都存在这里。把库目录改到别处只会移动回放归档，上述配置文件仍留在默认目录。": "：ゲームライブラリ（複数のゲームディレクトリ / 現在のゲーム）、変速機とローダーの状態、リプレイ保管庫の設定はすべてここに保存されます。保管庫ディレクトリを別の場所に変更しても移動するのはリプレイのアーカイブだけで、上記の設定ファイルは既定のディレクトリに残ります。",
      "…（卸载时按此校验，防止误删他人文件）": "…（アンインストール時にこれを照合し、他人のファイルを誤って削除しないようにします）",
      "。 确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。": "。 このまま戻す場合は下のボタンを押してください。保管庫内のコピーはどのみち保持されます。",
      "。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。": "。「version.dll を復元」を押してください。ローカルにファイルがない場合は、対応するバージョンのリリースパッケージをダウンロードします。",
      "· 按住生效": "· 長押しで適用",
      "· 本地构建": "· ローカルビルド",
      "· 编辑此人物的自定义素材": "· このキャラクターのカスタム素材を編集",
      "· 等级": "· レベル",
      "· 第": "· 第",
      "· 点按切换": "· クリックで切り替え",
      "· 累计转账": "· 累計送金",
      "· 模组": "· Mod",
      "· 提交": "· コミット",
      "· 游戏运行中": "· ゲーム実行中",
      "· 最终首领": "· 最終ボス",
      "‹ 返回大厅": "‹ ロビーに戻る",
      "\" alt=\"第": "\" alt=\"第",
      "\" aria-pressed=\"false\" aria-label=\"选择第": "\" aria-pressed=\"false\" aria-label=\"第",
      "（本地构建）": "（ローカルビルド）",
      "（本页的旧版变速器自带独立的配置与热键，功能到此为止。）": "（このページの旧版変速機は独自の設定とホットキーを持ち、機能はここまでです。）",
      "（创建后不可修改）": "（作成後は変更できません）",
      "（构建于 2024-04-13）。": "（2024-04-13 ビルド）。",
      "（回退 AppData）": "（AppData へフォールバック）",
      "（文档目录）": "（ドキュメントフォルダー）",
      "（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；": "（Mod の DLL と同じディレクトリにあり、Mod の SdkConfig が読み書きします。ゲーム再起動後に反映）。チェックボックス = オン/オフ、数値欄 = 数値、テキスト欄 = 文字列；",
      "（CesiumLoader，Doorstop 式代理）放到游戏 exe 旁边；游戏启动时由 UnityPlayer 自动加载，再读取": "（CesiumLoader、Doorstop 方式のプロキシ）をゲームの exe の隣に置きます。ゲーム起動時に UnityPlayer が自動で読み込み、次に読み取るのは",
      "(SFW 审查版本)": "（SFW 審査版）",
      ") 恢复为应用内置的原始卡面吗？": "）アプリ内蔵の元のカードイラストに戻しますか？",
      "）， 模组加载器用的也是同一个文件名": "）。 Mod ローダーも同じファイル名を使用します",
      "）都在同一个文件夹里。": "）はすべて同じフォルダーにあります。",
      "）为准：安装或更新时写入，界面读它显示「已安装」。看不出（0.4.7 之前的老安装、或清单被删）时界面会如实显示「版本未知」——重新安装一次即可纳入版本管理。": "）のみを基準にします：インストール時または更新時に書き込まれ、UI がそれを読んで「インストール済み」と表示します。判別できない場合（0.4.7 より前の古いインストール、またはマニフェストが削除された場合）は、UI は正直に「バージョン不明」と表示します。一度再インストールすればバージョン管理の対象になります。",
      "）做依赖排序与 SDK 版本协商，然后逐个加载，并在控制台窗口按颜色分级打印加载报告。不会修改游戏文件本身。": "）に基づいて依存関係の並べ替えと SDK バージョンの調整を行い、その後1つずつ読み込み、コンソールウィンドウに色分けした読み込みレポートを出力します。ゲームファイル自体は変更しません。",
      "[当前生效]": "[現在有効]",
      "」改这个模组自己的配置。": "」からこの Mod 独自の設定を変更できます。",
      "→ 包内": "→ パッケージ内",
      "→ 请到「": "→ 「",
      "→ 最新 v": "→ 最新 v",
      "＋ 按键": "＋ キー",
      "＋ 打开回放": "＋ リプレイを開く",
      "＋ 导入模组": "＋ Mod をインポート",
      "＋ 添加倍速档位": "＋ 倍速プリセットを追加",
      "＋ 新建方案": "＋ 新規構成",
      "＋ 选择回放文件": "＋ リプレイファイルを選択",
      "= 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。": "= クリックしてから割り当てるキーを押します（キーボードの任意のキー、またはマウスの左/右/中ボタン・サイドボタン。Esc でキャンセル）。",
      "= 全程 2 倍速；进游戏后还能用": "= 全区間 2 倍速。ゲーム開始後も使用可能",
      "= 正常，": "= 正常、",
      "⚠ 方案B 备用安全网 —— 实测开启会导致游戏启动崩溃（0xC0000005），请保持关闭": "⚠ プランB の予備セーフティネット —— 有効にするとゲーム起動時にクラッシュ（0xC0000005）することを確認済みのため、オフのままにしてください",
      "0 张卡面": "カードイラスト 0 枚",
      "1.0 = 正常速度": "1.0 = 通常速度",
      "1.0 = 正常速度；最低 1.0，不支持减速": "1.0 = 通常速度。最低 1.0、減速には非対応",
      "10 个席位": "10 枠",
      "安装": "インストール",
      "安装：": "インストール：",
      "安装」即可。": "インストール」を押すだけです。",
      "安装加载器": "ローダーをインストール",
      "安装加载器时一并装上内置模组（行为日志 + 自由相机 + 变速）": "ローダーのインストール時に内蔵 Mod（行動ログ + フリーカメラ + 変速）も一緒に導入する",
      "安装角色皮肤模组": "キャラクタースキン Mod をインストール",
      "安装清单": "インストールマニフェスト",
      "安装与覆盖选项": "インストールと上書きのオプション",
      "安装中…": "インストール中…",
      "按 ID 下载": "ID でダウンロード",
      "按编号导入动态卡面": "番号指定で動くカードイラストをインポート",
      "按编号导入动态卡面…": "番号指定で動くカードイラストをインポート…",
      "按对局时间": "対局日時順",
      "按回放 ID": "リプレイ ID 順",
      "按回放 ID 下载到回放库": "リプレイ ID を指定して保管庫へダウンロード",
      "按键按钮": "キーボタン",
      "按来源筛选": "ソースで絞り込み",
      "按玩家、回合、技能或动作搜索...": "プレイヤー、ラウンド、スキル、アクションで検索...",
      "按文件大小": "ファイルサイズ順",
      "按下按键…（Esc 取消）": "キーを押してください…（Esc でキャンセル）",
      "按资源比例裁剪，或保留完整图片": "元画像の比率でトリミング、または画像全体を保持",
      "把模组日志转发到控制台": "Mod のログをコンソールへ転送",
      "把内置": "内蔵の",
      "把喜欢的人物放进同一个方案": "お気に入りのキャラクターを同じ構成にまとめる",
      "版本": "バージョン",
      "版本读取中…": "バージョンを読み取り中…",
      "版本未知": "バージョン不明",
      "版本详情": "バージョン詳細",
      "版本信息": "バージョン情報",
      "版本信息只以": "バージョン情報は",
      "保持选中帧的画布尺寸；透明边缘和脚底位置会影响对齐。": "選択したフレームのキャンバスサイズを維持します。透明な余白と足元の位置が位置合わせに影響します。",
      "保存布局与动作参数": "レイアウトとアクションパラメータを保存",
      "保存裁剪": "トリミングを保存",
      "保存尺寸": "サイズを保存",
      "保存动作参数": "アクションパラメータを保存",
      "保存多个游戏位置（国服 / 国际服 / TapTap 端…）。在「模组」页顶部可切换": "複数のゲームの場所（中国版 / 国際版 / TapTap 版…）を保存できます。「Mod」ページ上部で切り替え可能",
      "保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。": "保存後にゲームを再起動すると反映されます。通常は「ローダーを有効化」または「起動時の基本倍速」を変更するだけで十分です。",
      "保存配置": "設定を保存",
      "保存设置": "設定を保存",
      "保存失败": "保存に失敗しました",
      "保存失败，请重试。": "保存に失敗しました。もう一度お試しください。",
      "保存为透明 PNG，尺寸": "透過 PNG で保存、サイズ",
      "保存文案": "テキストを保存",
      "保存信息": "情報を保存",
      "保留的组件无法确定加载器版本，请重新安装或更新加载器。": "残されたコンポーネントからローダーのバージョンを特定できません。ローダーを再インストールするか更新してください。",
      "备份并删除中…": "バックアップして削除中…",
      "备份后只删除 version.dll，保留所有模组与配置": "バックアップ後に version.dll のみを削除し、すべての Mod と設定を保持します",
      "背景物品图标": "背景アイテムのアイコン",
      "倍速": "倍速",
      "倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度；加载器不支持减速）": "倍速は 1.0 ～ 100 の範囲で指定してください（1.0 = 通常速度。ローダーは減速に非対応）",
      "倍速档位（满足按键组合时切换速度）": "倍速プリセット（キーの組み合わせを満たすと速度を切り替え）",
      "倍速基于修改进程时间函数，需在游戏设置里": "倍速はプロセスの時間関数を変更するため、ゲーム設定で",
      "被占用时写入或删除会失败——那种情况先完全退出游戏再操作。": "使用中は書き込みや削除に失敗します。その場合はゲームを完全に終了してから操作してください。",
      "本程序缺少内置加载器文件，无法安装 —— 请使用完整发布包（推荐 portable.zip）。": "このアプリには内蔵ローダーファイルがないためインストールできません —— 完全なリリースパッケージ（portable.zip 推奨）を使用してください。",
      "本程序缺少内置加载器资源，安装按钮不会生效 —— 请重新下载完整发布包（推荐 portable.zip，single-file 版偶发解包不完整）。": "このアプリには内蔵ローダーリソースがないため、インストールボタンは機能しません —— 完全なリリースパッケージを再ダウンロードしてください（portable.zip 推奨。single-file 版は展開が不完全になることがあります）。",
      "本地构建": "ローカルビルド",
      "本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，": "このツールではそれらのリザルトフレームを読み取れません（ツール内蔵のプロトコルバージョンがゲームより古い可能性があります）。ゲームでも読み取れない場合は、",
      "本回合转账": "今ラウンドの送金",
      "本局地图": "この対局のマップ",
      "本局没有商店进店记录": "この対局にはショップ来店記録がありません",
      "本页安装的是": "このページでインストールするのは",
      "比 GitHub 上的 v": "GitHub 上の v",
      "比内置的 v": "内蔵の v",
      "编辑方案信息": "構成情報を編集",
      "编辑配置 JSON": "設定 JSON を編集",
      "编辑人物皮肤配置": "キャラクタースキン設定を編集",
      "变速器已就位": "変速機の準備ができました",
      "变速器已就位；游戏正在运行，新写入的文件要重启游戏才加载": "変速機の準備ができました。ゲームが実行中のため、新しく書き込んだファイルはゲームを再起動するまで読み込まれません",
      "变速已禁用": "変速は無効です",
      "变速已启用": "変速は有効です",
      "表现分析": "パフォーマンス分析",
      "播放预览": "プレビューを再生",
      "不裁剪，等比缩放并透明补边": "トリミングせず、比率を保って拡大縮小し、透明で余白を埋める",
      "不兼容": "非互換",
      "不是本工具装的文件 —— 见下方「高级与排障」": "このツールがインストールしたファイルではありません —— 下の「詳細とトラブルシューティング」を参照してください",
      "不添加自定义覆盖": "カスタム上書きを追加しない",
      "不装 Steam 也能启动游戏（首页「启动方式」把这一套三个开关一起开 / 关）": "Steam なしでもゲームを起動できます（ホームの「起動方法」でこの3つのスイッチをまとめてオン/オフ）",
      "不装任何 mod、只用加速 → 就留在本页，按下面卡片里的「": "Mod を入れず速度アップだけを使う → このページに留まり、下のカードの「",
      "才生效，而且 DLL 被占用时写入/删除会失败——那种情况先完全退出游戏再操作即可。": "が反映されます。また DLL が使用中だと書き込み/削除に失敗します。その場合はゲームを完全に終了してから操作してください。",
      "才生效；修改配置后": "で反映されます。設定を変更した後は",
      "裁剪角色素材": "キャラクター素材をトリミング",
      "裁剪卡面": "カードイラストをトリミング",
      "裁剪为目标比例": "目標比率にトリミング",
      "裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置": "トリミング範囲：ドラッグで移動、四隅をドラッグでサイズ変更、方向キーで位置を微調整",
      "裁剪预览": "トリミングプレビュー",
      "参战玩家 (": "参加プレイヤー (",
      "操作": "操作",
      "操作失败。": "操作に失敗しました。",
      "操作完成。": "操作が完了しました。",
      "操作系统": "OS",
      "查看": "表示",
      "查看完整更新日志 ›": "完全な更新履歴を見る ›",
      "查看详细版本信息": "詳細なバージョン情報を表示",
      "常见位置": "よくある場所",
      "常用": "よく使う",
      "称号": "称号",
      "承受伤害": "被ダメージ",
      "程序集版本": "アセンブリバージョン",
      "程序集文件时间": "アセンブリファイル日時",
      "程序目录": "アプリのディレクトリ",
      "程序缺少变速器文件": "変速機ファイルが見つかりません",
      "程序缺少内置加载器文件": "内蔵ローダーファイルが見つかりません",
      "尺寸来自游戏原资源": "サイズはゲームの元アセットから取得",
      "筹码": "レリック",
      "筹码记录": "レリック記録",
      "筹码记录共": "レリック記録は合計",
      "筹码名称": "レリック名",
      "筹码选择品质分布": "レリック選択のレアリティ分布",
      "出牌": "カードプレイ",
      "出牌次数": "カードプレイ回数",
      "处理方式": "処理方法",
      "处理失败": "処理に失敗しました",
      "传说": "レジェンド",
      "创建": "作成",
      "创建成功后自动添加到当前方案，并进入此人物的图片、文案和动画编辑器。": "作成に成功すると現在の構成に自動で追加され、このキャラクターの画像・テキスト・アニメーションエディターに移動します。",
      "创建方案": "構成を作成",
      "创建后会自动加入当前方案，接着编辑图片、动画和文案。": "作成後は現在の構成に自動で追加され、続けて画像・アニメーション・テキストを編集できます。",
      "创建失败": "作成に失敗しました",
      "此动作没有自制帧。导入帧序列后可逐帧查看和替换。": "このアクションには自作フレームがありません。フレームシーケンスをインポートすると、フレームごとに表示・置換できます。",
      "此动作使用原精灵映射：替换单帧会同步游戏映射；同一原帧被重复引用时会一并更新。重新导入整组帧会改为独立帧序列。": "このアクションは元のスプライトマッピングを使用します。1フレームを置換するとゲーム側のマッピングにも同期され、同じ元フレームが複数参照されている場合はまとめて更新されます。フレーム一式を再インポートすると独立したフレームシーケンスに切り替わります。",
      "此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信": "この Mod はゲームを操作できると宣言しています（カードプレイ / サイコロ / 移動などのシミュレーション）。権限の仕組みは廃止されたため、ここでは注意のみ表示します：信頼できる提供元か確認してください",
      "此人物的相同原皮肤已有规则，请编辑原规则。": "このキャラクターの同じ元スキンには既にルールがあります。元のルールを編集してください。",
      "此人物的相同原皮肤已有配置，请编辑已有的人物皮肤。": "このキャラクターの同じ元スキンには既に設定があります。既存のキャラクタースキンを編集してください。",
      "此人物还没有皮肤包。可导入 ZIP，或选择「新建自定义皮肤」。": "このキャラクターにはまだスキンパックがありません。ZIP をインポートするか、「カスタムスキンを作成」を選択してください。",
      "次": "回",
      "次 · 平均每回合": "回 · 1ラウンド平均",
      "次（其中": "回（うち",
      "次商店进店": "回ショップ来店",
      "次有购买）": "回は購入あり）",
      "次掷骰 ·": "回サイコロ ·",
      "从 GitHub 更新": "GitHub から更新",
      "从 Steam 启动": "Steam から起動",
      "从 ZIP 导入此人物皮肤包": "ZIP からこのキャラクターのスキンパックをインポート",
      "从方案中创建会自动加入该方案。你可以导出原版素材作为起点，也可以取消勾选，从空白皮肤开始。": "構成から作成するとその構成に自動で追加されます。元の素材をエクスポートして開始することも、チェックを外して空のスキンから始めることもできます。",
      "从列表移除（不会删除游戏文件）": "リストから削除（ゲームファイルは削除されません）",
      "从内置模板新建…": "内蔵テンプレートから新規作成…",
      "从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。": "アプリ内蔵の既定パッケージから新しい構成を作成します。ディスク上で変更済みの画像は使用しません。ネット接続不要で、作成後にカードイラストを1枚ずつ差し替えられます。",
      "从游戏导出官方皮肤素材": "ゲームから公式スキン素材をエクスポート",
      "从游戏缓存中选择一局，或手动打开回放文件": "ゲームキャッシュから対局を選ぶか、リプレイファイルを手動で開きます",
      "错误：": "エラー：",
      "打开": "開く",
      "打开 mods 目录": "mods フォルダーを開く",
      "打开 sdk 目录": "sdk フォルダーを開く",
      "打开本方案目录": "この構成のフォルダーを開く",
      "打开变速器自带的日志控制台窗口": "変速機内蔵のログコンソールウィンドウを開く",
      "打开方案目录": "構成フォルダーを開く",
      "打开库目录": "保管庫フォルダーを開く",
      "打开目录": "フォルダーを開く",
      "打开目录修复": "フォルダーを開いて修復",
      "打开皮肤目录": "スキンフォルダーを開く",
      "打开日志目录": "ログフォルダーを開く",
      "大小:": "サイズ:",
      "单次播放": "1回再生",
      "当前": "現在",
      "当前报告没有逐回合数据，请重新打开原始回放。": "現在のレポートにはラウンドごとのデータがありません。元のリプレイを開き直してください。",
      "当前方案已切换，请重新打开规则编辑。": "現在の構成が切り替わりました。ルール編集を開き直してください。",
      "当前方案已切换，请重新添加人物。": "現在の構成が切り替わりました。キャラクターを追加し直してください。",
      "当前生效中": "現在有効",
      "当前星币": "現在のスターコイン",
      "当前选择 · 重启游戏生效": "現在の選択 · ゲーム再起動後に反映",
      "当前游戏": "現在のゲーム",
      "当前正在操作的游戏": "現在操作中のゲーム",
      "档位": "プリセット",
      "导出 AI 文本 (TXT)": "AI 用テキストを出力 (TXT)",
      "导出 JSON": "JSON を出力",
      "导出 ZIP": "ZIP を出力",
      "导出表格 (CSV)": "表を出力 (CSV)",
      "导出当前动作 GIF": "現在のアクションを GIF で出力",
      "导出哪套官方皮肤": "どの公式スキンを出力するか",
      "导出所选皮肤并创建": "選択したスキンを出力して作成",
      "导出所选皮肤的图片与二维动画作为编辑起点。方案中的替换范围保持原设置；独立创建的包默认对应所选皮肤。资源未下载时，请先在游戏中查看该皮肤。": "選択したスキンの画像と2Dアニメーションを編集の出発点として出力します。構成内の置換範囲は元の設定のままです。単独で作成したパックは既定で選択したスキンに対応します。アセットが未ダウンロードの場合は、先にゲーム内でそのスキンを表示してください。",
      "导入 ZIP": "ZIP をインポート",
      "导入本地 cesium-loader 发布 ZIP，安装加载器、SDK 与包内模组": "ローカルの cesium-loader リリース ZIP をインポートし、ローダー・SDK・同梱 Mod をインストール",
      "导入单个模组 DLL，或整个模组包（ZIP，解压到 mods 目录）": "単一の Mod DLL、または Mod パッケージ全体（ZIP、mods ディレクトリに展開）をインポート",
      "导入发布包": "リリースパッケージをインポート",
      "导入方案 ZIP": "構成 ZIP をインポート",
      "导入回放": "リプレイをインポート",
      "导入结束。": "インポートが完了しました。",
      "导入旧式皮肤包": "旧形式スキンパックをインポート",
      "导入皮肤包 (.zip)": "スキンパックをインポート (.zip)",
      "导入人物包": "キャラクターパックをインポート",
      "导入视频 / GIF": "動画 / GIF をインポート",
      "导入与管理": "インポートと管理",
      "导入帧序列": "フレームシーケンスをインポート",
      "导入中…": "インポート中…",
      "倒计时 / 地图像素头像": "カウントダウン / マップのドット絵アイコン",
      "地块标志": "マス目マーカー",
      "地图人物布局": "マップ上のキャラクター配置",
      "地图事件": "マップイベント",
      "地图事件 (": "マップイベント (",
      "的": "の",
      "的皮肤": "のスキン",
      "的自制帧和原精灵映射，重启游戏后恢复原动作。": "の自作フレームと元のスプライトマッピングを削除します。ゲームを再起動すると元のアクションに戻ります。",
      "等待 GameAssembly.dll 秒数": "GameAssembly.dll の待機秒数",
      "等待 HybridCLR 热更秒数": "HybridCLR ホットアップデートの待機秒数",
      "等待托管域初始化秒数": "マネージドドメイン初期化の待機秒数",
      "等待游戏配置": "ゲーム設定を待機中",
      "等待中": "待機中",
      "第": "第",
      "点 · 受控": "出目 · 操作対象",
      "点「安装加载器」把加载器写进游戏目录": "「ローダーをインストール」を押して、ローダーをゲームディレクトリに書き込みます",
      "点「安装加载器」装的就是这一份": "「ローダーをインストール」で導入されるのはこのファイルです",
      "点「查看完整更新日志」了解本次更新": "「完全な更新履歴を見る」で今回の更新内容を確認できます",
      "点「检查更新」获取": "「更新を確認」を押して取得",
      "点「检查更新」向 GitHub 查询": "「更新を確認」を押して GitHub に問い合わせる",
      "点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。": "クリックで選択/解除し、確定するとそのプリセットに適用されます。プリセットのキーは複数選択できます（例：Ctrl + Shift）。",
      "调试控制台": "デバッグコンソール",
      "调试控制台 console": "デバッグコンソール console",
      "调整界面偏好并查看应用信息": "画面の設定を調整し、アプリ情報を確認します",
      "动画": "アニメーション",
      "动画偏好": "アニメーション設定",
      "动画帧预览": "アニメーションフレームプレビュー",
      "动画逐帧图片": "アニメーションのフレーム画像",
      "动态": "アニメ",
      "动态卡面 ·": "動くカードイラスト ·",
      "动作": "アクション",
      "读不到更新日志内容。": "更新履歴の内容を読み取れません。",
      "读不到加载器更新日志（内置为空，联网获取也没成功）。点「": "ローダーの更新履歴を読み取れません（内蔵は空で、オンライン取得にも失敗しました）。「",
      "对局表现分析": "対局パフォーマンス分析",
      "对局回放分析工具": "対局リプレイ分析ツール",
      "对局结果": "対局結果",
      "对局摘要": "対局サマリー",
      "对战回放": "対戦リプレイ",
      "多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。": "多くの場合、別のツール（例：「ゲームツール」ページの旧版単体変速機）または第三者が置いたファイルです。",
      "多人物搭配 ·": "複数キャラの組み合わせ ·",
      "二维精灵动画": "2D スプライトアニメーション",
      "发布包完整路径": "リリースパッケージのフルパス",
      "发布包正在导入，请稍候。": "リリースパッケージをインポート中です。しばらくお待ちください。",
      "发现新版本：已装 v": "新しいバージョンがあります：インストール済み v",
      "繁體中文": "繁體中文",
      "反馈问题时把这段信息一起贴上，能省很多来回。": "不具合を報告する際にこの情報も貼り付けると、やり取りが大幅に減ります。",
      "返回当前方案": "現在の構成に戻る",
      "返回吉星派对大厅": "Astral Party のロビーに戻る",
      "方案：": "構成：",
      "方案标识：": "構成 ID：",
      "方案会移入 .deleted 文件夹，引用的自定义皮肤包会保留。": "構成は .deleted フォルダーへ移動します。参照されているカスタムスキンパックは保持されます。",
      "方案简介": "構成の概要",
      "方案描述（可选）：": "構成の説明（任意）：",
      "方案名称": "構成名",
      "方案内图片": "構成内の画像",
      "方案设置": "構成設定",
      "方案显示名称：": "構成の表示名：",
      "方案英文目录名（唯一标识）：": "構成の英語フォルダー名（一意の ID）：",
      "方案中的人物": "構成内のキャラクター",
      "方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。": "方向キーで位置を微調整、Shift を押しながらで 10 ピクセルずつ移動します。元の画像は変更されません。",
      "放到游戏 exe 旁边，游戏启动时自动加载。游戏运行中也允许安装/卸载，只是本次写入要": "ゲームの exe の隣に置くと、ゲーム起動時に自動で読み込まれます。ゲーム実行中でもインストール/アンインストールできますが、今回の書き込みは",
      "放回游戏": "ゲームに戻す",
      "份）": "件）",
      "附加保险：忽略 Steam 的“请从 Steam 启动”重启请求": "追加の保険：Steam の「Steam から起動してください」という再起動要求を無視する",
      "复制版本信息": "バージョン情報をコピー",
      "覆盖已有 ZIP？": "既存の ZIP を上書きしますか？",
      "该角色全部原皮肤": "このキャラクターのすべての元スキン",
      "该人物全部原皮肤": "このキャラクターのすべての元スキン",
      "该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。": "このプレイヤーの今ラウンドのスナップショット区間内における送金増分です。同じラウンドで複数回来店しても同じ値が表示され、重複して加算されません。",
      "高级": "詳細",
      "高级：编辑完整清单": "詳細：マニフェスト全体を編集",
      "高级：用系统默认编辑器直接改 JSON": "詳細：既定のエディターで JSON を直接編集",
      "高级编辑…": "詳細編集…",
      "高级设置：替换范围与混合搭配": "詳細設定：置換範囲と組み合わせ",
      "高级选项（Steam 绕过细节 · 引导 · 超时）": "詳細オプション（Steam バイパスの詳細 · ブートストラップ · タイムアウト）",
      "高级与排障": "詳細とトラブルシューティング",
      "个）": "件）",
      "个候选：": "件の候補：",
      "个进行中": "件進行中",
      "个人物": "キャラクター",
      "个人物 ·": "キャラクター ·",
      "个位置": "箇所",
      "更换图片": "画像を差し替え",
      "更新，无需更新。": "更新を確認しました。更新は不要です。",
      "更新: —": "更新: —",
      "更新（从 GitHub 更新过）": "より新しい（GitHub から更新済み）",
      "更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。": "更新済み（このファイルは GitHub から更新しました）—— 「更新を確認」を押して、さらに新しいバージョンがないか確認してください。",
      "更新到 v": "最新版 v",
      "更新到最新": "最新版に更新",
      "更新角色皮肤模组": "キャラクタースキン Mod を更新",
      "更新日志": "更新履歴",
      "更新时允许覆盖游戏目录里不属于本工具的 version.dll": "更新時に、ゲームディレクトリ内のこのツールが導入したものではない version.dll の上書きを許可する",
      "更新时允许降级（仅在发布包比已装版本旧时才需要）": "更新時にダウングレードを許可（リリースパッケージがインストール済みより古い場合のみ必要）",
      "更新中…": "更新中…",
      "工具来源：": "ツールの入手元：",
      "工具箱已更新": "ツールボックスを更新しました",
      "功能键": "ファンクションキー",
      "攻击 / 防御": "攻撃 / 防御",
      "共": "合計",
      "共 0 张卡面": "カードイラスト 合計 0 枚",
      "共筛选出": "絞り込み結果：",
      "构建配置": "ビルド構成",
      "购买结果": "購入結果",
      "挂接延迟 wait_with_hook": "フック遅延 wait_with_hook",
      "挂接延迟（毫秒）": "フック遅延（ミリ秒）",
      "关闭": "閉じる",
      "关闭垂直同步": "垂直同期をオフにする",
      "关闭消息": "メッセージを閉じる",
      "关闭自动倍速时固定为 1.0；快捷键档位仍可临时切换速度": "自動倍速オフ時は 1.0 に固定されます。ホットキーのプリセットでは引き続き一時的に速度を切り替えられます",
      "关卡进度": "ステージ進行度",
      "关于": "このアプリについて",
      "官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。": "公式素材で画像とキャラクターアニメーションを置き換えます。ボイス、動画、エフェクト、特殊演出はオリジナルのままです。",
      "官方皮肤": "公式スキン",
      "官方皮肤基础": "公式スキンベース",
      "管理游戏目录…": "ゲームディレクトリを管理…",
      "归档": "保管",
      "归档最旧的": "古い順に保管",
      "归档最旧的 3 局": "古い3対局を保管",
      "国服": "中国版",
      "国服（测试服）": "中国版（テスト）",
      "国服（正式服）": "中国版（正式）",
      "国际服": "国際版",
      "国际服（测试服）": "国際版（テスト）",
      "国际服（正式服）": "国際版（正式）",
      "还没有倍速档位——点下面「＋ 添加倍速档位」创建一个，例如 Ctrl = 2 倍速。": "まだ倍速プリセットがありません。下の「＋ 倍速プリセットを追加」を押して作成してください（例：Ctrl = 2 倍速）。",
      "还没有回放。在游戏里打完一局后，到「战绩」里对那条记录点「保存回放」，再回来刷新即可。": "リプレイがまだありません。ゲームで1対局を終えたら「戦績」でその記録の「リプレイを保存」を押し、戻って更新してください。",
      "还没有模组 —— 点「导入模组」添加 DLL 或 ZIP 模组包，或重新安装加载器时勾选内置模组": "まだ Mod がありません —— 「Mod をインポート」を押して DLL または ZIP の Mod パッケージを追加するか、ローダーを再インストールする際に内蔵 Mod にチェックを入れてください",
      "还没有配置文件（mods\\": "設定ファイルがまだありません（mods\\",
      "还没有添加人物": "キャラクターがまだ追加されていません",
      "还没有添加游戏目录。": "ゲームディレクトリがまだ追加されていません。",
      "还没找到游戏目录 —— 展开「高级与排障」点「自动检测」，或手动选择游戏 exe 所在的文件夹。": "ゲームディレクトリが見つかりません —— 「詳細とトラブルシューティング」を開いて「自動検出」を押すか、ゲームの exe があるフォルダーを手動で選択してください。",
      "合计": "合計",
      "后台皮肤任务": "バックグラウンドのスキンタスク",
      "候选卡牌": "候補カード",
      "候选选项": "候補",
      "恢复 version.dll": "version.dll を復元",
      "恢复此动作": "このアクションを復元",
      "恢复此动作？": "このアクションを復元しますか？",
      "恢复当前版本；本地缺失时下载对应版本发布包": "現在のバージョンに復元します。ローカルにない場合は対応するバージョンのリリースパッケージをダウンロードします",
      "恢复默认模板": "既定テンプレートに戻す",
      "恢复内置卡面": "内蔵カードイラストに戻す",
      "恢复内置默认包": "内蔵の既定パッケージに戻す",
      "恢复原版": "オリジナルに戻す",
      "恢复中…": "復元中…",
      "回车": "Enter",
      "回放": "リプレイ",
      "回放 ID": "リプレイ ID",
      "回放 ID 只能是数字——就是游戏战绩里那一长串数字。": "リプレイ ID は数字のみです —— ゲームの戦績にある長い数字の並びです。",
      "回放分析与复盘工具": "リプレイ分析・復盤ツール",
      "回放服务器": "リプレイサーバー",
      "回放服务器（不确定就按当前游戏版本选）": "リプレイサーバー（不明な場合は現在のゲームバージョンに合わせて選択）",
      "回放复盘": "リプレイ復盤",
      "回放库": "リプレイ保管庫",
      "回放库操作结果": "リプレイ保管庫の操作結果",
      "回放列表": "リプレイ一覧",
      "回放文件解析成功！": "リプレイファイルの解析に成功しました！",
      "回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。 填上回放 ID 就能直接下载进": "リプレイはサーバーが録画して公式リプレイサーバーに置いており、ゲーム内の「リプレイを保存」もそこから取得します。 リプレイ ID を入力すればそのまま保管庫へダウンロードできます。",
      "回合": "ラウンド",
      "回合 ·": "ラウンド ·",
      "回合：": "ラウンド：",
      "或其中的卡面图片。": "またはその中のカードイラスト。",
      "击杀": "キル",
      "击杀数量（怪物）": "キル数（モンスター）",
      "及其目录下的所有卡面图片吗？此操作不可撤销。": "とそのディレクトリ内のすべてのカードイラストを削除しますか？この操作は元に戻せません。",
      "吉星派对 · BWiki 维基攻略": "Astral Party · BWiki 攻略Wiki",
      "吉星派对回放与游戏工具箱": "Astral Party リプレイ＆ゲームツールボックス",
      "集成回放分析、协议帧查看、维基浏览和游戏变速器管理，数据均在本地处理。": "リプレイ分析、プロトコルフレーム表示、Wiki 閲覧、ゲーム変速機の管理を統合。データはすべてローカルで処理されます。",
      "技能使用": "スキル使用",
      "继续创建皮肤包": "スキンパックの作成を続ける",
      "继续导入会用包里的旧版本覆盖现有文件。玩家改过的": "インポートを続けると、パッケージ内の旧バージョンで既存のファイルが上書きされます。プレイヤーが変更した",
      "加载器": "ローダー",
      "加载器发布 ZIP 将一起安装加载器、SDK、bootstrap 和包内模组，保留已有加载器配置。仅使用可信的官方发布文件，重启游戏后生效。": "ローダーのリリース ZIP をインポートすると、ローダー・SDK・bootstrap・同梱 Mod がまとめてインストールされ、既存のローダー設定は保持されます。信頼できる公式リリースファイルのみを使用してください。ゲーム再起動後に反映されます。",
      "加载器更新日志": "ローダーの更新履歴",
      "加载器内置功能": "ローダー内蔵機能",
      "加载器缺失且版本未知": "ローダーがなく、バージョンも不明",
      "加载器设置": "ローダー設定",
      "加载器已就位": "ローダーの準備ができました",
      "加载器与旧版独立变速器（「游戏工具」页）共用": "ローダーは旧版単体変速機（「ゲームツール」ページ）と共用です",
      "检测到版本降级": "バージョンのダウングレードを検出",
      "检测到游戏正在运行——安装 / 卸载不会被拦，但先看清下面的后果：": "ゲームの実行を検出しました —— インストール/アンインストールはブロックされませんが、まず下の影響を確認してください：",
      "检测中": "検出中",
      "检查更新": "更新を確認",
      "检查更新失败：": "更新の確認に失敗しました：",
      "检查失败": "確認に失敗しました",
      "检查失败：": "確認に失敗しました：",
      "检查中…": "確認中…",
      "简体中文": "简体中文",
      "简要说明此方案的特色…": "この構成の特徴を簡単に説明…",
      "将": "次の",
      "将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。": "アプリ内蔵の既定パッケージからこのカードイラストを復元し、現在の構成内の同名画像を上書きします。ディスク上で変更された default は元テンプレートとして使用されません。",
      "将从应用内置资源恢复当前游戏的 default 方案，覆盖同名图片并移除 default 中的动态覆盖。不会改动其他方案，也不会自动切换当前选择。": "アプリ内蔵リソースから現在のゲームの default 構成を復元し、同名画像を上書きして default 内の動的オーバーライドを削除します。他の構成は変更せず、現在の選択も自動では切り替えません。",
      "将从最新 Release 下载默认包并安装到当前游戏，覆盖 default 中的同名图片。下载不会自动切换当前方案。": "最新の Release から既定パッケージをダウンロードして現在のゲームにインストールし、default 内の同名画像を上書きします。ダウンロードしても現在の構成は自動では切り替わりません。",
      "将含 bundle 的旧式卡面 ZIP 转换为卡牌皮肤方案": "bundle を含む旧形式のカードイラスト ZIP をカードスキン構成に変換",
      "将含 bundle 的旧式皮肤 ZIP 转换为自定义皮肤包": "bundle を含む旧形式のスキン ZIP をカスタムスキンパックに変換",
      "交给 Steam 启动（DRM / 云存档 / 更新检查都走 Steam），并关闭加载器的 Steam 绕过 —— Steam 大厅 / 好友邀请进房恢复正常（默认）": "Steam に起動を任せ（DRM / クラウドセーブ / 更新確認はすべて Steam 経由）、ローダーの Steam バイパスを無効にします —— Steam ロビー / フレンド招待からの入室が正常に戻ります（既定）",
      "角色": "キャラクター",
      "角色简介": "キャラクター紹介",
      "角色名字": "キャラクター名",
      "角色皮肤": "キャラクタースキン",
      "角色皮肤 · 图片 / 文案 / 精灵动画": "キャラクタースキン · 画像 / テキスト / スプライトアニメーション",
      "角色皮肤管理": "キャラクタースキン管理",
      "角色皮肤模组已安装": "キャラクタースキン Mod はインストール済みです",
      "角色缩略图": "キャラクターサムネイル",
      "角色头像": "キャラクターアイコン",
      "角色文案": "キャラクターテキスト",
      "脚底锚点 X": "足元アンカー X",
      "脚底锚点 Y": "足元アンカー Y",
      "阶段3 兜底（实测已作废、hook 从未命中；保持开启即可）": "フェーズ3 フォールバック（実測では無効、hook は一度もヒットせず。オンのままで問題ありません）",
      "结构可疑": "構造に疑義あり",
      "结算奖励": "リザルト報酬",
      "结算快照中的完整属性与行动统计，记录本局关键数据指标。": "リザルトスナップショット内の完全なステータスと行動統計。この対局の主要指標を記録します。",
      "结算快照中的总伤害、承伤、治疗、转账与行动次数。": "リザルトスナップショット内の総与ダメージ、被ダメージ、回復、送金、行動回数。",
      "解析不出结算帧，文件可能已损坏或来自不兼容的版本。": "リザルトフレームを解析できません。ファイルが破損しているか、互換性のないバージョンの可能性があります。",
      "解析失败": "解析に失敗しました",
      "解析展示失败:": "解析・表示に失敗しました:",
      "介绍一下这套搭配，例如适合的玩法或角色组合。": "この組み合わせを紹介してください（向いている遊び方やキャラクターの組み合わせなど）。",
      "界面动态效果": "画面のアニメーション効果",
      "界面动态效果已": "画面のアニメーション効果を",
      "界面与配表语言": "画面とデータ表の言語",
      "仅回放库": "保管庫のみ",
      "仅删除 version.dll": "version.dll のみ削除",
      "仅替换": "置換のみ",
      "仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。": "リプレイで実際に受信した移動用サイコロの結果のみを集計します。複数ダイスの出目は個別に保持し、操作された移動は別途マークし、戦闘用や判定用のダイスは混ぜません。",
      "仅游戏内": "ゲーム内のみ",
      "仅支持英文、数字、下划线及减号": "英数字、アンダースコア、ハイフンのみ使用できます",
      "进入游戏自动倍速": "ゲーム開始時に自動で倍速",
      "进游戏后等待再挂接时间函数的毫秒数（防崩溃）": "ゲーム開始後、時間関数をフックするまで待機するミリ秒数（クラッシュ防止）",
      "旧 —— 点「重新安装内置版本」可装回内置版": "より古い —— 「内蔵バージョンを再インストール」で内蔵版に戻せます",
      "旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。": "より古い —— 内蔵版に戻すには「内蔵バージョンを再インストール」、新しい版があるか知りたいときは「更新を確認」を押してください。",
      "旧版独立变速器": "旧版単体変速機",
      "旧配置已保留，但暂未转换：": "旧設定は保持されていますが、まだ変換されていません：",
      "居中重置": "中央にリセット",
      "局": "対局",
      "局 ·": "対局 ·",
      "局）": "対局）",
      "局）。": "対局）。",
      "橘雪莉": "橘雪莉",
      "卡面详情预览": "カードイラスト詳細プレビュー",
      "卡牌 / 技能": "カード / スキル",
      "卡牌 / 异画编号": "カード / 別イラスト番号",
      "卡牌编号:": "カード番号:",
      "卡牌名称:": "カード名:",
      "卡牌皮肤": "カードスキン",
      "卡牌皮肤 · 方案与卡面管理": "カードスキン · 構成とカードイラストの管理",
      "卡牌使用": "カード使用",
      "开局 / 全局事件": "開始時 / 全体イベント",
      "开启": "オン",
      "开启看板立绘与页面切换动画": "看板イラストとページ切り替えアニメーションを有効にする",
      "开启自动托管（保留": "自動アーカイブを有効にする（保持",
      "开始深扫": "詳細スキャンを開始",
      "开始下载": "ダウンロードを開始",
      "颗骰子 · 平均": "個のサイコロ · 平均",
      "可更新": "更新可能",
      "可以更新到 v": "更新可能: v",
      "空格": "Space",
      "控制台窗口置顶": "コンソールウィンドウを最前面に固定",
      "口径": "集計基準",
      "库目录": "保管庫ディレクトリ",
      "库目录不可用": "保管庫ディレクトリが使用できません",
      "快速扫描没找到，试试下面的深度扫描。": "クイックスキャンでは見つかりませんでした。下の詳細スキャンを試してください。",
      "来源": "ソース",
      "来源：随本程序内置（打包时的版本）": "ソース：このアプリに同梱（パッケージ時のバージョン）",
      "来源：GitHub 最新 Release": "ソース：GitHub の最新 Release",
      "来自回合快照中的累计转账；未记录收款对象时不做推断。": "ラウンドスナップショットの累計送金から取得。受取相手が記録されていない場合は推測しません。",
      "类别:": "カテゴリ:",
      "累计": "累計",
      "累计伤害": "累計与ダメージ",
      "累计死亡": "累計死亡",
      "累计星币": "累計スターコイン",
      "累计转账": "累計送金",
      "离上限只差一点。开启自动托管后，每次打开本工具都会自动把超出的旧回放归档进库。": "上限まであと少しです。自動アーカイブを有効にすると、このツールを開くたびに超過した古いリプレイが自動で保管庫へ保管されます。",
      "立绘物品图标": "立ち絵アイテムのアイコン",
      "立即复制创建": "コピーしてすぐ作成",
      "立即整理一次": "今すぐ整理する",
      "例如 10001": "例：10001",
      "例如：我的日常搭配": "例：いつもの組み合わせ",
      "两边都删": "両方削除",
      "浏览…": "参照…",
      "浏览器打开": "ブラウザーで開く",
      "浏览添加…": "参照して追加…",
      "锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。": "アンカーは 0～1 で指定し、Y はキャンバス下端から上向きに計算します。スケールは PPU に乗算され、ゲーム内のキャラクターの当たり判定や移動は変更しません。",
      "没检测到标准 Unity 结构": "標準的な Unity 構造を検出できませんでした",
      "没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）": "標準的な Unity ゲーム構造を検出できませんでした（*_Data または UnityPlayer.dll がありません）",
      "没有检测到桌面宿主，读不到程序集版本": "デスクトップホストが検出されず、アセンブリバージョンを読み取れません",
      "没有内嵌发布包": "同梱リリースパッケージがありません",
      "没有内嵌发布包（本地开发构建）": "同梱リリースパッケージがありません（ローカル開発ビルド）",
      "没有匹配的方案。": "一致する構成がありません。",
      "没有匹配的皮肤包。可新建皮肤包或导入 ZIP。": "一致するスキンパックがありません。新規作成するか ZIP をインポートしてください。",
      "每次打开本工具时，把超出保留名额的旧回放自动归档进库；最新的几局永远不会被动。": "このツールを開くたびに、保持枠を超えた古いリプレイを自動で保管庫へ保管します。最新の数対局は決して対象になりません。",
      "每次进入游戏自动启用设定倍速": "ゲームに入るたびに設定した倍速を自動で有効化",
      "每单位像素 PPU": "1単位あたりのピクセル数 PPU",
      "每个模组是引用 CesiumLoader.SDK 的 .NET DLL，一个模组一个文件夹：": "各 Mod は CesiumLoader.SDK を参照する .NET DLL で、1 Mod につき1フォルダー：",
      "每回合新增": "ラウンドごとの増加",
      "秒循环": "秒周期",
      "名)": "名）",
      "名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。": "名前とキャラクター紹介はキャラクター共通のテキストを使用するため、そのキャラクターの他のスキンにも影響する可能性があります。",
      "命运卡": "運命カード",
      "命运卡 (": "運命カード (",
      "模组": "Mod",
      "模组」页面。": "Mod」ページ。",
      "模组管理": "Mod 管理",
      "模组页面内置换速开关：": "Mod ページ内の変速スイッチ：",
      "模组与配置已保留，可恢复加载器": "Mod と設定は保持されています。ローダーを復元できます",
      "模组状态与使用说明": "Mod の状態と使い方",
      "默认：": "既定：",
      "默认：文档\\AstralPartyReplays": "既定：ドキュメント\\AstralPartyReplays",
      "默认方案不可删除": "既定の構成は削除できません",
      "默认方案预览": "既定構成のプレビュー",
      "默认目录": "既定のディレクトリ",
      "默认皮肤方案 default 禁止删除": "既定のスキン構成 default は削除できません",
      "默认替换此人物的全部原皮肤。自定义素材优先，未修改的部分沿用所选基础。": "既定ではこのキャラクターのすべての元スキンを置換します。カスタム素材が優先され、未変更の部分は選択したベースを引き継ぎます。",
      "目标角色": "対象キャラクター",
      "目录里是其它 version.dll": "ディレクトリ内は別の version.dll です",
      "目录内检测到游戏主程序（AstralParty*.exe）": "ディレクトリ内にゲーム本体（AstralParty*.exe）を検出",
      "目录内未找到 AstralParty 游戏程序（仍可安装，但请确认目录正确）": "ディレクトリ内に AstralParty のゲームプログラムが見つかりません（インストールは可能ですが、ディレクトリが正しいか確認してください）",
      "难度": "難易度",
      "内置": "内蔵",
      "内置 SHA-256": "内蔵 SHA-256",
      "内置加载器": "内蔵ローダー",
      "内置文件就绪：version.dll": "内蔵ファイルの準備完了：version.dll",
      "昵称，可留空": "ニックネーム（空欄可）",
      "您的昵称": "あなたのニックネーム",
      "排序方式": "並べ替え",
      "配置 ·": "設定 ·",
      "配置保存在": "設定の保存先は",
      "配置文件夹": "設定フォルダー",
      "配置写入后需按重载热键或重启游戏生效。": "設定を書き込んだ後は、リロード用ホットキーを押すかゲームを再起動すると反映されます。",
      "皮肤": "スキン",
      "皮肤包操作": "スキンパック操作",
      "皮肤包素材库": "スキンパック素材ライブラリ",
      "皮肤短描述": "スキンの短い説明",
      "皮肤方案管理与切换": "スキン構成の管理と切り替え",
      "皮肤故事": "スキンのストーリー",
      "皮肤类型": "スキンの種類",
      "偏移": "オフセット",
      "品质": "レアリティ",
      "普通": "ノーマル",
      "启动 Astral Party（吉星派对）": "Astral Party を起動",
      "启动 Astral Party（吉星派对）· 从 Steam 启动": "Astral Party を起動 · Steam から起動",
      "启动 Astral Party（吉星派对）· 绕过 Steam：直接启动游戏主程序": "Astral Party を起動 · Steam をバイパスしてゲーム本体を直接起動",
      "启动版本": "起動バージョン",
      "启动方式": "起動方法",
      "启动加速 startup_state": "起動高速化 startup_state",
      "启动加速倍率": "起動高速化の倍率",
      "启动加速时长（秒）": "起動高速化の時間（秒）",
      "启动时基础倍速（1.0 = 正常；2.0 = 全程 2 倍速；也可留 1.0 由游戏内热键变速。不支持减速，最小 1.0）": "起動時の基本倍速（1.0 = 通常。2.0 = 全区間 2 倍速。1.0 のままにしてゲーム内ホットキーで変速しても可。減速は非対応、最小 1.0）",
      "启动时基础倍速（1.0 = 正常；不支持减速，最小 1.0）": "起動時の基本倍速（1.0 = 通常。減速は非対応、最小 1.0）",
      "启动时基础倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度，不支持减速）": "起動時の基本倍速は 1.0 ～ 100 の範囲で指定してください（1.0 = 通常速度、減速は非対応）",
      "启动游戏": "ゲームを起動",
      "启用 / 禁用（重启游戏后生效）": "有効 / 無効（ゲーム再起動後に反映）",
      "启用 / 禁用与配置修改都在": "有効/無効の切り替えと設定の変更はすべて",
      "启用此皮肤": "このスキンを有効化",
      "启用后，启动游戏即按该倍速运行（全程保持）": "有効にすると、ゲーム起動時からその倍速で動作します（全区間維持）",
      "启用后无需按快捷键，游戏加载完成就持续使用下方倍速": "有効にするとホットキー不要で、ゲームの読み込み完了後から下の倍速が継続的に適用されます",
      "启用加载器": "ローダーを有効化",
      "启用启动加速": "起動高速化を有効化",
      "强制卸载（version.dll 不属于本工具时也删除）": "強制アンインストール（version.dll がこのツールのものでなくても削除）",
      "强制卸载（version.dll 与内置不一致时也删除）": "強制アンインストール（version.dll が内蔵と一致しなくても削除）",
      "清单 JSON 无效：": "マニフェスト JSON が無効です：",
      "清空搜索": "検索をクリア",
      "请等待当前操作完成。": "現在の操作が完了するまでお待ちください。",
      "请稍候": "しばらくお待ちください",
      "请输入方案名称。": "構成名を入力してください。",
      "请输入方案目录名": "構成のディレクトリ名を入力してください",
      "请输入方案显示名称": "構成の表示名を入力してください",
      "请输入有效的数字编号": "有効な数字の番号を入力してください",
      "请先创建或选择一个有效方案。": "先に有効な構成を作成するか選択してください。",
      "请先等待或取消当前转换": "現在の変換が終わるのを待つか、キャンセルしてください",
      "请先选择外部皮肤方案": "先に外部スキン構成を選択してください",
      "请选择发布包或粘贴完整路径。": "リリースパッケージを選択するか、フルパスを貼り付けてください。",
      "请选择官方皮肤基础或自定义皮肤包。": "公式スキンベースまたはカスタムスキンパックを選択してください。",
      "请选择一个要深度扫描的盘符。": "詳細スキャンするドライブを選択してください。",
      "请在左侧点击任意协议帧进行查看解码数据": "左側の任意のプロトコルフレームをクリックすると、デコードしたデータを表示します",
      "请展开模组状态查看详情。": "Mod の状態を展開して詳細を確認してください。",
      "区服": "リージョン",
      "取消": "キャンセル",
      "取消排队": "待機をキャンセル",
      "取消下载": "ダウンロードをキャンセル",
      "取消转换": "変換をキャンセル",
      "去模组页面（推荐）": "Mod ページへ（推奨）",
      "去重玩家动作统计": "重複を除いたプレイヤー行動統計",
      "全部来源": "すべてのソース",
      "全部类别": "すべてのカテゴリ",
      "全部原皮肤": "すべての元スキン",
      "全队转账": "チーム全体の送金",
      "缺少内置文件": "内蔵ファイルがありません",
      "缺失": "欠落",
      "确定（已选": "確定（選択済み",
      "确定删除模组「": "Mod「",
      "确定要彻底删除方案": "構成を完全に削除しますか：",
      "确定要将": "次の",
      "确认并导入": "確認してインポート",
      "确认还原": "復元する",
      "确认继续": "続行する",
      "确认删除": "削除する",
      "确认删除皮肤方案": "スキン構成を削除しますか",
      "确认卸载": "アンインストールする",
      "绕过 Steam 启动": "Steam をバイパスして起動",
      "热更新资源缓存": "ホットアップデートのリソースキャッシュ",
      "人物": "キャラクター",
      "人物皮肤": "キャラクタースキン",
      "人物皮肤包": "キャラクタースキンパック",
      "人物皮肤编辑分类": "キャラクタースキン編集カテゴリ",
      "人物设置": "キャラクター設定",
      "人物缩放": "キャラクターの拡大率",
      "任务加入后台队列后可继续新建；右下角查看进度或终止。": "タスクはバックグラウンドキューに追加されるため、続けて新規作成できます。右下で進捗を確認したり中止したりできます。",
      "仍然导入": "それでもインポート",
      "仍要放回（": "それでも戻す（",
      "日本語": "日本語",
      "如 二次元萌化包": "例：アニメ風スキンパック",
      "扫描系统": "システムをスキャン",
      "筛选筹码、玩家、品质、来源...": "レリック、プレイヤー、レアリティ、ソースで絞り込み...",
      "筛选商店类型、玩家、卡牌、购买结果...": "ショップ種別、プレイヤー、カード、購入結果で絞り込み...",
      "删除": "削除",
      "删除此方案": "この構成を削除",
      "删除方案": "構成を削除",
      "删除该方案": "その構成を削除",
      "删除回放": "リプレイを削除",
      "删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。": "削除したものはまず Windows のごみ箱に入り、復元できます。ゲーム内のコピーだけを削除するのが枠を空ける一般的な方法で、保管庫内のコピーこそが長期保存用です。",
      "删除角色皮肤？": "キャラクタースキンを削除しますか？",
      "删除皮肤包": "スキンパックを削除",
      "删除整个方案？": "構成全体を削除しますか？",
      "伤害": "ダメージ",
      "商店": "ショップ",
      "商店记录": "ショップ記録",
      "商店进店共": "ショップ来店は合計",
      "上次游戏未能应用方案：": "前回のゲームで構成を適用できませんでした：",
      "上面没有你的游戏？深度扫描某个盘（较慢，可取消）：": "上にゲームがない場合、ドライブを詳細スキャンします（時間がかかりますが、キャンセル可能）：",
      "上一帧": "前のフレーム",
      "尚未安装；游戏正在运行，本次安装重启游戏后才加载": "未インストール。ゲームが実行中のため、今回のインストールはゲーム再起動後に読み込まれます",
      "尚未安装到游戏": "ゲームには未インストール",
      "尚未安装加载器": "ローダーが未インストール",
      "尚未安装加载器 —— 点「安装加载器」，加载器与内置模组会一起装进游戏目录。": "ローダーが未インストールです —— 「ローダーをインストール」を押すと、ローダーと内蔵 Mod がまとめてゲームディレクトリにインストールされます。",
      "尚未安装加载器 / 目录为空": "ローダー未インストール / ディレクトリが空です",
      "尚未安装角色皮肤模组": "キャラクタースキン Mod は未インストール",
      "尚未安装角色皮肤模组，请从左侧管理菜单安装。": "キャラクタースキン Mod が未インストールです。左側の管理メニューからインストールしてください。",
      "尚未打开回放文件": "リプレイファイルが開かれていません",
      "尚未定位游戏目录": "ゲームディレクトリが特定されていません",
      "尚未启用": "未有効",
      "尚未选择游戏目录": "ゲームディレクトリが未選択",
      "尚未选择游戏目录（可点「自动检测」）": "ゲームディレクトリが未選択（「自動検出」を押せます）",
      "稍后再说": "あとで",
      "设为当前": "現在に設定",
      "设为当前皮肤": "現在のスキンに設定",
      "设置": "設定",
      "深度扫描": "詳細スキャン",
      "深度扫描没找到 AstralParty*.exe。": "詳細スキャンでも AstralParty*.exe が見つかりませんでした。",
      "生命": "HP",
      "生效；「": "が反映されます。「",
      "时长": "時間",
      "时长(秒)": "時間(秒)",
      "实时调整（最低 1 倍，不支持减速）。": "リアルタイムで調整できます（最低 1 倍、減速は非対応）。",
      "使用方式": "使い方",
      "使用托管引导（实验性，一般保持关闭）": "マネージドブートストラップを使用（実験的、通常はオフのまま）",
      "使用须知": "ご利用にあたって",
      "使用原版": "オリジナルを使用",
      "事件卡": "イベントカード",
      "事件卡 (": "イベントカード (",
      "事件时间线": "イベントタイムライン",
      "视频 / GIF 按选定区域裁剪为普通卡面的 404 × 400，所有帧使用同一选区。首次使用可能下载 FFmpeg。": "動画 / GIF は選択した範囲で通常カードイラストの 404 × 400 にトリミングされ、すべてのフレームが同じ範囲を使用します。初回使用時に FFmpeg をダウンロードする場合があります。",
      "适用于此人物全部原皮肤": "このキャラクターのすべての元スキンに適用",
      "适用原皮肤": "適用する元スキン",
      "手动打开任意回放文件，本地解析玩家、回合、动作、筹码流转与底层协议帧。": "任意のリプレイファイルを開き、プレイヤー、ラウンド、アクション、レリックの流れ、低レイヤーのプロトコルフレームをローカルで解析します。",
      "手牌卡": "手札カード",
      "手牌卡 (": "手札カード (",
      "售价": "価格",
      "输出伤害": "与ダメージ",
      "输出预览": "出力プレビュー",
      "输入方案名称": "構成名を入力",
      "输入角色名或皮肤名": "キャラクター名またはスキン名を入力",
      "数据统计": "データ統計",
      "数字": "数字",
      "刷新": "更新",
      "刷新方案与状态": "構成と状態を更新",
      "刷新记录": "記録を更新",
      "刷新列表": "リストを更新",
      "双击在资源管理器中打开": "ダブルクリックでエクスプローラーで開く",
      "死亡计数": "死亡回数",
      "搜索方案": "構成を検索",
      "搜索回放": "リプレイを検索",
      "搜索回放 ID / 地图 / 玩家…": "リプレイ ID / マップ / プレイヤーで検索…",
      "搜索角色 / 皮肤包": "キャラクター / スキンパックを検索",
      "搜索卡名、卡号 #101 或标识…": "カード名、カード番号 #101、識別子で検索…",
      "搜索帧号、CMD、消息名...": "フレーム番号、CMD、メッセージ名で検索...",
      "随本程序内置": "このアプリに同梱",
      "随加载器一起安装的内置模组": "ローダーと一緒にインストールされる内蔵 Mod",
      "所有数据均在本地安全离线处理": "すべてのデータはローカルで安全にオフライン処理されます",
      "它是怎么工作的？": "仕組み",
      "台词": "セリフ",
      "提交": "コミット",
      "替换": "置換",
      "替换哪些原皮肤": "置換する元スキン",
      "替换选中帧": "選択フレームを置換",
      "添加": "追加",
      "添加到多人物方案": "複数キャラ構成に追加",
      "添加到方案": "構成に追加",
      "添加第一个人物": "最初のキャラクターを追加",
      "添加或修改人物后，完全退出并重启游戏以应用搭配。": "キャラクターを追加・変更した後は、ゲームを完全に終了して再起動すると組み合わせが適用されます。",
      "添加人物皮肤": "キャラクタースキンを追加",
      "填写普通卡牌编号，或异画自己的编号。普通卡牌绑定只替换普通版；异画需要单独指定。": "通常カードの番号、または別イラスト固有の番号を入力します。通常カードへの紐付けは通常版のみを置換し、別イラストは個別に指定が必要です。",
      "条": "件",
      "条)": "件)",
      "条筹码流转": "件のレリックの流れ",
      "条事件记录": "件のイベント記録",
      "跳转到帧": "フレームへ移動",
      "停用并移入皮肤目录中的 .deleted 文件夹。": "無効化してスキンディレクトリ内の .deleted フォルダーへ移動します。",
      "停用此方案": "この構成を無効化",
      "同步配置：": "設定を同期：",
      "同步配置到游戏": "設定をゲームに同期",
      "同时也是工具箱的": "ツールボックスの",
      "图片": "画像",
      "图片预览加载失败": "画像プレビューの読み込みに失敗しました",
      "图片资源": "画像リソース",
      "退格": "Backspace",
      "托管引导程序集": "マネージドブートストラップアセンブリ",
      "拖动或用方向键移动裁剪区域": "ドラッグまたは方向キーでトリミング範囲を移動",
      "拖动选区移动位置，拖动四角调整大小。": "選択範囲をドラッグして移動、四隅をドラッグしてサイズ変更します。",
      "拖动选区移动位置，用滑块调整大小。透明背景会保留。": "選択範囲をドラッグして移動、スライダーでサイズ変更します。透明な背景は保持されます。",
      "完整立绘": "全身立ち絵",
      "完整卸载加载器": "ローダーを完全にアンインストール",
      "玩家": "プレイヤー",
      "玩家表现": "プレイヤーのパフォーマンス",
      "玩家数据对照": "プレイヤーデータ比較",
      "玩家转账贡献": "プレイヤーの送金貢献",
      "维基攻略": "攻略Wiki",
      "未安装": "未インストール",
      "未安装 · 游戏运行中": "未インストール · ゲーム実行中",
      "未保存修改": "未保存の変更",
      "未记录": "記録なし",
      "未记录骰面点数": "出目が記録されていません",
      "未检测": "未検出",
      "未检测到游戏回放目录": "ゲームのリプレイディレクトリが検出されません",
      "未检测到游戏回放目录。你仍然可以用左侧按钮直接打开任意回放文件。": "ゲームのリプレイディレクトリが検出されません。左側のボタンから任意のリプレイファイルを直接開くこともできます。",
      "未检查": "未確認",
      "未命名方案": "無名の構成",
      "未启用": "未有効",
      "未设置（用 mod 默认）": "未設定（Mod の既定値を使用）",
      "未设置按键": "キー未設定",
      "未识别": "未認識",
      "未识别的皮肤": "未認識のスキン",
      "未提供，使用原版": "未提供のためオリジナルを使用",
      "未添加游戏 —— 去设置页添加": "ゲームが未追加 —— 設定ページで追加してください",
      "未选择方案": "構成が未選択",
      "未找到匹配的卡牌": "一致するカードが見つかりません",
      "未找到原资源尺寸，使用受限的预设尺寸": "元アセットのサイズが見つからないため、制限付きのプリセットサイズを使用します",
      "未知": "不明",
      "未知版本": "不明なバージョン",
      "未知筹码": "不明なレリック",
      "文案": "テキスト",
      "文档\\AstralPartyReplays": "ドキュメント\\AstralPartyReplays",
      "文件": "ファイル",
      "文件不属于本工具": "このツールのファイルではありません",
      "文件不一致": "ファイルが一致しません",
      "文件名称:": "ファイル名:",
      "文件已选择，确认上方安装说明后点击「确认并导入」。": "ファイルを選択しました。上のインストール説明を確認してから「確認してインポート」を押してください。",
      "文件有误": "ファイルに問題があります",
      "我的方案": "マイ構成",
      "无": "なし",
      "无 —— 不是从 git 仓库构建的": "なし —— git リポジトリからのビルドではありません",
      "无法加载图片，请取消后重新选择。": "画像を読み込めません。キャンセルして選択し直してください。",
      "无法解析": "解析できません",
      "无符合条件的事件记录": "条件に合うイベント記録がありません",
      "稀有": "レア",
      "系统": "システム",
      "系统设置": "システム設定",
      "下每个模组的 sidecar（同名": "配下の各 Mod の sidecar（同名の",
      "下面这些模组，包里的版本比游戏目录里已装的更旧：": "以下の Mod は、パッケージ内のバージョンがゲームディレクトリにインストール済みのものより古いです：",
      "下一帧": "次のフレーム",
      "下载默认包": "既定パッケージをダウンロード",
      "下载默认皮肤包": "既定のスキンパックをダウンロード",
      "下载失败。": "ダウンロードに失敗しました。",
      "下载完成": "ダウンロード完了",
      "下载完成。": "ダウンロードが完了しました。",
      "下载中…": "ダウンロード中…",
      "先安装加载器": "先にローダーをインストール",
      "先安装角色皮肤模组，方案才能在游戏中生效。": "先にキャラクタースキン Mod をインストールすると、構成がゲーム内で有効になります。",
      "先给搭配起个名字，再添加喜欢的人物皮肤。": "まず組み合わせに名前を付け、次にお気に入りのキャラクタースキンを追加します。",
      "显示": "表示",
      "显示控制台窗口（模组日志）": "コンソールウィンドウを表示（Mod ログ）",
      "显示名称": "表示名",
      "相机工具 / 活动日志 / 模组管理 / 权限声明 / 游戏内热键变速…": "カメラツール / アクティビティログ / Mod 管理 / 権限宣言 / ゲーム内ホットキー変速…",
      "详情": "詳細",
      "想要新功能": "新機能を希望する",
      "项目主页": "プロジェクトページ",
      "消息类型": "メッセージ種別",
      "校验并保存清单": "マニフェストを検証して保存",
      "协议命令统计": "プロトコルコマンド統計",
      "协议载荷解码详情": "プロトコルペイロードのデコード詳細",
      "协议帧共": "プロトコルフレームは合計",
      "写入": "書き込み",
      "卸载：": "アンインストール：",
      "卸载变速器": "変速機をアンインストール",
      "卸载加载器": "ローダーをアンインストール",
      "卸载将删除加载器及 AstralParty_ModLoader 目录，包括模组、配置和日志。此操作无法通过工具撤销。": "アンインストールするとローダーと AstralParty_ModLoader ディレクトリ（Mod、設定、ログを含む）が削除されます。この操作はツールから取り消せません。",
      "卸载中…": "アンインストール中…",
      "新版本": "新バージョン",
      "新建第一个方案": "最初の構成を作成",
      "新建方案": "構成を作成",
      "新建皮肤方案": "スキン構成を作成",
      "新建一个方案，再添加人物皮肤。之后可以一键切换整套搭配。": "新しい構成を作成してからキャラクタースキンを追加します。後から組み合わせ全体をワンクリックで切り替えられます。",
      "新建一个角色皮肤包，或导入 ZIP 开始制作。": "新しいキャラクタースキンパックを作成するか、ZIP をインポートして制作を始めます。",
      "新建自定义皮肤": "カスタムスキンを作成",
      "新建自定义皮肤包": "カスタムスキンパックを作成",
      "新写入的 version.dll 要重启游戏才会加载，且运行中的游戏可能占用该文件导致覆盖失败。": "新しく書き込んだ version.dll はゲームを再起動するまで読み込まれず、実行中のゲームがファイルを使用していると上書きに失敗する場合があります。",
      "新增击杀": "キル増加",
      "新增伤害": "ダメージ増加",
      "新增转账": "送金増加",
      "星币": "スターコイン",
      "星币 · 击杀": "スターコイン · キル",
      "修复点“创建 / 加入房间”没反应（大厅匹配改为直接返回空结果）": "「ルーム作成 / 参加」を押しても反応しない問題を修正（ロビーマッチを空の結果を直接返す方式に変更）",
      "修复退房 / 被踢时报错（房间列表查询返回空列表）": "退室 / キック時にエラーになる問題を修正（ルーム一覧の問い合わせが空リストを返すように）",
      "修改后完全退出并重启游戏生效。图片、文案和地图动画已提供；媒体、特效及特殊演出保留原版，运行时效果待游戏验证。": "変更後はゲームを完全に終了して再起動すると反映されます。画像、テキスト、マップアニメーションは提供済みです。メディア、エフェクト、特殊演出はオリジナルのままで、実行時の見た目はゲームでの検証が必要です。",
      "修改时间:": "更新日時:",
      "修改显示名称、作者和方案描述": "表示名、作者、構成の説明を変更",
      "修饰键": "修飾キー",
      "需要修复": "要修復",
      "选区保持普通卡面的 404 : 400 比例，所有视频帧使用同一裁剪范围。": "選択範囲は通常カードイラストの 404 : 400 の比率を保ち、すべての動画フレームが同じトリミング範囲を使用します。",
      "选区边长（原图像素）": "選択範囲の辺長（元画像ピクセル）",
      "选区大小": "選択範囲のサイズ",
      "选区宽度（原图像素）": "選択範囲の幅（元画像ピクセル）",
      "选区始终为正方形。": "選択範囲は常に正方形です。",
      "选取你的卡面": "カードイラストを選択",
      "选择": "選択",
      "选择…": "選択…",
      "选择按键组合 ·": "キーの組み合わせを選択 ·",
      "选择当前正在操作的游戏": "現在操作中のゲームを選択",
      "选择动画帧": "アニメーションフレームを選択",
      "选择发布包或粘贴完整路径": "リリースパッケージを選択するかフルパスを貼り付け",
      "选择回放文件": "リプレイファイルを選択",
      "选择皮肤方案：": "スキン構成を選択：",
      "选择视频 / GIF": "動画 / GIF を選択",
      "选择文件…": "ファイルを選択…",
      "选择文件后，点击「确认并导入」才会安装。SDK 工具包或单独 SDK DLL 仅更新 SDK。": "ファイルを選択した後、「確認してインポート」を押すとインストールされます。SDK ツールキットまたは単体の SDK DLL は SDK のみを更新します。",
      "选择要启动的游戏版本": "起動するゲームバージョンを選択",
      "选择要深度扫描的盘符": "詳細スキャンするドライブを選択",
      "选择一个皮肤包，或新建方案开始制作。": "スキンパックを選択するか、新しい構成を作成して制作を始めてください。",
      "选择已有皮肤包、制作自定义皮肤，或搭配官方皮肤。": "既存のスキンパックを選ぶか、カスタムスキンを作成するか、公式スキンを組み合わせます。",
      "循环": "ループ",
      "沿用原皮肤": "元のスキンを引き継ぐ",
      "要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；": "CesiumLoader に切り替えるには、下の「ゲームディレクトリ内のこのツールのものではない version.dll の上書きを許可する」にチェックを入れ、さらに「ローダーをインストール」を押します；",
      "也包括变速本身": "変速自体も含む",
      "也可以导入新的皮肤包。": "新しいスキンパックをインポートすることもできます。",
      "依赖：": "依存関係：",
      "移除": "削除",
      "移除此人物规则？": "このキャラクターのルールを削除しますか？",
      "移动点数": "移動ポイント",
      "移动骰子统计": "移動サイコロ統計",
      "已安装": "インストール済み",
      "已安装 · 游戏运行中": "インストール済み · ゲーム実行中",
      "已安装 v": "インストール済み v",
      "已安装，但这一份没有版本清单，读不出准确版本 —— 重新安装一次即可纳入版本管理。": "インストール済みですが、このファイルにはバージョンマニフェストがないため正確なバージョンを読み取れません —— 再インストールするとバージョン管理の対象になります。",
      "已安装（内置版本号读取失败）": "インストール済み（内蔵バージョン番号の読み取りに失敗）",
      "已安装的模组": "インストール済みの Mod",
      "已持有": "所持済み",
      "已存在，继续将覆盖它。": "既に存在します。続行すると上書きされます。",
      "已禁用": "無効",
      "已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近": "自動アーカイブを有効にしました：以後このツールを開くたびに、枠を超えた古いリプレイが自動で保管庫へ保管されます（ゲーム内には直近",
      "已启用": "有効",
      "已启用 · 重启游戏后生效": "有効 · ゲーム再起動後に反映",
      "已切换语言": "言語を切り替えました",
      "已设为": "設定しました：",
      "已设为当前方案": "現在の構成に設定しました",
      "已是最新版本 v": "最新バージョン: v",
      "已售出": "売却済み",
      "已添加": "追加済み",
      "已停用": "無効化済み",
      "已停用或缺失，恢复时使用此版本": "無効化済みまたは欠落。復元時はこのバージョンを使用します",
      "已同步": "同期済み",
      "已完成": "完了",
      "已下载": "ダウンロード済み",
      "已应用": "適用済み",
      "已有皮肤包": "既存のスキンパック",
      "已终止": "中止しました",
      "已装的 v": "インストール済みの v",
      "已自定义": "カスタム済み",
      "以后自动保持最近 10 局": "以後、直近10対局を自動で保持",
      "异画道具": "別イラストアイテム",
      "异画道具 (": "別イラストアイテム (",
      "引导方法": "ブートストラップ方法",
      "引导类型": "ブートストラップ種別",
      "引导与超时（一般不用改）": "ブートストラップとタイムアウト（通常は変更不要）",
      "应用版本": "アプリのバージョン",
      "应用变速": "変速を適用",
      "应用此方案": "この構成を適用",
      "应用失败": "適用に失敗しました",
      "用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。": "与ダメージ、支援、行動データで対局の流れを比較します。役割はキャラクターごとに異なるため、総与ダメージだけでプレイヤーを評価しません。",
      "用右侧「浏览添加」手动选，或「扫描系统」自动找。": "右側の「参照して追加」で手動選択するか、「システムをスキャン」で自動検出します。",
      "游戏变速": "ゲーム変速",
      "游戏大厅": "ロビー",
      "游戏工具": "ゲームツール",
      "游戏加速（变速器）": "ゲーム高速化（変速機）",
      "游戏进程未运行，安装 / 卸载会立刻生效": "ゲームプロセスが実行されていないため、インストール/アンインストールはすぐに反映されます",
      "游戏默认方案仅使用游戏内部卡面，无需外部图片。": "ゲームの既定構成はゲーム内部のカードイラストのみを使用し、外部画像は不要です。",
      "游戏默认方案没有可编辑的方案信息": "ゲームの既定構成には編集できる構成情報がありません",
      "游戏目录 · 覆盖与降级 · SDK 依赖 · 卸载": "ゲームディレクトリ · 上書きとダウングレード · SDK 依存関係 · アンインストール",
      "游戏目录 speedhack_config.json 存在": "ゲームディレクトリに speedhack_config.json があります",
      "游戏目录 version.dll 与内置不一致——可能来自其它工具": "ゲームディレクトリの version.dll が内蔵と一致しません —— 別のツール由来の可能性があります",
      "游戏目录 version.dll 与内置一致": "ゲームディレクトリの version.dll は内蔵と一致しています",
      "游戏目录（多个位置）": "ゲームディレクトリ（複数の場所）",
      "游戏目录（version.dll 的安装位置）": "ゲームディレクトリ（version.dll のインストール先）",
      "游戏目录里的 version.dll 不是本工具安装的（可能是旧版独立变速器或第三方文件）——安装或更新前请先确认来源。": "ゲームディレクトリの version.dll はこのツールがインストールしたものではありません（旧版単体変速機や第三者ファイルの可能性があります）—— インストールまたは更新の前に入手元を確認してください。",
      "游戏目录里的 version.dll 不是本工具装的": "ゲームディレクトリの version.dll はこのツールがインストールしたものではありません",
      "游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：": "ゲームディレクトリの version.dll はこのアプリ内蔵のものでも、インストールマニフェストに記録されたものでもありません：",
      "游戏目录里暂无 version.dll，可直接安装": "ゲームディレクトリに version.dll がないため、そのままインストールできます",
      "游戏目录暂无 speedhack_config.json": "ゲームディレクトリに speedhack_config.json がありません",
      "游戏内": "ゲーム内",
      "游戏内保留局数": "ゲーム内に保持する対局数",
      "游戏内回放席位快满了（": "ゲーム内のリプレイ枠がほぼ満杯です（",
      "游戏内回放席位已满": "ゲーム内のリプレイ枠が満杯です",
      "游戏内回放席位已满（": "ゲーム内のリプレイ枠が満杯です（",
      "游戏内席位": "ゲーム内の枠",
      "游戏启动后自动以指定倍速加速一段时长（跳过开场动画）": "ゲーム起動後に指定倍速で一定時間加速します（オープニングをスキップ）",
      "游戏启动中，稍候即可再切换启动方式": "ゲーム起動中です。しばらく待ってから起動方法を切り替えてください",
      "游戏维基（Starengine Wiki）": "ゲームWiki（Starengine Wiki）",
      "游戏运行中也能安装 / 卸载，但": "ゲーム実行中でもインストール/アンインストールできますが、",
      "游戏在「本地回放」列表里遇到这种目录会把它整个删掉": "ゲームは「ローカルリプレイ」一覧でこの種のディレクトリを見つけると丸ごと削除します",
      "游戏正在运行：安装 / 更新 / 卸载都要重启游戏才生效。": "ゲームが実行中です：インストール / 更新 / アンインストールはすべてゲーム再起動後に反映されます。",
      "游戏正在运行：归档/放回随时可用，但别在游戏里正播着回放时操作": "ゲームが実行中です：保管/戻すはいつでも可能ですが、ゲーム内でリプレイを再生している最中は操作しないでください",
      "游戏中的角色皮肤模组为": "ゲーム内のキャラクタースキン Mod は",
      "游戏自带的「本地回放」只有": "ゲーム標準の「ローカルリプレイ」は",
      "有新版本 v": "新しいバージョン: v",
      "右Alt": "右Alt",
      "右Ctrl": "右Ctrl",
      "右Shift": "右Shift",
      "与「已禁用」状态仍会保留， 被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。": "「無効」状態も保持され、置き換えられた旧ファイルはごみ箱へ移動します（ごみ箱が使えない場合は元のファイルを保持）。アップグレードだけが目的の場合は、より新しい Mod パッケージを使用してください。",
      "与随程序内置的版本一致": "アプリ内蔵のバージョンと一致しています",
      "原理：把内置": "仕組み：内蔵の",
      "原始协议帧": "生のプロトコルフレーム",
      "原图": "元画像",
      "源码提交": "ソースのコミット",
      "远野汉娜": "遠野ハンナ",
      "允许安装较旧版本（降级）": "古いバージョンのインストール（ダウングレード）を許可",
      "允许把已装的加载器降级成内置的较旧版本": "インストール済みのローダーを内蔵の古いバージョンへダウングレードすることを許可",
      "允许覆盖不属于本工具的 version.dll": "このツールのものではない version.dll の上書きを許可",
      "允许覆盖游戏目录里不属于本工具的 version.dll": "ゲームディレクトリ内のこのツールのものではない version.dll の上書きを許可",
      "允许覆盖游戏目录里其它 version.dll（用于强行更新安装）": "ゲームディレクトリ内の他の version.dll の上書きを許可（強制更新インストール用）",
      "允许游戏内热键变速（Delete 在 1.0x 与刚才的倍率之间切换，Alt+= / Alt+- 调倍率）": "ゲーム内ホットキー変速を許可（Delete で 1.0x と直前に使った倍率を切り替え、Alt+= / Alt+- で倍率を調整）",
      "运行环境": "実行環境",
      "运行时": "ランタイム",
      "再次导入": "再インポート",
      "再在游戏里点「保存」会提示已达上限。归档最旧的几局就能立刻腾出席位——回放不会丢，它们会进上面的回放库。": "さらにゲーム内で「保存」を押すと上限に達したと表示されます。古い対局をいくつか保管すればすぐに枠が空きます —— リプレイは失われず、上のリプレイ保管庫に入ります。",
      "在 AstralParty.Toys 程序窗口里打开这一页才能查看更新日志。": "AstralParty.Toys のアプリウィンドウでこのページを開くと更新履歴を確認できます。",
      "在 AstralParty.Toys 程序窗口里打开这一页就能看到版本信息。": "AstralParty.Toys のアプリウィンドウでこのページを開くとバージョン情報を確認できます。",
      "在系统默认浏览器中打开": "システムの既定のブラウザーで開く",
      "在资源管理器中打开": "エクスプローラーで開く",
      "载荷大小:": "ペイロードサイズ:",
      "载荷字节": "ペイロードバイト",
      "暂停": "一時停止",
      "暂无方案描述": "構成の説明はありません",
      "暂无描述说明": "説明はありません",
      "暂无统计数据": "統計データはありません",
      "窄幅角色图": "細幅キャラクター画像",
      "战斗半身图 1": "戦闘バストアップ 1",
      "战斗半身图 2": "戦闘バストアップ 2",
      "战绩里那一长串数字": "戦績にある長い数字の並び",
      "张卡面": "枚のカードイラスト",
      "账号背景": "アカウント背景",
      "找到": "検出",
      "这份安装没有版本清单，重新安装一次即可记录": "このインストールにはバージョンマニフェストがないため、再インストールすると記録されます",
      "这个键不支持，请换一个": "このキーは使用できません。別のキーを選んでください",
      "这个目录已经不在了": "このディレクトリは既に存在しません",
      "这个鼠标键不支持，请换一个": "このマウスボタンは使用できません。別のものを選んでください",
      "这是本地构建：版本号来自项目文件，提交号与文件时间说明它对应哪一次源码。": "これはローカルビルドです。バージョン番号はプロジェクトファイルに由来し、コミット番号とファイル日時からどのソースに対応するかが分かります。",
      "这些回放解析不出结算帧": "これらのリプレイはリザルトフレームを解析できません",
      "帧": "フレーム",
      "帧 ·": "フレーム ·",
      "帧\" title=\"": "フレーム\" title=\"",
      "帧/秒 ·": "フレーム/秒 ·",
      "帧号:": "フレーム番号:",
      "帧预览": "フレームプレビュー",
      "正在保存…": "保存中…",
      "正在处理": "処理中",
      "正在处理，请稍候": "処理中です。しばらくお待ちください",
      "正在从 GitHub 拉取最新加载器更新日志…": "GitHub から最新のローダー更新履歴を取得中…",
      "正在读取…": "読み取り中…",
      "正在读取版本信息…": "バージョン情報を読み取り中…",
      "正在读取更新日志…": "更新履歴を読み取り中…",
      "正在读取加载器更新日志…": "ローダーの更新履歴を読み取り中…",
      "正在检测…": "検出中…",
      "正在检测安装状态…": "インストール状態を検出中…",
      "正在检测回放目录…": "リプレイディレクトリを検出中…",
      "正在解码 Protobuf 载荷…": "Protobuf ペイロードをデコード中…",
      "正在解析回放…": "リプレイを解析中…",
      "正在清理…": "クリーンアップ中…",
      "正在取消下载…": "ダウンロードをキャンセル中…",
      "正在统计事件…": "イベントを集計中…",
      "正在下载…": "ダウンロード中…",
      "正在校验并安装发布包…": "リリースパッケージを検証してインストール中…",
      "正在终止": "中止しています",
      "正在转换…": "変換中…",
      "正在准备资源": "リソースを準備中",
      "支持 MP4、WebM、MOV、AVI、MKV 和 GIF，最多 20 秒。先裁剪为 404 × 400，再在普通卡框中循环播放。": "MP4、WebM、MOV、AVI、MKV、GIF に対応。最大 20 秒。まず 404 × 400 にトリミングし、通常のカード枠内でループ再生します。",
      "支持简体中文、English、日本語、繁體中文": "简体中文、English、日本語、繁體中文 に対応",
      "支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。": "元のスプライト名マッピング SpriteMappings とカスタムリソース URL asset: に対応。メディアファイルはパッケージに保存できますが、現在は適用されません。",
      "知道回放 ID（游戏战绩里那一串数字）时，直接从官方回放服务器下载进回放库": "リプレイ ID（ゲームの戦績にある数字の並び）が分かる場合は、公式リプレイサーバーからリプレイ保管庫へ直接ダウンロードできます",
      "知道了，这一版不再提示": "了解しました。このバージョンでは今後表示しません",
      "直接启动游戏主程序，完全不经过 Steam；会打开加载器的 Steam 绕过（没装 Steam 也能进游戏），代价是 Steam 大厅 / 好友邀请进房失效": "ゲーム本体を直接起動し、Steam を一切経由しません。ローダーの Steam バイパスが有効になります（Steam 未インストールでも起動可能）。代わりに Steam ロビー / フレンド招待からの入室は使えなくなります",
      "只能装一个": "1つしかインストールできません",
      "只删库内副本（游戏里仍能看）": "保管庫内のコピーのみ削除（ゲーム内では引き続き視聴可能）",
      "只删游戏内副本（腾出席位）": "ゲーム内のコピーのみ削除（枠を空ける）",
      "只想清掉就勾选「强制卸载」。": "完全に消したい場合は「強制アンインストール」にチェックを入れてください。",
      "只想要变速": "変速だけを使いたい",
      "只想用旧版变速器？忽略这段，照下面操作即可。": "旧版変速機だけを使いたい場合は、この説明を無視して下の手順に従ってください。",
      "只修改方案对外展示的信息，不会改变方案目录名": "構成の表示情報のみを変更します。構成のディレクトリ名は変わりません",
      "只移除当前方案中的规则，自定义皮肤包会保留。": "現在の構成からルールを削除するだけで、カスタムスキンパックは保持されます。",
      "指标": "指標",
      "治疗": "回復",
      "终止任务": "タスクを中止",
      "重启游戏": "ゲームを再起動",
      "重启游戏后": "ゲーム再起動後",
      "重试导入": "インポートを再試行",
      "重新安装内置版本": "内蔵バージョンを再インストール",
      "重新导入帧序列会替换整个动作；替换选中帧保留帧数、顺序和播放参数。": "フレームシーケンスを再インポートするとアクション全体が置き換わります。選択フレームの置換では、フレーム数・順序・再生パラメータが保持されます。",
      "重新载入维基页面": "Wiki ページを再読み込み",
      "重载配置热键": "設定リロード用ホットキー",
      "重载配置热键 reload_config_keys": "設定リロード用ホットキー reload_config_keys",
      "重置筛选条件": "絞り込み条件をリセット",
      "逐回合数据表": "ラウンド別データ表",
      "主配置：": "メイン設定：",
      "注册表": "レジストリ",
      "注意：这个「变速器」和模组系统": "注意：この「変速機」と Mod システムは",
      "专业信息": "専門情報",
      "转出": "送金",
      "转换并保存": "変換して保存",
      "转换动态卡面": "動くカードイラストに変換",
      "转账": "送金",
      "转账贡献": "送金貢献",
      "转账星币": "送金スターコイン",
      "准备界面": "準備画面",
      "字节": "バイト",
      "字母": "英字",
      "自定义": "カスタム",
      "自定义 + 官方基础": "カスタム + 公式ベース",
      "自定义皮肤": "カスタムスキン",
      "自定义皮肤包": "カスタムスキンパック",
      "自动保持游戏内最近": "ゲーム内の直近",
      "自动保持游戏内最近 10 局": "ゲーム内の直近10対局を自動保持",
      "自动倍速": "自動倍速",
      "自动倍速倍率": "自動倍速の倍率",
      "自动检测": "自動検出",
      "自动托管已": "自動アーカイブを",
      "自动托管已开启（保留": "自動アーカイブが有効です（保持",
      "总承伤": "総被ダメージ",
      "总伤害": "総与ダメージ",
      "左Alt": "左Alt",
      "左Ctrl": "左Ctrl",
      "左Shift": "左Shift",
      "作者": "作者",
      "作者:": "作者:",
      "作者：": "作者：",
      "作者: —": "作者: —",
      "作者签名（可选）：": "作者の署名（任意）：",
      "AstralParty.Toys · 吉星派对工具箱": "AstralParty.Toys · Astral Party ツールボックス",
      "CardSkinMod · 方案管理 / 导入 / 单卡可视化替换": "CardSkinMod · 構成管理 / インポート / カード単位のビジュアル置換",
      "CesiumLoader · 加载器 / 模组 / 变速": "CesiumLoader · ローダー / Mod / 変速",
      "com.unity.addressables 目录": "com.unity.addressables ディレクトリ",
      "DLL 缺失": "DLL がありません",
      "DLL 缺失，无法确定恢复版本": "DLL がないため、復元するバージョンを特定できません",
      "GitHub 最新": "GitHub 最新",
      "GitHub 最新 Release": "GitHub の最新 Release",
      "HUD 角色图": "HUD キャラクター画像",
      "Mod 加载器": "Mod ローダー",
      "Mod 加载器与模组管理": "Mod ローダーと Mod 管理",
      "PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。": "PNG/JPG フレームはファイル名順に並びます。待機 Idle、前向き Walk、後ろ向き Walk-back。その他のアクション名は元のキャラクターに準じます。",
      "PVE商店": "PVEショップ",
      "SDK 版本（只读，随包更新）": "SDK バージョン（読み取り専用、パッケージと共に更新）",
      "sdk 目录为空（重新安装加载器会写入 CesiumLoader.SDK.dll）": "sdk ディレクトリが空です（ローダーを再インストールすると CesiumLoader.SDK.dll が書き込まれます）",
      "SDK 依赖（sdk\\ 目录）": "SDK 依存関係（sdk\\ ディレクトリ）",
      "SFW 半身图 1": "SFW バストアップ 1",
      "SFW 半身图 2": "SFW バストアップ 2",
      "SFW 立绘": "SFW 立ち絵",
      "SFW 缩略图": "SFW サムネイル",
      "SFW 窄幅图": "SFW 細幅画像",
      "SFW 准备图": "SFW 準備画像",
      "speedhack_config.json 配置": "speedhack_config.json 設定",
      "Steam 绕过细节 —— 要从 Steam 启动并使用真大厅，这三个开关必须全部关闭": "Steam バイパスの詳細 —— Steam から起動して本物のロビーを使うには、この3つのスイッチをすべてオフにする必要があります",
      "Task 构造方式（auto / direct / invoke，一般保持 auto）": "Task の構築方法（auto / direct / invoke、通常は auto のまま）",
      "Unity 结构": "Unity 構造",
      "version.dll 已被游戏占用，删除可能失败；重启游戏后才恢复正常速度。": "version.dll がゲームに使用されているため、削除に失敗する可能性があります。ゲームを再起動すると通常速度に戻ります。",
      "version.dll 已停用或缺失，保留版本": "version.dll は無効化済みまたは欠落。バージョンは保持",
      "x（重启游戏生效）": "x（ゲーム再起動後に反映）",
      "。修改配置后，重启游戏并刷新可查看新结果。": "。設定を変更した後、ゲームを再起動して更新すると新しい結果を確認できます。",
      "个筹码：": " 個のレリック：",
      "决胜": "決着",
      "可操作游戏": "ゲーム操作可能",
      "处理进度": "処理の進行状況",
      "拉取最新": "最新を取得",
      "拉取最新」重试。": "最新を取得」を押して再試行してください。",
      "曲线": "グラフ",
      "最近一次 Mod 记录：": "最新の Mod 記録：",
      "目录已失效": "ディレクトリが見つかりません",
      "预览": "プレビュー",
      "(未选择)": "（未選択）",
      "- 动作类型: 【最终选定筹码】": "- アクション種別: 【最終選択レリック】",
      "- 动作类型: 【首次呈现候选】": "- アクション種別: 【初回候補表示】",
      "- 选定筹码品质分布:": "- 選択レリックのレアリティ分布:",
      "- 选定筹码来源分布:": "- 選択レリックの入手元分布:",
      "1. 【流派与契合度评估】请结合每位玩家的角色技能与定位，分析他们选取的筹码是否形成了有效联动（Synergy）？有没有错失更优质的候选筹码？": "1. 【ビルドと相性の評価】各プレイヤーのキャラクタースキルと役割を踏まえ、選んだレリックが有効なシナジーを形成しているか分析してください。より良い候補を逃していないかも確認してください。",
      "2. 【刷新时机与策略分析】请评价各位玩家在各回合中的刷新决策：是否存在盲目刷新浪费机会，或是在关键轮次赌出了核心关键筹码？": "2. 【リロールのタイミングと戦略】各プレイヤーの各ラウンドのリロール判断を評価してください。無駄なリロールで機会を逃していないか、重要なラウンドで狙って中核レリックを引き当てたかを見てください。",
      "4. 【改进建议】如果给表现落后或伤害不足的玩家提供改进策略，在筹码获取和资源配置上应该做出哪些针对性调整？": "4. 【改善提案】成績が伸びない、またはダメージが不足しているプレイヤーに対して、レリックの獲得とリソース配分をどう調整すべきか具体的に提案してください。",
      "AstralParty_ModLoader\\doorstop_config.json 里的 speedhackBaseSpeed 即可变速。": "AstralParty_ModLoader\\doorstop_config.json の speedhackBaseSpeed で変速できます。",
      "CesiumLoader 还没有发布过任何 Release（打 modloader-* tag 触发 GitHub Action 即可）。": "CesiumLoader はまだ Release を1つも公開していません（modloader-* タグを打って GitHub Action を実行してください）。",
      "FPS 必须大于零": "FPS は 0 より大きい必要があります",
      "GIF 已保存：": "GIF を保存しました：",
      "SDK 不是有效的托管程序集。": "SDK は有効なマネージドアセンブリではありません。",
      "SDK 不是有效的程序集。": "SDK は有効なアセンブリではありません。",
      "SDK 压缩包中的文件为空或超过 16 MB。": "SDK 圧縮ファイル内のファイルが空か、16 MB を超えています。",
      "SDK 导入文件不能超过 64 MB。": "SDK のインポートファイルは 64 MB を超えられません。",
      "SDK 文件为空或超过 16 MB。": "SDK ファイルが空か、16 MB を超えています。",
      "SDK 的 PDB 符号与 DLL 不属于同一次构建。": "SDK の PDB シンボルが DLL と同じビルドのものではありません。",
      "SDK 符号文件过大。": "SDK のシンボルファイルが大きすぎます。",
      "SDK 解压后的文件超过 16 MB。": "SDK を展開したファイルが 16 MB を超えています。",
      "Steam 绕过主开关: 无 Steam 启动游戏(拦截 SteamManager.Awake)": "Steam バイパスのメインスイッチ: Steam なしでゲームを起動（SteamManager.Awake をインターセプト）",
      "Texture2D 尺寸超出 Unity 上限（最长边 16384）。": "Texture2D のサイズが Unity の上限（最長辺 16384）を超えています。",
      "UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：": "UnityFS のサイズ宣言が無効か、2 GiB を超えてメモリに読み込めません：",
      "UnityFS 头不完整：": "UnityFS ヘッダーが不完全です：",
      "UnityFS 存在无法解释的尾部数据，拒绝修复：": "UnityFS に解釈できない末尾データがあるため、修復を拒否しました：",
      "UnityFS 版本头无效：": "UnityFS のバージョンヘッダーが無効です：",
      "ZIP 不允许符号链接。": "ZIP にシンボリックリンクは使用できません。",
      "ZIP 中没有 Unity .bundle 卡牌资源。": "ZIP に Unity の .bundle カードリソースがありません。",
      "ZIP 中没有 Unity .bundle 资源。": "ZIP に Unity の .bundle リソースがありません。",
      "ZIP 包含未引用的文件": "ZIP に参照されていないファイルが含まれています",
      "ZIP 存在重复路径": "ZIP に重複したパスがあります",
      "ZIP 存在重复路径：": "ZIP に重複したパスがあります：",
      "ZIP 必须包含且仅包含一份 character-skin.json": "ZIP には character-skin.json がちょうど1つ含まれている必要があります",
      "ZIP 文件与目录路径冲突：": "ZIP のファイルとディレクトリのパスが競合しています：",
      "ZIP 文件长度不完整：": "ZIP のファイル長が不完全です：",
      "ZIP 解压大小超过声明。": "ZIP の展開後サイズが宣言を超えています。",
      "ZIP 路径无效：": "ZIP のパスが無効です：",
      "[★ 最终首领击杀者/MVP]": "[★ 最終ボス撃破者/MVP]",
      "mod 包不能超过 64 MB。": "Mod パッケージは 64 MB を超えられません。",
      "mod 包内文件数量超过限制。": "Mod パッケージ内のファイル数が上限を超えています。",
      "mod 包必须是 .zip 文件。": "Mod パッケージは .zip ファイルである必要があります。",
      "mod 包解压后超过大小限制。": "Mod パッケージの展開後サイズが上限を超えています。",
      "mod 必须是 .dll 文件（模组包请用 .zip）。": "Mod は .dll ファイルである必要があります（Mod パッケージは .zip を使用してください）。",
      "skin.json 根节点不是对象": "skin.json のルートノードがオブジェクトではありません",
      "skin.json 格式无效，请先修复后再编辑方案信息": "skin.json の形式が無効です。修復してから構成情報を編集してください",
      "version.dll 已停用或缺失，模组与配置已保留；点击恢复，缺少本地备份时会下载当前版本的加载器包。": "version.dll は無効化済みまたは欠落しています。Mod と設定は保持されています。復元をクリックしてください。ローカルバックアップがない場合は現在のバージョンのローダーパッケージをダウンロードします。",
      "version.dll 已缺失；其他文件未改动，可点击恢复。": "version.dll がありません。他のファイルは変更されていません。復元をクリックできます。",
      "version.dll 来源不明，不能作为 CesiumLoader 停用。请先确认文件来源。": "version.dll の入手元が不明なため、CesiumLoader として無効化できません。ファイルの入手元を確認してください。",
      "version.dll 缺失且版本未知，请重新安装或更新完整加载器。": "version.dll がなくバージョンも不明です。完全なローダーを再インストールするか更新してください。",
      "{ // 配置说明见对应 mod 文档; 公开字段(public field)才会被读取 }": "{ // 設定の説明は該当 Mod のドキュメントを参照。public フィールドのみ読み取られます }",
      "· 卡面转换导入": "· カードイラスト変換インポート",
      "· 转换导入": "· 変換インポート",
      "。可将此皮肤包添加到人物方案。": "。このスキンパックをキャラクター構成に追加できます。",
      "。确认要降级后再导入（玩家配置与停用状态仍会保留）。": "。ダウングレードすることを確認してからインポートしてください（プレイヤー設定と無効化状態は保持されます）。",
      "一、对局背景基本信息": "一、対局の基本情報",
      "三、全局筹码统计摘要": "三、全体レリック統計の概要",
      "下载内容超过大小限制。": "ダウンロード内容がサイズ上限を超えています。",
      "下载包版本与当前安装版本不一致。": "ダウンロードしたパッケージのバージョンが現在のインストールバージョンと一致しません。",
      "下载期间安装状态发生变化，请刷新后重试。": "ダウンロード中にインストール状態が変化しました。更新してから再試行してください。",
      "下载默认包失败：": "既定パッケージのダウンロードに失敗しました：",
      "不支持或重复的视频资源：": "未対応または重複した動画リソース：",
      "不支持的 MonoBehaviour：": "未対応の MonoBehaviour：",
      "不支持的皮肤方案版本": "未対応のスキン構成バージョン",
      "不支持的角色皮肤格式版本": "未対応のキャラクタースキン形式バージョン",
      "不支持的角色资源类型：": "未対応のキャラクターリソース種別：",
      "不是 UnityFS bundle：": "UnityFS bundle ではありません：",
      "个追加字节；原文件保存在 source/。": "個の追加バイト。元のファイルは source/ に保存されています。",
      "二、参战玩家与角色战绩概况": "二、参戦プレイヤーとキャラクター成績の概要",
      "五、供 AI 智能分析与复盘的建议 Prompt 方向": "五、AI による分析・復盤のための推奨プロンプトの方向性",
      "人物已变更，请从方案重新添加皮肤包": "キャラクターが変更されました。構成からスキンパックを追加し直してください",
      "人物皮肤包已创建并添加到方案": "キャラクタースキンパックを作成し、構成に追加しました",
      "人物规则不存在，请刷新方案": "キャラクターのルールが存在しません。構成を更新してください",
      "人物配置为空": "キャラクター設定が空です",
      "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。": "version.dll のみ削除しました。Mod、設定、SDK、ログは保持されています。",
      "仅使用游戏内部卡面，停用外部图片。重启游戏后生效。": "ゲーム内部のカードイラストのみを使用し、外部画像を無効化します。ゲーム再起動後に反映されます。",
      "仅支持静态 Texture2D 替换包；包内含动画、精灵或其他资源，不能部分导入。": "静的な Texture2D 置換パッケージのみ対応しています。パッケージにアニメーション、スプライト、その他のリソースが含まれている場合は部分インポートできません。",
      "从 Toys 导出的皮肤方案": "Toys からエクスポートしたスキン構成",
      "从旧式 Unity bundle 卡牌替换包转换。原作者文件和原 bundle 保存在 source/。": "旧形式の Unity bundle カード置換パッケージから変換しました。原作者のファイルと元の bundle は source/ に保存されています。",
      "从旧式 Unity bundle 皮肤包转换。原作者说明和原文件保存在 source/。": "旧形式の Unity bundle スキンパックから変換しました。原作者の説明と元ファイルは source/ に保存されています。",
      "任务已完成，皮肤列表刷新失败：": "タスクは完了しましたが、スキン一覧の更新に失敗しました：",
      "作者最长 100 个字符": "作者名は最大 100 文字です",
      "你可以直接复制以下 Prompt 向大语言模型提问：": "以下のプロンプトをそのままコピーして大規模言語モデルに質問できます：",
      "保存卡面方案与原作者文件": "カードイラスト構成と原作者ファイルを保存",
      "保存旧包转换结果": "旧パッケージの変換結果を保存",
      "倍速档位的速度必须大于 0。": "倍速プリセットの速度は 0 より大きい必要があります。",
      "全局 / 开局阶段": "全体 / 開始フェーズ",
      "内置角色模组缺失": "内蔵キャラクター Mod がありません",
      "初始化本地界面": "ローカル画面を初期化",
      "删除 version.dll 失败，请退出游戏并检查文件权限后重试。": "version.dll の削除に失敗しました。ゲームを終了し、ファイルの権限を確認してから再試行してください。",
      "加载器与变速器共用 version.dll，不能同时安装：装变速器会覆盖加载器，所有 mod 将失效。": "ローダーと変速機は version.dll を共用するため同時にインストールできません。変速機をインストールするとローダーが上書きされ、すべての Mod が無効になります。",
      "加载器发布包": "ローダーのリリースパッケージ",
      "加载器已卸载（游戏恢复正常）。": "ローダーをアンインストールしました（ゲームは正常に戻りました）。",
      "加载器已卸载；当前正在运行的游戏进程仍加载着加载器，重启游戏后恢复正常。": "ローダーをアンインストールしました。現在実行中のゲームプロセスにはまだローダーが読み込まれています。ゲームを再起動すると正常に戻ります。",
      "加载器已安装到游戏目录。启动游戏后会自动加载 mods\\ 目录下的所有 mod": "ローダーをゲームディレクトリにインストールしました。ゲームを起動すると mods\\ ディレクトリ内のすべての Mod が自動で読み込まれます",
      "加载器已完整卸载，请重新安装。": "ローダーは完全にアンインストールされました。再インストールしてください。",
      "加载器操作正在进行，请稍候。": "ローダーの操作を実行中です。しばらくお待ちください。",
      "加载器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载 mod。": "ローダーファイルをゲームディレクトリに書き込みました。ゲームが実行中のため、すぐには反映されません —— ゲームを再起動すると Mod が読み込まれます。",
      "加载器目录已缺失，请使用安装功能恢复完整组件。": "ローダーディレクトリがありません。インストール機能で完全なコンポーネントを復元してください。",
      "动作名无效": "アクション名が無効です",
      "动作名称无效或重复：": "アクション名が無効または重複しています：",
      "动作帧或帧率无效:": "アクションフレームまたはフレームレートが無効です:",
      "动作引用未知精灵：": "アクションが不明なスプライトを参照しています：",
      "动作没有可转换的精灵帧：": "アクションに変換可能なスプライトフレームがありません：",
      "动态卡面导入失败：": "動くカードイラストのインポートに失敗しました：",
      "动态卡面尺寸或总像素超过限制。": "動くカードイラストのサイズまたは総ピクセル数が上限を超えています。",
      "动态卡面已裁剪为 404×400。选择该方案并重启游戏后生效，需要新版 CardSkinMod。": "動くカードイラストを 404×400 にトリミングしました。この構成を選択してゲームを再起動すると反映されます。新しい CardSkinMod が必要です。",
      "动态卡面最多 20 秒（300 帧），请先剪辑视频或 GIF 后重试。": "動くカードイラストは最大 20 秒（300 フレーム）です。動画または GIF を編集してから再試行してください。",
      "动态卡面裁剪区域无效，请重新选择。": "動くカードイラストのトリミング範囲が無効です。選択し直してください。",
      "动态卡面转换失败：": "動くカードイラストの変換に失敗しました：",
      "动态覆盖已移除，重启游戏后使用原有静态卡面或游戏原图。": "動的オーバーライドを削除しました。ゲームを再起動すると元の静止カードイラストまたはゲーム元画像が使用されます。",
      "动画或技能视频属于其他角色，不能混合导入。": "アニメーションまたはスキル動画が別のキャラクターのものであるため、混在してインポートできません。",
      "包目录与标识不一致": "パッケージのディレクトリと識別子が一致しません",
      "协议": "プロトコル",
      "协议程序集不可用，无法解析结算信息。": "プロトコルアセンブリが利用できないため、リザルト情報を解析できません。",
      "单个资源包超过 2 GiB，无法读入内存：": "単一のアセットバンドルが 2 GiB を超えており、メモリに読み込めません：",
      "卡牌": "カード",
      "卡牌文件名不能为空": "カードのファイル名は空にできません",
      "卡面尺寸超出 Unity 上限（最长边 16384）：": "カードイラストのサイズが Unity の上限（最長辺 16384）を超えています：",
      "卡面必须是正方形": "カードイラストは正方形である必要があります",
      "卡面绑定名称不正确。": "カードイラストの紐付け名が正しくありません。",
      "即使删除成功，当前这局也已经加载了加载器，要重启游戏才会恢复正常。": "削除に成功しても、現在の対局では既にローダーが読み込まれているため、ゲームを再起動するまで正常に戻りません。",
      "即使删除成功，当前这局也已经加载了变速器，要重启游戏才会恢复正常速度。": "削除に成功しても、現在の対局では既に変速機が読み込まれているため、ゲームを再起動するまで通常速度に戻りません。",
      "压缩包内没有任何文件。": "圧縮ファイル内にファイルがありません。",
      "压缩包包含不安全或重复的路径。": "圧縮ファイルに安全でないパスまたは重複したパスが含まれています。",
      "压缩包包含不安全的相对路径。": "圧縮ファイルに安全でない相対パスが含まれています。",
      "压缩包里没有找到模组：需要 <ModId>/<ModId>.dll 这样的结构，或者把 <ModId>.dll 直接放在包根目录。": "圧縮ファイル内に Mod が見つかりません：<ModId>/<ModId>.dll という構造にするか、<ModId>.dll をパッケージのルートに直接置いてください。",
      "原安装版本": "元のインストールバージョン",
      "原精灵名称重复：": "元のスプライト名が重複しています：",
      "参数缺失": "パラメータが不足しています",
      "发布包 DLL 哈希校验失败。": "リリースパッケージの DLL ハッシュ検証に失敗しました。",
      "发布包不能超过 64 MB。": "リリースパッケージは 64 MB を超えられません。",
      "发布包包含不安全或重复的路径。": "リリースパッケージに安全でないパスまたは重複したパスが含まれています。",
      "发布包包含不安全路径、重复文件或超过大小限制。": "リリースパッケージに安全でないパス、重複ファイル、またはサイズ上限超過があります。",
      "发布包必须包含唯一的 CesiumLoader.SDK.dll（SDK 工具包或加载器发布包）。": "リリースパッケージには一意の CesiumLoader.SDK.dll が含まれている必要があります（SDK ツールキットまたはローダーのリリースパッケージ）。",
      "发布包文件超过大小限制。": "リリースパッケージのファイルがサイズ上限を超えています。",
      "发布包未提供 version.dll 的有效哈希。": "リリースパッケージに version.dll の有効なハッシュがありません。",
      "发布包缺少 version.dll。": "リリースパッケージに version.dll がありません。",
      "发布包缺少清单（cesium-loader.json），已停止安装。": "リリースパッケージにマニフェスト（cesium-loader.json）がないため、インストールを中止しました。",
      "发布包缺少版本清单。": "リリースパッケージにバージョンマニフェストがありません。",
      "发布包解压大小或文件数量超过限制。": "リリースパッケージの展開後サイズまたはファイル数が上限を超えています。",
      "发布包里没有任何 mod（AstralParty_ModLoader/mods/ 为空），已停止安装。": "リリースパッケージに Mod が1つもありません（AstralParty_ModLoader/mods/ が空です）。インストールを中止しました。",
      "变速器已卸载（游戏恢复正常速度）。": "変速機をアンインストールしました（ゲームは通常速度に戻りました）。",
      "变速器已卸载；当前正在运行的游戏进程仍加载着变速器，重启游戏后恢复正常速度。": "変速機をアンインストールしました。現在実行中のゲームプロセスにはまだ変速機が読み込まれています。ゲームを再起動すると通常速度に戻ります。",
      "变速器已安装到游戏目录。进入游戏后按配置的快捷键即可变速（需关闭垂直同步）。": "変速機をゲームディレクトリにインストールしました。ゲーム内で設定したホットキーを押すと変速できます（垂直同期をオフにする必要があります）。",
      "变速器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载变速器。": "変速機のファイルをゲームディレクトリに書き込みました。ゲームが実行中のため、すぐには反映されません —— ゲームを再起動すると変速機が読み込まれます。",
      "可在「模组 → 加载器设置」里打开它）": "「Mod → ローダー設定」から開けます）",
      "可在「模组」页一键安装加载器，或改选「从 Steam 启动」）": "「Mod」ページでワンクリックでローダーをインストールするか、「Steam から起動」に切り替えてください）",
      "可随时点击恢复 version.dll。": "いつでもクリックして version.dll を復元できます。",
      "吉星派对": "Astral Party",
      "同名方案已存在": "同名の構成が既に存在します",
      "同名方案已存在，请修改包的 Id 或先删除原方案": "同名の構成が既に存在します。パッケージの Id を変更するか、元の構成を削除してください",
      "同名自定义包已存在:": "同名のカスタムパッケージが既に存在します:",
      "向当前方案添加人物皮肤包": "現在の構成にキャラクタースキンパックを追加",
      "四、按回合详细筹码流转与决策时间线 (Chronological Relic Timeline)": "四、ラウンドごとの詳細なレリックの流れと意思決定タイムライン (Chronological Relic Timeline)",
      "回放库目录与游戏回放目录不能是同一个路径，操作已拒绝。请先在设置里修改库目录。": "リプレイ保管庫とゲームのリプレイディレクトリに同じパスは指定できません。操作を拒否しました。設定で保管庫ディレクトリを変更してください。",
      "回放库目录与游戏回放目录被设置成了同一个路径，已停用所有归档操作。请在设置里改成别的目录。": "リプレイ保管庫とゲームのリプレイディレクトリが同じパスに設定されているため、すべての保管操作を無効にしました。設定で別のディレクトリに変更してください。",
      "图片不存在:": "画像が存在しません:",
      "图片必须是 PNG/JPG，GIF 请先转换为帧序列": "画像は PNG/JPG である必要があります。GIF は先にフレームシーケンスへ変換してください",
      "图片槽位无效": "画像スロットが無効です",
      "基于应用内置默认包创建": "アプリ内蔵の既定パッケージを基に作成",
      "基础倍速必须大于 0。": "基本倍速は 0 より大きい必要があります。",
      "备份哈希无效。": "バックアップのハッシュが無効です。",
      "复制后校验失败，目标文件已清理，源文件保持不动。": "コピー後の検証に失敗しました。コピー先のファイルは削除され、元のファイルは変更されていません。",
      "多人物方案已保存。完全退出并重启游戏后生效。": "複数キャラクター構成を保存しました。ゲームを完全に終了して再起動すると反映されます。",
      "大厅匹配绕过: 修「点创建/加入房间没反应」": "ロビーマッチのバイパス: 「ルーム作成/参加を押しても反応しない」問題の修正",
      "如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。": "ゲームでも読み取れない場合は、ゲームディレクトリに戻さないでください —— ゲームが一覧で検出するとディレクトリごと削除します。",
      "如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。": "ゲームでも読み取れない場合、このリプレイを一覧で検出したときにディレクトリごと削除します。戻す場合はもう一度確認してください。",
      "如确定要覆盖，请勾选「允许覆盖其它 version.dll」。": "上書きする場合は「他の version.dll の上書きを許可する」にチェックを入れてください。",
      "如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。": "ダウングレードする場合は「古いローダーのインストール（ダウングレード）を許可する」にチェックを入れてください。通常は新しいバージョンの AstralParty.Toys を使用する方が適切です。",
      "媒体文件不存在:": "メディアファイルが存在しません:",
      "存在重复卡牌资源名：": "カードリソース名が重複しています：",
      "安装文件包含符号链接，无法可靠备份。": "インストールファイルにシンボリックリンクが含まれているため、確実にバックアップできません。",
      "定位安装目录和热更新缓存资源": "インストールディレクトリとホットアップデートのキャッシュリソースを特定",
      "对局": "対局",
      "对应版本的在线发布包": "対応バージョンのオンラインリリースパッケージ",
      "导入多人物皮肤方案": "複数キャラクターのスキン構成をインポート",
      "导入旧式卡牌皮肤包失败：": "旧形式のカードスキンパックのインポートに失敗しました：",
      "导入旧式卡牌皮肤包（自动转换 bundle）": "旧形式のカードスキンパックをインポート（bundle を自動変換）",
      "导入旧式角色皮肤包（自动转换 bundle）": "旧形式のキャラクタースキンパックをインポート（bundle を自動変換）",
      "导入角色皮肤包": "キャラクタースキンパックをインポート",
      "导出商店时间线表格 (CSV)": "ショップタイムラインの表を出力 (CSV)",
      "导出回放完整数据": "リプレイの完全なデータを出力",
      "导出失败：": "出力に失敗しました：",
      "导出当前动作为 GIF": "現在のアクションを GIF で出力",
      "导出方案及自定义皮肤包": "構成とカスタムスキンパックを出力",
      "导出来源皮肤无效": "出力元のスキンが無効です",
      "导出皮肤方案 ZIP": "スキン構成 ZIP を出力",
      "导出确认已过期，请重新导出。": "出力の確認が期限切れです。もう一度出力してください。",
      "导出筹码时间线 (自然语言 AI 文本)": "レリックタイムラインを出力（自然言語 AI テキスト）",
      "导出筹码时间线数据 (JSON)": "レリックタイムラインのデータを出力 (JSON)",
      "导出筹码时间线表格 (CSV)": "レリックタイムラインの表を出力 (CSV)",
      "导出角色皮肤包": "キャラクタースキンパックを出力",
      "尚未安装：点「安装加载器」，把加载器和内置模组装进游戏目录。": "未インストール：「ローダーをインストール」を押して、ローダーと内蔵 Mod をゲームディレクトリにインストールしてください。",
      "尚未安装：点击「安装变速器」把文件复制到游戏目录。": "未インストール：「変速機をインストール」を押して、ファイルをゲームディレクトリにコピーしてください。",
      "尚未找到游戏目录：可点击「选择游戏目录」手动指定安装位置。": "ゲームディレクトリが見つかりません：「ゲームディレクトリを選択」を押してインストール先を手動で指定できます。",
      "尚未找到游戏目录：点「自动检测」，或手动选择游戏 exe 所在的文件夹。": "ゲームディレクトリが見つかりません：「自動検出」を押すか、ゲームの exe があるフォルダーを手動で選択してください。",
      "尚未选择有效的游戏目录，请先自动检测或手动选择。": "有効なゲームディレクトリが選択されていません。自動検出するか手動で選択してください。",
      "已交给 Steam 启动 Astral Party，稍等一下。": "Steam に Astral Party の起動を依頼しました。しばらくお待ちください。",
      "已从应用内置默认包恢复 default，重启游戏后生效。": "アプリ内蔵の既定パッケージから default を復元しました。ゲーム再起動後に反映されます。",
      "已保存加载器设置（重启游戏生效）": "ローダー設定を保存しました（ゲーム再起動後に反映）",
      "已切换到从 Steam 启动：加载器的 Steam 绕过已整套关闭，Steam 大厅联机恢复正常（重启游戏生效）": "Steam からの起動に切り替えました：ローダーの Steam バイパスを一式すべて無効にし、Steam ロビーでのマルチプレイが正常に戻ります（ゲーム再起動後に反映）",
      "已切换到绕过 Steam：加载器的 Steam 绕过已打开（重启游戏生效）": "Steam バイパスに切り替えました：ローダーの Steam バイパスを有効にしました（ゲーム再起動後に反映）",
      "已加入后台卡面转换队列，右下角可查看进度或终止。": "バックグラウンドのカードイラスト変換キューに追加しました。右下で進捗を確認したり中止したりできます。",
      "已加入后台导出队列": "バックグラウンドの出力キューに追加しました",
      "已加入后台导出队列，完成后自动添加到方案": "バックグラウンドの出力キューに追加しました。完了すると自動で構成に追加されます",
      "已加入后台转换队列，右下角可查看进度或终止。": "バックグラウンドの変換キューに追加しました。右下で進捗を確認したり中止したりできます。",
      "已取消": "キャンセルしました",
      "已取消深度扫描。": "詳細スキャンをキャンセルしました。",
      "已取消读取，原卡面保留。": "読み取りをキャンセルしました。元のカードイラストは保持されています。",
      "已取消转换，原卡面保留。": "変換をキャンセルしました。元のカードイラストは保持されています。",
      "已恢复默认模板（Ctrl 切换 2 倍速）。": "既定テンプレートに戻しました（Ctrl で 2 倍速を切り替え）。",
      "已按请求的 ID 入库，需要的话可以改名后再放回游戏。": "要求された ID で保管庫に保存しました。必要なら名前を変更してからゲームに戻せます。",
      "已有卡面正在处理，请等待或取消。": "処理中のカードイラストがあります。完了を待つかキャンセルしてください。",
      "已有卡面正在转换。": "カードイラストを変換中です。",
      "已有发布包正在导入，请稍候。": "リリースパッケージをインポート中です。しばらくお待ちください。",
      "已禁用变速（恢复 1.0 正常速度，重启游戏生效）": "変速を無効化しました（通常の 1.0 倍に戻ります。ゲーム再起動後に反映）",
      "已终止任务，临时文件已清理": "タスクを中止しました。一時ファイルは削除されました",
      "已经有一个回放在下载了，等它结束再试。": "既に別のリプレイをダウンロード中です。完了してから再試行してください。",
      "已绕过 Steam，直接启动游戏主程序。": "Steam をバイパスし、ゲーム本体を直接起動しました。",
      "已记住这个选择，但没能同步到加载器配置（还没装加载器？在「模组」页安装一次就会同步）。": "この選択を記憶しましたが、ローダー設定に同期できませんでした（ローダー未インストールの場合は「Mod」ページで一度インストールすると同期されます）。",
      "帧号,回合,玩家ID,玩家名称,玩家角色,商店类型,候选卡牌,售价,折扣,免费卡,购买结果,已售出槽位,是否关闭": "フレーム番号,ラウンド,プレイヤーID,プレイヤー名,プレイヤーキャラクター,ショップ種別,候補カード,価格,割引,無料カード,購入結果,売却済みスロット,クローズ済み",
      "帧号,回合,玩家ID,玩家名称,玩家角色,操作类型,档位,筹码ID,筹码名称,品质,来源,刷新状态,候选选项": "フレーム番号,ラウンド,プレイヤーID,プレイヤー名,プレイヤーキャラクター,操作種別,プリセット,レリックID,レリック名,レアリティ,ソース,リロール状態,候補",
      "帧序列必须是 PNG/JPG": "フレームシーケンスは PNG/JPG である必要があります",
      "帧时长必须在 0～655350 ms 之间": "フレームの長さは 0～655350 ms の範囲で指定してください",
      "帧时长无效": "フレームの長さが無効です",
      "开始处理": "処理を開始",
      "当前加载器版本未知或格式无效，无法下载对应发布包。请重新安装或更新加载器。": "現在のローダーバージョンが不明または形式が無効なため、対応するリリースパッケージをダウンロードできません。ローダーを再インストールするか更新してください。",
      "当前动作没有动画帧": "現在のアクションにアニメーションフレームがありません",
      "当前动作过大，请缩小帧尺寸或减少帧数后导出 GIF": "現在のアクションが大きすぎます。フレームサイズを小さくするかフレーム数を減らしてから GIF を出力してください",
      "当前游戏已切换，请重新导出。": "現在のゲームが切り替わりました。もう一度出力してください。",
      "当前游戏已切换，请重新添加人物皮肤包": "現在のゲームが切り替わりました。キャラクタースキンパックを追加し直してください",
      "当前游戏已切换，请重新选择图片。": "現在のゲームが切り替わりました。画像を選択し直してください。",
      "当前游戏已切换，请重新选择素材": "現在のゲームが切り替わりました。素材を選択し直してください",
      "当前游戏已切换，请重新选择视频。": "現在のゲームが切り替わりました。動画を選択し直してください。",
      "当前游戏进程仍加载着加载器，下次启动才停用。": "現在のゲームプロセスにはまだローダーが読み込まれています。次回起動時に無効になります。",
      "恢复默认包失败：": "既定パッケージの復元に失敗しました：",
      "所选目录里没有找到游戏主程序（AstralParty*.exe），确认这是游戏 exe 所在的目录？": "選択したディレクトリにゲーム本体（AstralParty*.exe）が見つかりません。ゲームの exe があるディレクトリですか？",
      "找不到 WebUI/index.html（内嵌资源里也没有）。": "WebUI/index.html が見つかりません（内蔵リソースにもありません）。",
      "找不到要导入的 mod 包。": "インポートする Mod パッケージが見つかりません。",
      "找不到要导入的 mod 文件。": "インポートする Mod ファイルが見つかりません。",
      "技能视频不是可识别的 CRI USM：": "スキル動画が認識可能な CRI USM ではありません：",
      "挂接延迟不能为负数。": "フック遅延に負の値は指定できません。",
      "按配置中的完整资源名匹配目标皮肤": "設定内の完全なリソース名で対象スキンを照合",
      "提交完整卡牌方案": "カード構成全体をコミット",
      "提示：CesiumLoader 已内置变速引擎——请勿安装本变速器，直接改加载器的": "ヒント：CesiumLoader には変速エンジンが内蔵されています —— この変速機はインストールせず、ローダーの",
      "整个模组包（.zip）请用 ImportModPackage 导入，这里只接收单个 DLL。": "Mod パッケージ全体（.zip）は ImportModPackage でインポートしてください。ここでは単一の DLL のみ受け付けます。",
      "文案槽位或长度无效:": "テキストスロットまたは長さが無効です:",
      "新方案标识不能为空": "新しい構成の識別子は空にできません",
      "新方案标识包含非法字符": "新しい構成の識別子に不正な文字が含まれています",
      "方案B 备用安全网 —— 实测开启会导致启动崩溃, 必须保持 false": "プランB の予備セーフティネット —— 有効にすると起動時にクラッシュすることを確認済みのため、false のままにしてください",
      "方案C 调用方式: auto / direct / invoke": "プランC の呼び出し方法: auto / direct / invoke",
      "方案为空": "構成が空です",
      "方案信息已保存。": "構成情報を保存しました。",
      "方案共享图片冲突:": "構成の共有画像が競合しています:",
      "方案共享文案冲突:": "構成の共有テキストが競合しています:",
      "方案名不能为空": "構成名は空にできません",
      "方案名称不正确。": "構成名が正しくありません。",
      "方案名称不能为空": "構成名は空にできません",
      "方案名称最长 100 个字符": "構成名は最大 100 文字です",
      "方案存在空字段": "構成に空のフィールドがあります",
      "方案已存在": "構成は既に存在します",
      "方案描述最长 2000 个字符": "構成の説明は最大 2000 文字です",
      "方案操作失败：": "構成の操作に失敗しました：",
      "方案显示名称不能为空": "構成の表示名は空にできません",
      "方案显示名称最长 100 个字符": "構成の表示名は最大 100 文字です",
      "方案标识与保存目标不一致": "構成の識別子が保存先と一致しません",
      "方案标识仅支持字母、数字、下划线和短横线，最长 80 字符。": "構成の識別子は英数字、アンダースコア、ハイフンのみ使用でき、最大 80 文字です。",
      "方案目录与标识不一致": "構成のディレクトリと識別子が一致しません",
      "方案简介最长 2000 个字符": "構成の概要は最大 2000 文字です",
      "方案缺少自定义包:": "構成にカスタムパッケージがありません:",
      "无效的 PNG 帧。": "無効な PNG フレームです。",
      "无法保存加载器版本清单，本次安装未完整完成。": "ローダーのバージョンマニフェストを保存できないため、今回のインストールは完了していません。",
      "无法启动 FFmpeg。": "FFmpeg を起動できません。",
      "无法完整读取卡牌资源包": "カードアセットバンドルを完全に読み取れません",
      "无法完整读取旧资源包": "旧アセットバンドルを完全に読み取れません",
      "无法确定回放 ID。": "リプレイ ID を特定できません。",
      "无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。": "FFmpeg を取得できません。ffmpeg.exe をアプリの隣の tools ディレクトリに置くか、ASTRAL_FFMPEG_PATH を設定してください。",
      "无法设置首次安装的 Steam 启动方式，加载器配置不完整。": "初回インストール時の Steam 起動方法を設定できません。ローダー設定が不完全です。",
      "无法读取 bundle 内的资源文件：": "bundle 内のリソースファイルを読み取れません：",
      "无法读取视频首帧：": "動画の先頭フレームを読み取れません：",
      "旧版启用皮肤": "旧版で有効化されたスキン",
      "最新 Release 尚未提供默认皮肤包，请先发布包含 cesium-default-skins.zip 的版本。": "最新の Release に既定のスキンパックがまだありません。cesium-default-skins.zip を含むバージョンを先にリリースしてください。",
      "未刷新": "リロールなし",
      "未刷新，直接从初始候选选择": "リロールせず、初期候補から直接選択",
      "未知动画附属 MonoBehaviour，不能部分导入：": "不明なアニメーション付属 MonoBehaviour のため、部分インポートできません：",
      "未知图片槽位:": "不明な画像スロット:",
      "未知消息": "不明なメッセージ",
      "未知玩家": "不明なプレイヤー",
      "未知的方案操作": "不明な構成操作",
      "未知视频实现：": "不明な動画実装：",
      "未购买，关闭商店": "購入せず、ショップを閉じる",
      "本地备份": "ローカルバックアップ",
      "本地备份校验失败，已停止操作。": "ローカルバックアップの検証に失敗したため、操作を中止しました。",
      "来源未记录": "ソース未記録",
      "标识必须是 1～80 个字母、数字、下划线或短横线": "識別子は 1～80 文字の英字、数字、アンダースコア、ハイフンである必要があります",
      "校验 PNG 和皮肤清单": "PNG とスキンマニフェストを検証",
      "校验并保存模板": "テンプレートを検証して保存",
      "检查旧式卡牌皮肤 ZIP": "旧形式のカードスキン ZIP を検証",
      "检查旧皮肤 ZIP 的路径与可用空间": "旧スキン ZIP のパスと空き容量を確認",
      "检测到已安装 CesiumLoader Mod 加载器（AstralParty_ModLoader\\doorstop_config.json 存在）。": "CesiumLoader Mod ローダーのインストールを検出しました（AstralParty_ModLoader\\doorstop_config.json が存在します）。",
      "检测到游戏正在运行——安装或卸载前请先退出游戏。": "ゲームの実行を検出しました —— インストールまたはアンインストールの前にゲームを終了してください。",
      "模组包比游戏目录里已装的更旧：": "Mod パッケージがゲームディレクトリにインストール済みのものより古いです：",
      "正在下载最新 CesiumLoader 并安装…": "最新の CesiumLoader をダウンロードしてインストール中…",
      "正在准备协议和配置…": "プロトコルと設定を準備中…",
      "正在导入模组，请稍候。": "Mod をインポート中です。しばらくお待ちください。",
      "正在导出商店时间线…": "ショップタイムラインを出力中…",
      "正在导出完整协议数据…": "プロトコルデータ全体を出力中…",
      "正在导出筹码时间线…": "レリックタイムラインを出力中…",
      "正在扫描 Steam 库、注册表与常见安装位置…": "Steam ライブラリ、レジストリ、一般的なインストール場所をスキャン中…",
      "正在终止并清理临时文件…": "中止して一時ファイルを削除中…",
      "正在读取视频首帧，准备裁剪…": "動画の先頭フレームを読み取り、トリミングの準備中…",
      "正在转换动态卡面（15 帧/秒，最多 20 秒）…": "動くカードイラストを変換中（15 フレーム/秒、最大 20 秒）…",
      "此人物的相同原皮肤已在方案中，请编辑已有的人物皮肤": "このキャラクターの同じ元スキンは既に構成内にあります。既存のキャラクタースキンを編集してください",
      "每帧时长数量与帧数不一致": "各フレームの長さの数がフレーム数と一致しません",
      "没有可删除的回放。": "削除できるリプレイがありません。",
      "没能启动游戏：没有找到游戏主程序。请先在「变速器」页选择游戏目录，或确认游戏已安装。": "ゲームを起動できませんでした：ゲーム本体が見つかりません。先に「変速機」ページでゲームディレクトリを選択するか、ゲームがインストールされているか確認してください。",
      "清单为空": "マニフェストが空です",
      "游戏文件操作正在进行（安装 / 更新 / 导入），请等待完成后再修改。": "ゲームファイルの操作（インストール / 更新 / インポート）を実行中です。完了してから変更してください。",
      "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再安装。": "ゲーム実行中は version.dll が使用されるため上書きできません —— ゲームを完全に終了してからインストールしてください。",
      "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再更新。": "ゲーム実行中は version.dll が使用されるため上書きできません —— ゲームを完全に終了してから更新してください。",
      "游戏正在运行时也能卸：version.dll 已被游戏加载占用，删除通常会失败并提示文件被占用；": "ゲーム実行中でもアンインストールできます：version.dll はゲームに読み込まれて使用中のため、削除は通常失敗し、ファイル使用中のエラーが表示されます；",
      "游戏正在运行时也能同步：配置会写进游戏目录，已加载的变速器需要按重载热键（默认 Ctrl+Shift+R）重新读取，否则重启游戏后生效。": "ゲーム実行中でも同期できます：設定はゲームディレクトリに書き込まれます。読み込み済みの変速機はリロード用ホットキー（既定は Ctrl+Shift+R）で再読み込みしない場合、ゲーム再起動後に反映されます。",
      "游戏正在运行时也能装：version.dll 是游戏启动时加载的，本次写进去要重启游戏才会生效；": "ゲーム実行中でもインストールできます：version.dll はゲーム起動時に読み込まれるため、今回書き込んだ内容はゲームを再起動するまで反映されません；",
      "游戏正在运行时也能装：version.dll 由游戏启动时加载，本次写进去要重启游戏才会生效；": "ゲーム実行中でもインストールできます：version.dll はゲーム起動時に読み込まれるため、今回書き込んだ内容はゲームを再起動するまで反映されません；",
      "游戏正在运行时该文件已被加载占用，请完全退出游戏后再卸载（本次没有改动任何文件）。": "ゲーム実行中はこのファイルが読み込まれて使用中のため、ゲームを完全に終了してからアンインストールしてください（今回はどのファイルも変更していません）。",
      "游戏正在运行，重启游戏后生效。": "ゲームが実行中です。ゲーム再起動後に反映されます。",
      "游戏正在运行：安装、更新、停用或恢复要重启游戏才生效，version.dll 被占用时请完全退出游戏再操作。": "ゲームが実行中です：インストール、更新、無効化、復元はゲーム再起動後に反映されます。version.dll が使用中の場合はゲームを完全に終了してから操作してください。",
      "游戏正在运行：本次写入要重启游戏后才会加载。": "ゲームが実行中です：今回の書き込みはゲームを再起動するまで読み込まれません。",
      "游戏目录不存在，请先安装加载器或选择游戏目录。": "ゲームディレクトリが存在しません。先にローダーをインストールするか、ゲームディレクトリを選択してください。",
      "游戏目录中没有加载器文件，无需卸载。": "ゲームディレクトリにローダーファイルがないため、アンインストールは不要です。",
      "游戏目录中没有变速器文件，无需卸载。": "ゲームディレクトリに変速機ファイルがないため、アンインストールは不要です。",
      "游戏目录存在其它 version.dll（与内置文件不同）——覆盖或卸载前请先确认来源。": "ゲームディレクトリに別の version.dll（内蔵ファイルと異なる）があります —— 上書きまたはアンインストールの前に入手元を確認してください。",
      "游戏目录尚未安装变速器，请先安装。": "ゲームディレクトリに変速機がまだインストールされていません。先にインストールしてください。",
      "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的，如 CesiumLoader Mod 加载器）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。": "ゲームディレクトリに内蔵と異なる version.dll（別のツール、例えば CesiumLoader Mod ローダーのもの）が既にあります —— 上書きする場合は「他の version.dll の上書きを許可する」にチェックを入れてください。",
      "游戏目录已有 version.dll，未覆盖任何文件。请刷新状态并确认来源。": "ゲームディレクトリに version.dll が既にあります。どのファイルも上書きしていません。状態を更新して入手元を確認してください。",
      "游戏目录或 mod 文件名无效。": "ゲームディレクトリまたは Mod のファイル名が無効です。",
      "游戏目录里没有可归档的回放。": "ゲームディレクトリに保管できるリプレイがありません。",
      "游戏目录里的 version.dll 不是本工具装的（来源不明）——可能是旧版独立变速器或第三方文件，安装或卸载前请先确认。": "ゲームディレクトリの version.dll はこのツールがインストールしたものではありません（入手元不明）—— 旧版単体変速機や第三者ファイルの可能性があるため、インストールまたはアンインストールの前に確認してください。",
      "游戏目录里的 version.dll 不是本工具装的（来源不明）——未删除任何文件。如确认要删除请勾选「强制卸载」。": "ゲームディレクトリの version.dll はこのツールがインストールしたものではありません（入手元不明）—— どのファイルも削除していません。削除する場合は「強制アンインストール」にチェックを入れてください。",
      "游戏目录里的 version.dll 不是本工具装的（来源不明，可能是旧版独立变速器或第三方文件）——": "ゲームディレクトリの version.dll はこのツールがインストールしたものではありません（入手元不明。旧版単体変速機や第三者ファイルの可能性があります）——",
      "游戏配置没有可匹配的角色皮肤。": "ゲーム設定に一致するキャラクタースキンがありません。",
      "游戏默认": "ゲーム既定",
      "游戏默认方案不可修改": "ゲームの既定構成は変更できません",
      "游戏默认方案无需恢复外部卡面。": "ゲームの既定構成には外部カードイラストの復元は不要です。",
      "游戏默认方案没有外部图片可导出。": "ゲームの既定構成には出力できる外部画像がありません。",
      "源图片文件不存在": "ソース画像ファイルが存在しません",
      "物品": "アイテム",
      "皮肤不属于当前角色:": "スキンが現在のキャラクターのものではありません:",
      "皮肤动画总帧数超过 10000": "スキンアニメーションの総フレーム数が 10000 を超えています",
      "皮肤包已存在": "スキンパックは既に存在します",
      "皮肤压缩包不存在": "スキンの圧縮ファイルが存在しません",
      "皮肤方案不存在:": "スキン構成が存在しません:",
      "皮肤方案名称不能为空": "スキン構成名は空にできません",
      "皮肤方案已导出为 ZIP，可通过导入 ZIP 使用。": "スキン構成を ZIP として出力しました。ZIP のインポートから使用できます。",
      "皮肤方案目录不存在。": "スキン構成のディレクトリが存在しません。",
      "皮肤清单存在空字段": "スキンマニフェストに空のフィールドがあります",
      "相同角色和目标皮肤存在重复规则": "同じキャラクターと対象スキンに重複したルールがあります",
      "确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。": "CDN 上のこのファイルに差し替えるには、先に保管庫内の古いコピーを削除してください（または別の ID を使用してください）。",
      "移除动态卡面失败：": "動くカードイラストの削除に失敗しました：",
      "程序集名称不是 CesiumLoader.SDK。": "アセンブリ名が CesiumLoader.SDK ではありません。",
      "程序集缺少内置加载器资源（ModLoader.version.dll），请重新编译发布版本。": "アセンブリに内蔵ローダーリソース（ModLoader.version.dll）がないため、リリース版を再ビルドしてください。",
      "程序集缺少内置加载器资源（version.dll），无法安装。": "アセンブリに内蔵ローダーリソース（version.dll）がないため、インストールできません。",
      "程序集缺少内置变速器资源（SpeedhackTools.version.dll / speedhack_config.json），请重新编译发布版本。": "アセンブリに内蔵変速機リソース（SpeedhackTools.version.dll / speedhack_config.json）がないため、リリース版を再ビルドしてください。",
      "程序集缺少内置变速器资源（version.dll / speedhack_config.json），无法安装。": "アセンブリに内蔵変速機リソース（version.dll / speedhack_config.json）がないため、インストールできません。",
      "程序集缺少内置配置模板资源，无法恢复默认。": "アセンブリに内蔵設定テンプレートリソースがないため、既定に戻せません。",
      "空白皮肤包已创建": "空のスキンパックを作成しました",
      "等待处理": "処理待ち",
      "精灵名称无效或重复：": "スプライト名が無効または重複しています：",
      "精灵帧没有完整动作信息。": "スプライトフレームに完全なアクション情報がありません。",
      "纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：": "テクスチャサイズが Unity の上限（最長辺 16384）を超えているか、デコードデータが不完全です：",
      "纹理或动画帧尺寸超出 Unity 上限（最长边 16384）。": "テクスチャまたはアニメーションフレームのサイズが Unity の上限（最長辺 16384）を超えています。",
      "纹理资源名为空或重复：": "テクスチャリソース名が空または重複しています：",
      "绑定规则必须指定自定义包": "紐付けルールにはカスタムパッケージを指定する必要があります",
      "继续保存新回放会失败。播放不受影响，建议归档几局。": "このまま新しいリプレイを保存すると失敗します。再生には影響しません。数対局を保管することをおすすめします。",
      "编码并保存 GIF": "GIF をエンコードして保存",
      "而且正在运行的游戏往往占用着 version.dll，覆盖会失败并提示文件被占用——那样就先完全退出游戏再装。": "また、実行中のゲームが version.dll を使用していることが多く、上書きに失敗してファイル使用中のエラーが表示されます —— その場合はゲームを完全に終了してからインストールしてください。",
      "自动检测未找到游戏目录，请点「选择…」手动指定。": "自動検出でゲームディレクトリが見つかりませんでした。「選択…」を押して手動で指定してください。",
      "自定义包与规则角色不匹配": "カスタムパッケージとルールのキャラクターが一致しません",
      "自定义包已导入但绑定失败，回滚未完成；包可能被其他方案引用，已保留包。": "カスタムパッケージはインポートされましたが紐付けに失敗し、ロールバックも完了していません。パッケージが他の構成から参照されている可能性があるため、パッケージは保持されています。",
      "自定义包被皮肤方案引用，请先移除方案规则:": "カスタムパッケージはスキン構成から参照されています。先に構成のルールを削除してください:",
      "自定义包角色与规则不匹配:": "カスタムパッケージのキャラクターがルールと一致しません:",
      "裁剪区域必须是图片范围内的正方形": "トリミング範囲は画像内の正方形である必要があります",
      "裁剪卡面已保存，重启游戏后生效。": "カードイラストのトリミングを保存しました。ゲーム再起動後に反映されます。",
      "裁剪已取消或过期，请重新选择图片。": "トリミングがキャンセルされたか期限切れです。画像を選択し直してください。",
      "裁剪已取消或过期，请重新选择视频。": "トリミングがキャンセルされたか期限切れです。動画を選択し直してください。",
      "裁剪已过期，请重新选择图片": "トリミングの期限が切れました。画像を選択し直してください",
      "裁剪范围或输出尺寸无效": "トリミング範囲または出力サイズが無効です",
      "规则为空": "ルールが空です",
      "规则标识重复:": "ルールの識別子が重複しています:",
      "规则至少需要一种资源来源": "ルールには少なくとも1つのリソースソースが必要です",
      "规则角色或皮肤 ID 无效": "ルールのキャラクター ID またはスキン ID が無効です",
      "视频资源引用结构无效：": "動画リソースの参照構造が無効です：",
      "视频转换产生不一致的帧尺寸。": "動画変換でフレームサイズが一致しませんでした。",
      "视频转换失败：": "動画の変換に失敗しました：",
      "角色不存在:": "キャラクターが存在しません:",
      "角色或原皮肤 ID 无效": "キャラクター ID または元スキン ID が無効です",
      "角色皮肤已保存。游戏内修改在完全退出并重启后生效。": "キャラクタースキンを保存しました。ゲーム内の変更は完全に終了して再起動すると反映されます。",
      "角色皮肤操作失败：": "キャラクタースキンの操作に失敗しました：",
      "解析不出结算帧（cmd 1016），文件可能已损坏或来自不兼容的版本。": "リザルトフレーム（cmd 1016）を解析できません。ファイルが破損しているか、互換性のないバージョンの可能性があります。",
      "设置已保存。": "設定を保存しました。",
      "该 version.dll 与内置文件不同，可能不是本工具安装的——未删除任何文件。如确认要删除请勾选「强制卸载」。": "この version.dll は内蔵ファイルと異なり、このツールがインストールしたものではない可能性があります —— どのファイルも削除していません。削除する場合は「強制アンインストール」にチェックを入れてください。",
      "该帧已发生变化，请重新选择替换": "このフレームは変更されています。選択し直して置換してください",
      "该方案已经在导出队列中": "この構成は既に出力キューにあります",
      "该方案没有可导出的卡面。": "この構成には出力できるカードイラストがありません。",
      "请先在模组页安装 CesiumLoader。": "先に Mod ページで CesiumLoader をインストールしてください。",
      "请先安装加载器，再导入 SDK。": "先にローダーをインストールしてから SDK をインポートしてください。",
      "请先等待或取消当前动态卡面转换。": "現在の動くカードイラストの変換が終わるのを待つか、キャンセルしてください。",
      "请先等待或取消当前动态卡面转换，再修改或导出皮肤方案。": "現在の動くカードイラストの変換が終わるのを待つか、キャンセルしてからスキン構成を変更または出力してください。",
      "请先等待或取消当前转换。": "現在の変換が終わるのを待つか、キャンセルしてください。",
      "请先选择外部皮肤方案。": "先に外部スキン構成を選択してください。",
      "请先选择游戏目录。": "先にゲームディレクトリを選択してください。",
      "请填写回放 ID。": "リプレイ ID を入力してください。",
      "请填写存在的发布包完整路径。": "存在するリリースパッケージのフルパスを入力してください。",
      "请将适合本机的 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH 后重试。": "この PC に合った ffmpeg.exe をアプリの隣の tools ディレクトリに置くか、ASTRAL_FFMPEG_PATH を設定して再試行してください。",
      "请检查文件权限后重试（本次没有改动任何文件）。": "ファイルの権限を確認してから再試行してください（今回はどのファイルも変更していません）。",
      "请确认回放 ID 与区服是否对应。": "リプレイ ID とリージョンが対応しているか確認してください。",
      "请选择 1～2000 张图片": "1～2000 枚の画像を選択してください",
      "请选择 PNG/JPG 图片": "PNG/JPG 画像を選択してください",
      "请选择 SDK 发布 ZIP 或 CesiumLoader.SDK.dll。": "SDK のリリース ZIP または CesiumLoader.SDK.dll を選択してください。",
      "请选择有效的游戏主程序目录。": "有効なゲーム本体のディレクトリを選択してください。",
      "请选择视频或 GIF 文件。": "動画または GIF ファイルを選択してください。",
      "读取动画顺序与播放参数": "アニメーションの順序と再生パラメータを読み取り",
      "读取游戏档案，首次使用时检测 Steam 游戏目录": "ゲームプロファイルを読み取り、初回使用時に Steam のゲームディレクトリを検出",
      "读取角色与皮肤配置": "キャラクターとスキンの設定を読み取り",
      "资源不是支持的卡牌贴图，拒绝部分导入：": "サポート対象のカードテクスチャではないため、部分インポートを拒否しました：",
      "资源包没有可转换的 Texture2D：": "アセットバンドルに変換可能な Texture2D がありません：",
      "资源包没有可转换的图片、动作或已知技能视频。": "アセットバンドルに変換可能な画像、アクション、既知のスキル動画がありません。",
      "资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：": "リソースが複数のキャラクターまたはスキンに対応しており、自動識別できません。対象の立ち絵が明確なスキンパックを使用してください。マッチ結果：",
      "资源无法对应一个角色皮肤，或包中混有多个目标的资源。": "リソースを1つのキャラクタースキンに対応付けられないか、パッケージ内に複数の対象のリソースが混在しています。",
      "资源路径包含无效目录": "リソースパスに無効なディレクトリが含まれています",
      "资源路径必须位于皮肤包内": "リソースパスはスキンパック内にある必要があります",
      "资源路径越界": "リソースパスが範囲外です",
      "转换工具下载超过 256 MB。": "変換ツールのダウンロードが 256 MB を超えました。",
      "转换工具体积不符合预期。": "変換ツールのサイズが想定と一致しません。",
      "这个回放找不到了（可能已被移动或删除），点「刷新列表」重新扫描。": "このリプレイが見つかりません（移動または削除された可能性があります）。「リストを更新」を押して再スキャンしてください。",
      "这个文件不是能解出结算帧的回放，已拒绝导入。": "このファイルはリザルトフレームを解析できるリプレイではないため、インポートを拒否しました。",
      "这个目录已经不在了。": "このディレクトリは既に存在しません。",
      "这个目录已经不在了，或里面没有 AstralParty*.exe。": "このディレクトリは既に存在しないか、中に AstralParty*.exe がありません。",
      "这个目录里没有检测到游戏主程序。请确认所选目录包含游戏 exe、UnityPlayer.dll 和 *_Data 文件夹。": "このディレクトリにはゲーム本体が検出されませんでした。選択したディレクトリにゲームの exe、UnityPlayer.dll、*_Data フォルダーが含まれているか確認してください。",
      "这是 SDK 包，请用「导入发布包」更新 SDK。": "これは SDK パッケージです。「リリースパッケージをインポート」で SDK を更新してください。",
      "这是 Toys 皮肤包，请使用标准皮肤导入。": "これは Toys のスキンパックです。標準のスキンインポートを使用してください。",
      "这是加载器发布包（包含 version.dll），请用「导入发布包」安装。": "これはローダーのリリースパッケージ（version.dll を含む）です。「リリースパッケージをインポート」でインストールしてください。",
      "这是模组包（含 <ModId>/<ModId>.dll），请用「导入模组」安装；「导入发布包」只用于加载器与 SDK。": "これは Mod パッケージ（<ModId>/<ModId>.dll を含む）です。「Mod をインポート」でインストールしてください。「リリースパッケージをインポート」はローダーと SDK 専用です。",
      "退房兜底: 修退房/被踢时 await 之后的 NRE": "退室フォールバック: 退室/キック時に await 後に発生する NRE の修正",
      "选中的动画帧不存在": "選択したアニメーションフレームが存在しません",
      "选择 CesiumLoader 发布包": "CesiumLoader のリリースパッケージを選択",
      "选择动画帧（按文件名排序）": "アニメーションフレームを選択（ファイル名順）",
      "选择吉星派对回放文件": "Astral Party のリプレイファイルを選択",
      "选择吉星派对游戏目录（AstralParty exe 所在文件夹）": "Astral Party のゲームディレクトリを選択（AstralParty の exe があるフォルダー）",
      "选择回放库目录（不要和游戏的 Temp\\Replay 选成同一个）": "リプレイ保管庫ディレクトリを選択（ゲームの Temp\\Replay と同じにしないでください）",
      "选择的角色皮肤与包内资源的准确配置匹配结果不符。": "選択したキャラクタースキンが、パッケージ内リソースの正確な設定とのマッチ結果と一致しません。",
      "选择皮肤压缩包 (支持 cesium-default-skins 或自定义皮肤 ZIP)": "スキンの圧縮ファイルを選択（cesium-default-skins またはカスタムスキン ZIP に対応）",
      "选择目标必须同时指定角色与皮肤 ItemID。": "対象を選択するにはキャラクターとスキンの ItemID の両方を指定する必要があります。",
      "选择要导入回放库的回放文件": "リプレイ保管庫にインポートするリプレイファイルを選択",
      "选择要导入的模组（DLL 或 ZIP 模组包）": "インポートする Mod を選択（DLL または ZIP の Mod パッケージ）",
      "选择视频 / GIF，下一步裁剪普通卡面（404×400）": "動画 / GIF を選択。次の手順で通常カードイラスト（404×400）にトリミングします",
      "选择角色图片": "キャラクター画像を選択",
      "部分精灵没有被动作引用，不能部分导入。": "一部のスプライトがアクションから参照されていないため、部分インポートできません。",
      "配置内容为空，无法保存。": "設定内容が空のため保存できません。",
      "配置已保存到本地主配置，安装变速器时会一并复制到游戏目录。": "設定をローカルのメイン設定に保存しました。変速機のインストール時にゲームディレクトリへまとめてコピーされます。",
      "配置已同步到游戏目录，重启游戏后生效。": "設定をゲームディレクトリに同期しました。ゲーム再起動後に反映されます。",
      "配置文件不是有效的 JSON，请先用「用编辑器打开」修复，或点「恢复默认」。": "設定ファイルが有効な JSON ではありません。「エディターで開く」で修復するか、「既定に戻す」を押してください。",
      "锚点、缩放或 PPU 超出允许范围": "アンカー、スケール、PPU が許容範囲を超えています",
      "阶段3(实测作废、hook 从未命中; 保留无害)": "フェーズ3（実測で無効、hook は一度もヒットせず。残しても無害）",
      "附加保险: 让 SteamAPI_RestartAppIfNecessary 恒返回 0": "追加の保険: SteamAPI_RestartAppIfNecessary を常に 0 を返すようにする",
      "首次使用：正在下载 FFmpeg 转换工具…": "初回使用：FFmpeg 変換ツールをダウンロード中…",
      "默认包中没有卡面图片": "既定パッケージにカードイラストがありません",
      "默认包内容不符合预期": "既定パッケージの内容が想定と一致しません",
      "默认包解压体积过大": "既定パッケージの展開後サイズが大きすぎます",
      "默认方案 (default) 禁止删除。": "既定の構成（default）は削除できません。",
      "默认皮肤包已下载并安装，可选择 default 方案使用。": "既定のスキンパックをダウンロードしてインストールしました。default 構成を選択して使用できます。",
      "默认皮肤包超过 256 MB": "既定のスキンパックが 256 MB を超えています",
      "（提示：Steam 没在运行，加载器的「Steam 绕过」是关的，游戏可能会在启动早期自己退出 ——": "（ヒント：Steam が実行されておらず、ローダーの「Steam バイパス」がオフのため、ゲームが起動直後に自動終了する可能性があります ——",
      "（提示：Steam 没在运行，而且还没安装加载器 —— 缺了它的 Steam 绕过，游戏会在启动早期自己退出。": "（ヒント：Steam が実行されておらず、ローダーもインストールされていません —— ローダーの Steam バイパスがないため、ゲームは起動直後に自動終了します。",
      "（这个版本没有填写发布说明。）": "（このバージョンにはリリースノートがありません。）",
      "，已添加到方案": "、構成に追加しました",
      "，并在控制台窗口显示日志。": "。コンソールウィンドウにログを表示します。",
      "：移除与目录块尾部完全相同的": "：ディレクトリブロック末尾と完全に同じ",
      "；缺失：": "；欠落："
    },
    "zh-Hant": {
      "—— 点「检查更新」看看 GitHub 上有没有新版。": "—— 點「檢查更新」看看 GitHub 上有沒有新版。",
      "—— 同时安装会互相覆盖，游戏只会加载其中一个。": "—— 同時安裝會互相覆蓋，遊戲只會載入其中一個。",
      "——安装加载器、变速、导入模组都作用于当前这个。": "——安裝載入器、變速、匯入模組都作用於當前這個。",
      "——这是客户端写死的上限，和服务器无关：超了只是游戏拒绝保存新回放。把回放归档到下面的库目录即可腾出席位，需要时再一键放回游戏。": "——這是客戶端寫死的上限，和伺服器無關：超了只是遊戲拒絕儲存新重播。把重播歸檔到下面的庫目錄即可騰出席位，需要時再一鍵放回遊戲。",
      "，按": "，按",
      "，比随程序内置的 v": "，比隨程式內建的 v",
      "，点「更新到 v": "，點「更新到 v",
      "，或在游戏内按重载热键（默认 Ctrl+Shift+R）。": "，或在遊戲內按過載熱鍵（預設 Ctrl+Shift+R）。",
      "，两个只能装一个": "，兩個只能裝一個",
      "，请更新到": "，請更新到",
      "，然后重启游戏以使用最新修复。": "，然後重啟遊戲以使用最新修復。",
      "，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。": "，與隨程式內建的版本一致 —— 點「檢查更新」可確認 GitHub 上有沒有新版。",
      "，sidecar 与配置（": "，sidecar 與配置（",
      "；加载器本身已内置变速，装它就够了。": "；載入器本身已內建變速，裝它就夠了。",
      "：不用打开游戏，也不占游戏内的 10 个席位。": "：不用開啟遊戲，也不佔遊戲內的 10 個席位。",
      "：启用 = 启动游戏即按该倍速运行（全程保持）， 禁用 = 恢复 1.0 正常速度。最低 1.0（加载器不支持减速），重启游戏生效。": "：啟用 = 啟動遊戲即按該倍速執行（全程保持）， 禁用 = 恢復 1.0 正常速度。最低 1.0（載入器不支援減速），重啟遊戲生效。",
      "：已装": "：已裝",
      "：游戏库（多游戏目录 / 当前游戏）、变速器与加载器状态、回放库设置都存在这里。把库目录改到别处只会移动回放归档，上述配置文件仍留在默认目录。": "：遊戲庫（多遊戲目錄 / 當前遊戲）、變速器與載入器狀態、重播庫設定都存在這裡。把庫目錄改到別處只會移動重播歸檔，上述配置檔案仍留在預設目錄。",
      "…（卸载时按此校验，防止误删他人文件）": "…（解除安裝時按此校驗，防止誤刪他人檔案）",
      "。 确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。": "。 確認要繼續放回，就點下面的按鈕；庫裡的副本無論如何都會保留。",
      "。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。": "。點「恢復 version.dll」；本地檔案缺失時下載對應版本釋出包。",
      "· 按住生效": "· 按住生效",
      "· 本地构建": "· 本地構建",
      "· 编辑此人物的自定义素材": "· 編輯此人物的自定義素材",
      "· 等级": "· 等級",
      "· 第": "· 第",
      "· 点按切换": "· 點按切換",
      "· 累计转账": "· 累計轉賬",
      "· 模组": "· 模組",
      "· 提交": "· 提交",
      "· 游戏运行中": "· 遊戲執行中",
      "· 最终首领": "· 最終首領",
      "‹ 返回大厅": "‹ 返回大廳",
      "\" alt=\"第": "\" alt=\"第",
      "\" aria-pressed=\"false\" aria-label=\"选择第": "\" aria-pressed=\"false\" aria-label=\"選擇第",
      "（本地构建）": "（本地構建）",
      "（本页的旧版变速器自带独立的配置与热键，功能到此为止。）": "（本頁的舊版變速器自帶獨立的配置與熱鍵，功能到此為止。）",
      "（创建后不可修改）": "（建立後不可修改）",
      "（构建于 2024-04-13）。": "（構建於 2024-04-13）。",
      "（回退 AppData）": "（回退 AppData）",
      "（文档目录）": "（文件目錄）",
      "（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；": "（與 mod 的 dll 同目錄，由 mod 的 SdkConfig 讀寫；重啟遊戲後生效）。勾選框 = 開關，數字框 = 數值，文字框 = 文字；",
      "（CesiumLoader，Doorstop 式代理）放到游戏 exe 旁边；游戏启动时由 UnityPlayer 自动加载，再读取": "（CesiumLoader，Doorstop 式代理）放到遊戲 exe 旁邊；遊戲啟動時由 UnityPlayer 自動載入，再讀取",
      "(SFW 审查版本)": "(SFW 審查版本)",
      ") 恢复为应用内置的原始卡面吗？": ") 恢復為應用內建的原始卡面嗎？",
      "）， 模组加载器用的也是同一个文件名": "）， 模組載入器用的也是同一個檔名",
      "）都在同一个文件夹里。": "）都在同一個資料夾裡。",
      "）为准：安装或更新时写入，界面读它显示「已安装」。看不出（0.4.7 之前的老安装、或清单被删）时界面会如实显示「版本未知」——重新安装一次即可纳入版本管理。": "）為準：安裝或更新時寫入，介面讀它顯示「已安裝」。看不出（0.4.7 之前的老安裝、或清單被刪）時介面會如實顯示「版本未知」——重新安裝一次即可納入版本管理。",
      "）做依赖排序与 SDK 版本协商，然后逐个加载，并在控制台窗口按颜色分级打印加载报告。不会修改游戏文件本身。": "）做依賴排序與 SDK 版本協商，然後逐個載入，並在控制檯視窗按顏色分級列印載入報告。不會修改遊戲檔案本身。",
      "[当前生效]": "[當前生效]",
      "」改这个模组自己的配置。": "」改這個模組自己的配置。",
      "→ 包内": "→ 包內",
      "→ 请到「": "→ 請到「",
      "→ 最新 v": "→ 最新 v",
      "＋ 按键": "＋ 按鍵",
      "＋ 打开回放": "＋ 開啟重播",
      "＋ 导入模组": "＋ 匯入模組",
      "＋ 添加倍速档位": "＋ 新增倍速檔位",
      "＋ 新建方案": "＋ 新建方案",
      "＋ 选择回放文件": "＋ 選擇重播檔案",
      "= 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。": "= 點一下再按要綁的鍵（鍵盤任意鍵或滑鼠左/右/中鍵、側鍵，Esc 取消）。",
      "= 全程 2 倍速；进游戏后还能用": "= 全程 2 倍速；進遊戲後還能用",
      "= 正常，": "= 正常，",
      "⚠ 方案B 备用安全网 —— 实测开启会导致游戏启动崩溃（0xC0000005），请保持关闭": "⚠ 方案B 備用安全網 —— 實測開啟會導致遊戲啟動崩潰（0xC0000005），請保持關閉",
      "0 张卡面": "0 張卡面",
      "1.0 = 正常速度": "1.0 = 正常速度",
      "1.0 = 正常速度；最低 1.0，不支持减速": "1.0 = 正常速度；最低 1.0，不支援減速",
      "10 个席位": "10 個席位",
      "安装": "安裝",
      "安装：": "安裝：",
      "安装」即可。": "安裝」即可。",
      "安装加载器": "安裝載入器",
      "安装加载器时一并装上内置模组（行为日志 + 自由相机 + 变速）": "安裝載入器時一併裝上內建模組（行為日誌 + 自由相機 + 變速）",
      "安装角色皮肤模组": "安裝角色造型模組",
      "安装清单": "安裝清單",
      "安装与覆盖选项": "安裝與覆蓋選項",
      "安装中…": "安裝中…",
      "按 ID 下载": "按 ID 下載",
      "按编号导入动态卡面": "按編號匯入動態卡面",
      "按编号导入动态卡面…": "按編號匯入動態卡面…",
      "按对局时间": "按對局時間",
      "按回放 ID": "按重播 ID",
      "按回放 ID 下载到回放库": "按重播 ID 下載到重播庫",
      "按键按钮": "按鍵按鈕",
      "按来源筛选": "按來源篩選",
      "按玩家、回合、技能或动作搜索...": "按玩家、回合、技能或動作搜尋...",
      "按文件大小": "按檔案大小",
      "按下按键…（Esc 取消）": "按下按鍵…（Esc 取消）",
      "按资源比例裁剪，或保留完整图片": "按資源比例裁剪，或保留完整圖片",
      "把模组日志转发到控制台": "把模組日誌轉發到控制檯",
      "把内置": "把內建",
      "把喜欢的人物放进同一个方案": "把喜歡的人物放進同一個方案",
      "版本": "版本",
      "版本读取中…": "版本讀取中…",
      "版本未知": "版本未知",
      "版本详情": "版本詳情",
      "版本信息": "版本資訊",
      "版本信息只以": "版本資訊只以",
      "保持选中帧的画布尺寸；透明边缘和脚底位置会影响对齐。": "保持選中幀的畫布尺寸；透明邊緣和腳底位置會影響對齊。",
      "保存布局与动作参数": "儲存佈局與動作引數",
      "保存裁剪": "儲存裁剪",
      "保存尺寸": "儲存尺寸",
      "保存动作参数": "儲存動作引數",
      "保存多个游戏位置（国服 / 国际服 / TapTap 端…）。在「模组」页顶部可切换": "儲存多個遊戲位置（國服 / 國際服 / TapTap 端…）。在「模組」頁頂部可切換",
      "保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。": "儲存後重啟遊戲生效；日常只需要改「啟用載入器」或「啟動時基礎倍速」。",
      "保存配置": "儲存配置",
      "保存设置": "儲存設定",
      "保存失败": "儲存失敗",
      "保存失败，请重试。": "儲存失敗，請重試。",
      "保存为透明 PNG，尺寸": "儲存為透明 PNG，尺寸",
      "保存文案": "儲存文案",
      "保存信息": "儲存資訊",
      "保留的组件无法确定加载器版本，请重新安装或更新加载器。": "保留的元件無法確定載入器版本，請重新安裝或更新載入器。",
      "备份并删除中…": "備份並刪除中…",
      "备份后只删除 version.dll，保留所有模组与配置": "備份後只刪除 version.dll，保留所有模組與配置",
      "背景物品图标": "背景物品圖示",
      "倍速": "倍速",
      "倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度；加载器不支持减速）": "倍速必須在 1.0 ~ 100 之間（1.0 = 正常速度；載入器不支援減速）",
      "倍速档位（满足按键组合时切换速度）": "倍速檔位（滿足按鍵組合時切換速度）",
      "倍速基于修改进程时间函数，需在游戏设置里": "倍速基於修改程序時間函式，需在遊戲設定裡",
      "被占用时写入或删除会失败——那种情况先完全退出游戏再操作。": "被佔用時寫入或刪除會失敗——那種情況先完全退出遊戲再操作。",
      "本程序缺少内置加载器文件，无法安装 —— 请使用完整发布包（推荐 portable.zip）。": "本程式缺少內建載入器檔案，無法安裝 —— 請使用完整發布包（推薦 portable.zip）。",
      "本程序缺少内置加载器资源，安装按钮不会生效 —— 请重新下载完整发布包（推荐 portable.zip，single-file 版偶发解包不完整）。": "本程式缺少內建載入器資源，安裝按鈕不會生效 —— 請重新下載完整發布包（推薦 portable.zip，single-file 版偶發解包不完整）。",
      "本地构建": "本地構建",
      "本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，": "本工具讀不出它們的結算幀（工具自帶的協議版本可能比遊戲舊）。如果遊戲也讀不出來，",
      "本回合转账": "本回合轉賬",
      "本局地图": "本局地圖",
      "本局没有商店进店记录": "本局沒有商店進店記錄",
      "本页安装的是": "本頁安裝的是",
      "比 GitHub 上的 v": "比 GitHub 上的 v",
      "比内置的 v": "比內建的 v",
      "编辑方案信息": "編輯方案資訊",
      "编辑配置 JSON": "編輯配置 JSON",
      "编辑人物皮肤配置": "編輯人物造型配置",
      "变速器已就位": "變速器已就位",
      "变速器已就位；游戏正在运行，新写入的文件要重启游戏才加载": "變速器已就位；遊戲正在執行，新寫入的檔案要重啟遊戲才載入",
      "变速已禁用": "變速已禁用",
      "变速已启用": "變速已啟用",
      "表现分析": "表現分析",
      "播放预览": "播放預覽",
      "不裁剪，等比缩放并透明补边": "不裁剪，等比縮放並透明補邊",
      "不兼容": "不相容",
      "不是本工具装的文件 —— 见下方「高级与排障」": "不是本工具裝的檔案 —— 見下方「高階與排障」",
      "不添加自定义覆盖": "不新增自定義覆蓋",
      "不装 Steam 也能启动游戏（首页「启动方式」把这一套三个开关一起开 / 关）": "不裝 Steam 也能啟動遊戲（首頁「啟動方式」把這一套三個開關一起開 / 關）",
      "不装任何 mod、只用加速 → 就留在本页，按下面卡片里的「": "不裝任何 mod、只用加速 → 就留在本頁，按下面卡片裡的「",
      "才生效，而且 DLL 被占用时写入/删除会失败——那种情况先完全退出游戏再操作即可。": "才生效，而且 DLL 被佔用時寫入/刪除會失敗——那種情況先完全退出遊戲再操作即可。",
      "才生效；修改配置后": "才生效；修改配置後",
      "裁剪角色素材": "裁剪角色素材",
      "裁剪卡面": "裁剪卡面",
      "裁剪为目标比例": "裁剪為目標比例",
      "裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置": "裁剪選區：拖動移動，拖動四角調整大小，方向鍵微調位置",
      "裁剪预览": "裁剪預覽",
      "参战玩家 (": "參戰玩家 (",
      "操作": "操作",
      "操作失败。": "操作失敗。",
      "操作完成。": "操作完成。",
      "操作系统": "作業系統",
      "查看": "檢視",
      "查看完整更新日志 ›": "檢視完整更新日誌 ›",
      "查看详细版本信息": "檢視詳細版本資訊",
      "常见位置": "常見位置",
      "常用": "常用",
      "称号": "稱號",
      "承受伤害": "承受傷害",
      "程序集版本": "程式集版本",
      "程序集文件时间": "程式集檔案時間",
      "程序目录": "程式目錄",
      "程序缺少变速器文件": "程式缺少變速器檔案",
      "程序缺少内置加载器文件": "程式缺少內建載入器檔案",
      "尺寸来自游戏原资源": "尺寸來自遊戲原資源",
      "筹码": "籌碼",
      "筹码记录": "籌碼記錄",
      "筹码记录共": "籌碼記錄共",
      "筹码名称": "籌碼名稱",
      "筹码选择品质分布": "籌碼選擇品質分佈",
      "出牌": "出牌",
      "出牌次数": "出牌次數",
      "处理方式": "處理方式",
      "处理失败": "處理失敗",
      "传说": "傳說",
      "创建": "建立",
      "创建成功后自动添加到当前方案，并进入此人物的图片、文案和动画编辑器。": "建立成功後自動新增到當前方案，並進入此人物的圖片、文案和動畫編輯器。",
      "创建方案": "建立方案",
      "创建后会自动加入当前方案，接着编辑图片、动画和文案。": "建立後會自動加入當前方案，接著編輯圖片、動畫和文案。",
      "创建失败": "建立失敗",
      "此动作没有自制帧。导入帧序列后可逐帧查看和替换。": "此動作沒有自制幀。匯入幀序列後可逐幀檢視和替換。",
      "此动作使用原精灵映射：替换单帧会同步游戏映射；同一原帧被重复引用时会一并更新。重新导入整组帧会改为独立帧序列。": "此動作使用原精靈對映：替換單幀會同步遊戲對映；同一原幀被重複引用時會一併更新。重新匯入整組幀會改為獨立幀序列。",
      "此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信": "此模組宣告可操作遊戲（模擬出牌 / 擲骰 / 移動等）。許可權機制已取消，這裡只提示：請確認來源可信",
      "此人物的相同原皮肤已有规则，请编辑原规则。": "此人物的相同原造型已有規則，請編輯原規則。",
      "此人物的相同原皮肤已有配置，请编辑已有的人物皮肤。": "此人物的相同原造型已有配置，請編輯已有的人物造型。",
      "此人物还没有皮肤包。可导入 ZIP，或选择「新建自定义皮肤」。": "此人物還沒有造型包。可匯入 ZIP，或選擇「新建自定義造型」。",
      "次": "次",
      "次 · 平均每回合": "次 · 平均每回合",
      "次（其中": "次（其中",
      "次商店进店": "次商店進店",
      "次有购买）": "次有購買）",
      "次掷骰 ·": "次擲骰 ·",
      "从 GitHub 更新": "從 GitHub 更新",
      "从 Steam 启动": "從 Steam 啟動",
      "从 ZIP 导入此人物皮肤包": "從 ZIP 匯入此人物造型包",
      "从方案中创建会自动加入该方案。你可以导出原版素材作为起点，也可以取消勾选，从空白皮肤开始。": "從方案中建立會自動加入該方案。你可以匯出原版素材作為起點，也可以取消勾選，從空白造型開始。",
      "从列表移除（不会删除游戏文件）": "從列表移除（不會刪除遊戲檔案）",
      "从内置模板新建…": "從內建模板新建…",
      "从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。": "從應用內建預設包建立全新方案，不使用磁碟上已經修改過的圖片。無需聯網，新建後可更換單張卡面。",
      "从游戏导出官方皮肤素材": "從遊戲匯出官方造型素材",
      "从游戏缓存中选择一局，或手动打开回放文件": "從遊戲快取中選擇一局，或手動開啟重播檔案",
      "错误：": "錯誤：",
      "打开": "開啟",
      "打开 mods 目录": "開啟 mods 目錄",
      "打开 sdk 目录": "開啟 sdk 目錄",
      "打开本方案目录": "開啟本方案目錄",
      "打开变速器自带的日志控制台窗口": "開啟變速器自帶的日誌控制檯視窗",
      "打开方案目录": "開啟方案目錄",
      "打开库目录": "開啟庫目錄",
      "打开目录": "開啟目錄",
      "打开目录修复": "開啟目錄修復",
      "打开皮肤目录": "開啟造型目錄",
      "打开日志目录": "開啟日誌目錄",
      "大小:": "大小:",
      "单次播放": "單次播放",
      "当前": "當前",
      "当前报告没有逐回合数据，请重新打开原始回放。": "當前報告沒有逐回合資料，請重新開啟原始重播。",
      "当前方案已切换，请重新打开规则编辑。": "當前方案已切換，請重新開啟規則編輯。",
      "当前方案已切换，请重新添加人物。": "當前方案已切換，請重新新增人物。",
      "当前生效中": "當前生效中",
      "当前星币": "當前星幣",
      "当前选择 · 重启游戏生效": "當前選擇 · 重啟遊戲生效",
      "当前游戏": "當前遊戲",
      "当前正在操作的游戏": "當前正在操作的遊戲",
      "档位": "檔位",
      "导出 AI 文本 (TXT)": "匯出 AI 文字 (TXT)",
      "导出 JSON": "匯出 JSON",
      "导出 ZIP": "匯出 ZIP",
      "导出表格 (CSV)": "匯出表格 (CSV)",
      "导出当前动作 GIF": "匯出當前動作 GIF",
      "导出哪套官方皮肤": "匯出哪套官方造型",
      "导出所选皮肤并创建": "匯出所選造型並建立",
      "导出所选皮肤的图片与二维动画作为编辑起点。方案中的替换范围保持原设置；独立创建的包默认对应所选皮肤。资源未下载时，请先在游戏中查看该皮肤。": "匯出所選造型的圖片與二維動畫作為編輯起點。方案中的替換範圍保持原設定；獨立建立的包預設對應所選造型。資源未下載時，請先在遊戲中檢視該造型。",
      "导入 ZIP": "匯入 ZIP",
      "导入本地 cesium-loader 发布 ZIP，安装加载器、SDK 与包内模组": "匯入本地 cesium-loader 釋出 ZIP，安裝載入器、SDK 與包內模組",
      "导入单个模组 DLL，或整个模组包（ZIP，解压到 mods 目录）": "匯入單個模組 DLL，或整個模組包（ZIP，解壓到 mods 目錄）",
      "导入发布包": "匯入釋出包",
      "导入方案 ZIP": "匯入方案 ZIP",
      "导入回放": "匯入重播",
      "导入结束。": "匯入結束。",
      "导入旧式皮肤包": "匯入舊式造型包",
      "导入皮肤包 (.zip)": "匯入造型包 (.zip)",
      "导入人物包": "匯入人物包",
      "导入视频 / GIF": "匯入影片 / GIF",
      "导入与管理": "匯入與管理",
      "导入帧序列": "匯入幀序列",
      "导入中…": "匯入中…",
      "倒计时 / 地图像素头像": "倒計時 / 地影象素頭像",
      "地块标志": "地塊標誌",
      "地图人物布局": "地圖人物佈局",
      "地图事件": "地圖事件",
      "地图事件 (": "地圖事件 (",
      "的": "的",
      "的皮肤": "的造型",
      "的自制帧和原精灵映射，重启游戏后恢复原动作。": "的自制幀和原精靈對映，重啟遊戲後恢復原動作。",
      "等待 GameAssembly.dll 秒数": "等待 GameAssembly.dll 秒數",
      "等待 HybridCLR 热更秒数": "等待 HybridCLR 熱更秒數",
      "等待托管域初始化秒数": "等待託管域初始化秒數",
      "等待游戏配置": "等待遊戲配置",
      "等待中": "等待中",
      "第": "第",
      "点 · 受控": "點 · 受控",
      "点「安装加载器」把加载器写进游戏目录": "點「安裝載入器」把載入器寫進遊戲目錄",
      "点「安装加载器」装的就是这一份": "點「安裝載入器」裝的就是這一份",
      "点「查看完整更新日志」了解本次更新": "點「檢視完整更新日誌」瞭解本次更新",
      "点「检查更新」获取": "點「檢查更新」獲取",
      "点「检查更新」向 GitHub 查询": "點「檢查更新」向 GitHub 查詢",
      "点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。": "點選選擇 / 取消選擇，確認後應用到該檔位。檔位按鍵可多選（如 Ctrl + Shift）。",
      "调试控制台": "除錯控制檯",
      "调试控制台 console": "除錯控制檯 console",
      "调整界面偏好并查看应用信息": "調整介面偏好並檢視應用資訊",
      "动画": "動畫",
      "动画偏好": "動畫偏好",
      "动画帧预览": "動畫幀預覽",
      "动画逐帧图片": "動畫逐幀圖片",
      "动态": "動態",
      "动态卡面 ·": "動態卡面 ·",
      "动作": "動作",
      "读不到更新日志内容。": "讀不到更新日誌內容。",
      "读不到加载器更新日志（内置为空，联网获取也没成功）。点「": "讀不到載入器更新日誌（內建為空，聯網獲取也沒成功）。點「",
      "对局表现分析": "對局表現分析",
      "对局回放分析工具": "對局重播分析工具",
      "对局结果": "對局結果",
      "对局摘要": "對局摘要",
      "对战回放": "對戰重播",
      "多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。": "多半是別的工具（例如「遊戲工具」頁的舊版獨立變速器）或第三方放進來的檔案。",
      "多人物搭配 ·": "多人物搭配 ·",
      "二维精灵动画": "二維精靈動畫",
      "发布包完整路径": "釋出包完整路徑",
      "发布包正在导入，请稍候。": "釋出包正在匯入，請稍候。",
      "发现新版本：已装 v": "發現新版本：已裝 v",
      "繁體中文": "繁體中文",
      "反馈问题时把这段信息一起贴上，能省很多来回。": "反饋問題時把這段資訊一起貼上，能省很多來回。",
      "返回当前方案": "返回當前方案",
      "返回吉星派对大厅": "返回吉星派對大廳",
      "方案：": "方案：",
      "方案标识：": "方案標識：",
      "方案会移入 .deleted 文件夹，引用的自定义皮肤包会保留。": "方案會移入 .deleted 資料夾，引用的自定義造型包會保留。",
      "方案简介": "方案簡介",
      "方案描述（可选）：": "方案描述（可選）：",
      "方案名称": "方案名稱",
      "方案内图片": "方案內圖片",
      "方案设置": "方案設定",
      "方案显示名称：": "方案顯示名稱：",
      "方案英文目录名（唯一标识）：": "方案英文目錄名（唯一標識）：",
      "方案中的人物": "方案中的人物",
      "方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。": "方向鍵微調位置，按住 Shift 每次移動 10 畫素。原圖不會被修改。",
      "放到游戏 exe 旁边，游戏启动时自动加载。游戏运行中也允许安装/卸载，只是本次写入要": "放到遊戲 exe 旁邊，遊戲啟動時自動載入。遊戲執行中也允許安裝/解除安裝，只是本次寫入要",
      "放回游戏": "放回遊戲",
      "份）": "份）",
      "附加保险：忽略 Steam 的“请从 Steam 启动”重启请求": "附加保險：忽略 Steam 的“請從 Steam 啟動”重啟請求",
      "复制版本信息": "複製版本資訊",
      "覆盖已有 ZIP？": "覆蓋已有 ZIP？",
      "该角色全部原皮肤": "該角色全部原造型",
      "该人物全部原皮肤": "該人物全部原造型",
      "该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。": "該玩家本回合快照區間內的轉賬增量；同回合多次進店顯示相同值，不重複累計。",
      "高级": "高階",
      "高级：编辑完整清单": "高階：編輯完整清單",
      "高级：用系统默认编辑器直接改 JSON": "高階：用系統預設編輯器直接改 JSON",
      "高级编辑…": "高階編輯…",
      "高级设置：替换范围与混合搭配": "高階設定：替換範圍與混合搭配",
      "高级选项（Steam 绕过细节 · 引导 · 超时）": "高階選項（Steam 繞過細節 · 引導 · 超時）",
      "高级与排障": "高階與排障",
      "个）": "個）",
      "个候选：": "個候選：",
      "个进行中": "個進行中",
      "个人物": "個人物",
      "个人物 ·": "個人物 ·",
      "个位置": "個位置",
      "更换图片": "更換圖片",
      "更新，无需更新。": "更新，無需更新。",
      "更新: —": "更新: —",
      "更新（从 GitHub 更新过）": "更新（從 GitHub 更新過）",
      "更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。": "更新（這份是從 GitHub 更新來的）—— 點「檢查更新」看看還有沒有更新的版本。",
      "更新到 v": "更新到 v",
      "更新到最新": "更新到最新",
      "更新角色皮肤模组": "更新角色造型模組",
      "更新日志": "更新日誌",
      "更新时允许覆盖游戏目录里不属于本工具的 version.dll": "更新時允許覆蓋遊戲目錄裡不屬於本工具的 version.dll",
      "更新时允许降级（仅在发布包比已装版本旧时才需要）": "更新時允許降級（僅在釋出包比已裝版本舊時才需要）",
      "更新中…": "更新中…",
      "工具来源：": "工具來源：",
      "工具箱已更新": "工具箱已更新",
      "功能键": "功能鍵",
      "攻击 / 防御": "攻擊 / 防禦",
      "共": "共",
      "共 0 张卡面": "共 0 張卡面",
      "共筛选出": "共篩選出",
      "构建配置": "構建配置",
      "购买结果": "購買結果",
      "挂接延迟 wait_with_hook": "掛接延遲 wait_with_hook",
      "挂接延迟（毫秒）": "掛接延遲（毫秒）",
      "关闭": "關閉",
      "关闭垂直同步": "關閉垂直同步",
      "关闭消息": "關閉訊息",
      "关闭自动倍速时固定为 1.0；快捷键档位仍可临时切换速度": "關閉自動倍速時固定為 1.0；快捷鍵檔位仍可臨時切換速度",
      "关卡进度": "關卡進度",
      "关于": "關於",
      "官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。": "官方來源替換圖片與人物動畫。語音、影片、特效及特殊演出保留原版。",
      "官方皮肤": "官方造型",
      "官方皮肤基础": "官方造型基礎",
      "管理游戏目录…": "管理遊戲目錄…",
      "归档": "歸檔",
      "归档最旧的": "歸檔最舊的",
      "归档最旧的 3 局": "歸檔最舊的 3 局",
      "国服": "國服",
      "国服（测试服）": "國服（測試服）",
      "国服（正式服）": "國服（正式服）",
      "国际服": "國際服",
      "国际服（测试服）": "國際服（測試服）",
      "国际服（正式服）": "國際服（正式服）",
      "还没有倍速档位——点下面「＋ 添加倍速档位」创建一个，例如 Ctrl = 2 倍速。": "還沒有倍速檔位——點下面「＋ 新增倍速檔位」建立一個，例如 Ctrl = 2 倍速。",
      "还没有回放。在游戏里打完一局后，到「战绩」里对那条记录点「保存回放」，再回来刷新即可。": "還沒有重播。在遊戲裡打完一局後，到「戰績」裡對那條記錄點「儲存重播」，再回來重新整理即可。",
      "还没有模组 —— 点「导入模组」添加 DLL 或 ZIP 模组包，或重新安装加载器时勾选内置模组": "還沒有模組 —— 點「匯入模組」新增 DLL 或 ZIP 模組包，或重新安裝載入器時勾選內建模組",
      "还没有配置文件（mods\\": "還沒有配置檔案（mods\\",
      "还没有添加人物": "還沒有新增人物",
      "还没有添加游戏目录。": "還沒有新增遊戲目錄。",
      "还没找到游戏目录 —— 展开「高级与排障」点「自动检测」，或手动选择游戏 exe 所在的文件夹。": "還沒找到遊戲目錄 —— 展開「高階與排障」點「自動檢測」，或手動選擇遊戲 exe 所在的資料夾。",
      "合计": "合計",
      "后台皮肤任务": "後臺造型任務",
      "候选卡牌": "候選卡牌",
      "候选选项": "候選選項",
      "恢复 version.dll": "恢復 version.dll",
      "恢复此动作": "恢復此動作",
      "恢复此动作？": "恢復此動作？",
      "恢复当前版本；本地缺失时下载对应版本发布包": "恢復當前版本；本地缺失時下載對應版本釋出包",
      "恢复默认模板": "恢復預設模板",
      "恢复内置卡面": "恢復內建卡面",
      "恢复内置默认包": "恢復內建預設包",
      "恢复原版": "恢復原版",
      "恢复中…": "恢復中…",
      "回车": "回車",
      "回放": "重播",
      "回放 ID": "重播 ID",
      "回放 ID 只能是数字——就是游戏战绩里那一长串数字。": "重播 ID 只能是數字——就是遊戲戰績裡那一長串數字。",
      "回放分析与复盘工具": "重播分析與覆盤工具",
      "回放服务器": "重播伺服器",
      "回放服务器（不确定就按当前游戏版本选）": "重播伺服器（不確定就按當前遊戲版本選）",
      "回放复盘": "重播覆盤",
      "回放库": "重播庫",
      "回放库操作结果": "重播庫操作結果",
      "回放列表": "重播列表",
      "回放文件解析成功！": "重播檔案解析成功！",
      "回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。 填上回放 ID 就能直接下载进": "重播由伺服器錄製後放在官方重播伺服器上，遊戲裡點「儲存重播」也是從那裡拉的。 填上重播 ID 就能直接下載進",
      "回合": "回合",
      "回合 ·": "回合 ·",
      "回合：": "回合：",
      "或其中的卡面图片。": "或其中的卡面圖片。",
      "击杀": "擊殺",
      "击杀数量（怪物）": "擊殺數量（怪物）",
      "及其目录下的所有卡面图片吗？此操作不可撤销。": "及其目錄下的所有卡面圖片嗎？此操作不可撤銷。",
      "吉星派对 · BWiki 维基攻略": "吉星派對 · BWiki 維基攻略",
      "吉星派对回放与游戏工具箱": "吉星派對重播與遊戲工具箱",
      "集成回放分析、协议帧查看、维基浏览和游戏变速器管理，数据均在本地处理。": "整合重播分析、協議幀檢視、維基瀏覽和遊戲變速器管理，資料均在本地處理。",
      "技能使用": "技能使用",
      "继续创建皮肤包": "繼續建立造型包",
      "继续导入会用包里的旧版本覆盖现有文件。玩家改过的": "繼續匯入會用包裡的舊版本覆蓋現有檔案。玩家改過的",
      "加载器": "載入器",
      "加载器发布 ZIP 将一起安装加载器、SDK、bootstrap 和包内模组，保留已有加载器配置。仅使用可信的官方发布文件，重启游戏后生效。": "載入器釋出 ZIP 將一起安裝載入器、SDK、bootstrap 和包內模組，保留已有載入器配置。僅使用可信的官方釋出檔案，重啟遊戲後生效。",
      "加载器更新日志": "載入器更新日誌",
      "加载器内置功能": "載入器內建功能",
      "加载器缺失且版本未知": "載入器缺失且版本未知",
      "加载器设置": "載入器設定",
      "加载器已就位": "載入器已就位",
      "加载器与旧版独立变速器（「游戏工具」页）共用": "載入器與舊版獨立變速器（「遊戲工具」頁）共用",
      "检测到版本降级": "檢測到版本降級",
      "检测到游戏正在运行——安装 / 卸载不会被拦，但先看清下面的后果：": "檢測到遊戲正在執行——安裝 / 解除安裝不會被攔，但先看清下面的後果：",
      "检测中": "檢測中",
      "检查更新": "檢查更新",
      "检查更新失败：": "檢查更新失敗：",
      "检查失败": "檢查失敗",
      "检查失败：": "檢查失敗：",
      "检查中…": "檢查中…",
      "简体中文": "简体中文",
      "简要说明此方案的特色…": "簡要說明此方案的特色…",
      "将": "將",
      "将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。": "將從應用內建預設包恢復此卡面，覆蓋當前方案的同名圖片；磁碟上修改過的 default 不會被用作原始模板。",
      "将从应用内置资源恢复当前游戏的 default 方案，覆盖同名图片并移除 default 中的动态覆盖。不会改动其他方案，也不会自动切换当前选择。": "將從應用內建資源恢復當前遊戲的 default 方案，覆蓋同名圖片並移除 default 中的動態覆蓋。不會改動其他方案，也不會自動切換當前選擇。",
      "将从最新 Release 下载默认包并安装到当前游戏，覆盖 default 中的同名图片。下载不会自动切换当前方案。": "將從最新 Release 下載預設包並安裝到當前遊戲，覆蓋 default 中的同名圖片。下載不會自動切換當前方案。",
      "将含 bundle 的旧式卡面 ZIP 转换为卡牌皮肤方案": "將含 bundle 的舊式卡面 ZIP 轉換為卡牌造型方案",
      "将含 bundle 的旧式皮肤 ZIP 转换为自定义皮肤包": "將含 bundle 的舊式造型 ZIP 轉換為自定義造型包",
      "交给 Steam 启动（DRM / 云存档 / 更新检查都走 Steam），并关闭加载器的 Steam 绕过 —— Steam 大厅 / 好友邀请进房恢复正常（默认）": "交給 Steam 啟動（DRM / 雲存檔 / 更新檢查都走 Steam），並關閉載入器的 Steam 繞過 —— Steam 大廳 / 好友邀請進房恢復正常（預設）",
      "角色": "角色",
      "角色简介": "角色簡介",
      "角色名字": "角色名字",
      "角色皮肤": "角色造型",
      "角色皮肤 · 图片 / 文案 / 精灵动画": "角色造型 · 圖片 / 文案 / 精靈動畫",
      "角色皮肤管理": "角色造型管理",
      "角色皮肤模组已安装": "角色造型模組已安裝",
      "角色缩略图": "角色縮圖",
      "角色头像": "角色頭像",
      "角色文案": "角色文案",
      "脚底锚点 X": "腳底錨點 X",
      "脚底锚点 Y": "腳底錨點 Y",
      "阶段3 兜底（实测已作废、hook 从未命中；保持开启即可）": "階段3 兜底（實測已作廢、hook 從未命中；保持開啟即可）",
      "结构可疑": "結構可疑",
      "结算奖励": "結算獎勵",
      "结算快照中的完整属性与行动统计，记录本局关键数据指标。": "結算快照中的完整屬性與行動統計，記錄本局關鍵資料指標。",
      "结算快照中的总伤害、承伤、治疗、转账与行动次数。": "結算快照中的總傷害、承傷、治療、轉賬與行動次數。",
      "解析不出结算帧，文件可能已损坏或来自不兼容的版本。": "解析不出結算幀，檔案可能已損壞或來自不相容的版本。",
      "解析失败": "解析失敗",
      "解析展示失败:": "解析展示失敗:",
      "介绍一下这套搭配，例如适合的玩法或角色组合。": "介紹一下這套搭配，例如適合的玩法或角色組合。",
      "界面动态效果": "介面動態效果",
      "界面动态效果已": "介面動態效果已",
      "界面与配表语言": "介面與配表語言",
      "仅回放库": "僅重播庫",
      "仅删除 version.dll": "僅刪除 version.dll",
      "仅替换": "僅替換",
      "仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。": "僅統計重播中實際收到的移動擲骰結果；保留多骰的每顆點數，受控移動單獨標記，不混入戰鬥骰或檢定骰。",
      "仅游戏内": "僅遊戲內",
      "仅支持英文、数字、下划线及减号": "僅支援英文、數字、下劃線及減號",
      "进入游戏自动倍速": "進入遊戲自動倍速",
      "进游戏后等待再挂接时间函数的毫秒数（防崩溃）": "進遊戲後等待再掛接時間函式的毫秒數（防崩潰）",
      "旧 —— 点「重新安装内置版本」可装回内置版": "舊 —— 點「重新安裝內建版本」可裝回內建版",
      "旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。": "舊 —— 想回到內建版本就點「重新安裝內建版本」，想知道有沒有新版就點「檢查更新」。",
      "旧版独立变速器": "舊版獨立變速器",
      "旧配置已保留，但暂未转换：": "舊配置已保留，但暫未轉換：",
      "居中重置": "居中重置",
      "局": "局",
      "局 ·": "局 ·",
      "局）": "局）",
      "局）。": "局）。",
      "橘雪莉": "橘雪莉",
      "卡面详情预览": "卡面詳情預覽",
      "卡牌 / 技能": "卡牌 / 技能",
      "卡牌 / 异画编号": "卡牌 / 異畫編號",
      "卡牌编号:": "卡牌編號:",
      "卡牌名称:": "卡牌名稱:",
      "卡牌皮肤": "卡牌造型",
      "卡牌皮肤 · 方案与卡面管理": "卡牌造型 · 方案與卡面管理",
      "卡牌使用": "卡牌使用",
      "开局 / 全局事件": "開局 / 全域性事件",
      "开启": "開啟",
      "开启看板立绘与页面切换动画": "開啟看板立繪與頁面切換動畫",
      "开启自动托管（保留": "開啟自動託管（保留",
      "开始深扫": "開始深掃",
      "开始下载": "開始下載",
      "颗骰子 · 平均": "顆骰子 · 平均",
      "可更新": "可更新",
      "可以更新到 v": "可以更新到 v",
      "空格": "空格",
      "控制台窗口置顶": "控制檯視窗置頂",
      "口径": "口徑",
      "库目录": "庫目錄",
      "库目录不可用": "庫目錄不可用",
      "快速扫描没找到，试试下面的深度扫描。": "快速掃描沒找到，試試下面的深度掃描。",
      "来源": "來源",
      "来源：随本程序内置（打包时的版本）": "來源：隨本程式內建（打包時的版本）",
      "来源：GitHub 最新 Release": "來源：GitHub 最新 Release",
      "来自回合快照中的累计转账；未记录收款对象时不做推断。": "來自回合快照中的累計轉賬；未記錄收款物件時不做推斷。",
      "类别:": "類別:",
      "累计": "累計",
      "累计伤害": "累計傷害",
      "累计死亡": "累計死亡",
      "累计星币": "累計星幣",
      "累计转账": "累計轉賬",
      "离上限只差一点。开启自动托管后，每次打开本工具都会自动把超出的旧回放归档进库。": "離上限只差一點。開啟自動託管後，每次開啟本工具都會自動把超出的舊重播歸檔進庫。",
      "立绘物品图标": "立繪物品圖示",
      "立即复制创建": "立即複製建立",
      "立即整理一次": "立即整理一次",
      "例如 10001": "例如 10001",
      "例如：我的日常搭配": "例如：我的日常搭配",
      "两边都删": "兩邊都刪",
      "浏览…": "瀏覽…",
      "浏览器打开": "瀏覽器開啟",
      "浏览添加…": "瀏覽新增…",
      "锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。": "錨點使用 0～1，Y 從畫布底部向上計算；縮放乘在 PPU 上，不修改遊戲人物碰撞或移動。",
      "没检测到标准 Unity 结构": "沒檢測到標準 Unity 結構",
      "没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）": "沒檢測到標準 Unity 遊戲結構（缺 *_Data 或 UnityPlayer.dll）",
      "没有检测到桌面宿主，读不到程序集版本": "沒有檢測到桌面宿主，讀不到程式集版本",
      "没有内嵌发布包": "沒有內嵌釋出包",
      "没有内嵌发布包（本地开发构建）": "沒有內嵌釋出包（本地開發構建）",
      "没有匹配的方案。": "沒有匹配的方案。",
      "没有匹配的皮肤包。可新建皮肤包或导入 ZIP。": "沒有匹配的造型包。可新建造型包或匯入 ZIP。",
      "每次打开本工具时，把超出保留名额的旧回放自动归档进库；最新的几局永远不会被动。": "每次開啟本工具時，把超出保留名額的舊重播自動歸檔進庫；最新的幾局永遠不會被動。",
      "每次进入游戏自动启用设定倍速": "每次進入遊戲自動啟用設定倍速",
      "每单位像素 PPU": "每單位畫素 PPU",
      "每个模组是引用 CesiumLoader.SDK 的 .NET DLL，一个模组一个文件夹：": "每個模組是引用 CesiumLoader.SDK 的 .NET DLL，一個模組一個資料夾：",
      "每回合新增": "每回合新增",
      "秒循环": "秒迴圈",
      "名)": "名)",
      "名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。": "名字與角色簡介使用角色共用文案，可能影響該角色的其他造型。",
      "命运卡": "命運卡",
      "命运卡 (": "命運卡 (",
      "模组": "模組",
      "模组」页面。": "模組」頁面。",
      "模组管理": "模組管理",
      "模组页面内置换速开关：": "模組頁面內建換速開關：",
      "模组与配置已保留，可恢复加载器": "模組與配置已保留，可恢復載入器",
      "模组状态与使用说明": "模組狀態與使用說明",
      "默认：": "預設：",
      "默认：文档\\AstralPartyReplays": "預設：文件\\AstralPartyReplays",
      "默认方案不可删除": "預設方案不可刪除",
      "默认方案预览": "預設方案預覽",
      "默认目录": "預設目錄",
      "默认皮肤方案 default 禁止删除": "預設造型方案 default 禁止刪除",
      "默认替换此人物的全部原皮肤。自定义素材优先，未修改的部分沿用所选基础。": "預設替換此人物的全部原造型。自定義素材優先，未修改的部分沿用所選基礎。",
      "目标角色": "目標角色",
      "目录里是其它 version.dll": "目錄裡是其它 version.dll",
      "目录内检测到游戏主程序（AstralParty*.exe）": "目錄內檢測到遊戲主程式（AstralParty*.exe）",
      "目录内未找到 AstralParty 游戏程序（仍可安装，但请确认目录正确）": "目錄內未找到 AstralParty 遊戲程式（仍可安裝，但請確認目錄正確）",
      "难度": "難度",
      "内置": "內建",
      "内置 SHA-256": "內建 SHA-256",
      "内置加载器": "內建載入器",
      "内置文件就绪：version.dll": "內建檔案就緒：version.dll",
      "昵称，可留空": "暱稱，可留空",
      "您的昵称": "您的暱稱",
      "排序方式": "排序方式",
      "配置 ·": "配置 ·",
      "配置保存在": "配置儲存在",
      "配置文件夹": "配置資料夾",
      "配置写入后需按重载热键或重启游戏生效。": "配置寫入後需按過載熱鍵或重啟遊戲生效。",
      "皮肤": "造型",
      "皮肤包操作": "造型包操作",
      "皮肤包素材库": "造型包素材庫",
      "皮肤短描述": "造型短描述",
      "皮肤方案管理与切换": "造型方案管理與切換",
      "皮肤故事": "造型故事",
      "皮肤类型": "造型型別",
      "偏移": "偏移",
      "品质": "品質",
      "普通": "普通",
      "启动 Astral Party（吉星派对）": "啟動 Astral Party（吉星派對）",
      "启动 Astral Party（吉星派对）· 从 Steam 启动": "啟動 Astral Party（吉星派對）· 從 Steam 啟動",
      "启动 Astral Party（吉星派对）· 绕过 Steam：直接启动游戏主程序": "啟動 Astral Party（吉星派對）· 繞過 Steam：直接啟動遊戲主程式",
      "启动版本": "啟動版本",
      "启动方式": "啟動方式",
      "启动加速 startup_state": "啟動加速 startup_state",
      "启动加速倍率": "啟動加速倍率",
      "启动加速时长（秒）": "啟動加速時長（秒）",
      "启动时基础倍速（1.0 = 正常；2.0 = 全程 2 倍速；也可留 1.0 由游戏内热键变速。不支持减速，最小 1.0）": "啟動時基礎倍速（1.0 = 正常；2.0 = 全程 2 倍速；也可留 1.0 由遊戲內熱鍵變速。不支援減速，最小 1.0）",
      "启动时基础倍速（1.0 = 正常；不支持减速，最小 1.0）": "啟動時基礎倍速（1.0 = 正常；不支援減速，最小 1.0）",
      "启动时基础倍速必须在 1.0 ~ 100 之间（1.0 = 正常速度，不支持减速）": "啟動時基礎倍速必須在 1.0 ~ 100 之間（1.0 = 正常速度，不支援減速）",
      "启动游戏": "啟動遊戲",
      "启用 / 禁用（重启游戏后生效）": "啟用 / 禁用（重啟遊戲後生效）",
      "启用 / 禁用与配置修改都在": "啟用 / 禁用與配置修改都在",
      "启用此皮肤": "啟用此造型",
      "启用后，启动游戏即按该倍速运行（全程保持）": "啟用後，啟動遊戲即按該倍速執行（全程保持）",
      "启用后无需按快捷键，游戏加载完成就持续使用下方倍速": "啟用後無需按快捷鍵，遊戲載入完成就持續使用下方倍速",
      "启用加载器": "啟用載入器",
      "启用启动加速": "啟用啟動加速",
      "强制卸载（version.dll 不属于本工具时也删除）": "強制解除安裝（version.dll 不屬於本工具時也刪除）",
      "强制卸载（version.dll 与内置不一致时也删除）": "強制解除安裝（version.dll 與內建不一致時也刪除）",
      "清单 JSON 无效：": "清單 JSON 無效：",
      "清空搜索": "清空搜尋",
      "请等待当前操作完成。": "請等待當前操作完成。",
      "请稍候": "請稍候",
      "请输入方案名称。": "請輸入方案名稱。",
      "请输入方案目录名": "請輸入方案目錄名",
      "请输入方案显示名称": "請輸入方案顯示名稱",
      "请输入有效的数字编号": "請輸入有效的數字編號",
      "请先创建或选择一个有效方案。": "請先建立或選擇一個有效方案。",
      "请先等待或取消当前转换": "請先等待或取消當前轉換",
      "请先选择外部皮肤方案": "請先選擇外部造型方案",
      "请选择发布包或粘贴完整路径。": "請選擇釋出包或貼上完整路徑。",
      "请选择官方皮肤基础或自定义皮肤包。": "請選擇官方造型基礎或自定義造型包。",
      "请选择一个要深度扫描的盘符。": "請選擇一個要深度掃描的磁碟機代號。",
      "请在左侧点击任意协议帧进行查看解码数据": "請在左側點選任意協議幀進行檢視解碼資料",
      "请展开模组状态查看详情。": "請展開模組狀態檢視詳情。",
      "区服": "區服",
      "取消": "取消",
      "取消排队": "取消排隊",
      "取消下载": "取消下載",
      "取消转换": "取消轉換",
      "去模组页面（推荐）": "去模組頁面（推薦）",
      "去重玩家动作统计": "去重玩家動作統計",
      "全部来源": "全部來源",
      "全部类别": "全部類別",
      "全部原皮肤": "全部原造型",
      "全队转账": "全隊轉賬",
      "缺少内置文件": "缺少內建檔案",
      "缺失": "缺失",
      "确定（已选": "確定（已選",
      "确定删除模组「": "確定刪除模組「",
      "确定要彻底删除方案": "確定要徹底刪除方案",
      "确定要将": "確定要將",
      "确认并导入": "確認並匯入",
      "确认还原": "確認還原",
      "确认继续": "確認繼續",
      "确认删除": "確認刪除",
      "确认删除皮肤方案": "確認刪除造型方案",
      "确认卸载": "確認解除安裝",
      "绕过 Steam 启动": "繞過 Steam 啟動",
      "热更新资源缓存": "熱更新資源快取",
      "人物": "人物",
      "人物皮肤": "人物造型",
      "人物皮肤包": "人物造型包",
      "人物皮肤编辑分类": "人物造型編輯分類",
      "人物设置": "人物設定",
      "人物缩放": "人物縮放",
      "任务加入后台队列后可继续新建；右下角查看进度或终止。": "任務加入後臺佇列後可繼續新建；右下角檢視進度或終止。",
      "仍然导入": "仍然匯入",
      "仍要放回（": "仍要放回（",
      "日本語": "日本語",
      "如 二次元萌化包": "如 二次元萌化包",
      "扫描系统": "掃描系統",
      "筛选筹码、玩家、品质、来源...": "篩選籌碼、玩家、品質、來源...",
      "筛选商店类型、玩家、卡牌、购买结果...": "篩選商店型別、玩家、卡牌、購買結果...",
      "删除": "刪除",
      "删除此方案": "刪除此方案",
      "删除方案": "刪除方案",
      "删除该方案": "刪除該方案",
      "删除回放": "刪除重播",
      "删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。": "刪除會優先放進 Windows 回收站，仍可還原。只刪遊戲內副本是騰席位的常用做法；庫內副本才是長期存檔。",
      "删除角色皮肤？": "刪除角色造型？",
      "删除皮肤包": "刪除造型包",
      "删除整个方案？": "刪除整個方案？",
      "伤害": "傷害",
      "商店": "商店",
      "商店记录": "商店記錄",
      "商店进店共": "商店進店共",
      "上次游戏未能应用方案：": "上次遊戲未能應用方案：",
      "上面没有你的游戏？深度扫描某个盘（较慢，可取消）：": "上面沒有你的遊戲？深度掃描某個盤（較慢，可取消）：",
      "上一帧": "上一幀",
      "尚未安装；游戏正在运行，本次安装重启游戏后才加载": "尚未安裝；遊戲正在執行，本次安裝重啟遊戲後才載入",
      "尚未安装到游戏": "尚未安裝到遊戲",
      "尚未安装加载器": "尚未安裝載入器",
      "尚未安装加载器 —— 点「安装加载器」，加载器与内置模组会一起装进游戏目录。": "尚未安裝載入器 —— 點「安裝載入器」，載入器與內建模組會一起裝進遊戲目錄。",
      "尚未安装加载器 / 目录为空": "尚未安裝載入器 / 目錄為空",
      "尚未安装角色皮肤模组": "尚未安裝角色造型模組",
      "尚未安装角色皮肤模组，请从左侧管理菜单安装。": "尚未安裝角色造型模組，請從左側管理選單安裝。",
      "尚未打开回放文件": "尚未開啟重播檔案",
      "尚未定位游戏目录": "尚未定位遊戲目錄",
      "尚未启用": "尚未啟用",
      "尚未选择游戏目录": "尚未選擇遊戲目錄",
      "尚未选择游戏目录（可点「自动检测」）": "尚未選擇遊戲目錄（可點「自動檢測」）",
      "稍后再说": "稍後再說",
      "设为当前": "設為當前",
      "设为当前皮肤": "設為當前造型",
      "设置": "設定",
      "深度扫描": "深度掃描",
      "深度扫描没找到 AstralParty*.exe。": "深度掃描沒找到 AstralParty*.exe。",
      "生命": "生命",
      "生效；「": "生效；「",
      "时长": "時長",
      "时长(秒)": "時長(秒)",
      "实时调整（最低 1 倍，不支持减速）。": "實時調整（最低 1 倍，不支援減速）。",
      "使用方式": "使用方式",
      "使用托管引导（实验性，一般保持关闭）": "使用託管引導（實驗性，一般保持關閉）",
      "使用须知": "使用須知",
      "使用原版": "使用原版",
      "事件卡": "事件卡",
      "事件卡 (": "事件卡 (",
      "事件时间线": "事件時間線",
      "视频 / GIF 按选定区域裁剪为普通卡面的 404 × 400，所有帧使用同一选区。首次使用可能下载 FFmpeg。": "影片 / GIF 按選定區域裁剪為普通卡面的 404 × 400，所有幀使用同一選區。首次使用可能下載 FFmpeg。",
      "适用于此人物全部原皮肤": "適用於此人物全部原造型",
      "适用原皮肤": "適用原造型",
      "手动打开任意回放文件，本地解析玩家、回合、动作、筹码流转与底层协议帧。": "手動開啟任意重播檔案，本地解析玩家、回合、動作、籌碼流轉與底層協議幀。",
      "手牌卡": "手牌卡",
      "手牌卡 (": "手牌卡 (",
      "售价": "售價",
      "输出伤害": "輸出傷害",
      "输出预览": "輸出預覽",
      "输入方案名称": "輸入方案名稱",
      "输入角色名或皮肤名": "輸入角色名或造型名",
      "数据统计": "資料統計",
      "数字": "數字",
      "刷新": "重新整理",
      "刷新方案与状态": "重新整理方案與狀態",
      "刷新记录": "重新整理記錄",
      "刷新列表": "重新整理列表",
      "双击在资源管理器中打开": "雙擊在資源管理器中開啟",
      "死亡计数": "死亡計數",
      "搜索方案": "搜尋方案",
      "搜索回放": "搜尋重播",
      "搜索回放 ID / 地图 / 玩家…": "搜尋重播 ID / 地圖 / 玩家…",
      "搜索角色 / 皮肤包": "搜尋角色 / 造型包",
      "搜索卡名、卡号 #101 或标识…": "搜尋卡名、卡號 #101 或標識…",
      "搜索帧号、CMD、消息名...": "搜尋幀號、CMD、訊息名...",
      "随本程序内置": "隨本程式內建",
      "随加载器一起安装的内置模组": "隨載入器一起安裝的內建模組",
      "所有数据均在本地安全离线处理": "所有資料均在本地安全離線處理",
      "它是怎么工作的？": "它是怎麼工作的？",
      "台词": "臺詞",
      "提交": "提交",
      "替换": "替換",
      "替换哪些原皮肤": "替換哪些原造型",
      "替换选中帧": "替換選中幀",
      "添加": "新增",
      "添加到多人物方案": "新增到多人物方案",
      "添加到方案": "新增到方案",
      "添加第一个人物": "新增第一個人物",
      "添加或修改人物后，完全退出并重启游戏以应用搭配。": "新增或修改人物後，完全退出並重啟遊戲以應用搭配。",
      "添加人物皮肤": "新增人物造型",
      "填写普通卡牌编号，或异画自己的编号。普通卡牌绑定只替换普通版；异画需要单独指定。": "填寫普通卡牌編號，或異畫自己的編號。普通卡牌繫結只替換普通版；異畫需要單獨指定。",
      "条": "條",
      "条)": "條)",
      "条筹码流转": "條籌碼流轉",
      "条事件记录": "條事件記錄",
      "跳转到帧": "跳轉到幀",
      "停用并移入皮肤目录中的 .deleted 文件夹。": "停用並移入造型目錄中的 .deleted 資料夾。",
      "停用此方案": "停用此方案",
      "同步配置：": "同步配置：",
      "同步配置到游戏": "同步配置到遊戲",
      "同时也是工具箱的": "同時也是工具箱的",
      "图片": "圖片",
      "图片预览加载失败": "圖片預覽載入失敗",
      "图片资源": "圖片資源",
      "退格": "退格",
      "托管引导程序集": "託管載入程式集",
      "拖动或用方向键移动裁剪区域": "拖動或用方向鍵移動裁剪區域",
      "拖动选区移动位置，拖动四角调整大小。": "拖動選區移動位置，拖動四角調整大小。",
      "拖动选区移动位置，用滑块调整大小。透明背景会保留。": "拖動選區移動位置，用滑塊調整大小。透明背景會保留。",
      "完整立绘": "完整立繪",
      "完整卸载加载器": "完整解除安裝載入器",
      "玩家": "玩家",
      "玩家表现": "玩家表現",
      "玩家数据对照": "玩家資料對照",
      "玩家转账贡献": "玩家轉賬貢獻",
      "维基攻略": "維基攻略",
      "未安装": "未安裝",
      "未安装 · 游戏运行中": "未安裝 · 遊戲執行中",
      "未保存修改": "未儲存修改",
      "未记录": "未記錄",
      "未记录骰面点数": "未記錄骰麵點數",
      "未检测": "未檢測",
      "未检测到游戏回放目录": "未檢測到遊戲重播目錄",
      "未检测到游戏回放目录。你仍然可以用左侧按钮直接打开任意回放文件。": "未檢測到遊戲重播目錄。你仍然可以用左側按鈕直接開啟任意重播檔案。",
      "未检查": "未檢查",
      "未命名方案": "未命名方案",
      "未启用": "未啟用",
      "未设置（用 mod 默认）": "未設定（用 mod 預設）",
      "未设置按键": "未設定按鍵",
      "未识别": "未識別",
      "未识别的皮肤": "未識別的造型",
      "未提供，使用原版": "未提供，使用原版",
      "未添加游戏 —— 去设置页添加": "未新增遊戲 —— 去設定頁新增",
      "未选择方案": "未選擇方案",
      "未找到匹配的卡牌": "未找到匹配的卡牌",
      "未找到原资源尺寸，使用受限的预设尺寸": "未找到原資源尺寸，使用受限的預設尺寸",
      "未知": "未知",
      "未知版本": "未知版本",
      "未知筹码": "未知籌碼",
      "文案": "文案",
      "文档\\AstralPartyReplays": "文件\\AstralPartyReplays",
      "文件": "檔案",
      "文件不属于本工具": "檔案不屬於本工具",
      "文件不一致": "檔案不一致",
      "文件名称:": "檔名稱:",
      "文件已选择，确认上方安装说明后点击「确认并导入」。": "檔案已選擇，確認上方安裝說明後點選「確認並匯入」。",
      "文件有误": "檔案有誤",
      "我的方案": "我的方案",
      "无": "無",
      "无 —— 不是从 git 仓库构建的": "無 —— 不是從 git 倉庫構建的",
      "无法加载图片，请取消后重新选择。": "無法載入圖片，請取消後重新選擇。",
      "无法解析": "無法解析",
      "无符合条件的事件记录": "無符合條件的事件記錄",
      "稀有": "稀有",
      "系统": "系統",
      "系统设置": "系統設定",
      "下每个模组的 sidecar（同名": "下每個模組的 sidecar（同名",
      "下面这些模组，包里的版本比游戏目录里已装的更旧：": "下面這些模組，包裡的版本比遊戲目錄裡已裝的更舊：",
      "下一帧": "下一幀",
      "下载默认包": "下載預設包",
      "下载默认皮肤包": "下載預設造型包",
      "下载失败。": "下載失敗。",
      "下载完成": "下載完成",
      "下载完成。": "下載完成。",
      "下载中…": "下載中…",
      "先安装加载器": "先安裝載入器",
      "先安装角色皮肤模组，方案才能在游戏中生效。": "先安裝角色造型模組，方案才能在遊戲中生效。",
      "先给搭配起个名字，再添加喜欢的人物皮肤。": "先給搭配起個名字，再新增喜歡的人物造型。",
      "显示": "顯示",
      "显示控制台窗口（模组日志）": "顯示控制檯視窗（模組日誌）",
      "显示名称": "顯示名稱",
      "相机工具 / 活动日志 / 模组管理 / 权限声明 / 游戏内热键变速…": "相機工具 / 活動日誌 / 模組管理 / 許可權宣告 / 遊戲內熱鍵變速…",
      "详情": "詳情",
      "想要新功能": "想要新功能",
      "项目主页": "專案主頁",
      "消息类型": "訊息型別",
      "校验并保存清单": "校驗並儲存清單",
      "协议命令统计": "協議命令統計",
      "协议载荷解码详情": "協議載荷解碼詳情",
      "协议帧共": "協議幀共",
      "写入": "寫入",
      "卸载：": "解除安裝：",
      "卸载变速器": "解除安裝變速器",
      "卸载加载器": "解除安裝載入器",
      "卸载将删除加载器及 AstralParty_ModLoader 目录，包括模组、配置和日志。此操作无法通过工具撤销。": "解除安裝將刪除載入器及 AstralParty_ModLoader 目錄，包括模組、配置和日誌。此操作無法透過工具撤銷。",
      "卸载中…": "解除安裝中…",
      "新版本": "新版本",
      "新建第一个方案": "新建第一個方案",
      "新建方案": "新建方案",
      "新建皮肤方案": "新建造型方案",
      "新建一个方案，再添加人物皮肤。之后可以一键切换整套搭配。": "新建一個方案，再新增人物造型。之後可以一鍵切換整套搭配。",
      "新建一个角色皮肤包，或导入 ZIP 开始制作。": "新建一個角色造型包，或匯入 ZIP 開始製作。",
      "新建自定义皮肤": "新建自定義造型",
      "新建自定义皮肤包": "新建自定義造型包",
      "新写入的 version.dll 要重启游戏才会加载，且运行中的游戏可能占用该文件导致覆盖失败。": "新寫入的 version.dll 要重啟遊戲才會載入，且執行中的遊戲可能佔用該檔案導致覆蓋失敗。",
      "新增击杀": "新增擊殺",
      "新增伤害": "新增傷害",
      "新增转账": "新增轉賬",
      "星币": "星幣",
      "星币 · 击杀": "星幣 · 擊殺",
      "修复点“创建 / 加入房间”没反应（大厅匹配改为直接返回空结果）": "修復點“建立 / 加入房間”沒反應（大廳匹配改為直接返回空結果）",
      "修复退房 / 被踢时报错（房间列表查询返回空列表）": "修復退房 / 被踢時報錯（房間列表查詢返回空列表）",
      "修改后完全退出并重启游戏生效。图片、文案和地图动画已提供；媒体、特效及特殊演出保留原版，运行时效果待游戏验证。": "修改後完全退出並重啟遊戲生效。圖片、文案和地圖動畫已提供；媒體、特效及特殊演出保留原版，執行時效果待遊戲驗證。",
      "修改时间:": "修改時間:",
      "修改显示名称、作者和方案描述": "修改顯示名稱、作者和方案描述",
      "修饰键": "修飾鍵",
      "需要修复": "需要修復",
      "选区保持普通卡面的 404 : 400 比例，所有视频帧使用同一裁剪范围。": "選區保持普通卡面的 404 : 400 比例，所有影片幀使用同一裁剪範圍。",
      "选区边长（原图像素）": "選區邊長（原影象素）",
      "选区大小": "選區大小",
      "选区宽度（原图像素）": "選區寬度（原影象素）",
      "选区始终为正方形。": "選區始終為正方形。",
      "选取你的卡面": "選取你的卡面",
      "选择": "選擇",
      "选择…": "選擇…",
      "选择按键组合 ·": "選擇按鍵組合 ·",
      "选择当前正在操作的游戏": "選擇當前正在操作的遊戲",
      "选择动画帧": "選擇動畫幀",
      "选择发布包或粘贴完整路径": "選擇釋出包或貼上完整路徑",
      "选择回放文件": "選擇重播檔案",
      "选择皮肤方案：": "選擇造型方案：",
      "选择视频 / GIF": "選擇影片 / GIF",
      "选择文件…": "選擇檔案…",
      "选择文件后，点击「确认并导入」才会安装。SDK 工具包或单独 SDK DLL 仅更新 SDK。": "選擇檔案後，點選「確認並匯入」才會安裝。SDK 工具包或單獨 SDK DLL 僅更新 SDK。",
      "选择要启动的游戏版本": "選擇要啟動的遊戲版本",
      "选择要深度扫描的盘符": "選擇要深度掃描的磁碟機代號",
      "选择一个皮肤包，或新建方案开始制作。": "選擇一個造型包，或新建方案開始製作。",
      "选择已有皮肤包、制作自定义皮肤，或搭配官方皮肤。": "選擇已有造型包、製作自定義造型，或搭配官方造型。",
      "循环": "迴圈",
      "沿用原皮肤": "沿用原造型",
      "要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；": "要換成 CesiumLoader，勾選下面的「允許覆蓋遊戲目錄裡不屬於本工具的 version.dll」，再點「安裝載入器」；",
      "也包括变速本身": "也包括變速本身",
      "也可以导入新的皮肤包。": "也可以匯入新的造型包。",
      "依赖：": "依賴：",
      "移除": "移除",
      "移除此人物规则？": "移除此人物規則？",
      "移动点数": "移動點數",
      "移动骰子统计": "移動骰子統計",
      "已安装": "已安裝",
      "已安装 · 游戏运行中": "已安裝 · 遊戲執行中",
      "已安装 v": "已安裝 v",
      "已安装，但这一份没有版本清单，读不出准确版本 —— 重新安装一次即可纳入版本管理。": "已安裝，但這一份沒有版本清單，讀不出準確版本 —— 重新安裝一次即可納入版本管理。",
      "已安装（内置版本号读取失败）": "已安裝（內建版本號讀取失敗）",
      "已安装的模组": "已安裝的模組",
      "已持有": "已持有",
      "已存在，继续将覆盖它。": "已存在，繼續將覆蓋它。",
      "已禁用": "已禁用",
      "已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近": "已開啟自動託管：以後每次開啟本工具都會自動把超出席位的舊重播歸檔進庫（遊戲內保留最近",
      "已启用": "已啟用",
      "已启用 · 重启游戏后生效": "已啟用 · 重啟遊戲後生效",
      "已切换语言": "已切換語言",
      "已设为": "已設為",
      "已设为当前方案": "已設為當前方案",
      "已是最新版本 v": "已是最新版本 v",
      "已售出": "已售出",
      "已添加": "已新增",
      "已停用": "已停用",
      "已停用或缺失，恢复时使用此版本": "已停用或缺失，恢復時使用此版本",
      "已同步": "已同步",
      "已完成": "已完成",
      "已下载": "已下載",
      "已应用": "已應用",
      "已有皮肤包": "已有造型包",
      "已终止": "已終止",
      "已装的 v": "已裝的 v",
      "已自定义": "已自定義",
      "以后自动保持最近 10 局": "以後自動保持最近 10 局",
      "异画道具": "異畫道具",
      "异画道具 (": "異畫道具 (",
      "引导方法": "引導方法",
      "引导类型": "引導型別",
      "引导与超时（一般不用改）": "引導與超時（一般不用改）",
      "应用版本": "應用版本",
      "应用变速": "應用變速",
      "应用此方案": "應用此方案",
      "应用失败": "應用失敗",
      "用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。": "用輸出、支援和行動資料對照對局過程；不同角色分工不同，不以單一總傷害評定玩家。",
      "用右侧「浏览添加」手动选，或「扫描系统」自动找。": "用右側「瀏覽新增」手動選，或「掃描系統」自動找。",
      "游戏变速": "遊戲變速",
      "游戏大厅": "遊戲大廳",
      "游戏工具": "遊戲工具",
      "游戏加速（变速器）": "遊戲加速（變速器）",
      "游戏进程未运行，安装 / 卸载会立刻生效": "遊戲程序未執行，安裝 / 解除安裝會立刻生效",
      "游戏默认方案仅使用游戏内部卡面，无需外部图片。": "遊戲預設方案僅使用遊戲內部卡面，無需外部圖片。",
      "游戏默认方案没有可编辑的方案信息": "遊戲預設方案沒有可編輯的方案資訊",
      "游戏目录 · 覆盖与降级 · SDK 依赖 · 卸载": "遊戲目錄 · 覆蓋與降級 · SDK 依賴 · 解除安裝",
      "游戏目录 speedhack_config.json 存在": "遊戲目錄 speedhack_config.json 存在",
      "游戏目录 version.dll 与内置不一致——可能来自其它工具": "遊戲目錄 version.dll 與內建不一致——可能來自其它工具",
      "游戏目录 version.dll 与内置一致": "遊戲目錄 version.dll 與內建一致",
      "游戏目录（多个位置）": "遊戲目錄（多個位置）",
      "游戏目录（version.dll 的安装位置）": "遊戲目錄（version.dll 的安裝位置）",
      "游戏目录里的 version.dll 不是本工具安装的（可能是旧版独立变速器或第三方文件）——安装或更新前请先确认来源。": "遊戲目錄裡的 version.dll 不是本工具安裝的（可能是舊版獨立變速器或第三方檔案）——安裝或更新前請先確認來源。",
      "游戏目录里的 version.dll 不是本工具装的": "遊戲目錄裡的 version.dll 不是本工具裝的",
      "游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：": "遊戲目錄裡的 version.dll 既不是本程式內建的那一份，也沒有記錄在安裝清單裡：",
      "游戏目录里暂无 version.dll，可直接安装": "遊戲目錄裡暫無 version.dll，可直接安裝",
      "游戏目录暂无 speedhack_config.json": "遊戲目錄暫無 speedhack_config.json",
      "游戏内": "遊戲內",
      "游戏内保留局数": "遊戲內保留局數",
      "游戏内回放席位快满了（": "遊戲內重播席位快滿了（",
      "游戏内回放席位已满": "遊戲內重播席位已滿",
      "游戏内回放席位已满（": "遊戲內重播席位已滿（",
      "游戏内席位": "遊戲內席位",
      "游戏启动后自动以指定倍速加速一段时长（跳过开场动画）": "遊戲啟動後自動以指定倍速加速一段時長（跳過開場動畫）",
      "游戏启动中，稍候即可再切换启动方式": "遊戲啟動中，稍候即可再切換啟動方式",
      "游戏维基（Starengine Wiki）": "遊戲維基（Starengine Wiki）",
      "游戏运行中也能安装 / 卸载，但": "遊戲執行中也能安裝 / 解除安裝，但",
      "游戏在「本地回放」列表里遇到这种目录会把它整个删掉": "遊戲在「本地重播」列表裡遇到這種目錄會把它整個刪掉",
      "游戏正在运行：安装 / 更新 / 卸载都要重启游戏才生效。": "遊戲正在執行：安裝 / 更新 / 解除安裝都要重啟遊戲才生效。",
      "游戏正在运行：归档/放回随时可用，但别在游戏里正播着回放时操作": "遊戲正在執行：歸檔/放回隨時可用，但別在遊戲里正播著重播時操作",
      "游戏中的角色皮肤模组为": "遊戲中的角色造型模組為",
      "游戏自带的「本地回放」只有": "遊戲自帶的「本地重播」只有",
      "有新版本 v": "有新版本 v",
      "右Alt": "右Alt",
      "右Ctrl": "右Ctrl",
      "右Shift": "右Shift",
      "与「已禁用」状态仍会保留， 被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。": "與「已禁用」狀態仍會保留， 被替換掉的舊檔案會移入回收站（回收站不可用時保留原檔案）。如果只是想升級，請改用更新的模組包。",
      "与随程序内置的版本一致": "與隨程式內建的版本一致",
      "原理：把内置": "原理：把內建",
      "原始协议帧": "原始協議幀",
      "原图": "原圖",
      "源码提交": "原始碼提交",
      "远野汉娜": "遠野漢娜",
      "允许安装较旧版本（降级）": "允許安裝較舊版本（降級）",
      "允许把已装的加载器降级成内置的较旧版本": "允許把已裝的載入器降級成內建的較舊版本",
      "允许覆盖不属于本工具的 version.dll": "允許覆蓋不屬於本工具的 version.dll",
      "允许覆盖游戏目录里不属于本工具的 version.dll": "允許覆蓋遊戲目錄裡不屬於本工具的 version.dll",
      "允许覆盖游戏目录里其它 version.dll（用于强行更新安装）": "允許覆蓋遊戲目錄裡其它 version.dll（用於強行更新安裝）",
      "允许游戏内热键变速（Delete 在 1.0x 与刚才的倍率之间切换，Alt+= / Alt+- 调倍率）": "允許遊戲內熱鍵變速（Delete 在 1.0x 與剛才的倍率之間切換，Alt+= / Alt+- 調倍率）",
      "运行环境": "執行環境",
      "运行时": "執行時",
      "再次导入": "再次匯入",
      "再在游戏里点「保存」会提示已达上限。归档最旧的几局就能立刻腾出席位——回放不会丢，它们会进上面的回放库。": "再在遊戲裡點「儲存」會提示已達上限。歸檔最舊的幾局就能立刻騰出席位——重播不會丟，它們會進上面的重播庫。",
      "在 AstralParty.Toys 程序窗口里打开这一页才能查看更新日志。": "在 AstralParty.Toys 程式視窗裡開啟這一頁才能檢視更新日誌。",
      "在 AstralParty.Toys 程序窗口里打开这一页就能看到版本信息。": "在 AstralParty.Toys 程式視窗裡開啟這一頁就能看到版本資訊。",
      "在系统默认浏览器中打开": "在系統預設瀏覽器中開啟",
      "在资源管理器中打开": "在資源管理器中開啟",
      "载荷大小:": "載荷大小:",
      "载荷字节": "載荷位元組",
      "暂停": "暫停",
      "暂无方案描述": "暫無方案描述",
      "暂无描述说明": "暫無描述說明",
      "暂无统计数据": "暫無統計資料",
      "窄幅角色图": "窄幅角色圖",
      "战斗半身图 1": "戰鬥半身圖 1",
      "战斗半身图 2": "戰鬥半身圖 2",
      "战绩里那一长串数字": "戰績裡那一長串數字",
      "张卡面": "張卡面",
      "账号背景": "賬號背景",
      "找到": "找到",
      "这份安装没有版本清单，重新安装一次即可记录": "這份安裝沒有版本清單，重新安裝一次即可記錄",
      "这个键不支持，请换一个": "這個鍵不支援，請換一個",
      "这个目录已经不在了": "這個目錄已經不在了",
      "这个鼠标键不支持，请换一个": "這個滑鼠鍵不支援，請換一個",
      "这是本地构建：版本号来自项目文件，提交号与文件时间说明它对应哪一次源码。": "這是本地構建：版本號來自專案檔案，提交號與檔案時間說明它對應哪一次原始碼。",
      "这些回放解析不出结算帧": "這些重播解析不出結算幀",
      "帧": "幀",
      "帧 ·": "幀 ·",
      "帧\" title=\"": "幀\" title=\"",
      "帧/秒 ·": "幀/秒 ·",
      "帧号:": "幀號:",
      "帧预览": "幀預覽",
      "正在保存…": "正在儲存…",
      "正在处理": "正在處理",
      "正在处理，请稍候": "正在處理，請稍候",
      "正在从 GitHub 拉取最新加载器更新日志…": "正在從 GitHub 拉取最新載入器更新日誌…",
      "正在读取…": "正在讀取…",
      "正在读取版本信息…": "正在讀取版本資訊…",
      "正在读取更新日志…": "正在讀取更新日誌…",
      "正在读取加载器更新日志…": "正在讀取載入器更新日誌…",
      "正在检测…": "正在檢測…",
      "正在检测安装状态…": "正在檢測安裝狀態…",
      "正在检测回放目录…": "正在檢測重播目錄…",
      "正在解码 Protobuf 载荷…": "正在解碼 Protobuf 載荷…",
      "正在解析回放…": "正在解析重播…",
      "正在清理…": "正在清理…",
      "正在取消下载…": "正在取消下載…",
      "正在统计事件…": "正在統計事件…",
      "正在下载…": "正在下載…",
      "正在校验并安装发布包…": "正在校驗並安裝釋出包…",
      "正在终止": "正在終止",
      "正在转换…": "正在轉換…",
      "正在准备资源": "正在準備資源",
      "支持 MP4、WebM、MOV、AVI、MKV 和 GIF，最多 20 秒。先裁剪为 404 × 400，再在普通卡框中循环播放。": "支援 MP4、WebM、MOV、AVI、MKV 和 GIF，最多 20 秒。先裁剪為 404 × 400，再在普通卡框中迴圈播放。",
      "支持简体中文、English、日本語、繁體中文": "支援簡體中文、English、日本語、繁體中文",
      "支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。": "支援原精靈名對映 SpriteMappings、自定義資源地址 asset:。媒體檔案可隨包儲存，目前不應用。",
      "知道回放 ID（游戏战绩里那一串数字）时，直接从官方回放服务器下载进回放库": "知道重播 ID（遊戲戰績裡那一串數字）時，直接從官方重播伺服器下載進重播庫",
      "知道了，这一版不再提示": "知道了，這一版不再提示",
      "直接启动游戏主程序，完全不经过 Steam；会打开加载器的 Steam 绕过（没装 Steam 也能进游戏），代价是 Steam 大厅 / 好友邀请进房失效": "直接啟動遊戲主程式，完全不經過 Steam；會開啟載入器的 Steam 繞過（沒裝 Steam 也能進遊戲），代價是 Steam 大廳 / 好友邀請進房失效",
      "只能装一个": "只能裝一個",
      "只删库内副本（游戏里仍能看）": "只刪庫內副本（遊戲裡仍能看）",
      "只删游戏内副本（腾出席位）": "只刪遊戲內副本（騰出席位）",
      "只想清掉就勾选「强制卸载」。": "只想清掉就勾選「強制解除安裝」。",
      "只想要变速": "只想要變速",
      "只想用旧版变速器？忽略这段，照下面操作即可。": "只想用舊版變速器？忽略這段，照下面操作即可。",
      "只修改方案对外展示的信息，不会改变方案目录名": "只修改方案對外展示的資訊，不會改變方案目錄名",
      "只移除当前方案中的规则，自定义皮肤包会保留。": "只移除當前方案中的規則，自定義造型包會保留。",
      "指标": "指標",
      "治疗": "治療",
      "终止任务": "終止任務",
      "重启游戏": "重啟遊戲",
      "重启游戏后": "重啟遊戲後",
      "重试导入": "重試匯入",
      "重新安装内置版本": "重新安裝內建版本",
      "重新导入帧序列会替换整个动作；替换选中帧保留帧数、顺序和播放参数。": "重新匯入幀序列會替換整個動作；替換選中幀保留幀數、順序和播放引數。",
      "重新载入维基页面": "重新載入維基頁面",
      "重载配置热键": "過載配置熱鍵",
      "重载配置热键 reload_config_keys": "過載配置熱鍵 reload_config_keys",
      "重置筛选条件": "重置篩選條件",
      "逐回合数据表": "逐回合資料表",
      "主配置：": "主配置：",
      "注册表": "登錄檔",
      "注意：这个「变速器」和模组系统": "注意：這個「變速器」和模組系統",
      "专业信息": "專業資訊",
      "转出": "轉出",
      "转换并保存": "轉換並儲存",
      "转换动态卡面": "轉換動態卡面",
      "转账": "轉賬",
      "转账贡献": "轉賬貢獻",
      "转账星币": "轉賬星幣",
      "准备界面": "準備介面",
      "字节": "位元組",
      "字母": "字母",
      "自定义": "自定義",
      "自定义 + 官方基础": "自定義 + 官方基礎",
      "自定义皮肤": "自定義造型",
      "自定义皮肤包": "自定義造型包",
      "自动保持游戏内最近": "自動保持遊戲內最近",
      "自动保持游戏内最近 10 局": "自動保持遊戲內最近 10 局",
      "自动倍速": "自動倍速",
      "自动倍速倍率": "自動倍速倍率",
      "自动检测": "自動檢測",
      "自动托管已": "自動託管已",
      "自动托管已开启（保留": "自動託管已開啟（保留",
      "总承伤": "總承傷",
      "总伤害": "總傷害",
      "左Alt": "左Alt",
      "左Ctrl": "左Ctrl",
      "左Shift": "左Shift",
      "作者": "作者",
      "作者:": "作者:",
      "作者：": "作者：",
      "作者: —": "作者: —",
      "作者签名（可选）：": "作者簽名（可選）：",
      "AstralParty.Toys · 吉星派对工具箱": "AstralParty.Toys · 吉星派對工具箱",
      "CardSkinMod · 方案管理 / 导入 / 单卡可视化替换": "CardSkinMod · 方案管理 / 匯入 / 單卡視覺化替換",
      "CesiumLoader · 加载器 / 模组 / 变速": "CesiumLoader · 載入器 / 模組 / 變速",
      "com.unity.addressables 目录": "com.unity.addressables 目錄",
      "DLL 缺失": "DLL 缺失",
      "DLL 缺失，无法确定恢复版本": "DLL 缺失，無法確定恢復版本",
      "GitHub 最新": "GitHub 最新",
      "GitHub 最新 Release": "GitHub 最新 Release",
      "HUD 角色图": "HUD 角色圖",
      "Mod 加载器": "Mod 載入器",
      "Mod 加载器与模组管理": "Mod 載入器與模組管理",
      "PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。": "PNG/JPG 幀按檔名排序。待機 Idle、正向 Walk、背向 Walk-back；其餘動作名以原角色為準。",
      "PVE商店": "PVE商店",
      "SDK 版本（只读，随包更新）": "SDK 版本（只讀，隨包更新）",
      "sdk 目录为空（重新安装加载器会写入 CesiumLoader.SDK.dll）": "sdk 目錄為空（重新安裝載入器會寫入 CesiumLoader.SDK.dll）",
      "SDK 依赖（sdk\\ 目录）": "SDK 依賴（sdk\\ 目錄）",
      "SFW 半身图 1": "SFW 半身圖 1",
      "SFW 半身图 2": "SFW 半身圖 2",
      "SFW 立绘": "SFW 立繪",
      "SFW 缩略图": "SFW 縮圖",
      "SFW 窄幅图": "SFW 窄幅圖",
      "SFW 准备图": "SFW 準備圖",
      "speedhack_config.json 配置": "speedhack_config.json 配置",
      "Steam 绕过细节 —— 要从 Steam 启动并使用真大厅，这三个开关必须全部关闭": "Steam 繞過細節 —— 要從 Steam 啟動並使用真大廳，這三個開關必須全部關閉",
      "Task 构造方式（auto / direct / invoke，一般保持 auto）": "Task 構造方式（auto / direct / invoke，一般保持 auto）",
      "Unity 结构": "Unity 結構",
      "version.dll 已被游戏占用，删除可能失败；重启游戏后才恢复正常速度。": "version.dll 已被遊戲佔用，刪除可能失敗；重啟遊戲後才恢復正常速度。",
      "version.dll 已停用或缺失，保留版本": "version.dll 已停用或缺失，保留版本",
      "x（重启游戏生效）": "x（重啟遊戲生效）",
      "。修改配置后，重启游戏并刷新可查看新结果。": "。修改設定後，重新啟動遊戲並重新整理即可查看新結果。",
      "个筹码：": "個籌碼：",
      "决胜": "決勝",
      "可操作游戏": "可操作遊戲",
      "处理进度": "處理進度",
      "拉取最新": "拉取最新",
      "拉取最新」重试。": "拉取最新」重試。",
      "曲线": "曲線",
      "最近一次 Mod 记录：": "最近一次 Mod 紀錄：",
      "目录已失效": "目錄已失效",
      "预览": "預覽",
      "(未选择)": "(未選擇)",
      "- 动作类型: 【最终选定筹码】": "- 動作型別: 【最終選定籌碼】",
      "- 动作类型: 【首次呈现候选】": "- 動作型別: 【首次呈現候選】",
      "- 选定筹码品质分布:": "- 選定籌碼品質分佈:",
      "- 选定筹码来源分布:": "- 選定籌碼來源分佈:",
      "1. 【流派与契合度评估】请结合每位玩家的角色技能与定位，分析他们选取的筹码是否形成了有效联动（Synergy）？有没有错失更优质的候选筹码？": "1. 【流派與契合度評估】請結合每位玩家的角色技能與定位，分析他們選取的籌碼是否形成了有效聯動（Synergy）？有沒有錯失更優質的候選籌碼？",
      "2. 【刷新时机与策略分析】请评价各位玩家在各回合中的刷新决策：是否存在盲目刷新浪费机会，或是在关键轮次赌出了核心关键筹码？": "2. 【重新整理時機與策略分析】請評價各位玩家在各回合中的重新整理決策：是否存在盲目重新整理浪費機會，或是在關鍵輪次賭出了核心關鍵籌碼？",
      "4. 【改进建议】如果给表现落后或伤害不足的玩家提供改进策略，在筹码获取和资源配置上应该做出哪些针对性调整？": "4. 【改進建議】如果給表現落後或傷害不足的玩家提供改進策略，在籌碼獲取和資源配置上應該做出哪些針對性調整？",
      "AstralParty_ModLoader\\doorstop_config.json 里的 speedhackBaseSpeed 即可变速。": "AstralParty_ModLoader\\doorstop_config.json 裡的 speedhackBaseSpeed 即可變速。",
      "CesiumLoader 还没有发布过任何 Release（打 modloader-* tag 触发 GitHub Action 即可）。": "CesiumLoader 還沒有釋出過任何 Release（打 modloader-* tag 觸發 GitHub Action 即可）。",
      "FPS 必须大于零": "FPS 必須大於零",
      "GIF 已保存：": "GIF 已儲存：",
      "SDK 不是有效的托管程序集。": "SDK 不是有效的託管程式集。",
      "SDK 不是有效的程序集。": "SDK 不是有效的程式集。",
      "SDK 压缩包中的文件为空或超过 16 MB。": "SDK 壓縮包中的檔案為空或超過 16 MB。",
      "SDK 导入文件不能超过 64 MB。": "SDK 匯入檔案不能超過 64 MB。",
      "SDK 文件为空或超过 16 MB。": "SDK 檔案為空或超過 16 MB。",
      "SDK 的 PDB 符号与 DLL 不属于同一次构建。": "SDK 的 PDB 符號與 DLL 不屬於同一次構建。",
      "SDK 符号文件过大。": "SDK 符號檔案過大。",
      "SDK 解压后的文件超过 16 MB。": "SDK 解壓後的檔案超過 16 MB。",
      "Steam 绕过主开关: 无 Steam 启动游戏(拦截 SteamManager.Awake)": "Steam 繞過主開關: 無 Steam 啟動遊戲(攔截 SteamManager.Awake)",
      "Texture2D 尺寸超出 Unity 上限（最长边 16384）。": "Texture2D 尺寸超出 Unity 上限（最長邊 16384）。",
      "UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：": "UnityFS 大小宣告無效，或超過 2 GiB 無法讀入記憶體：",
      "UnityFS 头不完整：": "UnityFS 頭不完整：",
      "UnityFS 存在无法解释的尾部数据，拒绝修复：": "UnityFS 存在無法解釋的尾部資料，拒絕修復：",
      "UnityFS 版本头无效：": "UnityFS 版本頭無效：",
      "ZIP 不允许符号链接。": "ZIP 不允許符號連結。",
      "ZIP 中没有 Unity .bundle 卡牌资源。": "ZIP 中沒有 Unity .bundle 卡牌資源。",
      "ZIP 中没有 Unity .bundle 资源。": "ZIP 中沒有 Unity .bundle 資源。",
      "ZIP 包含未引用的文件": "ZIP 包含未引用的檔案",
      "ZIP 存在重复路径": "ZIP 存在重複路徑",
      "ZIP 存在重复路径：": "ZIP 存在重複路徑：",
      "ZIP 必须包含且仅包含一份 character-skin.json": "ZIP 必須包含且僅包含一份 character-skin.json",
      "ZIP 文件与目录路径冲突：": "ZIP 檔案與目錄路徑衝突：",
      "ZIP 文件长度不完整：": "ZIP 檔案長度不完整：",
      "ZIP 解压大小超过声明。": "ZIP 解壓大小超過宣告。",
      "ZIP 路径无效：": "ZIP 路徑無效：",
      "[★ 最终首领击杀者/MVP]": "[★ 最終首領擊殺者/MVP]",
      "mod 包不能超过 64 MB。": "mod 包不能超過 64 MB。",
      "mod 包内文件数量超过限制。": "mod 包內檔案數量超過限制。",
      "mod 包必须是 .zip 文件。": "mod 包必須是 .zip 檔案。",
      "mod 包解压后超过大小限制。": "mod 包解壓後超過大小限制。",
      "mod 必须是 .dll 文件（模组包请用 .zip）。": "mod 必須是 .dll 檔案（模組包請用 .zip）。",
      "skin.json 根节点不是对象": "skin.json 根節點不是物件",
      "skin.json 格式无效，请先修复后再编辑方案信息": "skin.json 格式無效，請先修復後再編輯方案資訊",
      "version.dll 已停用或缺失，模组与配置已保留；点击恢复，缺少本地备份时会下载当前版本的加载器包。": "version.dll 已停用或缺失，模組與配置已保留；點選恢復，缺少本地備份時會下載當前版本的載入器包。",
      "version.dll 已缺失；其他文件未改动，可点击恢复。": "version.dll 已缺失；其他檔案未改動，可點選恢復。",
      "version.dll 来源不明，不能作为 CesiumLoader 停用。请先确认文件来源。": "version.dll 來源不明，不能作為 CesiumLoader 停用。請先確認檔案來源。",
      "version.dll 缺失且版本未知，请重新安装或更新完整加载器。": "version.dll 缺失且版本未知，請重新安裝或更新完整載入器。",
      "{ // 配置说明见对应 mod 文档; 公开字段(public field)才会被读取 }": "{ // 配置說明見對應 mod 文件; 公開欄位(public field)才會被讀取 }",
      "· 卡面转换导入": "· 卡面轉換匯入",
      "· 转换导入": "· 轉換匯入",
      "。可将此皮肤包添加到人物方案。": "。可將此造型包新增到人物方案。",
      "。确认要降级后再导入（玩家配置与停用状态仍会保留）。": "。確認要降級後再匯入（玩家配置與停用狀態仍會保留）。",
      "一、对局背景基本信息": "一、對局背景基本資訊",
      "三、全局筹码统计摘要": "三、全域性籌碼統計摘要",
      "下载内容超过大小限制。": "下載內容超過大小限制。",
      "下载包版本与当前安装版本不一致。": "下載包版本與當前安裝版本不一致。",
      "下载期间安装状态发生变化，请刷新后重试。": "下載期間安裝狀態發生變化，請重新整理後重試。",
      "下载默认包失败：": "下載預設包失敗：",
      "不支持或重复的视频资源：": "不支援或重複的影片資源：",
      "不支持的 MonoBehaviour：": "不支援的 MonoBehaviour：",
      "不支持的皮肤方案版本": "不支援的造型方案版本",
      "不支持的角色皮肤格式版本": "不支援的角色造型格式版本",
      "不支持的角色资源类型：": "不支援的角色資源型別：",
      "不是 UnityFS bundle：": "不是 UnityFS bundle：",
      "个追加字节；原文件保存在 source/。": "個追加位元組；原檔案儲存在 source/。",
      "二、参战玩家与角色战绩概况": "二、參戰玩家與角色戰績概況",
      "五、供 AI 智能分析与复盘的建议 Prompt 方向": "五、供 AI 智慧分析與覆盤的建議 Prompt 方向",
      "人物已变更，请从方案重新添加皮肤包": "人物已變更，請從方案重新新增造型包",
      "人物皮肤包已创建并添加到方案": "人物造型包已建立並新增到方案",
      "人物规则不存在，请刷新方案": "人物規則不存在，請重新整理方案",
      "人物配置为空": "人物配置為空",
      "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。": "僅 version.dll 已刪除，模組、配置、SDK 和日誌已保留。",
      "仅使用游戏内部卡面，停用外部图片。重启游戏后生效。": "僅使用遊戲內部卡面，停用外部圖片。重啟遊戲後生效。",
      "仅支持静态 Texture2D 替换包；包内含动画、精灵或其他资源，不能部分导入。": "僅支援靜態 Texture2D 替換包；包內含動畫、精靈或其他資源，不能部分匯入。",
      "从 Toys 导出的皮肤方案": "從 Toys 匯出的造型方案",
      "从旧式 Unity bundle 卡牌替换包转换。原作者文件和原 bundle 保存在 source/。": "從舊式 Unity bundle 卡牌替換包轉換。原作者檔案和原 bundle 儲存在 source/。",
      "从旧式 Unity bundle 皮肤包转换。原作者说明和原文件保存在 source/。": "從舊式 Unity bundle 造型包轉換。原作者說明和原檔案儲存在 source/。",
      "任务已完成，皮肤列表刷新失败：": "任務已完成，造型列表重新整理失敗：",
      "作者最长 100 个字符": "作者最長 100 個字元",
      "你可以直接复制以下 Prompt 向大语言模型提问：": "你可以直接複製以下 Prompt 向大語言模型提問：",
      "保存卡面方案与原作者文件": "儲存卡面方案與原作者檔案",
      "保存旧包转换结果": "儲存舊包轉換結果",
      "倍速档位的速度必须大于 0。": "倍速檔位的速度必須大於 0。",
      "全局 / 开局阶段": "全域性 / 開局階段",
      "内置角色模组缺失": "內建角色模組缺失",
      "初始化本地界面": "初始化本地介面",
      "删除 version.dll 失败，请退出游戏并检查文件权限后重试。": "刪除 version.dll 失敗，請退出遊戲並檢查檔案許可權後重試。",
      "加载器与变速器共用 version.dll，不能同时安装：装变速器会覆盖加载器，所有 mod 将失效。": "載入器與變速器共用 version.dll，不能同時安裝：裝變速器會覆蓋載入器，所有 mod 將失效。",
      "加载器发布包": "載入器釋出包",
      "加载器已卸载（游戏恢复正常）。": "載入器已解除安裝（遊戲恢復正常）。",
      "加载器已卸载；当前正在运行的游戏进程仍加载着加载器，重启游戏后恢复正常。": "載入器已解除安裝；當前正在執行的遊戲程序仍載入著載入器，重啟遊戲後恢復正常。",
      "加载器已安装到游戏目录。启动游戏后会自动加载 mods\\ 目录下的所有 mod": "載入器已安裝到遊戲目錄。啟動遊戲後會自動載入 mods\\ 目錄下的所有 mod",
      "加载器已完整卸载，请重新安装。": "載入器已完整解除安裝，請重新安裝。",
      "加载器操作正在进行，请稍候。": "載入器操作正在進行，請稍候。",
      "加载器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载 mod。": "載入器檔案已寫入遊戲目錄。遊戲正在執行，本次不會立刻生效——重啟遊戲後才會載入 mod。",
      "加载器目录已缺失，请使用安装功能恢复完整组件。": "載入器目錄已缺失，請使用安裝功能恢復完整元件。",
      "动作名无效": "動作名無效",
      "动作名称无效或重复：": "動作名稱無效或重複：",
      "动作帧或帧率无效:": "動作幀或幀率無效:",
      "动作引用未知精灵：": "動作引用未知精靈：",
      "动作没有可转换的精灵帧：": "動作沒有可轉換的精靈幀：",
      "动态卡面导入失败：": "動態卡面匯入失敗：",
      "动态卡面尺寸或总像素超过限制。": "動態卡面尺寸或總畫素超過限制。",
      "动态卡面已裁剪为 404×400。选择该方案并重启游戏后生效，需要新版 CardSkinMod。": "動態卡面已裁剪為 404×400。選擇該方案並重啟遊戲後生效，需要新版 CardSkinMod。",
      "动态卡面最多 20 秒（300 帧），请先剪辑视频或 GIF 后重试。": "動態卡面最多 20 秒（300 幀），請先剪輯影片或 GIF 後重試。",
      "动态卡面裁剪区域无效，请重新选择。": "動態卡面裁剪區域無效，請重新選擇。",
      "动态卡面转换失败：": "動態卡面轉換失敗：",
      "动态覆盖已移除，重启游戏后使用原有静态卡面或游戏原图。": "動態覆蓋已移除，重啟遊戲後使用原有靜態卡面或遊戲原圖。",
      "动画或技能视频属于其他角色，不能混合导入。": "動畫或技能影片屬於其他角色，不能混合匯入。",
      "包目录与标识不一致": "包目錄與標識不一致",
      "协议": "協議",
      "协议程序集不可用，无法解析结算信息。": "協議程式集不可用，無法解析結算資訊。",
      "单个资源包超过 2 GiB，无法读入内存：": "單個資源包超過 2 GiB，無法讀入記憶體：",
      "卡牌": "卡牌",
      "卡牌文件名不能为空": "卡牌檔名不能為空",
      "卡面尺寸超出 Unity 上限（最长边 16384）：": "卡面尺寸超出 Unity 上限（最長邊 16384）：",
      "卡面必须是正方形": "卡面必須是正方形",
      "卡面绑定名称不正确。": "卡面繫結名稱不正確。",
      "即使删除成功，当前这局也已经加载了加载器，要重启游戏才会恢复正常。": "即使刪除成功，當前這局也已經載入了載入器，要重啟遊戲才會恢復正常。",
      "即使删除成功，当前这局也已经加载了变速器，要重启游戏才会恢复正常速度。": "即使刪除成功，當前這局也已經載入了變速器，要重啟遊戲才會恢復正常速度。",
      "压缩包内没有任何文件。": "壓縮包內沒有任何檔案。",
      "压缩包包含不安全或重复的路径。": "壓縮包包含不安全或重複的路徑。",
      "压缩包包含不安全的相对路径。": "壓縮包包含不安全的相對路徑。",
      "压缩包里没有找到模组：需要 <ModId>/<ModId>.dll 这样的结构，或者把 <ModId>.dll 直接放在包根目录。": "壓縮包裡沒有找到模組：需要 <ModId>/<ModId>.dll 這樣的結構，或者把 <ModId>.dll 直接放在包根目錄。",
      "原安装版本": "原安裝版本",
      "原精灵名称重复：": "原精靈名稱重複：",
      "参数缺失": "引數缺失",
      "发布包 DLL 哈希校验失败。": "釋出包 DLL 雜湊校驗失敗。",
      "发布包不能超过 64 MB。": "釋出包不能超過 64 MB。",
      "发布包包含不安全或重复的路径。": "釋出包包含不安全或重複的路徑。",
      "发布包包含不安全路径、重复文件或超过大小限制。": "釋出包包含不安全路徑、重複檔案或超過大小限制。",
      "发布包必须包含唯一的 CesiumLoader.SDK.dll（SDK 工具包或加载器发布包）。": "釋出包必須包含唯一的 CesiumLoader.SDK.dll（SDK 工具包或載入器釋出包）。",
      "发布包文件超过大小限制。": "釋出包檔案超過大小限制。",
      "发布包未提供 version.dll 的有效哈希。": "釋出包未提供 version.dll 的有效雜湊。",
      "发布包缺少 version.dll。": "釋出包缺少 version.dll。",
      "发布包缺少清单（cesium-loader.json），已停止安装。": "釋出包缺少清單（cesium-loader.json），已停止安裝。",
      "发布包缺少版本清单。": "釋出包缺少版本清單。",
      "发布包解压大小或文件数量超过限制。": "釋出包解壓大小或檔案數量超過限制。",
      "发布包里没有任何 mod（AstralParty_ModLoader/mods/ 为空），已停止安装。": "釋出包裡沒有任何 mod（AstralParty_ModLoader/mods/ 為空），已停止安裝。",
      "变速器已卸载（游戏恢复正常速度）。": "變速器已解除安裝（遊戲恢復正常速度）。",
      "变速器已卸载；当前正在运行的游戏进程仍加载着变速器，重启游戏后恢复正常速度。": "變速器已解除安裝；當前正在執行的遊戲程序仍載入著變速器，重啟遊戲後恢復正常速度。",
      "变速器已安装到游戏目录。进入游戏后按配置的快捷键即可变速（需关闭垂直同步）。": "變速器已安裝到遊戲目錄。進入遊戲後按配置的快捷鍵即可變速（需關閉垂直同步）。",
      "变速器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载变速器。": "變速器檔案已寫入遊戲目錄。遊戲正在執行，本次不會立刻生效——重啟遊戲後才會載入變速器。",
      "可在「模组 → 加载器设置」里打开它）": "可在「模組 → 載入器設定」裡開啟它）",
      "可在「模组」页一键安装加载器，或改选「从 Steam 启动」）": "可在「模組」頁一鍵安裝載入器，或改選「從 Steam 啟動」）",
      "可随时点击恢复 version.dll。": "可隨時點選恢復 version.dll。",
      "吉星派对": "吉星派對",
      "同名方案已存在": "同名方案已存在",
      "同名方案已存在，请修改包的 Id 或先删除原方案": "同名方案已存在，請修改包的 Id 或先刪除原方案",
      "同名自定义包已存在:": "同名自定義包已存在:",
      "向当前方案添加人物皮肤包": "向當前方案新增人物造型包",
      "四、按回合详细筹码流转与决策时间线 (Chronological Relic Timeline)": "四、按回合詳細籌碼流轉與決策時間線 (Chronological Relic Timeline)",
      "回放库目录与游戏回放目录不能是同一个路径，操作已拒绝。请先在设置里修改库目录。": "重播庫目錄與遊戲重播目錄不能是同一個路徑，操作已拒絕。請先在設定裡修改庫目錄。",
      "回放库目录与游戏回放目录被设置成了同一个路径，已停用所有归档操作。请在设置里改成别的目录。": "重播庫目錄與遊戲重播目錄被設定成了同一個路徑，已停用所有歸檔操作。請在設定裡改成別的目錄。",
      "图片不存在:": "圖片不存在:",
      "图片必须是 PNG/JPG，GIF 请先转换为帧序列": "圖片必須是 PNG/JPG，GIF 請先轉換為幀序列",
      "图片槽位无效": "圖片槽位無效",
      "基于应用内置默认包创建": "基於應用內建預設包建立",
      "基础倍速必须大于 0。": "基礎倍速必須大於 0。",
      "备份哈希无效。": "備份雜湊無效。",
      "复制后校验失败，目标文件已清理，源文件保持不动。": "複製後校驗失敗，目標檔案已清理，原始檔保持不動。",
      "多人物方案已保存。完全退出并重启游戏后生效。": "多人物方案已儲存。完全退出並重啟遊戲後生效。",
      "大厅匹配绕过: 修「点创建/加入房间没反应」": "大廳匹配繞過: 修「點建立/加入房間沒反應」",
      "如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。": "如果遊戲也讀不出來，別放回遊戲目錄——遊戲列到它會刪掉整個目錄。",
      "如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。": "如果遊戲也讀不出來，它列到這份重播時會把整個目錄刪掉。確認要放回請再確認一次。",
      "如确定要覆盖，请勾选「允许覆盖其它 version.dll」。": "如確定要覆蓋，請勾選「允許覆蓋其它 version.dll」。",
      "如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。": "如確定要降級，請勾選「允許降級安裝較舊的載入器」；通常更好的做法是改用較新版本的 AstralParty.Toys。",
      "媒体文件不存在:": "媒體檔案不存在:",
      "存在重复卡牌资源名：": "存在重複卡牌資源名：",
      "安装文件包含符号链接，无法可靠备份。": "安裝檔案包含符號連結，無法可靠備份。",
      "定位安装目录和热更新缓存资源": "定位安裝目錄和熱更新快取資源",
      "对局": "對局",
      "对应版本的在线发布包": "對應版本的線上釋出包",
      "导入多人物皮肤方案": "匯入多人物造型方案",
      "导入旧式卡牌皮肤包失败：": "匯入舊式卡牌造型包失敗：",
      "导入旧式卡牌皮肤包（自动转换 bundle）": "匯入舊式卡牌造型包（自動轉換 bundle）",
      "导入旧式角色皮肤包（自动转换 bundle）": "匯入舊式角色造型包（自動轉換 bundle）",
      "导入角色皮肤包": "匯入角色造型包",
      "导出商店时间线表格 (CSV)": "匯出商店時間線表格 (CSV)",
      "导出回放完整数据": "匯出重播完整資料",
      "导出失败：": "匯出失敗：",
      "导出当前动作为 GIF": "匯出當前動作為 GIF",
      "导出方案及自定义皮肤包": "匯出方案及自定義造型包",
      "导出来源皮肤无效": "匯出來源造型無效",
      "导出皮肤方案 ZIP": "匯出造型方案 ZIP",
      "导出确认已过期，请重新导出。": "匯出確認已過期，請重新匯出。",
      "导出筹码时间线 (自然语言 AI 文本)": "匯出籌碼時間線 (自然語言 AI 文字)",
      "导出筹码时间线数据 (JSON)": "匯出籌碼時間線資料 (JSON)",
      "导出筹码时间线表格 (CSV)": "匯出籌碼時間線表格 (CSV)",
      "导出角色皮肤包": "匯出角色造型包",
      "尚未安装：点「安装加载器」，把加载器和内置模组装进游戏目录。": "尚未安裝：點「安裝載入器」，把載入器和內建模組裝進遊戲目錄。",
      "尚未安装：点击「安装变速器」把文件复制到游戏目录。": "尚未安裝：點選「安裝變速器」把檔案複製到遊戲目錄。",
      "尚未找到游戏目录：可点击「选择游戏目录」手动指定安装位置。": "尚未找到遊戲目錄：可點選「選擇遊戲目錄」手動指定安裝位置。",
      "尚未找到游戏目录：点「自动检测」，或手动选择游戏 exe 所在的文件夹。": "尚未找到遊戲目錄：點「自動檢測」，或手動選擇遊戲 exe 所在的資料夾。",
      "尚未选择有效的游戏目录，请先自动检测或手动选择。": "尚未選擇有效的遊戲目錄，請先自動檢測或手動選擇。",
      "已交给 Steam 启动 Astral Party，稍等一下。": "已交給 Steam 啟動 Astral Party，稍等一下。",
      "已从应用内置默认包恢复 default，重启游戏后生效。": "已從應用內建預設包恢復 default，重啟遊戲後生效。",
      "已保存加载器设置（重启游戏生效）": "已儲存載入器設定（重啟遊戲生效）",
      "已切换到从 Steam 启动：加载器的 Steam 绕过已整套关闭，Steam 大厅联机恢复正常（重启游戏生效）": "已切換到從 Steam 啟動：載入器的 Steam 繞過已整套關閉，Steam 大廳聯機恢復正常（重啟遊戲生效）",
      "已切换到绕过 Steam：加载器的 Steam 绕过已打开（重启游戏生效）": "已切換到繞過 Steam：載入器的 Steam 繞過已開啟（重啟遊戲生效）",
      "已加入后台卡面转换队列，右下角可查看进度或终止。": "已加入後臺卡面轉換佇列，右下角可檢視進度或終止。",
      "已加入后台导出队列": "已加入後臺匯出佇列",
      "已加入后台导出队列，完成后自动添加到方案": "已加入後臺匯出佇列，完成後自動新增到方案",
      "已加入后台转换队列，右下角可查看进度或终止。": "已加入後臺轉換佇列，右下角可檢視進度或終止。",
      "已取消": "已取消",
      "已取消深度扫描。": "已取消深度掃描。",
      "已取消读取，原卡面保留。": "已取消讀取，原卡面保留。",
      "已取消转换，原卡面保留。": "已取消轉換，原卡面保留。",
      "已恢复默认模板（Ctrl 切换 2 倍速）。": "已恢復預設模板（Ctrl 切換 2 倍速）。",
      "已按请求的 ID 入库，需要的话可以改名后再放回游戏。": "已按請求的 ID 入庫，需要的話可以改名後再放回遊戲。",
      "已有卡面正在处理，请等待或取消。": "已有卡面正在處理，請等待或取消。",
      "已有卡面正在转换。": "已有卡面正在轉換。",
      "已有发布包正在导入，请稍候。": "已有釋出包正在匯入，請稍候。",
      "已禁用变速（恢复 1.0 正常速度，重启游戏生效）": "已禁用變速（恢復 1.0 正常速度，重啟遊戲生效）",
      "已终止任务，临时文件已清理": "已終止任務，臨時檔案已清理",
      "已经有一个回放在下载了，等它结束再试。": "已經有一個重播在下載了，等它結束再試。",
      "已绕过 Steam，直接启动游戏主程序。": "已繞過 Steam，直接啟動遊戲主程式。",
      "已记住这个选择，但没能同步到加载器配置（还没装加载器？在「模组」页安装一次就会同步）。": "已記住這個選擇，但沒能同步到載入器配置（還沒裝載入器？在「模組」頁安裝一次就會同步）。",
      "帧号,回合,玩家ID,玩家名称,玩家角色,商店类型,候选卡牌,售价,折扣,免费卡,购买结果,已售出槽位,是否关闭": "幀號,回合,玩家ID,玩家名稱,玩家角色,商店型別,候選卡牌,售價,折扣,免費卡,購買結果,已售出槽位,是否關閉",
      "帧号,回合,玩家ID,玩家名称,玩家角色,操作类型,档位,筹码ID,筹码名称,品质,来源,刷新状态,候选选项": "幀號,回合,玩家ID,玩家名稱,玩家角色,操作型別,檔位,籌碼ID,籌碼名稱,品質,來源,重新整理狀態,候選選項",
      "帧序列必须是 PNG/JPG": "幀序列必須是 PNG/JPG",
      "帧时长必须在 0～655350 ms 之间": "幀時長必須在 0～655350 ms 之間",
      "帧时长无效": "幀時長無效",
      "开始处理": "開始處理",
      "当前加载器版本未知或格式无效，无法下载对应发布包。请重新安装或更新加载器。": "當前載入器版本未知或格式無效，無法下載對應釋出包。請重新安裝或更新載入器。",
      "当前动作没有动画帧": "當前動作沒有動畫幀",
      "当前动作过大，请缩小帧尺寸或减少帧数后导出 GIF": "當前動作過大，請縮小幀尺寸或減少幀數後匯出 GIF",
      "当前游戏已切换，请重新导出。": "當前遊戲已切換，請重新匯出。",
      "当前游戏已切换，请重新添加人物皮肤包": "當前遊戲已切換，請重新新增人物造型包",
      "当前游戏已切换，请重新选择图片。": "當前遊戲已切換，請重新選擇圖片。",
      "当前游戏已切换，请重新选择素材": "當前遊戲已切換，請重新選擇素材",
      "当前游戏已切换，请重新选择视频。": "當前遊戲已切換，請重新選擇影片。",
      "当前游戏进程仍加载着加载器，下次启动才停用。": "當前遊戲程序仍載入著載入器，下次啟動才停用。",
      "恢复默认包失败：": "恢復預設包失敗：",
      "所选目录里没有找到游戏主程序（AstralParty*.exe），确认这是游戏 exe 所在的目录？": "所選目錄裡沒有找到遊戲主程式（AstralParty*.exe），確認這是遊戲 exe 所在的目錄？",
      "找不到 WebUI/index.html（内嵌资源里也没有）。": "找不到 WebUI/index.html（內嵌資源裡也沒有）。",
      "找不到要导入的 mod 包。": "找不到要匯入的 mod 包。",
      "找不到要导入的 mod 文件。": "找不到要匯入的 mod 檔案。",
      "技能视频不是可识别的 CRI USM：": "技能影片不是可識別的 CRI USM：",
      "挂接延迟不能为负数。": "掛接延遲不能為負數。",
      "按配置中的完整资源名匹配目标皮肤": "按配置中的完整資源名匹配目標造型",
      "提交完整卡牌方案": "提交完整卡牌方案",
      "提示：CesiumLoader 已内置变速引擎——请勿安装本变速器，直接改加载器的": "提示：CesiumLoader 已內建變速引擎——請勿安裝本變速器，直接改載入器的",
      "整个模组包（.zip）请用 ImportModPackage 导入，这里只接收单个 DLL。": "整個模組包（.zip）請用 ImportModPackage 匯入，這裡只接收單個 DLL。",
      "文案槽位或长度无效:": "文案槽位或長度無效:",
      "新方案标识不能为空": "新方案標識不能為空",
      "新方案标识包含非法字符": "新方案標識包含非法字元",
      "方案B 备用安全网 —— 实测开启会导致启动崩溃, 必须保持 false": "方案B 備用安全網 —— 實測開啟會導致啟動崩潰, 必須保持 false",
      "方案C 调用方式: auto / direct / invoke": "方案C 呼叫方式: auto / direct / invoke",
      "方案为空": "方案為空",
      "方案信息已保存。": "方案資訊已儲存。",
      "方案共享图片冲突:": "方案共享圖片衝突:",
      "方案共享文案冲突:": "方案共享文案衝突:",
      "方案名不能为空": "方案名不能為空",
      "方案名称不正确。": "方案名稱不正確。",
      "方案名称不能为空": "方案名稱不能為空",
      "方案名称最长 100 个字符": "方案名稱最長 100 個字元",
      "方案存在空字段": "方案存在空欄位",
      "方案已存在": "方案已存在",
      "方案描述最长 2000 个字符": "方案描述最長 2000 個字元",
      "方案操作失败：": "方案操作失敗：",
      "方案显示名称不能为空": "方案顯示名稱不能為空",
      "方案显示名称最长 100 个字符": "方案顯示名稱最長 100 個字元",
      "方案标识与保存目标不一致": "方案標識與儲存目標不一致",
      "方案标识仅支持字母、数字、下划线和短横线，最长 80 字符。": "方案標識僅支援字母、數字、下劃線和短橫線，最長 80 字元。",
      "方案目录与标识不一致": "方案目錄與標識不一致",
      "方案简介最长 2000 个字符": "方案簡介最長 2000 個字元",
      "方案缺少自定义包:": "方案缺少自定義包:",
      "无效的 PNG 帧。": "無效的 PNG 幀。",
      "无法保存加载器版本清单，本次安装未完整完成。": "無法儲存載入器版本清單，本次安裝未完整完成。",
      "无法启动 FFmpeg。": "無法啟動 FFmpeg。",
      "无法完整读取卡牌资源包": "無法完整讀取卡牌資源包",
      "无法完整读取旧资源包": "無法完整讀取舊資源包",
      "无法确定回放 ID。": "無法確定重播 ID。",
      "无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。": "無法獲取 FFmpeg。可將 ffmpeg.exe 放到程式旁 tools 目錄，或設定 ASTRAL_FFMPEG_PATH。",
      "无法设置首次安装的 Steam 启动方式，加载器配置不完整。": "無法設定首次安裝的 Steam 啟動方式，載入器配置不完整。",
      "无法读取 bundle 内的资源文件：": "無法讀取 bundle 內的資原始檔：",
      "无法读取视频首帧：": "無法讀取影片首幀：",
      "旧版启用皮肤": "舊版啟用造型",
      "最新 Release 尚未提供默认皮肤包，请先发布包含 cesium-default-skins.zip 的版本。": "最新 Release 尚未提供預設造型包，請先發布包含 cesium-default-skins.zip 的版本。",
      "未刷新": "未重新整理",
      "未刷新，直接从初始候选选择": "未重新整理，直接從初始候選選擇",
      "未知动画附属 MonoBehaviour，不能部分导入：": "未知動畫附屬 MonoBehaviour，不能部分匯入：",
      "未知图片槽位:": "未知圖片槽位:",
      "未知消息": "未知訊息",
      "未知玩家": "未知玩家",
      "未知的方案操作": "未知的方案操作",
      "未知视频实现：": "未知影片實現：",
      "未购买，关闭商店": "未購買，關閉商店",
      "本地备份": "本地備份",
      "本地备份校验失败，已停止操作。": "本地備份校驗失敗，已停止操作。",
      "来源未记录": "來源未記錄",
      "标识必须是 1～80 个字母、数字、下划线或短横线": "標識必須是 1～80 個字母、數字、下劃線或短橫線",
      "校验 PNG 和皮肤清单": "校驗 PNG 和造型清單",
      "校验并保存模板": "校驗並儲存模板",
      "检查旧式卡牌皮肤 ZIP": "檢查舊式卡牌造型 ZIP",
      "检查旧皮肤 ZIP 的路径与可用空间": "檢查舊造型 ZIP 的路徑與可用空間",
      "检测到已安装 CesiumLoader Mod 加载器（AstralParty_ModLoader\\doorstop_config.json 存在）。": "檢測到已安裝 CesiumLoader Mod 載入器（AstralParty_ModLoader\\doorstop_config.json 存在）。",
      "检测到游戏正在运行——安装或卸载前请先退出游戏。": "檢測到遊戲正在執行——安裝或解除安裝前請先退出遊戲。",
      "模组包比游戏目录里已装的更旧：": "模組包比遊戲目錄裡已裝的更舊：",
      "正在下载最新 CesiumLoader 并安装…": "正在下載最新 CesiumLoader 並安裝…",
      "正在准备协议和配置…": "正在準備協議和配置…",
      "正在导入模组，请稍候。": "正在匯入模組，請稍候。",
      "正在导出商店时间线…": "正在匯出商店時間線…",
      "正在导出完整协议数据…": "正在匯出完整協議資料…",
      "正在导出筹码时间线…": "正在匯出籌碼時間線…",
      "正在扫描 Steam 库、注册表与常见安装位置…": "正在掃描 Steam 庫、登錄檔與常見安裝位置…",
      "正在终止并清理临时文件…": "正在終止並清理臨時檔案…",
      "正在读取视频首帧，准备裁剪…": "正在讀取影片首幀，準備裁剪…",
      "正在转换动态卡面（15 帧/秒，最多 20 秒）…": "正在轉換動態卡面（15 幀/秒，最多 20 秒）…",
      "此人物的相同原皮肤已在方案中，请编辑已有的人物皮肤": "此人物的相同原造型已在方案中，請編輯已有的人物造型",
      "每帧时长数量与帧数不一致": "每幀時長數量與幀數不一致",
      "没有可删除的回放。": "沒有可刪除的重播。",
      "没能启动游戏：没有找到游戏主程序。请先在「变速器」页选择游戏目录，或确认游戏已安装。": "沒能啟動遊戲：沒有找到遊戲主程式。請先在「變速器」頁選擇遊戲目錄，或確認遊戲已安裝。",
      "清单为空": "清單為空",
      "游戏文件操作正在进行（安装 / 更新 / 导入），请等待完成后再修改。": "遊戲檔案操作正在進行（安裝 / 更新 / 匯入），請等待完成後再修改。",
      "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再安装。": "遊戲正在執行時 version.dll 會被佔用，無法覆蓋——請完全退出遊戲後再安裝。",
      "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再更新。": "遊戲正在執行時 version.dll 會被佔用，無法覆蓋——請完全退出遊戲後再更新。",
      "游戏正在运行时也能卸：version.dll 已被游戏加载占用，删除通常会失败并提示文件被占用；": "遊戲正在執行時也能卸：version.dll 已被遊戲載入佔用，刪除通常會失敗並提示檔案被佔用；",
      "游戏正在运行时也能同步：配置会写进游戏目录，已加载的变速器需要按重载热键（默认 Ctrl+Shift+R）重新读取，否则重启游戏后生效。": "遊戲正在執行時也能同步：配置會寫進遊戲目錄，已載入的變速器需要按重新載入熱鍵（預設 Ctrl+Shift+R）重新讀取，否則重啟遊戲後生效。",
      "游戏正在运行时也能装：version.dll 是游戏启动时加载的，本次写进去要重启游戏才会生效；": "遊戲正在執行時也能裝：version.dll 是遊戲啟動時載入的，本次寫進去要重啟遊戲才會生效；",
      "游戏正在运行时也能装：version.dll 由游戏启动时加载，本次写进去要重启游戏才会生效；": "遊戲正在執行時也能裝：version.dll 由遊戲啟動時載入，本次寫進去要重啟遊戲才會生效；",
      "游戏正在运行时该文件已被加载占用，请完全退出游戏后再卸载（本次没有改动任何文件）。": "遊戲正在執行時該檔案已被載入佔用，請完全退出遊戲後再解除安裝（本次沒有改動任何檔案）。",
      "游戏正在运行，重启游戏后生效。": "遊戲正在執行，重啟遊戲後生效。",
      "游戏正在运行：安装、更新、停用或恢复要重启游戏才生效，version.dll 被占用时请完全退出游戏再操作。": "遊戲正在執行：安裝、更新、停用或恢復要重啟遊戲才生效，version.dll 被佔用時請完全退出遊戲再操作。",
      "游戏正在运行：本次写入要重启游戏后才会加载。": "遊戲正在執行：本次寫入要重啟遊戲後才會載入。",
      "游戏目录不存在，请先安装加载器或选择游戏目录。": "遊戲目錄不存在，請先安裝載入器或選擇遊戲目錄。",
      "游戏目录中没有加载器文件，无需卸载。": "遊戲目錄中沒有載入器檔案，無需解除安裝。",
      "游戏目录中没有变速器文件，无需卸载。": "遊戲目錄中沒有變速器檔案，無需解除安裝。",
      "游戏目录存在其它 version.dll（与内置文件不同）——覆盖或卸载前请先确认来源。": "遊戲目錄存在其它 version.dll（與內建檔案不同）——覆蓋或解除安裝前請先確認來源。",
      "游戏目录尚未安装变速器，请先安装。": "遊戲目錄尚未安裝變速器，請先安裝。",
      "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的，如 CesiumLoader Mod 加载器）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。": "遊戲目錄已存在一個與內建不同的 version.dll（可能是其它工具的，如 CesiumLoader Mod 載入器）——如確定要覆蓋，請勾選「允許覆蓋其它 version.dll」。",
      "游戏目录已有 version.dll，未覆盖任何文件。请刷新状态并确认来源。": "遊戲目錄已有 version.dll，未覆蓋任何檔案。請重新整理狀態並確認來源。",
      "游戏目录或 mod 文件名无效。": "遊戲目錄或 mod 檔名無效。",
      "游戏目录里没有可归档的回放。": "遊戲目錄裡沒有可歸檔的重播。",
      "游戏目录里的 version.dll 不是本工具装的（来源不明）——可能是旧版独立变速器或第三方文件，安装或卸载前请先确认。": "遊戲目錄裡的 version.dll 不是本工具裝的（來源不明）——可能是舊版獨立變速器或第三方檔案，安裝或解除安裝前請先確認。",
      "游戏目录里的 version.dll 不是本工具装的（来源不明）——未删除任何文件。如确认要删除请勾选「强制卸载」。": "遊戲目錄裡的 version.dll 不是本工具裝的（來源不明）——未刪除任何檔案。如確認要刪除請勾選「強制解除安裝」。",
      "游戏目录里的 version.dll 不是本工具装的（来源不明，可能是旧版独立变速器或第三方文件）——": "遊戲目錄裡的 version.dll 不是本工具裝的（來源不明，可能是舊版獨立變速器或第三方檔案）——",
      "游戏配置没有可匹配的角色皮肤。": "遊戲配置沒有可匹配的角色造型。",
      "游戏默认": "遊戲預設",
      "游戏默认方案不可修改": "遊戲預設方案不可修改",
      "游戏默认方案无需恢复外部卡面。": "遊戲預設方案無需恢復外部卡面。",
      "游戏默认方案没有外部图片可导出。": "遊戲預設方案沒有外部圖片可匯出。",
      "源图片文件不存在": "源圖片檔案不存在",
      "物品": "物品",
      "皮肤不属于当前角色:": "造型不屬於當前角色:",
      "皮肤动画总帧数超过 10000": "造型動畫總幀數超過 10000",
      "皮肤包已存在": "造型包已存在",
      "皮肤压缩包不存在": "造型壓縮包不存在",
      "皮肤方案不存在:": "造型方案不存在:",
      "皮肤方案名称不能为空": "造型方案名稱不能為空",
      "皮肤方案已导出为 ZIP，可通过导入 ZIP 使用。": "造型方案已匯出為 ZIP，可透過匯入 ZIP 使用。",
      "皮肤方案目录不存在。": "造型方案目錄不存在。",
      "皮肤清单存在空字段": "造型清單存在空欄位",
      "相同角色和目标皮肤存在重复规则": "相同角色和目標造型存在重複規則",
      "确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。": "確認要換成 CDN 上這一份，請先刪掉庫裡的舊副本（或改用別的 ID）。",
      "移除动态卡面失败：": "移除動態卡面失敗：",
      "程序集名称不是 CesiumLoader.SDK。": "程式集名稱不是 CesiumLoader.SDK。",
      "程序集缺少内置加载器资源（ModLoader.version.dll），请重新编译发布版本。": "程式集缺少內建載入器資源（ModLoader.version.dll），請重新編譯釋出版本。",
      "程序集缺少内置加载器资源（version.dll），无法安装。": "程式集缺少內建載入器資源（version.dll），無法安裝。",
      "程序集缺少内置变速器资源（SpeedhackTools.version.dll / speedhack_config.json），请重新编译发布版本。": "程式集缺少內建變速器資源（SpeedhackTools.version.dll / speedhack_config.json），請重新編譯釋出版本。",
      "程序集缺少内置变速器资源（version.dll / speedhack_config.json），无法安装。": "程式集缺少內建變速器資源（version.dll / speedhack_config.json），無法安裝。",
      "程序集缺少内置配置模板资源，无法恢复默认。": "程式集缺少內建配置模板資源，無法恢復預設。",
      "空白皮肤包已创建": "空白造型包已建立",
      "等待处理": "等待處理",
      "精灵名称无效或重复：": "精靈名稱無效或重複：",
      "精灵帧没有完整动作信息。": "精靈幀沒有完整動作資訊。",
      "纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：": "紋理尺寸超出 Unity 上限（最長邊 16384）或解碼資料不完整：",
      "纹理或动画帧尺寸超出 Unity 上限（最长边 16384）。": "紋理或動畫幀尺寸超出 Unity 上限（最長邊 16384）。",
      "纹理资源名为空或重复：": "紋理資源名為空或重複：",
      "绑定规则必须指定自定义包": "繫結規則必須指定自定義包",
      "继续保存新回放会失败。播放不受影响，建议归档几局。": "繼續儲存新重播會失敗。播放不受影響，建議歸檔幾局。",
      "编码并保存 GIF": "編碼並儲存 GIF",
      "而且正在运行的游戏往往占用着 version.dll，覆盖会失败并提示文件被占用——那样就先完全退出游戏再装。": "而且正在執行的遊戲往往佔用著 version.dll，覆蓋會失敗並提示檔案被佔用——那樣就先完全退出遊戲再裝。",
      "自动检测未找到游戏目录，请点「选择…」手动指定。": "自動檢測未找到遊戲目錄，請點「選擇…」手動指定。",
      "自定义包与规则角色不匹配": "自定義包與規則角色不匹配",
      "自定义包已导入但绑定失败，回滚未完成；包可能被其他方案引用，已保留包。": "自定義包已匯入但繫結失敗，回滾未完成；包可能被其他方案引用，已保留包。",
      "自定义包被皮肤方案引用，请先移除方案规则:": "自定義包被造型方案引用，請先移除方案規則:",
      "自定义包角色与规则不匹配:": "自定義包角色與規則不匹配:",
      "裁剪区域必须是图片范围内的正方形": "裁剪區域必須是圖片範圍內的正方形",
      "裁剪卡面已保存，重启游戏后生效。": "裁剪卡面已儲存，重啟遊戲後生效。",
      "裁剪已取消或过期，请重新选择图片。": "裁剪已取消或過期，請重新選擇圖片。",
      "裁剪已取消或过期，请重新选择视频。": "裁剪已取消或過期，請重新選擇影片。",
      "裁剪已过期，请重新选择图片": "裁剪已過期，請重新選擇圖片",
      "裁剪范围或输出尺寸无效": "裁剪範圍或輸出尺寸無效",
      "规则为空": "規則為空",
      "规则标识重复:": "規則標識重複:",
      "规则至少需要一种资源来源": "規則至少需要一種資源來源",
      "规则角色或皮肤 ID 无效": "規則角色或造型 ID 無效",
      "视频资源引用结构无效：": "影片資源引用結構無效：",
      "视频转换产生不一致的帧尺寸。": "影片轉換產生不一致的幀尺寸。",
      "视频转换失败：": "影片轉換失敗：",
      "角色不存在:": "角色不存在:",
      "角色或原皮肤 ID 无效": "角色或原造型 ID 無效",
      "角色皮肤已保存。游戏内修改在完全退出并重启后生效。": "角色造型已儲存。遊戲內修改在完全退出並重啟後生效。",
      "角色皮肤操作失败：": "角色造型操作失敗：",
      "解析不出结算帧（cmd 1016），文件可能已损坏或来自不兼容的版本。": "解析不出結算幀（cmd 1016），檔案可能已損壞或來自不相容的版本。",
      "设置已保存。": "設定已儲存。",
      "该 version.dll 与内置文件不同，可能不是本工具安装的——未删除任何文件。如确认要删除请勾选「强制卸载」。": "該 version.dll 與內建檔案不同，可能不是本工具安裝的——未刪除任何檔案。如確認要刪除請勾選「強制解除安裝」。",
      "该帧已发生变化，请重新选择替换": "該幀已發生變化，請重新選擇替換",
      "该方案已经在导出队列中": "該方案已經在匯出佇列中",
      "该方案没有可导出的卡面。": "該方案沒有可匯出的卡面。",
      "请先在模组页安装 CesiumLoader。": "請先在模組頁安裝 CesiumLoader。",
      "请先安装加载器，再导入 SDK。": "請先安裝載入器，再匯入 SDK。",
      "请先等待或取消当前动态卡面转换。": "請先等待或取消當前動態卡面轉換。",
      "请先等待或取消当前动态卡面转换，再修改或导出皮肤方案。": "請先等待或取消當前動態卡面轉換，再修改或匯出造型方案。",
      "请先等待或取消当前转换。": "請先等待或取消當前轉換。",
      "请先选择外部皮肤方案。": "請先選擇外部造型方案。",
      "请先选择游戏目录。": "請先選擇遊戲目錄。",
      "请填写回放 ID。": "請填寫重播 ID。",
      "请填写存在的发布包完整路径。": "請填寫存在的釋出包完整路徑。",
      "请将适合本机的 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH 后重试。": "請將適合本機的 ffmpeg.exe 放到程式旁 tools 目錄，或設定 ASTRAL_FFMPEG_PATH 後重試。",
      "请检查文件权限后重试（本次没有改动任何文件）。": "請檢查檔案許可權後重試（本次沒有改動任何檔案）。",
      "请确认回放 ID 与区服是否对应。": "請確認重播 ID 與區服是否對應。",
      "请选择 1～2000 张图片": "請選擇 1～2000 張圖片",
      "请选择 PNG/JPG 图片": "請選擇 PNG/JPG 圖片",
      "请选择 SDK 发布 ZIP 或 CesiumLoader.SDK.dll。": "請選擇 SDK 釋出 ZIP 或 CesiumLoader.SDK.dll。",
      "请选择有效的游戏主程序目录。": "請選擇有效的遊戲主程式目錄。",
      "请选择视频或 GIF 文件。": "請選擇影片或 GIF 檔案。",
      "读取动画顺序与播放参数": "讀取動畫順序與播放引數",
      "读取游戏档案，首次使用时检测 Steam 游戏目录": "讀取遊戲檔案，首次使用時檢測 Steam 遊戲目錄",
      "读取角色与皮肤配置": "讀取角色與造型配置",
      "资源不是支持的卡牌贴图，拒绝部分导入：": "資源不是支援的卡牌貼圖，拒絕部分匯入：",
      "资源包没有可转换的 Texture2D：": "資源包沒有可轉換的 Texture2D：",
      "资源包没有可转换的图片、动作或已知技能视频。": "資源包沒有可轉換的圖片、動作或已知技能影片。",
      "资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：": "資源對應多個角色或造型，無法自動識別；請使用包含明確目標立繪的造型包。匹配結果：",
      "资源无法对应一个角色皮肤，或包中混有多个目标的资源。": "資源無法對應一個角色造型，或包中混有多個目標的資源。",
      "资源路径包含无效目录": "資源路徑包含無效目錄",
      "资源路径必须位于皮肤包内": "資源路徑必須位於造型包內",
      "资源路径越界": "資源路徑越界",
      "转换工具下载超过 256 MB。": "轉換工具下載超過 256 MB。",
      "转换工具体积不符合预期。": "轉換工具體積不符合預期。",
      "这个回放找不到了（可能已被移动或删除），点「刷新列表」重新扫描。": "這個重播找不到了（可能已被移動或刪除），點「重新整理列表」重新掃描。",
      "这个文件不是能解出结算帧的回放，已拒绝导入。": "這個檔案不是能解出結算幀的重播，已拒絕匯入。",
      "这个目录已经不在了。": "這個目錄已經不在了。",
      "这个目录已经不在了，或里面没有 AstralParty*.exe。": "這個目錄已經不在了，或裡面沒有 AstralParty*.exe。",
      "这个目录里没有检测到游戏主程序。请确认所选目录包含游戏 exe、UnityPlayer.dll 和 *_Data 文件夹。": "這個目錄裡沒有檢測到遊戲主程式。請確認所選目錄包含遊戲 exe、UnityPlayer.dll 和 *_Data 資料夾。",
      "这是 SDK 包，请用「导入发布包」更新 SDK。": "這是 SDK 包，請用「匯入釋出包」更新 SDK。",
      "这是 Toys 皮肤包，请使用标准皮肤导入。": "這是 Toys 造型包，請使用標準造型匯入。",
      "这是加载器发布包（包含 version.dll），请用「导入发布包」安装。": "這是載入器釋出包（包含 version.dll），請用「匯入釋出包」安裝。",
      "这是模组包（含 <ModId>/<ModId>.dll），请用「导入模组」安装；「导入发布包」只用于加载器与 SDK。": "這是模組包（含 <ModId>/<ModId>.dll），請用「匯入模組」安裝；「匯入釋出包」只用於載入器與 SDK。",
      "退房兜底: 修退房/被踢时 await 之后的 NRE": "退房兜底: 修退房/被踢時 await 之後的 NRE",
      "选中的动画帧不存在": "選中的動畫幀不存在",
      "选择 CesiumLoader 发布包": "選擇 CesiumLoader 釋出包",
      "选择动画帧（按文件名排序）": "選擇動畫幀（按檔名排序）",
      "选择吉星派对回放文件": "選擇吉星派對重播檔案",
      "选择吉星派对游戏目录（AstralParty exe 所在文件夹）": "選擇吉星派對遊戲目錄（AstralParty exe 所在資料夾）",
      "选择回放库目录（不要和游戏的 Temp\\Replay 选成同一个）": "選擇重播庫目錄（不要和遊戲的 Temp\\Replay 選成同一個）",
      "选择的角色皮肤与包内资源的准确配置匹配结果不符。": "選擇的角色造型與包內資源的準確配置匹配結果不符。",
      "选择皮肤压缩包 (支持 cesium-default-skins 或自定义皮肤 ZIP)": "選擇造型壓縮包 (支援 cesium-default-skins 或自定義造型 ZIP)",
      "选择目标必须同时指定角色与皮肤 ItemID。": "選擇目標必須同時指定角色與造型 ItemID。",
      "选择要导入回放库的回放文件": "選擇要匯入重播庫的重播檔案",
      "选择要导入的模组（DLL 或 ZIP 模组包）": "選擇要匯入的模組（DLL 或 ZIP 模組包）",
      "选择视频 / GIF，下一步裁剪普通卡面（404×400）": "選擇影片 / GIF，下一步裁剪普通卡面（404×400）",
      "选择角色图片": "選擇角色圖片",
      "部分精灵没有被动作引用，不能部分导入。": "部分精靈沒有被動作引用，不能部分匯入。",
      "配置内容为空，无法保存。": "配置內容為空，無法儲存。",
      "配置已保存到本地主配置，安装变速器时会一并复制到游戏目录。": "配置已儲存到本地主配置，安裝變速器時會一併複製到遊戲目錄。",
      "配置已同步到游戏目录，重启游戏后生效。": "配置已同步到遊戲目錄，重啟遊戲後生效。",
      "配置文件不是有效的 JSON，请先用「用编辑器打开」修复，或点「恢复默认」。": "配置檔案不是有效的 JSON，請先用「用編輯器開啟」修復，或點「恢復預設」。",
      "锚点、缩放或 PPU 超出允许范围": "錨點、縮放或 PPU 超出允許範圍",
      "阶段3(实测作废、hook 从未命中; 保留无害)": "階段3(實測作廢、hook 從未命中; 保留無害)",
      "附加保险: 让 SteamAPI_RestartAppIfNecessary 恒返回 0": "附加保險: 讓 SteamAPI_RestartAppIfNecessary 恆返回 0",
      "首次使用：正在下载 FFmpeg 转换工具…": "首次使用：正在下載 FFmpeg 轉換工具…",
      "默认包中没有卡面图片": "預設包中沒有卡面圖片",
      "默认包内容不符合预期": "預設包內容不符合預期",
      "默认包解压体积过大": "預設包解壓體積過大",
      "默认方案 (default) 禁止删除。": "預設方案 (default) 禁止刪除。",
      "默认皮肤包已下载并安装，可选择 default 方案使用。": "預設造型包已下載並安裝，可選擇 default 方案使用。",
      "默认皮肤包超过 256 MB": "預設造型包超過 256 MB",
      "（提示：Steam 没在运行，加载器的「Steam 绕过」是关的，游戏可能会在启动早期自己退出 ——": "（提示：Steam 沒在執行，載入器的「Steam 繞過」是關的，遊戲可能會在啟動早期自己退出 ——",
      "（提示：Steam 没在运行，而且还没安装加载器 —— 缺了它的 Steam 绕过，游戏会在启动早期自己退出。": "（提示：Steam 沒在執行，而且還沒安裝載入器 —— 缺了它的 Steam 繞過，遊戲會在啟動早期自己退出。",
      "（这个版本没有填写发布说明。）": "（這個版本沒有填寫釋出說明。）",
      "，已添加到方案": "，已新增到方案",
      "，并在控制台窗口显示日志。": "，並在控制檯視窗顯示日誌。",
      "：移除与目录块尾部完全相同的": "：移除與目錄塊尾部完全相同的",
      "；缺失：": "；缺失："
    }
  };
  window.I18N_TEMPLATES = {
    "en": [
      {
        "s": "：不用打开游戏，也不占游戏内的 10 个席位。",
        "t": ": no need to launch the game, and it doesn't use any of the game's 10 slots."
      },
      {
        "s": "：已装",
        "t": ": installed"
      },
      {
        "s": "。 确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。",
        "t": ". To go ahead and restore, click the button below; the library copy is kept either way."
      },
      {
        "s": "· 提交 {0}",
        "t": "· Commit {0}"
      },
      {
        "s": "（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；",
        "t": "(same folder as the mod's DLL, read and written by the mod's SdkConfig; takes effect after a game restart). Checkbox = toggle, number field = value, text field = text;"
      },
      {
        "s": ") 恢复为应用内置的原始卡面吗？",
        "t": ") back to the app's built-in original card art?"
      },
      {
        "s": "{0} · 编辑此人物的自定义素材{1}",
        "t": "{0} · Edit this character's custom assets{1}"
      },
      {
        "s": "{0} · 等级 {1}",
        "t": "{0} · Level {1}"
      },
      {
        "s": "{0} · 第{1}回合：{2}",
        "t": "{0} · Round {1}: {2}"
      },
      {
        "s": "{0} · 累计转账 {1} 星币",
        "t": "{0} · Total transferred {1} Star Coins"
      },
      {
        "s": "{0} ({1} 条)",
        "t": "{0} ({1} entries)"
      },
      {
        "s": "{0} / {1} 条",
        "t": "{0} / {1} entries"
      },
      {
        "s": "{0} 安装加载器",
        "t": "{0} Install Loader"
      },
      {
        "s": "{0} 次",
        "t": "{0} times"
      },
      {
        "s": "{0} 次掷骰 · {1} 颗骰子 · 平均 {2} 点 · 受控 {3} 次",
        "t": "{0} dice rolls · {1} dice · avg {2} pips · {3} controlled"
      },
      {
        "s": "{0} 从 GitHub 更新",
        "t": "{0} Update from GitHub"
      },
      {
        "s": "{0} 第 {1} 帧预览",
        "t": "{0} Frame {1} Preview"
      },
      {
        "s": "{0} 点",
        "t": "{0} pips"
      },
      {
        "s": "{0} 个进行中",
        "t": "{0} in progress"
      },
      {
        "s": "{0} 个人物 · {1}",
        "t": "{0} characters · {1}"
      },
      {
        "s": "{0} 个位置",
        "t": "{0} locations"
      },
      {
        "s": "{0} 工具箱",
        "t": "{0} Toolbox"
      },
      {
        "s": "{0} 加载器",
        "t": "{0} Loader"
      },
      {
        "s": "{0} 结构可疑",
        "t": "{0} Suspicious structure"
      },
      {
        "s": "{0} 局 · {1}",
        "t": "{0} matches · {1}"
      },
      {
        "s": "{0} 决胜",
        "t": "{0} Final Showdown"
      },
      {
        "s": "{0} 可操作游戏",
        "t": "{0} Can control the game"
      },
      {
        "s": "{0} 拉取最新",
        "t": "{0} Fetch Latest"
      },
      {
        "s": "{0} 目录已失效",
        "t": "{0} Folder no longer valid"
      },
      {
        "s": "{0} 星币",
        "t": "{0} Star Coins"
      },
      {
        "s": "{0} 张卡面",
        "t": "{0} card faces"
      },
      {
        "s": "{0} 帧",
        "t": "{0} frames"
      },
      {
        "s": "{0} 重新安装内置版本",
        "t": "{0} Reinstall Built-in Version"
      },
      {
        "s": "{0}{1}曲线",
        "t": "{0}{1} curve"
      },
      {
        "s": "{0}处理进度",
        "t": "{0} processing progress"
      },
      {
        "s": "{0}的皮肤",
        "t": "{0} skin"
      },
      {
        "s": "{0}预览",
        "t": "{0} preview"
      },
      {
        "s": "→ 包内",
        "t": "→ In package"
      },
      {
        "s": "＋ 按键",
        "t": "+ Key"
      },
      {
        "s": "= 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。",
        "t": "= click, then press the key to bind (any keyboard key or left/right/middle mouse button, side buttons; Esc to cancel)."
      },
      {
        "s": "安装：{0}",
        "t": "Install: {0}"
      },
      {
        "s": "按键按钮",
        "t": "Key Button"
      },
      {
        "s": "按资源比例裁剪，或保留完整图片",
        "t": "Crop to asset ratio, or keep the full image"
      },
      {
        "s": "保存布局与动作参数",
        "t": "Save Layout and Motion Settings"
      },
      {
        "s": "保存裁剪",
        "t": "Save Crop"
      },
      {
        "s": "保存尺寸",
        "t": "Save Size"
      },
      {
        "s": "保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。",
        "t": "Takes effect after saving and restarting the game; day to day you only need Enable Loader or Base Speed at Launch."
      },
      {
        "s": "保存配置",
        "t": "Save Config"
      },
      {
        "s": "保存设置",
        "t": "Save Settings"
      },
      {
        "s": "保存为透明 PNG，尺寸",
        "t": "Save as transparent PNG, size"
      },
      {
        "s": "保存文案",
        "t": "Save Text"
      },
      {
        "s": "保存信息",
        "t": "Save Info"
      },
      {
        "s": "倍速",
        "t": "Speed"
      },
      {
        "s": "本地构建{0}",
        "t": "Local build{0}"
      },
      {
        "s": "本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，",
        "t": "This tool can't read their settlement frame (its bundled protocol version may be older than the game's). If the game can't read them either,"
      },
      {
        "s": "本回合转账",
        "t": "Transferred This Round"
      },
      {
        "s": "本局地图",
        "t": "This Match's Map"
      },
      {
        "s": "比内置的 v{0} 更新（从 GitHub 更新过）",
        "t": "Newer than the bundled v{0} (updated from GitHub)"
      },
      {
        "s": "比内置的 v{0} 旧 —— 点「重新安装内置版本」可装回内置版",
        "t": "Older than the bundled v{0} — click Reinstall Built-in Version to go back to the bundled build"
      },
      {
        "s": "编辑人物皮肤",
        "t": "Edit Character Skin"
      },
      {
        "s": "播放预览",
        "t": "Play Preview"
      },
      {
        "s": "不裁剪，等比缩放并透明补边",
        "t": "No cropping; scale proportionally and pad with transparency"
      },
      {
        "s": "不添加自定义覆盖",
        "t": "Don't add custom overrides"
      },
      {
        "s": "裁剪结果预览",
        "t": "Crop Result Preview"
      },
      {
        "s": "裁剪为目标比例",
        "t": "Crop to Target Ratio"
      },
      {
        "s": "裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置",
        "t": "Crop selection: drag to move, drag a corner to resize, arrow keys to nudge"
      },
      {
        "s": "裁剪预览",
        "t": "Crop Preview"
      },
      {
        "s": "参战玩家 ({0} 名)",
        "t": "Players ({0})"
      },
      {
        "s": "查看详情",
        "t": "View Details"
      },
      {
        "s": "承受伤害",
        "t": "Damage Taken"
      },
      {
        "s": "筹码",
        "t": "Relics"
      },
      {
        "s": "筹码记录共 {0} 条",
        "t": "Relic records: {0} entries"
      },
      {
        "s": "出牌",
        "t": "Card Play"
      },
      {
        "s": "处理方式",
        "t": "Handling"
      },
      {
        "s": "创建自定义覆盖",
        "t": "Create Custom Override"
      },
      {
        "s": "此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信",
        "t": "This mod declares that it can drive the game (simulating card plays / dice rolls / movement). Permissions are no longer enforced; this is only a reminder: make sure the source is trustworthy"
      },
      {
        "s": "次",
        "t": " times"
      },
      {
        "s": "次 · 平均每回合",
        "t": " times · avg per round"
      },
      {
        "s": "从方案移除此人物",
        "t": "Remove This Character from Profile"
      },
      {
        "s": "从列表移除（不会删除游戏文件）",
        "t": "Remove from list (doesn't delete game files)"
      },
      {
        "s": "从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。",
        "t": "Create a brand-new profile from the app's built-in default pack without using images already modified on disk. No network needed; you can replace individual card faces afterwards."
      },
      {
        "s": "错误：{0}",
        "t": "Error: {0}"
      },
      {
        "s": "打开",
        "t": "Open"
      },
      {
        "s": "打开方案目录",
        "t": "Open Profile Folder"
      },
      {
        "s": "打开目录",
        "t": "Open Folder"
      },
      {
        "s": "打开目录修复",
        "t": "Open Folder to Fix"
      },
      {
        "s": "大小:",
        "t": "Size:"
      },
      {
        "s": "单次播放",
        "t": "Play Once"
      },
      {
        "s": "当前 {0}x（重启游戏生效）",
        "t": "Current {0}x (takes effect after a game restart)"
      },
      {
        "s": "当前星币",
        "t": "Current Star Coins"
      },
      {
        "s": "档位 {0}",
        "t": "Preset {0}"
      },
      {
        "s": "档位 {0}{1}",
        "t": "Preset {0}{1}"
      },
      {
        "s": "导出 AI 文本 (TXT)",
        "t": "Export AI Text (TXT)"
      },
      {
        "s": "导出 JSON",
        "t": "Export JSON"
      },
      {
        "s": "导出 ZIP",
        "t": "Export ZIP"
      },
      {
        "s": "导出表格 (CSV)",
        "t": "Export Table (CSV)"
      },
      {
        "s": "导出当前动作 GIF",
        "t": "Export Current Action GIF"
      },
      {
        "s": "导出方案 ZIP",
        "t": "Export Profile ZIP"
      },
      {
        "s": "导入方案 ZIP",
        "t": "Import Profile ZIP"
      },
      {
        "s": "导入人物包",
        "t": "Import Character Pack"
      },
      {
        "s": "导入视频 / GIF",
        "t": "Import Video / GIF"
      },
      {
        "s": "导入与管理",
        "t": "Import & Manage"
      },
      {
        "s": "导入帧序列",
        "t": "Import Frame Sequence"
      },
      {
        "s": "地图人物布局",
        "t": "Map Character Layout"
      },
      {
        "s": "第 {0} / {1} 帧 · {2} ms",
        "t": "Frame {0} / {1} · {2} ms"
      },
      {
        "s": "第 {0} 回合",
        "t": "Round {0}"
      },
      {
        "s": "第 {0} 回合 · {1}",
        "t": "Round {0} · {1}"
      },
      {
        "s": "第 {0} 帧",
        "t": "Frame {0}"
      },
      {
        "s": "点按切换（不勾选 = 按住才生效）",
        "t": "Click to toggle (unchecked = hold to apply)"
      },
      {
        "s": "点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。",
        "t": "Click to select or deselect; the choice applies to this preset once confirmed. A preset can use several keys (e.g. Ctrl + Shift)."
      },
      {
        "s": "点一下，再按下要绑的键（支持鼠标左/右/中键与侧键；Esc 取消）",
        "t": "Click, then press the key to bind (left/right/middle mouse buttons and side buttons supported; Esc to cancel)"
      },
      {
        "s": "动画帧预览",
        "t": "Animation Frame Preview"
      },
      {
        "s": "动画逐帧图片",
        "t": "Animation Frame Images"
      },
      {
        "s": "动态卡面 · {0} 帧/秒 · {1} 秒循环",
        "t": "Animated Card Art · {0} fps · {1} s loop"
      },
      {
        "s": "动作",
        "t": "Action"
      },
      {
        "s": "读不到加载器更新日志（内置为空，联网获取也没成功）。点「{0} 拉取最新」重试。",
        "t": "Can't read the loader changelog (nothing bundled, and fetching online failed). Click \"{0} Fetch Latest\" to retry."
      },
      {
        "s": "对局表现分析",
        "t": "Match Performance Analysis"
      },
      {
        "s": "对局结果",
        "t": "Match Result"
      },
      {
        "s": "多人物搭配 · {0} 个人物",
        "t": "Multi-character Lineup · {0} characters"
      },
      {
        "s": "二维精灵动画",
        "t": "2D Sprite Animation"
      },
      {
        "s": "发现新版本：已装 v{0} → 最新 v{1}，点「更新到 v{2}」。",
        "t": "New version found: installed v{0} → latest v{1}. Click Update to v{2}."
      },
      {
        "s": "方案：{0} · {1}",
        "t": "Profile: {0} · {1}"
      },
      {
        "s": "方案描述（可选）：",
        "t": "Profile description (optional):"
      },
      {
        "s": "方案设置",
        "t": "Profile Settings"
      },
      {
        "s": "方案显示名称：",
        "t": "Profile display name:"
      },
      {
        "s": "方案英文目录名（唯一标识）：",
        "t": "English folder name (unique ID):"
      },
      {
        "s": "方案中的人物",
        "t": "Characters in Profile"
      },
      {
        "s": "方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。",
        "t": "Use the arrow keys to nudge; hold Shift to move 10 pixels at a time. The original image is not modified."
      },
      {
        "s": "复制版本信息",
        "t": "Copy Version Info"
      },
      {
        "s": "该角色全部原皮肤",
        "t": "All original skins of this character"
      },
      {
        "s": "该人物全部原皮肤",
        "t": "All original skins of this character"
      },
      {
        "s": "该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。",
        "t": "This player's transfer increase within the round's snapshot window; multiple shop visits in the same round show the same value and aren't counted twice."
      },
      {
        "s": "高级：编辑完整清单",
        "t": "Advanced: Edit Full Manifest"
      },
      {
        "s": "高级：用系统默认编辑器直接改 JSON",
        "t": "Advanced: edit the JSON directly in your default editor"
      },
      {
        "s": "高级编辑…",
        "t": "Advanced Edit…"
      },
      {
        "s": "高级选项（Steam 绕过细节 · 引导 · 超时）",
        "t": "Advanced options (Steam bypass details · bootstrap · timeouts)"
      },
      {
        "s": "更换官方皮肤",
        "t": "Change Official Skin"
      },
      {
        "s": "更换图片",
        "t": "Replace Image"
      },
      {
        "s": "更新到 v{0}",
        "t": "Update to v{0}"
      },
      {
        "s": "攻击 / 防御",
        "t": "Attack / Defense"
      },
      {
        "s": "共 {0} 张卡面",
        "t": "{0} card faces total"
      },
      {
        "s": "共筛选出 {0} 条事件记录",
        "t": "Filtered to {0} event records"
      },
      {
        "s": "关闭",
        "t": "Close"
      },
      {
        "s": "关卡进度",
        "t": "Stage Progress"
      },
      {
        "s": "官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。",
        "t": "Replaces images and character animations from official sources. Voice, video, effects, and special cutscenes stay original."
      },
      {
        "s": "归档最旧的 {0} 局",
        "t": "Archive Oldest {0} Matches"
      },
      {
        "s": "还没有配置文件（mods\\{0}\\config.json）。保存后将创建。",
        "t": "No config file yet (mods\\{0}\\config.json). It's created when you save."
      },
      {
        "s": "合计",
        "t": "Total"
      },
      {
        "s": "恢复",
        "t": "Restore"
      },
      {
        "s": "恢复此动作",
        "t": "Restore This Action"
      },
      {
        "s": "恢复内置卡面",
        "t": "Restore Built-in Card Art"
      },
      {
        "s": "回放 ID",
        "t": "Replay ID"
      },
      {
        "s": "回放服务器",
        "t": "Replay Server"
      },
      {
        "s": "回放服务器（不确定就按当前游戏版本选）",
        "t": "Replay server (if unsure, match the current game version)"
      },
      {
        "s": "回放复盘",
        "t": "Replay Review"
      },
      {
        "s": "回放库",
        "t": "Replay Library"
      },
      {
        "s": "回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。 填上回放 ID 就能直接下载进",
        "t": "Replays are recorded by the server and stored on the official replay server; Save Replay in game pulls from there too. Enter a replay ID to download it straight into"
      },
      {
        "s": "回合",
        "t": "Round"
      },
      {
        "s": "或其中的卡面图片。",
        "t": "or the card art inside it."
      },
      {
        "s": "击杀",
        "t": "Kills"
      },
      {
        "s": "及其目录下的所有卡面图片吗？此操作不可撤销。",
        "t": "and all card art images in its folder? This can't be undone."
      },
      {
        "s": "技能使用",
        "t": "Skill Usage"
      },
      {
        "s": "继续导入会用包里的旧版本覆盖现有文件。玩家改过的",
        "t": "Continuing the import will overwrite existing files with the older versions in the pack. Player-modified"
      },
      {
        "s": "加载器 {0}",
        "t": "Loader {0}"
      },
      {
        "s": "检查更新失败：{0}",
        "t": "Update check failed: {0}"
      },
      {
        "s": "检查失败：{0}",
        "t": "Check failed: {0}"
      },
      {
        "s": "简要说明此方案的特色…",
        "t": "Briefly describe what makes this profile special…"
      },
      {
        "s": "将 {0} 停用并移入皮肤目录中的 .deleted 文件夹。",
        "t": "Disable {0} and move it to the .deleted folder in the skins folder."
      },
      {
        "s": "将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。",
        "t": "This restores the card face from the app's built-in default pack, overwriting the same-named image in the current profile; a modified default on disk won't be used as the original template."
      },
      {
        "s": "角色 {0}",
        "t": "Character {0}"
      },
      {
        "s": "角色文案",
        "t": "Character Text"
      },
      {
        "s": "结算奖励",
        "t": "Settlement Rewards"
      },
      {
        "s": "结算快照中的完整属性与行动统计，记录本局关键数据指标。",
        "t": "Full stats and action counts from the settlement snapshot, recording this match's key metrics."
      },
      {
        "s": "结算快照中的总伤害、承伤、治疗、转账与行动次数。",
        "t": "Total damage, damage taken, healing, transfers, and action counts from the settlement snapshot."
      },
      {
        "s": "解析展示失败: {0}",
        "t": "Failed to parse for display: {0}"
      },
      {
        "s": "界面动态效果已{0}",
        "t": "Interface animations {0}"
      },
      {
        "s": "仅替换 {0}",
        "t": "Replace only {0}"
      },
      {
        "s": "仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。",
        "t": "Only counts movement dice results actually received in the replay; every die in multi-dice rolls is kept, controlled moves are marked separately, and combat or check dice are excluded."
      },
      {
        "s": "仅支持英文、数字、下划线及减号",
        "t": "Only letters, digits, underscores, and hyphens are allowed"
      },
      {
        "s": "旧配置已保留，但暂未转换：{0}",
        "t": "Old config kept but not converted yet: {0}"
      },
      {
        "s": "居中重置",
        "t": "Reset to Center"
      },
      {
        "s": "卡牌 / 技能",
        "t": "Cards / Skills"
      },
      {
        "s": "卡牌编号:",
        "t": "Card number:"
      },
      {
        "s": "卡牌名称:",
        "t": "Card name:"
      },
      {
        "s": "卡牌使用",
        "t": "Cards Used"
      },
      {
        "s": "开启自动托管（保留 {0} 局）",
        "t": "Turn on auto-manage (keep {0} matches)"
      },
      {
        "s": "开始下载",
        "t": "Start Download"
      },
      {
        "s": "可以更新到 v{0}",
        "t": "Can update to v{0}"
      },
      {
        "s": "口径",
        "t": "Basis"
      },
      {
        "s": "来源与替换范围",
        "t": "Source and Replacement Scope"
      },
      {
        "s": "来自回合快照中的累计转账；未记录收款对象时不做推断。",
        "t": "From cumulative transfers in round snapshots; no inference is made when the recipient isn't recorded."
      },
      {
        "s": "类别:",
        "t": "Category:"
      },
      {
        "s": "累计",
        "t": "Cumulative"
      },
      {
        "s": "累计伤害",
        "t": "Total Damage"
      },
      {
        "s": "累计死亡",
        "t": "Total Deaths"
      },
      {
        "s": "累计转账",
        "t": "Total Transferred"
      },
      {
        "s": "立即复制创建",
        "t": "Copy and Create Now"
      },
      {
        "s": "留空使用原版",
        "t": "Leave blank to use the original"
      },
      {
        "s": "锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。",
        "t": "Anchors use 0–1, with Y measured up from the bottom of the canvas; scale multiplies PPU and doesn't alter the game's character collision or movement."
      },
      {
        "s": "没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）",
        "t": "No standard Unity game structure detected (missing *_Data or UnityPlayer.dll)"
      },
      {
        "s": "没有匹配的方案。",
        "t": "No matching profiles."
      },
      {
        "s": "每回合新增",
        "t": "Per round"
      },
      {
        "s": "名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。",
        "t": "The name and bio use the character's shared text and may affect the character's other skins."
      },
      {
        "s": "模组状态与使用说明",
        "t": "Mod status and usage notes"
      },
      {
        "s": "默认：{0}",
        "t": "Default: {0}"
      },
      {
        "s": "难度 {0} · 最终首领 {1}",
        "t": "Difficulty {0} · Final boss {1}"
      },
      {
        "s": "内置 SHA-256 {0}…（卸载时按此校验，防止误删他人文件）",
        "t": "Bundled SHA-256 {0}… (checked during uninstall so other people's files aren't deleted by mistake)"
      },
      {
        "s": "内置文件就绪：version.dll {0}",
        "t": "Bundled files ready: version.dll {0}"
      },
      {
        "s": "您的昵称",
        "t": "Your nickname"
      },
      {
        "s": "配置 · {0}",
        "t": "Config · {0}"
      },
      {
        "s": "配置保存在",
        "t": "Config is saved in"
      },
      {
        "s": "偏移",
        "t": "Offset"
      },
      {
        "s": "启用 / 禁用（重启游戏后生效）",
        "t": "Enable / disable (takes effect after a game restart)"
      },
      {
        "s": "清单 JSON 无效：{0}",
        "t": "Invalid manifest JSON: {0}"
      },
      {
        "s": "清空（让 mod 用它自己的默认键）",
        "t": "Clear (let the mod use its own default key)"
      },
      {
        "s": "请在左侧点击任意协议帧进行查看解码数据",
        "t": "Click any protocol frame on the left to view its decoded data"
      },
      {
        "s": "区服",
        "t": "Region"
      },
      {
        "s": "取消",
        "t": "Cancel"
      },
      {
        "s": "取消下载",
        "t": "Cancel Download"
      },
      {
        "s": "全队转账",
        "t": "Team Transfers"
      },
      {
        "s": "确定（已选 {0} 个）",
        "t": "Confirm ({0} selected)"
      },
      {
        "s": "确定删除模组「{0}」？",
        "t": "Delete mod \"{0}\"?"
      },
      {
        "s": "确定要彻底删除方案",
        "t": "Permanently delete profile"
      },
      {
        "s": "确定要将",
        "t": "Are you sure you want to"
      },
      {
        "s": "确认还原",
        "t": "Confirm Restore"
      },
      {
        "s": "确认继续",
        "t": "Confirm and Continue"
      },
      {
        "s": "确认删除",
        "t": "Confirm Delete"
      },
      {
        "s": "人物设置",
        "t": "Character Settings"
      },
      {
        "s": "仍然导入",
        "t": "Import Anyway"
      },
      {
        "s": "仍要放回（{0} 份）",
        "t": "Restore Anyway ({0} copies)"
      },
      {
        "s": "如 二次元萌化包",
        "t": "e.g. Anime Moe Pack"
      },
      {
        "s": "如 my_custom_skin",
        "t": "e.g. my_custom_skin"
      },
      {
        "s": "筛选筹码、玩家、品质、来源...",
        "t": "Filter by relic, player, quality, source..."
      },
      {
        "s": "筛选商店类型、玩家、卡牌、购买结果...",
        "t": "Filter by shop type, player, card, purchase result..."
      },
      {
        "s": "删除方案",
        "t": "Delete Profile"
      },
      {
        "s": "删除该档位",
        "t": "Delete This Preset"
      },
      {
        "s": "删除回放 {0}",
        "t": "Delete Replay {0}"
      },
      {
        "s": "删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。",
        "t": "Deletion goes to the Windows Recycle Bin first and can still be undone. Deleting only the in-game copy is the usual way to free a slot; the library copy is your long-term archive."
      },
      {
        "s": "删除模组",
        "t": "Delete Mod"
      },
      {
        "s": "删除皮肤包",
        "t": "Delete Skin Pack"
      },
      {
        "s": "删除这个模组",
        "t": "Delete This Mod"
      },
      {
        "s": "伤害",
        "t": "Damage"
      },
      {
        "s": "商店进店共 {0} 次（其中 {1} 次有购买）",
        "t": "Shop visits: {0} (of which {1} with purchases)"
      },
      {
        "s": "上次游戏未能应用方案：{0}",
        "t": "The profile couldn't be applied last game session: {0}"
      },
      {
        "s": "上一帧",
        "t": "Previous Frame"
      },
      {
        "s": "生命",
        "t": "Health"
      },
      {
        "s": "时长",
        "t": "Duration"
      },
      {
        "s": "输出伤害",
        "t": "Damage Dealt"
      },
      {
        "s": "输出预览",
        "t": "Output Preview"
      },
      {
        "s": "输入方案名称",
        "t": "Enter profile name"
      },
      {
        "s": "刷新方案与状态",
        "t": "Refresh Profiles and Status"
      },
      {
        "s": "死亡计数",
        "t": "Death Count"
      },
      {
        "s": "搜索方案",
        "t": "Search profiles"
      },
      {
        "s": "搜索帧号、CMD、消息名...",
        "t": "Search frame number, CMD, message name..."
      },
      {
        "s": "提交 {0}",
        "t": "Commit {0}"
      },
      {
        "s": "替换 {0} 第 {1} 帧",
        "t": "Replace {0} Frame {1}"
      },
      {
        "s": "替换选中帧",
        "t": "Replace Selected Frame"
      },
      {
        "s": "添加或修改人物后，完全退出并重启游戏以应用搭配。",
        "t": "After adding or changing characters, fully exit and restart the game to apply the lineup."
      },
      {
        "s": "添加人物皮肤",
        "t": "Add Character Skin"
      },
      {
        "s": "跳转到帧",
        "t": "Go to Frame"
      },
      {
        "s": "同步配置：{0}",
        "t": "Sync config: {0}"
      },
      {
        "s": "图片资源",
        "t": "Image assets"
      },
      {
        "s": "拖动或用方向键移动裁剪区域",
        "t": "Drag or use the arrow keys to move the crop area"
      },
      {
        "s": "拖动选区移动位置，拖动四角调整大小。{0}",
        "t": "Drag the selection to move it, or drag a corner to resize. {0}"
      },
      {
        "s": "拖动选区移动位置，用滑块调整大小。透明背景会保留。",
        "t": "Drag the selection to move it, or use the slider to resize. Transparent backgrounds are preserved."
      },
      {
        "s": "玩家",
        "t": "Player"
      },
      {
        "s": "玩家 {0}",
        "t": "Player {0}"
      },
      {
        "s": "玩家数据对照",
        "t": "Player Comparison"
      },
      {
        "s": "玩家转账贡献",
        "t": "Player Transfer Contribution"
      },
      {
        "s": "文件 {0} 已存在，继续将覆盖它。",
        "t": "{0} already exists; continuing will overwrite it."
      },
      {
        "s": "文件名称:",
        "t": "File name:"
      },
      {
        "s": "我的方案",
        "t": "My Profiles"
      },
      {
        "s": "下面这些模组，包里的版本比游戏目录里已装的更旧：",
        "t": "These mods in the package are older than the versions installed in the game folder:"
      },
      {
        "s": "下一帧",
        "t": "Next Frame"
      },
      {
        "s": "显示 {0} / {1} 次商店进店",
        "t": "Showing {0} / {1} shop visits"
      },
      {
        "s": "显示 {0} / {1} 条筹码流转",
        "t": "Showing {0} / {1} relic transfers"
      },
      {
        "s": "显示 {0} / {1} 帧",
        "t": "Showing {0} / {1} frames"
      },
      {
        "s": "消息类型",
        "t": "Message Type"
      },
      {
        "s": "校验并保存清单",
        "t": "Validate and Save Manifest"
      },
      {
        "s": "协议载荷解码详情",
        "t": "Protocol Payload Decode Details"
      },
      {
        "s": "协议帧共 {0} 帧",
        "t": "Protocol frames: {0}"
      },
      {
        "s": "卸载：{0}",
        "t": "Uninstall: {0}"
      },
      {
        "s": "新建方案",
        "t": "New Profile"
      },
      {
        "s": "新增击杀",
        "t": "New Kills"
      },
      {
        "s": "新增伤害",
        "t": "New Damage"
      },
      {
        "s": "新增转账",
        "t": "New Transfers"
      },
      {
        "s": "星币",
        "t": "Star Coins"
      },
      {
        "s": "星币 · 击杀",
        "t": "Star Coins · Kills"
      },
      {
        "s": "修改配置",
        "t": "Edit Config"
      },
      {
        "s": "修改时间: {0}",
        "t": "Modified: {0}"
      },
      {
        "s": "修改这个模组的配置",
        "t": "Edit this mod's config"
      },
      {
        "s": "选区大小",
        "t": "Selection size"
      },
      {
        "s": "选取你的卡面",
        "t": "Pick Your Card Art"
      },
      {
        "s": "选择按键组合 · {0}",
        "t": "Choose key combo · {0}"
      },
      {
        "s": "选择第 {0} 帧",
        "t": "Select Frame {0}"
      },
      {
        "s": "选择动画帧",
        "t": "Select animation frame"
      },
      {
        "s": "选择图片",
        "t": "Choose Image"
      },
      {
        "s": "循环",
        "t": "Loop"
      },
      {
        "s": "沿用原皮肤",
        "t": "Keep Original Skin"
      },
      {
        "s": "依赖：{0}",
        "t": "Dependencies: {0}"
      },
      {
        "s": "移除",
        "t": "Remove"
      },
      {
        "s": "移除 {0} 的自制帧和原精灵映射，重启游戏后恢复原动作。",
        "t": "Remove {0}'s custom frames and original sprite mappings; restart the game to restore the original action."
      },
      {
        "s": "移除动态覆盖",
        "t": "Remove Animated Override"
      },
      {
        "s": "移动点数",
        "t": "Movement Roll"
      },
      {
        "s": "移动骰子统计",
        "t": "Movement Dice Stats"
      },
      {
        "s": "已安装 v{0} —— 点「检查更新」看看 GitHub 上有没有新版。",
        "t": "Installed v{0} — click Check for Updates to see if a new version is on GitHub."
      },
      {
        "s": "已安装 v{0}，比随程序内置的 v{1} 更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。",
        "t": "Installed v{0}, newer than the bundled v{1} (this copy came from a GitHub update) — click Check for Updates to see if there's anything newer."
      },
      {
        "s": "已安装 v{0}，比随程序内置的 v{1} 旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。",
        "t": "Installed v{0}, older than the bundled v{1} — click Reinstall Built-in Version to go back to the bundled build, or Check for Updates to see if a newer one exists."
      },
      {
        "s": "已安装 v{0}，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。",
        "t": "Installed v{0}, matching the bundled version — click Check for Updates to confirm whether GitHub has a newer version."
      },
      {
        "s": "已持有 {0} 个筹码：",
        "t": "Owned {0} relics:"
      },
      {
        "s": "已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近 {0} 局）。",
        "t": "Auto-manage is on: from now on, every time you open this tool, replays beyond the slot limit are archived to the library automatically (the game keeps the latest {0} matches)."
      },
      {
        "s": "已设为 {0}",
        "t": "Set as {0}"
      },
      {
        "s": "已是最新版本 v{0}。",
        "t": "Already up to date (v{0})."
      },
      {
        "s": "已下载 {0}",
        "t": "Downloaded {0}"
      },
      {
        "s": "已下载 {0} / {1}（{2}%）",
        "t": "Downloaded {0} / {1} ({2}%)"
      },
      {
        "s": "已装的 v{0} 比 GitHub 上的 v{1} 更新，无需更新。",
        "t": "Installed v{0} is newer than v{1} on GitHub; no update needed."
      },
      {
        "s": "用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。",
        "t": "Compare the match with damage, support, and action data; roles differ, so players aren't judged on total damage alone."
      },
      {
        "s": "游戏名称",
        "t": "Game name"
      },
      {
        "s": "游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；只想清掉就勾选「强制卸载」。",
        "t": "The version.dll in the game folder is neither the bundled copy nor recorded in the install manifest: it's most likely from another tool (such as the legacy standalone speedhack on the Game Tools page) or a third-party file. To switch to CesiumLoader, tick Allow overwriting a version.dll in the game folder that this tool didn't install below, then click Install Loader; to just clear it, tick Force Uninstall."
      },
      {
        "s": "游戏内回放席位快满了（{0}/{1}）",
        "t": "In-game replay slots are almost full ({0}/{1})"
      },
      {
        "s": "游戏内回放席位已满（{0}/{1}）",
        "t": "In-game replay slots are full ({0}/{1})"
      },
      {
        "s": "游戏在「本地回放」列表里遇到这种目录会把它整个删掉",
        "t": "The game deletes such folders entirely when it encounters them in the Local Replays list"
      },
      {
        "s": "游戏中的角色皮肤模组为 {0}，请更新到 {1}，然后重启游戏以使用最新修复。",
        "t": "The character skin mod in game is {0}; please update to {1}, then restart the game to apply the latest fixes."
      },
      {
        "s": "有新版本 v{0} 可更新",
        "t": "New version v{0} available"
      },
      {
        "s": "与「已禁用」状态仍会保留， 被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。",
        "t": " and the Disabled state are kept; replaced old files go to the Recycle Bin (kept if it's unavailable). If you just want to upgrade, use a newer mod pack instead."
      },
      {
        "s": "原图 {0} × {1}",
        "t": "Original {0} × {1}"
      },
      {
        "s": "在资源管理器中打开",
        "t": "Open in File Explorer"
      },
      {
        "s": "载荷字节",
        "t": "Payload bytes"
      },
      {
        "s": "战绩里那一长串数字",
        "t": "the long number in your match history"
      },
      {
        "s": "找到 {0} 个候选：",
        "t": "Found {0} candidates:"
      },
      {
        "s": "这个目录已经不在了",
        "t": "That folder no longer exists"
      },
      {
        "s": "帧",
        "t": " frames"
      },
      {
        "s": "帧号: {0} · {1} CMD: {2} · {3} 载荷大小: {4} 字节 {5}",
        "t": "Frame: {0} · {1} CMD: {2} · {3} Payload size: {4} bytes {5}"
      },
      {
        "s": "正在读取…",
        "t": "Reading…"
      },
      {
        "s": "支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。",
        "t": "Supports original sprite name mapping (SpriteMappings) and custom asset addresses (asset:). Media files can be stored in the pack but aren't applied yet."
      },
      {
        "s": "只修改方案对外展示的信息，不会改变方案目录名",
        "t": "Only changes the profile's public info; the profile folder name stays the same"
      },
      {
        "s": "指标",
        "t": "Metric"
      },
      {
        "s": "治疗",
        "t": "Healing"
      },
      {
        "s": "逐回合数据表",
        "t": "Per-round data table"
      },
      {
        "s": "主配置：{0}{1}",
        "t": "Main config: {0}{1}"
      },
      {
        "s": "转出",
        "t": "Transferred Out"
      },
      {
        "s": "转换并保存",
        "t": "Convert and Save"
      },
      {
        "s": "转账",
        "t": "Transfers"
      },
      {
        "s": "转账贡献",
        "t": "Transfer Contribution"
      },
      {
        "s": "转账星币",
        "t": "Star Coins Transferred"
      },
      {
        "s": "自动保持游戏内最近 {0} 局",
        "t": "Auto-keep the latest {0} matches in game"
      },
      {
        "s": "自动托管已{0}",
        "t": "Auto-manage is {0}"
      },
      {
        "s": "自动托管已开启（保留 {0} 局）",
        "t": "Auto-manage is on (keep {0} matches)"
      },
      {
        "s": "总承伤",
        "t": "Total Damage Taken"
      },
      {
        "s": "总伤害",
        "t": "Total Damage"
      },
      {
        "s": "最近一次 Mod 记录：{0} · {1} · 模组 {2}{3}。修改配置后，重启游戏并刷新可查看新结果。",
        "t": "Latest Mod record: {0} · {1} · Mod {2}{3}. After changing the config, restart the game and refresh to see the new result."
      },
      {
        "s": "作者: {0}",
        "t": "Author: {0}"
      },
      {
        "s": "作者：{0}",
        "t": "Author: {0}"
      },
      {
        "s": "作者签名（可选）：",
        "t": "Author signature (optional):"
      },
      {
        "s": "PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。",
        "t": "PNG/JPG frames are sorted by file name. Idle for idle, Walk forward, Walk-back backward; other action names follow the original character."
      },
      {
        "s": "Unity 结构 {0}",
        "t": "Unity structure {0}"
      },
      {
        "s": "version.dll 已停用或缺失，保留版本 {0}。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。",
        "t": "version.dll is disabled or missing; keeping version {0}. Click Restore version.dll; if the local file is missing, the matching release package is downloaded."
      },
      {
        "s": "- 初始候选池: {0}",
        "t": "- Initial candidate pool: {0}"
      },
      {
        "s": "- 刷新后呈现的新候选池: {0}",
        "t": "- New candidate pool after reroll: {0}"
      },
      {
        "s": "- 刷新情况: {0}",
        "t": "- Reroll status: {0}"
      },
      {
        "s": "- 动作类型: 【刷新候选】(第 {0} 次刷新)",
        "t": "- Action type: [Reroll candidates] (reroll #{0})"
      },
      {
        "s": "- 参战角色: {0} (英雄ID: {1} · 账号等级: {2})",
        "t": "- Character: {0} (Hero ID: {1} · Account level: {2})"
      },
      {
        "s": "- 回合统计: 共 {0} 回合 (包含 {1} 个行动轮次, 帧数 {2})",
        "t": "- Round stats: {0} rounds total ({1} action turns, {2} frames)"
      },
      {
        "s": "- 回放文件: {0}",
        "t": "- Replay file: {0}"
      },
      {
        "s": "- 对局地图: {0} (地图ID: {1})",
        "t": "- Map: {0} (Map ID: {1})"
      },
      {
        "s": "- 对局时间: {0} 至 {1} (时长: {2})",
        "t": "- Match time: {0} to {1} (duration: {2})"
      },
      {
        "s": "- 对局结果: {0} (最终决胜: {1})",
        "t": "- Match result: {0} (final showdown: {1})"
      },
      {
        "s": "- 对局进程: {0}",
        "t": "- Match progress: {0}"
      },
      {
        "s": "- 最终持有筹码清单: {0}",
        "t": "- Final relics held: {0}"
      },
      {
        "s": "- 最终状态: {0}",
        "t": "- Final state: {0}"
      },
      {
        "s": "- 来源途径: {0} ({1})",
        "t": "- Source: {0} ({1})"
      },
      {
        "s": "- 棋盘移动: {0} 点 | 卡牌使用: {1} 张 | 技能释放: {2} 次",
        "t": "- Board movement: {0} pips | Cards used: {1} | Skills used: {2}"
      },
      {
        "s": "- 模式/难度: {0} / {1}",
        "t": "- Mode / difficulty: {0} / {1}"
      },
      {
        "s": "- 游戏版本: {0}",
        "t": "- Game version: {0}"
      },
      {
        "s": "- 玩家刷新候选次数: {0} 次",
        "t": "- Player rerolls: {0}"
      },
      {
        "s": "- 玩家阵亡次数: {0} 次",
        "t": "- Player deaths: {0}"
      },
      {
        "s": "- 生命值: {0} | 拥有星币: {1} | 攻防属性: 攻击 {2} / 防御 {3}",
        "t": "- Health: {0} | Star Coins: {1} | Attack / Defense: {2} / {3}"
      },
      {
        "s": "- 筹码地块购买次数: {0} 次 | 最终持有筹码数: {1} 个",
        "t": "- Relic tile purchases: {0} | Final relics held: {1}"
      },
      {
        "s": "- 筹码来源: {0}",
        "t": "- Relic source: {0}"
      },
      {
        "s": "- 筹码流转记录总数: {0} 条 (最终选定 {1} 次，出现候选 {2} 次)",
        "t": "- Relic flow records: {0} ({1} final picks, {2} candidate offers)"
      },
      {
        "s": "- 获得筹码: 【{0}】 (品质: {1} · ID: {2} · {3})",
        "t": "- Relic gained: [{0}] (quality: {1} · ID: {2} · {3})"
      },
      {
        "s": "- 输出伤害: {0} | 承受伤害: {1} | 击杀数: {2} | 治疗量: {3}",
        "t": "- Damage dealt: {0} | Damage taken: {1} | Kills: {2} | Healing: {3}"
      },
      {
        "s": "- 首领(BOSS): {0}",
        "t": "- Boss: {0}"
      },
      {
        "s": "3. 【关键胜负手复盘】根据获胜者（{0}）与其他玩家的数据，分析获胜者主要是依靠哪几个核心筹码建立优势并最终斩获胜利的？",
        "t": "3. [Key winning moves] Using the winner's ({0}) and other players' data, which core relics did the winner rely on to build an advantage and close out the win?"
      },
      {
        "s": "CDN 上没有这个回放（HTTP 404）——ID 可能写错了，或者这局不在{0}。",
        "t": "This replay isn't on the CDN (HTTP 404) — the ID may be wrong, or this match isn't on {0}."
      },
      {
        "s": "GIF 已保存：{0}",
        "t": "GIF saved: {0}"
      },
      {
        "s": "ID {0} · 槽位 {1} · 账号等级 {2}",
        "t": "ID {0} · Slot {1} · Account level {2}"
      },
      {
        "s": "Steam 不可用（{0}），已直接启动游戏主程序。",
        "t": "Steam isn't available ({0}); launched the game executable directly."
      },
      {
        "s": "UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：{0}",
        "t": "Invalid UnityFS size declaration, or over 2 GiB and can't be loaded into memory: {0}"
      },
      {
        "s": "UnityFS 头不完整：{0}",
        "t": "Incomplete UnityFS header: {0}"
      },
      {
        "s": "UnityFS 存在无法解释的尾部数据，拒绝修复：{0}",
        "t": "UnityFS has unexplained trailing data; refusing to repair: {0}"
      },
      {
        "s": "UnityFS 版本头无效：{0}",
        "t": "Invalid UnityFS version header: {0}"
      },
      {
        "s": "ZIP 存在重复路径：{0}",
        "t": "The ZIP contains duplicate paths: {0}"
      },
      {
        "s": "ZIP 文件与目录路径冲突：{0}",
        "t": "ZIP file and folder paths conflict: {0}"
      },
      {
        "s": "ZIP 文件长度不完整：{0}",
        "t": "Incomplete ZIP file length: {0}"
      },
      {
        "s": "ZIP 路径无效：{0}",
        "t": "Invalid ZIP path: {0}"
      },
      {
        "s": "[事件 {0} | 帧 {1}] 玩家「{2}」（角色：{3}）",
        "t": "[Event {0} | Frame {1}] Player \"{2}\" (character: {3})"
      },
      {
        "s": "mod 不存在：{0}",
        "t": "Mod not found: {0}"
      },
      {
        "s": "mod 包包含不安全的路径：{0}",
        "t": "The mod pack contains an unsafe path: {0}"
      },
      {
        "s": "mod 包包含重复文件：{0}",
        "t": "The mod pack contains a duplicate file: {0}"
      },
      {
        "s": "mod 包包含非法的文件名：{0}",
        "t": "The mod pack contains an invalid file name: {0}"
      },
      {
        "s": "mod 包里有两个同名模组（{0}），已停止导入。",
        "t": "The mod pack contains two mods with the same name ({0}); import stopped."
      },
      {
        "s": "mod 包里的 {0}.dll 没有成功写入，已停止导入。",
        "t": "{0}.dll from the mod pack wasn't written successfully; import stopped."
      },
      {
        "s": "version.dll 已删除，但 speedhack_config.json 删除失败：{0}",
        "t": "version.dll was deleted, but speedhack_config.json couldn't be deleted: {0}"
      },
      {
        "s": "version.dll 已删除，但 {0} 目录删除失败：{1}（可能仍有文件被占用）。",
        "t": "version.dll was deleted, but the {0} folder couldn't be removed: {1} (some files may still be in use)."
      },
      {
        "s": "{0} {1} 个",
        "t": "{0} {1}"
      },
      {
        "s": "{0} {1} 次",
        "t": "{0} {1} times"
      },
      {
        "s": "{0} · 卡面转换导入",
        "t": "{0} · Card art conversion import"
      },
      {
        "s": "{0} · 服务器 {1}",
        "t": "{0} · Server {1}"
      },
      {
        "s": "{0} · 转换导入",
        "t": "{0} · Conversion import"
      },
      {
        "s": "{0} 个旧版本文件没能移入回收站，已保留",
        "t": "{0} old-version files couldn't be moved to the Recycle Bin and were kept"
      },
      {
        "s": "{0} 仍为禁用状态",
        "t": "{0} is still disabled"
      },
      {
        "s": "{0} 元数据读取失败：{1}",
        "t": "Failed to read {0} metadata: {1}"
      },
      {
        "s": "{0} 已装 v{1} → 包内 v{2}",
        "t": "{0}: installed v{1} → package v{2}"
      },
      {
        "s": "{0} 帧 · {1} 种命令",
        "t": "{0} frames · {1} command types"
      },
      {
        "s": "{0}{1}。可将此皮肤包添加到人物方案。{2}{3}",
        "t": "{0}{1}. You can add this skin pack to a character profile. {2}{3}"
      },
      {
        "s": "{0}{1}，并在控制台窗口显示日志。",
        "t": "{0}{1}, and show logs in the console window."
      },
      {
        "s": "{0}如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。",
        "t": "{0}If the game can't read it either, don't restore it to the game folder — the game deletes the whole folder when it lists it."
      },
      {
        "s": "{0}如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。",
        "t": "{0}If the game can't read it either, it deletes the whole folder when it lists this replay. Confirm the restore once more if you want to proceed."
      },
      {
        "s": "{0}如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。",
        "t": "{0}If you really want to downgrade, tick \"Allow installing an older loader\"; usually it's better to use a newer version of AstralParty.Toys."
      },
      {
        "s": "{0}已按请求的 ID 入库，需要的话可以改名后再放回游戏。",
        "t": "{0}Archived under the requested ID; rename it before restoring to the game if needed."
      },
      {
        "s": "{0}确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。",
        "t": "{0}To switch to the CDN copy, delete the old library copy first (or use a different ID)."
      },
      {
        "s": "{0}继续保存新回放会失败。播放不受影响，建议归档几局。",
        "t": "{0}Saving new replays will keep failing. Playback is unaffected; consider archiving a few matches."
      },
      {
        "s": "{0}请确认回放 ID 与区服是否对应。",
        "t": "{0}Check that the replay ID matches the region."
      },
      {
        "s": "{0}（v{1}，版本未变）",
        "t": "{0} (v{1}, version unchanged)"
      },
      {
        "s": "{0}（升级 v{1} → v{2}）",
        "t": "{0} (upgraded v{1} → v{2})"
      },
      {
        "s": "{0}（折后 {1}）",
        "t": "{0} (after discount {1})"
      },
      {
        "s": "{0}（新装 v{1}）",
        "t": "{0} (new install v{1})"
      },
      {
        "s": "{0}（版本未知）",
        "t": "{0} (version unknown)"
      },
      {
        "s": "{0}（降级 v{1} → v{2}）",
        "t": "{0} (downgraded v{1} → v{2})"
      },
      {
        "s": "{0}：下载到的 {1} 字节不是一个回放帧流（CDN 可能返回了错误页）。",
        "t": "{0}: the downloaded {1} bytes aren't a replay frame stream (the CDN may have returned an error page)."
      },
      {
        "s": "{0}：下载失败（{1}）。",
        "t": "{0}: download failed ({1})."
      },
      {
        "s": "{0}：下载已取消，没有写入回放库。",
        "t": "{0}: download canceled; nothing was written to the replay library."
      },
      {
        "s": "{0}：从{1}下载到的是空文件，已放弃。",
        "t": "{0}: the download from {1} was empty; gave up."
      },
      {
        "s": "{0}：删除库内副本失败（{1}）。",
        "t": "{0}: failed to delete the library copy ({1})."
      },
      {
        "s": "{0}：删除游戏目录副本失败（{1}）。",
        "t": "{0}: failed to delete the in-game copy ({1})."
      },
      {
        "s": "{0}：回放库里已有同 ID 但内容不同的回放，为避免覆盖已中止。",
        "t": "{0}: the replay library already has a different replay with the same ID; stopped to avoid overwriting it."
      },
      {
        "s": "{0}：回放库里已经有同一份回放（{1}），本次下载已丢弃。",
        "t": "{0}: the replay library already has this exact replay ({1}); the download was discarded."
      },
      {
        "s": "{0}：回放库里没有这个回放，已跳过。",
        "t": "{0}: this replay isn't in the replay library; skipped."
      },
      {
        "s": "{0}：已从{1}下载并存入回放库（{2}）。",
        "t": "{0}: downloaded from {1} into the replay library ({2})."
      },
      {
        "s": "{0}：已从回放库删除{1}。",
        "t": "{0}: deleted {1} from the replay library."
      },
      {
        "s": "{0}：已从游戏目录删除{1}。",
        "t": "{0}: deleted {1} from the game folder."
      },
      {
        "s": "{0}：已复制进库，但删除游戏目录副本失败（{1}）。库里有备份，可手动删除。",
        "t": "{0}: copied to the library, but the in-game copy couldn't be deleted ({1}). There's a backup in the library; you can delete it manually."
      },
      {
        "s": "{0}：已归档到回放库。",
        "t": "{0}: archived to the replay library."
      },
      {
        "s": "{0}：已放回游戏目录，游戏内「本地回放」可直接观看。",
        "t": "{0}: restored to the game folder; it's now viewable under Local Replays in game."
      },
      {
        "s": "{0}：库里存在同 ID 但内容不同的回放，为避免覆盖已跳过（请手动处理）。",
        "t": "{0}: the library has a different replay with the same ID; skipped to avoid overwriting (please handle it manually)."
      },
      {
        "s": "{0}：库里已有同一份回放，已直接腾出游戏席位。",
        "t": "{0}: the library already has this replay, so the game slot was freed directly."
      },
      {
        "s": "{0}：库里已经有这一局了。",
        "t": "{0}: this match is already in the library."
      },
      {
        "s": "{0}：归档失败（{1}）。",
        "t": "{0}: archiving failed ({1})."
      },
      {
        "s": "{0}：放回失败（{1}）。",
        "t": "{0}: restore failed ({1})."
      },
      {
        "s": "{0}：本工具解析不出它的结算帧（{1}），文件仍已原样保留。",
        "t": "{0}: this tool can't parse its settlement frame ({1}); the file was kept as is."
      },
      {
        "s": "{0}：本工具解析不出它的结算帧（工具自带的协议版本可能比游戏旧）。",
        "t": "{0}: this tool can't parse its settlement frame (its bundled protocol version may be older than the game's)."
      },
      {
        "s": "{0}：注意文件内记录的 ID 是 {1}，与请求的 ID 不同；",
        "t": "{0}: note the ID recorded in the file is {1}, which differs from the requested ID;"
      },
      {
        "s": "{0}：游戏目录里已有同 ID 但内容不同的文件，为避免覆盖已跳过。",
        "t": "{0}: the game folder has a different file with the same ID; skipped to avoid overwriting."
      },
      {
        "s": "{0}：游戏目录里已经有这一局了。",
        "t": "{0}: this match is already in the game folder."
      },
      {
        "s": "{0}：游戏目录里没有这个回放，已跳过。",
        "t": "{0}: this replay isn't in the game folder; skipped."
      },
      {
        "s": "{0}：移除与目录块尾部完全相同的 {1} 个追加字节；原文件保存在 source/。",
        "t": "{0}: removed {1} appended bytes matching the end of the directory block; the original file is saved in source/."
      },
      {
        "s": "、{0} 个动作（{1} 张精灵帧）",
        "t": ", {0} actions ({1} sprite frames)"
      },
      {
        "s": "【{0}】(共 {1} 条流转事件)",
        "t": "[{0}] ({1} flow events)"
      },
      {
        "s": "【玩家】{0} (ID: {1})",
        "t": "[Player] {0} (ID: {1})"
      },
      {
        "s": "下载失败（{0}）请检查网络，或换一个区服试试（当前选的是{1}）。",
        "t": "Download failed ({0}). Check your network, or try another region (currently {1})."
      },
      {
        "s": "下载失败：HTTP {0}。",
        "t": "Download failed: HTTP {0}."
      },
      {
        "s": "下载失败：{0}",
        "t": "Download failed: {0}"
      },
      {
        "s": "下载超过 {0} MB 上限，已中止。",
        "t": "The download exceeded the {0} MB limit and was aborted."
      },
      {
        "s": "下载默认包失败：{0}",
        "t": "Failed to download the default pack: {0}"
      },
      {
        "s": "不支持或重复的视频资源：{0}",
        "t": "Unsupported or duplicate video asset: {0}"
      },
      {
        "s": "不支持的 MonoBehaviour：{0}",
        "t": "Unsupported MonoBehaviour: {0}"
      },
      {
        "s": "不支持的角色资源类型：{0}",
        "t": "Unsupported character asset type: {0}"
      },
      {
        "s": "不是 UnityFS bundle：{0}",
        "t": "Not a UnityFS bundle: {0}"
      },
      {
        "s": "主界面启动失败：{0}",
        "t": "Failed to start the main window: {0}"
      },
      {
        "s": "事件 #{0}",
        "t": "Event #{0}"
      },
      {
        "s": "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。{0}",
        "t": "Only version.dll was deleted; mods, config, SDK, and logs were kept. {0}"
      },
      {
        "s": "从本地游戏皮肤 {0} 导出的制作模板",
        "t": "Editing template exported from the local game skin {0}"
      },
      {
        "s": "任务已完成，皮肤列表刷新失败：{0}",
        "t": "Task completed, but refreshing the skin list failed: {0}"
      },
      {
        "s": "保存加载器设置失败：{0}",
        "t": "Failed to save loader settings: {0}"
      },
      {
        "s": "保存方案信息失败：{0}",
        "t": "Failed to save profile info: {0}"
      },
      {
        "s": "保存配置失败：{0}",
        "t": "Failed to save config: {0}"
      },
      {
        "s": "保留了 {0} 的已有配置",
        "t": "Kept the existing config for {0}"
      },
      {
        "s": "倍速必须在 [{0},{1}] 之间（1.0 = 正常速度，不支持减速）。",
        "t": "Speed must be between [{0},{1}] (1.0 = normal speed; slowing down isn't supported)."
      },
      {
        "s": "写入游戏目录失败：{0}。",
        "t": "Failed to write to the game folder: {0}."
      },
      {
        "s": "切换 mod 状态失败：{0}",
        "t": "Failed to toggle mod state: {0}"
      },
      {
        "s": "切换方案失败：{0}",
        "t": "Failed to switch profiles: {0}"
      },
      {
        "s": "删除 version.dll 失败：{0}。",
        "t": "Failed to delete version.dll: {0}."
      },
      {
        "s": "删除 {0} 失败：{1}（游戏运行时 DLL 可能被占用）。",
        "t": "Failed to delete {0}: {1} (the DLL may be in use while the game is running)."
      },
      {
        "s": "删除方案失败：{0}",
        "t": "Failed to delete profile: {0}"
      },
      {
        "s": "刷新 {0} 次后选择",
        "t": "Picked after {0} rerolls"
      },
      {
        "s": "加载器发布包 {0}",
        "t": "Loader release package {0}"
      },
      {
        "s": "加载器已就位，但这份安装没有版本清单 —— 重新安装一次就能纳入版本管理（{0}）。",
        "t": "The loader is in place, but this install has no version manifest — reinstall once to bring it under version tracking ({0})."
      },
      {
        "s": "加载器已就绪（{0}）。",
        "t": "Loader ready ({0})."
      },
      {
        "s": "加载器已就绪，与内置版本一致（{0}）。",
        "t": "Loader ready and matching the bundled version ({0})."
      },
      {
        "s": "加载器已更新{0}。",
        "t": "Loader updated{0}."
      },
      {
        "s": "动作名称无效或重复：{0}",
        "t": "Invalid or duplicate action name: {0}"
      },
      {
        "s": "动作帧或帧率无效: {0}",
        "t": "Invalid action frames or frame rate: {0}"
      },
      {
        "s": "动作引用未知精灵：{0}",
        "t": "Action references an unknown sprite: {0}"
      },
      {
        "s": "动作没有可转换的精灵帧：{0}",
        "t": "Action has no convertible sprite frames: {0}"
      },
      {
        "s": "动态卡面导入失败：{0}",
        "t": "Failed to import animated card art: {0}"
      },
      {
        "s": "动态卡面转换失败：{0}",
        "t": "Failed to convert animated card art: {0}"
      },
      {
        "s": "单个资源包超过 2 GiB，无法读入内存：{0}",
        "t": "A single asset bundle exceeds 2 GiB and can't be loaded into memory: {0}"
      },
      {
        "s": "卡面尺寸超出 Unity 上限（最长边 16384）：{0}",
        "t": "Card art size exceeds Unity's limit (longest side 16384): {0}"
      },
      {
        "s": "原精灵名称重复：{0}",
        "t": "Duplicate original sprite name: {0}"
      },
      {
        "s": "发布包含有非法路径：{0}",
        "t": "The release package contains an invalid path: {0}"
      },
      {
        "s": "发布包校验失败：{0} 哈希不一致。",
        "t": "Release package verification failed: {0} hash mismatch."
      },
      {
        "s": "发布包缺少文件：{0}",
        "t": "The release package is missing a file: {0}"
      },
      {
        "s": "发布包里的加载器（{0}）比游戏目录里已装的（{1}）更旧 —— 这是一次降级，已阻止。",
        "t": "The loader in the release package ({0}) is older than the one installed in the game folder ({1}) — this is a downgrade and was blocked."
      },
      {
        "s": "变速器已安装，每次进入游戏将自动使用 {0} 倍速。",
        "t": "Speedhack installed; the game will automatically run at {0}× speed each time you enter."
      },
      {
        "s": "变速基础倍率必须在 [{0},{1}] 之间（1.0 = 正常速度，不支持减速）。",
        "t": "Base speed must be between [{0},{1}] (1.0 = normal speed; slowing down isn't supported)."
      },
      {
        "s": "吉星派对 · {0}",
        "t": "Astral Party · {0}"
      },
      {
        "s": "同名自定义包已存在: {0}",
        "t": "A custom pack with the same name already exists: {0}"
      },
      {
        "s": "启动游戏失败：{0}",
        "t": "Failed to launch the game: {0}"
      },
      {
        "s": "命运 #{0}",
        "t": "Destiny #{0}"
      },
      {
        "s": "回放 CDN 返回 HTTP {0} {1}",
        "t": "Replay CDN returned HTTP {0} {1}"
      },
      {
        "s": "回放 ID 不该超过 {0} 位（收到 {1} 位）。",
        "t": "A replay ID shouldn't exceed {0} digits (got {1})."
      },
      {
        "s": "回放列表读取失败：{0}",
        "t": "Failed to read the replay list: {0}"
      },
      {
        "s": "回放库已切换到：{0}",
        "t": "Replay library switched to: {0}"
      },
      {
        "s": "图片不存在: {0}",
        "t": "Image not found: {0}"
      },
      {
        "s": "地图事件 #{0}",
        "t": "Map Event #{0}"
      },
      {
        "s": "媒体文件不存在: {0}",
        "t": "Media file not found: {0}"
      },
      {
        "s": "存在重复卡牌资源名：{0}",
        "t": "Duplicate card asset name: {0}"
      },
      {
        "s": "导入失败：{0}",
        "t": "Import failed: {0}"
      },
      {
        "s": "导入旧式卡牌皮肤包失败：{0}",
        "t": "Failed to import legacy card skin pack: {0}"
      },
      {
        "s": "导入皮肤失败: {0}",
        "t": "Failed to import skin: {0}"
      },
      {
        "s": "导出失败: {0}",
        "t": "Export failed: {0}"
      },
      {
        "s": "导出失败：{0}",
        "t": "Export failed: {0}"
      },
      {
        "s": "导出所选皮肤 {0} 的图片",
        "t": "Export images for the selected skin {0}"
      },
      {
        "s": "已从{0}恢复 version.dll（{1}），重启游戏后生效。",
        "t": "Restored version.dll from {0} ({1}); takes effect after a game restart."
      },
      {
        "s": "已保存配置：{0}",
        "t": "Config saved: {0}"
      },
      {
        "s": "已保留 {0} 个视频源资源，视频演出仍使用原版。",
        "t": "Kept {0} video source assets; video cutscenes still use the originals."
      },
      {
        "s": "已创建新皮肤方案: {0}",
        "t": "New skin profile created: {0}"
      },
      {
        "s": "已删除 mod：{0}",
        "t": "Mod deleted: {0}"
      },
      {
        "s": "已删除方案: {0}",
        "t": "Profile deleted: {0}"
      },
      {
        "s": "已加载 {0} 个模组",
        "t": "Loaded {0} mods"
      },
      {
        "s": "已启用 mod：{0}（重启游戏后生效）",
        "t": "Mod enabled: {0} (takes effect after a game restart)"
      },
      {
        "s": "已启用变速：{0}x（重启游戏生效）",
        "t": "Speedhack enabled: {0}x (takes effect after a game restart)"
      },
      {
        "s": "已处理 {0} 个资源包的重复尾部数据，原文件已保留。",
        "t": "Cleaned duplicate trailing data from {0} asset bundles; the original files were kept."
      },
      {
        "s": "已安装（{0}），配置修改后重启游戏生效。",
        "t": "Installed ({0}); config changes take effect after a game restart."
      },
      {
        "s": "已导入 {0} 个模组：{1}——重启游戏后生效",
        "t": "Imported {0} mods: {1} — takes effect after a game restart"
      },
      {
        "s": "已导入 {0} 到回放库。",
        "t": "Imported {0} into the replay library."
      },
      {
        "s": "已导入 {0}，重启游戏后生效。",
        "t": "Imported {0}; takes effect after a game restart."
      },
      {
        "s": "已导出 {0} / {1} 张动画帧",
        "t": "Exported {0} / {1} animation frames"
      },
      {
        "s": "已导出到 {0}",
        "t": "Exported to {0}"
      },
      {
        "s": "已归档 {0} 局最旧的回放，游戏内席位腾出 {1} 个。",
        "t": "Archived the {0} oldest replays and freed {1} in-game slots."
      },
      {
        "s": "已成功导出商店记录到 {0}",
        "t": "Shop records exported to {0}"
      },
      {
        "s": "已成功导出筹码记录到 {0}",
        "t": "Relic records exported to {0}"
      },
      {
        "s": "已扫描 {0} 个目录…",
        "t": "Scanned {0} folders…"
      },
      {
        "s": "已把 %AppData% 里的旧配置复制过来：{0}。",
        "t": "Copied the old config from %AppData%: {0}."
      },
      {
        "s": "已提取 {0} 个图片入口、{1} 个动作、{2} 帧",
        "t": "Extracted {0} image slots, {1} actions, {2} frames"
      },
      {
        "s": "已添加并切换到：{0}",
        "t": "Added and switched to: {0}"
      },
      {
        "s": "已添加：{0}",
        "t": "Added: {0}"
      },
      {
        "s": "已用默认编辑器打开配置：{0}",
        "t": "Opened the config in your default editor: {0}"
      },
      {
        "s": "已用默认编辑器打开：{0}",
        "t": "Opened in your default editor: {0}"
      },
      {
        "s": "已禁用 mod：{0}（重启游戏后生效）",
        "t": "Mod disabled: {0} (takes effect after a game restart)"
      },
      {
        "s": "已自动定位游戏目录：{0}",
        "t": "Game folder detected automatically: {0}"
      },
      {
        "s": "已记住这个选择，但同步加载器配置失败：{0}",
        "t": "Choice saved, but syncing the loader config failed: {0}"
      },
      {
        "s": "已转换动画帧 {0} / {1}",
        "t": "Converted animation frames {0} / {1}"
      },
      {
        "s": "已转换并导入 {0} 张卡面。预览后点击“设为当前”启用。",
        "t": "Converted and imported {0} card faces. Preview them, then click “Set as Current” to enable."
      },
      {
        "s": "已转换并导入 {0} 张图片",
        "t": "Converted and imported {0} images"
      },
      {
        "s": "已还原卡面为默认：{0} ({1})",
        "t": "Card art reverted to default: {0} ({1})"
      },
      {
        "s": "已选择卡面方案：{0}，完全退出并重启游戏后生效。",
        "t": "Card art profile selected: {0}; takes effect after fully exiting and restarting the game."
      },
      {
        "s": "已选择游戏目录：{0}",
        "t": "Game folder selected: {0}"
      },
      {
        "s": "异画 #{0}",
        "t": "Alt-Art #{0}"
      },
      {
        "s": "忽略了 {0} 个非模组文件",
        "t": "Ignored {0} non-mod files"
      },
      {
        "s": "恢复默认包失败：{0}",
        "t": "Failed to restore the default pack: {0}"
      },
      {
        "s": "成功导入皮肤方案: {0}",
        "t": "Skin profile imported: {0}"
      },
      {
        "s": "手牌 #{0}",
        "t": "Hand #{0}"
      },
      {
        "s": "打不开目录：{0}",
        "t": "Can't open the folder: {0}"
      },
      {
        "s": "打不开系统浏览器：{0}",
        "t": "Can't open the system browser: {0}"
      },
      {
        "s": "打开库目录失败：{0}",
        "t": "Failed to open the library folder: {0}"
      },
      {
        "s": "打开目录失败：{0}",
        "t": "Failed to open the folder: {0}"
      },
      {
        "s": "打开配置失败：{0}",
        "t": "Failed to open the config: {0}"
      },
      {
        "s": "扫描出错：{0}",
        "t": "Scan error: {0}"
      },
      {
        "s": "技能视频不是可识别的 CRI USM：{0}",
        "t": "The skill video isn't a recognizable CRI USM: {0}"
      },
      {
        "s": "把 {0} 个旧版本文件移入了回收站",
        "t": "Moved {0} old-version files to the Recycle Bin"
      },
      {
        "s": "操作失败：{0}",
        "t": "Operation failed: {0}"
      },
      {
        "s": "整理完成：游戏目录保留最新 {0} 局，归档 {1} 局。",
        "t": "Tidy-up complete: kept the latest {0} matches in the game folder and archived {1}."
      },
      {
        "s": "文件不存在：{0}",
        "t": "File not found: {0}"
      },
      {
        "s": "文案槽位或长度无效: {0}",
        "t": "Invalid text slot or length: {0}"
      },
      {
        "s": "文档目录不可写（{0}），配置暂时保存在 {1}。",
        "t": "The Documents folder isn't writable ({0}); the config is stored temporarily in {1}."
      },
      {
        "s": "新建方案失败: {0}",
        "t": "Failed to create profile: {0}"
      },
      {
        "s": "方案共享图片冲突: {0}",
        "t": "Profile shared image conflict: {0}"
      },
      {
        "s": "方案共享文案冲突: {0}",
        "t": "Profile shared text conflict: {0}"
      },
      {
        "s": "方案操作失败：{0}",
        "t": "Profile operation failed: {0}"
      },
      {
        "s": "方案缺少自定义包: {0}",
        "t": "Profile is missing a custom pack: {0}"
      },
      {
        "s": "无法下载当前版本 {0} 的加载器包：HTTP {1}。可重试或使用更新功能安装新版。",
        "t": "Can't download the loader package for the current version {0}: HTTP {1}. Try again or use Update to install a newer version."
      },
      {
        "s": "无法完整读取卡牌资源包 {0}：{1}",
        "t": "Couldn't read the card asset bundle {0} completely: {1}"
      },
      {
        "s": "无法完整读取旧资源包 {0}：{1}",
        "t": "Couldn't read the legacy asset bundle {0} completely: {1}"
      },
      {
        "s": "无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。{0}",
        "t": "FFmpeg is unavailable. Put ffmpeg.exe in a tools folder next to the app, or set ASTRAL_FFMPEG_PATH. {0}"
      },
      {
        "s": "无法读取 bundle 内的资源文件：{0}",
        "t": "Couldn't read the asset files inside the bundle: {0}"
      },
      {
        "s": "无法读取视频首帧：{0}",
        "t": "Couldn't read the video's first frame: {0}"
      },
      {
        "s": "更新失败：{0}",
        "t": "Update failed: {0}"
      },
      {
        "s": "替换卡面失败：{0}",
        "t": "Failed to replace card art: {0}"
      },
      {
        "s": "未找到 mod：{0}",
        "t": "Mod not found: {0}"
      },
      {
        "s": "未知动画附属 MonoBehaviour，不能部分导入：{0}",
        "t": "Unknown animation-attached MonoBehaviour; can't import partially: {0}"
      },
      {
        "s": "未知图片槽位: {0}",
        "t": "Unknown image slot: {0}"
      },
      {
        "s": "未知视频实现：{0}",
        "t": "Unknown video implementation: {0}"
      },
      {
        "s": "来源皮肤 {0} 的图片或动画未完整定位，请先在游戏中查看该皮肤以下载资源",
        "t": "The images or animations for source skin {0} couldn't be fully located; view that skin in the game first to download its assets"
      },
      {
        "s": "模组包比游戏目录里已装的更旧：{0}。确认要降级后再导入（玩家配置与停用状态仍会保留）。",
        "t": "The mod pack is older than what's installed in the game folder: {0}. Import only after confirming the downgrade (player config and disabled state are kept)."
      },
      {
        "s": "正在深度扫描 {0} …",
        "t": "Deep scanning {0} …"
      },
      {
        "s": "正在转换：{0} / {1} 帧上限",
        "t": "Converting: {0} / {1} frames max"
      },
      {
        "s": "此前累计刷新了 {0} 次后才确认选择",
        "t": "Confirmed after {0} total rerolls"
      },
      {
        "s": "注意：现在游戏目录里有 {0} 局，超过游戏上限 {1}，",
        "t": "Note: the game folder now has {0} matches, above the game's limit of {1},"
      },
      {
        "s": "深度扫描出错：{0}",
        "t": "Deep scan error: {0}"
      },
      {
        "s": "游戏目录不存在：{0}",
        "t": "Game folder doesn't exist: {0}"
      },
      {
        "s": "游戏目录里只有 {0} 局，未超过保留名额 {1}，无需整理。",
        "t": "The game folder only has {0} matches, within the retention limit of {1}; no tidy-up needed."
      },
      {
        "s": "游戏目录里已装的加载器（{0}）比本程序内置的（{1}）更新 —— 这是一次降级，已阻止。",
        "t": "The loader installed in the game folder ({0}) is newer than the bundled one ({1}) — this is a downgrade and was blocked."
      },
      {
        "s": "生成 GIF：{0} / {1} 帧",
        "t": "Generating GIF: {0} / {1} frames"
      },
      {
        "s": "皮肤不属于当前角色: {0}",
        "t": "The skin doesn't belong to the current character: {0}"
      },
      {
        "s": "皮肤方案不存在: {0}",
        "t": "Skin profile doesn't exist: {0}"
      },
      {
        "s": "目标方案已存在: {0}",
        "t": "The target profile already exists: {0}"
      },
      {
        "s": "移除动态卡面失败：{0}",
        "t": "Failed to remove animated card art: {0}"
      },
      {
        "s": "第 {0} 档",
        "t": "Preset {0}"
      },
      {
        "s": "第 {0} 次刷新",
        "t": "Reroll #{0}"
      },
      {
        "s": "第{0}格已售出",
        "t": "Slot {0} sold"
      },
      {
        "s": "精灵名称无效或重复：{0}",
        "t": "Invalid or duplicate sprite name: {0}"
      },
      {
        "s": "纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：{0}",
        "t": "Texture size exceeds Unity's limit (longest side 16384) or the decoded data is incomplete: {0}"
      },
      {
        "s": "纹理资源名为空或重复：{0}",
        "t": "Texture asset name is empty or duplicated: {0}"
      },
      {
        "s": "自定义包被皮肤方案引用，请先移除方案规则: {0}",
        "t": "The custom pack is referenced by a skin profile; remove the profile rule first: {0}"
      },
      {
        "s": "自定义包角色与规则不匹配: {0}",
        "t": "The custom pack's character doesn't match the rule: {0}"
      },
      {
        "s": "获取卡面失败：{0}",
        "t": "Failed to get card art: {0}"
      },
      {
        "s": "规则标识重复: {0}",
        "t": "Duplicate rule ID: {0}"
      },
      {
        "s": "视频资源引用结构无效：{0}",
        "t": "Invalid video asset reference structure: {0}"
      },
      {
        "s": "视频转换失败：{0}",
        "t": "Video conversion failed: {0}"
      },
      {
        "s": "角色 {0} 中不存在指定来源皮肤 {1}，无法导出；不会回退到默认皮肤",
        "t": "The specified source skin {1} doesn't exist on character {0}, so it can't be exported; it won't fall back to the default skin"
      },
      {
        "s": "角色 {0} 没有可导出的皮肤配置",
        "t": "Character {0} has no skin config to export"
      },
      {
        "s": "角色不存在: {0}",
        "t": "Character doesn't exist: {0}"
      },
      {
        "s": "角色皮肤操作失败：{0}",
        "t": "Character skin operation failed: {0}"
      },
      {
        "s": "设置变速失败：{0}",
        "t": "Failed to set speed: {0}"
      },
      {
        "s": "读取 {0} 个原资源包",
        "t": "Read {0} original asset bundles"
      },
      {
        "s": "读取变速器状态失败：{0}",
        "t": "Failed to read Speedhack status: {0}"
      },
      {
        "s": "读取模组状态失败：{0}",
        "t": "Failed to read mod status: {0}"
      },
      {
        "s": "读取游戏位置失败：{0}",
        "t": "Failed to read game locations: {0}"
      },
      {
        "s": "读取状态失败：{0}",
        "t": "Failed to read status: {0}"
      },
      {
        "s": "读取皮肤方案失败: {0}",
        "t": "Failed to read skin profile: {0}"
      },
      {
        "s": "资源不是支持的卡牌贴图，拒绝部分导入：{0}",
        "t": "The asset isn't a supported card texture; partial import refused: {0}"
      },
      {
        "s": "资源包没有可转换的 Texture2D：{0}",
        "t": "The asset bundle has no convertible Texture2D: {0}"
      },
      {
        "s": "资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：{0}",
        "t": "The assets match multiple characters or skins and can't be identified automatically; use a skin pack with a clear target key art. Matches: {0}"
      },
      {
        "s": "转换卡面 {0} / {1}：{2}",
        "t": "Converting card art {0} / {1}: {2}"
      },
      {
        "s": "转换资源 {0} / {1}：{2}",
        "t": "Converting asset {0} / {1}: {2}"
      },
      {
        "s": "还原卡面失败：{0}",
        "t": "Failed to restore card art: {0}"
      },
      {
        "s": "这个文件有 {0} MB，超过 {1} MB 上限，已中止。",
        "t": "This file is {0} MB, over the {1} MB limit; aborted."
      },
      {
        "s": "选择替换卡面图片 ({0})",
        "t": "Select replacement card art image ({0})"
      },
      {
        "s": "配置保存在「文档\\{0}」（和回放库同一个文件夹）：主配置、游戏目录记忆、回放库设置都在这里。",
        "t": "Config is saved in “Documents\\{0}” (the same folder as the replay library): the main config, remembered game folders, and replay library settings all live here."
      },
      {
        "s": "配置已保存并同步到游戏目录；下次进入游戏自动使用 {0} 倍速。",
        "t": "Config saved and synced to the game folder; the next time you enter the game it runs at {0}× speed automatically."
      },
      {
        "s": "（内置：{0}）",
        "t": "(bundled: {0})"
      },
      {
        "s": "；缺失：{0}",
        "t": "; missing: {0}"
      }
    ],
    "ja": [
      {
        "s": "：不用打开游戏，也不占游戏内的 10 个席位。",
        "t": "：ゲームを起動する必要がなく、ゲーム内の10枠も消費しません。"
      },
      {
        "s": "：已装",
        "t": "：インストール済み"
      },
      {
        "s": "。 确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。",
        "t": "。 このまま戻す場合は下のボタンを押してください。保管庫内のコピーはどのみち保持されます。"
      },
      {
        "s": "· 提交 {0}",
        "t": "· コミット {0}"
      },
      {
        "s": "（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；",
        "t": "（Mod の DLL と同じディレクトリにあり、Mod の SdkConfig が読み書きします。ゲーム再起動後に反映）。チェックボックス = オン/オフ、数値欄 = 数値、テキスト欄 = 文字列；"
      },
      {
        "s": ") 恢复为应用内置的原始卡面吗？",
        "t": "）アプリ内蔵の元のカードイラストに戻しますか？"
      },
      {
        "s": "{0} · 编辑此人物的自定义素材{1}",
        "t": "{0} · このキャラクターのカスタム素材を編集{1}"
      },
      {
        "s": "{0} · 等级 {1}",
        "t": "{0} · レベル {1}"
      },
      {
        "s": "{0} · 第{1}回合：{2}",
        "t": "{0} · 第{1}ラウンド：{2}"
      },
      {
        "s": "{0} · 累计转账 {1} 星币",
        "t": "{0} · 累計送金 {1} スターコイン"
      },
      {
        "s": "{0} ({1} 条)",
        "t": "{0} ({1} 件)"
      },
      {
        "s": "{0} / {1} 条",
        "t": "{0} / {1} 件"
      },
      {
        "s": "{0} 安装加载器",
        "t": "{0} ローダーをインストール"
      },
      {
        "s": "{0} 次",
        "t": "{0} 回"
      },
      {
        "s": "{0} 次掷骰 · {1} 颗骰子 · 平均 {2} 点 · 受控 {3} 次",
        "t": "{0} 回サイコロ · {1} 個 · 平均 {2} ポイント · 操作 {3} 回"
      },
      {
        "s": "{0} 从 GitHub 更新",
        "t": "{0} GitHub から更新"
      },
      {
        "s": "{0} 第 {1} 帧预览",
        "t": "{0} 第 {1} フレームのプレビュー"
      },
      {
        "s": "{0} 点",
        "t": "{0} ポイント"
      },
      {
        "s": "{0} 个进行中",
        "t": "{0} 件進行中"
      },
      {
        "s": "{0} 个人物 · {1}",
        "t": "{0} キャラクター · {1}"
      },
      {
        "s": "{0} 个位置",
        "t": "{0} 箇所"
      },
      {
        "s": "{0} 工具箱",
        "t": "{0} ツールボックス"
      },
      {
        "s": "{0} 加载器",
        "t": "{0} ローダー"
      },
      {
        "s": "{0} 结构可疑",
        "t": "{0} 構造に疑義あり"
      },
      {
        "s": "{0} 局 · {1}",
        "t": "{0} 対局 · {1}"
      },
      {
        "s": "{0} 决胜",
        "t": "{0} 決勝"
      },
      {
        "s": "{0} 可操作游戏",
        "t": "{0} ゲーム操作可能"
      },
      {
        "s": "{0} 拉取最新",
        "t": "{0} 最新を取得"
      },
      {
        "s": "{0} 目录已失效",
        "t": "{0} ディレクトリは無効です"
      },
      {
        "s": "{0} 星币",
        "t": "{0} スターコイン"
      },
      {
        "s": "{0} 张卡面",
        "t": "カードイラスト {0} 枚"
      },
      {
        "s": "{0} 帧",
        "t": "{0} フレーム"
      },
      {
        "s": "{0} 重新安装内置版本",
        "t": "{0} 内蔵バージョンを再インストール"
      },
      {
        "s": "{0}{1}曲线",
        "t": "{0}{1} 曲線"
      },
      {
        "s": "{0}处理进度",
        "t": "{0} の処理進捗"
      },
      {
        "s": "{0}的皮肤",
        "t": "{0} のスキン"
      },
      {
        "s": "{0}预览",
        "t": "{0} プレビュー"
      },
      {
        "s": "→ 包内",
        "t": "→ パッケージ内"
      },
      {
        "s": "＋ 按键",
        "t": "＋ キー"
      },
      {
        "s": "= 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。",
        "t": "= クリックしてから割り当てるキーを押します（キーボードの任意のキー、またはマウスの左/右/中ボタン・サイドボタン。Esc でキャンセル）。"
      },
      {
        "s": "安装：{0}",
        "t": "インストール：{0}"
      },
      {
        "s": "按键按钮",
        "t": "キーボタン"
      },
      {
        "s": "按资源比例裁剪，或保留完整图片",
        "t": "元画像の比率でトリミング、または画像全体を保持"
      },
      {
        "s": "保存布局与动作参数",
        "t": "レイアウトとアクションパラメータを保存"
      },
      {
        "s": "保存裁剪",
        "t": "トリミングを保存"
      },
      {
        "s": "保存尺寸",
        "t": "サイズを保存"
      },
      {
        "s": "保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。",
        "t": "保存後にゲームを再起動すると反映されます。通常は「ローダーを有効化」または「起動時の基本倍速」を変更するだけで十分です。"
      },
      {
        "s": "保存配置",
        "t": "設定を保存"
      },
      {
        "s": "保存设置",
        "t": "設定を保存"
      },
      {
        "s": "保存为透明 PNG，尺寸",
        "t": "透過 PNG で保存、サイズ"
      },
      {
        "s": "保存文案",
        "t": "テキストを保存"
      },
      {
        "s": "保存信息",
        "t": "情報を保存"
      },
      {
        "s": "倍速",
        "t": "倍速"
      },
      {
        "s": "本地构建{0}",
        "t": "ローカルビルド{0}"
      },
      {
        "s": "本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，",
        "t": "このツールではそれらのリザルトフレームを読み取れません（ツール内蔵のプロトコルバージョンがゲームより古い可能性があります）。ゲームでも読み取れない場合は、"
      },
      {
        "s": "本回合转账",
        "t": "今ラウンドの送金"
      },
      {
        "s": "本局地图",
        "t": "この対局のマップ"
      },
      {
        "s": "比内置的 v{0} 更新（从 GitHub 更新过）",
        "t": "内蔵の v{0} より新しい（GitHub から更新済み）"
      },
      {
        "s": "比内置的 v{0} 旧 —— 点「重新安装内置版本」可装回内置版",
        "t": "内蔵の v{0} より古い —— 「内蔵バージョンを再インストール」で内蔵版に戻せます"
      },
      {
        "s": "编辑人物皮肤",
        "t": "キャラクタースキンを編集"
      },
      {
        "s": "播放预览",
        "t": "プレビューを再生"
      },
      {
        "s": "不裁剪，等比缩放并透明补边",
        "t": "トリミングせず、比率を保って拡大縮小し、透明で余白を埋める"
      },
      {
        "s": "不添加自定义覆盖",
        "t": "カスタム上書きを追加しない"
      },
      {
        "s": "裁剪结果预览",
        "t": "トリミング結果のプレビュー"
      },
      {
        "s": "裁剪为目标比例",
        "t": "目標比率にトリミング"
      },
      {
        "s": "裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置",
        "t": "トリミング範囲：ドラッグで移動、四隅をドラッグでサイズ変更、方向キーで位置を微調整"
      },
      {
        "s": "裁剪预览",
        "t": "トリミングプレビュー"
      },
      {
        "s": "参战玩家 ({0} 名)",
        "t": "参加プレイヤー ({0} 名)"
      },
      {
        "s": "查看详情",
        "t": "詳細を表示"
      },
      {
        "s": "承受伤害",
        "t": "被ダメージ"
      },
      {
        "s": "筹码",
        "t": "レリック"
      },
      {
        "s": "筹码记录共 {0} 条",
        "t": "レリック記録は合計 {0} 件"
      },
      {
        "s": "出牌",
        "t": "カードプレイ"
      },
      {
        "s": "处理方式",
        "t": "処理方法"
      },
      {
        "s": "创建自定义覆盖",
        "t": "カスタム上書きを作成"
      },
      {
        "s": "此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信",
        "t": "この Mod はゲームを操作できると宣言しています（カードプレイ / サイコロ / 移動などのシミュレーション）。権限の仕組みは廃止されたため、ここでは注意のみ表示します：信頼できる提供元か確認してください"
      },
      {
        "s": "次",
        "t": "回"
      },
      {
        "s": "次 · 平均每回合",
        "t": "回 · 1ラウンド平均"
      },
      {
        "s": "从方案移除此人物",
        "t": "構成からこのキャラクターを削除"
      },
      {
        "s": "从列表移除（不会删除游戏文件）",
        "t": "リストから削除（ゲームファイルは削除されません）"
      },
      {
        "s": "从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。",
        "t": "アプリ内蔵の既定パッケージから新しい構成を作成します。ディスク上で変更済みの画像は使用しません。ネット接続不要で、作成後にカードイラストを1枚ずつ差し替えられます。"
      },
      {
        "s": "错误：{0}",
        "t": "エラー：{0}"
      },
      {
        "s": "打开",
        "t": "開く"
      },
      {
        "s": "打开方案目录",
        "t": "構成フォルダーを開く"
      },
      {
        "s": "打开目录",
        "t": "フォルダーを開く"
      },
      {
        "s": "打开目录修复",
        "t": "フォルダーを開いて修復"
      },
      {
        "s": "大小:",
        "t": "サイズ:"
      },
      {
        "s": "单次播放",
        "t": "1回再生"
      },
      {
        "s": "当前 {0}x（重启游戏生效）",
        "t": "現在 {0}x（ゲーム再起動後に反映）"
      },
      {
        "s": "当前星币",
        "t": "現在のスターコイン"
      },
      {
        "s": "档位 {0}",
        "t": "プリセット {0}"
      },
      {
        "s": "档位 {0}{1}",
        "t": "プリセット {0}{1}"
      },
      {
        "s": "导出 AI 文本 (TXT)",
        "t": "AI 用テキストを出力 (TXT)"
      },
      {
        "s": "导出 JSON",
        "t": "JSON を出力"
      },
      {
        "s": "导出 ZIP",
        "t": "ZIP を出力"
      },
      {
        "s": "导出表格 (CSV)",
        "t": "表を出力 (CSV)"
      },
      {
        "s": "导出当前动作 GIF",
        "t": "現在のアクションを GIF で出力"
      },
      {
        "s": "导出方案 ZIP",
        "t": "構成 ZIP を出力"
      },
      {
        "s": "导入方案 ZIP",
        "t": "構成 ZIP をインポート"
      },
      {
        "s": "导入人物包",
        "t": "キャラクターパックをインポート"
      },
      {
        "s": "导入视频 / GIF",
        "t": "動画 / GIF をインポート"
      },
      {
        "s": "导入与管理",
        "t": "インポートと管理"
      },
      {
        "s": "导入帧序列",
        "t": "フレームシーケンスをインポート"
      },
      {
        "s": "地图人物布局",
        "t": "マップ上のキャラクター配置"
      },
      {
        "s": "第 {0} / {1} 帧 · {2} ms",
        "t": "第 {0} / {1} フレーム · {2} ms"
      },
      {
        "s": "第 {0} 回合",
        "t": "第 {0} ラウンド"
      },
      {
        "s": "第 {0} 回合 · {1}",
        "t": "第 {0} ラウンド · {1}"
      },
      {
        "s": "第 {0} 帧",
        "t": "第 {0} フレーム"
      },
      {
        "s": "点按切换（不勾选 = 按住才生效）",
        "t": "クリックで切り替え（チェックなし = 長押しでのみ有効）"
      },
      {
        "s": "点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。",
        "t": "クリックで選択/解除し、確定するとそのプリセットに適用されます。プリセットのキーは複数選択できます（例：Ctrl + Shift）。"
      },
      {
        "s": "点一下，再按下要绑的键（支持鼠标左/右/中键与侧键；Esc 取消）",
        "t": "クリックしてから、割り当てるキーを押します（マウスの左/右/中ボタンとサイドボタンに対応。Esc でキャンセル）"
      },
      {
        "s": "动画帧预览",
        "t": "アニメーションフレームプレビュー"
      },
      {
        "s": "动画逐帧图片",
        "t": "アニメーションのフレーム画像"
      },
      {
        "s": "动态卡面 · {0} 帧/秒 · {1} 秒循环",
        "t": "動くカードイラスト · {0} フレーム/秒 · {1} 秒ループ"
      },
      {
        "s": "动作",
        "t": "アクション"
      },
      {
        "s": "读不到加载器更新日志（内置为空，联网获取也没成功）。点「{0} 拉取最新」重试。",
        "t": "ローダーの更新履歴を読み取れません（内蔵は空で、オンライン取得にも失敗しました）。「{0} 最新を取得」を押して再試行してください。"
      },
      {
        "s": "对局表现分析",
        "t": "対局パフォーマンス分析"
      },
      {
        "s": "对局结果",
        "t": "対局結果"
      },
      {
        "s": "多人物搭配 · {0} 个人物",
        "t": "複数キャラの組み合わせ · {0} キャラクター"
      },
      {
        "s": "二维精灵动画",
        "t": "2D スプライトアニメーション"
      },
      {
        "s": "发现新版本：已装 v{0} → 最新 v{1}，点「更新到 v{2}」。",
        "t": "新しいバージョンがあります：インストール済み v{0} → 最新 v{1}。「v{2} に更新」を押してください。"
      },
      {
        "s": "方案：{0} · {1}",
        "t": "構成：{0} · {1}"
      },
      {
        "s": "方案描述（可选）：",
        "t": "構成の説明（任意）："
      },
      {
        "s": "方案设置",
        "t": "構成設定"
      },
      {
        "s": "方案显示名称：",
        "t": "構成の表示名："
      },
      {
        "s": "方案英文目录名（唯一标识）：",
        "t": "構成の英語フォルダー名（一意の ID）："
      },
      {
        "s": "方案中的人物",
        "t": "構成内のキャラクター"
      },
      {
        "s": "方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。",
        "t": "方向キーで位置を微調整、Shift を押しながらで 10 ピクセルずつ移動します。元の画像は変更されません。"
      },
      {
        "s": "复制版本信息",
        "t": "バージョン情報をコピー"
      },
      {
        "s": "该角色全部原皮肤",
        "t": "このキャラクターのすべての元スキン"
      },
      {
        "s": "该人物全部原皮肤",
        "t": "このキャラクターのすべての元スキン"
      },
      {
        "s": "该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。",
        "t": "このプレイヤーの今ラウンドのスナップショット区間内における送金増分です。同じラウンドで複数回来店しても同じ値が表示され、重複して加算されません。"
      },
      {
        "s": "高级：编辑完整清单",
        "t": "詳細：マニフェスト全体を編集"
      },
      {
        "s": "高级：用系统默认编辑器直接改 JSON",
        "t": "詳細：既定のエディターで JSON を直接編集"
      },
      {
        "s": "高级编辑…",
        "t": "詳細編集…"
      },
      {
        "s": "高级选项（Steam 绕过细节 · 引导 · 超时）",
        "t": "詳細オプション（Steam バイパスの詳細 · ブートストラップ · タイムアウト）"
      },
      {
        "s": "更换官方皮肤",
        "t": "公式スキンを変更"
      },
      {
        "s": "更换图片",
        "t": "画像を差し替え"
      },
      {
        "s": "更新到 v{0}",
        "t": "v{0} に更新"
      },
      {
        "s": "攻击 / 防御",
        "t": "攻撃 / 防御"
      },
      {
        "s": "共 {0} 张卡面",
        "t": "カードイラスト 合計 {0} 枚"
      },
      {
        "s": "共筛选出 {0} 条事件记录",
        "t": "絞り込み結果：{0} 件のイベント記録"
      },
      {
        "s": "关闭",
        "t": "閉じる"
      },
      {
        "s": "关卡进度",
        "t": "ステージ進行度"
      },
      {
        "s": "官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。",
        "t": "公式素材で画像とキャラクターアニメーションを置き換えます。ボイス、動画、エフェクト、特殊演出はオリジナルのままです。"
      },
      {
        "s": "归档最旧的 {0} 局",
        "t": "古い {0} 対局を保管"
      },
      {
        "s": "还没有配置文件（mods\\{0}\\config.json）。保存后将创建。",
        "t": "設定ファイルがまだありません（mods\\{0}\\config.json）。保存すると作成されます。"
      },
      {
        "s": "合计",
        "t": "合計"
      },
      {
        "s": "恢复",
        "t": "復元"
      },
      {
        "s": "恢复此动作",
        "t": "このアクションを復元"
      },
      {
        "s": "恢复内置卡面",
        "t": "内蔵カードイラストに戻す"
      },
      {
        "s": "回放 ID",
        "t": "リプレイ ID"
      },
      {
        "s": "回放服务器",
        "t": "リプレイサーバー"
      },
      {
        "s": "回放服务器（不确定就按当前游戏版本选）",
        "t": "リプレイサーバー（不明な場合は現在のゲームバージョンに合わせて選択）"
      },
      {
        "s": "回放复盘",
        "t": "リプレイ復盤"
      },
      {
        "s": "回放库",
        "t": "リプレイ保管庫"
      },
      {
        "s": "回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。 填上回放 ID 就能直接下载进",
        "t": "リプレイはサーバーが録画して公式リプレイサーバーに置いており、ゲーム内の「リプレイを保存」もそこから取得します。 リプレイ ID を入力すればそのまま保管庫へダウンロードできます。"
      },
      {
        "s": "回合",
        "t": "ラウンド"
      },
      {
        "s": "或其中的卡面图片。",
        "t": "またはその中のカードイラスト。"
      },
      {
        "s": "击杀",
        "t": "キル"
      },
      {
        "s": "及其目录下的所有卡面图片吗？此操作不可撤销。",
        "t": "とそのディレクトリ内のすべてのカードイラストを削除しますか？この操作は元に戻せません。"
      },
      {
        "s": "技能使用",
        "t": "スキル使用"
      },
      {
        "s": "继续导入会用包里的旧版本覆盖现有文件。玩家改过的",
        "t": "インポートを続けると、パッケージ内の旧バージョンで既存のファイルが上書きされます。プレイヤーが変更した"
      },
      {
        "s": "加载器 {0}",
        "t": "ローダー {0}"
      },
      {
        "s": "检查更新失败：{0}",
        "t": "更新の確認に失敗しました：{0}"
      },
      {
        "s": "检查失败：{0}",
        "t": "確認に失敗しました：{0}"
      },
      {
        "s": "简要说明此方案的特色…",
        "t": "この構成の特徴を簡単に説明…"
      },
      {
        "s": "将 {0} 停用并移入皮肤目录中的 .deleted 文件夹。",
        "t": "{0} を無効化してスキンディレクトリ内の .deleted フォルダーへ移動します。"
      },
      {
        "s": "将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。",
        "t": "アプリ内蔵の既定パッケージからこのカードイラストを復元し、現在の構成内の同名画像を上書きします。ディスク上で変更された default は元テンプレートとして使用されません。"
      },
      {
        "s": "角色 {0}",
        "t": "キャラクター {0}"
      },
      {
        "s": "角色文案",
        "t": "キャラクターテキスト"
      },
      {
        "s": "结算奖励",
        "t": "リザルト報酬"
      },
      {
        "s": "结算快照中的完整属性与行动统计，记录本局关键数据指标。",
        "t": "リザルトスナップショット内の完全なステータスと行動統計。この対局の主要指標を記録します。"
      },
      {
        "s": "结算快照中的总伤害、承伤、治疗、转账与行动次数。",
        "t": "リザルトスナップショット内の総与ダメージ、被ダメージ、回復、送金、行動回数。"
      },
      {
        "s": "解析展示失败: {0}",
        "t": "解析・表示に失敗しました: {0}"
      },
      {
        "s": "界面动态效果已{0}",
        "t": "画面のアニメーション効果を{0}"
      },
      {
        "s": "仅替换 {0}",
        "t": "{0} のみ置換"
      },
      {
        "s": "仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。",
        "t": "リプレイで実際に受信した移動用サイコロの結果のみを集計します。複数ダイスの出目は個別に保持し、操作された移動は別途マークし、戦闘用や判定用のダイスは混ぜません。"
      },
      {
        "s": "仅支持英文、数字、下划线及减号",
        "t": "英数字、アンダースコア、ハイフンのみ使用できます"
      },
      {
        "s": "旧配置已保留，但暂未转换：{0}",
        "t": "旧設定は保持されていますが、まだ変換されていません：{0}"
      },
      {
        "s": "居中重置",
        "t": "中央にリセット"
      },
      {
        "s": "卡牌 / 技能",
        "t": "カード / スキル"
      },
      {
        "s": "卡牌编号:",
        "t": "カード番号:"
      },
      {
        "s": "卡牌名称:",
        "t": "カード名:"
      },
      {
        "s": "卡牌使用",
        "t": "カード使用"
      },
      {
        "s": "开启自动托管（保留 {0} 局）",
        "t": "自動アーカイブを有効にする（{0} 対局を保持）"
      },
      {
        "s": "开始下载",
        "t": "ダウンロードを開始"
      },
      {
        "s": "可以更新到 v{0}",
        "t": "v{0} に更新できます"
      },
      {
        "s": "口径",
        "t": "集計基準"
      },
      {
        "s": "来源与替换范围",
        "t": "ソースと置換範囲"
      },
      {
        "s": "来自回合快照中的累计转账；未记录收款对象时不做推断。",
        "t": "ラウンドスナップショットの累計送金から取得。受取相手が記録されていない場合は推測しません。"
      },
      {
        "s": "类别:",
        "t": "カテゴリ:"
      },
      {
        "s": "累计",
        "t": "累計"
      },
      {
        "s": "累计伤害",
        "t": "累計与ダメージ"
      },
      {
        "s": "累计死亡",
        "t": "累計死亡"
      },
      {
        "s": "累计转账",
        "t": "累計送金"
      },
      {
        "s": "立即复制创建",
        "t": "コピーしてすぐ作成"
      },
      {
        "s": "留空使用原版",
        "t": "空欄の場合はオリジナルを使用"
      },
      {
        "s": "锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。",
        "t": "アンカーは 0～1 で指定し、Y はキャンバス下端から上向きに計算します。スケールは PPU に乗算され、ゲーム内のキャラクターの当たり判定や移動は変更しません。"
      },
      {
        "s": "没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）",
        "t": "標準的な Unity ゲーム構造を検出できませんでした（*_Data または UnityPlayer.dll がありません）"
      },
      {
        "s": "没有匹配的方案。",
        "t": "一致する構成がありません。"
      },
      {
        "s": "每回合新增",
        "t": "ラウンドごとの増加"
      },
      {
        "s": "名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。",
        "t": "名前とキャラクター紹介はキャラクター共通のテキストを使用するため、そのキャラクターの他のスキンにも影響する可能性があります。"
      },
      {
        "s": "模组状态与使用说明",
        "t": "Mod の状態と使い方"
      },
      {
        "s": "默认：{0}",
        "t": "既定：{0}"
      },
      {
        "s": "难度 {0} · 最终首领 {1}",
        "t": "難易度 {0} · 最終ボス {1}"
      },
      {
        "s": "内置 SHA-256 {0}…（卸载时按此校验，防止误删他人文件）",
        "t": "内蔵 SHA-256 {0}…（アンインストール時にこれを照合し、他人のファイルを誤って削除しないようにします）"
      },
      {
        "s": "内置文件就绪：version.dll {0}",
        "t": "内蔵ファイルの準備完了：version.dll {0}"
      },
      {
        "s": "您的昵称",
        "t": "あなたのニックネーム"
      },
      {
        "s": "配置 · {0}",
        "t": "設定 · {0}"
      },
      {
        "s": "配置保存在",
        "t": "設定の保存先は"
      },
      {
        "s": "偏移",
        "t": "オフセット"
      },
      {
        "s": "启用 / 禁用（重启游戏后生效）",
        "t": "有効 / 無効（ゲーム再起動後に反映）"
      },
      {
        "s": "清单 JSON 无效：{0}",
        "t": "マニフェスト JSON が無効です：{0}"
      },
      {
        "s": "清空（让 mod 用它自己的默认键）",
        "t": "クリア（Mod 自身の既定キーを使用）"
      },
      {
        "s": "请在左侧点击任意协议帧进行查看解码数据",
        "t": "左側の任意のプロトコルフレームをクリックすると、デコードしたデータを表示します"
      },
      {
        "s": "区服",
        "t": "リージョン"
      },
      {
        "s": "取消",
        "t": "キャンセル"
      },
      {
        "s": "取消下载",
        "t": "ダウンロードをキャンセル"
      },
      {
        "s": "全队转账",
        "t": "チーム全体の送金"
      },
      {
        "s": "确定（已选 {0} 个）",
        "t": "確定（{0} 件選択済み）"
      },
      {
        "s": "确定删除模组「{0}」？",
        "t": "Mod「{0}」を削除しますか？"
      },
      {
        "s": "确定要彻底删除方案",
        "t": "構成を完全に削除しますか："
      },
      {
        "s": "确定要将",
        "t": "次の"
      },
      {
        "s": "确认还原",
        "t": "復元する"
      },
      {
        "s": "确认继续",
        "t": "続行する"
      },
      {
        "s": "确认删除",
        "t": "削除する"
      },
      {
        "s": "人物设置",
        "t": "キャラクター設定"
      },
      {
        "s": "仍然导入",
        "t": "それでもインポート"
      },
      {
        "s": "仍要放回（{0} 份）",
        "t": "それでも戻す（{0} 件）"
      },
      {
        "s": "如 二次元萌化包",
        "t": "例：アニメ風スキンパック"
      },
      {
        "s": "如 my_custom_skin",
        "t": "例：my_custom_skin"
      },
      {
        "s": "筛选筹码、玩家、品质、来源...",
        "t": "レリック、プレイヤー、レアリティ、ソースで絞り込み..."
      },
      {
        "s": "筛选商店类型、玩家、卡牌、购买结果...",
        "t": "ショップ種別、プレイヤー、カード、購入結果で絞り込み..."
      },
      {
        "s": "删除方案",
        "t": "構成を削除"
      },
      {
        "s": "删除该档位",
        "t": "このプリセットを削除"
      },
      {
        "s": "删除回放 {0}",
        "t": "リプレイ {0} を削除"
      },
      {
        "s": "删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。",
        "t": "削除したものはまず Windows のごみ箱に入り、復元できます。ゲーム内のコピーだけを削除するのが枠を空ける一般的な方法で、保管庫内のコピーこそが長期保存用です。"
      },
      {
        "s": "删除模组",
        "t": "Mod を削除"
      },
      {
        "s": "删除皮肤包",
        "t": "スキンパックを削除"
      },
      {
        "s": "删除这个模组",
        "t": "この Mod を削除"
      },
      {
        "s": "伤害",
        "t": "ダメージ"
      },
      {
        "s": "商店进店共 {0} 次（其中 {1} 次有购买）",
        "t": "ショップ来店は合計 {0} 回（うち {1} 回は購入あり）"
      },
      {
        "s": "上次游戏未能应用方案：{0}",
        "t": "前回のゲームで構成を適用できませんでした：{0}"
      },
      {
        "s": "上一帧",
        "t": "前のフレーム"
      },
      {
        "s": "生命",
        "t": "HP"
      },
      {
        "s": "时长",
        "t": "時間"
      },
      {
        "s": "输出伤害",
        "t": "与ダメージ"
      },
      {
        "s": "输出预览",
        "t": "出力プレビュー"
      },
      {
        "s": "输入方案名称",
        "t": "構成名を入力"
      },
      {
        "s": "刷新方案与状态",
        "t": "構成と状態を更新"
      },
      {
        "s": "死亡计数",
        "t": "死亡回数"
      },
      {
        "s": "搜索方案",
        "t": "構成を検索"
      },
      {
        "s": "搜索帧号、CMD、消息名...",
        "t": "フレーム番号、CMD、メッセージ名で検索..."
      },
      {
        "s": "提交 {0}",
        "t": "コミット {0}"
      },
      {
        "s": "替换 {0} 第 {1} 帧",
        "t": "{0} の第 {1} フレームを置換"
      },
      {
        "s": "替换选中帧",
        "t": "選択フレームを置換"
      },
      {
        "s": "添加或修改人物后，完全退出并重启游戏以应用搭配。",
        "t": "キャラクターを追加・変更した後は、ゲームを完全に終了して再起動すると組み合わせが適用されます。"
      },
      {
        "s": "添加人物皮肤",
        "t": "キャラクタースキンを追加"
      },
      {
        "s": "跳转到帧",
        "t": "フレームへ移動"
      },
      {
        "s": "同步配置：{0}",
        "t": "設定を同期：{0}"
      },
      {
        "s": "图片资源",
        "t": "画像リソース"
      },
      {
        "s": "拖动或用方向键移动裁剪区域",
        "t": "ドラッグまたは方向キーでトリミング範囲を移動"
      },
      {
        "s": "拖动选区移动位置，拖动四角调整大小。{0}",
        "t": "選択範囲をドラッグして移動、四隅をドラッグしてサイズ変更します。{0}"
      },
      {
        "s": "拖动选区移动位置，用滑块调整大小。透明背景会保留。",
        "t": "選択範囲をドラッグして移動、スライダーでサイズ変更します。透明な背景は保持されます。"
      },
      {
        "s": "玩家",
        "t": "プレイヤー"
      },
      {
        "s": "玩家 {0}",
        "t": "プレイヤー {0}"
      },
      {
        "s": "玩家数据对照",
        "t": "プレイヤーデータ比較"
      },
      {
        "s": "玩家转账贡献",
        "t": "プレイヤーの送金貢献"
      },
      {
        "s": "文件 {0} 已存在，继续将覆盖它。",
        "t": "ファイル {0} は既に存在します。続行すると上書きされます。"
      },
      {
        "s": "文件名称:",
        "t": "ファイル名:"
      },
      {
        "s": "我的方案",
        "t": "マイ構成"
      },
      {
        "s": "下面这些模组，包里的版本比游戏目录里已装的更旧：",
        "t": "以下の Mod は、パッケージ内のバージョンがゲームディレクトリにインストール済みのものより古いです："
      },
      {
        "s": "下一帧",
        "t": "次のフレーム"
      },
      {
        "s": "显示 {0} / {1} 次商店进店",
        "t": "ショップ来店 {0} / {1} 回を表示"
      },
      {
        "s": "显示 {0} / {1} 条筹码流转",
        "t": "レリックの流れ {0} / {1} 件を表示"
      },
      {
        "s": "显示 {0} / {1} 帧",
        "t": "フレーム {0} / {1} を表示"
      },
      {
        "s": "消息类型",
        "t": "メッセージ種別"
      },
      {
        "s": "校验并保存清单",
        "t": "マニフェストを検証して保存"
      },
      {
        "s": "协议载荷解码详情",
        "t": "プロトコルペイロードのデコード詳細"
      },
      {
        "s": "协议帧共 {0} 帧",
        "t": "プロトコルフレームは合計 {0} フレーム"
      },
      {
        "s": "卸载：{0}",
        "t": "アンインストール：{0}"
      },
      {
        "s": "新建方案",
        "t": "構成を作成"
      },
      {
        "s": "新增击杀",
        "t": "キル増加"
      },
      {
        "s": "新增伤害",
        "t": "ダメージ増加"
      },
      {
        "s": "新增转账",
        "t": "送金増加"
      },
      {
        "s": "星币",
        "t": "スターコイン"
      },
      {
        "s": "星币 · 击杀",
        "t": "スターコイン · キル"
      },
      {
        "s": "修改配置",
        "t": "設定を変更"
      },
      {
        "s": "修改时间: {0}",
        "t": "更新日時: {0}"
      },
      {
        "s": "修改这个模组的配置",
        "t": "この Mod の設定を変更"
      },
      {
        "s": "选区大小",
        "t": "選択範囲のサイズ"
      },
      {
        "s": "选取你的卡面",
        "t": "カードイラストを選択"
      },
      {
        "s": "选择按键组合 · {0}",
        "t": "キーの組み合わせを選択 · {0}"
      },
      {
        "s": "选择第 {0} 帧",
        "t": "第 {0} フレームを選択"
      },
      {
        "s": "选择动画帧",
        "t": "アニメーションフレームを選択"
      },
      {
        "s": "选择图片",
        "t": "画像を選択"
      },
      {
        "s": "循环",
        "t": "ループ"
      },
      {
        "s": "沿用原皮肤",
        "t": "元のスキンを引き継ぐ"
      },
      {
        "s": "依赖：{0}",
        "t": "依存関係：{0}"
      },
      {
        "s": "移除",
        "t": "削除"
      },
      {
        "s": "移除 {0} 的自制帧和原精灵映射，重启游戏后恢复原动作。",
        "t": "{0} の自作フレームと元のスプライトマッピングを削除します。ゲームを再起動すると元のアクションに戻ります。"
      },
      {
        "s": "移除动态覆盖",
        "t": "動的オーバーライドを削除"
      },
      {
        "s": "移动点数",
        "t": "移動ポイント"
      },
      {
        "s": "移动骰子统计",
        "t": "移動サイコロ統計"
      },
      {
        "s": "已安装 v{0} —— 点「检查更新」看看 GitHub 上有没有新版。",
        "t": "v{0} をインストール済み —— 「更新を確認」を押して、GitHub に新しいバージョンがないか見てみましょう。"
      },
      {
        "s": "已安装 v{0}，比随程序内置的 v{1} 更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。",
        "t": "v{0} をインストール済み。アプリ内蔵の v{1} より新しく（このファイルは GitHub から更新しました）—— 「更新を確認」を押して、さらに新しいバージョンがないか確認してください。"
      },
      {
        "s": "已安装 v{0}，比随程序内置的 v{1} 旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。",
        "t": "v{0} をインストール済み。アプリ内蔵の v{1} より古い —— 内蔵版に戻すには「内蔵バージョンを再インストール」、新しい版があるか知りたいときは「更新を確認」を押してください。"
      },
      {
        "s": "已安装 v{0}，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。",
        "t": "v{0} をインストール済み。アプリ内蔵のバージョンと一致しています —— 「更新を確認」を押すと、GitHub に新しいバージョンがあるか確認できます。"
      },
      {
        "s": "已持有 {0} 个筹码：",
        "t": "レリックを {0} 個所持："
      },
      {
        "s": "已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近 {0} 局）。",
        "t": "自動アーカイブを有効にしました：以後このツールを開くたびに、枠を超えた古いリプレイが自動で保管庫へ保管されます（ゲーム内には直近 {0} 対局を保持）。"
      },
      {
        "s": "已设为 {0}",
        "t": "{0} に設定しました"
      },
      {
        "s": "已是最新版本 v{0}。",
        "t": "最新バージョン v{0} です。"
      },
      {
        "s": "已下载 {0}",
        "t": "{0} をダウンロード済み"
      },
      {
        "s": "已下载 {0} / {1}（{2}%）",
        "t": "ダウンロード済み {0} / {1}（{2}%）"
      },
      {
        "s": "已装的 v{0} 比 GitHub 上的 v{1} 更新，无需更新。",
        "t": "インストール済みの v{0} は GitHub 上の v{1} より新しいため、更新は不要です。"
      },
      {
        "s": "用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。",
        "t": "与ダメージ、支援、行動データで対局の流れを比較します。役割はキャラクターごとに異なるため、総与ダメージだけでプレイヤーを評価しません。"
      },
      {
        "s": "游戏名称",
        "t": "ゲーム名"
      },
      {
        "s": "游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；只想清掉就勾选「强制卸载」。",
        "t": "ゲームディレクトリの version.dll はこのアプリ内蔵のものでも、インストールマニフェストに記録されたものでもありません：多くの場合、別のツール（例：「ゲームツール」ページの旧版単体変速機）または第三者が置いたファイルです。CesiumLoader に切り替えるには、下の「ゲームディレクトリ内のこのツールのものではない version.dll の上書きを許可する」にチェックを入れ、「ローダーをインストール」を押します。完全に消したいだけなら「強制アンインストール」にチェックを入れてください。"
      },
      {
        "s": "游戏内回放席位快满了（{0}/{1}）",
        "t": "ゲーム内のリプレイ枠がほぼ満杯です（{0}/{1}）"
      },
      {
        "s": "游戏内回放席位已满（{0}/{1}）",
        "t": "ゲーム内のリプレイ枠が満杯です（{0}/{1}）"
      },
      {
        "s": "游戏在「本地回放」列表里遇到这种目录会把它整个删掉",
        "t": "ゲームは「ローカルリプレイ」一覧でこの種のディレクトリを見つけると丸ごと削除します"
      },
      {
        "s": "游戏中的角色皮肤模组为 {0}，请更新到 {1}，然后重启游戏以使用最新修复。",
        "t": "ゲーム内のキャラクタースキン Mod は {0} です。{1} に更新してから、ゲームを再起動すると最新の修正が適用されます。"
      },
      {
        "s": "有新版本 v{0} 可更新",
        "t": "新しいバージョン v{0} に更新できます"
      },
      {
        "s": "与「已禁用」状态仍会保留， 被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。",
        "t": "「無効」状態も保持され、置き換えられた旧ファイルはごみ箱へ移動します（ごみ箱が使えない場合は元のファイルを保持）。アップグレードだけが目的の場合は、より新しい Mod パッケージを使用してください。"
      },
      {
        "s": "原图 {0} × {1}",
        "t": "元画像 {0} × {1}"
      },
      {
        "s": "在资源管理器中打开",
        "t": "エクスプローラーで開く"
      },
      {
        "s": "载荷字节",
        "t": "ペイロードバイト"
      },
      {
        "s": "战绩里那一长串数字",
        "t": "戦績にある長い数字の並び"
      },
      {
        "s": "找到 {0} 个候选：",
        "t": "{0} 件の候補が見つかりました："
      },
      {
        "s": "这个目录已经不在了",
        "t": "このディレクトリは既に存在しません"
      },
      {
        "s": "帧",
        "t": "フレーム"
      },
      {
        "s": "帧号: {0} · {1} CMD: {2} · {3} 载荷大小: {4} 字节 {5}",
        "t": "フレーム番号: {0} · {1} CMD: {2} · {3} ペイロードサイズ: {4} バイト {5}"
      },
      {
        "s": "正在读取…",
        "t": "読み取り中…"
      },
      {
        "s": "支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。",
        "t": "元のスプライト名マッピング SpriteMappings とカスタムリソース URL asset: に対応。メディアファイルはパッケージに保存できますが、現在は適用されません。"
      },
      {
        "s": "只修改方案对外展示的信息，不会改变方案目录名",
        "t": "構成の表示情報のみを変更します。構成のディレクトリ名は変わりません"
      },
      {
        "s": "指标",
        "t": "指標"
      },
      {
        "s": "治疗",
        "t": "回復"
      },
      {
        "s": "逐回合数据表",
        "t": "ラウンド別データ表"
      },
      {
        "s": "主配置：{0}{1}",
        "t": "メイン設定：{0}{1}"
      },
      {
        "s": "转出",
        "t": "送金"
      },
      {
        "s": "转换并保存",
        "t": "変換して保存"
      },
      {
        "s": "转账",
        "t": "送金"
      },
      {
        "s": "转账贡献",
        "t": "送金貢献"
      },
      {
        "s": "转账星币",
        "t": "送金スターコイン"
      },
      {
        "s": "自动保持游戏内最近 {0} 局",
        "t": "ゲーム内の直近 {0} 対局を自動保持"
      },
      {
        "s": "自动托管已{0}",
        "t": "自動アーカイブを{0}"
      },
      {
        "s": "自动托管已开启（保留 {0} 局）",
        "t": "自動アーカイブが有効です（{0} 対局を保持）"
      },
      {
        "s": "总承伤",
        "t": "総被ダメージ"
      },
      {
        "s": "总伤害",
        "t": "総与ダメージ"
      },
      {
        "s": "最近一次 Mod 记录：{0} · {1} · 模组 {2}{3}。修改配置后，重启游戏并刷新可查看新结果。",
        "t": "直近の Mod 記録：{0} · {1} · Mod {2}{3}。設定を変更した後、ゲームを再起動して更新すると新しい結果を確認できます。"
      },
      {
        "s": "作者: {0}",
        "t": "作者: {0}"
      },
      {
        "s": "作者：{0}",
        "t": "作者：{0}"
      },
      {
        "s": "作者签名（可选）：",
        "t": "作者の署名（任意）："
      },
      {
        "s": "PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。",
        "t": "PNG/JPG フレームはファイル名順に並びます。待機 Idle、前向き Walk、後ろ向き Walk-back。その他のアクション名は元のキャラクターに準じます。"
      },
      {
        "s": "Unity 结构 {0}",
        "t": "Unity 構造 {0}"
      },
      {
        "s": "version.dll 已停用或缺失，保留版本 {0}。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。",
        "t": "version.dll は無効化済みまたは欠落しています。バージョン {0} を保持します。「version.dll を復元」を押してください。ローカルにファイルがない場合は、対応するバージョンのリリースパッケージをダウンロードします。"
      },
      {
        "s": "- 初始候选池: {0}",
        "t": "- 初期候補プール: {0}"
      },
      {
        "s": "- 刷新后呈现的新候选池: {0}",
        "t": "- リロール後に表示される新しい候補プール: {0}"
      },
      {
        "s": "- 刷新情况: {0}",
        "t": "- リロール状況: {0}"
      },
      {
        "s": "- 动作类型: 【刷新候选】(第 {0} 次刷新)",
        "t": "- アクション種別: 【候補リロール】（{0} 回目のリロール）"
      },
      {
        "s": "- 参战角色: {0} (英雄ID: {1} · 账号等级: {2})",
        "t": "- 参戦キャラクター: {0}（ヒーローID: {1} · アカウントレベル: {2}）"
      },
      {
        "s": "- 回合统计: 共 {0} 回合 (包含 {1} 个行动轮次, 帧数 {2})",
        "t": "- ラウンド統計: 全 {0} ラウンド（{1} 回の行動ターンを含む、フレーム数 {2}）"
      },
      {
        "s": "- 回放文件: {0}",
        "t": "- リプレイファイル: {0}"
      },
      {
        "s": "- 对局地图: {0} (地图ID: {1})",
        "t": "- 対局マップ: {0}（マップID: {1}）"
      },
      {
        "s": "- 对局时间: {0} 至 {1} (时长: {2})",
        "t": "- 対局時間: {0} ～ {1}（所要時間: {2}）"
      },
      {
        "s": "- 对局结果: {0} (最终决胜: {1})",
        "t": "- 対局結果: {0}（最終決着: {1}）"
      },
      {
        "s": "- 对局进程: {0}",
        "t": "- 対局進行: {0}"
      },
      {
        "s": "- 最终持有筹码清单: {0}",
        "t": "- 最終所持レリック一覧: {0}"
      },
      {
        "s": "- 最终状态: {0}",
        "t": "- 最終状態: {0}"
      },
      {
        "s": "- 来源途径: {0} ({1})",
        "t": "- 入手経路: {0}（{1}）"
      },
      {
        "s": "- 棋盘移动: {0} 点 | 卡牌使用: {1} 张 | 技能释放: {2} 次",
        "t": "- ボード移動: {0} マス | カード使用: {1} 枚 | スキル発動: {2} 回"
      },
      {
        "s": "- 模式/难度: {0} / {1}",
        "t": "- モード/難易度: {0} / {1}"
      },
      {
        "s": "- 游戏版本: {0}",
        "t": "- ゲームバージョン: {0}"
      },
      {
        "s": "- 玩家刷新候选次数: {0} 次",
        "t": "- プレイヤーの候補リロール回数: {0} 回"
      },
      {
        "s": "- 玩家阵亡次数: {0} 次",
        "t": "- プレイヤーの死亡回数: {0} 回"
      },
      {
        "s": "- 生命值: {0} | 拥有星币: {1} | 攻防属性: 攻击 {2} / 防御 {3}",
        "t": "- HP: {0} | 所持スターコイン: {1} | 攻防ステータス: 攻撃 {2} / 防御 {3}"
      },
      {
        "s": "- 筹码地块购买次数: {0} 次 | 最终持有筹码数: {1} 个",
        "t": "- レリックマスの購入回数: {0} 回 | 最終所持レリック数: {1} 個"
      },
      {
        "s": "- 筹码来源: {0}",
        "t": "- レリックの入手元: {0}"
      },
      {
        "s": "- 筹码流转记录总数: {0} 条 (最终选定 {1} 次，出现候选 {2} 次)",
        "t": "- レリックの流れの記録総数: {0} 件（最終選択 {1} 回、候補出現 {2} 回）"
      },
      {
        "s": "- 获得筹码: 【{0}】 (品质: {1} · ID: {2} · {3})",
        "t": "- 獲得レリック: 【{0}】（レアリティ: {1} · ID: {2} · {3}）"
      },
      {
        "s": "- 输出伤害: {0} | 承受伤害: {1} | 击杀数: {2} | 治疗量: {3}",
        "t": "- 与ダメージ: {0} | 被ダメージ: {1} | キル数: {2} | 回復量: {3}"
      },
      {
        "s": "- 首领(BOSS): {0}",
        "t": "- ボス: {0}"
      },
      {
        "s": "3. 【关键胜负手复盘】根据获胜者（{0}）与其他玩家的数据，分析获胜者主要是依靠哪几个核心筹码建立优势并最终斩获胜利的？",
        "t": "3. 【勝敗を分けたポイントの復盤】勝者（{0}）と他のプレイヤーのデータから、勝者が主にどの中核レリックで優位を築き、勝利につなげたのかを分析してください。"
      },
      {
        "s": "CDN 上没有这个回放（HTTP 404）——ID 可能写错了，或者这局不在{0}。",
        "t": "CDN にこのリプレイはありません（HTTP 404）—— ID が間違っているか、この対局が{0}にない可能性があります。"
      },
      {
        "s": "GIF 已保存：{0}",
        "t": "GIF を保存しました：{0}"
      },
      {
        "s": "ID {0} · 槽位 {1} · 账号等级 {2}",
        "t": "ID {0} · スロット {1} · アカウントレベル {2}"
      },
      {
        "s": "Steam 不可用（{0}），已直接启动游戏主程序。",
        "t": "Steam を利用できないため（{0}）、ゲーム本体を直接起動しました。"
      },
      {
        "s": "UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：{0}",
        "t": "UnityFS のサイズ宣言が無効か、2 GiB を超えてメモリに読み込めません：{0}"
      },
      {
        "s": "UnityFS 头不完整：{0}",
        "t": "UnityFS ヘッダーが不完全です：{0}"
      },
      {
        "s": "UnityFS 存在无法解释的尾部数据，拒绝修复：{0}",
        "t": "UnityFS に解釈できない末尾データがあるため、修復を拒否しました：{0}"
      },
      {
        "s": "UnityFS 版本头无效：{0}",
        "t": "UnityFS のバージョンヘッダーが無効です：{0}"
      },
      {
        "s": "ZIP 存在重复路径：{0}",
        "t": "ZIP に重複したパスがあります：{0}"
      },
      {
        "s": "ZIP 文件与目录路径冲突：{0}",
        "t": "ZIP のファイルとディレクトリのパスが競合しています：{0}"
      },
      {
        "s": "ZIP 文件长度不完整：{0}",
        "t": "ZIP のファイル長が不完全です：{0}"
      },
      {
        "s": "ZIP 路径无效：{0}",
        "t": "ZIP のパスが無効です：{0}"
      },
      {
        "s": "[事件 {0} | 帧 {1}] 玩家「{2}」（角色：{3}）",
        "t": "[イベント {0} | フレーム {1}] プレイヤー「{2}」（キャラクター: {3}）"
      },
      {
        "s": "mod 不存在：{0}",
        "t": "Mod が存在しません：{0}"
      },
      {
        "s": "mod 包包含不安全的路径：{0}",
        "t": "Mod パッケージに安全でないパスが含まれています：{0}"
      },
      {
        "s": "mod 包包含重复文件：{0}",
        "t": "Mod パッケージに重複したファイルがあります：{0}"
      },
      {
        "s": "mod 包包含非法的文件名：{0}",
        "t": "Mod パッケージに不正なファイル名があります：{0}"
      },
      {
        "s": "mod 包里有两个同名模组（{0}），已停止导入。",
        "t": "Mod パッケージ内に同名の Mod（{0}）が2つあるため、インポートを中止しました。"
      },
      {
        "s": "mod 包里的 {0}.dll 没有成功写入，已停止导入。",
        "t": "Mod パッケージ内の {0}.dll を書き込めなかったため、インポートを中止しました。"
      },
      {
        "s": "version.dll 已删除，但 speedhack_config.json 删除失败：{0}",
        "t": "version.dll は削除しましたが、speedhack_config.json の削除に失敗しました：{0}"
      },
      {
        "s": "version.dll 已删除，但 {0} 目录删除失败：{1}（可能仍有文件被占用）。",
        "t": "version.dll は削除しましたが、{0} ディレクトリの削除に失敗しました：{1}（ファイルが使用中の可能性があります）。"
      },
      {
        "s": "{0} {1} 个",
        "t": "{0} {1} 個"
      },
      {
        "s": "{0} {1} 次",
        "t": "{0} {1} 回"
      },
      {
        "s": "{0} · 卡面转换导入",
        "t": "{0} · カードイラスト変換インポート"
      },
      {
        "s": "{0} · 服务器 {1}",
        "t": "{0} · サーバー {1}"
      },
      {
        "s": "{0} · 转换导入",
        "t": "{0} · 変換インポート"
      },
      {
        "s": "{0} 个旧版本文件没能移入回收站，已保留",
        "t": "{0} 個の旧バージョンファイルをごみ箱へ移動できなかったため、そのまま保持しました"
      },
      {
        "s": "{0} 仍为禁用状态",
        "t": "{0} は無効のままです"
      },
      {
        "s": "{0} 元数据读取失败：{1}",
        "t": "{0} のメタデータを読み取れませんでした：{1}"
      },
      {
        "s": "{0} 已装 v{1} → 包内 v{2}",
        "t": "{0} インストール済み v{1} → パッケージ内 v{2}"
      },
      {
        "s": "{0} 帧 · {1} 种命令",
        "t": "{0} フレーム · {1} 種類のコマンド"
      },
      {
        "s": "{0}{1}。可将此皮肤包添加到人物方案。{2}{3}",
        "t": "{0}{1}。このスキンパックをキャラクター構成に追加できます。{2}{3}"
      },
      {
        "s": "{0}{1}，并在控制台窗口显示日志。",
        "t": "{0}{1}。コンソールウィンドウにログを表示します。"
      },
      {
        "s": "{0}如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。",
        "t": "{0}ゲームでも読み取れない場合は、ゲームディレクトリに戻さないでください —— ゲームが一覧で検出するとディレクトリごと削除します。"
      },
      {
        "s": "{0}如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。",
        "t": "{0}ゲームでも読み取れない場合、このリプレイを一覧で検出したときにディレクトリごと削除します。戻す場合はもう一度確認してください。"
      },
      {
        "s": "{0}如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。",
        "t": "{0}ダウングレードする場合は「古いローダーのインストール（ダウングレード）を許可する」にチェックを入れてください。通常は新しいバージョンの AstralParty.Toys を使用する方が適切です。"
      },
      {
        "s": "{0}已按请求的 ID 入库，需要的话可以改名后再放回游戏。",
        "t": "{0}要求された ID で保管庫に保存しました。必要なら名前を変更してからゲームに戻せます。"
      },
      {
        "s": "{0}确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。",
        "t": "{0}CDN 上のこのファイルに差し替えるには、先に保管庫内の古いコピーを削除してください（または別の ID を使用してください）。"
      },
      {
        "s": "{0}继续保存新回放会失败。播放不受影响，建议归档几局。",
        "t": "{0}このまま新しいリプレイを保存すると失敗します。再生には影響しません。数対局を保管することをおすすめします。"
      },
      {
        "s": "{0}请确认回放 ID 与区服是否对应。",
        "t": "{0}リプレイ ID とリージョンが対応しているか確認してください。"
      },
      {
        "s": "{0}（v{1}，版本未变）",
        "t": "{0}（v{1}、バージョン変更なし）"
      },
      {
        "s": "{0}（升级 v{1} → v{2}）",
        "t": "{0}（アップグレード v{1} → v{2}）"
      },
      {
        "s": "{0}（折后 {1}）",
        "t": "{0}（割引後 {1}）"
      },
      {
        "s": "{0}（新装 v{1}）",
        "t": "{0}（新規インストール v{1}）"
      },
      {
        "s": "{0}（版本未知）",
        "t": "{0}（バージョン不明）"
      },
      {
        "s": "{0}（降级 v{1} → v{2}）",
        "t": "{0}（ダウングレード v{1} → v{2}）"
      },
      {
        "s": "{0}：下载到的 {1} 字节不是一个回放帧流（CDN 可能返回了错误页）。",
        "t": "{0}：ダウンロードした {1} バイトはリプレイのフレームストリームではありません（CDN がエラーページを返した可能性があります）。"
      },
      {
        "s": "{0}：下载失败（{1}）。",
        "t": "{0}：ダウンロードに失敗しました（{1}）。"
      },
      {
        "s": "{0}：下载已取消，没有写入回放库。",
        "t": "{0}：ダウンロードをキャンセルしました。リプレイ保管庫には書き込んでいません。"
      },
      {
        "s": "{0}：从{1}下载到的是空文件，已放弃。",
        "t": "{0}：{1}からダウンロードしたファイルが空のため、中止しました。"
      },
      {
        "s": "{0}：删除库内副本失败（{1}）。",
        "t": "{0}：保管庫内のコピーの削除に失敗しました（{1}）。"
      },
      {
        "s": "{0}：删除游戏目录副本失败（{1}）。",
        "t": "{0}：ゲームディレクトリ内のコピーの削除に失敗しました（{1}）。"
      },
      {
        "s": "{0}：回放库里已有同 ID 但内容不同的回放，为避免覆盖已中止。",
        "t": "{0}：保管庫に同じ ID で内容の異なるリプレイがあるため、上書きを避けて中止しました。"
      },
      {
        "s": "{0}：回放库里已经有同一份回放（{1}），本次下载已丢弃。",
        "t": "{0}：保管庫に同じリプレイが既にあるため（{1}）、今回のダウンロードは破棄しました。"
      },
      {
        "s": "{0}：回放库里没有这个回放，已跳过。",
        "t": "{0}：保管庫にこのリプレイがないため、スキップしました。"
      },
      {
        "s": "{0}：已从{1}下载并存入回放库（{2}）。",
        "t": "{0}：{1}からダウンロードして保管庫に保存しました（{2}）。"
      },
      {
        "s": "{0}：已从回放库删除{1}。",
        "t": "{0}：保管庫から{1}を削除しました。"
      },
      {
        "s": "{0}：已从游戏目录删除{1}。",
        "t": "{0}：ゲームディレクトリから{1}を削除しました。"
      },
      {
        "s": "{0}：已复制进库，但删除游戏目录副本失败（{1}）。库里有备份，可手动删除。",
        "t": "{0}：保管庫へのコピーは完了しましたが、ゲームディレクトリ内のコピーの削除に失敗しました（{1}）。保管庫にバックアップがあるため手動で削除できます。"
      },
      {
        "s": "{0}：已归档到回放库。",
        "t": "{0}：リプレイ保管庫に保管しました。"
      },
      {
        "s": "{0}：已放回游戏目录，游戏内「本地回放」可直接观看。",
        "t": "{0}：ゲームディレクトリに戻しました。ゲーム内の「ローカルリプレイ」から直接視聴できます。"
      },
      {
        "s": "{0}：库里存在同 ID 但内容不同的回放，为避免覆盖已跳过（请手动处理）。",
        "t": "{0}：保管庫に同じ ID で内容の異なるリプレイがあるため、上書きを避けてスキップしました（手動で処理してください）。"
      },
      {
        "s": "{0}：库里已有同一份回放，已直接腾出游戏席位。",
        "t": "{0}：保管庫に同じリプレイが既にあるため、ゲーム内の枠をそのまま空けました。"
      },
      {
        "s": "{0}：库里已经有这一局了。",
        "t": "{0}：この対局は既に保管庫にあります。"
      },
      {
        "s": "{0}：归档失败（{1}）。",
        "t": "{0}：保管に失敗しました（{1}）。"
      },
      {
        "s": "{0}：放回失败（{1}）。",
        "t": "{0}：戻すのに失敗しました（{1}）。"
      },
      {
        "s": "{0}：本工具解析不出它的结算帧（{1}），文件仍已原样保留。",
        "t": "{0}：このツールではリザルトフレームを解析できません（{1}）。ファイルはそのまま保持されています。"
      },
      {
        "s": "{0}：本工具解析不出它的结算帧（工具自带的协议版本可能比游戏旧）。",
        "t": "{0}：このツールではリザルトフレームを解析できません（ツール内蔵のプロトコルバージョンがゲームより古い可能性があります）。"
      },
      {
        "s": "{0}：注意文件内记录的 ID 是 {1}，与请求的 ID 不同；",
        "t": "{0}：ファイル内に記録された ID は {1} で、要求された ID と異なります。"
      },
      {
        "s": "{0}：游戏目录里已有同 ID 但内容不同的文件，为避免覆盖已跳过。",
        "t": "{0}：ゲームディレクトリに同じ ID で内容の異なるファイルがあるため、上書きを避けてスキップしました。"
      },
      {
        "s": "{0}：游戏目录里已经有这一局了。",
        "t": "{0}：ゲームディレクトリにこの対局が既にあります。"
      },
      {
        "s": "{0}：游戏目录里没有这个回放，已跳过。",
        "t": "{0}：ゲームディレクトリにこのリプレイがないため、スキップしました。"
      },
      {
        "s": "{0}：移除与目录块尾部完全相同的 {1} 个追加字节；原文件保存在 source/。",
        "t": "{0}：ディレクトリブロック末尾と完全に同じ {1} 個の追加バイトを除去しました。元のファイルは source/ に保存されています。"
      },
      {
        "s": "、{0} 个动作（{1} 张精灵帧）",
        "t": "、{0} 個のアクション（{1} 枚のスプライトフレーム）"
      },
      {
        "s": "【{0}】(共 {1} 条流转事件)",
        "t": "【{0}】（全 {1} 件の流れイベント）"
      },
      {
        "s": "【玩家】{0} (ID: {1})",
        "t": "【プレイヤー】{0}（ID: {1}）"
      },
      {
        "s": "下载失败（{0}）请检查网络，或换一个区服试试（当前选的是{1}）。",
        "t": "ダウンロードに失敗しました（{0}）。ネットワークを確認するか、別のリージョンを試してください（現在の選択: {1}）。"
      },
      {
        "s": "下载失败：HTTP {0}。",
        "t": "ダウンロードに失敗しました：HTTP {0}。"
      },
      {
        "s": "下载失败：{0}",
        "t": "ダウンロードに失敗しました：{0}"
      },
      {
        "s": "下载超过 {0} MB 上限，已中止。",
        "t": "ダウンロードが上限 {0} MB を超えたため中止しました。"
      },
      {
        "s": "下载默认包失败：{0}",
        "t": "既定パッケージのダウンロードに失敗しました：{0}"
      },
      {
        "s": "不支持或重复的视频资源：{0}",
        "t": "未対応または重複した動画リソース：{0}"
      },
      {
        "s": "不支持的 MonoBehaviour：{0}",
        "t": "未対応の MonoBehaviour：{0}"
      },
      {
        "s": "不支持的角色资源类型：{0}",
        "t": "未対応のキャラクターリソース種別：{0}"
      },
      {
        "s": "不是 UnityFS bundle：{0}",
        "t": "UnityFS bundle ではありません：{0}"
      },
      {
        "s": "主界面启动失败：{0}",
        "t": "メイン画面の起動に失敗しました：{0}"
      },
      {
        "s": "事件 #{0}",
        "t": "イベント #{0}"
      },
      {
        "s": "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。{0}",
        "t": "version.dll のみ削除しました。Mod、設定、SDK、ログは保持されています。{0}"
      },
      {
        "s": "从本地游戏皮肤 {0} 导出的制作模板",
        "t": "ローカルゲームのスキン {0} からエクスポートした制作テンプレート"
      },
      {
        "s": "任务已完成，皮肤列表刷新失败：{0}",
        "t": "タスクは完了しましたが、スキン一覧の更新に失敗しました：{0}"
      },
      {
        "s": "保存加载器设置失败：{0}",
        "t": "ローダー設定の保存に失敗しました：{0}"
      },
      {
        "s": "保存方案信息失败：{0}",
        "t": "構成情報の保存に失敗しました：{0}"
      },
      {
        "s": "保存配置失败：{0}",
        "t": "設定の保存に失敗しました：{0}"
      },
      {
        "s": "保留了 {0} 的已有配置",
        "t": "{0} の既存設定を保持しました"
      },
      {
        "s": "倍速必须在 [{0},{1}] 之间（1.0 = 正常速度，不支持减速）。",
        "t": "倍速は [{0},{1}] の範囲で指定してください（1.0 = 通常速度、減速は非対応）。"
      },
      {
        "s": "写入游戏目录失败：{0}。",
        "t": "ゲームディレクトリへの書き込みに失敗しました：{0}。"
      },
      {
        "s": "切换 mod 状态失败：{0}",
        "t": "Mod の状態切り替えに失敗しました：{0}"
      },
      {
        "s": "切换方案失败：{0}",
        "t": "構成の切り替えに失敗しました：{0}"
      },
      {
        "s": "删除 version.dll 失败：{0}。",
        "t": "version.dll の削除に失敗しました：{0}。"
      },
      {
        "s": "删除 {0} 失败：{1}（游戏运行时 DLL 可能被占用）。",
        "t": "{0} の削除に失敗しました：{1}（ゲーム実行中は DLL が使用されている可能性があります）。"
      },
      {
        "s": "删除方案失败：{0}",
        "t": "構成の削除に失敗しました：{0}"
      },
      {
        "s": "刷新 {0} 次后选择",
        "t": "{0} 回リロールして選択"
      },
      {
        "s": "加载器发布包 {0}",
        "t": "ローダーのリリースパッケージ {0}"
      },
      {
        "s": "加载器已就位，但这份安装没有版本清单 —— 重新安装一次就能纳入版本管理（{0}）。",
        "t": "ローダーは配置済みですが、このインストールにはバージョンマニフェストがありません —— 再インストールするとバージョン管理の対象になります（{0}）。"
      },
      {
        "s": "加载器已就绪（{0}）。",
        "t": "ローダーの準備ができました（{0}）。"
      },
      {
        "s": "加载器已就绪，与内置版本一致（{0}）。",
        "t": "ローダーの準備ができました。内蔵バージョンと一致しています（{0}）。"
      },
      {
        "s": "加载器已更新{0}。",
        "t": "ローダーを更新しました{0}。"
      },
      {
        "s": "动作名称无效或重复：{0}",
        "t": "アクション名が無効または重複しています：{0}"
      },
      {
        "s": "动作帧或帧率无效: {0}",
        "t": "アクションフレームまたはフレームレートが無効です: {0}"
      },
      {
        "s": "动作引用未知精灵：{0}",
        "t": "アクションが不明なスプライトを参照しています：{0}"
      },
      {
        "s": "动作没有可转换的精灵帧：{0}",
        "t": "アクションに変換可能なスプライトフレームがありません：{0}"
      },
      {
        "s": "动态卡面导入失败：{0}",
        "t": "動くカードイラストのインポートに失敗しました：{0}"
      },
      {
        "s": "动态卡面转换失败：{0}",
        "t": "動くカードイラストの変換に失敗しました：{0}"
      },
      {
        "s": "单个资源包超过 2 GiB，无法读入内存：{0}",
        "t": "単一のアセットバンドルが 2 GiB を超えており、メモリに読み込めません：{0}"
      },
      {
        "s": "卡面尺寸超出 Unity 上限（最长边 16384）：{0}",
        "t": "カードイラストのサイズが Unity の上限（最長辺 16384）を超えています：{0}"
      },
      {
        "s": "原精灵名称重复：{0}",
        "t": "元のスプライト名が重複しています：{0}"
      },
      {
        "s": "发布包含有非法路径：{0}",
        "t": "リリースパッケージに不正なパスがあります：{0}"
      },
      {
        "s": "发布包校验失败：{0} 哈希不一致。",
        "t": "リリースパッケージの検証に失敗しました：{0} のハッシュが一致しません。"
      },
      {
        "s": "发布包缺少文件：{0}",
        "t": "リリースパッケージにファイルがありません：{0}"
      },
      {
        "s": "发布包里的加载器（{0}）比游戏目录里已装的（{1}）更旧 —— 这是一次降级，已阻止。",
        "t": "リリースパッケージ内のローダー（{0}）がゲームディレクトリにインストール済みのもの（{1}）より古いため、ダウングレードとしてブロックしました。"
      },
      {
        "s": "变速器已安装，每次进入游戏将自动使用 {0} 倍速。",
        "t": "変速機をインストールしました。ゲームに入るたびに自動で {0} 倍速が適用されます。"
      },
      {
        "s": "变速基础倍率必须在 [{0},{1}] 之间（1.0 = 正常速度，不支持减速）。",
        "t": "変速の基本倍率は [{0},{1}] の範囲で指定してください（1.0 = 通常速度、減速は非対応）。"
      },
      {
        "s": "吉星派对 · {0}",
        "t": "Astral Party · {0}"
      },
      {
        "s": "同名自定义包已存在: {0}",
        "t": "同名のカスタムパッケージが既に存在します: {0}"
      },
      {
        "s": "启动游戏失败：{0}",
        "t": "ゲームの起動に失敗しました：{0}"
      },
      {
        "s": "命运 #{0}",
        "t": "運命 #{0}"
      },
      {
        "s": "回放 CDN 返回 HTTP {0} {1}",
        "t": "リプレイ CDN が HTTP {0} {1} を返しました"
      },
      {
        "s": "回放 ID 不该超过 {0} 位（收到 {1} 位）。",
        "t": "リプレイ ID は {0} 桁を超えてはいけません（{1} 桁を受信しました）。"
      },
      {
        "s": "回放列表读取失败：{0}",
        "t": "リプレイ一覧の読み取りに失敗しました：{0}"
      },
      {
        "s": "回放库已切换到：{0}",
        "t": "リプレイ保管庫を切り替えました：{0}"
      },
      {
        "s": "图片不存在: {0}",
        "t": "画像が存在しません: {0}"
      },
      {
        "s": "地图事件 #{0}",
        "t": "マップイベント #{0}"
      },
      {
        "s": "媒体文件不存在: {0}",
        "t": "メディアファイルが存在しません: {0}"
      },
      {
        "s": "存在重复卡牌资源名：{0}",
        "t": "カードリソース名が重複しています：{0}"
      },
      {
        "s": "导入失败：{0}",
        "t": "インポートに失敗しました：{0}"
      },
      {
        "s": "导入旧式卡牌皮肤包失败：{0}",
        "t": "旧形式のカードスキンパックのインポートに失敗しました：{0}"
      },
      {
        "s": "导入皮肤失败: {0}",
        "t": "スキンのインポートに失敗しました: {0}"
      },
      {
        "s": "导出失败: {0}",
        "t": "出力に失敗しました: {0}"
      },
      {
        "s": "导出失败：{0}",
        "t": "出力に失敗しました：{0}"
      },
      {
        "s": "导出所选皮肤 {0} 的图片",
        "t": "選択したスキン {0} の画像を出力"
      },
      {
        "s": "已从{0}恢复 version.dll（{1}），重启游戏后生效。",
        "t": "{0}から version.dll を復元しました（{1}）。ゲーム再起動後に反映されます。"
      },
      {
        "s": "已保存配置：{0}",
        "t": "設定を保存しました：{0}"
      },
      {
        "s": "已保留 {0} 个视频源资源，视频演出仍使用原版。",
        "t": "{0} 個の動画ソースリソースを保持しました。動画演出は引き続きオリジナルを使用します。"
      },
      {
        "s": "已创建新皮肤方案: {0}",
        "t": "新しいスキン構成を作成しました: {0}"
      },
      {
        "s": "已删除 mod：{0}",
        "t": "Mod を削除しました：{0}"
      },
      {
        "s": "已删除方案: {0}",
        "t": "構成を削除しました: {0}"
      },
      {
        "s": "已加载 {0} 个模组",
        "t": "{0} 個の Mod を読み込みました"
      },
      {
        "s": "已启用 mod：{0}（重启游戏后生效）",
        "t": "Mod を有効化しました：{0}（ゲーム再起動後に反映）"
      },
      {
        "s": "已启用变速：{0}x（重启游戏生效）",
        "t": "変速を有効化しました：{0}x（ゲーム再起動後に反映）"
      },
      {
        "s": "已处理 {0} 个资源包的重复尾部数据，原文件已保留。",
        "t": "{0} 個のアセットバンドルの重複した末尾データを処理しました。元のファイルは保持されています。"
      },
      {
        "s": "已安装（{0}），配置修改后重启游戏生效。",
        "t": "インストール済み（{0}）。設定の変更はゲーム再起動後に反映されます。"
      },
      {
        "s": "已导入 {0} 个模组：{1}——重启游戏后生效",
        "t": "{0} 個の Mod をインポートしました：{1} —— ゲーム再起動後に反映されます"
      },
      {
        "s": "已导入 {0} 到回放库。",
        "t": "{0} をリプレイ保管庫にインポートしました。"
      },
      {
        "s": "已导入 {0}，重启游戏后生效。",
        "t": "{0} をインポートしました。ゲーム再起動後に反映されます。"
      },
      {
        "s": "已导出 {0} / {1} 张动画帧",
        "t": "アニメーションフレームを {0} / {1} 枚出力しました"
      },
      {
        "s": "已导出到 {0}",
        "t": "{0} に出力しました"
      },
      {
        "s": "已归档 {0} 局最旧的回放，游戏内席位腾出 {1} 个。",
        "t": "最も古い {0} 対局のリプレイを保管し、ゲーム内の枠を {1} 個空けました。"
      },
      {
        "s": "已成功导出商店记录到 {0}",
        "t": "ショップ記録を {0} に出力しました"
      },
      {
        "s": "已成功导出筹码记录到 {0}",
        "t": "レリック記録を {0} に出力しました"
      },
      {
        "s": "已扫描 {0} 个目录…",
        "t": "{0} 個のディレクトリをスキャンしました…"
      },
      {
        "s": "已把 %AppData% 里的旧配置复制过来：{0}。",
        "t": "%AppData% 内の旧設定をコピーしました：{0}。"
      },
      {
        "s": "已提取 {0} 个图片入口、{1} 个动作、{2} 帧",
        "t": "{0} 個の画像エントリ、{1} 個のアクション、{2} フレームを抽出しました"
      },
      {
        "s": "已添加并切换到：{0}",
        "t": "追加して切り替えました：{0}"
      },
      {
        "s": "已添加：{0}",
        "t": "追加しました：{0}"
      },
      {
        "s": "已用默认编辑器打开配置：{0}",
        "t": "既定のエディターで設定を開きました：{0}"
      },
      {
        "s": "已用默认编辑器打开：{0}",
        "t": "既定のエディターで開きました：{0}"
      },
      {
        "s": "已禁用 mod：{0}（重启游戏后生效）",
        "t": "Mod を無効化しました：{0}（ゲーム再起動後に反映）"
      },
      {
        "s": "已自动定位游戏目录：{0}",
        "t": "ゲームディレクトリを自動で特定しました：{0}"
      },
      {
        "s": "已记住这个选择，但同步加载器配置失败：{0}",
        "t": "この選択を記憶しましたが、ローダー設定の同期に失敗しました：{0}"
      },
      {
        "s": "已转换动画帧 {0} / {1}",
        "t": "アニメーションフレームを変換しました {0} / {1}"
      },
      {
        "s": "已转换并导入 {0} 张卡面。预览后点击“设为当前”启用。",
        "t": "{0} 枚のカードイラストを変換してインポートしました。プレビュー後に「現在に設定」を押すと有効になります。"
      },
      {
        "s": "已转换并导入 {0} 张图片",
        "t": "{0} 枚の画像を変換してインポートしました"
      },
      {
        "s": "已还原卡面为默认：{0} ({1})",
        "t": "カードイラストを既定に戻しました：{0} ({1})"
      },
      {
        "s": "已选择卡面方案：{0}，完全退出并重启游戏后生效。",
        "t": "カードイラスト構成を選択しました：{0}。ゲームを完全に終了して再起動すると反映されます。"
      },
      {
        "s": "已选择游戏目录：{0}",
        "t": "ゲームディレクトリを選択しました：{0}"
      },
      {
        "s": "异画 #{0}",
        "t": "別イラスト #{0}"
      },
      {
        "s": "忽略了 {0} 个非模组文件",
        "t": "Mod 以外のファイル {0} 個を無視しました"
      },
      {
        "s": "恢复默认包失败：{0}",
        "t": "既定パッケージの復元に失敗しました：{0}"
      },
      {
        "s": "成功导入皮肤方案: {0}",
        "t": "スキン構成をインポートしました: {0}"
      },
      {
        "s": "手牌 #{0}",
        "t": "手札 #{0}"
      },
      {
        "s": "打不开目录：{0}",
        "t": "ディレクトリを開けません：{0}"
      },
      {
        "s": "打不开系统浏览器：{0}",
        "t": "システムのブラウザーを開けません：{0}"
      },
      {
        "s": "打开库目录失败：{0}",
        "t": "保管庫ディレクトリを開けませんでした：{0}"
      },
      {
        "s": "打开目录失败：{0}",
        "t": "ディレクトリを開けませんでした：{0}"
      },
      {
        "s": "打开配置失败：{0}",
        "t": "設定を開けませんでした：{0}"
      },
      {
        "s": "扫描出错：{0}",
        "t": "スキャン中にエラーが発生しました：{0}"
      },
      {
        "s": "技能视频不是可识别的 CRI USM：{0}",
        "t": "スキル動画が認識可能な CRI USM ではありません：{0}"
      },
      {
        "s": "把 {0} 个旧版本文件移入了回收站",
        "t": "{0} 個の旧バージョンファイルをごみ箱へ移動しました"
      },
      {
        "s": "操作失败：{0}",
        "t": "操作に失敗しました：{0}"
      },
      {
        "s": "整理完成：游戏目录保留最新 {0} 局，归档 {1} 局。",
        "t": "整理が完了しました：ゲームディレクトリには最新 {0} 対局を保持し、{1} 対局を保管しました。"
      },
      {
        "s": "文件不存在：{0}",
        "t": "ファイルが存在しません：{0}"
      },
      {
        "s": "文案槽位或长度无效: {0}",
        "t": "テキストスロットまたは長さが無効です: {0}"
      },
      {
        "s": "文档目录不可写（{0}），配置暂时保存在 {1}。",
        "t": "ドキュメントディレクトリに書き込めません（{0}）。設定は一時的に {1} に保存されます。"
      },
      {
        "s": "新建方案失败: {0}",
        "t": "構成の作成に失敗しました: {0}"
      },
      {
        "s": "方案共享图片冲突: {0}",
        "t": "構成の共有画像が競合しています: {0}"
      },
      {
        "s": "方案共享文案冲突: {0}",
        "t": "構成の共有テキストが競合しています: {0}"
      },
      {
        "s": "方案操作失败：{0}",
        "t": "構成の操作に失敗しました：{0}"
      },
      {
        "s": "方案缺少自定义包: {0}",
        "t": "構成にカスタムパッケージがありません: {0}"
      },
      {
        "s": "无法下载当前版本 {0} 的加载器包：HTTP {1}。可重试或使用更新功能安装新版。",
        "t": "現在のバージョン {0} のローダーパッケージをダウンロードできません：HTTP {1}。再試行するか、更新機能で新しいバージョンをインストールしてください。"
      },
      {
        "s": "无法完整读取卡牌资源包 {0}：{1}",
        "t": "カードアセットバンドル {0} を完全に読み取れません：{1}"
      },
      {
        "s": "无法完整读取旧资源包 {0}：{1}",
        "t": "旧アセットバンドル {0} を完全に読み取れません：{1}"
      },
      {
        "s": "无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。{0}",
        "t": "FFmpeg を取得できません。ffmpeg.exe をアプリの隣の tools ディレクトリに置くか、ASTRAL_FFMPEG_PATH を設定してください。{0}"
      },
      {
        "s": "无法读取 bundle 内的资源文件：{0}",
        "t": "bundle 内のリソースファイルを読み取れません：{0}"
      },
      {
        "s": "无法读取视频首帧：{0}",
        "t": "動画の先頭フレームを読み取れません：{0}"
      },
      {
        "s": "更新失败：{0}",
        "t": "更新に失敗しました：{0}"
      },
      {
        "s": "替换卡面失败：{0}",
        "t": "カードイラストの置換に失敗しました：{0}"
      },
      {
        "s": "未找到 mod：{0}",
        "t": "Mod が見つかりません：{0}"
      },
      {
        "s": "未知动画附属 MonoBehaviour，不能部分导入：{0}",
        "t": "不明なアニメーション付属 MonoBehaviour のため、部分インポートできません：{0}"
      },
      {
        "s": "未知图片槽位: {0}",
        "t": "不明な画像スロット: {0}"
      },
      {
        "s": "未知视频实现：{0}",
        "t": "不明な動画実装：{0}"
      },
      {
        "s": "来源皮肤 {0} 的图片或动画未完整定位，请先在游戏中查看该皮肤以下载资源",
        "t": "ソーススキン {0} の画像またはアニメーションを完全に特定できません。先にゲーム内でそのスキンを表示してリソースをダウンロードしてください"
      },
      {
        "s": "模组包比游戏目录里已装的更旧：{0}。确认要降级后再导入（玩家配置与停用状态仍会保留）。",
        "t": "Mod パッケージがゲームディレクトリにインストール済みのものより古いです：{0}。ダウングレードすることを確認してからインポートしてください（プレイヤー設定と無効化状態は保持されます）。"
      },
      {
        "s": "正在深度扫描 {0} …",
        "t": "{0} を詳細スキャン中…"
      },
      {
        "s": "正在转换：{0} / {1} 帧上限",
        "t": "変換中：{0} / {1} フレーム上限"
      },
      {
        "s": "此前累计刷新了 {0} 次后才确认选择",
        "t": "選択確定までに累計 {0} 回リロールしました"
      },
      {
        "s": "注意：现在游戏目录里有 {0} 局，超过游戏上限 {1}，",
        "t": "注意：現在ゲームディレクトリに {0} 対局あり、ゲームの上限 {1} を超えています。"
      },
      {
        "s": "深度扫描出错：{0}",
        "t": "詳細スキャン中にエラーが発生しました：{0}"
      },
      {
        "s": "游戏目录不存在：{0}",
        "t": "ゲームディレクトリが存在しません：{0}"
      },
      {
        "s": "游戏目录里只有 {0} 局，未超过保留名额 {1}，无需整理。",
        "t": "ゲームディレクトリには {0} 対局しかなく、保持枠 {1} を超えていないため整理は不要です。"
      },
      {
        "s": "游戏目录里已装的加载器（{0}）比本程序内置的（{1}）更新 —— 这是一次降级，已阻止。",
        "t": "ゲームディレクトリにインストール済みのローダー（{0}）がこのアプリ内蔵のもの（{1}）より新しいため、ダウングレードとしてブロックしました。"
      },
      {
        "s": "生成 GIF：{0} / {1} 帧",
        "t": "GIF を生成中：{0} / {1} フレーム"
      },
      {
        "s": "皮肤不属于当前角色: {0}",
        "t": "スキンが現在のキャラクターのものではありません: {0}"
      },
      {
        "s": "皮肤方案不存在: {0}",
        "t": "スキン構成が存在しません: {0}"
      },
      {
        "s": "目标方案已存在: {0}",
        "t": "対象の構成が既に存在します: {0}"
      },
      {
        "s": "移除动态卡面失败：{0}",
        "t": "動くカードイラストの削除に失敗しました：{0}"
      },
      {
        "s": "第 {0} 档",
        "t": "プリセット {0}"
      },
      {
        "s": "第 {0} 次刷新",
        "t": "{0} 回目のリロール"
      },
      {
        "s": "第{0}格已售出",
        "t": "{0} 番目のスロットは売却済み"
      },
      {
        "s": "精灵名称无效或重复：{0}",
        "t": "スプライト名が無効または重複しています：{0}"
      },
      {
        "s": "纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：{0}",
        "t": "テクスチャサイズが Unity の上限（最長辺 16384）を超えているか、デコードデータが不完全です：{0}"
      },
      {
        "s": "纹理资源名为空或重复：{0}",
        "t": "テクスチャリソース名が空または重複しています：{0}"
      },
      {
        "s": "自定义包被皮肤方案引用，请先移除方案规则: {0}",
        "t": "カスタムパッケージはスキン構成から参照されています。先に構成のルールを削除してください: {0}"
      },
      {
        "s": "自定义包角色与规则不匹配: {0}",
        "t": "カスタムパッケージのキャラクターがルールと一致しません: {0}"
      },
      {
        "s": "获取卡面失败：{0}",
        "t": "カードイラストの取得に失敗しました：{0}"
      },
      {
        "s": "规则标识重复: {0}",
        "t": "ルールの識別子が重複しています: {0}"
      },
      {
        "s": "视频资源引用结构无效：{0}",
        "t": "動画リソースの参照構造が無効です：{0}"
      },
      {
        "s": "视频转换失败：{0}",
        "t": "動画の変換に失敗しました：{0}"
      },
      {
        "s": "角色 {0} 中不存在指定来源皮肤 {1}，无法导出；不会回退到默认皮肤",
        "t": "キャラクター {0} に指定のソーススキン {1} が存在しないため出力できません。既定スキンへのフォールバックは行いません"
      },
      {
        "s": "角色 {0} 没有可导出的皮肤配置",
        "t": "キャラクター {0} に出力できるスキン設定がありません"
      },
      {
        "s": "角色不存在: {0}",
        "t": "キャラクターが存在しません: {0}"
      },
      {
        "s": "角色皮肤操作失败：{0}",
        "t": "キャラクタースキンの操作に失敗しました：{0}"
      },
      {
        "s": "设置变速失败：{0}",
        "t": "変速の設定に失敗しました：{0}"
      },
      {
        "s": "读取 {0} 个原资源包",
        "t": "{0} 個の元アセットバンドルを読み取り"
      },
      {
        "s": "读取变速器状态失败：{0}",
        "t": "変速機の状態の読み取りに失敗しました：{0}"
      },
      {
        "s": "读取模组状态失败：{0}",
        "t": "Mod の状態の読み取りに失敗しました：{0}"
      },
      {
        "s": "读取游戏位置失败：{0}",
        "t": "ゲームの場所の読み取りに失敗しました：{0}"
      },
      {
        "s": "读取状态失败：{0}",
        "t": "状態の読み取りに失敗しました：{0}"
      },
      {
        "s": "读取皮肤方案失败: {0}",
        "t": "スキン構成の読み取りに失敗しました: {0}"
      },
      {
        "s": "资源不是支持的卡牌贴图，拒绝部分导入：{0}",
        "t": "サポート対象のカードテクスチャではないため、部分インポートを拒否しました：{0}"
      },
      {
        "s": "资源包没有可转换的 Texture2D：{0}",
        "t": "アセットバンドルに変換可能な Texture2D がありません：{0}"
      },
      {
        "s": "资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：{0}",
        "t": "リソースが複数のキャラクターまたはスキンに対応しており、自動識別できません。対象の立ち絵が明確なスキンパックを使用してください。マッチ結果：{0}"
      },
      {
        "s": "转换卡面 {0} / {1}：{2}",
        "t": "カードイラストを変換中 {0} / {1}：{2}"
      },
      {
        "s": "转换资源 {0} / {1}：{2}",
        "t": "リソースを変換中 {0} / {1}：{2}"
      },
      {
        "s": "还原卡面失败：{0}",
        "t": "カードイラストの復元に失敗しました：{0}"
      },
      {
        "s": "这个文件有 {0} MB，超过 {1} MB 上限，已中止。",
        "t": "このファイルは {0} MB で、上限 {1} MB を超えているため中止しました。"
      },
      {
        "s": "选择替换卡面图片 ({0})",
        "t": "差し替えるカードイラストを選択 ({0})"
      },
      {
        "s": "配置保存在「文档\\{0}」（和回放库同一个文件夹）：主配置、游戏目录记忆、回放库设置都在这里。",
        "t": "設定は「ドキュメント\\{0}」（リプレイ保管庫と同じフォルダー）に保存されます：メイン設定、ゲームディレクトリの記憶、リプレイ保管庫の設定がすべてここにあります。"
      },
      {
        "s": "配置已保存并同步到游戏目录；下次进入游戏自动使用 {0} 倍速。",
        "t": "設定を保存し、ゲームディレクトリに同期しました。次回ゲームに入ると自動で {0} 倍速が適用されます。"
      },
      {
        "s": "（内置：{0}）",
        "t": "（内蔵: {0}）"
      },
      {
        "s": "；缺失：{0}",
        "t": "；欠落：{0}"
      }
    ],
    "zh-Hant": [
      {
        "s": "：不用打开游戏，也不占游戏内的 10 个席位。",
        "t": "：不用開啟遊戲，也不佔遊戲內的 10 個席位。"
      },
      {
        "s": "：已装",
        "t": "：已裝"
      },
      {
        "s": "。 确认要继续放回，就点下面的按钮；库里的副本无论如何都会保留。",
        "t": "。 確認要繼續放回，就點下面的按鈕；庫裡的副本無論如何都會保留。"
      },
      {
        "s": "· 提交 {0}",
        "t": "· 提交 {0}"
      },
      {
        "s": "（与 mod 的 dll 同目录，由 mod 的 SdkConfig 读写；重启游戏后生效）。勾选框 = 开关，数字框 = 数值，文本框 = 文字；",
        "t": "（與 mod 的 dll 同目錄，由 mod 的 SdkConfig 讀寫；重啟遊戲後生效）。勾選框 = 開關，數字框 = 數值，文字框 = 文字；"
      },
      {
        "s": ") 恢复为应用内置的原始卡面吗？",
        "t": ") 恢復為應用內建的原始卡面嗎？"
      },
      {
        "s": "{0} · 编辑此人物的自定义素材{1}",
        "t": "{0} · 編輯此人物的自定義素材{1}"
      },
      {
        "s": "{0} · 等级 {1}",
        "t": "{0} · 等級 {1}"
      },
      {
        "s": "{0} · 第{1}回合：{2}",
        "t": "{0} · 第{1}回合：{2}"
      },
      {
        "s": "{0} · 累计转账 {1} 星币",
        "t": "{0} · 累計轉賬 {1} 星幣"
      },
      {
        "s": "{0} ({1} 条)",
        "t": "{0} ({1} 條)"
      },
      {
        "s": "{0} / {1} 条",
        "t": "{0} / {1} 條"
      },
      {
        "s": "{0} 安装加载器",
        "t": "{0} 安裝載入器"
      },
      {
        "s": "{0} 次",
        "t": "{0} 次"
      },
      {
        "s": "{0} 次掷骰 · {1} 颗骰子 · 平均 {2} 点 · 受控 {3} 次",
        "t": "{0} 次擲骰 · {1} 顆骰子 · 平均 {2} 點 · 受控 {3} 次"
      },
      {
        "s": "{0} 从 GitHub 更新",
        "t": "{0} 從 GitHub 更新"
      },
      {
        "s": "{0} 第 {1} 帧预览",
        "t": "{0} 第 {1} 幀預覽"
      },
      {
        "s": "{0} 点",
        "t": "{0} 點"
      },
      {
        "s": "{0} 个进行中",
        "t": "{0} 個進行中"
      },
      {
        "s": "{0} 个人物 · {1}",
        "t": "{0} 個人物 · {1}"
      },
      {
        "s": "{0} 个位置",
        "t": "{0} 個位置"
      },
      {
        "s": "{0} 工具箱",
        "t": "{0} 工具箱"
      },
      {
        "s": "{0} 加载器",
        "t": "{0} 載入器"
      },
      {
        "s": "{0} 结构可疑",
        "t": "{0} 結構可疑"
      },
      {
        "s": "{0} 局 · {1}",
        "t": "{0} 局 · {1}"
      },
      {
        "s": "{0} 决胜",
        "t": "{0} 決勝"
      },
      {
        "s": "{0} 可操作游戏",
        "t": "{0} 可操作遊戲"
      },
      {
        "s": "{0} 拉取最新",
        "t": "{0} 拉取最新"
      },
      {
        "s": "{0} 目录已失效",
        "t": "{0} 目錄已失效"
      },
      {
        "s": "{0} 星币",
        "t": "{0} 星幣"
      },
      {
        "s": "{0} 张卡面",
        "t": "{0} 張卡面"
      },
      {
        "s": "{0} 帧",
        "t": "{0} 幀"
      },
      {
        "s": "{0} 重新安装内置版本",
        "t": "{0} 重新安裝內建版本"
      },
      {
        "s": "{0}{1}曲线",
        "t": "{0}{1}曲線"
      },
      {
        "s": "{0}处理进度",
        "t": "{0}處理進度"
      },
      {
        "s": "{0}的皮肤",
        "t": "{0}的造型"
      },
      {
        "s": "{0}预览",
        "t": "{0}預覽"
      },
      {
        "s": "→ 包内",
        "t": "→ 包內"
      },
      {
        "s": "＋ 按键",
        "t": "＋ 按鍵"
      },
      {
        "s": "= 点一下再按要绑的键（键盘任意键或鼠标左/右/中键、侧键，Esc 取消）。",
        "t": "= 點一下再按要綁的鍵（鍵盤任意鍵或滑鼠左/右/中鍵、側鍵，Esc 取消）。"
      },
      {
        "s": "安装：{0}",
        "t": "安裝：{0}"
      },
      {
        "s": "按键按钮",
        "t": "按鍵按鈕"
      },
      {
        "s": "按资源比例裁剪，或保留完整图片",
        "t": "按資源比例裁剪，或保留完整圖片"
      },
      {
        "s": "保存布局与动作参数",
        "t": "儲存佈局與動作引數"
      },
      {
        "s": "保存裁剪",
        "t": "儲存裁剪"
      },
      {
        "s": "保存尺寸",
        "t": "儲存尺寸"
      },
      {
        "s": "保存后重启游戏生效；日常只需要改「启用加载器」或「启动时基础倍速」。",
        "t": "儲存後重啟遊戲生效；日常只需要改「啟用載入器」或「啟動時基礎倍速」。"
      },
      {
        "s": "保存配置",
        "t": "儲存配置"
      },
      {
        "s": "保存设置",
        "t": "儲存設定"
      },
      {
        "s": "保存为透明 PNG，尺寸",
        "t": "儲存為透明 PNG，尺寸"
      },
      {
        "s": "保存文案",
        "t": "儲存文案"
      },
      {
        "s": "保存信息",
        "t": "儲存資訊"
      },
      {
        "s": "倍速",
        "t": "倍速"
      },
      {
        "s": "本地构建{0}",
        "t": "本地構建{0}"
      },
      {
        "s": "本工具读不出它们的结算帧（工具自带的协议版本可能比游戏旧）。如果游戏也读不出来，",
        "t": "本工具讀不出它們的結算幀（工具自帶的協議版本可能比遊戲舊）。如果遊戲也讀不出來，"
      },
      {
        "s": "本回合转账",
        "t": "本回合轉賬"
      },
      {
        "s": "本局地图",
        "t": "本局地圖"
      },
      {
        "s": "比内置的 v{0} 更新（从 GitHub 更新过）",
        "t": "比內建的 v{0} 更新（從 GitHub 更新過）"
      },
      {
        "s": "比内置的 v{0} 旧 —— 点「重新安装内置版本」可装回内置版",
        "t": "比內建的 v{0} 舊 —— 點「重新安裝內建版本」可裝回內建版"
      },
      {
        "s": "编辑人物皮肤",
        "t": "編輯人物造型"
      },
      {
        "s": "播放预览",
        "t": "播放預覽"
      },
      {
        "s": "不裁剪，等比缩放并透明补边",
        "t": "不裁剪，等比縮放並透明補邊"
      },
      {
        "s": "不添加自定义覆盖",
        "t": "不新增自定義覆蓋"
      },
      {
        "s": "裁剪结果预览",
        "t": "裁剪結果預覽"
      },
      {
        "s": "裁剪为目标比例",
        "t": "裁剪為目標比例"
      },
      {
        "s": "裁剪选区：拖动移动，拖动四角调整大小，方向键微调位置",
        "t": "裁剪選區：拖動移動，拖動四角調整大小，方向鍵微調位置"
      },
      {
        "s": "裁剪预览",
        "t": "裁剪預覽"
      },
      {
        "s": "参战玩家 ({0} 名)",
        "t": "參戰玩家 ({0} 名)"
      },
      {
        "s": "查看详情",
        "t": "檢視詳情"
      },
      {
        "s": "承受伤害",
        "t": "承受傷害"
      },
      {
        "s": "筹码",
        "t": "籌碼"
      },
      {
        "s": "筹码记录共 {0} 条",
        "t": "籌碼記錄共 {0} 條"
      },
      {
        "s": "出牌",
        "t": "出牌"
      },
      {
        "s": "处理方式",
        "t": "處理方式"
      },
      {
        "s": "创建自定义覆盖",
        "t": "建立自定義覆蓋"
      },
      {
        "s": "此模组声明可操作游戏（模拟出牌 / 掷骰 / 移动等）。权限机制已取消，这里只提示：请确认来源可信",
        "t": "此模組宣告可操作遊戲（模擬出牌 / 擲骰 / 移動等）。許可權機制已取消，這裡只提示：請確認來源可信"
      },
      {
        "s": "次",
        "t": "次"
      },
      {
        "s": "次 · 平均每回合",
        "t": "次 · 平均每回合"
      },
      {
        "s": "从方案移除此人物",
        "t": "從方案移除此人物"
      },
      {
        "s": "从列表移除（不会删除游戏文件）",
        "t": "從列表移除（不會刪除遊戲檔案）"
      },
      {
        "s": "从应用内置默认包创建全新方案，不使用磁盘上已经修改过的图片。无需联网，新建后可更换单张卡面。",
        "t": "從應用內建預設包建立全新方案，不使用磁碟上已經修改過的圖片。無需聯網，新建後可更換單張卡面。"
      },
      {
        "s": "错误：{0}",
        "t": "錯誤：{0}"
      },
      {
        "s": "打开",
        "t": "開啟"
      },
      {
        "s": "打开方案目录",
        "t": "開啟方案目錄"
      },
      {
        "s": "打开目录",
        "t": "開啟目錄"
      },
      {
        "s": "打开目录修复",
        "t": "開啟目錄修復"
      },
      {
        "s": "大小:",
        "t": "大小:"
      },
      {
        "s": "单次播放",
        "t": "單次播放"
      },
      {
        "s": "当前 {0}x（重启游戏生效）",
        "t": "當前 {0}x（重啟遊戲生效）"
      },
      {
        "s": "当前星币",
        "t": "當前星幣"
      },
      {
        "s": "档位 {0}",
        "t": "檔位 {0}"
      },
      {
        "s": "档位 {0}{1}",
        "t": "檔位 {0}{1}"
      },
      {
        "s": "导出 AI 文本 (TXT)",
        "t": "匯出 AI 文字 (TXT)"
      },
      {
        "s": "导出 JSON",
        "t": "匯出 JSON"
      },
      {
        "s": "导出 ZIP",
        "t": "匯出 ZIP"
      },
      {
        "s": "导出表格 (CSV)",
        "t": "匯出表格 (CSV)"
      },
      {
        "s": "导出当前动作 GIF",
        "t": "匯出當前動作 GIF"
      },
      {
        "s": "导出方案 ZIP",
        "t": "匯出方案 ZIP"
      },
      {
        "s": "导入方案 ZIP",
        "t": "匯入方案 ZIP"
      },
      {
        "s": "导入人物包",
        "t": "匯入人物包"
      },
      {
        "s": "导入视频 / GIF",
        "t": "匯入影片 / GIF"
      },
      {
        "s": "导入与管理",
        "t": "匯入與管理"
      },
      {
        "s": "导入帧序列",
        "t": "匯入幀序列"
      },
      {
        "s": "地图人物布局",
        "t": "地圖人物佈局"
      },
      {
        "s": "第 {0} / {1} 帧 · {2} ms",
        "t": "第 {0} / {1} 幀 · {2} ms"
      },
      {
        "s": "第 {0} 回合",
        "t": "第 {0} 回合"
      },
      {
        "s": "第 {0} 回合 · {1}",
        "t": "第 {0} 回合 · {1}"
      },
      {
        "s": "第 {0} 帧",
        "t": "第 {0} 幀"
      },
      {
        "s": "点按切换（不勾选 = 按住才生效）",
        "t": "點按切換（不勾選 = 按住才生效）"
      },
      {
        "s": "点击选择 / 取消选择，确认后应用到该档位。档位按键可多选（如 Ctrl + Shift）。",
        "t": "點選選擇 / 取消選擇，確認後應用到該檔位。檔位按鍵可多選（如 Ctrl + Shift）。"
      },
      {
        "s": "点一下，再按下要绑的键（支持鼠标左/右/中键与侧键；Esc 取消）",
        "t": "點一下，再按下要綁的鍵（支援滑鼠左/右/中鍵與側鍵；Esc 取消）"
      },
      {
        "s": "动画帧预览",
        "t": "動畫幀預覽"
      },
      {
        "s": "动画逐帧图片",
        "t": "動畫逐幀圖片"
      },
      {
        "s": "动态卡面 · {0} 帧/秒 · {1} 秒循环",
        "t": "動態卡面 · {0} 幀/秒 · {1} 秒迴圈"
      },
      {
        "s": "动作",
        "t": "動作"
      },
      {
        "s": "读不到加载器更新日志（内置为空，联网获取也没成功）。点「{0} 拉取最新」重试。",
        "t": "讀不到載入器更新日誌（內建為空，聯網獲取也沒成功）。點「{0} 拉取最新」重試。"
      },
      {
        "s": "对局表现分析",
        "t": "對局表現分析"
      },
      {
        "s": "对局结果",
        "t": "對局結果"
      },
      {
        "s": "多人物搭配 · {0} 个人物",
        "t": "多人物搭配 · {0} 個人物"
      },
      {
        "s": "二维精灵动画",
        "t": "二維精靈動畫"
      },
      {
        "s": "发现新版本：已装 v{0} → 最新 v{1}，点「更新到 v{2}」。",
        "t": "發現新版本：已裝 v{0} → 最新 v{1}，點「更新到 v{2}」。"
      },
      {
        "s": "方案：{0} · {1}",
        "t": "方案：{0} · {1}"
      },
      {
        "s": "方案描述（可选）：",
        "t": "方案描述（可選）："
      },
      {
        "s": "方案设置",
        "t": "方案設定"
      },
      {
        "s": "方案显示名称：",
        "t": "方案顯示名稱："
      },
      {
        "s": "方案英文目录名（唯一标识）：",
        "t": "方案英文目錄名（唯一標識）："
      },
      {
        "s": "方案中的人物",
        "t": "方案中的人物"
      },
      {
        "s": "方向键微调位置，按住 Shift 每次移动 10 像素。原图不会被修改。",
        "t": "方向鍵微調位置，按住 Shift 每次移動 10 畫素。原圖不會被修改。"
      },
      {
        "s": "复制版本信息",
        "t": "複製版本資訊"
      },
      {
        "s": "该角色全部原皮肤",
        "t": "該角色全部原造型"
      },
      {
        "s": "该人物全部原皮肤",
        "t": "該人物全部原造型"
      },
      {
        "s": "该玩家本回合快照区间内的转账增量；同回合多次进店显示相同值，不重复累计。",
        "t": "該玩家本回合快照區間內的轉賬增量；同回合多次進店顯示相同值，不重複累計。"
      },
      {
        "s": "高级：编辑完整清单",
        "t": "高階：編輯完整清單"
      },
      {
        "s": "高级：用系统默认编辑器直接改 JSON",
        "t": "高階：用系統預設編輯器直接改 JSON"
      },
      {
        "s": "高级编辑…",
        "t": "高階編輯…"
      },
      {
        "s": "高级选项（Steam 绕过细节 · 引导 · 超时）",
        "t": "高階選項（Steam 繞過細節 · 引導 · 超時）"
      },
      {
        "s": "更换官方皮肤",
        "t": "更換官方造型"
      },
      {
        "s": "更换图片",
        "t": "更換圖片"
      },
      {
        "s": "更新到 v{0}",
        "t": "更新到 v{0}"
      },
      {
        "s": "攻击 / 防御",
        "t": "攻擊 / 防禦"
      },
      {
        "s": "共 {0} 张卡面",
        "t": "共 {0} 張卡面"
      },
      {
        "s": "共筛选出 {0} 条事件记录",
        "t": "共篩選出 {0} 條事件記錄"
      },
      {
        "s": "关闭",
        "t": "關閉"
      },
      {
        "s": "关卡进度",
        "t": "關卡進度"
      },
      {
        "s": "官方来源替换图片与人物动画。语音、视频、特效及特殊演出保留原版。",
        "t": "官方來源替換圖片與人物動畫。語音、影片、特效及特殊演出保留原版。"
      },
      {
        "s": "归档最旧的 {0} 局",
        "t": "歸檔最舊的 {0} 局"
      },
      {
        "s": "还没有配置文件（mods\\{0}\\config.json）。保存后将创建。",
        "t": "還沒有配置檔案（mods\\{0}\\config.json）。儲存後將建立。"
      },
      {
        "s": "合计",
        "t": "合計"
      },
      {
        "s": "恢复",
        "t": "恢復"
      },
      {
        "s": "恢复此动作",
        "t": "恢復此動作"
      },
      {
        "s": "恢复内置卡面",
        "t": "恢復內建卡面"
      },
      {
        "s": "回放 ID",
        "t": "重播 ID"
      },
      {
        "s": "回放服务器",
        "t": "重播伺服器"
      },
      {
        "s": "回放服务器（不确定就按当前游戏版本选）",
        "t": "重播伺服器（不確定就按當前遊戲版本選）"
      },
      {
        "s": "回放复盘",
        "t": "重播覆盤"
      },
      {
        "s": "回放库",
        "t": "重播庫"
      },
      {
        "s": "回放由服务器录制后放在官方回放服务器上，游戏里点「保存回放」也是从那里拉的。 填上回放 ID 就能直接下载进",
        "t": "重播由伺服器錄製後放在官方重播伺服器上，遊戲裡點「儲存重播」也是從那裡拉的。 填上重播 ID 就能直接下載進"
      },
      {
        "s": "回合",
        "t": "回合"
      },
      {
        "s": "或其中的卡面图片。",
        "t": "或其中的卡面圖片。"
      },
      {
        "s": "击杀",
        "t": "擊殺"
      },
      {
        "s": "及其目录下的所有卡面图片吗？此操作不可撤销。",
        "t": "及其目錄下的所有卡面圖片嗎？此操作不可撤銷。"
      },
      {
        "s": "技能使用",
        "t": "技能使用"
      },
      {
        "s": "继续导入会用包里的旧版本覆盖现有文件。玩家改过的",
        "t": "繼續匯入會用包裡的舊版本覆蓋現有檔案。玩家改過的"
      },
      {
        "s": "加载器 {0}",
        "t": "載入器 {0}"
      },
      {
        "s": "检查更新失败：{0}",
        "t": "檢查更新失敗：{0}"
      },
      {
        "s": "检查失败：{0}",
        "t": "檢查失敗：{0}"
      },
      {
        "s": "简要说明此方案的特色…",
        "t": "簡要說明此方案的特色…"
      },
      {
        "s": "将 {0} 停用并移入皮肤目录中的 .deleted 文件夹。",
        "t": "將 {0} 停用並移入造型目錄中的 .deleted 資料夾。"
      },
      {
        "s": "将从应用内置默认包恢复此卡面，覆盖当前方案的同名图片；磁盘上修改过的 default 不会被用作原始模板。",
        "t": "將從應用內建預設包恢復此卡面，覆蓋當前方案的同名圖片；磁碟上修改過的 default 不會被用作原始模板。"
      },
      {
        "s": "角色 {0}",
        "t": "角色 {0}"
      },
      {
        "s": "角色文案",
        "t": "角色文案"
      },
      {
        "s": "结算奖励",
        "t": "結算獎勵"
      },
      {
        "s": "结算快照中的完整属性与行动统计，记录本局关键数据指标。",
        "t": "結算快照中的完整屬性與行動統計，記錄本局關鍵資料指標。"
      },
      {
        "s": "结算快照中的总伤害、承伤、治疗、转账与行动次数。",
        "t": "結算快照中的總傷害、承傷、治療、轉賬與行動次數。"
      },
      {
        "s": "解析展示失败: {0}",
        "t": "解析展示失敗: {0}"
      },
      {
        "s": "界面动态效果已{0}",
        "t": "介面動態效果已{0}"
      },
      {
        "s": "仅替换 {0}",
        "t": "僅替換 {0}"
      },
      {
        "s": "仅统计回放中实际收到的移动掷骰结果；保留多骰的每颗点数，受控移动单独标记，不混入战斗骰或检定骰。",
        "t": "僅統計重播中實際收到的移動擲骰結果；保留多骰的每顆點數，受控移動單獨標記，不混入戰鬥骰或檢定骰。"
      },
      {
        "s": "仅支持英文、数字、下划线及减号",
        "t": "僅支援英文、數字、下劃線及減號"
      },
      {
        "s": "旧配置已保留，但暂未转换：{0}",
        "t": "舊配置已保留，但暫未轉換：{0}"
      },
      {
        "s": "居中重置",
        "t": "居中重置"
      },
      {
        "s": "卡牌 / 技能",
        "t": "卡牌 / 技能"
      },
      {
        "s": "卡牌编号:",
        "t": "卡牌編號:"
      },
      {
        "s": "卡牌名称:",
        "t": "卡牌名稱:"
      },
      {
        "s": "卡牌使用",
        "t": "卡牌使用"
      },
      {
        "s": "开启自动托管（保留 {0} 局）",
        "t": "開啟自動託管（保留 {0} 局）"
      },
      {
        "s": "开始下载",
        "t": "開始下載"
      },
      {
        "s": "可以更新到 v{0}",
        "t": "可以更新到 v{0}"
      },
      {
        "s": "口径",
        "t": "口徑"
      },
      {
        "s": "来源与替换范围",
        "t": "來源與替換範圍"
      },
      {
        "s": "来自回合快照中的累计转账；未记录收款对象时不做推断。",
        "t": "來自回合快照中的累計轉賬；未記錄收款物件時不做推斷。"
      },
      {
        "s": "类别:",
        "t": "類別:"
      },
      {
        "s": "累计",
        "t": "累計"
      },
      {
        "s": "累计伤害",
        "t": "累計傷害"
      },
      {
        "s": "累计死亡",
        "t": "累計死亡"
      },
      {
        "s": "累计转账",
        "t": "累計轉賬"
      },
      {
        "s": "立即复制创建",
        "t": "立即複製建立"
      },
      {
        "s": "留空使用原版",
        "t": "留空使用原版"
      },
      {
        "s": "锚点使用 0～1，Y 从画布底部向上计算；缩放乘在 PPU 上，不修改游戏人物碰撞或移动。",
        "t": "錨點使用 0～1，Y 從畫布底部向上計算；縮放乘在 PPU 上，不修改遊戲人物碰撞或移動。"
      },
      {
        "s": "没检测到标准 Unity 游戏结构（缺 *_Data 或 UnityPlayer.dll）",
        "t": "沒檢測到標準 Unity 遊戲結構（缺 *_Data 或 UnityPlayer.dll）"
      },
      {
        "s": "没有匹配的方案。",
        "t": "沒有匹配的方案。"
      },
      {
        "s": "每回合新增",
        "t": "每回合新增"
      },
      {
        "s": "名字与角色简介使用角色共用文案，可能影响该角色的其他皮肤。",
        "t": "名字與角色簡介使用角色共用文案，可能影響該角色的其他造型。"
      },
      {
        "s": "模组状态与使用说明",
        "t": "模組狀態與使用說明"
      },
      {
        "s": "默认：{0}",
        "t": "預設：{0}"
      },
      {
        "s": "难度 {0} · 最终首领 {1}",
        "t": "難度 {0} · 最終首領 {1}"
      },
      {
        "s": "内置 SHA-256 {0}…（卸载时按此校验，防止误删他人文件）",
        "t": "內建 SHA-256 {0}…（解除安裝時按此校驗，防止誤刪他人檔案）"
      },
      {
        "s": "内置文件就绪：version.dll {0}",
        "t": "內建檔案就緒：version.dll {0}"
      },
      {
        "s": "您的昵称",
        "t": "您的暱稱"
      },
      {
        "s": "配置 · {0}",
        "t": "配置 · {0}"
      },
      {
        "s": "配置保存在",
        "t": "配置儲存在"
      },
      {
        "s": "偏移",
        "t": "偏移"
      },
      {
        "s": "启用 / 禁用（重启游戏后生效）",
        "t": "啟用 / 禁用（重啟遊戲後生效）"
      },
      {
        "s": "清单 JSON 无效：{0}",
        "t": "清單 JSON 無效：{0}"
      },
      {
        "s": "清空（让 mod 用它自己的默认键）",
        "t": "清空（讓 mod 用它自己的預設鍵）"
      },
      {
        "s": "请在左侧点击任意协议帧进行查看解码数据",
        "t": "請在左側點選任意協議幀進行檢視解碼資料"
      },
      {
        "s": "区服",
        "t": "區服"
      },
      {
        "s": "取消",
        "t": "取消"
      },
      {
        "s": "取消下载",
        "t": "取消下載"
      },
      {
        "s": "全队转账",
        "t": "全隊轉賬"
      },
      {
        "s": "确定（已选 {0} 个）",
        "t": "確定（已選 {0} 個）"
      },
      {
        "s": "确定删除模组「{0}」？",
        "t": "確定刪除模組「{0}」？"
      },
      {
        "s": "确定要彻底删除方案",
        "t": "確定要徹底刪除方案"
      },
      {
        "s": "确定要将",
        "t": "確定要將"
      },
      {
        "s": "确认还原",
        "t": "確認還原"
      },
      {
        "s": "确认继续",
        "t": "確認繼續"
      },
      {
        "s": "确认删除",
        "t": "確認刪除"
      },
      {
        "s": "人物设置",
        "t": "人物設定"
      },
      {
        "s": "仍然导入",
        "t": "仍然匯入"
      },
      {
        "s": "仍要放回（{0} 份）",
        "t": "仍要放回（{0} 份）"
      },
      {
        "s": "如 二次元萌化包",
        "t": "如 二次元萌化包"
      },
      {
        "s": "如 my_custom_skin",
        "t": "如 my_custom_skin"
      },
      {
        "s": "筛选筹码、玩家、品质、来源...",
        "t": "篩選籌碼、玩家、品質、來源..."
      },
      {
        "s": "筛选商店类型、玩家、卡牌、购买结果...",
        "t": "篩選商店型別、玩家、卡牌、購買結果..."
      },
      {
        "s": "删除方案",
        "t": "刪除方案"
      },
      {
        "s": "删除该档位",
        "t": "刪除該檔位"
      },
      {
        "s": "删除回放 {0}",
        "t": "刪除重播 {0}"
      },
      {
        "s": "删除会优先放进 Windows 回收站，仍可还原。只删游戏内副本是腾席位的常用做法；库内副本才是长期存档。",
        "t": "刪除會優先放進 Windows 回收站，仍可還原。只刪遊戲內副本是騰席位的常用做法；庫內副本才是長期存檔。"
      },
      {
        "s": "删除模组",
        "t": "刪除模組"
      },
      {
        "s": "删除皮肤包",
        "t": "刪除造型包"
      },
      {
        "s": "删除这个模组",
        "t": "刪除這個模組"
      },
      {
        "s": "伤害",
        "t": "傷害"
      },
      {
        "s": "商店进店共 {0} 次（其中 {1} 次有购买）",
        "t": "商店進店共 {0} 次（其中 {1} 次有購買）"
      },
      {
        "s": "上次游戏未能应用方案：{0}",
        "t": "上次遊戲未能應用方案：{0}"
      },
      {
        "s": "上一帧",
        "t": "上一幀"
      },
      {
        "s": "生命",
        "t": "生命"
      },
      {
        "s": "时长",
        "t": "時長"
      },
      {
        "s": "输出伤害",
        "t": "輸出傷害"
      },
      {
        "s": "输出预览",
        "t": "輸出預覽"
      },
      {
        "s": "输入方案名称",
        "t": "輸入方案名稱"
      },
      {
        "s": "刷新方案与状态",
        "t": "重新整理方案與狀態"
      },
      {
        "s": "死亡计数",
        "t": "死亡計數"
      },
      {
        "s": "搜索方案",
        "t": "搜尋方案"
      },
      {
        "s": "搜索帧号、CMD、消息名...",
        "t": "搜尋幀號、CMD、訊息名..."
      },
      {
        "s": "提交 {0}",
        "t": "提交 {0}"
      },
      {
        "s": "替换 {0} 第 {1} 帧",
        "t": "替換 {0} 第 {1} 幀"
      },
      {
        "s": "替换选中帧",
        "t": "替換選中幀"
      },
      {
        "s": "添加或修改人物后，完全退出并重启游戏以应用搭配。",
        "t": "新增或修改人物後，完全退出並重啟遊戲以應用搭配。"
      },
      {
        "s": "添加人物皮肤",
        "t": "新增人物造型"
      },
      {
        "s": "跳转到帧",
        "t": "跳轉到幀"
      },
      {
        "s": "同步配置：{0}",
        "t": "同步配置：{0}"
      },
      {
        "s": "图片资源",
        "t": "圖片資源"
      },
      {
        "s": "拖动或用方向键移动裁剪区域",
        "t": "拖動或用方向鍵移動裁剪區域"
      },
      {
        "s": "拖动选区移动位置，拖动四角调整大小。{0}",
        "t": "拖動選區移動位置，拖動四角調整大小。{0}"
      },
      {
        "s": "拖动选区移动位置，用滑块调整大小。透明背景会保留。",
        "t": "拖動選區移動位置，用滑塊調整大小。透明背景會保留。"
      },
      {
        "s": "玩家",
        "t": "玩家"
      },
      {
        "s": "玩家 {0}",
        "t": "玩家 {0}"
      },
      {
        "s": "玩家数据对照",
        "t": "玩家資料對照"
      },
      {
        "s": "玩家转账贡献",
        "t": "玩家轉賬貢獻"
      },
      {
        "s": "文件 {0} 已存在，继续将覆盖它。",
        "t": "檔案 {0} 已存在，繼續將覆蓋它。"
      },
      {
        "s": "文件名称:",
        "t": "檔名稱:"
      },
      {
        "s": "我的方案",
        "t": "我的方案"
      },
      {
        "s": "下面这些模组，包里的版本比游戏目录里已装的更旧：",
        "t": "下面這些模組，包裡的版本比遊戲目錄裡已裝的更舊："
      },
      {
        "s": "下一帧",
        "t": "下一幀"
      },
      {
        "s": "显示 {0} / {1} 次商店进店",
        "t": "顯示 {0} / {1} 次商店進店"
      },
      {
        "s": "显示 {0} / {1} 条筹码流转",
        "t": "顯示 {0} / {1} 條籌碼流轉"
      },
      {
        "s": "显示 {0} / {1} 帧",
        "t": "顯示 {0} / {1} 幀"
      },
      {
        "s": "消息类型",
        "t": "訊息型別"
      },
      {
        "s": "校验并保存清单",
        "t": "校驗並儲存清單"
      },
      {
        "s": "协议载荷解码详情",
        "t": "協議載荷解碼詳情"
      },
      {
        "s": "协议帧共 {0} 帧",
        "t": "協議幀共 {0} 幀"
      },
      {
        "s": "卸载：{0}",
        "t": "解除安裝：{0}"
      },
      {
        "s": "新建方案",
        "t": "新建方案"
      },
      {
        "s": "新增击杀",
        "t": "新增擊殺"
      },
      {
        "s": "新增伤害",
        "t": "新增傷害"
      },
      {
        "s": "新增转账",
        "t": "新增轉賬"
      },
      {
        "s": "星币",
        "t": "星幣"
      },
      {
        "s": "星币 · 击杀",
        "t": "星幣 · 擊殺"
      },
      {
        "s": "修改配置",
        "t": "修改配置"
      },
      {
        "s": "修改时间: {0}",
        "t": "修改時間: {0}"
      },
      {
        "s": "修改这个模组的配置",
        "t": "修改這個模組的配置"
      },
      {
        "s": "选区大小",
        "t": "選區大小"
      },
      {
        "s": "选取你的卡面",
        "t": "選取你的卡面"
      },
      {
        "s": "选择按键组合 · {0}",
        "t": "選擇按鍵組合 · {0}"
      },
      {
        "s": "选择第 {0} 帧",
        "t": "選擇第 {0} 幀"
      },
      {
        "s": "选择动画帧",
        "t": "選擇動畫幀"
      },
      {
        "s": "选择图片",
        "t": "選擇圖片"
      },
      {
        "s": "循环",
        "t": "迴圈"
      },
      {
        "s": "沿用原皮肤",
        "t": "沿用原造型"
      },
      {
        "s": "依赖：{0}",
        "t": "依賴：{0}"
      },
      {
        "s": "移除",
        "t": "移除"
      },
      {
        "s": "移除 {0} 的自制帧和原精灵映射，重启游戏后恢复原动作。",
        "t": "移除 {0} 的自制幀和原精靈對映，重啟遊戲後恢復原動作。"
      },
      {
        "s": "移除动态覆盖",
        "t": "移除動態覆蓋"
      },
      {
        "s": "移动点数",
        "t": "移動點數"
      },
      {
        "s": "移动骰子统计",
        "t": "移動骰子統計"
      },
      {
        "s": "已安装 v{0} —— 点「检查更新」看看 GitHub 上有没有新版。",
        "t": "已安裝 v{0} —— 點「檢查更新」看看 GitHub 上有沒有新版。"
      },
      {
        "s": "已安装 v{0}，比随程序内置的 v{1} 更新（这份是从 GitHub 更新来的）—— 点「检查更新」看看还有没有更新的版本。",
        "t": "已安裝 v{0}，比隨程式內建的 v{1} 更新（這份是從 GitHub 更新來的）—— 點「檢查更新」看看還有沒有更新的版本。"
      },
      {
        "s": "已安装 v{0}，比随程序内置的 v{1} 旧 —— 想回到内置版本就点「重新安装内置版本」，想知道有没有新版就点「检查更新」。",
        "t": "已安裝 v{0}，比隨程式內建的 v{1} 舊 —— 想回到內建版本就點「重新安裝內建版本」，想知道有沒有新版就點「檢查更新」。"
      },
      {
        "s": "已安装 v{0}，与随程序内置的版本一致 —— 点「检查更新」可确认 GitHub 上有没有新版。",
        "t": "已安裝 v{0}，與隨程式內建的版本一致 —— 點「檢查更新」可確認 GitHub 上有沒有新版。"
      },
      {
        "s": "已持有 {0} 个筹码：",
        "t": "已持有 {0} 個籌碼："
      },
      {
        "s": "已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近 {0} 局）。",
        "t": "已開啟自動託管：以後每次開啟本工具都會自動把超出席位的舊重播歸檔進庫（遊戲內保留最近 {0} 局）。"
      },
      {
        "s": "已设为 {0}",
        "t": "已設為 {0}"
      },
      {
        "s": "已是最新版本 v{0}。",
        "t": "已是最新版本 v{0}。"
      },
      {
        "s": "已下载 {0}",
        "t": "已下載 {0}"
      },
      {
        "s": "已下载 {0} / {1}（{2}%）",
        "t": "已下載 {0} / {1}（{2}%）"
      },
      {
        "s": "已装的 v{0} 比 GitHub 上的 v{1} 更新，无需更新。",
        "t": "已裝的 v{0} 比 GitHub 上的 v{1} 更新，無需更新。"
      },
      {
        "s": "用输出、支援和行动数据对照对局过程；不同角色分工不同，不以单一总伤害评定玩家。",
        "t": "用輸出、支援和行動資料對照對局過程；不同角色分工不同，不以單一總傷害評定玩家。"
      },
      {
        "s": "游戏名称",
        "t": "遊戲名稱"
      },
      {
        "s": "游戏目录里的 version.dll 既不是本程序内置的那一份，也没有记录在安装清单里：多半是别的工具（例如「游戏工具」页的旧版独立变速器）或第三方放进来的文件。要换成 CesiumLoader，勾选下面的「允许覆盖游戏目录里不属于本工具的 version.dll」，再点「安装加载器」；只想清掉就勾选「强制卸载」。",
        "t": "遊戲目錄裡的 version.dll 既不是本程式內建的那一份，也沒有記錄在安裝清單裡：多半是別的工具（例如「遊戲工具」頁的舊版獨立變速器）或第三方放進來的檔案。要換成 CesiumLoader，勾選下面的「允許覆蓋遊戲目錄裡不屬於本工具的 version.dll」，再點「安裝載入器」；只想清掉就勾選「強制解除安裝」。"
      },
      {
        "s": "游戏内回放席位快满了（{0}/{1}）",
        "t": "遊戲內重播席位快滿了（{0}/{1}）"
      },
      {
        "s": "游戏内回放席位已满（{0}/{1}）",
        "t": "遊戲內重播席位已滿（{0}/{1}）"
      },
      {
        "s": "游戏在「本地回放」列表里遇到这种目录会把它整个删掉",
        "t": "遊戲在「本地重播」列表裡遇到這種目錄會把它整個刪掉"
      },
      {
        "s": "游戏中的角色皮肤模组为 {0}，请更新到 {1}，然后重启游戏以使用最新修复。",
        "t": "遊戲中的角色造型模組為 {0}，請更新到 {1}，然後重啟遊戲以使用最新修復。"
      },
      {
        "s": "有新版本 v{0} 可更新",
        "t": "有新版本 v{0} 可更新"
      },
      {
        "s": "与「已禁用」状态仍会保留， 被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。",
        "t": "與「已禁用」狀態仍會保留， 被替換掉的舊檔案會移入回收站（回收站不可用時保留原檔案）。如果只是想升級，請改用更新的模組包。"
      },
      {
        "s": "原图 {0} × {1}",
        "t": "原圖 {0} × {1}"
      },
      {
        "s": "在资源管理器中打开",
        "t": "在資源管理器中開啟"
      },
      {
        "s": "载荷字节",
        "t": "載荷位元組"
      },
      {
        "s": "战绩里那一长串数字",
        "t": "戰績裡那一長串數字"
      },
      {
        "s": "找到 {0} 个候选：",
        "t": "找到 {0} 個候選："
      },
      {
        "s": "这个目录已经不在了",
        "t": "這個目錄已經不在了"
      },
      {
        "s": "帧",
        "t": "幀"
      },
      {
        "s": "帧号: {0} · {1} CMD: {2} · {3} 载荷大小: {4} 字节 {5}",
        "t": "幀號: {0} · {1} CMD: {2} · {3} 載荷大小: {4} 位元組 {5}"
      },
      {
        "s": "正在读取…",
        "t": "正在讀取…"
      },
      {
        "s": "支持原精灵名映射 SpriteMappings、自定义资源地址 asset:。媒体文件可随包保存，目前不应用。",
        "t": "支援原精靈名對映 SpriteMappings、自定義資源地址 asset:。媒體檔案可隨包儲存，目前不應用。"
      },
      {
        "s": "只修改方案对外展示的信息，不会改变方案目录名",
        "t": "只修改方案對外展示的資訊，不會改變方案目錄名"
      },
      {
        "s": "指标",
        "t": "指標"
      },
      {
        "s": "治疗",
        "t": "治療"
      },
      {
        "s": "逐回合数据表",
        "t": "逐回合資料表"
      },
      {
        "s": "主配置：{0}{1}",
        "t": "主配置：{0}{1}"
      },
      {
        "s": "转出",
        "t": "轉出"
      },
      {
        "s": "转换并保存",
        "t": "轉換並儲存"
      },
      {
        "s": "转账",
        "t": "轉賬"
      },
      {
        "s": "转账贡献",
        "t": "轉賬貢獻"
      },
      {
        "s": "转账星币",
        "t": "轉賬星幣"
      },
      {
        "s": "自动保持游戏内最近 {0} 局",
        "t": "自動保持遊戲內最近 {0} 局"
      },
      {
        "s": "自动托管已{0}",
        "t": "自動託管已{0}"
      },
      {
        "s": "自动托管已开启（保留 {0} 局）",
        "t": "自動託管已開啟（保留 {0} 局）"
      },
      {
        "s": "总承伤",
        "t": "總承傷"
      },
      {
        "s": "总伤害",
        "t": "總傷害"
      },
      {
        "s": "最近一次 Mod 记录：{0} · {1} · 模组 {2}{3}。修改配置后，重启游戏并刷新可查看新结果。",
        "t": "最近一次 Mod 記錄：{0} · {1} · 模組 {2}{3}。修改配置後，重啟遊戲並重新整理可檢視新結果。"
      },
      {
        "s": "作者: {0}",
        "t": "作者: {0}"
      },
      {
        "s": "作者：{0}",
        "t": "作者：{0}"
      },
      {
        "s": "作者签名（可选）：",
        "t": "作者簽名（可選）："
      },
      {
        "s": "PNG/JPG 帧按文件名排序。待机 Idle、正向 Walk、背向 Walk-back；其余动作名以原角色为准。",
        "t": "PNG/JPG 幀按檔名排序。待機 Idle、正向 Walk、背向 Walk-back；其餘動作名以原角色為準。"
      },
      {
        "s": "Unity 结构 {0}",
        "t": "Unity 結構 {0}"
      },
      {
        "s": "version.dll 已停用或缺失，保留版本 {0}。点「恢复 version.dll」；本地文件缺失时下载对应版本发布包。",
        "t": "version.dll 已停用或缺失，保留版本 {0}。點「恢復 version.dll」；本地檔案缺失時下載對應版本釋出包。"
      },
      {
        "s": "- 初始候选池: {0}",
        "t": "- 初始候選池: {0}"
      },
      {
        "s": "- 刷新后呈现的新候选池: {0}",
        "t": "- 重新整理後呈現的新候選池: {0}"
      },
      {
        "s": "- 刷新情况: {0}",
        "t": "- 重新整理情況: {0}"
      },
      {
        "s": "- 动作类型: 【刷新候选】(第 {0} 次刷新)",
        "t": "- 動作型別: 【重新整理候選】(第 {0} 次重新整理)"
      },
      {
        "s": "- 参战角色: {0} (英雄ID: {1} · 账号等级: {2})",
        "t": "- 參戰角色: {0} (英雄ID: {1} · 賬號等級: {2})"
      },
      {
        "s": "- 回合统计: 共 {0} 回合 (包含 {1} 个行动轮次, 帧数 {2})",
        "t": "- 回合統計: 共 {0} 回合 (包含 {1} 個行動輪次, 幀數 {2})"
      },
      {
        "s": "- 回放文件: {0}",
        "t": "- 重播檔案: {0}"
      },
      {
        "s": "- 对局地图: {0} (地图ID: {1})",
        "t": "- 對局地圖: {0} (地圖ID: {1})"
      },
      {
        "s": "- 对局时间: {0} 至 {1} (时长: {2})",
        "t": "- 對局時間: {0} 至 {1} (時長: {2})"
      },
      {
        "s": "- 对局结果: {0} (最终决胜: {1})",
        "t": "- 對局結果: {0} (最終決勝: {1})"
      },
      {
        "s": "- 对局进程: {0}",
        "t": "- 對局程序: {0}"
      },
      {
        "s": "- 最终持有筹码清单: {0}",
        "t": "- 最終持有籌碼清單: {0}"
      },
      {
        "s": "- 最终状态: {0}",
        "t": "- 最終狀態: {0}"
      },
      {
        "s": "- 来源途径: {0} ({1})",
        "t": "- 來源途徑: {0} ({1})"
      },
      {
        "s": "- 棋盘移动: {0} 点 | 卡牌使用: {1} 张 | 技能释放: {2} 次",
        "t": "- 棋盤移動: {0} 點 | 卡牌使用: {1} 張 | 技能釋放: {2} 次"
      },
      {
        "s": "- 模式/难度: {0} / {1}",
        "t": "- 模式/難度: {0} / {1}"
      },
      {
        "s": "- 游戏版本: {0}",
        "t": "- 遊戲版本: {0}"
      },
      {
        "s": "- 玩家刷新候选次数: {0} 次",
        "t": "- 玩家重新整理候選次數: {0} 次"
      },
      {
        "s": "- 玩家阵亡次数: {0} 次",
        "t": "- 玩家陣亡次數: {0} 次"
      },
      {
        "s": "- 生命值: {0} | 拥有星币: {1} | 攻防属性: 攻击 {2} / 防御 {3}",
        "t": "- 生命值: {0} | 擁有星幣: {1} | 攻防屬性: 攻擊 {2} / 防禦 {3}"
      },
      {
        "s": "- 筹码地块购买次数: {0} 次 | 最终持有筹码数: {1} 个",
        "t": "- 籌碼地塊購買次數: {0} 次 | 最終持有籌碼數: {1} 個"
      },
      {
        "s": "- 筹码来源: {0}",
        "t": "- 籌碼來源: {0}"
      },
      {
        "s": "- 筹码流转记录总数: {0} 条 (最终选定 {1} 次，出现候选 {2} 次)",
        "t": "- 籌碼流轉記錄總數: {0} 條 (最終選定 {1} 次，出現候選 {2} 次)"
      },
      {
        "s": "- 获得筹码: 【{0}】 (品质: {1} · ID: {2} · {3})",
        "t": "- 獲得籌碼: 【{0}】 (品質: {1} · ID: {2} · {3})"
      },
      {
        "s": "- 输出伤害: {0} | 承受伤害: {1} | 击杀数: {2} | 治疗量: {3}",
        "t": "- 輸出傷害: {0} | 承受傷害: {1} | 擊殺數: {2} | 治療量: {3}"
      },
      {
        "s": "- 首领(BOSS): {0}",
        "t": "- 首領(BOSS): {0}"
      },
      {
        "s": "3. 【关键胜负手复盘】根据获胜者（{0}）与其他玩家的数据，分析获胜者主要是依靠哪几个核心筹码建立优势并最终斩获胜利的？",
        "t": "3. 【關鍵勝負手覆盤】根據獲勝者（{0}）與其他玩家的資料，分析獲勝者主要是依靠哪幾個核心籌碼建立優勢並最終斬獲勝利的？"
      },
      {
        "s": "CDN 上没有这个回放（HTTP 404）——ID 可能写错了，或者这局不在{0}。",
        "t": "CDN 上沒有這個重播（HTTP 404）——ID 可能寫錯了，或者這局不在{0}。"
      },
      {
        "s": "GIF 已保存：{0}",
        "t": "GIF 已儲存：{0}"
      },
      {
        "s": "ID {0} · 槽位 {1} · 账号等级 {2}",
        "t": "ID {0} · 槽位 {1} · 賬號等級 {2}"
      },
      {
        "s": "Steam 不可用（{0}），已直接启动游戏主程序。",
        "t": "Steam 不可用（{0}），已直接啟動遊戲主程式。"
      },
      {
        "s": "UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：{0}",
        "t": "UnityFS 大小宣告無效，或超過 2 GiB 無法讀入記憶體：{0}"
      },
      {
        "s": "UnityFS 头不完整：{0}",
        "t": "UnityFS 頭不完整：{0}"
      },
      {
        "s": "UnityFS 存在无法解释的尾部数据，拒绝修复：{0}",
        "t": "UnityFS 存在無法解釋的尾部資料，拒絕修復：{0}"
      },
      {
        "s": "UnityFS 版本头无效：{0}",
        "t": "UnityFS 版本頭無效：{0}"
      },
      {
        "s": "ZIP 存在重复路径：{0}",
        "t": "ZIP 存在重複路徑：{0}"
      },
      {
        "s": "ZIP 文件与目录路径冲突：{0}",
        "t": "ZIP 檔案與目錄路徑衝突：{0}"
      },
      {
        "s": "ZIP 文件长度不完整：{0}",
        "t": "ZIP 檔案長度不完整：{0}"
      },
      {
        "s": "ZIP 路径无效：{0}",
        "t": "ZIP 路徑無效：{0}"
      },
      {
        "s": "[事件 {0} | 帧 {1}] 玩家「{2}」（角色：{3}）",
        "t": "[事件 {0} | 幀 {1}] 玩家「{2}」（角色：{3}）"
      },
      {
        "s": "mod 不存在：{0}",
        "t": "mod 不存在：{0}"
      },
      {
        "s": "mod 包包含不安全的路径：{0}",
        "t": "mod 包包含不安全的路徑：{0}"
      },
      {
        "s": "mod 包包含重复文件：{0}",
        "t": "mod 包包含重複檔案：{0}"
      },
      {
        "s": "mod 包包含非法的文件名：{0}",
        "t": "mod 包包含非法的檔名：{0}"
      },
      {
        "s": "mod 包里有两个同名模组（{0}），已停止导入。",
        "t": "mod 包裡有兩個同名模組（{0}），已停止匯入。"
      },
      {
        "s": "mod 包里的 {0}.dll 没有成功写入，已停止导入。",
        "t": "mod 包裡的 {0}.dll 沒有成功寫入，已停止匯入。"
      },
      {
        "s": "version.dll 已删除，但 speedhack_config.json 删除失败：{0}",
        "t": "version.dll 已刪除，但 speedhack_config.json 刪除失敗：{0}"
      },
      {
        "s": "version.dll 已删除，但 {0} 目录删除失败：{1}（可能仍有文件被占用）。",
        "t": "version.dll 已刪除，但 {0} 目錄刪除失敗：{1}（可能仍有檔案被佔用）。"
      },
      {
        "s": "{0} {1} 个",
        "t": "{0} {1} 個"
      },
      {
        "s": "{0} {1} 次",
        "t": "{0} {1} 次"
      },
      {
        "s": "{0} · 卡面转换导入",
        "t": "{0} · 卡面轉換匯入"
      },
      {
        "s": "{0} · 服务器 {1}",
        "t": "{0} · 伺服器 {1}"
      },
      {
        "s": "{0} · 转换导入",
        "t": "{0} · 轉換匯入"
      },
      {
        "s": "{0} 个旧版本文件没能移入回收站，已保留",
        "t": "{0} 箇舊版本檔案沒能移入回收站，已保留"
      },
      {
        "s": "{0} 仍为禁用状态",
        "t": "{0} 仍為禁用狀態"
      },
      {
        "s": "{0} 元数据读取失败：{1}",
        "t": "{0} 後設資料讀取失敗：{1}"
      },
      {
        "s": "{0} 已装 v{1} → 包内 v{2}",
        "t": "{0} 已裝 v{1} → 包內 v{2}"
      },
      {
        "s": "{0} 帧 · {1} 种命令",
        "t": "{0} 幀 · {1} 種命令"
      },
      {
        "s": "{0}{1}。可将此皮肤包添加到人物方案。{2}{3}",
        "t": "{0}{1}。可將此造型包新增到人物方案。{2}{3}"
      },
      {
        "s": "{0}{1}，并在控制台窗口显示日志。",
        "t": "{0}{1}，並在控制檯視窗顯示日誌。"
      },
      {
        "s": "{0}如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。",
        "t": "{0}如果遊戲也讀不出來，別放回遊戲目錄——遊戲列到它會刪掉整個目錄。"
      },
      {
        "s": "{0}如果游戏也读不出来，它列到这份回放时会把整个目录删掉。确认要放回请再确认一次。",
        "t": "{0}如果遊戲也讀不出來，它列到這份重播時會把整個目錄刪掉。確認要放回請再確認一次。"
      },
      {
        "s": "{0}如确定要降级，请勾选「允许降级安装较旧的加载器」；通常更好的做法是改用较新版本的 AstralParty.Toys。",
        "t": "{0}如確定要降級，請勾選「允許降級安裝較舊的載入器」；通常更好的做法是改用較新版本的 AstralParty.Toys。"
      },
      {
        "s": "{0}已按请求的 ID 入库，需要的话可以改名后再放回游戏。",
        "t": "{0}已按請求的 ID 入庫，需要的話可以改名後再放回遊戲。"
      },
      {
        "s": "{0}确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。",
        "t": "{0}確認要換成 CDN 上這一份，請先刪掉庫裡的舊副本（或改用別的 ID）。"
      },
      {
        "s": "{0}继续保存新回放会失败。播放不受影响，建议归档几局。",
        "t": "{0}繼續儲存新重播會失敗。播放不受影響，建議歸檔幾局。"
      },
      {
        "s": "{0}请确认回放 ID 与区服是否对应。",
        "t": "{0}請確認重播 ID 與區服是否對應。"
      },
      {
        "s": "{0}（v{1}，版本未变）",
        "t": "{0}（v{1}，版本未變）"
      },
      {
        "s": "{0}（升级 v{1} → v{2}）",
        "t": "{0}（升級 v{1} → v{2}）"
      },
      {
        "s": "{0}（折后 {1}）",
        "t": "{0}（折後 {1}）"
      },
      {
        "s": "{0}（新装 v{1}）",
        "t": "{0}（新裝 v{1}）"
      },
      {
        "s": "{0}（版本未知）",
        "t": "{0}（版本未知）"
      },
      {
        "s": "{0}（降级 v{1} → v{2}）",
        "t": "{0}（降級 v{1} → v{2}）"
      },
      {
        "s": "{0}：下载到的 {1} 字节不是一个回放帧流（CDN 可能返回了错误页）。",
        "t": "{0}：下載到的 {1} 位元組不是一個重播幀流（CDN 可能返回了錯誤頁）。"
      },
      {
        "s": "{0}：下载失败（{1}）。",
        "t": "{0}：下載失敗（{1}）。"
      },
      {
        "s": "{0}：下载已取消，没有写入回放库。",
        "t": "{0}：下載已取消，沒有寫入重播庫。"
      },
      {
        "s": "{0}：从{1}下载到的是空文件，已放弃。",
        "t": "{0}：從{1}下載到的是空檔案，已放棄。"
      },
      {
        "s": "{0}：删除库内副本失败（{1}）。",
        "t": "{0}：刪除庫內副本失敗（{1}）。"
      },
      {
        "s": "{0}：删除游戏目录副本失败（{1}）。",
        "t": "{0}：刪除遊戲目錄副本失敗（{1}）。"
      },
      {
        "s": "{0}：回放库里已有同 ID 但内容不同的回放，为避免覆盖已中止。",
        "t": "{0}：重播庫裡已有同 ID 但內容不同的重播，為避免覆蓋已中止。"
      },
      {
        "s": "{0}：回放库里已经有同一份回放（{1}），本次下载已丢弃。",
        "t": "{0}：重播庫裡已經有同一份重播（{1}），本次下載已丟棄。"
      },
      {
        "s": "{0}：回放库里没有这个回放，已跳过。",
        "t": "{0}：重播庫裡沒有這個重播，已跳過。"
      },
      {
        "s": "{0}：已从{1}下载并存入回放库（{2}）。",
        "t": "{0}：已從{1}下載並存入重播庫（{2}）。"
      },
      {
        "s": "{0}：已从回放库删除{1}。",
        "t": "{0}：已從重播庫刪除{1}。"
      },
      {
        "s": "{0}：已从游戏目录删除{1}。",
        "t": "{0}：已從遊戲目錄刪除{1}。"
      },
      {
        "s": "{0}：已复制进库，但删除游戏目录副本失败（{1}）。库里有备份，可手动删除。",
        "t": "{0}：已複製進庫，但刪除遊戲目錄副本失敗（{1}）。庫裡有備份，可手動刪除。"
      },
      {
        "s": "{0}：已归档到回放库。",
        "t": "{0}：已歸檔到重播庫。"
      },
      {
        "s": "{0}：已放回游戏目录，游戏内「本地回放」可直接观看。",
        "t": "{0}：已放回遊戲目錄，遊戲內「本地重播」可直接觀看。"
      },
      {
        "s": "{0}：库里存在同 ID 但内容不同的回放，为避免覆盖已跳过（请手动处理）。",
        "t": "{0}：庫裡存在同 ID 但內容不同的重播，為避免覆蓋已跳過（請手動處理）。"
      },
      {
        "s": "{0}：库里已有同一份回放，已直接腾出游戏席位。",
        "t": "{0}：庫裡已有同一份重播，已直接騰出遊戲席位。"
      },
      {
        "s": "{0}：库里已经有这一局了。",
        "t": "{0}：庫裡已經有這一局了。"
      },
      {
        "s": "{0}：归档失败（{1}）。",
        "t": "{0}：歸檔失敗（{1}）。"
      },
      {
        "s": "{0}：放回失败（{1}）。",
        "t": "{0}：放回失敗（{1}）。"
      },
      {
        "s": "{0}：本工具解析不出它的结算帧（{1}），文件仍已原样保留。",
        "t": "{0}：本工具解析不出它的結算幀（{1}），檔案仍已原樣保留。"
      },
      {
        "s": "{0}：本工具解析不出它的结算帧（工具自带的协议版本可能比游戏旧）。",
        "t": "{0}：本工具解析不出它的結算幀（工具自帶的協議版本可能比遊戲舊）。"
      },
      {
        "s": "{0}：注意文件内记录的 ID 是 {1}，与请求的 ID 不同；",
        "t": "{0}：注意檔案內記錄的 ID 是 {1}，與請求的 ID 不同；"
      },
      {
        "s": "{0}：游戏目录里已有同 ID 但内容不同的文件，为避免覆盖已跳过。",
        "t": "{0}：遊戲目錄裡已有同 ID 但內容不同的檔案，為避免覆蓋已跳過。"
      },
      {
        "s": "{0}：游戏目录里已经有这一局了。",
        "t": "{0}：遊戲目錄裡已經有這一局了。"
      },
      {
        "s": "{0}：游戏目录里没有这个回放，已跳过。",
        "t": "{0}：遊戲目錄裡沒有這個重播，已跳過。"
      },
      {
        "s": "{0}：移除与目录块尾部完全相同的 {1} 个追加字节；原文件保存在 source/。",
        "t": "{0}：移除與目錄塊尾部完全相同的 {1} 個追加位元組；原檔案儲存在 source/。"
      },
      {
        "s": "、{0} 个动作（{1} 张精灵帧）",
        "t": "、{0} 個動作（{1} 張精靈幀）"
      },
      {
        "s": "【{0}】(共 {1} 条流转事件)",
        "t": "【{0}】(共 {1} 條流轉事件)"
      },
      {
        "s": "【玩家】{0} (ID: {1})",
        "t": "【玩家】{0} (ID: {1})"
      },
      {
        "s": "下载失败（{0}）请检查网络，或换一个区服试试（当前选的是{1}）。",
        "t": "下載失敗（{0}）請檢查網路，或換一個區服試試（當前選的是{1}）。"
      },
      {
        "s": "下载失败：HTTP {0}。",
        "t": "下載失敗：HTTP {0}。"
      },
      {
        "s": "下载失败：{0}",
        "t": "下載失敗：{0}"
      },
      {
        "s": "下载超过 {0} MB 上限，已中止。",
        "t": "下載超過 {0} MB 上限，已中止。"
      },
      {
        "s": "下载默认包失败：{0}",
        "t": "下載預設包失敗：{0}"
      },
      {
        "s": "不支持或重复的视频资源：{0}",
        "t": "不支援或重複的影片資源：{0}"
      },
      {
        "s": "不支持的 MonoBehaviour：{0}",
        "t": "不支援的 MonoBehaviour：{0}"
      },
      {
        "s": "不支持的角色资源类型：{0}",
        "t": "不支援的角色資源型別：{0}"
      },
      {
        "s": "不是 UnityFS bundle：{0}",
        "t": "不是 UnityFS bundle：{0}"
      },
      {
        "s": "主界面启动失败：{0}",
        "t": "主介面啟動失敗：{0}"
      },
      {
        "s": "事件 #{0}",
        "t": "事件 #{0}"
      },
      {
        "s": "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。{0}",
        "t": "僅 version.dll 已刪除，模組、配置、SDK 和日誌已保留。{0}"
      },
      {
        "s": "从本地游戏皮肤 {0} 导出的制作模板",
        "t": "從本地遊戲造型 {0} 匯出的製作模板"
      },
      {
        "s": "任务已完成，皮肤列表刷新失败：{0}",
        "t": "任務已完成，造型列表重新整理失敗：{0}"
      },
      {
        "s": "保存加载器设置失败：{0}",
        "t": "儲存載入器設定失敗：{0}"
      },
      {
        "s": "保存方案信息失败：{0}",
        "t": "儲存方案資訊失敗：{0}"
      },
      {
        "s": "保存配置失败：{0}",
        "t": "儲存配置失敗：{0}"
      },
      {
        "s": "保留了 {0} 的已有配置",
        "t": "保留了 {0} 的已有配置"
      },
      {
        "s": "倍速必须在 [{0},{1}] 之间（1.0 = 正常速度，不支持减速）。",
        "t": "倍速必須在 [{0},{1}] 之間（1.0 = 正常速度，不支援減速）。"
      },
      {
        "s": "写入游戏目录失败：{0}。",
        "t": "寫入遊戲目錄失敗：{0}。"
      },
      {
        "s": "切换 mod 状态失败：{0}",
        "t": "切換 mod 狀態失敗：{0}"
      },
      {
        "s": "切换方案失败：{0}",
        "t": "切換方案失敗：{0}"
      },
      {
        "s": "删除 version.dll 失败：{0}。",
        "t": "刪除 version.dll 失敗：{0}。"
      },
      {
        "s": "删除 {0} 失败：{1}（游戏运行时 DLL 可能被占用）。",
        "t": "刪除 {0} 失敗：{1}（遊戲執行時 DLL 可能被佔用）。"
      },
      {
        "s": "删除方案失败：{0}",
        "t": "刪除方案失敗：{0}"
      },
      {
        "s": "刷新 {0} 次后选择",
        "t": "重新整理 {0} 次後選擇"
      },
      {
        "s": "加载器发布包 {0}",
        "t": "載入器釋出包 {0}"
      },
      {
        "s": "加载器已就位，但这份安装没有版本清单 —— 重新安装一次就能纳入版本管理（{0}）。",
        "t": "載入器已就位，但這份安裝沒有版本清單 —— 重新安裝一次就能納入版本管理（{0}）。"
      },
      {
        "s": "加载器已就绪（{0}）。",
        "t": "載入器已就緒（{0}）。"
      },
      {
        "s": "加载器已就绪，与内置版本一致（{0}）。",
        "t": "載入器已就緒，與內建版本一致（{0}）。"
      },
      {
        "s": "加载器已更新{0}。",
        "t": "載入器已更新{0}。"
      },
      {
        "s": "动作名称无效或重复：{0}",
        "t": "動作名稱無效或重複：{0}"
      },
      {
        "s": "动作帧或帧率无效: {0}",
        "t": "動作幀或幀率無效: {0}"
      },
      {
        "s": "动作引用未知精灵：{0}",
        "t": "動作引用未知精靈：{0}"
      },
      {
        "s": "动作没有可转换的精灵帧：{0}",
        "t": "動作沒有可轉換的精靈幀：{0}"
      },
      {
        "s": "动态卡面导入失败：{0}",
        "t": "動態卡面匯入失敗：{0}"
      },
      {
        "s": "动态卡面转换失败：{0}",
        "t": "動態卡面轉換失敗：{0}"
      },
      {
        "s": "单个资源包超过 2 GiB，无法读入内存：{0}",
        "t": "單個資源包超過 2 GiB，無法讀入記憶體：{0}"
      },
      {
        "s": "卡面尺寸超出 Unity 上限（最长边 16384）：{0}",
        "t": "卡面尺寸超出 Unity 上限（最長邊 16384）：{0}"
      },
      {
        "s": "原精灵名称重复：{0}",
        "t": "原精靈名稱重複：{0}"
      },
      {
        "s": "发布包含有非法路径：{0}",
        "t": "釋出包含有非法路徑：{0}"
      },
      {
        "s": "发布包校验失败：{0} 哈希不一致。",
        "t": "釋出包校驗失敗：{0} 雜湊不一致。"
      },
      {
        "s": "发布包缺少文件：{0}",
        "t": "釋出包缺少檔案：{0}"
      },
      {
        "s": "发布包里的加载器（{0}）比游戏目录里已装的（{1}）更旧 —— 这是一次降级，已阻止。",
        "t": "釋出包裡的載入器（{0}）比遊戲目錄裡已裝的（{1}）更舊 —— 這是一次降級，已阻止。"
      },
      {
        "s": "变速器已安装，每次进入游戏将自动使用 {0} 倍速。",
        "t": "變速器已安裝，每次進入遊戲將自動使用 {0} 倍速。"
      },
      {
        "s": "变速基础倍率必须在 [{0},{1}] 之间（1.0 = 正常速度，不支持减速）。",
        "t": "變速基礎倍率必須在 [{0},{1}] 之間（1.0 = 正常速度，不支援減速）。"
      },
      {
        "s": "吉星派对 · {0}",
        "t": "吉星派對 · {0}"
      },
      {
        "s": "同名自定义包已存在: {0}",
        "t": "同名自定義包已存在: {0}"
      },
      {
        "s": "启动游戏失败：{0}",
        "t": "啟動遊戲失敗：{0}"
      },
      {
        "s": "命运 #{0}",
        "t": "命運 #{0}"
      },
      {
        "s": "回放 CDN 返回 HTTP {0} {1}",
        "t": "重播 CDN 返回 HTTP {0} {1}"
      },
      {
        "s": "回放 ID 不该超过 {0} 位（收到 {1} 位）。",
        "t": "重播 ID 不該超過 {0} 位（收到 {1} 位）。"
      },
      {
        "s": "回放列表读取失败：{0}",
        "t": "重播列表讀取失敗：{0}"
      },
      {
        "s": "回放库已切换到：{0}",
        "t": "重播庫已切換到：{0}"
      },
      {
        "s": "图片不存在: {0}",
        "t": "圖片不存在: {0}"
      },
      {
        "s": "地图事件 #{0}",
        "t": "地圖事件 #{0}"
      },
      {
        "s": "媒体文件不存在: {0}",
        "t": "媒體檔案不存在: {0}"
      },
      {
        "s": "存在重复卡牌资源名：{0}",
        "t": "存在重複卡牌資源名：{0}"
      },
      {
        "s": "导入失败：{0}",
        "t": "匯入失敗：{0}"
      },
      {
        "s": "导入旧式卡牌皮肤包失败：{0}",
        "t": "匯入舊式卡牌造型包失敗：{0}"
      },
      {
        "s": "导入皮肤失败: {0}",
        "t": "匯入造型失敗: {0}"
      },
      {
        "s": "导出失败: {0}",
        "t": "匯出失敗: {0}"
      },
      {
        "s": "导出失败：{0}",
        "t": "匯出失敗：{0}"
      },
      {
        "s": "导出所选皮肤 {0} 的图片",
        "t": "匯出所選造型 {0} 的圖片"
      },
      {
        "s": "已从{0}恢复 version.dll（{1}），重启游戏后生效。",
        "t": "已從{0}恢復 version.dll（{1}），重啟遊戲後生效。"
      },
      {
        "s": "已保存配置：{0}",
        "t": "已儲存配置：{0}"
      },
      {
        "s": "已保留 {0} 个视频源资源，视频演出仍使用原版。",
        "t": "已保留 {0} 個影片源資源，影片演出仍使用原版。"
      },
      {
        "s": "已创建新皮肤方案: {0}",
        "t": "已建立新造型方案: {0}"
      },
      {
        "s": "已删除 mod：{0}",
        "t": "已刪除 mod：{0}"
      },
      {
        "s": "已删除方案: {0}",
        "t": "已刪除方案: {0}"
      },
      {
        "s": "已加载 {0} 个模组",
        "t": "已載入 {0} 個模組"
      },
      {
        "s": "已启用 mod：{0}（重启游戏后生效）",
        "t": "已啟用 mod：{0}（重啟遊戲後生效）"
      },
      {
        "s": "已启用变速：{0}x（重启游戏生效）",
        "t": "已啟用變速：{0}x（重啟遊戲生效）"
      },
      {
        "s": "已处理 {0} 个资源包的重复尾部数据，原文件已保留。",
        "t": "已處理 {0} 個資源包的重複尾部資料，原檔案已保留。"
      },
      {
        "s": "已安装（{0}），配置修改后重启游戏生效。",
        "t": "已安裝（{0}），配置修改後重啟遊戲生效。"
      },
      {
        "s": "已导入 {0} 个模组：{1}——重启游戏后生效",
        "t": "已匯入 {0} 個模組：{1}——重啟遊戲後生效"
      },
      {
        "s": "已导入 {0} 到回放库。",
        "t": "已匯入 {0} 到重播庫。"
      },
      {
        "s": "已导入 {0}，重启游戏后生效。",
        "t": "已匯入 {0}，重啟遊戲後生效。"
      },
      {
        "s": "已导出 {0} / {1} 张动画帧",
        "t": "已匯出 {0} / {1} 張動畫幀"
      },
      {
        "s": "已导出到 {0}",
        "t": "已匯出到 {0}"
      },
      {
        "s": "已归档 {0} 局最旧的回放，游戏内席位腾出 {1} 个。",
        "t": "已歸檔 {0} 局最舊的重播，遊戲內席位騰出 {1} 個。"
      },
      {
        "s": "已成功导出商店记录到 {0}",
        "t": "已成功匯出商店記錄到 {0}"
      },
      {
        "s": "已成功导出筹码记录到 {0}",
        "t": "已成功匯出籌碼記錄到 {0}"
      },
      {
        "s": "已扫描 {0} 个目录…",
        "t": "已掃描 {0} 個目錄…"
      },
      {
        "s": "已把 %AppData% 里的旧配置复制过来：{0}。",
        "t": "已把 %AppData% 裡的舊配置複製過來：{0}。"
      },
      {
        "s": "已提取 {0} 个图片入口、{1} 个动作、{2} 帧",
        "t": "已提取 {0} 個圖片入口、{1} 個動作、{2} 幀"
      },
      {
        "s": "已添加并切换到：{0}",
        "t": "已新增並切換到：{0}"
      },
      {
        "s": "已添加：{0}",
        "t": "已新增：{0}"
      },
      {
        "s": "已用默认编辑器打开配置：{0}",
        "t": "已用預設編輯器開啟配置：{0}"
      },
      {
        "s": "已用默认编辑器打开：{0}",
        "t": "已用預設編輯器開啟：{0}"
      },
      {
        "s": "已禁用 mod：{0}（重启游戏后生效）",
        "t": "已禁用 mod：{0}（重啟遊戲後生效）"
      },
      {
        "s": "已自动定位游戏目录：{0}",
        "t": "已自動定位遊戲目錄：{0}"
      },
      {
        "s": "已记住这个选择，但同步加载器配置失败：{0}",
        "t": "已記住這個選擇，但同步載入器配置失敗：{0}"
      },
      {
        "s": "已转换动画帧 {0} / {1}",
        "t": "已轉換動畫幀 {0} / {1}"
      },
      {
        "s": "已转换并导入 {0} 张卡面。预览后点击“设为当前”启用。",
        "t": "已轉換並匯入 {0} 張卡面。預覽後點選“設為當前”啟用。"
      },
      {
        "s": "已转换并导入 {0} 张图片",
        "t": "已轉換並匯入 {0} 張圖片"
      },
      {
        "s": "已还原卡面为默认：{0} ({1})",
        "t": "已還原卡面為預設：{0} ({1})"
      },
      {
        "s": "已选择卡面方案：{0}，完全退出并重启游戏后生效。",
        "t": "已選擇卡面方案：{0}，完全退出並重啟遊戲後生效。"
      },
      {
        "s": "已选择游戏目录：{0}",
        "t": "已選擇遊戲目錄：{0}"
      },
      {
        "s": "异画 #{0}",
        "t": "異畫 #{0}"
      },
      {
        "s": "忽略了 {0} 个非模组文件",
        "t": "忽略了 {0} 個非模組檔案"
      },
      {
        "s": "恢复默认包失败：{0}",
        "t": "恢復預設包失敗：{0}"
      },
      {
        "s": "成功导入皮肤方案: {0}",
        "t": "成功匯入造型方案: {0}"
      },
      {
        "s": "手牌 #{0}",
        "t": "手牌 #{0}"
      },
      {
        "s": "打不开目录：{0}",
        "t": "打不開目錄：{0}"
      },
      {
        "s": "打不开系统浏览器：{0}",
        "t": "打不開系統瀏覽器：{0}"
      },
      {
        "s": "打开库目录失败：{0}",
        "t": "開啟庫目錄失敗：{0}"
      },
      {
        "s": "打开目录失败：{0}",
        "t": "開啟目錄失敗：{0}"
      },
      {
        "s": "打开配置失败：{0}",
        "t": "開啟配置失敗：{0}"
      },
      {
        "s": "扫描出错：{0}",
        "t": "掃描出錯：{0}"
      },
      {
        "s": "技能视频不是可识别的 CRI USM：{0}",
        "t": "技能影片不是可識別的 CRI USM：{0}"
      },
      {
        "s": "把 {0} 个旧版本文件移入了回收站",
        "t": "把 {0} 箇舊版本檔案移入了回收站"
      },
      {
        "s": "操作失败：{0}",
        "t": "操作失敗：{0}"
      },
      {
        "s": "整理完成：游戏目录保留最新 {0} 局，归档 {1} 局。",
        "t": "整理完成：遊戲目錄保留最新 {0} 局，歸檔 {1} 局。"
      },
      {
        "s": "文件不存在：{0}",
        "t": "檔案不存在：{0}"
      },
      {
        "s": "文案槽位或长度无效: {0}",
        "t": "文案槽位或長度無效: {0}"
      },
      {
        "s": "文档目录不可写（{0}），配置暂时保存在 {1}。",
        "t": "文件目錄不可寫（{0}），配置暫時儲存在 {1}。"
      },
      {
        "s": "新建方案失败: {0}",
        "t": "新建方案失敗: {0}"
      },
      {
        "s": "方案共享图片冲突: {0}",
        "t": "方案共享圖片衝突: {0}"
      },
      {
        "s": "方案共享文案冲突: {0}",
        "t": "方案共享文案衝突: {0}"
      },
      {
        "s": "方案操作失败：{0}",
        "t": "方案操作失敗：{0}"
      },
      {
        "s": "方案缺少自定义包: {0}",
        "t": "方案缺少自定義包: {0}"
      },
      {
        "s": "无法下载当前版本 {0} 的加载器包：HTTP {1}。可重试或使用更新功能安装新版。",
        "t": "無法下載當前版本 {0} 的載入器包：HTTP {1}。可重試或使用更新功能安裝新版。"
      },
      {
        "s": "无法完整读取卡牌资源包 {0}：{1}",
        "t": "無法完整讀取卡牌資源包 {0}：{1}"
      },
      {
        "s": "无法完整读取旧资源包 {0}：{1}",
        "t": "無法完整讀取舊資源包 {0}：{1}"
      },
      {
        "s": "无法获取 FFmpeg。可将 ffmpeg.exe 放到程序旁 tools 目录，或设置 ASTRAL_FFMPEG_PATH。{0}",
        "t": "無法獲取 FFmpeg。可將 ffmpeg.exe 放到程式旁 tools 目錄，或設定 ASTRAL_FFMPEG_PATH。{0}"
      },
      {
        "s": "无法读取 bundle 内的资源文件：{0}",
        "t": "無法讀取 bundle 內的資原始檔：{0}"
      },
      {
        "s": "无法读取视频首帧：{0}",
        "t": "無法讀取影片首幀：{0}"
      },
      {
        "s": "更新失败：{0}",
        "t": "更新失敗：{0}"
      },
      {
        "s": "替换卡面失败：{0}",
        "t": "替換卡面失敗：{0}"
      },
      {
        "s": "未找到 mod：{0}",
        "t": "未找到 mod：{0}"
      },
      {
        "s": "未知动画附属 MonoBehaviour，不能部分导入：{0}",
        "t": "未知動畫附屬 MonoBehaviour，不能部分匯入：{0}"
      },
      {
        "s": "未知图片槽位: {0}",
        "t": "未知圖片槽位: {0}"
      },
      {
        "s": "未知视频实现：{0}",
        "t": "未知影片實現：{0}"
      },
      {
        "s": "来源皮肤 {0} 的图片或动画未完整定位，请先在游戏中查看该皮肤以下载资源",
        "t": "來源造型 {0} 的圖片或動畫未完整定位，請先在遊戲中檢視該造型以下載資源"
      },
      {
        "s": "模组包比游戏目录里已装的更旧：{0}。确认要降级后再导入（玩家配置与停用状态仍会保留）。",
        "t": "模組包比遊戲目錄裡已裝的更舊：{0}。確認要降級後再匯入（玩家配置與停用狀態仍會保留）。"
      },
      {
        "s": "正在深度扫描 {0} …",
        "t": "正在深度掃描 {0} …"
      },
      {
        "s": "正在转换：{0} / {1} 帧上限",
        "t": "正在轉換：{0} / {1} 幀上限"
      },
      {
        "s": "此前累计刷新了 {0} 次后才确认选择",
        "t": "此前累計重新整理了 {0} 次後才確認選擇"
      },
      {
        "s": "注意：现在游戏目录里有 {0} 局，超过游戏上限 {1}，",
        "t": "注意：現在遊戲目錄裡有 {0} 局，超過遊戲上限 {1}，"
      },
      {
        "s": "深度扫描出错：{0}",
        "t": "深度掃描出錯：{0}"
      },
      {
        "s": "游戏目录不存在：{0}",
        "t": "遊戲目錄不存在：{0}"
      },
      {
        "s": "游戏目录里只有 {0} 局，未超过保留名额 {1}，无需整理。",
        "t": "遊戲目錄裡只有 {0} 局，未超過保留名額 {1}，無需整理。"
      },
      {
        "s": "游戏目录里已装的加载器（{0}）比本程序内置的（{1}）更新 —— 这是一次降级，已阻止。",
        "t": "遊戲目錄裡已裝的載入器（{0}）比本程式內建的（{1}）更新 —— 這是一次降級，已阻止。"
      },
      {
        "s": "生成 GIF：{0} / {1} 帧",
        "t": "生成 GIF：{0} / {1} 幀"
      },
      {
        "s": "皮肤不属于当前角色: {0}",
        "t": "造型不屬於當前角色: {0}"
      },
      {
        "s": "皮肤方案不存在: {0}",
        "t": "造型方案不存在: {0}"
      },
      {
        "s": "目标方案已存在: {0}",
        "t": "目標方案已存在: {0}"
      },
      {
        "s": "移除动态卡面失败：{0}",
        "t": "移除動態卡面失敗：{0}"
      },
      {
        "s": "第 {0} 档",
        "t": "第 {0} 檔"
      },
      {
        "s": "第 {0} 次刷新",
        "t": "第 {0} 次重新整理"
      },
      {
        "s": "第{0}格已售出",
        "t": "第{0}格已售出"
      },
      {
        "s": "精灵名称无效或重复：{0}",
        "t": "精靈名稱無效或重複：{0}"
      },
      {
        "s": "纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：{0}",
        "t": "紋理尺寸超出 Unity 上限（最長邊 16384）或解碼資料不完整：{0}"
      },
      {
        "s": "纹理资源名为空或重复：{0}",
        "t": "紋理資源名為空或重複：{0}"
      },
      {
        "s": "自定义包被皮肤方案引用，请先移除方案规则: {0}",
        "t": "自定義包被造型方案引用，請先移除方案規則: {0}"
      },
      {
        "s": "自定义包角色与规则不匹配: {0}",
        "t": "自定義包角色與規則不匹配: {0}"
      },
      {
        "s": "获取卡面失败：{0}",
        "t": "獲取卡面失敗：{0}"
      },
      {
        "s": "规则标识重复: {0}",
        "t": "規則標識重複: {0}"
      },
      {
        "s": "视频资源引用结构无效：{0}",
        "t": "影片資源引用結構無效：{0}"
      },
      {
        "s": "视频转换失败：{0}",
        "t": "影片轉換失敗：{0}"
      },
      {
        "s": "角色 {0} 中不存在指定来源皮肤 {1}，无法导出；不会回退到默认皮肤",
        "t": "角色 {0} 中不存在指定來源造型 {1}，無法匯出；不會回退到預設造型"
      },
      {
        "s": "角色 {0} 没有可导出的皮肤配置",
        "t": "角色 {0} 沒有可匯出的造型配置"
      },
      {
        "s": "角色不存在: {0}",
        "t": "角色不存在: {0}"
      },
      {
        "s": "角色皮肤操作失败：{0}",
        "t": "角色造型操作失敗：{0}"
      },
      {
        "s": "设置变速失败：{0}",
        "t": "設定變速失敗：{0}"
      },
      {
        "s": "读取 {0} 个原资源包",
        "t": "讀取 {0} 個原資源包"
      },
      {
        "s": "读取变速器状态失败：{0}",
        "t": "讀取變速器狀態失敗：{0}"
      },
      {
        "s": "读取模组状态失败：{0}",
        "t": "讀取模組狀態失敗：{0}"
      },
      {
        "s": "读取游戏位置失败：{0}",
        "t": "讀取遊戲位置失敗：{0}"
      },
      {
        "s": "读取状态失败：{0}",
        "t": "讀取狀態失敗：{0}"
      },
      {
        "s": "读取皮肤方案失败: {0}",
        "t": "讀取造型方案失敗: {0}"
      },
      {
        "s": "资源不是支持的卡牌贴图，拒绝部分导入：{0}",
        "t": "資源不是支援的卡牌貼圖，拒絕部分匯入：{0}"
      },
      {
        "s": "资源包没有可转换的 Texture2D：{0}",
        "t": "資源包沒有可轉換的 Texture2D：{0}"
      },
      {
        "s": "资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：{0}",
        "t": "資源對應多個角色或造型，無法自動識別；請使用包含明確目標立繪的造型包。匹配結果：{0}"
      },
      {
        "s": "转换卡面 {0} / {1}：{2}",
        "t": "轉換卡面 {0} / {1}：{2}"
      },
      {
        "s": "转换资源 {0} / {1}：{2}",
        "t": "轉換資源 {0} / {1}：{2}"
      },
      {
        "s": "还原卡面失败：{0}",
        "t": "還原卡面失敗：{0}"
      },
      {
        "s": "这个文件有 {0} MB，超过 {1} MB 上限，已中止。",
        "t": "這個檔案有 {0} MB，超過 {1} MB 上限，已中止。"
      },
      {
        "s": "选择替换卡面图片 ({0})",
        "t": "選擇替換卡面圖片 ({0})"
      },
      {
        "s": "配置保存在「文档\\{0}」（和回放库同一个文件夹）：主配置、游戏目录记忆、回放库设置都在这里。",
        "t": "配置儲存在「文件\\{0}」（和重播庫同一個資料夾）：主配置、遊戲目錄記憶、重播庫設定都在這裡。"
      },
      {
        "s": "配置已保存并同步到游戏目录；下次进入游戏自动使用 {0} 倍速。",
        "t": "配置已儲存並同步到遊戲目錄；下次進入遊戲自動使用 {0} 倍速。"
      },
      {
        "s": "（内置：{0}）",
        "t": "（內建：{0}）"
      },
      {
        "s": "；缺失：{0}",
        "t": "；缺失：{0}"
      }
    ]
  };
})();
