// settings.js - Settings & Preferences Page Logic
(function () {
  const SettingsModule = {
    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindLibrary();

      const animToggle = $('animToggle');
      if (!animToggle) return;

      const appVersion = AppState.homeData?.appVersion;
      if ($('aboutVersion')) $('aboutVersion').textContent = appVersion ? `v${appVersion}` : '未知';

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

      const keepInput = $('libraryKeepInput');
      if (keepInput) {
        const capacity = settings.capacity || 10;
        keepInput.max = String(capacity);
        keepInput.value = String(settings.keepInGame || capacity);
      }
    }
  };

  window.SettingsModule = SettingsModule;
})();
