// settings.js - Settings & Preferences Page Logic
(function () {
  const SettingsModule = {
    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindLibrary();
      window.GameLibModule?.init?.();

      const animToggle = $('animToggle');
      if (!animToggle) return;

      // 「关于」里的版本/提交/运行环境由 version.js 统一填（同一份数据源），这里只补一次兜底渲染
      window.VersionModule?.renderAbout?.();

      const motionEnabled = window.localStorage.getItem('motionEnabled') !== 'false';
      animToggle.checked = motionEnabled;
      document.body.classList.toggle('reduce-motion', !motionEnabled);
      animToggle.addEventListener('change', e => {
        document.body.classList.toggle('reduce-motion', !e.target.checked);
        window.localStorage.setItem('motionEnabled', String(e.target.checked));
        toast(`界面动态效果已${e.target.checked ? '开启' : '关闭'}`);
      });

      $('aboutRepositoryBtn')?.addEventListener('click', () => {
        window.open('https://github.com/higashitaniyume/AstralParty.Toys', '_blank', 'noopener');
      });
      $('aboutSpeedhackBtn')?.addEventListener('click', () => {
        window.open('https://github.com/Hirtol/speedhack-rs', '_blank', 'noopener');
      });
    },

    // ---------- 回放库 ----------
    bindLibrary() {
      $('libraryRootBrowseBtn')?.addEventListener('click', () => post({ type: 'libraryBrowseRoot' }));
      $('libraryRootOpenBtn')?.addEventListener('click', () => {
        this.pushLibrarySettings();
        post({ type: 'libraryOpenFolder' });
      });
      // 手动输入的库目录也要落盘：以前只有点「浏览/打开」等按钮才保存，直接敲进去会被静默丢弃
      $('libraryRootInput')?.addEventListener('change', () => this.pushLibrarySettings());
      $('libraryAutoMaintainToggle')?.addEventListener('change', event => {
        this.pushLibrarySettings();
        toast(`自动托管已${event.target.checked ? '开启' : '关闭'}`);
      });
      $('libraryKeepInput')?.addEventListener('change', () => this.pushLibrarySettings());
      $('libraryMaintainNowBtn')?.addEventListener('click', () => {
        this.pushLibrarySettings();
        post({ type: 'libraryMaintain' });
      });

      if (AppState.librarySettings) this.applyLibrarySettings(AppState.librarySettings);
      else post({ type: 'libraryGetSettings' });
    },

    pushLibrarySettings() {
      const keepInput = $('libraryKeepInput');
      const parsed = parseInt(keepInput?.value, 10);
      const keep = Number.isFinite(parsed) ? Math.min(Math.max(parsed, 1), 10) : 10;
      if (keepInput) keepInput.value = String(keep);

      post({
        type: 'librarySaveSettings',
        libraryRoot: ($('libraryRootInput')?.value || '').trim() || null,
        autoMaintain: !!$('libraryAutoMaintainToggle')?.checked,
        keepInGame: keep
      });
    },

    applyLibrarySettings(settings) {
      if (!settings) return;
      const rootInput = $('libraryRootInput');
      if (rootInput) {
        rootInput.value = settings.libraryRoot || '';
        rootInput.placeholder = settings.defaultLibraryRoot
          ? `默认：${settings.defaultLibraryRoot}`
          : '默认：文档\\AstralPartyReplays';
      }
      const autoToggle = $('libraryAutoMaintainToggle');
      if (autoToggle) autoToggle.checked = !!settings.autoMaintain;

      const capacity = settings.capacity || 10;
      const keep = settings.keepInGame || capacity;
      const keepInput = $('libraryKeepInput');
      if (keepInput) {
        keepInput.max = String(capacity);
        keepInput.value = String(keep);
      }
      // 标题跟随实际保留局数，不再写死"10 局"（同一张卡片下面就能把它改成别的数）
      const autoLabel = $('libraryAutoMaintainLabel');
      if (autoLabel) autoLabel.textContent = `自动保持游戏内最近 ${keep} 局`;
    }
  };

  window.SettingsModule = SettingsModule;
})();
