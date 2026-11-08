// replay/state.js - ReplayModule state
(function () {
  'use strict';
  const ReplayModule = window.ReplayModule = {
    libraryData: null,
    // 当前是否在回放详情页：决定左上角按钮是「返回回放列表」还是「返回大厅」
    detailOpen: false,
    // 归档 / 放回 / 删除正在进行中（文件 IO + 之后整表重扫）：挡住重复点击
    busyButton: null,
    // 「按 ID 下载」弹窗正在下载的回放 ID（进度消息靠它认领自己的进度）
    downloadId: null,

  };
})();
