## CesiumLoader 2.4.10 / Unity 2022 兼容构建（2026-10-15，未发布）

- SDK 与 7 个公开内置 Mod 使用游戏 3.3.0 的程序集强制重新编译，修复 `UIStringType.Build` 插入后卡牌及其他名称查询错位。
- 打包流程强制重编托管层，避免替换游戏 DLL 后增量构建继续分发旧枚举常量。
- 分发产物通过 IL 枚举值检查及 DLL/PDB 配对检查。此构建已内嵌到 Toys 并部署国服游戏目录，用户确认运行正常。

以下为此前版本发布说明。

## CesiumLoader 2.4.8

## 动态卡面与异画替换

- 内置 CardSkinMod 更新至 **1.2.0**，支持普通卡牌与显式指定异画的序列帧动态卡面。
- 配合 AstralParty.Toys 1.1.0 导入视频或 GIF，先裁剪为普通卡面的 **404×400**，再转换为短循环，在普通卡框内播放。
- 普通卡牌与异画分别绑定；未指定的异画保留原版，SFW 动态资源可单独配置。
- 保留资源 URL 和当前帧缓存，处理抽牌、出牌及手牌重排；原视频图层在替换时停止，损坏或卸载时恢复。
- 动画共享时钟，滚动保留当前与上一帧，回收闲置缓存和原生纹理。
- 加载器、SDK、CLI 与默认配置版本同步为 **2.4.8**。

## 验证

CardSkinMod 57 项测试通过，Release 构建通过，用户已完成游戏内验证。默认皮肤包单独提供；升级时保留自己的配置与皮肤方案。

## 下载

- `cesium-loader.zip`：加载器与内置模组。
- `cesium-default-skins.zip`：默认皮肤包。
- `cesium-sdk-tools.zip`：SDK 工具包。

视频／GIF 转换和裁剪入口位于配套 [AstralParty.Toys](https://github.com/higashitaniyume/AstralParty.Toys/releases) 的卡牌皮肤页。
