// mods/state.js - ModsModule state
(function () {
  'use strict';
  const ModsModule = window.ModsModule = {
    modStatus: null,
    searchQuery: '',
    // 联网检查更新的结果（跨页面切换保留；state: idle | checking | done）
    updateInfo: { state: 'idle', latest: '', error: '' },
    builtInMods: [],
    // 正在进行的耗时操作（安装/卸载/检查/更新）：按钮转圈 + 锁住，避免重复点击
    busy: null,
    busyTimer: null,

    // ---------- 生命周期 ----------

  };
})();
