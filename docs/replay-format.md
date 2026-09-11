# 吉星派对（Astral Party）回放文件格式与解析说明

本文记录**回放文件长什么样**（字节级格式）与**怎么把它解析出来**（游戏侧权威逻辑 + 本工具的解析管线）。
内容来源：

- 反编译 `AstralParty.Runtime.dll` 得到的 `GameLogic.Replay.*` / `GameLogic.ReplayLogic` / `UI.AccountInfoWindow`（Unity 2021.3.45f2，IL2CPP + HybridCLR，逻辑在热更 DLL 里）；
- 本工具的实现：`replaytool/Services/ReplayAnalyzer.cs`、`GameProtocolContext.cs`、`ReplayLibraryService.cs`。

一句话概括：

> **回放文件 = 服务端把一局游戏期间下发给客户端的 S2C 网络消息，按顺序原样落盘。**
> 里面没有地图几何、没有画面数据，只有「6 字节帧头 + protobuf 载荷」的帧流；客户端回放时把它当"假联机"逐帧重放。

---

## 1. 文件在哪、怎么来的

### 1.1 路径与命名

| 项 | 值 |
| --- | --- |
| 根目录 | `Application.persistentDataPath + "/Temp/Replay"` |
| Windows 实际路径 | `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay` |
| 单局目录 | `<根目录>\<replayId>\` |
| 回放文件 | `<根目录>\<replayId>\<replayId>` |

**目录名必须等于文件名**，两边都靠这个约定工作：

- 游戏判缓存：`IsReplayCached(id)` → `File.Exists(Path.Combine(root, id, id))`；
- 游戏列列表 `ListAllReplayIds()`：只枚举根目录下的**子目录**，且要求子目录里存在**同名文件**才认；
- 所以只建目录不写文件（或文件名与目录名不一致）的对局，游戏列表里根本不会出现。

`replayId` 来自结算消息 `GameFinishS2C.ReplayId`（字段 11）。工具侧解析不到时退回用文件名代替。

### 1.2 文件从哪来：CDN 下载，不是本地录制

这局打完**不会**在本地生成回放。客户端拿到 `GameFinishS2C.ReplayId` 后，只有用户主动点「保存回放 / 播放」才会去下载：

| 行为 | 调用 | 结果 |
| --- | --- | --- |
| 点「保存回放」 | `TryLoadReplayFile(id, needSave: true)` | 下载并**留在本地** |
| 点「播放回放」 | `TryLoadReplayFile(id, IsReplayCached(id))` | 已缓存则直接读；未缓存则下载到内存，读完**删掉临时目录**（不留文件） |
| 打开战绩列表 | `ListLocalReplaySummariesAsync()` | 只读本地已有的目录，不联网 |

```csharp
// GameLogic.ReplayLogic
private readonly string _replayRoot = Application.persistentDataPath + "/Temp/Replay";
private string _cdnBaseUrl = "https://sereplaycn.feimogames.com/prod/";   // 默认 CN prod
// url = _cdnBaseUrl + replayId
```

下载用 `UnityWebRequest.Get(url)` + `DownloadHandlerFile(savePath)`，**直接流式写盘**到 `<root>\<id>\<id>`；失败则 `TryDeleteExtractDir` 把整个目录删掉（不会留半截文件）。
CDN 可用 `ChangeReplayCDNUrl(index)` 切换：`0` = JP dev、`1` = JP prod、`2` = CN dev、`3` = CN prod。

> 推论：**回放文件本身是服务端录制的产物**（消息流原样下发），客户端只是下载器 + 播放器。
> 这也解释了为什么文件里能出现 `ReplaySnapshotS2C` 这种专门给回放用的消息——它是服务端为了让客户端能重建场景而额外发的。

### 1.3 上限：满 10 局就不让存了

```csharp
if (needSave && !IsReplayCached(replayId) && ListAllReplayIds().Count >= StaticGlobalData.MAX_BATTLE_RECORDS)
    return (null, ReplayLoadStatus.SaveLimitReached);   // 界面上提示 1145
```

| 项 | 说明 |
| --- | --- |
| 常量 | `StaticGlobalData.MAX_BATTLE_RECORDS`（配表驱动，`FindValue_Int("MAX_BATTLE_RECORDS")`，当前版本实测 **10**） |
| 判定时机 | **只在"保存"时**判定；已经缓存的局不受影响 |
| 播放 | 不受上限限制（`needSave: false` 走的是"用完即删"路径） |
| 状态码 | `Success` / `DownloadFailed`(提示 1137) / `ReadFailed` / `SaveLimitReached`(提示 1145) |

### 1.4 游戏自己会删回放

`ListLocalReplaySummariesAsync()` 解析每个目录的摘要（`TryParseReplaySettlementOnly`），**解析不出结算的对局会被直接删掉整个目录**：

```csharp
BattleShortRecord obj = await ParseReplaySummaryAsync(r, cancellationToken);
if (obj == null) DeleteReplayFile(r.id);      // → Directory.Delete(recursive: true)
```

这是本工具在「把回放放回游戏目录」之前必须先校验一次的根本原因：**放进去一个游戏读不出的文件，下次打开战绩列表就会被连目录一起清掉。**

摘要结果按 `(mtimeUtc, size)` 缓存在内存（`_summaryCache`），文件没变就不重复解析；解析并发度 4。

### 1.5 本工具的回放库（对同一格式的另一种组织）

工具不改文件内容，只是把 `<replayId>\<replayId>` 这份**原样**挪到自己的库目录里，并补一份旁的索引：

```
<库根>\<replayId>\<replayId>          ← 与游戏目录里的文件逐字节相同
<库根>\_index\<replayId>.json         ← 工具写的摘要（SHA-256、大小、版本、健康标记…）
<库根>\operations.log                 ← 归档/还原/删除的操作流水
<库根>\replay-library.json            ← 工具设置（入库上限、是否自动托管…）
```

> 库根 = 配置根（默认 `%USERPROFILE%\Documents\AstralPartyReplays`），所以变速器的配置、状态文件也在这个目录下。

| 操作 | 语义 | 对游戏名额的影响 |
| --- | --- | --- |
| 归档 | **移动**（游戏目录 → 库） | 腾出名额 |
| 还原 | **复制**（库 → 游戏目录，库里留备份） | 占一个名额 |
| 删除 | 删除库里的副本 | 无 |

因为游戏只认 `<id>\<id>` 这个结构，工具全程保持目录名 = 文件名；归档/还原来回搬运不会破坏格式。

---

## 2. 容器格式：帧流

### 2.1 字节布局

没有魔数、没有版本号、没有帧数、没有索引表、没有校验和、没有文件尾。**整个文件就是若干帧首尾相接，一直排到 EOF。**

```
偏移 0
┌──────────────────────────────────────────────┐
│ 帧 #0                                        │
│   +0   int16 BE    cmdId           (协议号)   │
│   +2   int32 BE    payloadLength   (载荷长度) │
│   +6   byte[]      protobuf payload           │
├──────────────────────────────────────────────┤
│ 帧 #1  … 同上 …                               │
├──────────────────────────────────────────────┤
│ …                                            │
└──────────────────────────────────────────────┘ EOF
```

| 项 | 值 |
| --- | --- |
| 帧头长度 | `6`（游戏常量 `ReplayLoader.SERVER_PACK_HEADER_LENGTH = 6`） |
| 字节序 | **大端**（游戏用 `ByteBuf.ReadShort()/ReadInt()`，工具用 `BinaryPrimitives.ReadInt16BigEndian/ReadInt32BigEndian`，两边实测一致） |
| `cmdId` | 协议号，对应 `party.protocol.*S2C` 消息类型，见第 3 节 |
| `payloadLength` | 载荷字节数；`0` 合法（空载荷帧，`ReplayFrame.HasPayload == false`） |
| 结束条件 | 走到 EOF。**帧头 + 载荷必须精确铺满文件**：`Σ(6 + payloadLength) == fileSize` |

### 2.2 游戏侧的帧流读取（`ReplayLoader.LoadServerPackStream`）

```csharp
while (byteBuf.ReadableBytes() > 0) {
    int frameOffset = byteBuf.ReaderIndex();
    if (!byteBuf.HasBytes(6)) return Fail("TruncatedFrameHeader", ...);
    short cmdId = byteBuf.ReadShort();
    int   len   = byteBuf.ReadInt();
    if (len < 0)                     return Fail("FramePayloadTooLarge", ...);
    if (len > byteBuf.ReadableBytes()) return Fail("TruncatedFramePayload", ...);
    byte[] payload = len > 0 ? ReadPayloadBytes(byteBuf, len) : Array.Empty<byte>();
    package.Frames.Add(new ReplayFrame(index, cmdId, payload));
}
```

要点：

- **帧头剩不满 6 字节**（哪怕只剩 1 字节）= 文件被截断；
- 长度为负 = 数据坏了；
- 载荷声明长度超出剩余字节 = 文件被截断；
- 载荷长度 `0` 不报错，直接给空数组。

### 2.3 帧的数据结构（两侧对照）

| 游戏侧 `GameLogic.Replay.ReplayFrame`（readonly struct） | 工具侧 `ProtocolFrame` |
| --- | --- |
| `int Index`（帧序号，从 0 开始） | `Index` |
| `int CmdId` | `CmdId` |
| `byte[] Payload` | `Payload` |
| `bool HasPayload => Payload.Length != 0` | 同义判断 |
| `int PayloadLength` | `Payload.Length` |
| — | `Offset`（帧头在文件中的字节偏移，游戏侧不保留） |
| — | `MessageName`（命令号 → 类型短名） |

---

## 3. 载荷：protobuf 消息

载荷是 `Google.Protobuf` 序列化的消息体，类型由 `cmdId` 决定。反序列化用的是**游戏自己的生成类**（`AstralParty.Runtime.dll` 里的 `party.protocol.*`）。
`cmdId` 与类型的对应关系是**反查出来的**：按帧号扫描游戏程序集里的协议类/注册表得到，不是文件里带的。

### 3.1 本工具已登记的命令号

| cmdId | 消息类型 | 在回放里的作用 |
| --- | --- | --- |
| 1002 | `PredictActionS2C` | 行动消息（内含 `Actions` 列表），事件时间线的主要来源 |
| 1003 | `RunningGameS2C` | **开局锚点**，`Room` = 对局初始全量状态 |
| 1016 | `GameFinishS2C` | **结算**：ReplayId / Version / Winer / FinishTime / Awards |
| 1040 | `UpdateHeroAttrS2C` | 属性变化（含 `HeroAttrEffect`，用于识别升级） |
| 1096 | `HeroSkillMoveEffectS2C` | 技能移动效果（内含多段 `UpdateHeroAttrS2C`） |
| 1098 | `SayPhraseNotifyS2C` | 台词/播报（`TriggerType == TransferGold` 时表示转账） |
| 1112 | `SyncRelicsS2C` | 筹码同步（工具只登记，不据此统计） |
| 1113 | `ReplaySnapshotS2C` | **每次行动前的全量快照**（`PlayerId` + `Room`），回合/行动节点的唯一依据 |
| 1115 | `ReplayDieS2C` | 单位死亡（`PlayerId` / `HeroId` / `KillerId`） |
| 5212 | `SelectRelicS2C` | 筹码三选一的结果（`RelicId` / `IsReroll`） |
| 5214 | `MonsterPursuitS2C` | 怪物追击结算（工具只记一条系统事件） |
| 5216 | `PVEShopBuyS2C` | PVE 商店购买（工具只记一条系统事件） |
| 5250 | `BuyRelicS2C` | 筹码地块购买结果 |

> 其余命令号未登记：工具不猜类型，只把**前 4096 字节**打成十六进制给用户看。
> 回放是"整局 S2C 消息流"的落盘，工具只登记了解析所需的那一小部分协议，因此**解析器必须能跳过未知帧**。

### 3.2 关键消息字段号

（`FieldNumber`，protobuf wire 里的 tag；调试十六进制时按这个对照）

**`RunningGameS2C`**

| 字段 | 号 |
| --- | --- |
| Room | 1 |

**`GameFinishS2C`**

| 字段 | 号 | 字段 | 号 |
| --- | --- | --- | --- |
| RoomId | 1 | Awards | 6 |
| Winer | 2 | NewAchieve | 7 |
| FinishTime | 3 | CampScores | 8 |
| Achieve | 4 | RookieBonusAwards | 9 |
| ActivityId | 5 | ReturnBonusAwards | 10 |
| **ReplayId** | **11** | **Version** | **12** |
| MapType | 13 | Rank | 14 |

> 注意拼写就是 `Winer`（少一个 n），解析时别写成 `Winner`。
> `FinishTime` 是 **Unix 秒**；`Awards` 是 `map<int,int>`（物品 ID → 数量）。

**`ReplaySnapshotS2C`**

| 字段 | 号 |
| --- | --- |
| PlayerId | 1 |
| Room | 2 |

**`ReplayDieS2C`**

| 字段 | 号 |
| --- | --- |
| PlayerId | 1 |
| HeroId | 2 |
| KillerId | 3 |

**`SelectRelicS2C`**

| 字段 | 号 |
| --- | --- |
| PlayerId | 1 |
| RelicId | 2 |
| IsReroll | 3 |

**`HeroAttrEffect`**（`UpdateHeroAttrS2C.EffectDatas` 的元素）：`PlayerId = 1`，之后是一大串 `oneof DataOneofCase`：`Gold=2 / Hp=3 / Atk=4 / Def=5 / Buff=6 / Place=7 / Lottery=8 / Card=9 / Bomb=10 / Lv=11 / Cd=12 / Reroll=13 / SpecialScore=14 / CureNum=15 / SalaryNum=16 / MarkNum=17 / UseCardNum=18 / CardDistance=19 / CanCounter=20 / CounterNum=21 / ModifyNum=22 / NotSelect=23 / ConvertCard=24 / Disappear=25 / Combine=26 / AddMove=27 / UniqueNum=28 / AddTerm=29 / CardAtk=30 / EnergyNum=31 / CrimeNum=32`。

### 3.3 关键模型字段号

**`party.model.Room`**（回放里最重的结构，快照里每次都全量带一份）

| 字段 | 号 | 字段 | 号 |
| --- | --- | --- | --- |
| Id | 1 | StartTime | 30 |
| Name | 2 | GameProgress | 46 |
| MapId | 5 | GameMaxProgress | 47 |
| MasterId | 6 | Difficulty | 48 |
| CreateTime | 7 | MonsterIndex | 55 |
| UpdateTime | 8 | **PlayerTotalDie** | **56** |
| **Players** | **9** | IsMatchRoom | 57 |
| Monsters | 10 | LevelId | 58 |
| **State** | **11** | VictoryCondition | 59 |
| Lands | 12 | SpecialScore | 60 |
| Predicts | 13 | MapStatus | 61 |
| **Round** | **14** | MapIndex | 62 |
| MapDataId | 15 | MapDifficultyId | 63 |
| MapType | 36 | WaitExecEventIds | 64 |
| VoteInfo | 65 | RoomServerId | 1001 |

`Room.State` 枚举（`OriginalName`）：

| 名 | 值 |
| --- | --- |
| None (`none`) | 0 |
| Wait (`wait`) | 1 |
| ChoiceHero (`choice_hero`) | 10 |
| ChoiceSkin (`choice_skin`) | 15 |
| Ready1 (`ready1`) | 20 |
| **Running (`running`)** | **25** ← 回放锚点认这个 |
| Finish (`finish`) | 30 |

**`party.model.Player`**：`Id=1 / Nick=2 / Slot=6 / Hero=10 / IsBot=20 / Level=25`
**`party.model.Hero`**：`PlayerId=1 / HeroId=2 / Gold=7 / MovePoint=10 / Hp=14 / Defense=15 / Attack=16 / MaxHp=23 / Lv=33 / Cond=47 / MonsterType=49 / SelectRelics=52 / BuyRelicNum=68`

> `Hero.SelectRelics`（52）是 `map<int,bool>`（筹码 ID → 是否选过），不是 repeated；`Hero.Cond`（47）是嵌套统计结构（`TotalDamage` / `KillPveMonster` / `UseCard` …）。

**注意游戏里有两个同名 `MonsterType` 枚举，别混用：**

| 枚举 | 用在哪 | 成员 |
| --- | --- | --- |
| `party.model.Hero.Types.MonsterType` | `Hero.MonsterType`（字段 49）的实际类型，protobuf 按 `(int)` 序列化 | `None=0`(`NONE`)、`Thief=1`(`Thief`)、`Boss=2`(`BOSS`)、`PveBoss=10`(`PVE_BOSS`)、`PveMonster=11`(`PVE_MONSTER`)、`PveEliteMonster=12`(`PVE_ELITE_MONSTER`)、`PveAllyMonster=30`(`PVE_ALLY_MONSTER`) |
| 全局 `MonsterType` | 配表，以及 `ReplayLoader` 的行为标记判定 | `None`(`MonsterType_None`)、`Normal`(`MonsterType_Normal`)、`Boss`(`MonsterType_Boss`)、`Elite`(`MonsterType_Elite`) |

- 工具侧判 Boss：读 `Room.Monsters[].Hero.MonsterType` 的**文本**（C# 名，如 `Boss`）做忽略大小写的包含 `BOSS` 比较；
- 游戏侧判"击杀精英"：`StaticConfigure.Monster.InfoDict[HeroId].MonsterType == MonsterType.Elite`（全局枚举的那个 `Elite`）。

---

## 4. 游戏侧权威解析（`GameLogic.Replay.ReplayLoader`）

改动解析逻辑前先看这一节：**这局能不能播，完全由这几个函数说了算。**

### 4.1 完整校验链 `ValidatePackage`

```
LoadServerPackStream(bytes)                    ← 2.2 拆帧
  └─ ValidatePackage(package)
       1. package == null                → PackageNull
       2. Frames 为空                    → FramesEmpty
       3. TryFindRunningGameRoom()       ← 找 1003 且 Room.State == Running
       4. FillPackageHeaderFromRoom()    ← 用 Room 填包头的 RoomId/RoomServerId/MapId/MapType
       5. ExtractTurnNodes()             ← 建回合/行动节点（并给节点打行为标记）
       6. TryFindGameFinish()            ← 找 1016
```

### 4.2 开局锚点 `TryFindRunningGameRoom`

- 从头扫描，找 **`cmdId == 1003`** 的帧；
- 载荷为空 → 记一条 warning 跳过（不失败）；
- 反序列化抛异常 → **`RunningGameParseFailed`**（失败，整个回放不可播）；
- `Room == null` → warning 跳过；
- `Room.State == Room.Types.State.Running(25)` → 采纳：`RunningGame` / `RebuildFrameIndex = 帧号` / `PlaybackStartFrameIndex = 帧号 + 1`；
- 扫完都没有 → **`RunningGameRunningRoomNotFound`**。

### 4.3 回合 / 行动节点 `ExtractTurnNodes`

这是游戏回放 UI 的骨架（进度条、跳转回合、跳转行动全靠它）：

```csharp
foreach frame:
  if cmdId != 1113:
      if 有载荷 && 当前行动节点 != null:  TryApplyBehaviorMarker(...)   // 给当前行动补标记
      continue
  if 解析出 ReplaySnapshotS2C（Room 非空）:
      if 当前回合 == null || 当前回合.Round != snapshot.Room.Round:
          开新 ReplayRoundNode(Round, FrameIndex=i, PlaybackStartIndex=i+1)
      新建 ReplayTurnNode(FrameIndex=i, PlaybackStartIndex=i+1,
                          TurnIndex = 当前回合已有节点数, Round, PlayerId, Snapshot)
      挂到回合与 package.TurnNodes
```

最后一步很关键：

```csharp
// 丢掉最后一个行动节点（它是"结算/收尾"快照，不是真正的行动）
package.TurnNodes.RemoveAt(TurnNodes.Count - 1);
lastRound.Turns.RemoveAt(lastRound.Turns.Count - 1);
if (lastRound.Turns.Count == 0) package.Rounds.RemoveAt(Rounds.Count - 1);
```

节点结构：

| 类型 | 字段 |
| --- | --- |
| `ReplayTurnNode` | `FrameIndex`（快照帧号）、`PlaybackStartIndex`（从这一帧开始播）、`TurnIndex`（本回合内第几个行动）、`PlayerId`、`Round`、`DisplayName`、`Snapshot`（完整 `ReplaySnapshotS2C`）、`Behaviors`（行为标记位） |
| `ReplayRoundNode` | `Round`、`FrameIndex`、`PlaybackStartIndex`、`Turns`（本回合行动列表） |
| `TurnBehavior`（Flags） | `NONE=0`、`TRANSFER_GOLD=1`、`LEVEL_UP=2`、`KILL_MONSTER=4` |

行为标记 `TryApplyBehaviorMarker`（**只作用于当前行动节点**）：

| cmdId | 判定条件 | 置位 |
| --- | --- | --- |
| 1040 `UpdateHeroAttrS2C` | `EffectDatas` 里有 `HeroAttrEffect.Lv.PlayerId == 节点.PlayerId` | `LEVEL_UP` |
| 1096 `HeroSkillMoveEffectS2C` | `EffectData.Values[].Effects[].EffectDatas` 同上判定 | `LEVEL_UP` |
| 1098 `SayPhraseNotifyS2C` | `Phrase.TriggerType == TransferGold && Phrase.ActivePlayerId == 节点.PlayerId` | `TRANSFER_GOLD` |
| 1115 `ReplayDieS2C` | `KillerId == 节点.PlayerId` 且 `HeroId` 对应的 `MonsterType == Elite` | `KILL_MONSTER` |

解析失败**不致命**，只加一条 warning（`ApplyBehaviorMarker: parse failed…`）。

### 4.4 结算锚点 `TryFindGameFinish`

从**最后一帧往前**扫到 `PlaybackStartFrameIndex` 为止，找 `cmdId == 1016`：

- 有载荷且能反序列化 → `GameFinish` / `GameFinishFrameIndex`；
- 载荷为空 → warning 跳过；
- 反序列化抛异常 → **`GameFinishParseFailed`**；
- 找不到 → **`GameFinishNotFound`**（附 `frameCount` 与 `playbackStartFrameIndex`）。

### 4.5 轻量摘要 `TryParseReplaySettlementOnly`（列表用，也当权威判据）

只看两种帧，不建包、不建节点，**O(文件大小) 一遍过**：

```csharp
while (byteBuf.ReadableBytes() >= 6) {
    short cmd = byteBuf.ReadShort();
    int   len = byteBuf.ReadInt();
    if (len < 0) return (gameFinish, lastSnapshot);        // 数据坏了 → 返回已有结果
    if (len > byteBuf.ReadableBytes()) return (...);       // 截断 → 返回已有结果
    if (cmd == 1016 && len > 0) gameFinish   = ReadObject<GameFinishS2C>(payload);      // 保留最后一个
    else if (cmd == 1113 && len > 0) lastSnapshot = ReadObject<ReplaySnapshotS2C>(payload); // 保留最后一个
    else if (len > 0) byteBuf.SkipBytes(len);
}
```

语义要点：

- **保留最后一个** `1016` 与**最后一个** `1113`（不是第一个）；
- 反序列化失败**静默吞掉**（对应字段保持上一次的值）；
- 遇到坏帧就**提前返回已有结果**，不报错——所以这个函数对"尾巴被截断"是宽容的（而 4.1 的完整校验链会报 `TruncatedFrameHeader/Payload`）；
- 判定"这局是否可读"的口径是：**`gameFinish != null`**。

本工具用反射直接调用它（`GameProtocolContext.TryParseSettlement`），因此"工具认为能读" ⇔ "游戏认为能读"，不会出现两套标准。

### 4.6 播放参数（`ReplayConfig`）

| 常量 | 值 | 含义 |
| --- | --- | --- |
| `REPLAY_BASE_SPEED_TYPE` | `GameSpeedType.Fast` | 回放基准速度档 |
| `FRAME_DELAY_MS` | 16 | 每帧间隔（≈60fps 重放） |
| `JUMP_DELAY_MS` | 1000 | 跳转后的等待 |
| `PLAY_SPEED_SEQUENCE` | `1, 2, 4, 0.5` | 倍速循环 |

### 4.7 错误码全表

| 错误码 | 触发条件 |
| --- | --- |
| `BytesEmpty` | 输入字节为空 |
| `TruncatedFrameHeader` | 帧头剩不满 6 字节 |
| `FramePayloadTooLarge` | 载荷长度为负 |
| `TruncatedFramePayload` | 载荷长度超过剩余字节 |
| `PackageNull` | 包为 null |
| `FramesEmpty` | 一帧都没有 |
| `RunningGameParseFailed` | 1003 反序列化失败 |
| `RunningGameRunningRoomNotFound` | 没有 `State == Running` 的 1003 |
| `GameFinishParseFailed` | 1016 反序列化失败 |
| `GameFinishNotFound` | 找不到带载荷的 1016 |

---

## 5. 本工具的解析管线（`ReplayAnalyzer.Analyze`）

### 5.1 步骤

```
1. 安全检查：非空、≤ 512 MB（MaxFileSize）
2. 读全量字节 + 算 SHA-256
3. ReadFrames(bytes)             ← 与 2.2 同样的大端 6 字节帧头；单帧载荷 ≤ 64 MB（MaxPayloadSize）
                                    并且要求精确走到 EOF，否则报错
4. 锚点：第一个 1003 → RunningGameS2C.Room（初始状态）；没有就抛"协议版本不兼容"
5. 结算：最后一个 1016 → GameFinishS2C（允许没有：结算区留空）
6. 快照：所有 1113 → ReplaySnapshotS2C；最后一个快照的 Room = 末局状态
7. 填对局摘要 / 玩家表 / 事件时间线 / 筹码时间线 / 统计
```

### 5.2 摘要字段映射

| 报表字段 | 来源 |
| --- | --- |
| 房间号 / 服务器房间号 | `1003.Room.Id` / `1003.Room.RoomServerId` |
| 地图 / 地图类型 / 难度 | `Room.MapId` + `ConfigCatalog.Map` / `Room.MapType` / `Room.Difficulty` |
| 总回合数 `RoundCount` | 所有快照 `Room.Round` 的**最大值** |
| 行动数 `TurnCount` | `快照数 - 1`（与游戏去掉最后一个节点的口径一致） |
| 开始时间 | `1003.Room.StartTime`（Unix 秒 → 本地时间） |
| 结束时间 / 时长 | `1016.FinishTime`（时长 = 两者之差） |
| 回放 ID / 游戏版本 | `1016.ReplayId`（空则退回文件名）/ `1016.Version` |
| 获胜方 | `1016.Winer`（`> 0` 显示"获胜方 N"） |
| 奖励 | `1016.Awards`（`map<物品ID,数量>`）经 `ConfigCatalog.Item` 翻名字，过滤数量 0 |
| 死亡总数 / 进度 | 末局 `Room.PlayerTotalDie` / `Room.GameProgress`、`GameMaxProgress` |
| Boss 名 | 末局 `Room.Monsters[].Hero.MonsterType` 含 `Boss` 的那个，经 `ConfigCatalog.Monster` 翻名字 |

### 5.3 玩家表（开局 × 末局合并）

以 `1003.Room.Players` 为基准，按 `Id` 与**最后一个快照**的 `Players` 连接：

| 分组 | 字段 |
| --- | --- |
| 开局 | `Id`、`Nick`（空则用 ID）、`Slot`、`Level`、`Hero.HeroId` → 角色名/头像 |
| 末局 | `Hero.Hp` / `MaxHp` / `Gold` / `Attack` / `Defense`、`IsBot` |
| 末局统计 | `Hero.Cond.*`：`TotalDamage`（伤害）、`TotalInjured`（承伤）、`KillPveMonster`（击杀）、`TreatmentScore`（治疗）、`MovePoint`（移动）、`UseCard` / `UseSkill`（出牌/技能）、`TransferGold`（转账）、`FinalKillBoss`（终局击杀 Boss） |
| 筹码 | 选中列表由 **`5212` 帧累计**（谁选了哪些、几个）；购买个数读末局 `Hero.BuyRelicNum`（68） |

> 末局数据来自最后一个 `ReplaySnapshotS2C`。**机器人可能没有末局英雄数据**（`isBot && Hero == null` → 标注"机器人占位"），代码里对 `null` 全做了兜底。

### 5.4 事件与筹码时间线

主循环一遍扫全部帧，用 `1113` 维护"当前回合 + 当前行动玩家"上下文：

| cmdId | 产出 |
| --- | --- |
| 1113 | 更新 `currentRound` / `currentPlayer`；**除最后一个快照外**各产生一条"行动回合开始"事件 |
| 1002 | `ParseActions()`：遍历 `Actions`，`Action.Sn` 去重后按动作表计数并出事件（详见 5.5） |
| 5212 | `IsReroll` → "请求刷新筹码"；否则 → 一条"获得筹码"事件 + 一条筹码记录（含品质、来源、刷新次数） |
| 5250 | `Select > 0` → 确认购买；`Exit` → "离开筹码地块（没有购买）" |
| 1115 | "击败 <怪物>"（击杀者 = `KillerId`，图标取怪物图） |
| 5214 / 5216 | 系统事件（怪物追击结算 / PVE 商店购买） |
| 1016 | 收尾一条"对局结束"事件 |

### 5.5 `1002` 行动消息的解析细节

`PredictActionS2C.Actions` 是 `Action { Id, Sn, PlayerId, Data }` 列表：

- **`Sn` 去重**：非 0 的 `Sn` 记进 `HashSet`，重复的跳过（同一行动会在多帧里重发）；
- **`Id` → 名称**：查下表（未知动作显示"动作 <ID>"，图标用通用事件图）；
- **`Data`**：部分动作的 `Data` 是**另一条 protobuf 消息的序列化字节**，需要二次解码：

| 动作 ID | 二次解码 | 用途 |
| --- | --- | --- |
| 5211 | `SelectRelicC2S` → `Lv` / `SupLv` / `Relics` | 三个候选筹码出现（含刷新次数、来源、买价） |
| 5249 | `BuyRelicC2S` → `RelicGold` | 记录筹码地块的购买价 |
| 5250 | — | 标记"已确认购买"（与 5211 的上下文配对得出"来源 = 筹码地块购买"） |

**筹码来源判定**（`RelicSource`）：有购买价 → "筹码地块购买"；`SupLv > 0` → "升星"；否则 → "任务"。

动作 ID 表（当前工具已登记 33 项）：

| ID | 名称 | ID | 名称 | ID | 名称 |
| --- | --- | --- | --- | --- | --- |
| 5021 | 掷骰 | 5049 | 战斗结算 | 5077 | 特殊事件 |
| 5027 | 移动 | 5053 | 技能行动 | 5081 | Buff 事件 |
| 5029 | 购买 | 5055 | 属性结算 | 5083 | 效果事件 |
| 5033 | 战斗 | 5059 | 商店事件 | 5093 | 任务事件 |
| 5035 | 回合流转 | 5063 | 随机事件 | 5211 | 筹码候选 |
| 5037 | 行动开始 | 5067 | 特殊移动 | 5213 | 怪物追击 |
| 5039 | 行动结束 | 5069 | 恢复事件 | 5215 | PVE 商店 |
| 5041 | 使用卡牌 | 5071 | 星币事件 | 5249 | 到达筹码地块 |
| 5047 | 地块事件 | 5073 | 角色事件 | 5259 | 筹码事件 |
| 5309 | PVE 事件 | 5313 | PVE 事件 | 5317 | 特殊事件 |
| 5323 | PVE 事件 | 5377 | 特殊事件 | | |

### 5.6 依赖

| 依赖 | 位置 | 用途 |
| --- | --- | --- |
| `AstralParty.Runtime.dll` + 依赖（Unity / `Google.Protobuf.Runtime.dll` / UniTask / mscorlib…） | `Protocol/` | 加载游戏自己的 protobuf 生成类来解码，并通过反射调用 `ReplayLoader.TryParseReplaySettlementOnly` |
| `Character/Item/Map/Monster/Relic.bin` + `STR*.bin` | `GameData/` | ID → 名称/品质/素材键的映射（`ConfigCatalog`） |
| 258 张 webp（`PackedAssets/**`，编译期内嵌） | 通过 `EmbeddedAssetStore` 提供 | 事件/角色/怪物/地图/筹码图标；也可用外部素材目录覆盖 |
| `ReflectionValue` | `Services/` | 反射取字段的工具，避免编译期依赖游戏程序集 |

---

## 6. 容错、限制与安全上限

| 检查 | 工具行为 |
| --- | --- |
| 文件不存在 / 为空 | 直接报错，不进解析 |
| 文件 > 512 MB | 拒绝（`MaxFileSize`） |
| 帧头不足 6 字节 | 报"帧头被截断"，附 offset 与剩余字节数 |
| 载荷长度为负 / > 64 MB | 报"无效载荷长度"（`MaxPayloadSize`） |
| 载荷越界 | 报"帧载荷被截断"，附帧号与所需/剩余字节 |
| 走完不等于文件长度 | 结构坏，报错 |
| 找不到 1003 / 解码失败 | 报"找不到开局锚点"或"可能与当前游戏协议版本不兼容" |
| 没有 1016 | **不报错**，结算相关字段留空，仍可看事件与统计 |
| 未知 cmdId | 跳过，不猜类型；需要时显示前 4096 字节十六进制 |
| 单个消息解码失败 | 该帧按"解析不出"处理，不影响其它帧 |

设计原则：**能明确报错的地方绝不用默认值糊过去**，因为静默的错误数据比缺数据更危险（尤其是"这局能不能放回游戏"这种判断）。

---

## 7. 已知坑

1. **目录名 = 文件名**：改名、只留目录、或把文件放进别的目录名里，游戏都认不出来（工具归档时严格保持 `<id>\<id>`）。
2. **游戏会删自己读不出的回放**（1.4）：工具在"放回"之前先跑一遍游戏自己的摘要解析，通过才放。
3. **满 10 局只是"不让存"**：已经有 10 局时点保存直接报上限；不影响播放，也不影响工具归档后再放回（归档是**移动**，会腾出名额）。
4. **`Winer` 是拼写如此**，写 `Winner` 取不到值。
5. **`FinishTime` 是 Unix 秒**，不是毫秒；`StartTime` 同理。
6. **字节序是大端**：`BitConverter`（小端）读帧头会得到荒唐的 cmdId/长度。
7. **快照是"全量 Room"**：一局下来 `1113` 帧占文件绝大部分体积（每个快照都带完整 Room + Players + Monsters），这是回放文件体积的主要来源；解析时不要把每个快照都留引用（工具只留最后一个）。
8. **最后一个快照不是行动**：游戏 `ExtractTurnNodes` 明确丢掉最后一个节点；工具 `TurnCount = 快照数 - 1` 与之一致。
9. **动作 `Data` 是嵌套消息**：不二次解码就只能看到"到达筹码地块"这类壳动作，拿不到买价/候选筹码。
10. **反编译源码是 GBK/双重编码**：中文注释和错误消息在 `ilspycmd` 输出里是乱码（英文错误码/字段名正常），读的时候按 GBK 解或用英文标识符判断，别依赖乱码中文。

---

## 8. 验证与复现

冒烟程序在 `replaytool-smoke\`（独立于工具项目，不进安装包）。`<appDir>` 一律填工具的输出目录
`replaytool\bin\Debug\net8.0-windows`（它需要 `Protocol\` 与 `GameData\` 在同一个目录里）。

```powershell
# 先构建
dotnet build replaytool-smoke\ReplayTool.Smoke.csproj

# 1) 解析单个回放（默认模式）：打印帧/回合/行动/玩家/事件/筹码统计，并自检角色表与素材
ReplayTool.Smoke.exe <appDir> <replayFile>
#    可选：追加期望值做回归断言（帧数、回合数）
ReplayTool.Smoke.exe <appDir> <replayFile> 2862 9
#    输出形如：PASS frames=2862 rounds=9 turns=44 players=4 events=692 relics=62

# 2) 回放库：归档 / 还原 / 淘汰托管（以真实回放目录为源）
ReplayTool.Smoke.exe --library-smoke <appDir> <realReplayRoot>

# 3) 配置目录规则：Documents 优先、不可写回退 AppData、旧配置迁移
ReplayTool.Smoke.exe --config-location-smoke <appDir>

# 4) 变速器安装 / 卸载（需要一个假的游戏目录）
ReplayTool.Smoke.exe --speedhack-smoke <appDir> <fakeGameDir>

# 5) 变速器诊断
ReplayTool.Smoke.exe --speedhack-diag <appDir>
```

也可以 `dotnet run --project replaytool-smoke -- <参数…>`（会自动构建）。

默认模式里内置的断言（可作为"解析口径没跑偏"的回归网）：角色表 ≥ 35 个且 101/102 号名称与称号正确、
`FrameCount > 0`、`RoundCount > 0`、`TurnCount > 0`、`MapId > 0`、有玩家、`GameVersion != "—"`、
**末帧 `CmdId == 1016`**、内嵌 webp ≥ 245、所有引用到的素材都能从内嵌资源直接流式打开。

实测样例（4 人局）：`frames=2862 rounds=9 turns=44 players=4 events=692 relics=62`。

自检的两条硬口径：

- 帧流格式正确 ⇒ **`Σ(6 + payloadLength) == 文件长度`** 必须严格相等；
- 解析口径正确 ⇒ 工具判定"健康"的回放，游戏自己的 `TryParseReplaySettlementOnly` 也必须返回非 null（工具就是直接调它）。

---

## 9. 附录

### 9.1 结构速查

```
<根目录> = %USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay
└─ <replayId>\
   └─ <replayId>                ← 唯一文件；6 字节帧头 + protobuf 载荷 的帧流
       帧 #0  cmdId=1003  RunningGameS2C   （开局锚点，Room.State=Running）
       帧 #1  cmdId=1113  ReplaySnapshotS2C（行动前快照）
       帧 #2  cmdId=1002  PredictActionS2C （行动）
       …
       帧 #N  cmdId=1016  GameFinishS2C    （结算，含 ReplayId/Version/Winer/FinishTime/Awards）
```

帧的顺序没有强制约束（解析靠"找锚点"而不是"按位置读"），但实测的两条规律很有用：
**首个 `1003` 是开局、末帧是 `1016` 结算**（冒烟程序就把"末帧 = 1016"当作断言）。

### 9.2 常量速查

| 常量 | 值 | 出处 |
| --- | --- | --- |
| 帧头长度 | 6 | `ReplayLoader.SERVER_PACK_HEADER_LENGTH` |
| 字节序 | 大端 | `ByteBuf.ReadShort/ReadInt` |
| 每局上限 | `MAX_BATTLE_RECORDS`（实测 10） | `StaticGlobalData` |
| 回放根目录 | `persistentDataPath + "/Temp/Replay"` | `ReplayLogic._replayRoot` |
| CDN（CN prod） | `https://sereplaycn.feimogames.com/prod/` | `ReplayLogic._cdnBaseUrl` |
| 摘要并发度 | 4 | `SUMMARY_PIPELINE_CONCURRENCY` |
| 加载遮罩最短时长 | 1000 ms | `MIN_MASK_DURATION_MS` |
| 重放帧间隔 | 16 ms | `ReplayConfig.FRAME_DELAY_MS` |
| 跳转等待 | 1000 ms | `ReplayConfig.JUMP_DELAY_MS` |
| 倍速序列 | 1 / 2 / 4 / 0.5 | `ReplayConfig.PLAY_SPEED_SEQUENCE` |
| 工具文件上限 | 512 MB | `ReplayAnalyzer.MaxFileSize` |
| 工具载荷上限 | 64 MB | `ReplayAnalyzer.MaxPayloadSize` |
| 工具十六进制回显上限 | 4096 字节 | `GameProtocolContext.FormatFrame` |

### 9.3 相关源码位置

| 主题 | 游戏侧（反编译） | 工具侧 |
| --- | --- | --- |
| 拆帧 / 校验 / 回合节点 | `GameLogic.Replay.ReplayLoader` | `Services/ReplayAnalyzer.cs`（`ReadFrames`） |
| 回放包与节点模型 | `GameLogic.Replay.ReplayPackage` / `ReplayTurnNode` / `ReplayRoundNode` / `ReplayFrame` / `TurnBehavior` | `Models.cs`（`ProtocolFrame` / `ReplayReport`） |
| 下载 / 保存 / 上限 / 列表 | `GameLogic.ReplayLogic` | `Services/ReplayLibraryService.cs` |
| 播放控制 | `GameLogic.ReplayLogic`（`StartPlaybackAsync` / `JumpToTurnAsync`）、`GameLogic.Replay.ReplayConfig` | — |
| 协议解码 / 权威校验 | `party.protocol.*`、`ByteBuf.ReadObject<T>` | `Services/GameProtocolContext.cs` |
| ID → 名称 | `StaticConfigure.*` | `Services/ConfigCatalog.cs` + `GameData/*.bin` |
| 界面入口 | `UI.AccountInfoWindow`（保存/播放按钮） | `Services/HomeDataService.cs`、`WebUI/` |
