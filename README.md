# Astral Replay

基于 WPF / .NET 8 + Microsoft Edge WebView2 的《吉星派对》离线回放分析器。WPF 负责本地文件、游戏 DLL 和 protobuf，HTML/CSS/JavaScript 负责界面渲染；原生 WPF 页面保留为回退入口。

## 运行

```powershell
dotnet run --project .\replaytool\AstralParty.ReplayTool.csproj
```

打开程序后选择回放文件，或把游戏缓存中的无扩展名回放文件拖入窗口。

发布版入口：`bin/Release/net8.0-windows/win-x64/publish/AstralParty.ReplayTool.exe`。需要系统安装 .NET 8 Desktop Runtime 和 WebView2 Evergreen Runtime（Windows 10/11 通常已有后者）。

## 功能

- 本地回放库：启动时自动读取 `%USERPROFILE%\AppData\LocalLow\feimo\AstralParty_CN\Temp\Replay`，首页按时间展示最近 100 个回放并可直接打开
- 浅色玩家界面：对局页只保留常用信息；统计与原始协议统一收纳在“专业信息”
- 游戏素材化展示：星币、生命、攻击、防御、治疗、移动、卡牌、怪物和筹码地块等优先使用素材库图标
- 回合时间线：每个回合独占一行，同回合事件横向排列
- 筹码来源：区分升星、任务、筹码地块购买，并按连续候选记录推算刷新次数
- 对局摘要：地图、结果、版本、时长、进度、Boss、奖励
- 玩家表现：角色、末局状态、伤害、承伤、治疗、移动、卡牌、技能、筹码
- 事件时间线：按回合、玩家和事件筛选
- 筹码记录：候选包、选择、刷新、名称与品质
- 原始协议：逐帧查看 protobuf JSON 或未知消息十六进制
- 统计信息：命令、去重 Action、筹码品质频率
- JSON 导出：包含完整事件和原始帧 Base64
- WebView2 消息桥：网页请求打开文件、解析、按需解码帧和导出
- 原生 WPF 回退：WebView2 不可用时仍可进入旧界面

## 图片资源

发布版把应用会用到的角色、地图、怪物、筹码和界面图片转换为保留透明度的无损 WebP，并作为标准 .NET `EmbeddedResource` 编入程序集。WebView2 通过虚拟素材地址直接读取程序集资源流；图片不会释放到用户目录，也不会生成素材缓存文件，因此发布版不依赖解包素材目录。

开发时仍可通过 `asset-sources.json` 的 `materialRoots` 按优先级指定多个游戏解包素材目录。`replaytool-assetpack` 会根据游戏配置和精确文件名生成素材清单，`scripts/pack_webp.py` 负责无损转换及像素级回读校验。

外部素材匹配只使用配置表资源字段、文件名和所在分类，不读取图片像素，也不调用图片识别：

- 地图：`MapImage` / `MapSceneImage`
- 角色：`CharacterMap`，其次是 `UT_Hero_ProfilePhoto_<角色ID>`
- 怪物：`CharacterMap`，其次是 `UT_Monster_Card_<怪物ID>` / `UT_Monster_Bust_<怪物ID>_0`
- 筹码：`Icon`，其次是 `UT_Relic_<筹码ID>`

只有精确、唯一的文件名结果才会打包；缺失或仍有歧义的素材保留占位，不会用相似图片猜测。图片缺失不会影响回放解析。

## 格式说明

回放按大端序读取，帧结构为 `[cmdId:int16][payloadLength:int32][payload]`。工具完全离线运行，不启动或修改游戏。
