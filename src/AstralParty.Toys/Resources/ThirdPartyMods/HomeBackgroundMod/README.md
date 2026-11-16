# HomeBackgroundMod（第三方模组，随工具箱分发）

- 作者：TenkinMori
- 源码：https://github.com/TenkinMori/astralparty-homebg-cesium （MIT，见同目录 `LICENSE`）
- 版本：1.1.0（Windows），即 Release `v1.1.0-pc` 的 `HomeBackgroundMod.dll`
- SHA-256：`474e6aebfcfed3259b565b008731a61c85afa650052b8fd413faf0fec35a9d21`（与市场 forge.hiynet.com 上的 1.1.0 相同）

工具箱不把它放进加载器的内置模组清单（`BuiltInMods.props` 由发布流程从 CesiumLoader 发布包同步），
而是在「自定义 → 主页背景」第一次设置背景时装进游戏的 `AstralParty_ModLoader\mods\HomeBackgroundMod\`。
更新时：替换这里的 dll / json，改上面的版本和 SHA-256。
