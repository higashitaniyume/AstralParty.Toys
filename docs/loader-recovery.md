# 加载器停用、恢复与本地历史

## 仅删除与恢复

「仅删除 version.dll」确认文件属于 CesiumLoader 后，先把当前安装文件写入配置根目录的本地历史并校验，然后只删除游戏主程序同目录的 `version.dll`。`version.pdb`、整个 `AstralParty_ModLoader` 及其他文件原样保留。文件被游戏占用时提示退出游戏重试。

恢复仅写回 `version.dll`，不重新安装组件或改写游戏内配置/清单。版本优先读取游戏安装清单，清单缺失时使用当前游戏的历史记录：

1. 使用当前版本记录的本地 DLL，并校验 SHA256。
2. DLL 缓存缺失时，尝试从匹配的本地发布包中提取。
3. 没有可用本地内容时，请求固定版本：
   `https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-{version}/cesium-loader-{version}.zip`。
4. 校验 ZIP 路径/大小、清单版本与 DLL SHA256；将包及 DLL 缓存后，仅恢复 DLL。

联网恢复不会使用 latest。内嵌 DLL 与同版本 GitHub Release DLL 可能来自不同构建，因此联网恢复校验对应版本发布包自己的清单哈希，并在本地记录实际恢复的哈希。游戏内旧清单保持原样，后续来源识别会使用与该清单对应的本地实际记录。

版本未知且本地也没有可靠的 DLL 时，提示重新安装或更新，不猜版本。下载失败/404/校验失败不写游戏目录；目标位置已有 DLL 时恢复拒绝覆盖。整体联网恢复限时 30 秒。

停用后安装或更新加载器会更新留存及配套组件，继续保留停用状态，点击恢复才重新启用。手动删除 DLL 的旧安装也能凭游戏清单按版本联网恢复。

桥接：`modDisableLoader`、`modRestoreLoader`；操作结束返回 `modRecoveryDone` 和刷新后的 `modStatus`。

## 本地历史

所有数据位于 Toys 已有配置根（默认用户文档的 `AstralPartyReplays`）下：

```text
modloader-history/
  files/<sha256>                       文件/ZIP 的内容缓存，按哈希去重
  games/<规范化游戏路径的sha256>/
    current.json                       当前版本、DLL/包哈希、停用状态、文件映射
    operations/<时间>-<唯一标识>.json   操作前后快照、输入文件哈希和 running/done/failed
```

内嵌安装、下载/本地发布包安装、Mod DLL 导入、SDK 导入、停用、恢复和完整卸载均留痕。覆盖前保存旧文件，写入前保存输入，成功后保存实际落盘快照。快照包括加载器与符号、根配置/清单、SDK、bootstrap、mods 的内容；运行日志、桥接状态及速度请求不纳入安装快照。备份失败阻止游戏文件变更。部分安装失败记录 failed，不把操作标为成功。

完整卸载保留配置根里的历史；不自动清理缓存。首版提供 DLL 恢复，整套历史版本回滚及清理界面尚未提供。

## 验证

```powershell
dotnet test tests/AstralParty.Toys.Tests/AstralParty.Toys.Tests.csproj -c Release
```

`LoaderRecoveryTests` 使用临时假游戏目录及可控 HTTP 响应验证只删除/恢复 DLL、固定版本下载、离线包缓存、坏包拒绝、更新后仍停用、多目录隔离和备份失败保护。
