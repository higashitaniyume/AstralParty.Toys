// mods.js - 模组管理页（加载器安装/更新 + 模组列表 + 变速 + 高级排障）
//
// 三条设计前提：
//   1. 用户不会手改 JSON —— 所有可配置项都在界面里可视化编辑；
//   2. **版本信息是第一等公民**：这是一个 mod 管理器，用户来这里多半就是为了确认
//      「装的是哪一版 / 要不要更新」。所以「已安装 / 随程序内置 / GitHub 最新」三栏常驻页面顶部，
//      下面再给一句人话结论；版本号只认安装清单，读不到就说"未知"，不猜、不用别的字段顶替。
//   3. 去繁就简：常用操作（安装、检查更新、导入模组、开关模组）在前，
//      破坏性与排障项（覆盖 / 降级 / 强制卸载 / SDK 依赖 / 原理说明）收进「高级与排障」，默认折叠。
(function () {
  // ---------- 版本号比较（数字点分；认不出的段按 0 处理） ----------

  function parseVersion(value) {
    return String(value ?? '')
      .split('.')
      .map(part => parseInt(part, 10) || 0);
  }

  /** a > b → 1；a < b → -1；相等 / 无法比较 → 0。 */
  function compareVersion(a, b) {
    const left = parseVersion(a);
    const right = parseVersion(b);
    const length = Math.max(left.length, right.length);
    for (let i = 0; i < length; i++) {
      const l = left[i] || 0;
      const r = right[i] || 0;
      if (l !== r) return l > r ? 1 : -1;
    }
    return 0;
  }

  /** 写一栏版本信息：值 + 说明 + 状态色（状态同时有文字说明，不靠颜色单独表意）。 */
  function setVersionCell(cellId, valueId, noteId, options) {
    const valueEl = $(valueId);
    if (valueEl) valueEl.textContent = options.value;
    const noteEl = noteId ? $(noteId) : null;
    if (noteEl && options.note !== undefined) noteEl.textContent = options.note;
    const cell = $(cellId);
    if (!cell) return;
    cell.classList.remove('is-current', 'is-update', 'is-conflict', 'is-unknown');
    if (options.state) cell.classList.add(options.state);
  }

  const ModsModule = {
    modStatus: null,
    searchQuery: '',
    // 联网检查更新的结果（跨页面切换保留；state: idle | checking | done）
    updateInfo: { state: 'idle', latest: '', error: '' },
    builtInMods: [],
    // 正在进行的耗时操作（安装/卸载/检查/更新）：按钮转圈 + 锁住，避免重复点击
    busy: null,
    busyTimer: null,

    // ---------- 生命周期 ----------

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

    openUninstallConfirm() {
      openModal('卸载加载器', `
        <div class="cfg-form">
          <p class="cfg-hint">卸载将删除加载器及 AstralParty_ModLoader 目录，包括模组、配置和日志。此操作无法通过工具撤销。</p>
          <div class="cfg-actions">
            <button class="secondary-btn" id="modUninstallCancel">取消</button>
            <button class="danger-btn" id="modUninstallConfirm">确认卸载</button>
          </div>
        </div>`, 'settings');
      $('modUninstallCancel')?.addEventListener('click', closeModal);
      $('modUninstallConfirm')?.addEventListener('click', () => {
        closeModal();
        this.setBusy('modUninstallBtn', '卸载中…');
        post({ type: 'modUninstall', force: $('modForceUninstallCheck')?.checked === true });
      });
    },

    openPackageImport() {
      if (this.packageImportPending) { toast('发布包正在导入，请稍候。'); return; }
      const html = `
        <div class="cfg-form">
          <p class="cfg-hint">加载器发布 ZIP 将一起安装加载器、SDK、bootstrap 和包内模组，保留已有加载器配置。仅使用可信的官方发布文件，重启游戏后生效。</p>
          <label class="cfg-row">
            <span class="cfg-label">发布包完整路径</span>
            <input type="text" id="modPackagePath" placeholder="选择发布包或粘贴完整路径" autocomplete="off">
          </label>
          <div class="cfg-actions"><button class="secondary-btn" id="modPackageBrowse">选择文件…</button></div>
          <p class="cfg-hint">选择文件后，点击「确认并导入」才会安装。SDK 工具包或单独 SDK DLL 仅更新 SDK。</p>
          <label class="util-check-row"><input type="checkbox" id="modPackageOverwrite">允许覆盖不属于本工具的 version.dll</label>
          <label class="util-check-row"><input type="checkbox" id="modPackageDowngrade">允许安装较旧版本（降级）</label>
          <div class="cfg-hint" id="modPackageResult" role="status" aria-live="polite"></div>
          <div class="cfg-actions">
            <button class="secondary-btn" id="modPackageCancel">取消</button>
            <button class="primary-btn" id="modPackageSubmit">确认并导入</button>
          </div>
        </div>`;
      openModal('导入发布包', html, 'settings');
      $('modPackageOverwrite').checked = $('modUpdateOverwriteCheck')?.checked === true;
      $('modPackageDowngrade').checked = $('modUpdateDowngradeCheck')?.checked === true;
      $('modPackageCancel')?.addEventListener('click', closeModal);
      $('modPackageBrowse')?.addEventListener('click', () => post({ type: 'modPickPackageFile' }));
      $('modPackageSubmit')?.addEventListener('click', () => {
        const path = $('modPackagePath').value.trim().replace(/^"|"$/g, '');
        if (!path) {
          $('modPackageResult').textContent = '请选择发布包或粘贴完整路径。';
          $('modPackagePath').focus();
          return;
        }
        this.packageImportPending = true;
        $('modPackageBrowse').disabled = true;
        $('modPackagePath').readOnly = true;
        $('modPackageSubmit').disabled = true;
        $('modPackageSubmit').textContent = '导入中…';
        $('modPackageResult').textContent = '正在校验并安装发布包…';
        post({ type: 'modImportPackage', path,
          overwriteDll: $('modPackageOverwrite').checked,
          allowDowngrade: $('modPackageDowngrade').checked });
      });
      $('modPackagePath')?.focus();
    },

    applyPackageFile(payload) {
      const input = $('modPackagePath');
      if (input && payload?.path && !this.packageImportPending) {
        input.value = payload.path;
        $('modPackageResult').textContent = '文件已选择，确认上方安装说明后点击「确认并导入」。';
      }
    },

    renderPackageImportResult(payload) {
      this.packageImportPending = false;
      if ($('modPackageBrowse')) $('modPackageBrowse').disabled = false;
      if ($('modPackagePath')) $('modPackagePath').readOnly = false;
      const result = $('modPackageResult');
      if (result) result.textContent = payload?.message || '导入结束。';
      const button = $('modPackageSubmit');
      if (button) {
        button.disabled = false;
        button.textContent = payload?.success ? '再次导入' : '重试导入';
      }
      const cancel = $('modPackageCancel');
      if (cancel) cancel.textContent = '关闭';
    },

    /** 导入前的降级确认：包内版本比已装的旧，不能无声覆盖。（Host 只在预检发现降级时发这条消息） */
    openImportConfirm(payload) {
      const packages = payload?.packages || [];
      const rows = packages.map(pkg => {
        const mods = (pkg.mods || []).map(mod =>
          `<li><b>${esc(mod.name)}</b>：已装 <code>v${esc(mod.from)}</code> → 包内 <code>v${esc(mod.to)}</code></li>`
        ).join('');
        return `<li><b>${esc(pkg.name)}</b><ul class="cfg-list">${mods}</ul></li>`;
      }).join('');
      const html = `
        <div class="cfg-form">
          <p class="cfg-hint">下面这些模组，包里的版本比游戏目录里已装的更旧：</p>
          <ul class="cfg-list">${rows}</ul>
          <p class="cfg-hint">继续导入会用包里的旧版本覆盖现有文件。玩家改过的 <code>config.json</code> 与「已禁用」状态仍会保留，
          被替换掉的旧文件会移入回收站（回收站不可用时保留原文件）。如果只是想升级，请改用更新的模组包。</p>
          <div class="cfg-actions">
            <button class="secondary-btn" id="modImportDowngradeCancel">取消</button>
            <button class="danger-btn" id="modImportDowngradeConfirm">仍然导入</button>
          </div>
        </div>`;
      openModal('检测到版本降级', html, 'warning');
      const modal = $('globalModal');
      // 无论点按钮还是右上角 X 关闭，都要给宿主一个答复，否则它会一直等在这个确认上
      const finish = allow => {
        if (modal) modal._onClose = null;
        post({ type: 'modImportConfirmAnswer', allow });
        closeModal();
      };
      $('modImportDowngradeCancel')?.addEventListener('click', () => finish(false));
      $('modImportDowngradeConfirm')?.addEventListener('click', () => finish(true));
      if (modal) modal._onClose = () => post({ type: 'modImportConfirmAnswer', allow: false });
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
        this.updateInfo = { state: 'checking', latest: this.updateInfo.latest, error: '' };
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

    refreshModStatus() {
      post({ type: 'modStatus' });
    },

    /** 已知的「GitHub 最新版本」是否比已装版本更新；没有最新信息时返回 ''。 */
    newerVersion(installedVersion) {
      const latest = this.updateInfo.latest || '';
      if (!latest || !installedVersion) return '';
      return compareVersion(latest, installedVersion) > 0 ? latest : '';
    },

    renderModStatus(payload) {
      if (!payload) return;
      // 兼容两种载荷：{ status, builtInMods }（当前）与直接给 status（老格式）
      const status = payload.status ?? payload;
      this.modStatus = status;
      if (Array.isArray(payload.builtInMods)) this.builtInMods = payload.builtInMods;

      // 恢复只由 modRecoveryDone 收尾，切换游戏时的状态消息不能提前解锁。
      if (!this.recoveryBusy) this.clearBusy();

