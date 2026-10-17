# 多人物角色皮肤方案

在皮肤页选择「角色皮肤 → 我的方案」。左侧搜索或选择方案，右侧查看其中的人物卡片。新建方案只需填写名称，内部标识自动生成；点击「添加人物皮肤」，选择人物以及已有皮肤包、新建自定义皮肤、官方皮肤中的一种方式。默认适用于人物的全部原皮肤，特定替换范围和混合基础放在「高级设置」。点击右上角「应用此方案」后，完全退出并重启游戏生效；切换方案会使用新方案的全部人物配置。重命名、导出和删除放在「方案设置」，导入方案位于左侧「导入与管理」。

新建人物皮肤包完成后自动加入当前方案并进入素材编辑器，通过「图片 / 动画 / 文案 / 高级」四个页签操作；点击「返回当前方案」继续添加其他人物。人物卡片的「编辑人物皮肤」可随时继续编辑，官方来源卡片可直接「更换官方皮肤」，也可在「人物设置」里「创建自定义覆盖」。「皮肤包素材库」仍可独立管理与复用包。官方来源和自定义包可同时使用，自定义包中没有填写的部分沿用基础外观。已安装旧版角色模组时，点击「更新角色皮肤模组」安装 Toys 内嵌的新版本。

创建包时勾选「从游戏导出官方皮肤素材」，可在「导出哪套官方皮肤」中选择此人物的任意已列出皮肤。导出器按所选皮肤查找图片和动画资源，不再固定为默认皮肤。此来源与方案中的替换范围独立：例如导出皮肤 B 的素材后替换原皮肤 A。所需资源必须已下载到本地；指定皮肤不存在或属于其他人物会报错，不会静默改导默认皮肤。

在「皮肤包素材库」点击「导入旧式皮肤包」，可选择原本通过覆盖游戏 `.bundle` 文件安装的 ZIP。Toys 在后台提取静态贴图及角色精灵动画，并通过配表中的资源名识别人物和来源皮肤，生成可编辑的自定义包；右下角可查看进度或终止。动画保留原精灵名、画布布局、帧引用顺序、采样率及循环标记。包中的 CRI 技能视频原文件保存在 `source/`，当前视频演出仍使用原版，不会因保存视频而启用媒体替换。完成后在素材库选择该包，点击「添加到多人物方案」，再应用方案。导入不会自动启用，也不覆盖游戏原 bundle。导入不设文件数量或包体积上限，只受 Unity 纹理边长（16384）、单个 bundle 可读入内存和磁盘剩余空间限制。不能识别目标、存在多个目标或无法完整解码时会报错并清理临时文件。

卡牌页顶部也提供「导入旧式皮肤包」。选择包含卡面 bundle 的 ZIP 后，后台逐包转换，按原资源名生成 PNG 卡面方案，支持手牌、事件、命运及异画等现有卡面资源前缀；原始文件保存在方案的 `source/` 下。完成后自动选中新方案供预览，点击「设为当前」后才启用。混入角色等非卡面资源或任一资源转换失败时，整包回滚，不留下半成品。进度和取消共用右下角「后台皮肤任务」。

角色皮肤 Mod 0.2.1 修复了自定义图片使用独立资源地址后丢失游戏立绘布局的问题。原图像素尺寸与游戏中的显示尺寸并不相同：界面还使用按图片地址索引的偏移、对局缩放和翻转。遇到图片尺寸正确但游戏中放大或偏移时，请先更新角色皮肤模组，再重启游戏，无需为此重新裁剪图片。Toys 会在方案页提示游戏中已装模组与内嵌新版的版本差异。

方案导出会带上引用的自定义皮肤包，官方皮肤仅保存 ID；游戏版本与资源可用性由加载时检查。语音、视频、特效和特殊演出保持原版。实际游戏中的渲染仍需真机验证，WebView2 页面验证不能替代游戏验证。

`CesiumLoader.CharacterSkins.CharacterSkinScheme` 为多角色方案，`Rules` 中每项为 `CharacterSkinBinding`。SchemaVersion 固定为 1。方案与规则标识仅允许 1～80 个 ASCII 字母、数字、下划线、短横线；规则标识大小写不敏感去重，同一 `(HeroId, TargetSkinItemId)` 不允许重复。

`HeroId > 0`；`TargetSkinItemId = 0` 匹配该角色全部皮肤；`OfficialSourceSkinItemId = 0` 表示无官方来源；`CustomPackageId = ""` 表示无自定义来源。每条规则至少有一种来源，允许官方来源与自定义来源同时存在。共享 `CharacterSkinSchemeRules.Validate` 校验结构，`Resolve` 优先返回精确目标，其次通配，未命中返回 null。`ValidateSharedOverrides` 拒绝角色共用地图头像、角色共用文案与直接 `asset:` 地址的不同覆盖值，允许同一包在多个目标复用。官方来源及非零目标必须在 Toys 目录中属于该角色；自定义包必须存在且其 HeroId 与规则一致。旧包 SkinItemId 仅用于默认创建规则目标，不限制同角色复用。

方案文件位于 `<游戏>/AstralParty_ModLoader/character-skin-schemes/<id>/character-scheme.json`；自定义包保留原 `character-skins/<id>/character-skin.json` 布局。配置仍使用 `mods/CharacterSkinMod/config.json`，新增 `ActiveSchemeId`，保留 `ActivePackages`。

## Toys API

两个独立仓库各自包含相同的 `CharacterSkinScheme.cs` 契约源码：加载器维护 `shared/CharacterSkins/CharacterSkinScheme.cs`，Toys 保留 `Services/CharacterSkinScheme.cs` 同步副本，保证 Toys 单独检出时可构建。修改契约后必须同步两份文件；不使用跨仓库的 Compile 路径。

`new CharacterSkinSchemeManager(IEnumerable<CharacterSkinTarget>? catalog = null)` 可由宿主传入当前配表。默认从程序目录的 `Protocol`、`GameData` 加载现有目录。

| 方法 | 返回与行为 |
| --- | --- |
| `Profiles(game)` | 返回 `List<CharacterSkinSchemeProfile>`，字段 `Id, Scheme, Active, Error`；首次读取时迁移旧启用包；无效方案以错误条目显示 |
| `Load(game, id)` | 读取并验证方案及全部引用 |
| `Create(game, id, name)` | 创建空方案并返回模型；已有目录拒绝覆盖 |
| `Save(game, id, scheme)` | 标识须与目标一致，校验全部引用后原子写清单，不自动启用 |
| `CreateBindingFromPackage(game, packageId, ruleId)` | 返回默认规则，目标来自旧包 SkinItemId；不写文件 |
| `SetActive(game, id)` | 验证后设为唯一启用方案，清空旧 ActivePackages；空串取消启用 |
| `Delete(game, id)` | 将方案移到 `.deleted/<id>-<随机标识>`；删除当前方案会取消启用 |
| `EnsureMigration(game)` | 当无启用方案且有旧 ActivePackages 时创建 `legacy` 方案，备份旧配置至 `config.json.legacy-backup`，再启用方案、清空旧列表；已有 legacy 不覆盖，使用编号后缀 |
| `Import(game, zipPath)` | 返回导入方案标识，导入后不启用；已有方案或引用包拒绝覆盖 |
| `Export(game, id, destination)` | 导出方案及所有去重的自定义包引用，不包含官方游戏资源 |
| `Root(game)` / `SchemeDirectory(game,id)` | 获取方案根目录与经过标识验证的方案目录 |

ZIP 根目录固定含 `character-scheme.json`，引用包位于 `packages/<id>/`，包含各自清单与文件。拒绝未引用文件、路径逃逸和大小写不敏感的重复路径；不设文件数量或解压大小上限，只拒绝无法解码的资源、超出 Unity 纹理边长上限（16384）的图片，并在解压前确认磁盘剩余空间。导入先在隐藏临时目录验证完整内容与角色归属，再落盘；失败清理临时内容及已移动的新目录。

`CharacterSkinManager` 的编辑与保存操作不改变启用方案。显式旧 `SetActive(..., true)` 保留旧操作语义并清空 ActiveSchemeId。删除被非隐藏方案引用的自定义包会拒绝，须先移除引用或删除方案；删除方案不删除包。迁移遇到缺包或冲突时保留原配置并报错，避免静默丢弃规则。
