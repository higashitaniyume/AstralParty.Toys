# Linux 安装部署指南（Proton，实验性）

AstralParty.Toys 是基于 WPF 和 WebView2 的 Windows 应用。本文通过 Proton 运行 Windows 发布包，**不提供原生 Linux 构建，也不代表 Linux 已成为官方支持的平台**。

以下步骤来自一次社区实测安装。其他发行版、Proton 构建、软件版本和 Steam 安装方式的表现可能不同。

## 已验证范围与准备工作

- x86_64 Arch Linux，原生安装的 Steam 客户端，niri 和 xwayland-satellite。
- AstralParty.Toys **v2.3.3**，使用 `win-x64-self-contained-portable.zip`。
- CesiumLoader **3.8.3**，Steam 国服《吉星派对》，App ID 为 **2622000**，主程序为 `AstralParty_CN.exe`。
- Steam 提供的 **Proton - Experimental** 和 `SteamLinuxRuntime_4`。
- 工具箱独立的 Wine 前缀（prefix）内安装 Microsoft Edge WebView2 Evergreen **157.0.4341.60**。
- Bash、`curl` 和 `unzip`；如需创建应用菜单入口，可用 `desktop-file-validate` 检查桌面文件。

实测中，从工具箱按钮交给 Steam 启动游戏后，工具箱与游戏可以同时运行；本次新生成的加载器日志显示 23 个模组加载成功、0 个失败。这只验证了启动流程，没有逐项验证回放、编辑器、联机或所有模组功能。**v2.4.0 及后续版本尚未按此流程验证**；Flatpak/Snap Steam、ARM 和 TapTap 也不在已验证范围内。

先安装游戏及上述 Steam 工具，在游戏的 Steam 兼容性设置中选择 Proton - Experimental，通过 Steam 启动一次未安装模组的游戏，再正常退出。配置期间保持 Steam 客户端运行。

**安装、更新、卸载或修改模组配置前，务必完全退出游戏。** 使用会移动回放或管理账号档案的功能前，另行备份游戏数据。第三方模组仍需遵守游戏规则，本文不保证反作弊兼容性。

## 1. 确认路径并解压工具箱

示例使用 `~/.local/share/Steam` 下的原生 Steam 及其默认库。请按实际安装位置调整变量。游戏主程序所在的子目录可能变化，可从 Steam 的「管理 → 浏览本地文件」查找；`game_dir` 应指向包含 `AstralParty_CN.exe` 的目录，而不是它的上一级。

下载 [v2.3.3 自包含便携版](https://github.com/higashitaniyume/AstralParty.Toys/releases/tag/v2.3.3)和 [CesiumLoader 3.8.3](https://github.com/higashitaniyume/CesiumLoader/releases/tag/modloader-3.8.3)。命令假定文件保存在 `~/Downloads`，如下载位置不同，请修改对应路径。工具箱使用新建目录，不要直接覆盖已有安装及其数据。

以下配置命令需在**同一个 Bash 会话**中执行：

```bash
steam_dir="$HOME/.local/share/Steam"
game_library="$steam_dir"
app_dir="$HOME/.local/share/astralparty-toys"
game_dir="$game_library/steamapps/common/Astral Party/8vJXn6CN"
game_compat="$game_library/steamapps/compatdata/2622000"
proton="$steam_dir/steamapps/common/Proton - Experimental/proton"
runtime="$steam_dir/steamapps/common/SteamLinuxRuntime_4/_v2-entry-point"

mkdir -p "$app_dir" "$app_dir/compatdata"
unzip "$HOME/Downloads/AstralParty.Toys-v2.3.3-win-x64-self-contained-portable.zip" \
  -d "$app_dir"
```

解压后，`AstralParty.Toys.exe` 应直接位于 `app_dir` 内。自包含发布包自带 .NET，但不包含 WebView2。

定义以下临时函数，供后续配置命令使用。它通过与启动器相同的 Proton 和 Steam Runtime 运行 Windows 工具：

```bash
run_in_prefix() {
  local compat_dir="$1" install_dir="$2"
  shift 2
  STEAM_COMPAT_CLIENT_INSTALL_PATH="$steam_dir" \
  STEAM_COMPAT_DATA_PATH="$compat_dir" \
  STEAM_COMPAT_INSTALL_PATH="$install_dir" \
  STEAM_COMPAT_APP_ID=0 SteamAppId=0 SteamGameId=0 \
    "$runtime" --verb=run -- "$proton" run "$@"
}
```

## 2. 创建独立前缀并安装 WebView2

**不要**把工具箱放在游戏的 `steamapps/compatdata/2622000` 前缀中运行。Steam 启动游戏前可能等待该 Wine 前缀内已有的进程退出，工具箱一直开着就可能阻塞游戏启动。本文为工具箱单独使用 `$app_dir/compatdata`，保留 Steam 原有的游戏前缀。

下载微软提供的 [WebView2 Evergreen 独立 x64 安装包](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section)，安装到工具箱前缀：

```bash
webview_installer="$HOME/Downloads/MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
curl --http1.1 --fail --location \
  'https://go.microsoft.com/fwlink/p/?LinkId=2124701' \
  --output "$webview_installer"

run_in_prefix "$app_dir/compatdata" "$app_dir" \
  "$webview_installer" /silent /install
```

Evergreen 下载地址提供的版本可能与实测版本不同。实测环境下载时需要使用 HTTP/1.1；下载失败时不要通过关闭证书校验来绕过问题。

保留 Proton 默认的应用兼容性设置。实测中，将 `msedgewebview2.exe` 强制设为 Windows 10 后出现过界面空白，恢复 Proton 默认覆盖设置后可再次显示界面。不要给整个前缀统一覆盖 Windows 版本。

## 3. 关联 Steam 与现有游戏数据

### Steam URL 协议

工具箱的正常 Steam 启动方式使用 `steam://rungameid/2622000`。独立前缀中的 Windows Steam 入口并不等同于宿主上的原生 Steam 客户端。**仅在工具箱前缀内**注册以下 URI 处理方式，让 Wine 将请求交给宿主的浏览器／URL 集成：

```bash
run_in_prefix "$app_dir/compatdata" "$app_dir" reg.exe add \
  'HKCU\Software\Classes\steam' /ve /t REG_SZ /d 'URL:Steam Protocol' /f
run_in_prefix "$app_dir/compatdata" "$app_dir" reg.exe add \
  'HKCU\Software\Classes\steam' /v 'URL Protocol' /t REG_SZ /d '' /f
run_in_prefix "$app_dir/compatdata" "$app_dir" reg.exe add \
  'HKCU\Software\Classes\steam\shell\open\command' /ve /t REG_SZ \
  /d '"C:\windows\system32\winebrowser.exe" "%1"' /f
```

宿主需要已将 Steam URL 关联到原生 Steam。可执行：

```bash
xdg-mime query default x-scheme-handler/steam
```

实测系统返回 `steam.desktop`。如果没有注册 Steam 处理程序，请先修复发行版的 Steam 桌面集成，再测试按钮；不需要为此修改游戏的 Steam 启动选项。

### 现有存档、账号与回放缓存

独立前缀有自己的 Windows 用户数据目录。只将游戏原有的 `LocalLow/feimo` 链接到工具箱预期的位置，不复制存档，也不共享整个前缀：

```bash
game_data="$game_compat/pfx/drive_c/users/steamuser/AppData/LocalLow/feimo"
tools_local_low="$app_dir/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow"

mkdir -p "$tools_local_low"
ln -s "$game_data" "$tools_local_low/feimo"
```

源目录 `game_data` 必须已经存在。如果目标位置已有 `feimo`，请先停止并检查内容，不要为了执行命令而覆盖或删除现有档案。本文适用于新建安装，不会自动迁移其他工具箱前缀中的数据。

**此链接让工具箱直接访问真实游戏数据，并非复制副本。** 账号档案操作和回放管理可能影响游戏文件；配置和管理数据时保持游戏关闭。工具箱自己的普通数据仍单独位于其前缀内的 `drive_c/users/steamuser/Documents/AstralParty.Toys`。

## 4. 安装加载器并设置游戏专用 DLL 覆盖

已有 CesiumLoader 时，跳过下面的新安装解压步骤，在游戏完全退出后使用工具箱的常规加载器管理功能。不要覆盖属于其他加载器的 `version.dll`。

实测的新安装将加载器组件解压到游戏主程序旁，保留压缩包内的目录结构，再将发布清单移入加载器目录，让工具箱识别归属和版本：

```bash
unzip "$HOME/Downloads/cesium-loader-3.8.3.zip" \
  'version.dll' 'AstralParty_ModLoader/*' 'cesium-loader.json' \
  'CesiumLoader-LICENSE.txt' 'CesiumLoader-NOTICE.txt' \
  -d "$game_dir"
mv "$game_dir/cesium-loader.json" \
  "$game_dir/AstralParty_ModLoader/cesium-loader.json"
```

可选皮肤资源单独分发，见 [CesiumLoader 文档](https://github.com/higashitaniyume/CesiumLoader)。基础加载器压缩包已足够完成加载器初始化。

为此游戏主程序设置原生 `version.dll` 优先加载。与安装 WebView2 和注册 Steam 协议不同，这条命令作用于**游戏前缀**：

```bash
run_in_prefix "$game_compat" "$game_dir" reg.exe add \
  'HKCU\Software\Wine\AppDefaults\AstralParty_CN.exe\DllOverrides' \
  /v version /t REG_SZ /d native,builtin /f
```

该覆盖只针对这个 EXE，不会替换已有的 Steam 启动选项，也不会应用到其他程序。首次测试时，保持加载器的 Steam/TapTap 绕过开关关闭，使用默认账号档案，基础倍率设为 1.0。

## 5. 创建启动器

创建 `~/.local/bin/astralparty-toys`。如果前面调整了 `app_dir`、`steam_dir` 或 Steam 工具位置，也需修改脚本内的对应路径。

```bash
mkdir -p "$HOME/.local/bin"
cat > "$HOME/.local/bin/astralparty-toys" <<'SH'
#!/usr/bin/env bash
set -euo pipefail

app_dir="$HOME/.local/share/astralparty-toys"
steam_dir="$HOME/.local/share/Steam"
cd "$app_dir"

export STEAM_COMPAT_CLIENT_INSTALL_PATH="$steam_dir"
export STEAM_COMPAT_DATA_PATH="$app_dir/compatdata"
export STEAM_COMPAT_INSTALL_PATH="$app_dir"
export STEAM_COMPAT_APP_ID=0 SteamAppId=0 SteamGameId=0
export WINEDEBUG=-all

# Use Wine's D3D9 for WPF; DXVK turned the window black after tiling.
export WINEDLLOVERRIDES="${WINEDLLOVERRIDES:+${WINEDLLOVERRIDES};}d3d9=b"
exec "$steam_dir/steamapps/common/SteamLinuxRuntime_4/_v2-entry-point" --verb=run -- \
    "$steam_dir/steamapps/common/Proton - Experimental/proton" run \
    "$app_dir/AstralParty.Toys.exe" "$@"
SH
chmod +x "$HOME/.local/bin/astralparty-toys"
```

`d3d9=b` 仅作用于工具箱进程树。实测中，它解决了窗口平铺或调整大小后 WPF 界面变黑的问题，不改变游戏的渲染配置。保留 `proton run`，不要改成 `runinprefix` 或混用其他 Wine 安装。

如需应用菜单入口，可创建：

```bash
mkdir -p "$HOME/.local/share/applications"
cat > "$HOME/.local/share/applications/astralparty-toys.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=AstralParty.Toys
Comment=Astral Party replay and mod toolbox (Proton)
Exec="$HOME/.local/bin/astralparty-toys"
Icon=applications-games
Terminal=false
Categories=Game;
EOF
```

如已安装 `desktop-file-validate`，可用它检查此桌面文件；也可直接运行 `~/.local/bin/astralparty-toys`。

## 6. 选择游戏并验证本次启动

1. 打开工具箱，添加或选择包含 `AstralParty_CN.exe` 的游戏目录。从 Wine 的 **Z:** 盘进入 `game_dir` 对应的 Linux 路径。不要照搬游戏前缀中的 **S:** 盘路径：两个前缀的盘符映射可能不同，Windows Steam 库的自动检测也不一定能找到原生 Steam。
2. 确认工具箱能识别 CesiumLoader 及其版本。
3. 在「概览」选择「从 **Steam** 启动」，点击工具箱的「**启动游戏**」按钮。本次测试不要使用 Steam/TapTap 绕过方式。
4. 保持工具箱打开，确认 Steam 创建了新的游戏进程，且实际游戏窗口到达开始界面。
5. 检查 `AstralParty_ModLoader/logs/cesium-loader-*.log` 下**时间对应本次启动的新日志**。日志应记录原生 `version.dll` 已加载、绕过开关关闭，启用的模组加载成功。旧会话的成功日志不能证明这次启动成功。

仅收到 Steam URL 不代表游戏已经启动。Steam 的 `logs/console_log.txt` 可区分 URI 接收、`CreatingProcess` 和 `WaitingGameWindow` 等阶段，还需确认实际游戏窗口。

## 已知限制与排障

- **游戏运行时，工具箱可能仍显示未运行。** 一个 Wine 前缀中的 Windows 进程枚举不能可靠识别另一个前缀里的游戏。不要把此状态当作安全检查：修改加载器／模组文件或管理游戏数据前，务必完全退出游戏。
- **X11 显示器或输入错误可能导致工具箱退出。** 在 niri/xwayland-satellite 下观察到过 RANDR `RRGetCrtcInfo` 的 `BadRRCrtc`，以及 `XI_BadDevice`，部分发生在长时间运行之后。崩溃后可重新打开工具箱；反馈时请提供合成器、显示器布局／缩放及近期显示器或输入设备变化。具体触发条件尚未确认，本文并未永久修复这些故障，也不要求全局关闭合成器或 GPU。
- **工具箱空白或黑屏不等于加载器失效。** 检查工具箱专用的 `d3d9=b`，保留 Proton 默认的 WebView2 兼容性设置。WebView2 必须安装在工具箱前缀中，仅装在游戏前缀里不够。
- **按钮已交给 Steam，但没有游戏窗口。** 如果 Steam 一直停在 `CheckShaderDepotManifest`，没有创建游戏进程，请先正常退出其他游戏，再完全退出并重新打开 Steam，然后重试工具箱按钮。已有 Steam 会话中曾出现此问题，重启 Steam 后成功启动了游戏。不要通过启用 Steam 绕过或反复重装加载器来处理。

## 更新与卸载

按[常规更新说明](../README.md#安装)操作，保留独立前缀及其中的工具箱数据。更换版本后需要重新验证兼容性；上述结果不保证未来的 Proton、WebView2 或工具箱版本也能工作。

卸载加载器前先退出游戏，再使用工具箱的常规卸载功能。「停用」与「卸载」的区别见[加载器恢复说明](loader-recovery.md)。删除前面添加的 EXE 专用 `version` 注册表值；如果此前已有该值，则恢复原值。应用菜单入口和 shell 启动器可以单独移除。

删除工具箱目录前，先保留其前缀中需要的回放库和配置。**不要**因卸载工具箱而删除 Steam 的 `compatdata/2622000` 或游戏原始 `LocalLow/feimo` 目录；前面的数据链接并不是备份。
