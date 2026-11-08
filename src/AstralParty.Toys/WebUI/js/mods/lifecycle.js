// mods/lifecycle.js - lifecycle and event binding
(function () {
  'use strict';
  const { parseVersion, compareVersion, setVersionCell, fmtBytes } = window.ModsHelpers;
  Object.assign(window.ModsModule, {
    init() {
      this.bindEvents();
      window.GameLibModule?.init?.();
    },
    onShowMods() {
      // 每次进入模组页面重置旧信息，显示读取中状态
      const searchInput = $('modSearchInput');
      if (searchInput) searchInput.value = '';
      this.searchQuery = '';
      this.modStatus = null;

      const container = $('modListContainer');
      if (container) {
        const loadingText = window.i18n ? window.i18n.t('common.loading') : '正在读取模组信息…';
        container.innerHTML = `<p class="mods-empty"><span class="mi" data-icon="hourglass_empty"></span> ${esc(loadingText)}</p>`;
        window.Icons?.hydrate(container);
      }
      const sdkContainer = $('modSdkContainer');
      if (sdkContainer) {
        const loadingText = window.i18n ? window.i18n.t('common.loading') : '正在读取 SDK 信息…';
        sdkContainer.innerHTML = `<p class="mods-empty"><span class="mi" data-icon="hourglass_empty"></span> ${esc(loadingText)}</p>`;
        window.Icons?.hydrate(sdkContainer);
      }
      const count = $('modModsCount');
      if (count) count.textContent = '…';

      const badge = $('modStatusBadge');
      if (badge) {
        badge.textContent = window.i18n ? window.i18n.t('mods.detecting') || '检测中' : '检测中';
        badge.className = 'badge badge-notice';
      }
      const title = $('modStatusTitle');
      if (title) title.textContent = '正在检测安装状态…';
      const text = $('modStatusText');
      if (text) text.textContent = '请稍候';
      const verdictText = $('modVersionVerdictText');
      if (verdictText) verdictText.textContent = '正在读取版本信息…';
      const instVer = $('modInstalledVersion');
      if (instVer) instVer.textContent = '—';
      const instNote = $('modInstalledNote');
      if (instNote) instNote.textContent = '正在检测…';

      window.GameLibModule?.init?.();
      this.refreshModStatus();
      // 只同步变速栏状态(不弹设置窗口; 弹窗仅在点「⚙ 加载器设置」时出现)
      post({ type: 'modSyncSpeedhack' });
    },
    bindEvents() {
      $('modsBackHomeBtn')?.addEventListener('click', () => showPage('home'));

      // ---------- Mod 加载器 ----------
      $('modInstallBtn')?.addEventListener('click', () => {
        this.setBusy('modInstallBtn', '安装中…');
        post({
          type: 'modInstall',
          overwriteDll: $('modOverwriteCheck')?.checked === true,
          allowDowngrade: $('modDowngradeCheck')?.checked === true,
          includeSample: $('modIncludeSampleCheck')?.checked !== false
        });
      });
      $('modUninstallBtn')?.addEventListener('click', () => this.openUninstallConfirm());
      $('modDisableLoaderBtn')?.addEventListener('click', () => this.startRecovery(false));
      $('modRestoreLoaderBtn')?.addEventListener('click', () => this.startRecovery(true));
      $('modDetectDirBtn')?.addEventListener('click', () => post({ type: 'modDetect' }));
      $('modBrowseDirBtn')?.addEventListener('click', () => post({ type: 'modBrowse' }));
      $('modOpenModsBtn')?.addEventListener('click', () => post({ type: 'modOpenModsFolder' }));
      $('modOpenFolderTopBtn')?.addEventListener('click', () => post({ type: 'modOpenModsFolder' }));
      $('modOpenSdkBtn')?.addEventListener('click', () => post({ type: 'modOpenSdkFolder' }));
      $('modImportPackageBtn')?.addEventListener('click', () => this.openPackageImport());
      $('modRefreshBtn')?.addEventListener('click', () => this.onShowMods());
      $('modOpenLogsBtn')?.addEventListener('click', () => post({ type: 'modOpenLogsFolder' }));
      $('modImportBtn')?.addEventListener('click', () => post({ type: 'modPickImport' }));
      $('modImportTopBtn')?.addEventListener('click', () => post({ type: 'modPickImport' }));
      // 镜像源切换
      $('modLoaderMirrorSelect')?.addEventListener('change', e => {
        const val = e.target.value;
        $('modLoaderCustomMirrorInput')?.classList.toggle('hidden', val !== 'custom');
        this.saveMirrorSetting();
      });
      $('modLoaderCustomMirrorInput')?.addEventListener('change', () => {
        this.saveMirrorSetting();
      });

      $('modCheckUpdateBtn')?.addEventListener('click', () => {
        this.updateInfo = { ...(this.updateInfo || {}), state: 'checking', error: '' };
        this.setBusy('modCheckUpdateBtn', '检查中…');
        this.renderUpdateInfo();
        post({
          type: 'modCheckUpdate',
          mirrorType: $('modLoaderMirrorSelect')?.value,
          customMirrorUrl: ($('modLoaderCustomMirrorInput')?.value || '').trim()
        });
      });
      $('modDownloadUpdateBtn')?.addEventListener('click', () => {
        // 下载 + 安装可能十几秒：按钮上转圈并锁住，避免重复点击（宿主完成/失败时才解除）
        this.setBusy('modDownloadUpdateBtn', '更新中…');
        post({
          type: 'modDownloadUpdate',
          overwriteDll: $('modUpdateOverwriteCheck')?.checked === true,
          allowDowngrade: $('modUpdateDowngradeCheck')?.checked === true,
          mirrorType: $('modLoaderMirrorSelect')?.value,
          customMirrorUrl: ($('modLoaderCustomMirrorInput')?.value || '').trim()
        });
      });
      $('modLoaderSettingsBtn')?.addEventListener('click', () => this.openLoaderSettings());
      $('modLoaderChangelogBtn')?.addEventListener('click', () => window.VersionModule?.requestChangelog('loader'));
      // 游戏变速栏（加载器内置功能）
      $('modSpeedhackEnable')?.addEventListener('change', () => {
        const enabled = $('modSpeedhackEnable').checked;
        const label = $('modSpeedhackEnableLabel');
        if (label) label.textContent = enabled ? '变速已启用' : '变速已禁用';
        $('modSpeedhackSpeed').disabled = !enabled;
      });
      $('modSpeedhackSaveBtn')?.addEventListener('click', () => this.saveSpeedhack());
      // 高级区里的游戏目录路径：双击在资源管理器里打开
      $('modGameDirText')?.addEventListener('dblclick', () => {
        const dir = this.modStatus?.gameDirectory;
        if (dir) post({ type: 'speedhackOpenGameDir' });
      });
      // mod 列表删除/配置按钮（事件委托）
      $('modListContainer')?.addEventListener('click', e => {
        const del = e.target.closest('.mod-del-btn');
        if (del) {
          const fileName = del.dataset.file;
          const prompt = window.i18n?.translateSource(`确定删除模组「${fileName}」？`) || `确定删除模组「${fileName}」？`;
          if (fileName && confirm(prompt)) {
            post({ type: 'modDelete', fileName });
          }
          return;
        }
        const cfg = e.target.closest('.mod-config-btn');
        if (cfg) {
          const fileName = cfg.dataset.configFile;
          if (fileName) this.openModConfig(fileName);
          return;
        }
      });
      // mod 启用/禁用开关（change 事件）
      $('modListContainer')?.addEventListener('change', e => {
        const toggle = e.target.closest('.mod-toggle input[data-toggle-file]');
        if (toggle) {
          const fileName = toggle.dataset.toggleFile;
          if (fileName) post({ type: 'modToggle', fileName, enabled: toggle.checked });
        }
      });
      const searchInput = $('modSearchInput');
      if (searchInput) {
        const onSearch = () => {
          this.searchQuery = (searchInput.value || '').trim().toLowerCase();
          this.renderFilteredMods();
        };
        searchInput.addEventListener('input', onSearch);
        searchInput.addEventListener('search', onSearch);
        searchInput.addEventListener('keydown', e => {
          if (e.key === 'Escape') {
            searchInput.value = '';
            this.searchQuery = '';
            this.renderFilteredMods();
            searchInput.blur();
          } else if (e.key === 'Enter') {
            e.preventDefault();
          }
        });
      }
      window.addEventListener('appLanguageChanged', () => {
        if (this.modStatus) this.renderFilteredMods();
      });
      if (AppState.librarySettings) {
        this.applyLibrarySettings(AppState.librarySettings);
      }
    },
    saveMirrorSetting() {
      const mirrorType = $('modLoaderMirrorSelect')?.value || 'gh-proxy';
      const customUrl = ($('modLoaderCustomMirrorInput')?.value || '').trim();
      if (AppState.librarySettings) {
        AppState.librarySettings.githubMirrorType = mirrorType;
        AppState.librarySettings.customGitHubMirrorUrl = customUrl;
      }
      post({
        type: 'librarySaveSettings',
        githubMirrorType: mirrorType,
        customGitHubMirrorUrl: customUrl
      });
    },
    applyLibrarySettings(settings) {
      if (!settings) return;
      const select = $('modLoaderMirrorSelect');
      if (select && settings.githubMirrorType) {
        select.value = settings.githubMirrorType;
        $('modLoaderCustomMirrorInput')?.classList.toggle('hidden', select.value !== 'custom');
      }
      const custom = $('modLoaderCustomMirrorInput');
      if (custom && settings.customGitHubMirrorUrl !== undefined) {
        custom.value = settings.customGitHubMirrorUrl || '';
      }
    },

    // ---------- 状态 ----------
  });
})();
