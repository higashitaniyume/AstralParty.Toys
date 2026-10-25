## modloader-2.8.1

- 修复随包 `doorstop_config.json` 里的 SDK 版本没有同步到 2.8.0 的问题（仍为 `2.7.2`）：装出来的配置会声明偏低的 SDK 版本，声明要求更高版本的模组可能被误判为不兼容。
- 随包配置模板补上 `debugShell` 项（CesiumLoader Debug Shell，默认开启），新安装可以直接在配置文件里看到这个开关。

更新后重启游戏生效，已有模组配置保留。

