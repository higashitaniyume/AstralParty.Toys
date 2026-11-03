// settings.js - Settings & Preferences Page Logic
(function () {
  const SettingsModule = {
    init() {
      if (this.initialized) return;
      this.initialized = true;

      this.bindLibrary();
      this.bindUpdates();
      window.GameLibModule?.init?.();

      const langSelect = $('langSelect');
      if (langSelect) {
        langSelect.value = window.i18n?.lang || 'zh-Hans';
        langSelect.addEventListener('change', e => {
          const selected = e.target.value;
          window.i18n?.setLanguage(selected, true);
          post({ type: 'setLanguage', language: selected });
          toast(window.t ? window.t('common.saved') : '已切换语言');
        });
      }

      const animToggle = $('animToggle');
      if (animToggle) {
        const motionEnabled = window.localStorage.getItem('motionEnabled') !== 'false';
        animToggle.checked = motionEnabled;
        document.body.classList.toggle('reduce-motion', !motionEnabled);
        animToggle.addEventListener('change', e => {
          document.body.classList.toggle('reduce-motion', !e.target.checked);
          window.localStorage.setItem('motionEnabled', String(e.target.checked));
          toast(`界面动态效果已${e.target.checked ? '开启' : '关闭'}`);
        });
      }

      // 「关于」里的版本/提交/运行环境由 version.js 统一填（同一份数据源），这里只补一次兜底渲染
      window.VersionModule?.renderAbout?.();

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
      $('workshopUrlInput')?.addEventListener('change', () => this.pushLibrarySettings());
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
        keepInGame: keep,
        workshopUrl: ($('workshopUrlInput')?.value || '').trim() || null,
        autoCheckUpdates: !!$('autoCheckUpdatesToggle')?.checked,
        autoDownloadUpdates: !!$('autoDownloadUpdatesToggle')?.checked,
        autoInstallUpdates: !!$('autoInstallUpdatesToggle')?.checked,
        githubMirrorType: $('githubMirrorSelect')?.value || 'gh-proxy',
        customGitHubMirrorUrl: ($('customMirrorInput')?.value || '').trim()
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

      const workshopInput = $('workshopUrlInput');
      if (workshopInput) {
        const fallback = settings.defaultWorkshopUrl || 'https://forge.hiynet.com';
        workshopInput.value = settings.workshopUrl || fallback;
        workshopInput.placeholder = fallback;
      }

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

      if (settings.language) {
        const langSelect = $('langSelect');
        if (langSelect && langSelect.value !== settings.language) {
          langSelect.value = settings.language;
        }
      }

      const autoCheck = $('autoCheckUpdatesToggle');
      if (autoCheck) autoCheck.checked = settings.autoCheckUpdates !== false;

      const autoDown = $('autoDownloadUpdatesToggle');
      if (autoDown) autoDown.checked = !!settings.autoDownloadUpdates;

      const autoInst = $('autoInstallUpdatesToggle');
      if (autoInst) autoInst.checked = !!settings.autoInstallUpdates;

      const mirrorSelect = $('githubMirrorSelect');
      if (mirrorSelect) {
        mirrorSelect.value = settings.githubMirrorType || 'gh-proxy';
        $('customMirrorRow')?.classList.toggle('hidden', mirrorSelect.value !== 'custom');
      }

      const customInput = $('customMirrorInput');
      if (customInput) customInput.value = settings.customGitHubMirrorUrl || '';
    },

    bindUpdates() {
      $('checkUpdateManualBtn')?.addEventListener('click', () => {
        post({ type: 'checkAppUpdate' });
      });

      $('applyUpdateNowBtn')?.addEventListener('click', () => {
        post({ type: 'applyAppUpdate' });
      });

      $('githubMirrorSelect')?.addEventListener('change', e => {
        $('customMirrorRow')?.classList.toggle('hidden', e.target.value !== 'custom');
        this.pushLibrarySettings();
      });

      $('customMirrorInput')?.addEventListener('change', () => this.pushLibrarySettings());
      $('autoCheckUpdatesToggle')?.addEventListener('change', () => this.pushLibrarySettings());
      $('autoDownloadUpdatesToggle')?.addEventListener('change', () => this.pushLibrarySettings());
      $('autoInstallUpdatesToggle')?.addEventListener('change', () => this.pushLibrarySettings());

      if (AppState.appUpdateState) {
        this.renderUpdateState(AppState.appUpdateState);
      } else {
        post({ type: 'getAppUpdateState' });
      }
    },

    renderUpdateState(payload) {
      if (!payload) return;

      const verNode = $('settingsCurrentVersion');
      if (verNode) {
        let text = payload.currentVersion ? `v${payload.currentVersion}` : (AppState.appInfo?.version ? `v${AppState.appInfo.version}` : '未知');
        if (payload.runtimeKind) {
          text += ` · ${payload.runtimeKind}`;
        } else if (AppState.appInfo?.runtime) {
          text += ` · ${AppState.appInfo.runtime}`;
        }
        if (payload.isInstaller) text += ' (安装包)';
        verNode.textContent = text;
      }

      const msgNode = $('settingsUpdateMessage');
      const progressRow = $('settingsUpdateProgressRow');
      const progressBar = $('settingsUpdateProgressBar');
      const progressPercent = $('settingsUpdateProgressPercent');
      const applyRow = $('settingsUpdateApplyRow');
      const checkBtn = $('checkUpdateManualBtn');

      const status = (payload.status || 'idle').toLowerCase();

      if (status === 'checking') {
        if (msgNode) msgNode.textContent = '正在检查更新…';
        if (checkBtn) checkBtn.disabled = true;
        progressRow?.classList.add('hidden');
        applyRow?.classList.add('hidden');
      } else if (status === 'uptodate') {
        if (msgNode) msgNode.textContent = '当前已是最新版本。';
        if (checkBtn) checkBtn.disabled = false;
        progressRow?.classList.add('hidden');
        applyRow?.classList.add('hidden');
      } else if (status === 'updateavailable') {
        if (msgNode) msgNode.textContent = `发现新版本 v${payload.latestVersion}。`;
        if (checkBtn) checkBtn.disabled = false;
        progressRow?.classList.add('hidden');
        applyRow?.classList.add('hidden');
      } else if (status === 'downloading') {
        if (msgNode) msgNode.textContent = `正在下载新版本 v${payload.latestVersion} 安装包…`;
        if (checkBtn) checkBtn.disabled = true;
        progressRow?.classList.remove('hidden');
        applyRow?.classList.add('hidden');
        const percent = payload.percent ?? 0;
        if (progressBar) progressBar.style.width = `${percent}%`;
        if (progressPercent) progressPercent.textContent = `${percent}%`;
      } else if (status === 'readytoinstall') {
        if (msgNode) msgNode.textContent = `新版本 v${payload.latestVersion} 安装包已就绪！`;
        if (checkBtn) checkBtn.disabled = false;
        progressRow?.classList.add('hidden');
        applyRow?.classList.remove('hidden');
      } else if (status === 'error') {
        if (msgNode) msgNode.textContent = `更新出错：${payload.error || '网络连接失败'}`;
        if (checkBtn) checkBtn.disabled = false;
        progressRow?.classList.add('hidden');
        applyRow?.classList.add('hidden');
      }
    }
  };

  window.SettingsModule = SettingsModule;
})();
