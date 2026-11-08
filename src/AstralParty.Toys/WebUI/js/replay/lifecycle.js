// replay/lifecycle.js - lifecycle, navigation and tabs
(function () {
  'use strict';
  const { fmtBytes, QUALITY_TONE, qualityTone } = window.ReplayHelpers;
  Object.assign(window.ReplayModule, {
    init() {
      this.bindEvents();
    },
    bindEvents() {
      // Sub-nav tab switcher（只绑外层页签：内层「数据统计/原始协议帧」也用 .subnav-tab，它们各有处理器）
      document.querySelectorAll('#replaySubnav .subnav-tab').forEach(tab => {
        tab.addEventListener('click', () => {
          if (!tab.dataset.tab) return;
          this.switchReplayTab(tab.dataset.tab);
        });
      });

      // Actions in Tools Header
      $('toolOpenBtn')?.addEventListener('click', () => post({ type: 'openReplay' }));
      $('toolExportBtn')?.addEventListener('click', () => post({ type: 'export' }));
      // 列表页返回大厅；详情页返回回放列表
      $('toolsBackHomeBtn')?.addEventListener('click', () => {
        if (this.detailOpen) this.showLibrary();
        else showPage('home');
      });

      // Empty state buttons
      $('refreshLibraryBtn')?.addEventListener('click', () => post({ type: 'refreshReplays' }));

      // 回放库：下载（按 ID）/ 导入 / 打开目录 / 筛选 / 横幅
      $('libraryDownloadBtn')?.addEventListener('click', () => this.openDownloadDialog());
      $('libraryImportBtn')?.addEventListener('click', () => post({ type: 'libraryImport' }));
      $('libraryOpenFolderBtn')?.addEventListener('click', () => post({ type: 'libraryOpenFolder' }));
      $('librarySearchInput')?.addEventListener('input', () => this.renderLibraryRows());
      $('libraryFilterSelect')?.addEventListener('change', () => this.renderLibraryRows());
      $('librarySortSelect')?.addEventListener('change', () => this.renderLibraryRows());
      $('recentReplayList')?.addEventListener('click', event => this.onLibraryClick(event));
      $('libraryBannerArchiveBtn')?.addEventListener('click', () => {
        const data = this.libraryData || {};
        const overflow = Math.max(0, (data.gameCount || 0) - (data.capacity || 10));
        post({ type: 'libraryArchiveOldest', count: overflow > 0 ? overflow : 3 });
      });
      $('libraryBannerAutoBtn')?.addEventListener('click', () => {
        const data = this.libraryData || {};
        // 保留局数用用户在设置页配的值；拿 capacity（游戏内上限，恒为 10）去覆盖会静默改掉用户的设置
        const keepInGame = data.keepInGame || data.capacity || 10;
        post({
          type: 'librarySaveSettings',
          libraryRoot: data.libraryRoot || null,
          autoMaintain: true,
          keepInGame
        });
        toast(`已开启自动托管：以后每次打开本工具都会自动把超出席位的旧回放归档进库（游戏内保留最近 ${keepInGame} 局）。`);
      });
      $('libraryBannerDismissBtn')?.addEventListener('click', () => {
        const data = this.libraryData || {};
        window.localStorage.setItem('libraryBannerSignature', `${data.gameCount || 0}/${data.capacity || 10}`);
        $('libraryBanner')?.classList.add('hidden');
      });

      // Technical sub tabs
      $('techTabStatistics')?.addEventListener('click', () => this.switchTechPane('stats'));
      $('techTabProtocol')?.addEventListener('click', () => this.switchTechPane('proto'));
    },
    onShowTools() {
      // 详情页（有已解析回放）或回放列表（按 ID 下载 / 从缓存打开）
      if (AppState.replayReport) this.setDetailMode(true);
      else this.showLibrary();
    },

    /** 切换列表 / 详情布局，并同步左上角返回按钮的文案与行为。 */
    setDetailMode(inDetail) {
      this.detailOpen = !!inDetail;
      $('replayEmptyBox')?.classList.toggle('hidden', this.detailOpen);
      $('replayWorkspaceBox')?.classList.toggle('hidden', !this.detailOpen);
      $('replaySubnav')?.classList.toggle('hidden', !this.detailOpen);
      if ($('toolExportBtn')) $('toolExportBtn').disabled = !this.detailOpen;

      const back = $('toolsBackHomeBtn');
      if (back) {
        const key = this.detailOpen ? 'replay.back_to_list' : 'replay.back_to_lobby';
        back.dataset.i18n = key;
        if (typeof window.t === 'function') back.textContent = window.t(key);
      }

      const meta = $('toolFileMeta');
      if (meta && this.detailOpen) {
        meta.removeAttribute('data-i18n');
      } else if (meta) {
        meta.dataset.i18n = 'replay.list_hint';
        if (typeof window.t === 'function') meta.textContent = window.t('replay.list_hint');
      }
    },
    showLibrary() {
      this.setDetailMode(false);
      // 回到列表即丢弃上一局详情，避免从大厅再次进入时又跳回旧报告
      AppState.replayReport = null;
      AppState.selectedFrame = -1;
      post({ type: 'refreshReplays' });
    },
    switchReplayTab(tabName) {
      AppState.activeReplayTab = tabName;
      document.querySelectorAll('#replaySubnav .subnav-tab').forEach(tab => {
        tab.classList.toggle('active', tab.dataset.tab === tabName);
      });

      const current = document.querySelector('.replay-view:not(.hidden)');
      const next = $(`${tabName}View`);
      if (!next || current === next) return;
      current?.classList.add('replay-view-leaving');
      window.setTimeout(() => {
        document.querySelectorAll('.replay-view').forEach(view => {
          view.classList.add('hidden');
          view.classList.remove('replay-view-leaving');
        });
        next.classList.remove('hidden');
        next.classList.add('replay-view-entering');
        requestAnimationFrame(() => next.classList.remove('replay-view-entering'));
      }, document.body.classList.contains('reduce-motion') ? 0 : 120);
    },
    switchTechPane(paneName) {
      const isStats = paneName === 'stats';
      $('techStatsPane')?.classList.toggle('hidden', !isStats);
      $('techProtocolPane')?.classList.toggle('hidden', isStats);
      $('techTabStatistics')?.classList.toggle('active', isStats);
      $('techTabProtocol')?.classList.toggle('active', !isStats);
    },
  });
})();
