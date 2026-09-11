# 图片资源接口

正式发布素材位于 `PackedAssets`，以保留透明度的无损 WebP 作为标准 .NET `EmbeddedResource` 编入程序集。本目录只作为没有内嵌素材时的开发回退入口。

支持 png、jpg、jpeg、webp、bmp。开发时放进本目录并重新构建；使用发布版时直接放进程序旁的 `Assets` 目录，重新启动即可自动显示。

- `Maps/<地图ID>.png`，例如 `Maps/82016.png`
- `Characters/<角色ID>.png`，例如 `Characters/112.png`
- `Relics/<筹码ID>.png`，例如 `Relics/50043.png`
- `Monsters/<怪物ID>.png`，例如 `Monsters/1067.png`
- `Items/<物品ID>.png`
- `Cards/<卡牌ID>.png`
- `Skills/<技能ID>.png`
- `Buffs/<Buff ID>.png`
- `Lands/<地块类型或ID>.png`
- `Events/<事件键>.png`，例如 `Events/dice.png`、`Events/move.png`

没有图片时界面会显示带 ID 的占位区域，不影响回放解析。
