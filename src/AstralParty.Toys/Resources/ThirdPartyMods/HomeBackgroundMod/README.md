# HomeBackgroundMod（第三方模组，随工具箱分发，已修改）

- 原作者：TenkinMori
- 源码：https://github.com/TenkinMori/astralparty-homebg-cesium （MIT，见同目录 `LICENSE`）
- 版本：1.1.0.1 = 原版 1.1.0（commit `3d4887b`，Release `v1.1.0-pc`）+ 工具箱的修改
- SHA-256：`8f7781adb8d485e0924ab6573df4021b27950feb228968dce299e218c0600d5b`
  （原版 1.1.0 是 `474e6aebfcfed3259b565b008731a61c85afa650052b8fd413faf0fec35a9d21`，与市场 forge.hiynet.com 上的相同）

## 改了什么（见同目录 `toys-changes.patch`）

原版在视频 `Prepare()` 一调用就当作视频背景已启动：游戏放不了的视频（例如 VP9 编码的 webm，
Player.log 里是 `Unsupported video codec 'VP9'`）也会藏掉主题背景、盖上一层空白画面，主页一闪一闪。

修改后：视频准备好（`isPrepared`）才换上；准备期间不动主页；10 秒还没准备好就当放不了，退回图片背景。
用轮询而不是 `errorReceived` 事件，是因为游戏是 IL2CPP，反射订阅事件不一定可靠。

版本号写成 `1.1.0.1`：比市场上的 1.1.0 新，所以会把市场装的 1.1.0 升级成修好的版本；
原作者以后发 1.1.1 或更高版本时，工具箱不会降级它。

## 重新编译

克隆上面的源码，切到 `3d4887b`，`git apply toys-changes.patch`，
把 `HomeBackgroundMod.json` 的版本改成 `1.1.0.1`，运行 `build.ps1`（.NET SDK 8+），
产物 `dist\HomeBackgroundMod\HomeBackgroundMod.dll` 的 SHA-256 应与上面相同。

## 其他

工具箱不把它放进加载器的内置模组清单（内置清单直接来自 CesiumLoader 发布包里的 `mods\` 目录），
而是在「自定义 → 主页背景」第一次设置背景时装进游戏的 `AstralParty_ModLoader\mods\HomeBackgroundMod\`。
更新时：替换这里的 dll / json，改上面的版本和 SHA-256。
