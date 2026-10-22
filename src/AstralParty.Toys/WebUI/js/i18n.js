// i18n.js - Lightweight internationalization engine and translation dictionaries for AstralParty.Toys
(function () {
  'use strict';

  const LANGUAGES = {
    'zh-Hans': { name: '简体中文', locale: 'zh-CN' },
    'en': { name: 'English', locale: 'en-US' },
    'ja': { name: '日本語', locale: 'ja-JP' },
    'zh-Hant': { name: '繁體中文', locale: 'zh-TW' }
  };

  const TRANSLATIONS = {
    'zh-Hans': {
      // Nav
      'nav.brand_sub': 'REPLAY & COMPANION TOOLBOX',
      'nav.home': '游戏大厅',
      'nav.replays': '回放',
      'nav.mods': '模组',
      'nav.skins': '皮肤',
      'nav.wiki': '维基攻略',
      'nav.settings': '系统设置',
      'nav.version_tooltip': '查看详细版本信息',

      // Common
      'common.confirm': '确认',
      'common.cancel': '取消',
      'common.close': '关闭',
      'common.save': '保存',
      'common.delete': '删除',
      'common.refresh': '刷新',
      'common.loading': '加载中...',
      'common.success': '成功',
      'common.error': '错误',
      'common.warning': '警告',
      'common.tip': '提示',
      'common.browse': '浏览',
      'common.open_folder': '打开目录',
      'common.reset': '重置',
      'common.all': '全部',
      'common.none': '无',
      'common.copy': '复制',
      'common.copied': '已复制到剪贴板',

      // Home
      'home.title': '游戏大厅',
      'home.launch_game': '启动游戏',
      'home.launching': '正在启动...',
      'home.launch_steam': '从 Steam 启动',
      'home.launch_bypass': '绕过 Steam 启动',
      'home.game_version': '启动版本',
      'home.status_ready': '就绪',
      'home.status_running': '游戏运行中',
      'home.quick_replay': '快速复盘',
      'home.quick_mods': '模组管理',
      'home.quick_skins': '皮肤方案',
      'home.quick_settings': '偏好设置',
      'home.recent_replays': '最近对局',
      'home.no_recent_replays': '暂无最近回放记录',

      // Replay
      'replay.title': '对局回放与复盘',
      'replay.open_file': '打开回放文件',
      'replay.download_id': '按 ID 下载回放',
      'replay.download_placeholder': '输入 16 位对局回放 ID...',
      'replay.download_btn': '拉取入库',
      'replay.library': '回放库',
      'replay.summary': '对局概览',
      'replay.timeline': '事件时间线',
      'replay.relics': '筹码流转',
      'replay.frames': '帧流检查',
      'replay.ai_summary': 'AI 复盘摘要',
      'replay.copy_ai': '复制 AI Prompt',
      'replay.filter_all': '全部玩家',
      'replay.stat_hero': '角色',
      'replay.stat_hp': '生命',
      'replay.stat_atk_def': '攻/防',
      'replay.stat_coins': '星币',
      'replay.stat_damage': '造成伤害',
      'replay.stat_taken': '承受伤害',
      'replay.stat_heal': '治疗量',
      'replay.stat_move': '移动点数',
      'replay.stat_cards': '用牌数',
      'replay.stat_skills': '技能数',
      'replay.stat_relics': '筹码数',
      'replay.round': '回合',
      'replay.col_time': '时间',
      'replay.col_map': '地图',
      'replay.col_winner': '获胜者',
      'replay.col_rounds': '总回合',
      'replay.col_actions': '操作',
      'replay.back_to_lobby': '‹ 返回大厅',
      'replay.no_file_loaded': '尚未打开回放文件',
      'replay.export_json': '导出 JSON',
      'replay.tab_summary': '对局摘要',
      'replay.tab_players': '玩家表现',
      'replay.tab_performance': '表现分析',
      'replay.tab_timeline': '事件时间线',
      'replay.tab_relics': '筹码记录',
      'replay.tab_shops': '商店记录',
      'replay.tab_technical': '专业信息',
      'replay.tech_stats': '数据统计',
      'replay.tech_protocol': '原始协议帧',
      'replay.select_file_title': '选择回放文件',
      'replay.select_file_desc': '手动打开任意回放文件，本地解析玩家、回合、动作、筹码流转与底层协议帧。',
      'replay.select_file_btn': '＋ 选择回放文件',
      'replay.offline_note': '所有数据均在本地安全离线处理',
      'replay.search_placeholder': '搜索回放 ID / 地图 / 玩家…',
      'replay.filter_all_sources': '全部来源',
      'replay.filter_game_only': '仅游戏内',
      'replay.filter_library_only': '仅回放库',
      'replay.sort_time': '按对局时间',
      'replay.sort_size': '按文件大小',
      'replay.sort_id': '按回放 ID',
      'replay.game_slots': '游戏内席位',
      'replay.open_library_folder': '打开库目录',
      'replay.badge_game': '游戏内',
      'replay.badge_library': '回放库',
      'replay.badge_unparseable': '无法解析',
      'replay.action_view': '查看',
      'replay.action_archive': '归档',
      'replay.action_restore': '放回游戏',
      'replay.action_upgrade': '升级',
      'replay.upgrade_tooltip': '升级到本工具支持的 {version}（原文件会备份）',
      'replay.upgrade_current': '已是当前支持版本',
      'replay.upgrade_newer': '版本高于本工具支持的 {version}',
      'replay.upgrade_unknown': '文件里没有版本标记',
      'replay.summary_map': '本局地图',
      'replay.summary_result': '对局结果',
      'replay.summary_awards': '结算奖励',
      'replay.summary_duration': '时长',
      'replay.summary_progress': '关卡进度',
      'replay.summary_deaths': '死亡计数',
      'replay.summary_players': '参战玩家',
      'replay.th_round': '回合',
      'replay.th_player': '玩家',
      'replay.th_hero': '角色',
      'replay.th_source': '来源',
      'replay.th_action': '操作',
      'replay.th_relic_name': '筹码名称',
      'replay.th_quality': '品质',
      'replay.th_rerolls': '刷新记录',
      'replay.th_options': '候选选项',
      'replay.th_shop_type': '商店类型',
      'replay.th_price': '卡牌单价',
      'replay.th_shop_cards': '候选卡牌',
      'replay.th_purchase_result': '购买结果',

      // Mods & Speedhack
      'mods.title': '模组与加载器管理',
      'mods.loader_title': 'CesiumLoader 加载器',
      'mods.installed_ver': '已安装版本',
      'mods.builtin_ver': '随本程序内置',
      'mods.latest_ver': 'GitHub 最新版',
      'mods.install_btn': '安装加载器',
      'mods.update_btn': '更新加载器',
      'mods.uninstall_btn': '卸载加载器',
      'mods.check_update': '检查更新',
      'mods.loader_settings': '加载器设置',
      'mods.speed_title': '游戏变速',
      'mods.speed_enabled': '开启变速',
      'mods.speed_base': '基础倍速',
      'mods.speed_hotkey': '允许 Mod 热键变速',
      'mods.steam_bypass': 'Steam 客户端绕过',
      'mods.installed_mods': '已安装模组',
      'mods.import_mod': '导入 Mod (DLL/ZIP)',
      'mods.tag_builtin': '内置',
      'mods.tag_can_operate': '可操作游戏',
      'mods.open_mods_folder': '打开 Mods 目录',
      'mods.open_logs_folder': '打开日志目录',
      'mods.current_game': '当前游戏',
      'mods.manage_game_dirs': '管理游戏目录…',
      'mods.import_package': '导入发布包',

      // Skins
      'skins.title': '角色与卡牌皮肤工坊',
      'skins.schemes': '多角色皮肤方案',
      'skins.card_skins': '卡面替换',
      'skins.dynamic_cards': '动态卡面 (GIF/视频)',
      'skins.char_skins': '角色全身像替换',
      'skins.resource_mods': '资源模组',
      'resource_mods.import': '＋ 导入资源模组 (.zip)',
      'resource_mods.apply': '应用',
      'resource_mods.restore': '恢复原版',
      'resource_mods.open_folder': '打开目录',
      'resource_mods.empty_title': '还没有资源模组',
      'resource_mods.empty_desc': '导入 RML 格式的 ZIP（包内含 .bundle 文件），安装后由 Toys 改写 Addressables catalog 生效；重启游戏后可见。',
      'resource_mods.status_version': '资源版本 {v}',
      'resource_mods.status_applied': '已应用 {n} 处重定向',
      'resource_mods.status_not_applied': '尚未应用',
      'resource_mods.status_pending': '有改动待应用',
      'resource_mods.status_running': '游戏运行中，修改前请先退出',
      'resource_mods.bundles': '{n} 个条目',
      'resource_mods.matched': '{n} 可替换',
      'resource_mods.unmatched_summary': '{n} 个 bundle 未匹配（可能是旧版本资源）',
      'resource_mods.enabled': '已启用',
      'resource_mods.disabled': '已停用',
      'resource_mods.toggle_on': '启用',
      'resource_mods.toggle_off': '停用',
      'resource_mods.delete': '删除',
      'resource_mods.delete_title': '删除资源模组',
      'resource_mods.delete_body': '确定删除「{name}」？安装的文件会被移除；如果它正在生效，Toys 会先按剩余模组重新生成 catalog。',
      'skins.new_scheme': '新建方案',
      'skins.import_zip': '导入方案 ZIP',
      'skins.export_zip': '导出方案 ZIP',
      'skins.apply_scheme': '应用方案到游戏',
      'skins.restore_default': '恢复官方默认',
      'skins.import_legacy': '旧版 Bundle 导入',
      'skins.crop_title': '卡面/贴图裁剪',
      'skins.open_folder': '打开皮肤目录',
      'skins.download_default': '下载默认包',
      'skins.restore_builtin': '恢复内置默认包',
      'skins.my_schemes': '我的方案',
      'skins.packages_library': '皮肤包素材库',

      // Wiki
      'wiki.open_external': '浏览器打开',

      // Settings
      'settings.title': '系统设置与偏好',
      'settings.group_general': '通用偏好',
      'settings.language': '界面与配表语言',
      'settings.lang_auto': '跟随系统语言 (Auto)',
      'settings.motion': '界面平滑过渡动效',
      'settings.motion_desc': '开启后在页面切换与弹窗展开时使用平滑动画',
      'settings.group_library': '回放库托管',
      'settings.library_root': '回放库存储目录',
      'settings.auto_maintain': '自动归档托管',
      'settings.auto_maintain_desc': '自动将游戏产生的旧回放移入本地库，防止被游戏清理',
      'settings.keep_count': '游戏目录保留局数',
      'settings.maintain_now': '立即执行归档清理',
      'settings.group_about': '关于 AstralParty.Toys',
      'settings.about_desc': '吉星派对 Windows 本地离线工具箱，整合复盘、模组、皮肤与变速管理。',
      'settings.github_repo': 'GitHub 开源主页',
      'settings.speedhack_repo': 'speedhack-rs 变速项目',
      'settings.group_game_dirs': '游戏目录（多个位置）',
      'settings.scan_system': '扫描系统',
      'settings.browse_add': '浏览添加…',

      // Utilities
      'utilities.title': '实用工具箱',
      'utilities.clean_cache': '清理游戏临时缓存',
      'utilities.clean_cache_btn': '一键清理',
      'utilities.ffmpeg_title': 'FFmpeg 动态卡面转换工具',
      'utilities.ffmpeg_status': 'FFmpeg 状态',
      'utilities.ffmpeg_download': '下载 / 修复 FFmpeg'
    },

    'en': {
      // Nav
      'nav.brand_sub': 'REPLAY & COMPANION TOOLBOX',
      'nav.home': 'Lobby',
      'nav.replays': 'Replays',
      'nav.mods': 'Mods',
      'nav.skins': 'Skins',
      'nav.wiki': 'Wiki',
      'nav.settings': 'Settings',
      'nav.version_tooltip': 'View detailed version info',

      // Common
      'common.confirm': 'Confirm',
      'common.cancel': 'Cancel',
      'common.close': 'Close',
      'common.save': 'Save',
      'common.delete': 'Delete',
      'common.refresh': 'Refresh',
      'common.loading': 'Loading...',
      'common.success': 'Success',
      'common.error': 'Error',
      'common.warning': 'Warning',
      'common.tip': 'Tip',
      'common.browse': 'Browse',
      'common.open_folder': 'Open Folder',
      'common.reset': 'Reset',
      'common.all': 'All',
      'common.none': 'None',
      'common.copy': 'Copy',
      'common.copied': 'Copied to clipboard',

      // Home
      'home.title': 'Lobby',
      'home.launch_game': 'Launch Game',
      'home.launching': 'Launching...',
      'home.launch_steam': 'Launch via Steam',
      'home.launch_bypass': 'Bypass Steam Launch',
      'home.game_version': 'Target Profile',
      'home.status_ready': 'Ready',
      'home.status_running': 'Game is running',
      'home.quick_replay': 'Replay Analysis',
      'home.quick_mods': 'Mod Manager',
      'home.quick_skins': 'Skin Schemes',
      'home.quick_settings': 'Preferences',
      'home.recent_replays': 'Recent Matches',
      'home.no_recent_replays': 'No recent replays found',

      // Replay
      'replay.title': 'Replay Analysis & Review',
      'replay.open_file': 'Open Replay File',
      'replay.download_id': 'Download by Match ID',
      'replay.download_placeholder': 'Enter 16-digit replay ID...',
      'replay.download_btn': 'Fetch Replay',
      'replay.library': 'Library',
      'replay.summary': 'Match Overview',
      'replay.timeline': 'Event Timeline',
      'replay.relics': 'Relic Timeline',
      'replay.frames': 'Frame Inspector',
      'replay.ai_summary': 'AI Summary',
      'replay.copy_ai': 'Copy AI Prompt',
      'replay.filter_all': 'All Players',
      'replay.stat_hero': 'Hero',
      'replay.stat_hp': 'HP',
      'replay.stat_atk_def': 'Atk/Def',
      'replay.stat_coins': 'Star Coins',
      'replay.stat_damage': 'Dmg Dealt',
      'replay.stat_taken': 'Dmg Taken',
      'replay.stat_heal': 'Healing',
      'replay.stat_move': 'Move Pts',
      'replay.stat_cards': 'Cards',
      'replay.stat_skills': 'Skills',
      'replay.stat_relics': 'Relics',
      'replay.round': 'Round',
      'replay.col_time': 'Time',
      'replay.col_map': 'Map',
      'replay.col_winner': 'Winner',
      'replay.col_rounds': 'Rounds',
      'replay.col_actions': 'Actions',
      'replay.back_to_lobby': '‹ Back to Lobby',
      'replay.no_file_loaded': 'No replay file loaded',
      'replay.export_json': 'Export JSON',
      'replay.tab_summary': 'Summary',
      'replay.tab_players': 'Players',
      'replay.tab_performance': 'Performance',
      'replay.tab_timeline': 'Timeline',
      'replay.tab_relics': 'Relics',
      'replay.tab_shops': 'Shops',
      'replay.tab_technical': 'Technical',
      'replay.tech_stats': 'Statistics',
      'replay.tech_protocol': 'Raw Protocol',
      'replay.select_file_title': 'Select Replay File',
      'replay.select_file_desc': 'Open any local replay file to inspect players, rounds, actions, relics, and raw frames.',
      'replay.select_file_btn': '＋ Select Replay File',
      'replay.offline_note': 'All data is processed locally and offline',
      'replay.search_placeholder': 'Search replay ID / map / player…',
      'replay.filter_all_sources': 'All Sources',
      'replay.filter_game_only': 'In-Game Only',
      'replay.filter_library_only': 'Library Only',
      'replay.sort_time': 'By Match Time',
      'replay.sort_size': 'By File Size',
      'replay.sort_id': 'By Replay ID',
      'replay.game_slots': 'In-Game Slots',
      'replay.open_library_folder': 'Open Library Folder',
      'replay.badge_game': 'In-Game',
      'replay.badge_library': 'Library',
      'replay.badge_unparseable': 'Unparseable',
      'replay.action_view': 'View',
      'replay.action_archive': 'Archive',
      'replay.action_restore': 'Restore',
      'replay.action_upgrade': 'Upgrade',
      'replay.upgrade_tooltip': 'Upgrade to {version}, supported by this tool (the original is backed up)',
      'replay.upgrade_current': 'Already on the supported version',
      'replay.upgrade_newer': 'Newer than this tool supports ({version})',
      'replay.upgrade_unknown': 'No version marker in this file',
      'replay.summary_map': 'Map',
      'replay.summary_result': 'Match Result',
      'replay.summary_awards': 'Rewards',
      'replay.summary_duration': 'Duration',
      'replay.summary_progress': 'Progress',
      'replay.summary_deaths': 'Deaths',
      'replay.summary_players': 'Players',
      'replay.th_round': 'Round',
      'replay.th_player': 'Player',
      'replay.th_hero': 'Hero',
      'replay.th_source': 'Source',
      'replay.th_action': 'Action',
      'replay.th_relic_name': 'Relic Name',
      'replay.th_quality': 'Rarity',
      'replay.th_rerolls': 'Rerolls',
      'replay.th_options': 'Offered Options',
      'replay.th_shop_type': 'Shop Type',
      'replay.th_price': 'Unit Price',
      'replay.th_shop_cards': 'Offered Cards',
      'replay.th_purchase_result': 'Purchase Result',

      // Mods & Speedhack
      'mods.title': 'Mod & Loader Management',
      'mods.loader_title': 'CesiumLoader',
      'mods.installed_ver': 'Installed Version',
      'mods.builtin_ver': 'Built-in Version',
      'mods.latest_ver': 'GitHub Latest',
      'mods.install_btn': 'Install Loader',
      'mods.update_btn': 'Update Loader',
      'mods.uninstall_btn': 'Uninstall Loader',
      'mods.check_update': 'Check Updates',
      'mods.loader_settings': 'Loader Settings',
      'mods.speed_title': 'Speedhack',
      'mods.speed_enabled': 'Enable Speedhack',
      'mods.speed_base': 'Base Speed',
      'mods.speed_hotkey': 'Allow Mod Hotkey Speed',
      'mods.steam_bypass': 'Steam Bypass',
      'mods.installed_mods': 'Installed Mods',
      'mods.import_mod': 'Import Mod (DLL/ZIP)',
      'mods.tag_builtin': 'Built-in',
      'mods.tag_can_operate': 'Can Operate',
      'mods.open_mods_folder': 'Open Mods Folder',
      'mods.open_logs_folder': 'Open Logs Folder',
      'mods.current_game': 'Current Game',
      'mods.manage_game_dirs': 'Manage Directories…',
      'mods.import_package': 'Import Package',

      // Skins
      'skins.title': 'Character & Card Skins Workshop',
      'skins.schemes': 'Character Schemes',
      'skins.card_skins': 'Card Faces',
      'skins.dynamic_cards': 'Dynamic Cards (GIF/Video)',
      'skins.char_skins': 'Character Portraits',
      'skins.resource_mods': 'Resource Mods',
      'resource_mods.import': '＋ Import Resource Mod (.zip)',
      'resource_mods.apply': 'Apply',
      'resource_mods.restore': 'Restore Original',
      'resource_mods.open_folder': 'Open Folder',
      'resource_mods.empty_title': 'No resource mods yet',
      'resource_mods.empty_desc': 'Import an RML-format ZIP containing .bundle files. Toys rewrites the Addressables catalog on install; restart the game to see the changes.',
      'resource_mods.status_version': 'Resource version {v}',
      'resource_mods.status_applied': 'Applied {n} redirects',
      'resource_mods.status_not_applied': 'Not applied',
      'resource_mods.status_pending': 'Changes pending',
      'resource_mods.status_running': 'Game is running; exit before making changes',
      'resource_mods.bundles': '{n} entries',
      'resource_mods.matched': '{n} replaceable',
      'resource_mods.unmatched_summary': '{n} bundles unmatched (possibly from an older game version)',
      'resource_mods.enabled': 'Enabled',
      'resource_mods.disabled': 'Disabled',
      'resource_mods.toggle_on': 'Enable',
      'resource_mods.toggle_off': 'Disable',
      'resource_mods.delete': 'Delete',
      'resource_mods.delete_title': 'Delete resource mod',
      'resource_mods.delete_body': 'Delete "{name}"? Installed files will be removed; if it is active, Toys regenerates the catalog from the remaining mods first.',
      'skins.new_scheme': 'New Scheme',
      'skins.import_zip': 'Import Scheme ZIP',
      'skins.export_zip': 'Export Scheme ZIP',
      'skins.apply_scheme': 'Apply to Game',
      'skins.restore_default': 'Restore Official Defaults',
      'skins.import_legacy': 'Import Legacy Bundle',
      'skins.crop_title': 'Crop Texture',
      'skins.open_folder': 'Open Skins Folder',
      'skins.download_default': 'Download Default Pack',
      'skins.restore_builtin': 'Restore Built-in Pack',
      'skins.my_schemes': 'My Schemes',
      'skins.packages_library': 'Asset Library',

      // Wiki
      'wiki.open_external': 'Open in Browser',

      // Settings
      'settings.title': 'Preferences & Settings',
      'settings.group_general': 'General Preferences',
      'settings.language': 'Language',
      'settings.lang_auto': 'System Default (Auto)',
      'settings.motion': 'Interface Transitions & Animations',
      'settings.motion_desc': 'Enable smooth page transitions and modal animations',
      'settings.group_library': 'Replay Library Maintenance',
      'settings.library_root': 'Library Root Directory',
      'settings.auto_maintain': 'Automatic Archival',
      'settings.auto_maintain_desc': 'Automatically archive older replays to prevent game cleanup',
      'settings.keep_count': 'Matches Kept in Game Directory',
      'settings.maintain_now': 'Maintain Library Now',
      'settings.group_about': 'About AstralParty.Toys',
      'settings.about_desc': 'Astral Party companion toolbox for offline replays, mods, skins, and speedhack.',
      'settings.github_repo': 'GitHub Repository',
      'settings.speedhack_repo': 'speedhack-rs Project',
      'settings.group_game_dirs': 'Game Directories',
      'settings.scan_system': 'Scan System',
      'settings.browse_add': 'Browse & Add…',

      // Utilities
      'utilities.title': 'Utilities',
      'utilities.clean_cache': 'Clean Game Temp Cache',
      'utilities.clean_cache_btn': 'Clean Cache',
      'utilities.ffmpeg_title': 'FFmpeg Video Converter Tool',
      'utilities.ffmpeg_status': 'FFmpeg Status',
      'utilities.ffmpeg_download': 'Download / Repair FFmpeg'
    },

    'ja': {
      // Nav
      'nav.brand_sub': 'REPLAY & COMPANION TOOLBOX',
      'nav.home': 'ロビー',
      'nav.replays': 'リプレイ',
      'nav.mods': 'Mod管理',
      'nav.skins': 'スキン',
      'nav.wiki': '攻略Wiki',
      'nav.settings': '設定',
      'nav.version_tooltip': 'バージョン詳細を確認',

      // Common
      'common.confirm': '確認',
      'common.cancel': 'キャンセル',
      'common.close': '閉じる',
      'common.save': '保存',
      'common.delete': '削除',
      'common.refresh': '更新',
      'common.loading': '読み込み中...',
      'common.success': '成功',
      'common.error': 'エラー',
      'common.warning': '警告',
      'common.tip': 'ヒント',
      'common.browse': '参照',
      'common.open_folder': 'フォルダーを開く',
      'common.reset': 'リセット',
      'common.all': 'すべて',
      'common.none': 'なし',
      'common.copy': 'コピー',
      'common.copied': 'クリップボードにコピーしました',

      // Home
      'home.title': 'ロビー',
      'home.launch_game': 'ゲーム起動',
      'home.launching': '起動中...',
      'home.launch_steam': 'Steam経由で起動',
      'home.launch_bypass': 'Steamバイパスで起動',
      'home.game_version': '起動プロファイル',
      'home.status_ready': '準備完了',
      'home.status_running': 'ゲーム実行中',
      'home.quick_replay': 'リプレイ分析',
      'home.quick_mods': 'Mod管理',
      'home.quick_skins': 'スキン設定',
      'home.quick_settings': 'システム設定',
      'home.recent_replays': '最近の対局',
      'home.no_recent_replays': '最近のリプレイ記録はありません',

      // Replay
      'replay.title': '対局リプレイ＆復盤分析',
      'replay.open_file': 'リプレイファイルを開く',
      'replay.download_id': '対局IDでダウンロード',
      'replay.download_placeholder': '16桁の対局IDを入力...',
      'replay.download_btn': 'ダウンロード',
      'replay.library': 'リプレイ保管庫',
      'replay.summary': '対局概要',
      'replay.timeline': 'タイムライン',
      'replay.relics': 'レリック推移',
      'replay.frames': 'パケット検証',
      'replay.ai_summary': 'AI復盤サマリー',
      'replay.copy_ai': 'AIプロンプトをコピー',
      'replay.filter_all': '全プレイヤー',
      'replay.stat_hero': 'キャラ',
      'replay.stat_hp': 'HP',
      'replay.stat_atk_def': '攻/防',
      'replay.stat_coins': 'スターコイン',
      'replay.stat_damage': '与ダメージ',
      'replay.stat_taken': '被ダメージ',
      'replay.stat_heal': '回復量',
      'replay.stat_move': '移動点',
      'replay.stat_cards': 'カード数',
      'replay.stat_skills': 'スキル数',
      'replay.stat_relics': 'レリック数',
      'replay.round': 'ラウンド',
      'replay.col_time': '日時',
      'replay.col_map': 'マップ',
      'replay.col_winner': '勝者',
      'replay.col_rounds': '総R数',
      'replay.col_actions': '操作',
      'replay.back_to_lobby': '‹ ロビーに戻る',
      'replay.no_file_loaded': 'リプレイファイルが読み込まれていません',
      'replay.export_json': 'JSONエクスポート',
      'replay.tab_summary': '対局サマリー',
      'replay.tab_players': 'プレイヤー成績',
      'replay.tab_performance': 'パフォーマンス分析',
      'replay.tab_timeline': 'タイムライン',
      'replay.tab_relics': 'レリック推移',
      'replay.tab_shops': 'ショップ履歴',
      'replay.tab_technical': '技術詳細',
      'replay.tech_stats': 'データ統計',
      'replay.tech_protocol': 'プロトコルフレーム',
      'replay.select_file_title': 'リプレイファイルを選択',
      'replay.select_file_desc': '任意のリプレイファイルをローカルで開き、プレイヤー、ラウンド、アクション、レリックの推移およびフレームを解析します。',
      'replay.select_file_btn': '＋ リプレイファイルを選択',
      'replay.offline_note': 'すべてのデータは安全にローカルでオフライン処理されます',
      'replay.search_placeholder': 'リプレイID / マップ / プレイヤーを検索…',
      'replay.filter_all_sources': 'すべてのソース',
      'replay.filter_game_only': 'ゲーム内のみ',
      'replay.filter_library_only': '保管庫のみ',
      'replay.sort_time': '対局日時順',
      'replay.sort_size': 'ファイルサイズ順',
      'replay.sort_id': 'リプレイID順',
      'replay.game_slots': 'ゲーム内スロット',
      'replay.open_library_folder': 'フォルダーを開く',
      'replay.badge_game': 'ゲーム内',
      'replay.badge_library': '保管庫',
      'replay.badge_unparseable': '解析不可',
      'replay.action_view': '確認',
      'replay.action_archive': '保管',
      'replay.action_restore': 'ゲームに戻す',
      'replay.action_upgrade': 'アップグレード',
      'replay.upgrade_tooltip': 'このツールが対応する {version} にアップグレード（元ファイルはバックアップされます）',
      'replay.upgrade_current': '既にサポート対象バージョンです',
      'replay.upgrade_newer': 'このツールの対応バージョン {version} より新しい',
      'replay.upgrade_unknown': 'ファイルにバージョン情報がありません',
      'replay.summary_map': '対局マップ',
      'replay.summary_result': '対局結果',
      'replay.summary_awards': 'クリア報酬',
      'replay.summary_duration': '所要時間',
      'replay.summary_progress': '進行状況',
      'replay.summary_deaths': '死亡回数',
      'replay.summary_players': '参加プレイヤー',
      'replay.th_round': 'ラウンド',
      'replay.th_player': 'プレイヤー',
      'replay.th_hero': 'キャラ',
      'replay.th_source': '入手元',
      'replay.th_action': 'アクション',
      'replay.th_relic_name': 'レリック名',
      'replay.th_quality': 'レアリティ',
      'replay.th_rerolls': 'リロール履歴',
      'replay.th_options': '候補リスト',
      'replay.th_shop_type': 'ショップ種別',
      'replay.th_price': '単価',
      'replay.th_shop_cards': '販売カード',
      'replay.th_purchase_result': '購入結果',

      // Mods & Speedhack
      'mods.title': 'Mod＆ローダー管理',
      'mods.loader_title': 'CesiumLoader',
      'mods.installed_ver': '導入済みバージョン',
      'mods.builtin_ver': '同梱バージョン',
      'mods.latest_ver': 'GitHub最新版',
      'mods.install_btn': 'ローダーをインストール',
      'mods.update_btn': 'ローダーを更新',
      'mods.uninstall_btn': 'ローダーをアンインストール',
      'mods.check_update': '更新確認',
      'mods.loader_settings': 'ローダー詳細設定',
      'mods.speed_title': 'ゲーム変速 (Speedhack)',
      'mods.speed_enabled': '変速機能を有効化',
      'mods.speed_base': '基本倍速',
      'mods.speed_hotkey': 'Modホットキー変速を許可',
      'mods.steam_bypass': 'Steamクライアントバイパス',
      'mods.installed_mods': '導入済みMod一覧',
      'mods.import_mod': 'Modを追加 (DLL/ZIP)',
      'mods.tag_builtin': '内蔵',
      'mods.tag_can_operate': 'ゲーム操作可能',
      'mods.open_mods_folder': 'Modsフォルダーを開く',
      'mods.open_logs_folder': 'ログフォルダーを開く',
      'mods.current_game': '現在のゲーム',
      'mods.manage_game_dirs': 'ゲームディレクトリ管理…',
      'mods.import_package': 'パッケージを導入',

      // Skins
      'skins.title': 'キャラ＆カードスキン工房',
      'skins.schemes': 'キャラ別スキン構成',
      'skins.card_skins': 'カードイラスト変更',
      'skins.dynamic_cards': 'アニメーションカード (GIF/動画)',
      'skins.char_skins': '立ち絵変更',
      'skins.resource_mods': 'リソースMOD',
      'resource_mods.import': '＋ リソースMODをインポート (.zip)',
      'resource_mods.apply': '適用',
      'resource_mods.restore': '元に戻す',
      'resource_mods.open_folder': 'フォルダーを開く',
      'resource_mods.empty_title': 'リソースMODはまだありません',
      'resource_mods.empty_desc': '.bundle を含む RML 形式の ZIP をインポートします。インストール時に Toys が Addressables カタログを書き換えます。ゲーム再起動後に反映されます。',
      'resource_mods.status_version': 'リソースバージョン {v}',
      'resource_mods.status_applied': '{n} 件のリダイレクトを適用済み',
      'resource_mods.status_not_applied': '未適用',
      'resource_mods.status_pending': '未適用の変更あり',
      'resource_mods.status_running': 'ゲーム実行中です。変更前に終了してください',
      'resource_mods.bundles': '{n} 項目',
      'resource_mods.matched': '{n} 個置換可能',
      'resource_mods.unmatched_summary': '{n} 個の bundle が未一致（旧バージョンの可能性）',
      'resource_mods.enabled': '有効',
      'resource_mods.disabled': '無効',
      'resource_mods.toggle_on': '有効化',
      'resource_mods.toggle_off': '無効化',
      'resource_mods.delete': '削除',
      'resource_mods.delete_title': 'リソースMODを削除',
      'resource_mods.delete_body': '「{name}」を削除しますか？インストール済みファイルは削除されます。適用中の場合は、残りのMODでカタログを再生成します。',
      'skins.new_scheme': '新規構成',
      'skins.import_zip': '構成ZIPをインポート',
      'skins.export_zip': '構成ZIPをエクスポート',
      'skins.apply_scheme': 'ゲームに適用',
      'skins.restore_default': '公式デフォルトに戻す',
      'skins.import_legacy': '旧形式Bundle変換',
      'skins.crop_title': '画像トリミング',
      'skins.open_folder': 'スキンフォルダーを開く',
      'skins.download_default': '初期パッケージを取得',
      'skins.restore_builtin': '同梱初期設定に戻す',
      'skins.my_schemes': 'マイ構成',
      'skins.packages_library': '素材ライブラリ',

      // Wiki
      'wiki.open_external': 'ブラウザで開く',

      // Settings
      'settings.title': '環境設定',
      'settings.group_general': '全般設定',
      'settings.language': '表示言語',
      'settings.lang_auto': 'システム言語に従う (Auto)',
      'settings.motion': 'なめらかな画面アニメーション',
      'settings.motion_desc': '画面切り替え時のトランジション演出を有効にします',
      'settings.group_library': 'リプレイ自動保管',
      'settings.library_root': 'リプレイ保管先フォルダー',
      'settings.auto_maintain': '自動アーカイブ',
      'settings.auto_maintain_desc': 'ゲーム本体の古い対局リプレイを自動的に退避・保護します',
      'settings.keep_count': 'ゲーム内に保持する局数',
      'settings.maintain_now': '今すぐ整理を実行',
      'settings.group_about': 'AstralParty.Toys について',
      'settings.about_desc': '吉星派対（Astral Party）向けオフラインリプレイ分析・Mod・スキン管理ツール。',
      'settings.github_repo': 'GitHubリポジトリ',
      'settings.speedhack_repo': 'speedhack-rs プロジェクト',
      'settings.group_game_dirs': 'ゲームインストール先（複数管理）',
      'settings.scan_system': 'システムをスキャン',
      'settings.browse_add': '参照して追加…',

      // Utilities
      'utilities.title': '便利ツール',
      'utilities.clean_cache': 'ゲーム一時キャッシュの削除',
      'utilities.clean_cache_btn': 'キャッシュを削除',
      'utilities.ffmpeg_title': 'FFmpeg動画変換ツール',
      'utilities.ffmpeg_status': 'FFmpeg状態',
      'utilities.ffmpeg_download': 'FFmpegをダウンロード/修復'
    },

    'zh-Hant': {
      // Nav
      'nav.brand_sub': 'REPLAY & COMPANION TOOLBOX',
      'nav.home': '遊戲大廳',
      'nav.replays': '回放',
      'nav.mods': '模組',
      'nav.skins': '皮膚',
      'nav.wiki': '維基攻略',
      'nav.settings': '系統設定',
      'nav.version_tooltip': '查看詳細版本資訊',

      // Common
      'common.confirm': '確認',
      'common.cancel': '取消',
      'common.close': '關閉',
      'common.save': '儲存',
      'common.delete': '刪除',
      'common.refresh': '重新整理',
      'common.loading': '載入中...',
      'common.success': '成功',
      'common.error': '錯誤',
      'common.warning': '警告',
      'common.tip': '提示',
      'common.browse': '瀏覽',
      'common.open_folder': '開啟資料夾',
      'common.reset': '重設',
      'common.all': '全部',
      'common.none': '無',
      'common.copy': '複製',
      'common.copied': '已複製到剪貼簿',

      // Home
      'home.title': '遊戲大廳',
      'home.launch_game': '啟動遊戲',
      'home.launching': '正在啟動...',
      'home.launch_steam': '從 Steam 啟動',
      'home.launch_bypass': '繞過 Steam 啟動',
      'home.game_version': '啟動版本',
      'home.status_ready': '就緒',
      'home.status_running': '遊戲執行中',
      'home.quick_replay': '快速複盤',
      'home.quick_mods': '模組管理',
      'home.quick_skins': '造型方案',
      'home.quick_settings': '偏好設定',
      'home.recent_replays': '最近對局',
      'home.no_recent_replays': '暫無最近重播記錄',

      // Replay
      'replay.title': '對局重播與複盤',
      'replay.open_file': '開啟重播檔案',
      'replay.download_id': '按 ID 下載重播',
      'replay.download_placeholder': '輸入 16 位對局重播 ID...',
      'replay.download_btn': '拉取入庫',
      'replay.library': '重播庫',
      'replay.summary': '對局概覽',
      'replay.timeline': '事件時間軸',
      'replay.relics': '籌碼流轉',
      'replay.frames': '影格檢查',
      'replay.ai_summary': 'AI 複盤摘要',
      'replay.copy_ai': '複製 AI 提示詞',
      'replay.filter_all': '全部玩家',
      'replay.stat_hero': '角色',
      'replay.stat_hp': '生命',
      'replay.stat_atk_def': '攻/防',
      'replay.stat_coins': '星幣',
      'replay.stat_damage': '造成傷害',
      'replay.stat_taken': '承受傷害',
      'replay.stat_heal': '治療量',
      'replay.stat_move': '移動點數',
      'replay.stat_cards': '出牌數',
      'replay.stat_skills': '技能數',
      'replay.stat_relics': '籌碼數',
      'replay.round': '回合',
      'replay.col_time': '時間',
      'replay.col_map': '地圖',
      'replay.col_winner': '獲勝者',
      'replay.col_rounds': '總回合',
      'replay.col_actions': '操作',
      'replay.back_to_lobby': '‹ 返回大廳',
      'replay.no_file_loaded': '尚未開啟重播檔案',
      'replay.export_json': '匯出 JSON',
      'replay.tab_summary': '對局摘要',
      'replay.tab_players': '玩家表現',
      'replay.tab_performance': '表現分析',
      'replay.tab_timeline': '事件時間軸',
      'replay.tab_relics': '籌碼紀錄',
      'replay.tab_shops': '商店紀錄',
      'replay.tab_technical': '專業資訊',
      'replay.tech_stats': '數據統計',
      'replay.tech_protocol': '原始協議影格',
      'replay.select_file_title': '選擇重播檔案',
      'replay.select_file_desc': '手動開啟任意重播檔案，本地解析玩家、回合、動作、籌碼流轉與底層協議影格。',
      'replay.select_file_btn': '＋ 選擇重播檔案',
      'replay.offline_note': '所有數據均在本地安全離線處理',
      'replay.search_placeholder': '搜尋重播 ID / 地圖 / 玩家…',
      'replay.filter_all_sources': '全部來源',
      'replay.filter_game_only': '僅遊戲內',
      'replay.filter_library_only': '僅重播庫',
      'replay.sort_time': '按對局時間',
      'replay.sort_size': '按檔案大小',
      'replay.sort_id': '按重播 ID',
      'replay.game_slots': '遊戲內席位',
      'replay.open_library_folder': '開啟庫目錄',
      'replay.badge_game': '遊戲內',
      'replay.badge_library': '重播庫',
      'replay.badge_unparseable': '無法解析',
      'replay.action_view': '查看',
      'replay.action_archive': '封存',
      'replay.action_restore': '放回遊戲',
      'replay.action_upgrade': '升級',
      'replay.upgrade_tooltip': '升級到此工具支援的 {version}（原檔案會備份）',
      'replay.upgrade_current': '已是目前支援版本',
      'replay.upgrade_newer': '版本高於此工具支援的 {version}',
      'replay.upgrade_unknown': '檔案裡沒有版本標記',
      'replay.summary_map': '本局地圖',
      'replay.summary_result': '對局結果',
      'replay.summary_awards': '結算獎勵',
      'replay.summary_duration': '時長',
      'replay.summary_progress': '關卡進度',
      'replay.summary_deaths': '死亡計數',
      'replay.summary_players': '參戰玩家',
      'replay.th_round': '回合',
      'replay.th_player': '玩家',
      'replay.th_hero': '角色',
      'replay.th_source': '來源',
      'replay.th_action': '操作',
      'replay.th_relic_name': '籌碼名稱',
      'replay.th_quality': '品質',
      'replay.th_rerolls': '刷新紀錄',
      'replay.th_options': '候選選項',
      'replay.th_shop_type': '商店類型',
      'replay.th_price': '卡牌單價',
      'replay.th_shop_cards': '候選卡牌',
      'replay.th_purchase_result': '購買結果',

      // Mods & Speedhack
      'mods.title': '模組與載入器管理',
      'mods.loader_title': 'CesiumLoader 載入器',
      'mods.installed_ver': '已安裝版本',
      'mods.builtin_ver': '隨本程式內建',
      'mods.latest_ver': 'GitHub 最新版',
      'mods.install_btn': '安裝載入器',
      'mods.update_btn': '更新載入器',
      'mods.uninstall_btn': '解除安裝載入器',
      'mods.check_update': '檢查更新',
      'mods.loader_settings': '載入器設定',
      'mods.speed_title': '遊戲變速',
      'mods.speed_enabled': '開啟變速',
      'mods.speed_base': '基礎倍速',
      'mods.speed_hotkey': '允許 Mod 快捷鍵變速',
      'mods.steam_bypass': 'Steam 用戶端繞過',
      'mods.installed_mods': '已安裝模組',
      'mods.import_mod': '匯入 Mod (DLL/ZIP)',
      'mods.tag_builtin': '內建',
      'mods.tag_can_operate': '可操作遊戲',
      'mods.open_mods_folder': '開啟 Mods 資料夾',
      'mods.open_logs_folder': '開啟日誌資料夾',
      'mods.current_game': '目前遊戲',
      'mods.manage_game_dirs': '管理遊戲目錄…',
      'mods.import_package': '匯入發布包',

      // Skins
      'skins.title': '角色與卡面造型工坊',
      'skins.schemes': '多角色造型方案',
      'skins.card_skins': '卡面替換',
      'skins.dynamic_cards': '動態卡面 (GIF/影片)',
      'skins.char_skins': '角色全身像替換',
      'skins.resource_mods': '資源模組',
      'resource_mods.import': '＋ 匯入資源模組 (.zip)',
      'resource_mods.apply': '套用',
      'resource_mods.restore': '還原原版',
      'resource_mods.open_folder': '開啟目錄',
      'resource_mods.empty_title': '還沒有資源模組',
      'resource_mods.empty_desc': '匯入 RML 格式的 ZIP（內含 .bundle 檔案），安裝後由 Toys 改寫 Addressables catalog 生效；重新啟動遊戲後可見。',
      'resource_mods.status_version': '資源版本 {v}',
      'resource_mods.status_applied': '已套用 {n} 處重定向',
      'resource_mods.status_not_applied': '尚未套用',
      'resource_mods.status_pending': '有變更待套用',
      'resource_mods.status_running': '遊戲執行中，修改前請先結束',
      'resource_mods.bundles': '{n} 個項目',
      'resource_mods.matched': '{n} 可替換',
      'resource_mods.unmatched_summary': '{n} 個 bundle 未匹配（可能是舊版本資源）',
      'resource_mods.enabled': '已啟用',
      'resource_mods.disabled': '已停用',
      'resource_mods.toggle_on': '啟用',
      'resource_mods.toggle_off': '停用',
      'resource_mods.delete': '刪除',
      'resource_mods.delete_title': '刪除資源模組',
      'resource_mods.delete_body': '確定刪除「{name}」？安裝的檔案會被移除；若正在生效，Toys 會先依剩餘模組重新產生 catalog。',
      'skins.new_scheme': '新增方案',
      'skins.import_zip': '匯入方案 ZIP',
      'skins.export_zip': '匯出方案 ZIP',
      'skins.apply_scheme': '套用方案到遊戲',
      'skins.restore_default': '還原官方預設',
      'skins.import_legacy': '舊版 Bundle 匯入',
      'skins.crop_title': '卡面/貼圖裁切',
      'skins.open_folder': '開啟造型目錄',
      'skins.download_default': '下載預設包',
      'skins.restore_builtin': '還原內建預設包',
      'skins.my_schemes': '我的方案',
      'skins.packages_library': '造型包素材庫',

      // Wiki
      'wiki.open_external': '瀏覽器開啟',

      // Settings
      'settings.title': '系統設定與偏好',
      'settings.group_general': '一般偏好',
      'settings.language': '介面與配表語言',
      'settings.lang_auto': '跟隨系統語言 (Auto)',
      'settings.motion': '介面平滑過渡動效',
      'settings.motion_desc': '開啟後在頁面切換與彈窗展開時使用平滑動畫',
      'settings.group_library': '重播庫託管',
      'settings.library_root': '重播庫儲存目錄',
      'settings.auto_maintain': '自動封存託管',
      'settings.auto_maintain_desc': '自動將遊戲產生的舊重播移入本地庫，防止被遊戲清除',
      'settings.keep_count': '遊戲目錄保留局数',
      'settings.maintain_now': '立即執行封存清理',
      'settings.group_about': '關於 AstralParty.Toys',
      'settings.about_desc': '吉星派對 Windows 本地離線工具箱，整合複盤、模組、造型與變速管理。',
      'settings.github_repo': 'GitHub 開源首頁',
      'settings.speedhack_repo': 'speedhack-rs 變速專案',
      'settings.group_game_dirs': '遊戲目錄（多個位置）',
      'settings.scan_system': '掃描系統',
      'settings.browse_add': '瀏覽新增…',

      // Utilities
      'utilities.title': '實用工具箱',
      'utilities.clean_cache': '清理遊戲暫存快取',
      'utilities.clean_cache_btn': '一鍵清理',
      'utilities.ffmpeg_title': 'FFmpeg 動態卡面轉換工具',
      'utilities.ffmpeg_status': 'FFmpeg 狀態',
      'utilities.ffmpeg_download': '下載 / 修復 FFmpeg'
    }
  };

  // Source-text catalog (gettext style): keys are the Simplified Chinese strings
  // that ship in the HTML and JS. It lets the UI translate text that is written
  // directly in markup or assembled by page modules without hand-annotating
  // hundreds of elements. `js/i18n.source.js` provides the data.
  const SOURCE_TEXT = window.I18N_SOURCE || {};
  const SOURCE_PATTERNS = window.I18N_PATTERNS || {};
  const SOURCE_TEMPLATES = window.I18N_TEMPLATES || {};
  const SKIP_TAGS = new Set(['SCRIPT', 'STYLE', 'TEXTAREA', 'INPUT', 'CODE', 'PRE']);
  const SKIP_SELECTOR = '[data-i18n],[data-i18n-html],[data-i18n-title],[data-i18n-placeholder],[data-i18n-skip]';

  function normalizeSource(text) {
    return String(text ?? '').replace(/\u00a0/g, ' ').replace(/\s+/g, ' ').trim();
  }

  const I18n = {
    lang: 'zh-Hans',
    _textSources: new Map(),
    _attrSources: new Map(),
    _observer: null,
    _switching: false,
    _titleSource: null,
    _templateLang: null,
    _templateList: [],

    init() {
      const stored = window.localStorage?.getItem('appLanguage');
      if (stored && LANGUAGES[stored]) {
        this.lang = stored;
      } else {
        this.lang = this.detectSystemLanguage();
      }
      this.updateHtmlLang();
      this.localizeDom(document.body);
      this.observe();
    },

    detectSystemLanguage() {
      const navLang = (navigator.language || navigator.userLanguage || '').toLowerCase();
      if (navLang.startsWith('ja')) return 'ja';
      if (navLang.includes('tw') || navLang.includes('hk') || navLang.includes('mo') || navLang.includes('hant')) {
        return 'zh-Hant';
      }
      if (navLang.startsWith('zh')) return 'zh-Hans';
      if (navLang.startsWith('en')) return 'en';
      return 'zh-Hans';
    },

    updateHtmlLang() {
      document.documentElement.lang = LANGUAGES[this.lang]?.locale || 'zh-CN';
    },

    currentLocale() {
      return LANGUAGES[this.lang]?.locale || 'zh-CN';
    },

    setLanguage(lang, persist = true) {
      if (!LANGUAGES[lang] || lang === this.lang) return;
      this._switching = true;
      this.restoreSources();
      this.lang = lang;
      this.updateHtmlLang();
      if (persist) {
        window.localStorage?.setItem('appLanguage', lang);
      }
      this.applyDOM();
      this._switching = false;
      this.localizeDom(document.body);
      window.dispatchEvent(new CustomEvent('appLanguageChanged', { detail: { lang } }));
    },

    /** Translate a plain source string (Chinese) into the current language. */
    translateSource(value) {
      const text = String(value ?? '');
      const source = normalizeSource(text);
      if (!source || this.lang === 'zh-Hans') return text;
      const dict = SOURCE_TEXT[this.lang];
      if (dict && Object.prototype.hasOwnProperty.call(dict, source)) {
        const translated = dict[source];
        if (translated) return translated;
      }
      const templates = this._compiledTemplates();
      if (templates.length) {
        for (const item of templates) {
          const match = item.re.exec(source);
          if (!match) continue;
          return item.t.replace(/\{(\d+)\}/g, (_, n) => this._translateExact(match[Number(n) + 1] ?? ''));
        }
      }
      const patterns = SOURCE_PATTERNS[this.lang];
      if (Array.isArray(patterns)) {
        for (const entry of patterns) {
          const re = entry?.[0];
          if (!(re instanceof RegExp) || !re.test(source)) continue;
          return source.replace(re, entry[1]);
        }
      }
      return text;
    },

    _translateExact(value) {
      const source = normalizeSource(value);
      if (!source || this.lang === 'zh-Hans') return value;
      const dict = SOURCE_TEXT[this.lang];
      const translated = dict && Object.prototype.hasOwnProperty.call(dict, source) ? dict[source] : '';
      return translated || value;
    },

    _compiledTemplates() {
      if (this._templateLang === this.lang) return this._templateList;
      this._templateLang = this.lang;
      const list = SOURCE_TEMPLATES[this.lang];
      if (!Array.isArray(list)) {
        this._templateList = [];
        return this._templateList;
      }
      const escape = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      this._templateList = list
        .filter(item => item && typeof item.s === 'string' && typeof item.t === 'string')
        .map(item => {
          const parts = item.s.split(/\{\d+\}/);
          const re = new RegExp('^' + parts.map(escape).join('([\\s\\S]+?)') + '$');
          const literalLength = parts.join('').length;
          return { re, t: item.t, literalLength };
        })
        .sort((a, b) => b.literalLength - a.literalLength);
      return this._templateList;
    },

    _translateValue(value) {
      const match = String(value ?? '').match(/^(\s*)([\s\S]*?)(\s*)$/);
      if (!match) return value;
      const translated = this.translateSource(match[2]);
      return translated === match[2] ? value : match[1] + translated + match[3];
    },

    _hasHan(value) {
      return /[\u3400-\u9fff]/.test(String(value ?? ''));
    },

    localizeTextNode(node) {
      if (this.lang === 'zh-Hans' || !node || node.nodeType !== Node.TEXT_NODE || this._switching) return;
      const parent = node.parentElement;
      if (!parent || SKIP_TAGS.has(parent.tagName)) return;
      if (parent.closest(SKIP_SELECTOR)) return;
      const current = node.nodeValue;
      const cached = this._textSources.get(node);
      const source = cached ?? current;
      if (!this._hasHan(source)) {
        if (cached) this._textSources.delete(node);
        return;
      }
      const desired = this._translateValue(source);
      if (desired !== source) this._textSources.set(node, source);
      else if (cached) this._textSources.delete(node);
      const output = desired === source ? desired : this._padEnglishBoundary(node, desired);
      if (node.nodeValue !== output) node.nodeValue = output;
    },

    _isInlineElement(el) {
      return !!el && el.nodeType === Node.ELEMENT_NODE &&
        ['B', 'STRONG', 'CODE', 'SPAN', 'A', 'EM', 'I', 'SMALL', 'LABEL'].includes(el.tagName);
    },

    _boundaryChar(node, direction) {
      const sibling = direction < 0 ? node.previousSibling : node.nextSibling;
      if (!sibling) return '';
      if (sibling.nodeType === Node.TEXT_NODE) {
        const text = this._translateValue(sibling.nodeValue);
        return direction < 0 ? text.trimEnd().slice(-1) : text.trimStart().slice(0, 1);
      }
      if (this._isInlineElement(sibling)) {
        const text = this._translateValue(sibling.textContent || '');
        return direction < 0 ? text.trimEnd().slice(-1) : text.trimStart().slice(0, 1);
      }
      return '';
    },

    /** English needs a space where Chinese markup butts text against an inline <b>/<code>. */
    _padEnglishBoundary(node, text) {
      if (this.lang !== 'en' || !text) return text;
      let out = text;
      if (!/^\s/.test(out) && /^[A-Za-z0-9]/.test(out) && /[A-Za-z0-9]$/.test(this._boundaryChar(node, -1))) {
        out = ' ' + out;
      }
      if (!/\s$/.test(out) && /[A-Za-z0-9]$/.test(out) && /^[A-Za-z0-9]/.test(this._boundaryChar(node, 1))) {
        out = out + ' ';
      }
      return out;
    },

    localizeAttributes(el) {
      if (this.lang === 'zh-Hans' || !el || el.nodeType !== Node.ELEMENT_NODE || this._switching) return;
      if (el.closest(SKIP_SELECTOR)) return;
      const stored = this._attrSources.get(el) || {};
      let changed = false;
      for (const attr of ['title', 'placeholder', 'aria-label', 'alt']) {
        const current = el.getAttribute(attr);
        if (current == null) continue;
        const source = stored[attr] ?? current;
        if (!this._hasHan(source)) {
          if (stored[attr]) { delete stored[attr]; changed = true; }
          continue;
        }
        const desired = this._translateValue(source);
        if (desired !== source) stored[attr] = source;
        else if (stored[attr]) delete stored[attr];
        if (current !== desired) el.setAttribute(attr, desired);
      }
      if (Object.keys(stored).length) this._attrSources.set(el, stored);
      else if (changed) this._attrSources.delete(el);
    },

    /** Translate text nodes and UI attributes below `root` (document.body by default). */
    localizeDom(root = document.body) {
      if (!root || this._switching || this.lang === 'zh-Hans') return;
      if (root.nodeType === Node.TEXT_NODE) {
        this.localizeTextNode(root);
        return;
      }
      if (root.nodeType !== Node.ELEMENT_NODE && root.nodeType !== Node.DOCUMENT_NODE) return;

      if (root.nodeType === Node.ELEMENT_NODE) this.localizeAttributes(root);
      const doc = root.ownerDocument || document;
      const walker = doc.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
        acceptNode: node => {
          const parent = node.parentElement;
          if (!parent || SKIP_TAGS.has(parent.tagName) || parent.closest(SKIP_SELECTOR)) {
            return NodeFilter.FILTER_REJECT;
          }
          return this._hasHan(node.nodeValue) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
        }
      });
      const nodes = [];
      while (walker.nextNode()) nodes.push(walker.currentNode);
      nodes.forEach(node => this.localizeTextNode(node));

      root.querySelectorAll?.('[title],[placeholder],[aria-label],[alt]').forEach(el => this.localizeAttributes(el));
      if (root === document.body) this.localizeDocumentTitle();
    },

    localizeDocumentTitle() {
      const current = document.title;
      const source = this._titleSource ?? current;
      const translated = this._translateValue(source);
      if (translated !== source) this._titleSource = source;
      if (translated !== current) document.title = translated;
    },

    restoreSources() {
      if (this._titleSource) document.title = this._titleSource;
      for (const [node, source] of this._textSources) {
        if (!node.isConnected) { this._textSources.delete(node); continue; }
        if (node.nodeValue !== source) node.nodeValue = source;
      }
      for (const [el, attrs] of this._attrSources) {
        if (!el.isConnected) { this._attrSources.delete(el); continue; }
        for (const [attr, source] of Object.entries(attrs)) {
          if (el.getAttribute(attr) !== source) el.setAttribute(attr, source);
        }
      }
    },

    observe() {
      if (this._observer || !window.MutationObserver || !document.body) return;
      this._observer = new MutationObserver(records => {
        if (this._switching) return;
        const nodes = new Set();
        for (const record of records) {
          if (record.type === 'childList') {
            record.addedNodes.forEach(node => {
              if (node.nodeType === Node.TEXT_NODE || node.nodeType === Node.ELEMENT_NODE) nodes.add(node);
            });
          } else if (record.type === 'characterData') {
            nodes.add(record.target);
          } else if (record.type === 'attributes') {
            nodes.add(record.target);
          }
        }
        nodes.forEach(node => {
          if (node.nodeType === Node.TEXT_NODE) this.localizeTextNode(node);
          else if (node.nodeType === Node.ELEMENT_NODE) {
            this.localizeAttributes(node);
            this.localizeDom(node);
          }
        });
      });
      this._observer.observe(document.body, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: ['title', 'placeholder', 'aria-label', 'alt']
      });
    },

    t(key, params) {
      const dict = TRANSLATIONS[this.lang] || TRANSLATIONS['zh-Hans'];
      let text = dict[key] ?? TRANSLATIONS['zh-Hans'][key] ?? key;
      if (params && typeof params === 'object') {
        Object.keys(params).forEach(p => {
          text = text.replace(new RegExp(`\\{${p}\\}`, 'g'), params[p]);
        });
      }
      return text;
    },

    applyDOM(root = document) {
      root.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const val = this.t(key);
        if (val !== undefined) el.textContent = val;
      });

      root.querySelectorAll('[data-i18n-html]').forEach(el => {
        const key = el.getAttribute('data-i18n-html');
        const val = this.t(key);
        if (val !== undefined && val !== key) el.innerHTML = val;
      });

      root.querySelectorAll('[data-i18n-title]').forEach(el => {
        const key = el.getAttribute('data-i18n-title');
        const val = this.t(key);
        if (val !== undefined) el.title = val;
      });

      root.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        const val = this.t(key);
        if (val !== undefined) el.placeholder = val;
      });
    },

    getSupportedLanguages() {
      return Object.keys(LANGUAGES).map(code => ({
        code,
        name: LANGUAGES[code].name,
        locale: LANGUAGES[code].locale
      }));
    }
  };

  I18n.init();
  window.i18n = I18n;
  window.t = (key, params) => I18n.t(key, params);
})();
